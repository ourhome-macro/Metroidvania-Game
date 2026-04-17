using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerFeelQuickSetupTool
{
    private const string MenuPath = "Tools/Player/Repair Movement + Dodge Feel";
    private const string DefaultPlayerConfigPath = "Assets/Scripts/Data/PlayerConfig.asset";

    [MenuItem(MenuPath)]
    public static void RepairSelectedPlayerFeel()
    {
        PlayerController2D playerController = ResolvePlayerController();
        if (playerController == null)
        {
            Debug.LogError("[PlayerFeelSetup] PlayerController2D not found. Select player or tag it as 'Player'.");
            return;
        }

        GameObject playerRoot = playerController.gameObject;
        Selection.activeGameObject = playerRoot;

        PlayerStateMachineQuickSetupTool.SetupSelectedPlayer();
        PerfectDodgeQuickSetupTool.SetupSelectedPlayer();

        PlayerCombat playerCombat = playerRoot.GetComponent<PlayerCombat>();
        PlayerHealth playerHealth = playerRoot.GetComponent<PlayerHealth>();
        PerfectDodgeSystem perfectDodgeSystem = playerRoot.GetComponent<PerfectDodgeSystem>();
        PlayerStateMachine playerStateMachine = playerRoot.GetComponent<PlayerStateMachine>();
        PlayerConfigSO playerConfig = LoadPlayerConfig();
        Transform groundCheck = FindChildByName(playerRoot.transform, "GroundCheck");

        ApplyPlayerControllerBindings(playerController, playerCombat, playerConfig, groundCheck);
        ApplyPlayerCombatBindings(playerCombat, playerController, playerHealth);
        ApplyPerfectDodgeBindings(perfectDodgeSystem, playerHealth);
        ApplyPlayerStateMachineDefaults(playerStateMachine);

        EditorUtility.SetDirty(playerRoot);
        EditorSceneManager.MarkSceneDirty(playerRoot.scene);
        Debug.Log($"[PlayerFeelSetup] Repaired '{playerRoot.name}'. ConfigBound={playerConfig != null}, HealthBound={playerHealth != null}, DodgeBound={perfectDodgeSystem != null}.", playerRoot);
    }

    private static PlayerController2D ResolvePlayerController()
    {
        if (Selection.activeGameObject != null)
        {
            PlayerController2D selected = Selection.activeGameObject.GetComponentInParent<PlayerController2D>();
            if (selected != null)
            {
                return selected;
            }
        }

        GameObject taggedPlayer = null;
        try
        {
            taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            taggedPlayer = null;
        }

        if (taggedPlayer != null)
        {
            PlayerController2D taggedController = taggedPlayer.GetComponent<PlayerController2D>();
            if (taggedController != null)
            {
                return taggedController;
            }
        }

        PlayerController2D[] allControllers = Object.FindObjectsOfType<PlayerController2D>(true);
        return allControllers != null && allControllers.Length > 0 ? allControllers[0] : null;
    }

    private static PlayerConfigSO LoadPlayerConfig()
    {
        PlayerConfigSO config = AssetDatabase.LoadAssetAtPath<PlayerConfigSO>(DefaultPlayerConfigPath);
        if (config != null)
        {
            return config;
        }

        string[] guids = AssetDatabase.FindAssets("t:PlayerConfigSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            config = AssetDatabase.LoadAssetAtPath<PlayerConfigSO>(path);
            if (config != null)
            {
                return config;
            }
        }

        Debug.LogWarning("[PlayerFeelSetup] PlayerConfigSO asset not found. Controller keeps serialized fallback values.");
        return null;
    }

    private static void ApplyPlayerControllerBindings(
        PlayerController2D playerController,
        PlayerCombat playerCombat,
        PlayerConfigSO playerConfig,
        Transform groundCheck)
    {
        if (playerController == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(playerController);
        SetObjectRef(so, "playerCombat", playerCombat);
        SetObjectRef(so, "playerConfig", playerConfig);
        if (groundCheck != null)
        {
            SetObjectRef(so, "groundCheck", groundCheck);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerController);
    }

    private static void ApplyPlayerCombatBindings(
        PlayerCombat playerCombat,
        PlayerController2D playerController,
        PlayerHealth playerHealth)
    {
        if (playerCombat == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(playerCombat);
        SetObjectRef(so, "playerController", playerController);
        SetObjectRef(so, "playerHealth", playerHealth);
        SetFloat(so, "perfectParryWindow", 0.18f);
        SetFloat(so, "perfectParryInvincibleTime", 0.08f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerCombat);
    }

    private static void ApplyPerfectDodgeBindings(PerfectDodgeSystem perfectDodgeSystem, PlayerHealth playerHealth)
    {
        if (perfectDodgeSystem == null)
        {
            return;
        }

        MonoBehaviour volumeComponent = FindCompatibleVolumeComponent();

        SerializedObject so = new SerializedObject(perfectDodgeSystem);
        SetObjectRef(so, "playerHealth", playerHealth);
        if (volumeComponent != null)
        {
            SetObjectRef(so, "worldVolumeComponent", volumeComponent);
        }
        SetFloat(so, "perfectWindowSeconds", 0.2f);
        SetFloat(so, "freezeDurationSeconds", 0.04f);
        SetFloat(so, "perfectParryFreezeDurationSeconds", 0.06f);
        SetFloat(so, "visualRecoverSeconds", 0.18f);
        SetFloat(so, "freezeVolumeWeight", 1f);
        SetFloat(so, "perfectParryVolumeWeight", 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(perfectDodgeSystem);
    }

    private static void ApplyPlayerStateMachineDefaults(PlayerStateMachine playerStateMachine)
    {
        if (playerStateMachine == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(playerStateMachine);
        SetBool(so, "logStateChange", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerStateMachine);
    }

    private static MonoBehaviour FindCompatibleVolumeComponent()
    {
        MonoBehaviour[] allBehaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = allBehaviours[i];
            if (behaviour == null || behaviour.GetType() == null || string.IsNullOrEmpty(behaviour.GetType().FullName))
            {
                continue;
            }

            string fullName = behaviour.GetType().FullName;
            if (fullName == "UnityEngine.Rendering.Volume" ||
                fullName == "UnityEngine.Rendering.PostProcessing.PostProcessVolume")
            {
                return behaviour;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null && candidate != root && candidate.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }
}
