using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BatchCreateAnimAssets
{
    private const string TargetFolder = "Assets/Player/prefabs-player";
    private const float DefaultFps = 12f;

    [MenuItem("Tools/Animation/Generate Clips And Controllers")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Debug.LogError($"Folder not found: {TargetFolder}");
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TargetFolder });
        int processed = 0;
        int skipped = 0;

        foreach (string guid in textureGuids)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            if (!texturePath.EndsWith(".png"))
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
            string controllerPath = $"{dir}/{baseName}.controller";

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

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                AnimatorState state = stateMachine.AddState("Default");
                state.motion = clip;
                stateMachine.defaultState = state;
            }

            processed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PlayerAnimatorStateMachineSetup.SetupStateMachine();
        Debug.Log($"Animation generation finished. Processed: {processed}, Skipped: {skipped}");
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
        List<Sprite> sprites = AssetDatabase
            .LoadAllAssetRepresentationsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name, new NaturalStringComparer())
            .ToList();

        if (sprites.Count > 0)
        {
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
                pivot = new Vector2(0.5f, 0.5f),
                alignment = (int)SpriteAlignment.Center
            };
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            changed = true;
        }

        importer.spritesheet = metas;
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

        if (!changed)
        {
            return false;
        }

        importer.SaveAndReimport();
        return true;
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return EditorUtility.NaturalCompare(x, y);
        }
    }
}
