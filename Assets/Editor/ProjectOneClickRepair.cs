using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectOneClickRepair
{
    [MenuItem("Tools/Repair/One Click Repair")]
    public static void Repair()
    {
        int removedMissingScripts = RemoveMissingScriptsInLoadedScenes();
        int repairedCinemachineComponents = RepairCinemachineBindings();

        try
        {
            BossBatchCreateAnimAssets.Generate();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OneClickRepair] Boss animation rebuild failed: {ex.Message}");
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[OneClickRepair] Done. Removed missing scripts: {removedMissingScripts}, " +
            $"Repaired Cinemachine components: {repairedCinemachineComponents}.");
    }

    private static int RemoveMissingScriptsInLoadedScenes()
    {
        int removed = 0;

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
                foreach (Transform t in roots[r].GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missingCount <= 0)
                    {
                        continue;
                    }

                    Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    EditorUtility.SetDirty(go);

                    removed += missingCount;
                    sceneChanged = true;
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return removed;
    }

    private static int RepairCinemachineBindings()
    {
        Type brainType = FindType("Cinemachine.CinemachineBrain");
        Type vcamType = FindType("Cinemachine.CinemachineVirtualCamera");
        Type framingType = FindType("Cinemachine.CinemachineFramingTransposer");

        if (brainType == null || vcamType == null)
        {
            Debug.LogWarning("[OneClickRepair] Cinemachine types not found. Skip Cinemachine repair.");
            return 0;
        }

        int repaired = 0;

        GameObject mainCamera = FindByNameOrTag("Main Camera", "MainCamera");
        if (mainCamera != null)
        {
            repaired += EnsureComponent(mainCamera, brainType);
        }

        GameObject cmObject = FindByNameOrTag("cm", null);
        if (cmObject == null)
        {
            cmObject = new GameObject("cm");
            repaired++;
        }

        Component vcam = cmObject.GetComponent(vcamType);
        if (vcam == null)
        {
            vcam = cmObject.AddComponent(vcamType);
            repaired++;
        }

        if (vcam != null && framingType != null)
        {
            repaired += EnsureCinemachineBodyComponent(vcam, framingType);
        }

        return repaired;
    }

    private static int EnsureComponent(GameObject go, Type type)
    {
        if (go.GetComponent(type) != null)
        {
            return 0;
        }

        go.AddComponent(type);
        EditorUtility.SetDirty(go);
        return 1;
    }

    private static int EnsureCinemachineBodyComponent(Component vcam, Type bodyType)
    {
        MethodInfo getMethod = vcam.GetType().GetMethod("GetCinemachineComponent", new[] { typeof(Type) });
        MethodInfo addMethod = vcam.GetType().GetMethod("AddCinemachineComponent", new[] { typeof(Type) });

        if (getMethod == null || addMethod == null)
        {
            return 0;
        }

        object existing = getMethod.Invoke(vcam, new object[] { bodyType });
        if (existing != null)
        {
            return 0;
        }

        addMethod.Invoke(vcam, new object[] { bodyType });
        EditorUtility.SetDirty(vcam);
        return 1;
    }

    private static GameObject FindByNameOrTag(string name, string tag)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                foreach (Transform t in roots[r].GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    bool nameMatch = !string.IsNullOrEmpty(name) && string.Equals(go.name, name, StringComparison.Ordinal);
                    bool tagMatch = !string.IsNullOrEmpty(tag) && go.CompareTag(tag);
                    if (nameMatch || tagMatch)
                    {
                        return go;
                    }
                }
            }
        }

        return null;
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type t = assemblies[i].GetType(fullName, false);
            if (t != null)
            {
                return t;
            }
        }

        return null;
    }
}
