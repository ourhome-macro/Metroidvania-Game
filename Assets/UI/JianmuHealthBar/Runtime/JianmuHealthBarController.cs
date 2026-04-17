using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class JianmuHealthBarController : MonoBehaviour
{
    private const float MaxFillWidth = 148f;
    private const float FillHeight = 10f;
    private const float LerpSpeed = 6.5f;

    private static readonly Vector2[] BranchBasePositions =
    {
        new Vector2(70f, -10f),
        new Vector2(104f, -5f),
        new Vector2(138f, -9f),
        new Vector2(172f, -3f)
    };

    private CanvasGroup canvasGroup;
    private RectTransform rootRect;
    private RectTransform fillRect;
    private Image auraImage;
    private Image fillImage;
    private Image cracksImage;
    private Image treeImage;
    private Image rootImage;
    private Image[] branchImages;
    private JianmuHealthBarArtFactory.SpriteSet sprites;

    private float targetRatio = 1f;
    private float displayedRatio = 1f;
    private bool hasHealthSource;
    private PlayerHealth playerHealth;
    private Coroutine rebindRoutine;
    private static JianmuHealthBarController instance;

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
        GameEvents.OnHpChanged += HandleHpChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RebindToPlayer();
    }

    private void OnDisable()
    {
        GameEvents.OnHpChanged -= HandleHpChanged;
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
        displayedRatio = Mathf.MoveTowards(displayedRatio, targetRatio, Time.unscaledDeltaTime * LerpSpeed);
        ApplyVisualState(displayedRatio);
    }

    private void BuildUi()
    {
        sprites = JianmuHealthBarArtFactory.GetOrCreate();

        int uiLayer = LayerMask.NameToLayer("UI");
        gameObject.layer = uiLayer >= 0 ? uiLayer : 0;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 24;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        rootRect = CreateRect("JianmuRoot", transform, new Vector2(236f, 76f), new Vector2(134f, -54f));
        canvasGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        auraImage = CreateImage("Aura", rootRect, sprites.Aura, new Vector2(224f, 64f), new Vector2(112f, -34f));
        auraImage.color = new Color32(111, 194, 151, 60);

        treeImage = CreateImage("JianmuTree", rootRect, sprites.TreeStages[0], new Vector2(44f, 52f), new Vector2(24f, -34f));
        rootImage = CreateImage("RootOrnament", rootRect, sprites.Root, new Vector2(42f, 14f), new Vector2(28f, -58f));

        CreateImage("Frame", rootRect, sprites.Frame, new Vector2(156f, 18f), new Vector2(130f, -37f));

        fillRect = CreateRect("Fill", rootRect, new Vector2(MaxFillWidth, FillHeight), new Vector2(130f, -37f));
        fillImage = fillRect.gameObject.AddComponent<Image>();
        fillImage.sprite = sprites.Fill;
        fillImage.type = Image.Type.Simple;
        fillImage.raycastTarget = false;

        cracksImage = CreateImage("Cracks", rootRect, sprites.Cracks, new Vector2(MaxFillWidth, FillHeight), new Vector2(130f, -37f));
        cracksImage.color = new Color(1f, 1f, 1f, 0f);

        branchImages = new Image[BranchBasePositions.Length];
        for (int i = 0; i < branchImages.Length; i++)
        {
            branchImages[i] = CreateImage(
                "Branch_" + i,
                rootRect,
                sprites.Branch,
                new Vector2(18f, 14f),
                BranchBasePositions[i]);
        }

        ApplyVisualState(displayedRatio);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (rebindRoutine != null)
        {
            StopCoroutine(rebindRoutine);
        }

        rebindRoutine = StartCoroutine(RebindNextFrame());
    }

    private IEnumerator RebindNextFrame()
    {
        yield return null;
        RebindToPlayer();
    }

    private void RebindToPlayer()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();
        hasHealthSource = playerHealth != null;

        if (!hasHealthSource)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        SetHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth, true);
        canvasGroup.alpha = 1f;
    }

    private void HandleHpChanged(int currentHp, int maxHp)
    {
        hasHealthSource = true;
        canvasGroup.alpha = 1f;
        SetHealth(currentHp, maxHp, false);
    }

    private void SetHealth(int currentHp, int maxHp, bool snap)
    {
        targetRatio = Mathf.Clamp01(maxHp <= 0 ? 0f : (float)currentHp / maxHp);
        if (snap)
        {
            displayedRatio = targetRatio;
        }

        ApplyVisualState(displayedRatio);
    }

    private void ApplyVisualState(float ratio)
    {
        if (rootRect == null)
        {
            return;
        }

        float clampedRatio = Mathf.Clamp01(ratio);
        float wither = 1f - clampedRatio;

        int stageIndex = Mathf.Clamp(
            Mathf.RoundToInt(wither * (sprites.TreeStages.Length - 1)),
            0,
            sprites.TreeStages.Length - 1);

        treeImage.sprite = sprites.TreeStages[stageIndex];
        treeImage.color = Color.Lerp(
            new Color(0.82f, 0.66f, 0.46f, 0.95f),
            Color.white,
            Mathf.SmoothStep(0f, 1f, clampedRatio));

        rootImage.color = Color.Lerp(
            new Color(0.58f, 0.41f, 0.26f, 0.9f),
            Color.white,
            Mathf.Lerp(0.25f, 0.8f, clampedRatio));

        float fillWidth = Mathf.Round(MaxFillWidth * clampedRatio);
        if (clampedRatio > 0f && fillWidth < 4f)
        {
            fillWidth = 4f;
        }

        fillRect.sizeDelta = new Vector2(fillWidth, FillHeight);
        fillRect.gameObject.SetActive(fillWidth > 0f);

        float lowHealthPulse = 1f;
        if (clampedRatio < 0.22f && clampedRatio > 0f)
        {
            lowHealthPulse = 0.85f + Mathf.PingPong(Time.unscaledTime * 2.75f, 0.35f);
        }

        fillImage.color = Color.Lerp(
            new Color(0.74f, 0.32f, 0.22f, 0.95f),
            new Color(0.34f, 0.84f, 0.62f, 1f),
            Mathf.SmoothStep(0f, 1f, clampedRatio)) * lowHealthPulse;

        auraImage.color = Color.Lerp(
            new Color(0.48f, 0.21f, 0.12f, 0.24f),
            new Color(0.34f, 0.78f, 0.60f, 0.38f),
            clampedRatio);
        auraImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.03f, lowHealthPulse * 0.5f);

        cracksImage.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, wither) * 0.8f);

        float segmentSize = 1f / branchImages.Length;
        for (int i = 0; i < branchImages.Length; i++)
        {
            float segmentRatio = Mathf.Clamp01((clampedRatio - (i * segmentSize)) / segmentSize);
            float eased = segmentRatio * segmentRatio * (3f - (2f * segmentRatio));

            RectTransform branchRect = branchImages[i].rectTransform;
            branchRect.anchoredPosition = BranchBasePositions[i] + new Vector2(0f, Mathf.Lerp(-8f, 0f, eased));
            branchRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, -6f, eased));

            branchImages[i].color = Color.Lerp(
                new Color(0.60f, 0.39f, 0.21f, 0.75f),
                new Color(0.57f, 0.89f, 0.47f, 1f),
                eased);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredCenter)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredCenter;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 anchoredCenter)
    {
        RectTransform rect = CreateRect(name, parent, size, anchoredCenter);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }
}
