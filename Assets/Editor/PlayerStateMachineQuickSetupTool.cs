using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerStateMachineQuickSetupTool
{
    private const string MenuPath = "Tools/Player/Setup Runtime Player State Machine";
    private const string StateParameterName = "playerState";

    [MenuItem(MenuPath)]
    public static void SetupSelectedPlayer()
    {
        PlayerController2D playerController = ResolvePlayerController();
        if (playerController == null)
        {
            Debug.LogError("[PlayerStateMachineSetup] PlayerController2D not found. Select player or tag it as 'Player'.");
            return;
        }

        GameObject playerRoot = playerController.gameObject;
        PlayerCombat playerCombat = playerRoot.GetComponent<PlayerCombat>();
        if (playerCombat == null)
        {
            Debug.LogError("[PlayerStateMachineSetup] PlayerCombat is missing on player root.");
            return;
        }

        Animator animator = playerRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = playerRoot.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            Debug.LogError("[PlayerStateMachineSetup] Animator not found on player.");
            return;
        }

        PlayerStateMachine stateMachine = playerRoot.GetComponent<PlayerStateMachine>();
        if (stateMachine == null)
        {
            stateMachine = Undo.AddComponent<PlayerStateMachine>(playerRoot);
        }

        SerializedObject stateMachineSo = new SerializedObject(stateMachine);
        SetObjectRef(stateMachineSo, "playerController", playerController);
        SetObjectRef(stateMachineSo, "playerCombat", playerCombat);
        SetObjectRef(stateMachineSo, "animator", animator);
        SetBool(stateMachineSo, "syncAnimatorIntParameter", true);
        SetString(stateMachineSo, "animatorIntParameterName", StateParameterName);
        SetBool(stateMachineSo, "logStateChange", true);
        stateMachineSo.ApplyModifiedPropertiesWithoutUndo();

        bool parameterAdded = EnsureAnimatorIntParameter(animator, StateParameterName);

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(playerRoot);
        if (parameterAdded)
        {
            AssetDatabase.SaveAssets();
        }

        Selection.activeGameObject = playerRoot;
        Debug.Log($"[PlayerStateMachineSetup] Done on '{playerRoot.name}'. SyncInt=true, Param='{StateParameterName}', AddedParam={parameterAdded}.", playerRoot);
    }

    [MenuItem("Tools/Player/Repair Runtime Player State Machine")]
    public static void Repair()
    {
        SetupSelectedPlayer();
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

        GameObject tagged = null;
        try
        {
            tagged = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            tagged = null;
        }

        if (tagged != null)
        {
            PlayerController2D taggedController = tagged.GetComponent<PlayerController2D>();
            if (taggedController != null)
            {
                return taggedController;
            }
        }

        PlayerController2D[] all = Object.FindObjectsOfType<PlayerController2D>(true);
        if (all != null && all.Length > 0)
        {
            if (all.Length > 1)
            {
                Debug.LogWarning($"[PlayerStateMachineSetup] Found {all.Length} PlayerController2D objects. Using '{all[0].name}'. Select the target player to avoid ambiguity.");
            }

            if (all[0] != null)
            {
                return all[0];
            }
        }

        return null;
    }

    private static bool EnsureAnimatorIntParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorController controller = ResolveAnimatorController(animator.runtimeAnimatorController);
        if (controller == null)
        {
            Debug.LogWarning("[PlayerStateMachineSetup] Runtime controller is not editable AnimatorController. Skip parameter injection.", animator);
            return false;
        }

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
            if (p.type == AnimatorControllerParameterType.Int && p.name == parameterName)
            {
                return false;
            }
        }

        Undo.RecordObject(controller, "Add player state int parameter");
        controller.AddParameter(parameterName, AnimatorControllerParameterType.Int);
        EditorUtility.SetDirty(controller);
        return true;
    }

    private static AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
    {
        if (runtimeController == null)
        {
            return null;
        }

        AnimatorController direct = runtimeController as AnimatorController;
        if (direct != null)
        {
            return direct;
        }

        AnimatorOverrideController overrideController = runtimeController as AnimatorOverrideController;
        if (overrideController != null)
        {
            return overrideController.runtimeAnimatorController as AnimatorController;
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

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }
}
