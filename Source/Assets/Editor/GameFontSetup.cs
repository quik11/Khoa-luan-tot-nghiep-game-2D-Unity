using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tự tạo font TextMesh Pro từ Assets/Fonts/VT323-Regular.ttf (font pixel có đủ chữ tiếng Việt có dấu)
// vào Assets/Resources/Fonts/VT323 SDF.asset, kèm viền đen + bóng đổ cho chữ dễ đọc trên mọi nền.
// Mở Unity là tự chạy 1 lần nếu chưa có. Muốn làm lại (vd đổi độ dày viền): menu Brand NEU day > Tạo lại font chữ VT323.
// Đổi font cho chữ trong game do Assets/Scripts/GameFont.cs lo lúc chạy, không sửa scene/prefab.
[InitializeOnLoad]
public static class GameFontSetup
{
    private const string fontPath = "Assets/Fonts/VT323-Regular.ttf";
    private const string resourcesFolder = "Assets/Resources";
    private const string fontFolder = "Assets/Resources/Fonts";
    private const string assetPath = "Assets/Resources/Fonts/VT323 SDF.asset";

    // viền + bóng: chỉnh ở đây rồi chạy lại menu
    private const float outlineWidth = 0.2f;
    private static readonly Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
    private static readonly Color shadowColor = new Color(0f, 0f, 0f, 0.55f);
    private const float shadowOffset = 0.8f;

    // nạp sẵn chữ hay dùng (các chữ khác vẫn tự thêm lúc chạy)
    private const string commonCharacters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~–…" +
        "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ" +
        "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ";

    static GameFontSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) == null) Create();
        };
    }

    [MenuItem("Brand NEU day/Tạo lại font chữ VT323 cho giao diện")]
    public static void Create()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogError("Font chữ: không thấy " + fontPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(resourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(fontFolder)) AssetDatabase.CreateFolder(resourcesFolder, "Fonts");
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);

        // SDF cỡ mẫu 64, đệm 8: đủ chỗ cho viền + bóng mà nét pixel vẫn vuông vức
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                                                                AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null)
        {
            Debug.LogError("Font chữ: TextMesh Pro không tạo được font từ " + fontPath);
            return;
        }
        fontAsset.name = "VT323 SDF";
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        // ảnh atlas và material phải lưu kèm trong file font, không thì mở lại Unity bị mất
        Texture2D atlas = fontAsset.atlasTextures[0];
        atlas.name = "VT323 SDF Atlas";
        AssetDatabase.AddObjectToAsset(atlas, fontAsset);

        Material material = fontAsset.material;
        material.name = "VT323 SDF Material";
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, shadowColor);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadowOffset);
        AssetDatabase.AddObjectToAsset(material, fontAsset);

        string missing;
        fontAsset.TryAddCharacters(commonCharacters, out missing);
        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning("Font chữ: VT323 thiếu các chữ: " + missing);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Font chữ: đã tạo " + assetPath + " (viền đen + bóng). Vào màn chơi là chữ tiếng Việt tự đổi sang font này.");
    }
}
