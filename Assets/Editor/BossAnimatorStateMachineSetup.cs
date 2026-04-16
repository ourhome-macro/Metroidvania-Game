using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BossAnimatorStateMachineSetup
{
    private const string ControllerPath = "Assets/Boss/Boss.controller";
    private const string ClipsFolder = "Assets/Boss";
    private const string SessionKey = "BossAnimatorStateMachineSetup.Done";

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
                Debug.LogError($"[BossAnimatorSetup] Auto setup failed: {ex.Message}");
            }
            finally
            {
                SessionState.SetBool(SessionKey, true);
            }
        };
    }

    [MenuItem("Tools/Animation/Setup Boss Animator State Machine")]
    public static void SetupStateMachine()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BossAnimatorSetup] Failed to create controller: {ControllerPath}");
                return;
            }
        }

        Dictionary<string, AnimationClip> clips = LoadClips();
        AnimationClip walk = GetClip(clips, "boss_walk") ?? GetClip(clips, "walk") ?? GetAnyClip(clips);
        AnimationClip attack = GetClip(clips, "boss_attack") ?? GetClip(clips, "attack") ?? walk;
        AnimationClip slam = GetClip(clips, "ground_slam") ?? GetClip(clips, "slam") ?? walk;
        AnimationClip death = GetClip(clips, "boss_death") ?? GetClip(clips, "death") ?? walk;

        if (walk == null)
        {
            Debug.LogError("[BossAnimatorSetup] No animation clips found in Boss folder.");
            return;
        }

        EnsureClipLoop(walk, true);
        EnsureClipLoop(attack, false);
        EnsureClipLoop(slam, false);
        EnsureClipLoop(death, false);
        EnsureWalkUsesRunSegment(walk, 8, 16);

        ClearParameters(controller);
        AddParameters(controller);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        ClearStateMachine(sm);

        AnimatorState walkState = sm.AddState("Walk", new Vector3(300, 190, 0));
        AnimatorState attackState = sm.AddState("Attack", new Vector3(580, 80, 0));
        AnimatorState slamState = sm.AddState("GroundSlam", new Vector3(580, 190, 0));
        AnimatorState deathState = sm.AddState("Death", new Vector3(580, 300, 0));

        walkState.motion = walk;
        attackState.motion = attack;
        slamState.motion = slam;
        deathState.motion = death;

        sm.defaultState = walkState;

        AddAnyStateTriggerTransition(sm, attackState, "Attack");
        AddAnyStateTriggerTransition(sm, slamState, "GroundSlam");
        AddAnyStateTriggerTransition(sm, deathState, "Die");
        AddBoolTransition(walkState, deathState, "isDead", true, false, 0.02f);
        AddBoolTransition(attackState, deathState, "isDead", true, false, 0.02f);
        AddBoolTransition(slamState, deathState, "isDead", true, false, 0.02f);

        AddExitToLocomotion(attackState, walkState, 0.95f);
        AddExitToLocomotion(slamState, walkState, 0.95f);

        BindControllerToBossObjects(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BossAnimatorSetup] Boss animator state machine configured.");
    }

    private static void BindControllerToBossObjects(AnimatorController controller)
    {
        int matchedBosses = 0;
        int updatedAnimators = 0;
        int createdVisualNodes = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < all.Length; t++)
                {
                    GameObject go = all[t].gameObject;
                    if (!go.CompareTag("Boss"))
                    {
                        continue;
                    }

                    matchedBosses++;
                    GameObject visual = GetOrCreateBossVisual(go, ref sceneChanged, ref createdVisualNodes);
                    EnsureVisualSpriteRenderer(go, visual, ref sceneChanged);
                    EnsurePixelSnap(visual, ref sceneChanged);

                    Animator animator = visual.GetComponent<Animator>();
                    if (animator == null)
                    {
                        animator = visual.AddComponent<Animator>();
                        sceneChanged = true;
                        updatedAnimators++;
                    }

                    if (animator.runtimeAnimatorController != controller)
                    {
                        animator.runtimeAnimatorController = controller;
                        EditorUtility.SetDirty(animator);
                        sceneChanged = true;
                        updatedAnimators++;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Debug.Log($"[BossAnimatorSetup] Boss binding finished. Matched Boss tag objects: {matchedBosses}, Created BossVisual: {createdVisualNodes}, Updated animators: {updatedAnimators}");
    }

    private static GameObject GetOrCreateBossVisual(GameObject bossRoot, ref bool sceneChanged, ref int createdVisualNodes)
    {
        Transform visualTransform = bossRoot.transform.Find("BossVisual");
        if (visualTransform != null)
        {
            return visualTransform.gameObject;
        }

        GameObject visual = new GameObject("BossVisual");
        visual.transform.SetParent(bossRoot.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        sceneChanged = true;
        createdVisualNodes++;
        return visual;
    }

    private static void EnsureVisualSpriteRenderer(GameObject bossRoot, GameObject visual, ref bool sceneChanged)
    {
        SpriteRenderer rootRenderer = bossRoot.GetComponent<SpriteRenderer>();
        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();

        if (visualRenderer == null)
        {
            visualRenderer = visual.AddComponent<SpriteRenderer>();
            sceneChanged = true;
        }

        if (rootRenderer != null)
        {
            CopySpriteRenderer(rootRenderer, visualRenderer);

            if (rootRenderer.enabled)
            {
                rootRenderer.enabled = false;
                EditorUtility.SetDirty(rootRenderer);
                sceneChanged = true;
            }
        }
    }

    private static void CopySpriteRenderer(SpriteRenderer from, SpriteRenderer to)
    {
        to.sprite = from.sprite;
        to.color = from.color;
        to.flipX = from.flipX;
        to.flipY = from.flipY;
        to.drawMode = from.drawMode;
        to.size = from.size;
        to.maskInteraction = from.maskInteraction;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder;
        to.sharedMaterial = from.sharedMaterial;
        EditorUtility.SetDirty(to);
    }

    private static void EnsurePixelSnap(GameObject visual, ref bool sceneChanged)
    {
        BossVisualPixelSnap snap = visual.GetComponent<BossVisualPixelSnap>();
        if (snap == null)
        {
            visual.AddComponent<BossVisualPixelSnap>();
            sceneChanged = true;
        }
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
        controller.AddParameter("isDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("GroundSlam", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
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

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string conditionName, bool value, bool hasExitTime, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = hasExitTime;
        t.duration = duration;
        t.hasFixedDuration = true;
        t.exitTime = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, conditionName);
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState to, string triggerName)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0.03f;
        t.exitTime = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddExitToLocomotion(AnimatorState from, AnimatorState walkState, float exitTime)
    {
        AnimatorStateTransition t = from.AddTransition(walkState);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = 0.03f;
        t.interruptionSource = TransitionInterruptionSource.None;
    }

    private static void EnsureClipLoop(AnimationClip clip, bool loop)
    {
        if (clip == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(clip);
        SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
        if (settings == null)
        {
            return;
        }

        SerializedProperty loopTimeProp = settings.FindPropertyRelative("m_LoopTime");
        if (loopTimeProp == null || loopTimeProp.boolValue == loop)
        {
            return;
        }

        loopTimeProp.boolValue = loop;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureWalkUsesRunSegment(AnimationClip walkClip, int startFrame, int endFrame)
    {
        if (walkClip == null)
        {
            return;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(walkClip);
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
            {
                continue;
            }

            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(walkClip, binding);
            if (frames == null || frames.Length <= endFrame)
            {
                continue;
            }

            int length = endFrame - startFrame + 1;
            if (length <= 1)
            {
                return;
            }

            bool alreadyTrimmed = frames.Length == length;
            if (alreadyTrimmed)
            {
                bool sequenceMatch = true;
                for (int f = 0; f < length; f++)
                {
                    float expectedTime = f / walkClip.frameRate;
                    if (Mathf.Abs(frames[f].time - expectedTime) > 0.0001f)
                    {
                        sequenceMatch = false;
                        break;
                    }
                }

                if (sequenceMatch)
                {
                    return;
                }
            }

            ObjectReferenceKeyframe[] trimmed = new ObjectReferenceKeyframe[length];
            for (int f = 0; f < length; f++)
            {
                trimmed[f] = new ObjectReferenceKeyframe
                {
                    time = f / walkClip.frameRate,
                    value = frames[startFrame + f].value
                };
            }

            AnimationUtility.SetObjectReferenceCurve(walkClip, binding, trimmed);

            SerializedObject so = new SerializedObject(walkClip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty startTimeProp = settings.FindPropertyRelative("m_StartTime");
                SerializedProperty stopTimeProp = settings.FindPropertyRelative("m_StopTime");
                if (startTimeProp != null)
                {
                    startTimeProp.floatValue = 0f;
                }

                if (stopTimeProp != null)
                {
                    stopTimeProp.floatValue = (length - 1) / walkClip.frameRate;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(walkClip);
            return;
        }
    }
}
