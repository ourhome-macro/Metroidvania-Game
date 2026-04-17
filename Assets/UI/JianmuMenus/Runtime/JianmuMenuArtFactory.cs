using UnityEngine;

public static class JianmuMenuArtFactory
{
    public sealed class SpriteSet
    {
        public Sprite PausePanel;
        public Sprite Button;
        public Sprite InkBlot;
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
            PausePanel = CreatePausePanelSprite(),
            Button = CreateButtonSprite(),
            InkBlot = CreateInkBlotSprite()
        };

        return cached;
    }

    private static Sprite CreatePausePanelSprite()
    {
        Texture2D texture = CreateTexture(312, 196);
        Color32 outline = new Color32(16, 12, 12, 240);
        Color32 barkDark = new Color32(33, 22, 19, 232);
        Color32 barkMid = new Color32(57, 39, 31, 224);
        Color32 barkLight = new Color32(112, 76, 49, 224);
        Color32 cyber = new Color32(46, 237, 216, 78);
        Color32 magenta = new Color32(230, 49, 170, 68);

        DrawFilledRect(texture, 0, 0, 312, 196, outline);
        DrawFilledRect(texture, 4, 4, 304, 188, barkDark);
        DrawFilledRect(texture, 10, 10, 292, 176, barkMid);

        for (int y = 18; y < 178; y += 8)
        {
            for (int x = 16; x < 294; x += 28)
            {
                if (((x + y) / 8) % 2 == 0)
                {
                    SetPixel(texture, x, y, barkLight);
                    SetPixel(texture, x + 1, y, barkLight);
                }
            }
        }

        DrawLine(texture, 26, 162, 286, 46, cyber);
        DrawLine(texture, 40, 176, 214, 42, magenta);
        DrawLine(texture, 116, 170, 260, 90, cyber);

        DrawCorner(texture, 14, 14, barkLight);
        DrawCorner(texture, 297, 14, barkLight);
        DrawCorner(texture, 14, 181, barkLight);
        DrawCorner(texture, 297, 181, barkLight);

        return FinalizeSprite(texture, "JianmuPausePanel");
    }

    private static Sprite CreateButtonSprite()
    {
        Texture2D texture = CreateTexture(216, 56);
        Color32 outline = new Color32(14, 11, 10, 255);
        Color32 barkDark = new Color32(44, 29, 24, 250);
        Color32 barkMid = new Color32(81, 53, 33, 250);
        Color32 barkLight = new Color32(138, 94, 55, 250);
        Color32 cyber = new Color32(42, 238, 214, 92);

        DrawFilledRect(texture, 0, 0, 216, 56, outline);
        DrawFilledRect(texture, 2, 2, 212, 52, barkDark);
        DrawFilledRect(texture, 6, 6, 204, 44, barkMid);

        for (int x = 12; x < 204; x += 16)
        {
            SetPixel(texture, x, 46, barkLight);
            SetPixel(texture, x + 1, 46, barkLight);
            SetPixel(texture, x + 2, 46, barkLight);
        }

        DrawLine(texture, 20, 42, 196, 18, cyber);
        DrawLine(texture, 40, 18, 78, 38, barkLight);
        DrawLine(texture, 132, 14, 168, 30, barkLight);

        return FinalizeSprite(texture, "JianmuMenuButton");
    }

    private static Sprite CreateInkBlotSprite()
    {
        Texture2D texture = CreateTexture(32, 24);
        Color32 inkCore = new Color32(31, 24, 24, 210);
        Color32 inkSoft = new Color32(64, 54, 55, 142);
        Color32 neonCyan = new Color32(44, 234, 214, 70);
        Color32 neonMagenta = new Color32(230, 62, 176, 58);

        int[,] corePoints =
        {
            { 7, 11, 5 },
            { 11, 7, 6 },
            { 16, 10, 7 },
            { 21, 8, 5 },
            { 23, 14, 5 },
            { 16, 16, 6 },
            { 11, 15, 5 }
        };

        int[,] softPoints =
        {
            { 5, 10, 6 },
            { 10, 5, 5 },
            { 18, 6, 6 },
            { 25, 11, 4 },
            { 19, 18, 6 },
            { 10, 18, 5 }
        };

        for (int i = 0; i < softPoints.GetLength(0); i++)
        {
            DrawBlob(texture, softPoints[i, 0], softPoints[i, 1], softPoints[i, 2], inkSoft);
        }

        for (int i = 0; i < corePoints.GetLength(0); i++)
        {
            DrawBlob(texture, corePoints[i, 0], corePoints[i, 1], corePoints[i, 2], inkCore);
        }

        DrawBlob(texture, 10, 6, 2, neonCyan);
        DrawBlob(texture, 22, 15, 2, neonMagenta);
        DrawLine(texture, 12, 3, 9, 0, inkSoft);
        DrawLine(texture, 23, 18, 27, 22, inkSoft);

        return FinalizeSprite(texture, "JianmuInkBlot");
    }

    private static void DrawCorner(Texture2D texture, int x, int y, Color32 color)
    {
        DrawFilledRect(texture, x, y, 12, 4, color);
        DrawFilledRect(texture, x, y, 4, 12, color);
    }

    private static void DrawBlob(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
    {
        int squared = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) > squared)
                {
                    continue;
                }

                SetPixel(texture, centerX + x, centerY + y, color);
            }
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

    private static Sprite FinalizeSprite(Texture2D texture, string name)
    {
        texture.name = name;
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = name;
        return sprite;
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
