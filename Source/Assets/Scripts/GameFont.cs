using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Đổi font chữ giao diện sang font pixel VT323 (có đủ dấu tiếng Việt) lúc chạy, khỏi phải sửa từng chữ trong scene/prefab.
// Chỉ đổi những chữ đang dùng LiberationSans (font tiếng Việt cũ); tên game "Brand NEU day" (font Super Squad) giữ nguyên.
// Font tạo bằng menu Brand NEU day > Tạo lại font chữ VT323 (tự tạo lần đầu mở Unity). Chưa có font thì giữ nguyên chữ cũ.
public static class GameFont
{
    private const string resourcePath = "Fonts/VT323 SDF";
    // VT323 nét chữ thấp hơn LiberationSans cùng cỡ (chữ hoa cao 0.56 so với 0.69) -> phóng cỡ chữ lên cho bằng
    public const float SizeScale = 1.2f;

    private static TMP_FontAsset font;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        font = null;
        loaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static TMP_FontAsset Font
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                font = Resources.Load<TMP_FontAsset>(resourcePath);
            }
            return font;
        }
    }

    // sceneLoaded chạy sau Awake nhưng trước Start: các chữ nhân bản ở Start (thanh kiến thức, bảng tạm dừng...) mang luôn font mới
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Font == null) return;
        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Apply(text);
        }
    }

    // gọi cho chữ sinh ra sau khi vào scene (vd số sát thương bay lên)
    public static void Apply(TMP_Text text)
    {
        if (text == null || Font == null || text.font == Font) return;
        if (text.font != null && !text.font.name.Contains("LiberationSans")) return;

        text.font = Font;
        text.fontSharedMaterial = Font.material;
        text.fontSize *= SizeScale;
        text.fontSizeMin *= SizeScale;
        text.fontSizeMax *= SizeScale;
    }

    // chữ tạo bằng code: chọn font + cỡ cho khớp (cỡ ghi theo LiberationSans)
    public static void Setup(TMP_Text text, TMP_FontAsset fallback, float size)
    {
        if (Font != null)
        {
            text.font = Font;
            text.fontSize = size * SizeScale;
        }
        else
        {
            if (fallback != null) text.font = fallback;
            text.fontSize = size;
        }
    }
}
