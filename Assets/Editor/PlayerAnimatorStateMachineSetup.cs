using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerAnimatorStateMachineSetup
{
    private const string StateParameterName = "playerState";
    private const string GeneratedControllerPath = "Assets/Generated/Player/PlayerStateMachine.controller";

    private static readonly string[] SearchClipFolders =
    {
        "Assets/Player/prefabs-player",
        "Assets/Player",
        "Assets"
    };

    private enum PlayerAnimState
    {
        Idle = 0,
        Run = 1,
        Jump = 2,
        Fall = 3,
        Defend = 4,
        Attack = 5,
        Skip = 6,
        Hit = 7,
        Dead = 8
    }

    [MenuItem("Tools/Animation/Setup Player Animator State Machine")]
    public static void SetupStateMachine()
    {
        Animator animator = ResolvePlayerAnimator();
        if (animator == null)
        {
            Debug.LogError("[AnimatorSetup] Cannot find player Animator. Select player object or tag it as 'Player'.");
            return;
        }

        AnimatorController controller = ResolveOrCreateController(animator);
        if (controller == null)
        {
            Debug.LogError("[AnimatorSetup] Failed to resolve or create AnimatorController.", animator);
            return;
        }

        Dictionary<string, AnimationClip> clips = LoadClips();
        AnimationClip idle = FindClip(clips, "idle", "waepon idle");
        if (idle == null)
        {
            idle = GetAnyClip(clips);
        }

        if (idle == null)
        {
            Debug.LogError("[AnimatorSetup] No AnimationClip found in project.", animator);
            return;
        }

        AnimationClip run = FindClip(clips, "run", "running") ?? idle;
        AnimationClip jump = FindClip(clips, "jump_up", "jump") ?? idle;
        AnimationClip fall = FindClip(clips, "fall", "jump_down", "drop") ?? jump;
        AnimationClip defend = FindClip(clips, "defend", "block") ?? idle;
        AnimationClip attack = FindClipExcluding(clips, new[] { "attack", "atk" }, "be_atk", "hit", "hurt") ?? idle;
        AnimationClip skip = FindClip(clips, "skip", "dodge", "roll") ?? idle;
        AnimationClip hit = FindClip(clips, "be_atk", "be_atked", "hit", "hurt") ?? idle;
        AnimationClip dead = FindClip(clips, "dead", "die") ?? hit;

        EnsureParameters(controller);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        Dictionary<PlayerAnimState, AnimatorState> states = new Dictionary<PlayerAnimState, AnimatorState>
        {
            { PlayerAnimState.Idle, AddState(stateMachine, "Idle", idle, new Vector3(250, 200, 0)) },
            { PlayerAnimState.Run, AddState(stateMachine, "Run", run, new Vector3(460, 200, 0)) },
            { PlayerAnimState.Jump, AddState(stateMachine, "Jump", jump, new Vector3(350, 60, 0)) },
            { PlayerAnimState.Fall, AddState(stateMachine, "Fall", fall, new Vector3(350, 340, 0)) },
            { PlayerAnimState.Defend, AddState(stateMachine, "Defend", defend, new Vector3(250, 430, 0)) },
            { PlayerAnimState.Attack, AddState(stateMachine, "Attack", attack, new Vector3(680, 70, 0)) },
            { PlayerAnimState.Skip, AddState(stateMachine, "Skip", skip, new Vector3(680, 200, 0)) },
            { PlayerAnimState.Hit, AddState(stateMachine, "Hit", hit, new Vector3(680, 330, 0)) },
            { PlayerAnimState.Dead, AddState(stateMachine, "Dead", dead, new Vector3(680, 460, 0)) }
        };

        stateMachine.defaultState = states[PlayerAnimState.Idle];

        foreach (KeyValuePair<PlayerAnimState, AnimatorState> from in states)
        {
            foreach (KeyValuePair<PlayerAnimState, AnimatorState> to in states)
            {
                if (from.Key == to.Key)
                {
                    continue;
                }

                AddStateEqualsTransition(from.Value, to.Value, (int)to.Key);
            }
        }

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(animator);
        if (animator.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AnimatorSetup] Rebuilt player state machine on controller '{controller.name}'.", animator);
    }

    [MenuItem("Tools/Animation/Repair Player Animator Graph")]
    public static void RepairAnimatorGraph()
    {
        SetupStateMachine();
    }

    private static Animator ResolvePlayerAnimator()
    {
        if (Selection.activeGameObject != null)
        {
            Animator selectedAnimator = Selection.activeGameObject.GetComponentInParent<Animator>();
            if (selectedAnimator != null)
            {
                return selectedAnimator;
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
            Animator taggedAnimator = tagged.GetComponent<Animator>();
            if (taggedAnimator != null)
            {
                return taggedAnimator;
            }

            taggedAnimator = tagged.GetComponentInChildren<Animator>(true);
            if (taggedAnimator != null)
            {
                return taggedAnimator;
            }
        }

        Animator[] allAnimators = UnityEngine.Object.FindObjectsOfType<Animator>(true);
        for (int i = 0; i < allAnimators.Length; i++)
        {
            if (allAnimators[i] == null)
            {
                continue;
            }

            if (allAnimators[i].GetComponent<PlayerController2D>() != null || allAnimators[i].GetComponentInParent<PlayerController2D>() != null)
            {
                return allAnimators[i];
            }
        }

        return null;
    }

    private static AnimatorController ResolveOrCreateController(Animator animator)
    {
        EnsureFolderForAsset(GeneratedControllerPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratedControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(GeneratedControllerPath);
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
        }

        return controller;
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

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        for (int f = 0; f < SearchClipFolders.Length; f++)
        {
            string folder = SearchClipFolders[f];
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                if (!clips.ContainsKey(clip.name))
                {
                    clips.Add(clip.name, clip);
                }
            }

            if (clips.Count > 0)
            {
                break;
            }
        }

        return clips;
    }

    private static AnimationClip GetAnyClip(Dictionary<string, AnimationClip> clips)
    {
        foreach (KeyValuePair<string, AnimationClip> kv in clips)
        {
            return kv.Value;
        }

        return null;
    }

    private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, params string[] keys)
    {
        foreach (string key in keys)
        {
            foreach (KeyValuePair<string, AnimationClip> kv in clips)
            {
                if (kv.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return kv.Value;
                }
            }
        }

        return null;
    }

    private static AnimationClip FindClipExcluding(Dictionary<string, AnimationClip> clips, string[] includeKeys, params string[] excludes)
    {
        for (int i = 0; i < includeKeys.Length; i++)
        {
            string include = includeKeys[i];
            foreach (KeyValuePair<string, AnimationClip> kv in clips)
            {
                string lower = kv.Key.ToLowerInvariant();
                if (!lower.Contains(include))
                {
                    continue;
                }

                bool rejected = false;
                for (int e = 0; e < excludes.Length; e++)
                {
                    if (lower.Contains(excludes[e]))
                    {
                        rejected = true;
                        break;
                    }
                }

                if (!rejected)
                {
                    return kv.Value;
                }
            }
        }

        return null;
    }

    private static void EnsureParameters(AnimatorController controller)
    {
        EnsureParameter(controller, StateParameterName, AnimatorControllerParameterType.Int);

        EnsureParameter(controller, "isRunning", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isJumping", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isDefending", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "isSkipping", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "isHit", AnimatorControllerParameterType.Trigger);
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name && parameters[i].type == type)
            {
                return;
            }
        }

        controller.AddParameter(name, type);
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
        }

        AnimatorTransition[] entryTransitions = stateMachine.entryTransitions;
        for (int i = entryTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveEntryTransition(entryTransitions[i]);
        }

        ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
        for (int i = childStateMachines.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveStateMachine(childStateMachines[i].stateMachine);
        }

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveState(states[i].state);
        }
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 pos)
    {
        AnimatorState state = stateMachine.AddState(name, pos);
        state.motion = motion;
        return state;
    }

    private static void AddStateEqualsTransition(AnimatorState from, AnimatorState to, int stateValue)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.exitTime = 0f;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.AddCondition(AnimatorConditionMode.Equals, stateValue, StateParameterName);
    }

    private static void EnsureFolderForAsset(string assetPath)
    {
        int slash = assetPath.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string folder = assetPath.Substring(0, slash);
        EnsureFolder(folder);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        int slash = folderPath.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string parent = folderPath.Substring(0, slash);
        string leaf = folderPath.Substring(slash + 1);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
