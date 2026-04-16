using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimatorStateMachineSetup
{
    private const string ControllerPath = "Assets/Player/prefabs-player/running(896x128).controller";
    private const string ClipsFolder = "Assets/Player/prefabs-player";
    private const string SessionKey = "PlayerAnimatorStateMachineSetup.Done";

    [InitializeOnLoadMethod]
    private static void AutoSetupOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupStateMachine();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSetup] Auto setup failed: {ex.Message}");
            }
            finally
            {
                SessionState.SetBool(SessionKey, true);
            }
        };
    }

    [MenuItem("Tools/Animation/Setup Player Animator State Machine")]
    public static void SetupStateMachine()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimatorSetup] Controller not found: {ControllerPath}");
            return;
        }

        Dictionary<string, AnimationClip> clips = LoadClips();
        AnimationClip idle = GetClip(clips, "waepon idle") ?? GetAnyClip(clips);
        AnimationClip run = GetClip(clips, "running") ?? idle;
        AnimationClip jump = GetClip(clips, "jump") ?? idle;
        AnimationClip defend = GetClip(clips, "defend") ?? idle;
        AnimationClip attack = GetClip(clips, "atk") ?? idle;
        AnimationClip skip = GetClip(clips, "skip") ?? idle;
        AnimationClip hit = GetClip(clips, "be_atked") ?? idle;

        if (idle == null)
        {
            Debug.LogError("[AnimatorSetup] No animation clips found in prefabs-player folder.");
            return;
        }

        ClearParameters(controller);
        AddParameters(controller);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        ClearStateMachine(sm);

        AnimatorState idleState = sm.AddState("Idle", new Vector3(250, 200, 0));
        AnimatorState runState = sm.AddState("Run", new Vector3(520, 200, 0));
        AnimatorState jumpState = sm.AddState("Jump", new Vector3(380, 40, 0));
        AnimatorState defendState = sm.AddState("Defend", new Vector3(250, 360, 0));
        AnimatorState attackState = sm.AddState("Attack", new Vector3(700, 80, 0));
        AnimatorState skipState = sm.AddState("Skip", new Vector3(700, 200, 0));
        AnimatorState hitState = sm.AddState("Hit", new Vector3(700, 320, 0));

        idleState.motion = idle;
        runState.motion = run;
        jumpState.motion = jump;
        defendState.motion = defend;
        attackState.motion = attack;
        skipState.motion = skip;
        hitState.motion = hit;

        sm.defaultState = idleState;

        AddBoolTransition(idleState, runState, "isRunning", true, false, 0.05f);
        AddBoolTransition(runState, idleState, "isRunning", false, false, 0.05f);

        AddBoolTransition(idleState, jumpState, "isJumping", true, false, 0.02f);
        AddBoolTransition(runState, jumpState, "isJumping", true, false, 0.02f);
        AddBoolTransition(jumpState, runState, "isJumping", false, false, 0.02f, "isRunning", true);
        AddBoolTransition(jumpState, idleState, "isJumping", false, false, 0.02f, "isRunning", false);

        AddBoolTransition(idleState, defendState, "isDefending", true, false, 0.02f);
        AddBoolTransition(runState, defendState, "isDefending", true, false, 0.02f);
        AddBoolTransition(jumpState, defendState, "isDefending", true, false, 0.02f);
        AddBoolTransition(defendState, idleState, "isDefending", false, false, 0.02f);

        AddAnyStateTriggerTransition(sm, attackState, "Attack");
        AddAnyStateTriggerTransition(sm, skipState, "isSkipping");
        AddAnyStateTriggerTransition(sm, hitState, "isHit");

        AddExitToLocomotion(attackState, idleState, 0.95f);
        AddExitToLocomotion(skipState, idleState, 0.95f);
        AddExitToLocomotion(hitState, idleState, 0.95f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AnimatorSetup] Player animator state machine configured.");
    }

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipsFolder });
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
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

        return clips;
    }

    private static AnimationClip GetAnyClip(Dictionary<string, AnimationClip> clips)
    {
        return clips.Values.FirstOrDefault();
    }

    private static AnimationClip GetClip(Dictionary<string, AnimationClip> clips, string contains)
    {
        foreach (KeyValuePair<string, AnimationClip> kv in clips)
        {
            if (kv.Key.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return kv.Value;
            }
        }

        return null;
    }

    private static void ClearParameters(AnimatorController controller)
    {
        AnimatorControllerParameter[] ps = controller.parameters;
        for (int i = ps.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(ps[i]);
        }
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isDefending", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("isSkipping", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("isHit", AnimatorControllerParameterType.Trigger);
    }

    private static void ClearStateMachine(AnimatorStateMachine sm)
    {
        ChildAnimatorState[] states = sm.states;
        for (int i = states.Length - 1; i >= 0; i--)
        {
            sm.RemoveState(states[i].state);
        }

        AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--)
        {
            sm.RemoveAnyStateTransition(anyTransitions[i]);
        }

        AnimatorTransition[] entryTransitions = sm.entryTransitions;
        for (int i = entryTransitions.Length - 1; i >= 0; i--)
        {
            sm.RemoveEntryTransition(entryTransitions[i]);
        }
    }

    private static void AddBoolTransition(
        AnimatorState from,
        AnimatorState to,
        string conditionName,
        bool value,
        bool hasExitTime,
        float duration,
        string secondConditionName = null,
        bool secondValue = false)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = hasExitTime;
        t.duration = duration;
        t.hasFixedDuration = true;
        t.exitTime = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, conditionName);

        if (!string.IsNullOrEmpty(secondConditionName))
        {
            t.AddCondition(secondValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, secondConditionName);
        }
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState to, string triggerName)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0.03f;
        t.exitTime = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        t.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddExitToLocomotion(AnimatorState from, AnimatorState idleState, float exitTime)
    {
        AnimatorStateTransition t = from.AddTransition(idleState);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = 0.03f;
        t.interruptionSource = TransitionInterruptionSource.None;
    }
}
