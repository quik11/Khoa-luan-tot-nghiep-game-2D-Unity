using System.Collections.Generic;
using UnityEngine;

// Các loại nâng cấp (thẻ) chọn khi đầy thanh kiến thức. Số đi kèm là vị trí lưu trong PlayerPrefs:
// thêm loại mới thì dùng số tiếp theo (10, 11...), không dùng lại số cũ, không đổi số đang có.
// Số 3 (Thể lực) và 5 (Chạy nhanh) đã bỏ khỏi game, để trống, save cũ có cấp ở 2 ô đó thì bỏ qua.
public enum UpgradeType
{
    HutMau = 0,           // hạ 1 địch hồi máu
    SatThuongDam = 1,     // cộng sát thương đòn đánh thường (combo đất + trên không, không gồm đòn mạnh I)
    BangDanTo = 2,        // thêm sức chứa đạn tơ
    TamToKeo = 4,         // tơ kéo bắn xa hơn (đang tắt: tối đa 0 cấp)
    HoiToKhiHaDich = 6,   // hạ địch thì được hồi tơ
    SatThuongDonManh = 7, // cộng sát thương đòn mạnh I
    HatVangDonManh = 8,   // đòn mạnh hất văng xa hơn
    ThoiGianTroi = 9,     // trói địch lâu hơn (đòn mạnh + 2 phát tơ đạn)
    NeHoanHaoHoiTo = 10   // né hoàn hảo thì hồi tơ
}

// Lưu cấp của từng nâng cấp bằng PlayerPrefs (vd "1,0,2,0,0,0,1,0,0,0"), điểm kiến thức đang tích
// và danh sách quái/sách đã cho kiến thức (chống chơi lại 1 màn để cày). Tắt game mở lại vẫn còn, "Chơi mới" thì xóa hết.
public static class PlayerUpgrades
{
    private const string saveKey = "BrandNEU_NangCap";
    private const string knowledgeKey = "BrandNEU_KienThuc";
    private const string collectedKey = "BrandNEU_DaNhanKienThuc";

    // số ô trong chuỗi lưu = số lớn nhất trong enum + 1 (enum có chỗ trống nên không đếm số loại được)
    public static int SlotCount
    {
        get
        {
            int max = 0;
            foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
            {
                max = Mathf.Max(max, (int)type);
            }
            return max + 1;
        }
    }

    public static int GetLevel(UpgradeType type)
    {
        int[] levels = Load();
        return levels[(int)type];
    }

    public static void AddLevel(UpgradeType type)
    {
        int[] levels = Load();
        levels[(int)type]++;
        Save(levels);
    }

    // cho bảng chỉnh thông số trong Editor đặt thẳng cấp (bấm +/-) khi thử nghiệm
    public static void SetLevel(UpgradeType type, int level)
    {
        int[] levels = Load();
        levels[(int)type] = Mathf.Max(0, level);
        Save(levels);
    }

    // điểm kiến thức đang tích cho thanh hiện tại (phần dư sau lần nâng cấp trước)
    public static int Knowledge
    {
        get { return Mathf.Max(0, PlayerPrefs.GetInt(knowledgeKey, 0)); }
        set
        {
            PlayerPrefs.SetInt(knowledgeKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.DeleteKey(knowledgeKey);
        PlayerPrefs.DeleteKey(collectedKey);
        PlayerPrefs.Save();
    }

    // ===== Chống cày: mỗi con quái / quyển sách chỉ cho kiến thức 1 lần trong cả game =====
    // Mã của 1 vật = tên scene + tên object + vị trí lúc vào màn (làm tròn 0.1 ô), băm ngắn lại cho chuỗi lưu đỡ dài.
    // Quái và sách đặt sẵn trong scene nên vào lại màn vẫn ra đúng mã cũ. Dựng lại màn mà dời chỗ quái thì coi như quái mới.

    public static string MakeId(string sceneName, string objectName, Vector2 position)
    {
        string key = sceneName + "|" + objectName + "|" + Mathf.RoundToInt(position.x * 10f) + "|" + Mathf.RoundToInt(position.y * 10f);

        // FNV-1a 32 bit: tự viết vì string.GetHashCode không chắc ra cùng số giữa các lần mở game
        uint hash = 2166136261;
        foreach (char c in key)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash.ToString("x8");
    }

    public static bool IsCollected(string id)
    {
        return LoadCollected().Contains(id);
    }

    // trả về true nếu đây là lần đầu (vừa ghi vào danh sách), false nếu đã nhận từ trước
    public static bool MarkCollected(string id)
    {
        HashSet<string> collected = LoadCollected();
        if (!collected.Add(id)) return false;

        PlayerPrefs.SetString(collectedKey, string.Join(",", new List<string>(collected).ToArray()));
        PlayerPrefs.Save();
        return true;
    }

    public static int CollectedCount
    {
        get { return LoadCollected().Count; }
    }

    // cho bảng chỉnh thông số: quên danh sách đã nhận để thử lại các màn từ đầu (giữ nguyên cấp nâng cấp)
    public static void ResetCollected()
    {
        PlayerPrefs.DeleteKey(collectedKey);
        PlayerPrefs.Save();
    }

    // đọc thẳng PlayerPrefs mỗi lần (không giữ bản sao static) cho khỏi lệch khi "Chơi mới" hay bảng chỉnh thông số xóa
    private static HashSet<string> LoadCollected()
    {
        HashSet<string> collected = new HashSet<string>();
        string saved = PlayerPrefs.GetString(collectedKey, "");
        if (string.IsNullOrEmpty(saved)) return collected;

        foreach (string id in saved.Split(','))
        {
            if (!string.IsNullOrEmpty(id)) collected.Add(id);
        }
        return collected;
    }

    private static int[] Load()
    {
        int[] levels = new int[SlotCount];
        string saved = PlayerPrefs.GetString(saveKey, "");
        if (string.IsNullOrEmpty(saved)) return levels;

        // chuỗi lưu từ bản cũ (ít ô hơn) thì các loại mới thêm sau cứ để cấp 0
        string[] parts = saved.Split(',');
        for (int i = 0; i < parts.Length && i < levels.Length; i++)
        {
            int value;
            if (int.TryParse(parts[i], out value)) levels[i] = Mathf.Max(0, value);
        }
        return levels;
    }

    private static void Save(int[] levels)
    {
        // đổi sang mảng chuỗi trước cho chắc, nối mảng int trực tiếp dễ ra nhầm "System.Int32[]"
        PlayerPrefs.SetString(saveKey, string.Join(",", System.Array.ConvertAll(levels, level => level.ToString())));
        PlayerPrefs.Save();
    }
}
