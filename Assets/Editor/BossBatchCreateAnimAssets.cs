using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BossBatchCreateAnimAssets
{
    private const string TargetFolder = "Assets/Boss";
    private const string ControllerPath = "Assets/Boss/Boss.controller";
    private const float DefaultFps = 12f;
    private const int UnityTextureSideLimit = 16384;
    private const float DefaultBossSpritePpu = 128f;

    [MenuItem("Tools/Animation/Generate Boss Clips And Controller")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Debug.LogError($"Folder not found: {TargetFolder}");
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TargetFolder });
        List<string> texturePaths = textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png"))
            .OrderBy(p => p, new NaturalStringComparer())
            .ToList();

        int processed = 0;
        int skipped = 0;

        HashSet<string> consumed = new HashSet<string>();
        Dictionary<string, List<string>> stripGroups = BuildStripGroups(texturePaths);

        foreach (KeyValuePair<string, List<string>> kv in stripGroups)
        {
            List<string> groupPaths = kv.Value;
            if (groupPaths == null || groupPaths.Count <= 1)
            {
                continue;
            }

            string firstPath = groupPaths[0];
            if (IsTextureOversize(firstPath, ref skipped))
            {
                continue;
            }

            bool anyOversize = false;
            for (int i = 1; i < groupPaths.Count; i++)
            {
                if (IsTextureOversize(groupPaths[i], ref skipped))
                {
                    anyOversize = true;
                }
            }

            if (anyOversize)
            {
                continue;
            }

            List<Sprite> mergedSprites = new List<Sprite>();
            for (int i = 0; i < groupPaths.Count; i++)
            {
                List<Sprite> sprites = LoadSprites(groupPaths[i]);
                mergedSprites.AddRange(sprites);
            }

            if (mergedSprites.Count == 0)
            {
                Debug.LogWarning($"Skip merged strip group '{kv.Key}': no Sprite found.");
                skipped++;
                continue;
            }

            string dir = Path.GetDirectoryName(firstPath)?.Replace('\\', '/') ?? TargetFolder;
            string clipPath = $"{dir}/{kv.Key}.anim";
            CreateOrUpdateClip(clipPath, mergedSprites);

            for (int i = 0; i < groupPaths.Count; i++)
            {
                consumed.Add(groupPaths[i]);
            }

            processed++;
            Debug.Log($"[BossAnimGen] Merged strip clip: {clipPath} (sprites={mergedSprites.Count})");
        }

        foreach (string texturePath in texturePaths)
        {
            if (consumed.Contains(texturePath))
            {
                continue;
            }

            if (IsTextureOversize(texturePath, ref skipped))
            {
                continue;
            }

            List<Sprite> sprites = LoadSprites(texturePath);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"Skip '{texturePath}': no Sprite found. Set Texture Type to Sprite.");
                skipped++;
                continue;
            }

            string dir = Path.GetDirectoryName(texturePath)?.Replace('\\', '/') ?? TargetFolder;
            string baseName = Path.GetFileNameWithoutExtension(texturePath);
            string clipPath = $"{dir}/{baseName}.anim";

            CreateOrUpdateClip(clipPath, sprites);

            processed++;
        }

        EnsureBossController();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BossAnimatorStateMachineSetup.SetupStateMachine();
        Debug.Log($"Boss animation generation finished. Processed: {processed}, Skipped: {skipped}");
    }

    private static Dictionary<string, List<string>> BuildStripGroups(List<string> texturePaths)
    {
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();

        for (int i = 0; i < texturePaths.Count; i++)
        {
            string path = texturePaths[i];
            string baseName = Path.GetFileNameWithoutExtension(path);

            if (!TryGetStripGroup(baseName, out string groupKey, out _))
            {
                continue;
            }

            if (!groups.TryGetValue(groupKey, out List<string> list))
            {
                list = new List<string>();
                groups.Add(groupKey, list);
            }

            list.Add(path);
        }

        foreach (KeyValuePair<string, List<string>> kv in groups.ToList())
        {
            kv.Value.Sort((a, b) =>
            {
                string na = Path.GetFileNameWithoutExtension(a);
                string nb = Path.GetFileNameWithoutExtension(b);
                TryGetStripGroup(na, out _, out char sa);
                TryGetStripGroup(nb, out _, out char sb);
                int c = sa.CompareTo(sb);
                if (c != 0)
                {
                    return c;
                }

                return EditorUtility.NaturalCompare(na, nb);
            });
        }

        return groups;
    }

    private static bool TryGetStripGroup(string baseName, out string groupKey, out char suffix)
    {
        groupKey = null;
        suffix = '\0';

        if (string.IsNullOrEmpty(baseName) || baseName.Length < 3)
        {
            return false;
        }

        int split = baseName.LastIndexOf('_');
        if (split <= 0 || split >= baseName.Length - 1)
        {
            return false;
        }

        string lastSegment = baseName.Substring(split + 1);
        if (lastSegment.Length != 1 || !char.IsLetter(lastSegment[0]))
        {
            return false;
        }

        suffix = char.ToLowerInvariant(lastSegment[0]);
        groupKey = baseName.Substring(0, split);
        return true;
    }

    private static bool IsTextureOversize(string texturePath, ref int skipped)
    {
        if (TryReadPngSize(texturePath, out int sourceWidth, out int sourceHeight))
        {
            if (sourceWidth > UnityTextureSideLimit || sourceHeight > UnityTextureSideLimit)
            {
                Debug.LogError(
                    $"Skip '{texturePath}': source size is {sourceWidth}x{sourceHeight}, " +
                    $"which exceeds Unity single-texture limit {UnityTextureSideLimit}. " +
                    "Please split this sheet into smaller strips or export as sequence.");
                skipped++;
                return true;
            }
        }

        return false;
    }

    private static void CreateOrUpdateClip(string clipPath, List<Sprite> sprites)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            RebuildClip(clip, sprites);
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        else
        {
            RebuildClip(clip, sprites);
            EditorUtility.SetDirty(clip);
        }
    }

    private static void EnsureBossController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }
    }

    private static void RebuildClip(AnimationClip clip, List<Sprite> sprites)
    {
        clip.frameRate = DefaultFps;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / DefaultFps,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
    }

    private static List<Sprite> LoadSprites(string texturePath)
    {
        TryNormalizeSpriteImportSettings(texturePath);

        List<Sprite> sprites = AssetDatabase
            .LoadAllAssetRepresentationsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name, new NaturalStringComparer())
            .ToList();

        if (sprites.Count > 0)
        {
            if (sprites.Count == 1 && ShouldTryAutoSliceWhenSingle(texturePath, sprites[0]))
            {
                if (TrySliceHorizontalSpriteSheet(texturePath))
                {
                    sprites = AssetDatabase
                        .LoadAllAssetRepresentationsAtPath(texturePath)
                        .OfType<Sprite>()
                        .OrderBy(s => s.name, new NaturalStringComparer())
                        .ToList();
                }
            }

            return sprites;
        }

        Sprite singleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        if (singleSprite != null)
        {
            sprites.Add(singleSprite);

            if (TrySliceHorizontalSpriteSheet(texturePath))
            {
                sprites = AssetDatabase
                    .LoadAllAssetRepresentationsAtPath(texturePath)
                    .OfType<Sprite>()
                    .OrderBy(s => s.name, new NaturalStringComparer())
                    .ToList();

                if (sprites.Count > 0)
                {
                    return sprites;
                }
            }

            return sprites;
        }

        if (TryReimportAsSingleSprite(texturePath))
        {
            singleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (singleSprite != null)
            {
                sprites.Add(singleSprite);
            }
        }

        return sprites;
    }

    private static bool ShouldTryAutoSliceWhenSingle(string texturePath, Sprite sprite)
    {
        if (sprite == null)
        {
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            return false;
        }

        Rect r = sprite.rect;
        bool coversWholeTexture = Mathf.Abs(r.width - texture.width) <= 0.01f && Mathf.Abs(r.height - texture.height) <= 0.01f;
        bool veryWide = texture.width >= texture.height * 4;
        return coversWholeTexture && veryWide;
    }

    private static bool TryReadPngSize(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 24)
                {
                    return false;
                }

                byte[] header = new byte[24];
                int read = fs.Read(header, 0, header.Length);
                if (read < 24)
                {
                    return false;
                }

                bool isPng =
                    header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71 &&
                    header[4] == 13 && header[5] == 10 && header[6] == 26 && header[7] == 10;

                if (!isPng)
                {
                    return false;
                }

                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySliceHorizontalSpriteSheet(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (importer == null || texture == null)
        {
            return false;
        }

        int width = texture.width;
        int height = texture.height;
        if (width <= height || height <= 0)
        {
            return false;
        }

        if (width % height != 0)
        {
            return false;
        }

        int frameSize = height;
        int frameCount = width / frameSize;
        if (frameCount < 2)
        {
            return false;
        }

        SpriteMetaData[] metas = new SpriteMetaData[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            metas[i] = new SpriteMetaData
            {
                name = $"Frame_{i}",
                rect = new Rect(i * frameSize, 0, frameSize, frameSize),
                pivot = new Vector2(0.5f, 0f),
                alignment = (int)SpriteAlignment.BottomCenter
            };
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
        }

        if (Mathf.Abs(importer.spritePixelsPerUnit - DefaultBossSpritePpu) > 0.01f)
        {
            importer.spritePixelsPerUnit = DefaultBossSpritePpu;
        }

#pragma warning disable 618
        importer.spritesheet = metas;
#pragma warning restore 618
        importer.SaveAndReimport();
        return true;
    }

    private static bool TryReimportAsSingleSprite(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            return false;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (Mathf.Abs(importer.spritePixelsPerUnit - DefaultBossSpritePpu) > 0.01f)
        {
            importer.spritePixelsPerUnit = DefaultBossSpritePpu;
            changed = true;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
        {
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        importer.SaveAndReimport();
        return true;
    }

    private static void TryNormalizeSpriteImportSettings(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (Mathf.Abs(importer.spritePixelsPerUnit - DefaultBossSpritePpu) > 0.01f)
        {
            importer.spritePixelsPerUnit = DefaultBossSpritePpu;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return EditorUtility.NaturalCompare(x, y);
        }
    }
}
