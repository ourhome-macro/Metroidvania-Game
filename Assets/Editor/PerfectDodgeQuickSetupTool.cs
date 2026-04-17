using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PerfectDodgeQuickSetupTool
{
    private const string MenuPath = "Tools/Combat/Setup Perfect Dodge On Selected Player";
    private const string DefaultProfilePath = "Assets/Settings/PerfectDodge/PerfectDodgeVolumeProfile.asset";
    private const string DefaultPpsProfilePath = "Assets/Settings/PerfectDodge/PerfectDodgePostProcessProfile.asset";

    [MenuItem(MenuPath)]
    public static void SetupSelectedPlayer()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            selected = TryFindBestPlayerObject();
            if (selected != null)
            {
                Selection.activeGameObject = selected;
            }
        }

        if (selected == null)
        {
            Debug.LogError("[PerfectDodgeSetup] No selected player object. Select player in Hierarchy or tag it as 'Player'.");
            return;
        }

        PlayerHealth playerHealth = selected.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = Undo.AddComponent<PlayerHealth>(selected);
        }

        PerfectDodgeSystem dodgeSystem = selected.GetComponent<PerfectDodgeSystem>();
        if (dodgeSystem == null)
        {
            dodgeSystem = Undo.AddComponent<PerfectDodgeSystem>(selected);
        }

        MonoBehaviour volumeComponent = FindBestVolumeComponent();
        if (volumeComponent == null)
        {
            volumeComponent = CreateVolumeObjectWithProfile();
        }

        SerializedObject so = new SerializedObject(dodgeSystem);
        so.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        if (volumeComponent != null)
        {
            so.FindProperty("worldVolumeComponent").objectReferenceValue = volumeComponent;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(dodgeSystem);
        EditorUtility.SetDirty(playerHealth);
        if (volumeComponent != null)
        {
            EditorUtility.SetDirty(volumeComponent);
            EditorUtility.SetDirty(volumeComponent.gameObject);
        }

        if (volumeComponent == null)
        {
            Debug.LogWarning("[PerfectDodgeSetup] Setup partial: PlayerHealth + PerfectDodgeSystem added, but no compatible Volume found/created. Install URP or Post Processing package and re-run tool.");
        }
        else
        {
            Debug.Log("[PerfectDodgeSetup] Setup complete: PlayerHealth + PerfectDodgeSystem + Volume binding.");
        }
    }

    private static MonoBehaviour FindBestVolumeComponent()
    {
        MonoBehaviour[] allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
        MonoBehaviour firstVolume = null;

        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = allBehaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type t = behaviour.GetType();
            if (!IsSupportedVolumeType(t))
            {
                continue;
            }

            if (firstVolume == null)
            {
                firstVolume = behaviour;
            }

            if (TryGetBoolMember(behaviour, "isGlobal", out bool isGlobal) && isGlobal)
            {
                return behaviour;
            }
        }

        return firstVolume;
    }

    private static MonoBehaviour CreateVolumeObjectWithProfile()
    {
        Type urpVolumeType = ResolveType(
            "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime",
            "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core"
        );

        if (urpVolumeType != null)
        {
            return CreateUrpVolumeObjectWithProfile(urpVolumeType);
        }

        Type postProcessVolumeType = ResolveType(
            "UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime"
        );

        if (postProcessVolumeType != null)
        {
            return CreatePpsVolumeObjectWithProfile(postProcessVolumeType);
        }

        Debug.LogWarning("[PerfectDodgeSetup] No compatible Volume type found. Install URP (Volume) or Post Processing v2 package.");
        return null;
    }

    private static MonoBehaviour CreateUrpVolumeObjectWithProfile(Type volumeType)
    {
        GameObject go = new GameObject("PerfectDodge_GlobalVolume");
        Undo.RegisterCreatedObjectUndo(go, "Create Perfect Dodge Volume");

        Component volume = Undo.AddComponent(go, volumeType);
        TrySetMember(volume, "isGlobal", true);
        TrySetMember(volume, "priority", 100f);
        TrySetMember(volume, "weight", 0f);

        ScriptableObject profile = CreateOrLoadUrpVolumeProfile();
        if (profile != null)
        {
            TrySetMember(volume, "sharedProfile", profile);
            TrySetMember(volume, "profile", profile);
        }

        return volume as MonoBehaviour;
    }

    private static MonoBehaviour CreatePpsVolumeObjectWithProfile(Type volumeType)
    {
        GameObject go = new GameObject("PerfectDodge_PostProcessVolume");
        Undo.RegisterCreatedObjectUndo(go, "Create Perfect Dodge PostProcessVolume");

        Component volume = Undo.AddComponent(go, volumeType);
        TrySetMember(volume, "isGlobal", true);
        TrySetMember(volume, "priority", 100f);
        TrySetMember(volume, "weight", 0f);

        ScriptableObject profile = CreateOrLoadPpsProfile();
        if (profile != null)
        {
            TrySetMember(volume, "sharedProfile", profile);
            TrySetMember(volume, "profile", profile);
        }

        EnsurePostProcessLayerOnCamera();
        return volume as MonoBehaviour;
    }

    private static ScriptableObject CreateOrLoadUrpVolumeProfile()
    {
        Type profileType = ResolveType(
            "UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime",
            "UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core"
        );

        if (profileType == null)
        {
            return null;
        }

        EnsureFolder("Assets/Settings");
        EnsureFolder("Assets/Settings/PerfectDodge");

        ScriptableObject profile = AssetDatabase.LoadAssetAtPath(DefaultProfilePath, profileType) as ScriptableObject;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance(profileType);
            AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        }

        TryAddUrpDeepBlueColorAdjustments(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }

    private static ScriptableObject CreateOrLoadPpsProfile()
    {
        Type profileType = ResolveType(
            "UnityEngine.Rendering.PostProcessing.PostProcessProfile, Unity.Postprocessing.Runtime"
        );

        if (profileType == null)
        {
            return null;
        }

        EnsureFolder("Assets/Settings");
        EnsureFolder("Assets/Settings/PerfectDodge");

        ScriptableObject profile = AssetDatabase.LoadAssetAtPath(DefaultPpsProfilePath, profileType) as ScriptableObject;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance(profileType);
            AssetDatabase.CreateAsset(profile, DefaultPpsProfilePath);
        }

        TryAddPpsDeepBlueColorGrading(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }

    private static void TryAddUrpDeepBlueColorAdjustments(ScriptableObject profile)
    {
        if (profile == null)
        {
            return;
        }

        Type colorAdjustmentsType = ResolveType(
            "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime"
        );

        if (colorAdjustmentsType == null)
        {
            return;
        }

        MethodInfo addMethod = profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
        if (addMethod == null)
        {
            return;
        }

        object component = addMethod.Invoke(profile, new object[] { colorAdjustmentsType, true });
        if (component == null)
        {
            return;
        }

        SetVolumeParameter(component, "colorFilter", new Color(0.06f, 0.14f, 0.42f, 1f));
        SetVolumeParameter(component, "saturation", -50f);
        SetVolumeParameter(component, "contrast", 20f);
    }

    private static void TryAddPpsDeepBlueColorGrading(ScriptableObject profile)
    {
        if (profile == null)
        {
            return;
        }

        Type colorGradingType = ResolveType(
            "UnityEngine.Rendering.PostProcessing.ColorGrading, Unity.Postprocessing.Runtime"
        );
        Type effectSettingsType = ResolveType(
            "UnityEngine.Rendering.PostProcessing.PostProcessEffectSettings, Unity.Postprocessing.Runtime"
        );

        if (colorGradingType == null || effectSettingsType == null)
        {
            return;
        }

        MethodInfo hasSettingsMethod = profile.GetType().GetMethod("HasSettings", new[] { typeof(Type) });
        if (hasSettingsMethod != null)
        {
            object has = hasSettingsMethod.Invoke(profile, new object[] { colorGradingType });
            if (has is bool exists && exists)
            {
                return;
            }
        }

        ScriptableObject colorGrading = ScriptableObject.CreateInstance(colorGradingType);
        if (colorGrading == null)
        {
            return;
        }

        SetVolumeParameter(colorGrading, "colorFilter", new Color(0.06f, 0.14f, 0.42f, 1f));
        SetVolumeParameter(colorGrading, "saturation", -50f);
        SetVolumeParameter(colorGrading, "contrast", 20f);

        MethodInfo addSettingsMethod = profile.GetType().GetMethod("AddSettings", new[] { effectSettingsType });
        if (addSettingsMethod != null)
        {
            addSettingsMethod.Invoke(profile, new object[] { colorGrading });
        }
    }

    private static void SetVolumeParameter(object volumeComponent, string parameterFieldName, object value)
    {
        if (volumeComponent == null)
        {
            return;
        }

        FieldInfo field = volumeComponent.GetType().GetField(parameterFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            return;
        }

        object parameter = field.GetValue(volumeComponent);
        if (parameter == null)
        {
            return;
        }

        TrySetMember(parameter, "overrideState", true);
        TrySetMember(parameter, "value", value);
    }

    private static Type ResolveType(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            Type t = Type.GetType(candidates[i]);
            if (t != null)
            {
                return t;
            }
        }

        return null;
    }

    private static bool IsSupportedVolumeType(Type t)
    {
        if (t == null || string.IsNullOrEmpty(t.FullName))
        {
            return false;
        }

        return string.Equals(t.FullName, "UnityEngine.Rendering.Volume", StringComparison.Ordinal)
            || string.Equals(t.FullName, "UnityEngine.Rendering.PostProcessing.PostProcessVolume", StringComparison.Ordinal);
    }

    private static GameObject TryFindBestPlayerObject()
    {
        GameObject byTag = null;
        try
        {
            byTag = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            byTag = null;
        }

        if (byTag != null)
        {
            return byTag;
        }

        PlayerCombat[] combats = UnityEngine.Object.FindObjectsOfType<PlayerCombat>(true);
        if (combats != null && combats.Length == 1 && combats[0] != null)
        {
            return combats[0].gameObject;
        }

        PerfectDodgeSystem[] existing = UnityEngine.Object.FindObjectsOfType<PerfectDodgeSystem>(true);
        if (existing != null && existing.Length == 1 && existing[0] != null)
        {
            return existing[0].gameObject;
        }

        return null;
    }

    private static void EnsurePostProcessLayerOnCamera()
    {
        Type ppLayerType = ResolveType(
            "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime"
        );
        if (ppLayerType == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] cams = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            if (cams != null && cams.Length > 0)
            {
                cam = cams[0];
            }
        }

        if (cam == null)
        {
            return;
        }

        Component layer = cam.GetComponent(ppLayerType);
        if (layer == null)
        {
            layer = Undo.AddComponent(cam.gameObject, ppLayerType);
        }

        TrySetMember(layer, "volumeLayer", -1);
    }

    private static bool TryGetBoolMember(object target, string name, out bool value)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        Type t = target.GetType();
        PropertyInfo prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanRead && prop.PropertyType == typeof(bool))
        {
            object v = prop.GetValue(target, null);
            if (v is bool b)
            {
                value = b;
                return true;
            }
        }

        FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(bool))
        {
            object v = field.GetValue(target);
            if (v is bool b)
            {
                value = b;
                return true;
            }
        }

        return false;
    }

    private static void TrySetMember(object target, string name, object value)
    {
        if (target == null)
        {
            return;
        }

        Type t = target.GetType();
        PropertyInfo prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value, null);
            return;
        }

        FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
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
