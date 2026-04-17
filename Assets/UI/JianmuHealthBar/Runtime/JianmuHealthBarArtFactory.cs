using UnityEngine;

public static class JianmuHealthBarArtFactory
{
    public sealed class SpriteSet
    {
        public Sprite Aura;
        public Sprite Frame;
        public Sprite Fill;
        public Sprite Cracks;
        public Sprite Branch;
        public Sprite Root;
        public Sprite[] TreeStages;
    }

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static SpriteSet cached;

    public static SpriteSet GetOrCreate()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = new SpriteSet
        {
            Aura = CreateAuraSprite(),
            Frame = CreateFrameSprite(),
            Fill = CreateFillSprite(),
            Cracks = CreateCrackSprite(),
            Branch = CreateBranchSprite(),
            Root = CreateRootSprite(),
            TreeStages = CreateTreeStages(6)
        };

        return cached;
    }

    private static Sprite[] CreateTreeStages(int count)
    {
        Sprite[] stages = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            stages[i] = CreateTreeStageSprite(i, count);
        }

        return stages;
    }

    private static Sprite CreateAuraSprite()
    {
        Texture2D texture = CreateTexture(224, 64);
        Vector2 center = new Vector2(112f, 32f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x - center.x) / 102f;
                float ny = (y - center.y) / 26f;
                float distance = nx * nx + ny * ny;
                if (distance > 1.12f)
                {
                    continue;
                }

                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * 0.7f;
                SetPixel(texture, x, y, new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f)));
            }
        }

        return FinalizeSprite(texture, "JianmuAura", 1f);
    }

    private static Sprite CreateFrameSprite()
    {
        Texture2D texture = CreateTexture(156, 18);
        Color32 barkOutline = new Color32(38, 24, 16, 255);
        Color32 barkDark = new Color32(73, 48, 28, 255);
        Color32 barkMid = new Color32(112, 74, 42, 255);
        Color32 barkLight = new Color32(153, 108, 61, 255);

        DrawFilledRect(texture, 0, 0, 156, 18, barkOutline);
        DrawFilledRect(texture, 1, 1, 154, 16, barkDark);
        DrawFilledRect(texture, 2, 2, 152, 14, barkMid);
        ClearRect(texture, 4, 4, 148, 10);

        for (int x = 2; x < 154; x += 9)
        {
            SetPixel(texture, x, 14, barkLight);
            SetPixel(texture, x + 1, 14, barkLight);
            SetPixel(texture, x + 2, 3, barkOutline);
        }

        int[] knotPositions = { 23, 61, 97, 131 };
        for (int i = 0; i < knotPositions.Length; i++)
        {
            int x = knotPositions[i];
            DrawFilledRect(texture, x, 2, 3, 3, barkDark);
            DrawFilledRect(texture, x + 1, 3, 1, 1, barkOutline);
            DrawFilledRect(texture, x + 2, 13, 2, 2, barkDark);
        }

        return FinalizeSprite(texture, "JianmuFrame", 1f);
    }

    private static Sprite CreateFillSprite()
    {
        Texture2D texture = CreateTexture(32, 10);
        Color32 main = new Color32(230, 230, 230, 255);
        Color32 highlight = new Color32(255, 255, 255, 255);
        Color32 shadow = new Color32(184, 184, 184, 255);

        DrawFilledRect(texture, 0, 0, 32, 10, main);
        DrawFilledRect(texture, 0, 8, 32, 2, highlight);
        DrawFilledRect(texture, 0, 0, 32, 2, shadow);

        for (int x = 2; x < 32; x += 6)
        {
            SetPixel(texture, x, 5, highlight);
            SetPixel(texture, x + 1, 4, highlight);
            SetPixel(texture, x + 2, 5, shadow);
        }

        return FinalizeSprite(texture, "JianmuFill", 1f);
    }

    private static Sprite CreateCrackSprite()
    {
        Texture2D texture = CreateTexture(148, 10);
        Color32 crack = new Color32(49, 30, 17, 165);

        DrawLine(texture, 14, 7, 33, 3, crack);
        DrawLine(texture, 33, 3, 40, 6, crack);
        DrawLine(texture, 54, 6, 73, 2, crack);
        DrawLine(texture, 73, 2, 82, 5, crack);
        DrawLine(texture, 89, 7, 111, 3, crack);
        DrawLine(texture, 111, 3, 120, 6, crack);
        DrawLine(texture, 118, 8, 140, 4, crack);

        return FinalizeSprite(texture, "JianmuCracks", 1f);
    }

    private static Sprite CreateBranchSprite()
    {
        Texture2D texture = CreateTexture(18, 14);
        Color32 branch = new Color32(226, 226, 226, 220);
        Color32 leaf = new Color32(255, 255, 255, 255);

        DrawLine(texture, 1, 6, 9, 8, branch);
        DrawLine(texture, 8, 8, 12, 11, branch);
        DrawLeafCluster(texture, 12, 9, 2, leaf, branch);
        DrawLeafCluster(texture, 9, 11, 1, leaf, branch);

        return FinalizeSprite(texture, "JianmuBranch", 1f);
    }

    private static Sprite CreateRootSprite()
    {
        Texture2D texture = CreateTexture(42, 14);
        Color32 barkDark = new Color32(57, 36, 21, 255);
        Color32 barkLight = new Color32(121, 82, 47, 255);

        DrawLine(texture, 20, 12, 18, 8, barkDark);
        DrawLine(texture, 18, 8, 12, 5, barkDark);
        DrawLine(texture, 18, 8, 24, 5, barkDark);
        DrawLine(texture, 24, 5, 32, 3, barkDark);
        DrawLine(texture, 12, 5, 4, 3, barkDark);
        DrawLine(texture, 14, 10, 10, 12, barkDark);
        DrawLine(texture, 26, 10, 34, 12, barkDark);

        DrawLine(texture, 21, 12, 19, 8, barkLight);
        DrawLine(texture, 19, 8, 13, 5, barkLight);
        DrawLine(texture, 19, 8, 25, 5, barkLight);
        DrawLine(texture, 25, 5, 33, 3, barkLight);

        return FinalizeSprite(texture, "JianmuRoots", 1f);
    }

    private static Sprite CreateTreeStageSprite(int stage, int totalStages)
    {
        float vitality = 1f - (float)stage / Mathf.Max(1, totalStages - 1);
        float wither = 1f - vitality;

        Texture2D texture = CreateTexture(44, 52);
        Color32 barkOutline = new Color32(34, 21, 14, 255);
        Color32 barkMid = Color.Lerp(new Color(0.56f, 0.37f, 0.22f, 1f), new Color(0.39f, 0.28f, 0.19f, 1f), wither);
        Color32 barkLight = Color.Lerp(new Color(0.70f, 0.49f, 0.29f, 1f), new Color(0.51f, 0.38f, 0.25f, 1f), wither);
        Color32 leafMain = Color.Lerp(new Color(0.53f, 0.34f, 0.19f, 1f), new Color(0.43f, 0.74f, 0.37f, 1f), vitality);
        Color32 leafLight = Color.Lerp(new Color(0.67f, 0.44f, 0.24f, 1f), new Color(0.69f, 0.91f, 0.56f, 1f), vitality);

        DrawFilledRect(texture, 20, 4, 3, 30, barkOutline);
        DrawFilledRect(texture, 21, 4, 1, 30, barkLight);
        DrawLine(texture, 21, 33, 16, 40, barkMid);
        DrawLine(texture, 21, 33, 27, 41, barkMid);

        DrawBranch(texture, 21, 12, 10, -1, vitality, barkOutline, barkLight);
        DrawBranch(texture, 21, 18, 9, 1, vitality, barkOutline, barkLight);
        DrawBranch(texture, 21, 25, 11, -1, vitality, barkOutline, barkLight);
        DrawBranch(texture, 21, 29, 8, 1, vitality, barkOutline, barkLight);
        DrawLine(texture, 21, 32, 21, 39, barkLight);

        int crownSize = vitality > 0.7f ? 3 : vitality > 0.35f ? 2 : 1;
        if (vitality > 0.1f)
        {
            DrawLeafCluster(texture, 12, 14 + Mathf.RoundToInt(wither * 2f), crownSize, leafMain, leafLight);
            DrawLeafCluster(texture, 31, 21 + Mathf.RoundToInt(wither * 2f), crownSize, leafMain, leafLight);
            DrawLeafCluster(texture, 9, 28 + Mathf.RoundToInt(wither * 3f), crownSize, leafMain, leafLight);
            DrawLeafCluster(texture, 30, 31 + Mathf.RoundToInt(wither * 2f), crownSize, leafMain, leafLight);
            DrawLeafCluster(texture, 21, 39, crownSize, leafMain, leafLight);
        }
        else
        {
            DrawLine(texture, 10, 29, 7, 33, barkOutline);
            DrawLine(texture, 31, 32, 35, 34, barkOutline);
            SetPixel(texture, 21, 39, barkOutline);
        }

        return FinalizeSprite(texture, "JianmuTreeStage" + stage, 1f);
    }

    private static void DrawBranch(
        Texture2D texture,
        int startX,
        int startY,
        int length,
        int direction,
        float vitality,
        Color32 barkOutline,
        Color32 barkLight)
    {
        int drop = Mathf.RoundToInt((1f - vitality) * 5f);
        int endX = startX + (length * direction);
        int endY = startY + Mathf.RoundToInt(length * 0.35f) - drop;

        DrawLine(texture, startX, startY, endX, endY, barkOutline);
        DrawLine(texture, startX, startY + 1, endX, endY + 1, barkLight);
    }

    private static void DrawLeafCluster(Texture2D texture, int x, int y, int radius, Color32 main, Color32 highlight)
    {
        int[,] offsets =
        {
            { 0, 0 },
            { -1, 0 },
            { 1, 0 },
            { 0, 1 },
            { 0, -1 },
            { -1, 1 },
            { 1, 1 }
        };

        for (int i = 0; i < offsets.GetLength(0); i++)
        {
            int px = x + offsets[i, 0] * radius;
            int py = y + offsets[i, 1] * radius;
            SetPixel(texture, px, py, main);
            SetPixel(texture, px, py + 1, highlight);
        }
    }

    private static Texture2D CreateTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Clear;
        }

        texture.SetPixels32(pixels);
        return texture;
    }

    private static Sprite FinalizeSprite(Texture2D texture, string name, float pixelsPerUnit)
    {
        texture.name = name;
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0u,
            SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    private static void ClearRect(Texture2D texture, int x, int y, int width, int height)
    {
        DrawFilledRect(texture, x, y, width, height, Clear);
    }

    private static void DrawFilledRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        for (int ix = x; ix < x + width; ix++)
        {
            for (int iy = y; iy < y + height; iy++)
            {
                SetPixel(texture, ix, iy, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            SetPixel(texture, x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int twiceError = error * 2;
            if (twiceError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twiceError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
        {
            return;
        }

        texture.SetPixel(x, y, color);
    }
}
