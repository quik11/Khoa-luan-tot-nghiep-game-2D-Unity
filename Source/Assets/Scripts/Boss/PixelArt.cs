using System.Collections.Generic;
using UnityEngine;

// Vẽ hình pixel nhỏ bằng code (đồng hồ cát, tấm bằng, vạch cảnh báo...) để khỏi phải nhập thêm file ảnh.
// Có hình tự vẽ thì kéo vào ô sprite tương ứng trên boss, ô để trống mới dùng hình vẽ bằng code.
public static class PixelArt
{
    public const float PixelsPerUnit = 16f; // giống ô gạch và nhân vật

    private static Sprite whiteSprite;

    // ô vuông trắng 1x1 ô (đổi màu bằng SpriteRenderer.color, kéo giãn bằng scale)
    public static Sprite White
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return whiteSprite;
        }
    }

    // dựng sprite từ bản vẽ chữ: mỗi ký tự 1 pixel, dòng đầu là hàng trên cùng; ký tự không có trong bảng màu = trong suốt
    public static Sprite FromRows(string[] rows, Dictionary<char, Color> palette, Vector2 pivot)
    {
        int height = rows.Length;
        int width = 0;
        foreach (string row in rows) width = Mathf.Max(width, row.Length);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < height; y++)
        {
            string row = rows[height - 1 - y];
            for (int x = 0; x < width; x++)
            {
                Color color = clear;
                bool filled = x < row.Length && palette.TryGetValue(row[x], out color);
                tex.SetPixel(x, y, filled ? color : clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
    }

    // đồng hồ cát 12x18: khung gỗ nâu, kính xanh nhạt, cát vàng (nửa trên còn ít, nửa dưới đầy)
    public static Sprite Hourglass()
    {
        string[] rows =
        {
            "wwwwwwwwwwww",
            "wwwwwwwwwwww",
            ".w.gggggg.w.",
            ".w.gssssg.w.",
            ".w..gssg..w.",
            ".w..gssg..w.",
            ".w...gg...w.",
            ".w...gs...w.",
            ".w...sg...w.",
            ".w..g.sg..w.",
            ".w..g..g..w.",
            ".w.g..s.g.w.",
            ".w.g.sss.gw.",
            ".w.gsssssgw.",
            ".w.gssssg.w.",
            ".w.gggggg.w.",
            "wwwwwwwwwwww",
            "wwwwwwwwwwww",
        };
        Dictionary<char, Color> palette = new Dictionary<char, Color>
        {
            { 'w', new Color32(120, 72, 40, 255) },
            { 'g', new Color32(170, 220, 235, 255) },
            { 's', new Color32(245, 200, 80, 255) },
        };
        return FromRows(rows, palette, new Vector2(0.5f, 0.5f));
    }

    // tấm bằng tốt nghiệp 16x12: giấy cuộn màu kem, dải lụa đỏ, dấu vàng
    public static Sprite Diploma()
    {
        string[] rows =
        {
            "..pppppppppppp..",
            ".pPPPPPPPPPPPPp.",
            "pPccccccccccccPp",
            "pPcllllllllllcPp",
            "pPccccccccccccPp",
            "pPcllllllrrlccPp",
            "pPcccccccryrccPp",
            "pPcllllllrrlccPp",
            "pPcccccccrcrccPp",
            "pPccccccr.c.rcPp",
            ".pPPPPPPPPPPPPp.",
            "..pppppppppppp..",
        };
        Dictionary<char, Color> palette = new Dictionary<char, Color>
        {
            { 'p', new Color32(150, 110, 60, 255) },
            { 'P', new Color32(235, 220, 180, 255) },
            { 'c', new Color32(250, 243, 220, 255) },
            { 'l', new Color32(150, 140, 120, 255) },
            { 'r', new Color32(200, 40, 40, 255) },
            { 'y', new Color32(255, 210, 60, 255) },
        };
        return FromRows(rows, palette, new Vector2(0.5f, 0.5f));
    }
}
