using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyAiAutoBinderSetup
{
    private const string SessionKey = "EnemyAiAutoBinderSetup.Done";

    [InitializeOnLoadMethod]
    private static void AutoBindOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                BindAllLoadedScenes();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnemyAIAutoBinder] Auto bind failed: {ex.Message}");
            }
            finally
            {
                SessionState.SetBool(SessionKey, true);
            }
        };
    }

    [MenuItem("Tools/AI/Auto Bind Enemy AI")]
    public static void BindAllLoadedScenes()
    {
        EnemyConfigSO defaultConfig = FindDefaultConfig();
        int matched = 0;
        int addedCombat = 0;
        int addedAi = 0;
        int addedRb = 0;
        int createdAttackPoint = 0;
        int updatedAi = 0;
        int updatedCombat = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            Transform player = FindPlayerTransform(scene);
            GameObject[] roots = scene.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < all.Length; t++)
                {
                    Transform tr = all[t];
                    if (tr == null)
                    {
                        continue;
                    }

                    GameObject go = tr.gameObject;
                    EnemyCombat combat = go.GetComponent<EnemyCombat>();
                    string tag = go.tag;
                    bool byTag = tag == "Boss" || tag == "Enemy";

                    if (combat == null && !byTag)
                    {
                        continue;
                    }

                    if (combat == null)
                    {
                        combat = go.AddComponent<EnemyCombat>();
                        addedCombat++;
                        sceneChanged = true;
                    }

                    matched++;

                    EnemyAIController2D ai = go.GetComponent<EnemyAIController2D>();
                    if (ai == null)
                    {
                        ai = go.AddComponent<EnemyAIController2D>();
                        addedAi++;
                        sceneChanged = true;
                    }

                    Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                    if (rb != null && EnsureNewlyAddedRbSafeDefaults(rb))
                    {
                        addedRb++;
                        sceneChanged = true;
                    }

                    Animator animator = FindBestAnimator(go);

                    Transform attackPoint = EnsureAttackPoint(go, combat, ref sceneChanged, ref createdAttackPoint);

                    if (BindAiSerializedFields(ai, defaultConfig, player, combat, animator))
                    {
                        updatedAi++;
                        sceneChanged = true;
                    }

                    if (BindCombatSerializedFields(combat, animator, attackPoint))
                    {
                        updatedCombat++;
                        sceneChanged = true;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Debug.Log($"[EnemyAIAutoBinder] Done. Matched targets: {matched}, Added EnemyCombat: {addedCombat}, Added EnemyAIController2D: {addedAi}, Added/Configured Rigidbody2D: {addedRb}, Created AttackPoint: {createdAttackPoint}, Updated AI refs: {updatedAi}, Updated combat refs: {updatedCombat}");
    }

    private static bool BindAiSerializedFields(EnemyAIController2D ai, EnemyConfigSO config, Transform player, EnemyCombat combat, Animator animator)
    {
        SerializedObject so = new SerializedObject(ai);
        bool changed = false;

        changed |= SetObjectFieldIfNull(so, "config", config);
        changed |= SetObjectFieldIfNull(so, "playerTarget", player);
        changed |= SetObjectField(so, "enemyCombat", combat);
        changed |= SetObjectFieldIfNull(so, "animator", animator);

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ai);
        }

        return changed;
    }

    private static bool BindCombatSerializedFields(EnemyCombat combat, Animator animator, Transform attackPoint)
    {
        SerializedObject so = new SerializedObject(combat);
        bool changed = false;

        changed |= SetBoolField(so, "useInternalAi", false);
        changed |= SetObjectFieldIfNull(so, "animator", animator);
        changed |= SetObjectFieldIfNull(so, "attackPoint", attackPoint);
        changed |= SetObjectFieldIfNull(so, "contactPoint", attackPoint);
        changed |= SetLayerMaskIfZero(so, "playerLayer", LayerMask.GetMask("Player"));

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(combat);
        }

        return changed;
    }

    private static bool SetLayerMaskIfZero(SerializedObject so, string name, int mask)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null || p.propertyType != SerializedPropertyType.LayerMask)
        {
            return false;
        }

        if (p.intValue != 0 || mask == 0)
        {
            return false;
        }

        p.intValue = mask;
        return true;
    }

    private static Transform EnsureAttackPoint(GameObject owner, EnemyCombat combat, ref bool sceneChanged, ref int created)
    {
        SerializedObject so = new SerializedObject(combat);
        SerializedProperty attackPointProp = so.FindProperty("attackPoint");
        if (attackPointProp != null && attackPointProp.objectReferenceValue is Transform existing)
        {
            return existing;
        }

        Transform point = owner.transform.Find("AttackPoint");
        if (point == null)
        {
            GameObject child = new GameObject("AttackPoint");
            point = child.transform;
            point.SetParent(owner.transform, false);
            point.localPosition = new Vector3(0.8f, 0f, 0f);
            created++;
            sceneChanged = true;
        }

        return point;
    }

    private static bool EnsureNewlyAddedRbSafeDefaults(Rigidbody2D rb)
    {
        if (rb == null)
        {
            return false;
        }

        bool changed = false;
        if (Mathf.Approximately(rb.gravityScale, 1f))
        {
            rb.gravityScale = 0f;
            changed = true;
        }

        RigidbodyConstraints2D target = rb.constraints | RigidbodyConstraints2D.FreezeRotation;
        if (rb.constraints != target)
        {
            rb.constraints = target;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(rb);
        }

        return changed;
    }

    private static bool SetObjectField(SerializedObject so, string name, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
        {
            return false;
        }

        if (p.objectReferenceValue == value)
        {
            return false;
        }

        p.objectReferenceValue = value;
        return true;
    }

    private static bool SetObjectFieldIfNull(SerializedObject so, string name, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
        {
            return false;
        }

        if (p.objectReferenceValue != null || value == null)
        {
            return false;
        }

        p.objectReferenceValue = value;
        return true;
    }

    private static bool SetBoolField(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null || p.propertyType != SerializedPropertyType.Boolean)
        {
            return false;
        }

        if (p.boolValue == value)
        {
            return false;
        }

        p.boolValue = value;
        return true;
    }

    private static EnemyConfigSO FindDefaultConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyConfigSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnemyConfigSO config = AssetDatabase.LoadAssetAtPath<EnemyConfigSO>(path);
            if (config != null)
            {
                return config;
            }
        }

        return null;
    }

    private static Animator FindBestAnimator(GameObject go)
    {
        if (go == null)
        {
            return null;
        }

        Transform bossVisual = go.transform.Find("BossVisual");
        if (bossVisual != null)
        {
            Animator visualAnimator = bossVisual.GetComponent<Animator>();
            if (visualAnimator != null)
            {
                return visualAnimator;
            }
        }

        Animator[] animators = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].gameObject != go)
            {
                return animators[i];
            }
        }

        return go.GetComponent<Animator>();
    }

    private static Transform FindPlayerTransform(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < all.Length; t++)
            {
                if (all[t] != null && all[t].CompareTag("Player"))
                {
                    return all[t];
                }
            }
        }

        return null;
    }
}
