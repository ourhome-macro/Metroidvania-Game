using UnityEditor;
using UnityEngine;

public static class EnemyBloodFxPrefabSetup
{
    private const string PrefabFolder = "Assets/Prefabs/VFX";
    private const string PrefabPath = PrefabFolder + "/BloodHitSmall.prefab";

    [MenuItem("Tools/VFX/Create Enemy Blood Prefab")]
    public static void CreateEnemyBloodPrefab()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);

        GameObject root = new GameObject("BloodHitSmall");
        ParticleSystem ps = root.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.28f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.42f, 0.04f, 0.04f, 0.95f),
            new Color(0.58f, 0.06f, 0.06f, 0.95f)
        );
        main.gravityModifier = 0.65f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)10, (short)16, 1, 0.01f)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.02f;
        shape.arc = 22f;
        shape.rotation = new Vector3(0f, -90f, 0f);
        shape.randomDirectionAmount = 0f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.62f, 0.07f, 0.07f), 0f),
                new GradientColorKey(new Color(0.26f, 0.02f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.78f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.9f),
            new Keyframe(1f, 0.35f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var forceOverLifetime = ps.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.space = ParticleSystemSimulationSpace.World;
        forceOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
        forceOverLifetime.y = new ParticleSystem.MinMaxCurve(-8f, -5f);
        forceOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.03f;
        noise.frequency = 0.35f;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;
        Material defaultParticleMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (defaultParticleMaterial != null)
        {
            renderer.sharedMaterial = defaultParticleMaterial;
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyBloodFxPrefabSetup] Created prefab: {PrefabPath}");
    }

    [MenuItem("Tools/VFX/Create + Assign Enemy Blood Prefab")]
    public static void CreateAndAssignEnemyBloodPrefab()
    {
        CreateEnemyBloodPrefab();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[EnemyBloodFxPrefabSetup] Failed to load prefab: {PrefabPath}");
            return;
        }

        EnemyCombat[] enemies = Object.FindObjectsOfType<EnemyCombat>(true);
        int assigned = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyCombat enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(enemy);
            SerializedProperty prop = so.FindProperty("hitBloodFxPrefab");
            if (prop == null)
            {
                continue;
            }

            prop.objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
            assigned++;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int assignedInPrefabs = 0;
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabRoot == null)
            {
                continue;
            }

            EnemyCombat prefabCombat = prefabRoot.GetComponentInChildren<EnemyCombat>(true);
            if (prefabCombat == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(prefabCombat);
            SerializedProperty prop = so.FindProperty("hitBloodFxPrefab");
            if (prop == null || prop.objectReferenceValue == prefab)
            {
                continue;
            }

            prop.objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefabCombat);
            assignedInPrefabs++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[EnemyBloodFxPrefabSetup] Assigned prefab to {assigned} EnemyCombat components in opened scenes and {assignedInPrefabs} prefab assets.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string parent = path.Substring(0, slash);
        string folderName = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
