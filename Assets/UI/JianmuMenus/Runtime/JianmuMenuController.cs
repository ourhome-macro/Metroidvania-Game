using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(1100)]
public sealed class JianmuMenuController : MonoBehaviour
{
    private sealed class InkWisp
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 AnchorPosition;
        public float DriftX;
        public float DriftY;
        public float Speed;
        public float Phase;
        public float RotationAmplitude;
        public float Scale;
        public float Alpha;
    }

    private enum MenuMode
    {
        None,
        Opening,
        Pause
    }

    private static JianmuMenuController instance;
    private static bool openingShownThisSession;

    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly List<InkWisp> openingWisps = new List<InkWisp>();
    private readonly List<InkWisp> pauseWisps = new List<InkWisp>();

    private Canvas canvas;
    private Image openingLogoImage;
    private Image pauseLogoImage;
    private GameObject openingRoot;
    private GameObject pauseRoot;
    private Button startButton;
    private Button continueButton;
    private Button quitButton;
    private Coroutine sceneRefreshRoutine;
    private MenuMode currentMode;
    private bool menuOwnsTimeScale;
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RefreshForCurrentScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        AnimateLogos();

        if (currentMode == MenuMode.Opening)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                StartGame();
            }

            return;
        }

        if (currentMode == MenuMode.Pause)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ResumeGame();
            }

            return;
        }

        if (!openingShownThisSession)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && CanOpenPauseMenu())
        {
            ShowPauseMenu();
        }
    }

    private void BuildUi()
    {
        JianmuMenuArtFactory.SpriteSet art = JianmuMenuArtFactory.GetOrCreate();
        int uiLayer = LayerMask.NameToLayer("UI");
        gameObject.layer = uiLayer >= 0 ? uiLayer : 0;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        openingRoot = CreateRoot("OpeningRoot");
        pauseRoot = CreateRoot("PauseRoot");
        pauseRoot.SetActive(false);

        BuildOpeningScreen(openingRoot.transform, art);
        BuildPauseScreen(pauseRoot.transform, art);
    }

    private void BuildOpeningScreen(Transform parent, JianmuMenuArtFactory.SpriteSet art)
    {
        Image backdrop = CreateImage("Backdrop", parent, LoadSprite("Generated/JianmuOpeningBackdrop"));
        StretchFull(backdrop.rectTransform);
        backdrop.preserveAspect = false;

        Image veil = CreateSolidImage("Veil", parent, new Color(0.03f, 0.03f, 0.04f, 0.22f));
        StretchFull(veil.rectTransform);

        openingLogoImage = CreateImage("TitleLogo", parent, LoadSprite("Generated/JianmuTitleLogo"));
        openingLogoImage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        openingLogoImage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        openingLogoImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        openingLogoImage.rectTransform.sizeDelta = new Vector2(420f, 156f);
        openingLogoImage.rectTransform.anchoredPosition = new Vector2(0f, -86f);
        openingLogoImage.color = new Color(1f, 1f, 1f, 0.96f);

        RectTransform openingInkLayer = CreateLayerRect("OpeningInkLayer", parent);
        openingInkLayer.SetSiblingIndex(2);
        CreateInkWisps(openingInkLayer, openingWisps, art.InkBlot, 11, 0);

        startButton = CreateMenuButton(
            "StartButton",
            parent,
            LoadSprite("Generated/StartGameLabel"),
            new Vector2(220f, 54f),
            new Vector2(0f, -262f),
            StartGame);

        Image startGlow = CreateSolidImage("StartGlow", parent, new Color(0f, 1f, 0.85f, 0.06f));
        startGlow.rectTransform.sizeDelta = new Vector2(260f, 74f);
        startGlow.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        startGlow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        startGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        startGlow.rectTransform.anchoredPosition = new Vector2(0f, -262f);
        startGlow.transform.SetSiblingIndex(2);
    }

    private void BuildPauseScreen(Transform parent, JianmuMenuArtFactory.SpriteSet art)
    {
        Image dim = CreateSolidImage("Dim", parent, new Color(0f, 0f, 0f, 0.62f));
        StretchFull(dim.rectTransform);

        Image panel = CreateImage("PausePanel", parent, art.PausePanel);
        panel.type = Image.Type.Simple;
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(312f, 196f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        pauseLogoImage = CreateImage("PauseLogo", panel.transform, LoadSprite("Generated/JianmuTitleLogo"));
        pauseLogoImage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        pauseLogoImage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        pauseLogoImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        pauseLogoImage.rectTransform.sizeDelta = new Vector2(220f, 82f);
        pauseLogoImage.rectTransform.anchoredPosition = new Vector2(0f, -42f);
        pauseLogoImage.color = new Color(1f, 1f, 1f, 0.94f);

        RectTransform pauseInkLayer = CreateLayerRect("PauseInkLayer", panel.transform);
        pauseInkLayer.SetSiblingIndex(0);
        CreateInkWisps(pauseInkLayer, pauseWisps, art.InkBlot, 7, 1);

        continueButton = CreateMenuButton(
            "ContinueButton",
            panel.transform,
            LoadSprite("Generated/ContinueGameLabel"),
            new Vector2(214f, 52f),
            new Vector2(0f, -104f),
            ResumeGame);

        quitButton = CreateMenuButton(
            "QuitButton",
            panel.transform,
            LoadSprite("Generated/QuitGameLabel"),
            new Vector2(214f, 52f),
            new Vector2(0f, -160f),
            QuitGame);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (sceneRefreshRoutine != null)
        {
            StopCoroutine(sceneRefreshRoutine);
        }

        sceneRefreshRoutine = StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        RefreshForCurrentScene();
    }

    private void RefreshForCurrentScene()
    {
        HideMenusImmediate();

        if (openingShownThisSession)
        {
            return;
        }

        if (FindObjectOfType<PlayerHealth>() == null)
        {
            return;
        }

        ShowOpening();
    }

    private void ShowOpening()
    {
        currentMode = MenuMode.Opening;
        openingRoot.SetActive(true);
        pauseRoot.SetActive(false);
        ApplyMenuPause(true);
        EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
    }

    private void StartGame()
    {
        openingShownThisSession = true;
        openingRoot.SetActive(false);
        pauseRoot.SetActive(false);
        currentMode = MenuMode.None;
        ApplyMenuPause(false);
    }

    private bool CanOpenPauseMenu()
    {
        if (FindObjectOfType<PlayerHealth>() == null)
        {
            return false;
        }

        if (currentMode != MenuMode.None)
        {
            return false;
        }

        return Time.timeScale > 0.0001f;
    }

    private void ShowPauseMenu()
    {
        currentMode = MenuMode.Pause;
        pauseRoot.SetActive(true);
        ApplyMenuPause(true);
        GameEvents.GamePaused(true);
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
    }

    private void ResumeGame()
    {
        pauseRoot.SetActive(false);
        currentMode = MenuMode.None;
        ApplyMenuPause(false);
        GameEvents.GamePaused(false);
    }

    private void QuitGame()
    {
        ApplyMenuPause(false);
        GameEvents.GamePaused(false);

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HideMenusImmediate()
    {
        openingRoot.SetActive(false);
        pauseRoot.SetActive(false);
        currentMode = MenuMode.None;
        ApplyMenuPause(false);
        GameEvents.GamePaused(false);
    }

    private void ApplyMenuPause(bool paused)
    {
        if (paused)
        {
            if (!menuOwnsTimeScale)
            {
                cachedTimeScale = Time.timeScale;
                cachedFixedDeltaTime = Time.fixedDeltaTime;
                menuOwnsTimeScale = true;
            }

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            AudioListener.pause = true;
            return;
        }

        if (!menuOwnsTimeScale)
        {
            return;
        }

        Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime > 0f ? cachedFixedDeltaTime : 0.02f;
        AudioListener.pause = false;
        menuOwnsTimeScale = false;
    }

    private void AnimateLogos()
    {
        float pulse = 0.985f + Mathf.Sin(Time.unscaledTime * 1.35f) * 0.018f;
        if (openingLogoImage != null)
        {
            openingLogoImage.rectTransform.localScale = Vector3.one * pulse;
        }

        if (pauseLogoImage != null)
        {
            pauseLogoImage.rectTransform.localScale = Vector3.one * (0.99f + Mathf.Sin(Time.unscaledTime * 1.1f) * 0.012f);
        }

        AnimateInkWisps(openingWisps);
        AnimateInkWisps(pauseWisps);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemRoot = new GameObject("EventSystem", typeof(RectTransform), typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemRoot);
    }

    private GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.layer = gameObject.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        StretchFull(rect);
        return root;
    }

    private RectTransform CreateLayerRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        StretchFull(rect);
        return rect;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Image CreateSolidImage(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, null);
        image.color = color;
        return image;
    }

    private void CreateInkWisps(RectTransform layer, List<InkWisp> target, Sprite inkSprite, int count, int seedOffset)
    {
        if (inkSprite == null)
        {
            return;
        }

        System.Random random = new System.Random(16384 + seedOffset * 97);
        Vector2 layerSize = new Vector2(640f, 360f);
        bool pauseLayer = seedOffset > 0;
        if (pauseLayer)
        {
            layerSize = new Vector2(300f, 186f);
        }

        for (int i = 0; i < count; i++)
        {
            Image image = CreateImage($"InkWisp_{seedOffset}_{i}", layer, inkSprite);
            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(36f + random.Next(0, 28), 28f + random.Next(0, 22));

            float x = ((float)random.NextDouble() - 0.5f) * layerSize.x * (pauseLayer ? 0.78f : 0.92f);
            float y = ((float)random.NextDouble() - 0.5f) * layerSize.y * (pauseLayer ? 0.7f : 0.82f);
            image.rectTransform.anchoredPosition = new Vector2(x, y);

            Color tint;
            switch (i % 3)
            {
                case 0:
                    tint = new Color(0.10f, 0.09f, 0.10f, 0.34f);
                    break;
                case 1:
                    tint = new Color(0.12f, 0.11f, 0.12f, 0.26f);
                    break;
                default:
                    tint = new Color(0.18f, 0.17f, 0.18f, 0.21f);
                    break;
            }

            image.color = tint;

            InkWisp wisp = new InkWisp
            {
                Rect = image.rectTransform,
                Image = image,
                AnchorPosition = image.rectTransform.anchoredPosition,
                DriftX = 5f + (float)random.NextDouble() * (pauseLayer ? 12f : 18f),
                DriftY = 8f + (float)random.NextDouble() * (pauseLayer ? 10f : 16f),
                Speed = 0.35f + (float)random.NextDouble() * 0.7f,
                Phase = (float)random.NextDouble() * Mathf.PI * 2f,
                RotationAmplitude = 5f + (float)random.NextDouble() * 10f,
                Scale = 0.85f + (float)random.NextDouble() * 0.75f,
                Alpha = tint.a
            };

            image.rectTransform.localScale = Vector3.one * wisp.Scale;
            target.Add(wisp);
        }
    }

    private void AnimateInkWisps(List<InkWisp> wisps)
    {
        float time = Time.unscaledTime;
        for (int i = 0; i < wisps.Count; i++)
        {
            InkWisp wisp = wisps[i];
            float phase = wisp.Phase + time * wisp.Speed;
            float sin = Mathf.Sin(phase);
            float cos = Mathf.Cos(phase * 0.7f);

            wisp.Rect.anchoredPosition = wisp.AnchorPosition + new Vector2(cos * wisp.DriftX, sin * wisp.DriftY);
            wisp.Rect.localRotation = Quaternion.Euler(0f, 0f, sin * wisp.RotationAmplitude);
            wisp.Rect.localScale = Vector3.one * (wisp.Scale * (0.92f + (cos * 0.08f)));

            Color color = wisp.Image.color;
            color.a = wisp.Alpha * (0.72f + ((sin + 1f) * 0.14f));
            wisp.Image.color = color;
        }
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private Button CreateMenuButton(string name, Transform parent, Sprite labelSprite, Vector2 size, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = gameObject.layer;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image background = go.GetComponent<Image>();
        background.sprite = JianmuMenuArtFactory.GetOrCreate().Button;
        background.type = Image.Type.Simple;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 0.96f);
        colors.highlightedColor = new Color(0.72f, 1f, 0.96f, 1f);
        colors.pressedColor = new Color(1f, 0.72f, 0.94f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.44f, 0.44f, 0.44f, 0.8f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);

        Image label = CreateImage("Label", rect, labelSprite);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(Mathf.Min(size.x - 28f, 176f), 46f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.preserveAspect = true;

        return button;
    }

    private Sprite LoadSprite(string resourcePath)
    {
        if (spriteCache.TryGetValue(resourcePath, out Sprite cached))
        {
            return cached;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"Jianmu menu asset not found at Resources/{resourcePath}", this);
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = texture.name + "_RuntimeSprite";
        spriteCache[resourcePath] = sprite;
        return sprite;
    }
}
