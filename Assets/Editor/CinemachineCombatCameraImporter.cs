using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CinemachineCombatCameraImporter
{
    [MenuItem("Tools/Cinemachine/Import Combat Camera Rig")]
    public static void ImportCombatCameraRig()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[CinemachineImporter] No valid loaded scene.");
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject cameraGo = FindByName("Main Camera");
            if (cameraGo != null)
            {
                mainCam = cameraGo.GetComponent<Camera>();
            }
        }

        if (mainCam == null)
        {
            Debug.LogError("[CinemachineImporter] Main Camera not found.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[CinemachineImporter] Player(tag=Player) not found.");
            return;
        }

        CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
        }

        CinemachineBlenderSettings blendSettings = EnsureCombatBlendAsset();
        brain.m_CustomBlends = blendSettings;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.18f);

        GameObject rigRoot = FindByName("CM_CombatRig");
        if (rigRoot == null)
        {
            rigRoot = new GameObject("CM_CombatRig");
            Undo.RegisterCreatedObjectUndo(rigRoot, "Create CM Combat Rig");
        }

        CinemachineTargetGroup targetGroup = EnsureTargetGroup(rigRoot.transform, player.transform);

        CinemachineVirtualCamera normal = EnsureVcam(rigRoot.transform, "VCam_Normal", player.transform, player.transform, 10);
        CinemachineVirtualCamera dash = EnsureVcam(rigRoot.transform, "VCam_Dash", player.transform, player.transform, 20);
        CinemachineVirtualCamera bossUlt = EnsureVcam(rigRoot.transform, "VCam_BossUlt", targetGroup.transform, targetGroup.transform, 30);

        ConfigureFraming(normal, 0.18f, 0.14f, 0.6f);
        ConfigureFraming(dash, 0.12f, 0.1f, 0.62f);
        ConfigureFraming(bossUlt, 0.2f, 0.16f, 0.58f);

        ConfigureLens(normal, 60f, 5f);
        ConfigureLens(dash, 78f, 4.5f);
        ConfigureLens(bossUlt, 52f, 6f);

        Collider2D bounds2D = FindCameraBounds2D();
        if (bounds2D != null)
        {
            ConfigureConfiner2D(normal, bounds2D);
            ConfigureConfiner2D(dash, bounds2D);
            ConfigureConfiner2D(bossUlt, bounds2D);
        }
        else
        {
            Debug.LogWarning("[CinemachineImporter] Camera bounds collider not found. Confiner2D was skipped.");
        }

        CinemachineImpulseSource impulse = player.GetComponent<CinemachineImpulseSource>();
        if (impulse == null)
        {
            impulse = Undo.AddComponent<CinemachineImpulseSource>(player);
        }

        CombatCameraDirector2D director = rigRoot.GetComponent<CombatCameraDirector2D>();
        if (director == null)
        {
            director = Undo.AddComponent<CombatCameraDirector2D>(rigRoot);
        }

        director.ApplySetupFromImporter(
            normal,
            dash,
            bossUlt,
            targetGroup,
            player.transform,
            player.GetComponent<PlayerController2D>(),
            impulse);

        director.InvalidateConfinerCache();

        EditorUtility.SetDirty(mainCam.gameObject);
        EditorUtility.SetDirty(rigRoot);
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[CinemachineImporter] Combat camera rig ready: Normal / Dash / BossUlt.");
    }

    private static CinemachineBlenderSettings EnsureCombatBlendAsset()
    {
        const string folderRoot = "Assets/Settings";
        const string folder = "Assets/Settings/Cinemachine";
        const string assetPath = "Assets/Settings/Cinemachine/CM_CombatBlends.asset";

        if (!AssetDatabase.IsValidFolder(folderRoot))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(folderRoot, "Cinemachine");
        }

        CinemachineBlenderSettings settings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(assetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            AssetDatabase.CreateAsset(settings, assetPath);
        }

        settings.m_CustomBlends = new[]
        {
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = "VCam_Normal",
                m_To = "VCam_Dash",
                m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.12f)
            },
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = "VCam_Dash",
                m_To = "VCam_Normal",
                m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.2f)
            },
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = "VCam_Normal",
                m_To = "VCam_BossUlt",
                m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.35f)
            },
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = "VCam_BossUlt",
                m_To = "VCam_Normal",
                m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.28f)
            }
        };

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static CinemachineTargetGroup EnsureTargetGroup(Transform parent, Transform player)
    {
        Transform existing = parent.Find("CM_BossUltTargetGroup");
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("CM_BossUltTargetGroup");
            Undo.RegisterCreatedObjectUndo(go, "Create BossUlt TargetGroup");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
        }
        else
        {
            go = existing.gameObject;
        }

        CinemachineTargetGroup group = go.GetComponent<CinemachineTargetGroup>();
        if (group == null)
        {
            group = Undo.AddComponent<CinemachineTargetGroup>(go);
        }

        group.m_Targets = new[]
        {
            new CinemachineTargetGroup.Target
            {
                target = player,
                weight = 1f,
                radius = 2f
            }
        };

        return group;
    }

    private static CinemachineVirtualCamera EnsureVcam(Transform parent, string name, Transform follow, Transform lookAt, int priority)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create VCam");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
        }
        else
        {
            go = existing.gameObject;
        }

        CinemachineVirtualCamera vcam = go.GetComponent<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            vcam = Undo.AddComponent<CinemachineVirtualCamera>(go);
        }

        vcam.Follow = follow;
        vcam.LookAt = lookAt;
        vcam.Priority = priority;

        return vcam;
    }

    private static void ConfigureFraming(CinemachineVirtualCamera vcam, float xDamping, float yDamping, float screenY)
    {
        if (vcam == null)
        {
            return;
        }

        CinemachineFramingTransposer framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing == null)
        {
            framing = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
        }

        framing.m_XDamping = Mathf.Max(0f, xDamping);
        framing.m_YDamping = Mathf.Max(0f, yDamping);
        framing.m_DeadZoneWidth = 0.08f;
        framing.m_DeadZoneHeight = 0.05f;
        framing.m_ScreenY = Mathf.Clamp01(screenY);
    }

    private static void ConfigureLens(CinemachineVirtualCamera vcam, float fov, float orthoSize)
    {
        if (vcam == null)
        {
            return;
        }

        LensSettings lens = vcam.m_Lens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Max(0.1f, orthoSize);
        }
        else
        {
            lens.FieldOfView = Mathf.Clamp(fov, 1f, 179f);
        }

        vcam.m_Lens = lens;
    }

    private static void ConfigureConfiner2D(CinemachineVirtualCamera vcam, Collider2D bounds)
    {
        if (vcam == null || bounds == null)
        {
            return;
        }

        CinemachineConfiner2D confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
        }

        confiner.m_BoundingShape2D = bounds;
        confiner.InvalidateCache();
    }

    private static Collider2D FindCameraBounds2D()
    {
        Collider2D[] colliders = Object.FindObjectsOfType<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            string n = colliders[i].name.ToLowerInvariant();
            if (n.Contains("camera") && (n.Contains("bound") || n.Contains("confine")))
            {
                return colliders[i];
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col is PolygonCollider2D || col is CompositeCollider2D)
            {
                return col;
            }
        }

        return null;
    }

    private static GameObject FindByName(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }

            Transform child = roots[i].transform.Find(objectName);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
