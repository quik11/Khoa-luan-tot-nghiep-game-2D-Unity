using UnityEngine;
using UnityEngine.SceneManagement;

// Lưu tiến độ chơi (màn đã mở khóa) bằng PlayerPrefs - tắt game mở lại vẫn còn.
// Thêm màn mới: thêm tên scene vào levelScenes (đúng thứ tự) và kéo scene đó vào File > Build Profiles > Scene List.
public static class GameProgress
{
    // phần tử 0 là Màn 1, phần tử 1 là Màn 2...
    public static readonly string[] levelScenes = { "GameplayScene", "Man2", "Man3", "Man4", "Man5", "Man6" };

    public const string mainMenuScene = "MainMenuScene";

    private const string unlockedKey = "BrandNEU_ManDaMo";

    // bật lên trước khi về menu chính để menu mở thẳng bảng chọn màn (vd vừa qua cửa xong)
    public static bool openLevelSelectOnMenu = false;

    // Editor có thể tắt "Reload Domain" khi bấm Play -> biến static giữ giá trị lần chơi trước, phải tự xóa
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openLevelSelectOnMenu = false;
    }

    public static int LevelCount
    {
        get { return levelScenes.Length; }
    }

    // màn cao nhất đã mở khóa (ít nhất là Màn 1)
    public static int UnlockedLevel
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(unlockedKey, 1), 1, LevelCount); }
    }

    public static bool IsUnlocked(int level)
    {
        return level >= 1 && level <= UnlockedLevel;
    }

    // màn đã có scene trong Build Profiles chưa (màn chưa làm thì nút chọn màn hiện "sắp có")
    public static bool IsLevelBuilt(int level)
    {
        return level >= 1 && level <= LevelCount && Application.CanStreamedLevelBeLoaded(levelScenes[level - 1]);
    }

    // scene đang chơi là màn số mấy (0 nếu không nằm trong danh sách, vd đang ở menu)
    public static int CurrentLevel
    {
        get
        {
            string current = SceneManager.GetActiveScene().name;
            for (int i = 0; i < levelScenes.Length; i++)
            {
                if (levelScenes[i] == current) return i + 1;
            }
            return 0;
        }
    }

    public static bool IsLevelScene(string sceneName)
    {
        return System.Array.IndexOf(levelScenes, sceneName) >= 0;
    }

    // qua cửa xong thì mở khóa màn kế tiếp
    public static void CompleteLevel(int level)
    {
        if (level < 1) return;

        int next = Mathf.Min(level + 1, LevelCount);
        if (next > UnlockedLevel)
        {
            PlayerPrefs.SetInt(unlockedKey, next);
            PlayerPrefs.Save();
        }
    }

    public static bool LoadLevel(int level)
    {
        if (!IsLevelBuilt(level))
        {
            Debug.LogWarning("GameProgress: Màn " + level + " chưa có scene trong Build Profiles (tên cần có: '" + (level >= 1 && level <= LevelCount ? levelScenes[level - 1] : "?") + "').");
            return false;
        }

        SceneLoader.LoadLevelScene(levelScenes[level - 1]); // màn hình chờ: tên màn + mẹo
        return true;
    }

    // nút "Chơi mới": xóa hết tiến độ (màn đã mở + nâng cấp + kiến thức), quay về chỉ mở Màn 1
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(unlockedKey);
        PlayerUpgrades.ResetAll();
        LevelStats.ResetAll();
        StoryIntro.ResetAll();
        PlayerPrefs.Save();
    }

    // cho bảng chỉnh thông số trong Editor: mở hết các màn để thử nhanh (kể cả đòn mạnh vì đã qua Màn 1)
    public static void UnlockAllLevels()
    {
        PlayerPrefs.SetInt(unlockedKey, LevelCount);
        PlayerPrefs.Save();
    }
}
