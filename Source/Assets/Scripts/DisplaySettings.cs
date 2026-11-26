using System.Collections.Generic;
using UnityEngine;

// Cài đặt hiển thị: toàn màn hình / cửa sổ và độ phân giải. Lưu PlayerPrefs, tự áp lại mỗi lần mở game.
// Chỉ có tác dụng ở bản đã đóng gói (.exe); chạy trong Unity Editor thì cửa sổ Game không đổi (bình thường).
public static class DisplaySettings
{
    private const string fullscreenKey = "BrandNEU_ToanManHinh";
    private const string resolutionKey = "BrandNEU_DoPhanGiai"; // lưu dạng "1920x1080"

    // các cỡ 16:9 hay gặp; cỡ lớn hơn màn hình đang dùng thì bỏ
    private static readonly Vector2Int[] commonSizes =
    {
        new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160)
    };

    private static List<Vector2Int> options;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        options = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedOnStart()
    {
        if (Application.isEditor) return;
        if (!PlayerPrefs.HasKey(fullscreenKey) && !PlayerPrefs.HasKey(resolutionKey)) return; // chưa chỉnh lần nào: để mặc định của bản build
        Apply();
    }

    public static bool Fullscreen
    {
        get { return PlayerPrefs.GetInt(fullscreenKey, 1) == 1; }
        set { PlayerPrefs.SetInt(fullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
    }

    public static List<Vector2Int> Options
    {
        get
        {
            if (options != null) return options;

            options = new List<Vector2Int>();
            Resolution display = Screen.currentResolution; // cỡ màn hình đang dùng
            foreach (Vector2Int size in commonSizes)
            {
                if (size.x <= display.width && size.y <= display.height) options.Add(size);
            }
            Vector2Int native = new Vector2Int(display.width, display.height);
            if (!options.Contains(native)) options.Add(native); // luôn có cỡ đúng bằng màn hình
            options.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            return options;
        }
    }

    // vị trí cỡ đang chọn trong Options (chưa chọn bao giờ thì lấy cỡ lớn nhất = vừa màn hình)
    public static int ResolutionIndex
    {
        get
        {
            List<Vector2Int> list = Options;
            string saved = PlayerPrefs.GetString(resolutionKey, "");
            for (int i = 0; i < list.Count; i++)
            {
                if (Format(list[i]) == saved) return i;
            }
            return list.Count - 1;
        }
        set
        {
            List<Vector2Int> list = Options;
            int index = (value % list.Count + list.Count) % list.Count; // bấm quá thì vòng lại đầu/cuối
            PlayerPrefs.SetString(resolutionKey, Format(list[index]));
            PlayerPrefs.Save();
            Apply();
        }
    }

    public static string CurrentResolutionLabel
    {
        get { return Format(Options[ResolutionIndex]); }
    }

    public static string Format(Vector2Int size)
    {
        return size.x + "x" + size.y;
    }

    private static void Apply()
    {
        Vector2Int size = Options[ResolutionIndex];
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(size.x, size.y, mode);
    }
}
