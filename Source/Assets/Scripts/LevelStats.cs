using UnityEngine;
using UnityEngine.SceneManagement;

// Thống kê 1 lượt chơi màn (thời gian, số lần ngã, máu đã mất) -> quy ra GPA thang 4 lúc qua màn.
// Lưu GPA cao nhất của từng màn (PlayerPrefs BrandNEU_GPA_ManN); GPA tích lũy = trung bình GPA cao nhất các màn đã qua.
// Không cần gắn vào đâu: vào màn chơi nào (có trong GameProgress.levelScenes) cũng tự tạo, kèm cốt truyện + chữ hướng dẫn.
public class LevelStats : MonoBehaviour
{
    public static LevelStats Instance { get; private set; }

    // ===== cách tính GPA (chỉnh ở đây) =====
    // thời gian chuẩn của từng màn (giây), quá giờ chuẩn mới bị trừ - Màn 1 ở đầu
    private static readonly float[] parTimes = { 240f, 300f, 360f, 420f, 240f, 420f };
    private const float deathPenalty = 0.4f;        // mỗi lần ngã (chết rồi hồi sinh)
    private const float damagePenaltyPer100 = 0.25f; // mỗi 100 máu bị mất
    private const float timePenaltyPer30s = 0.1f;   // mỗi 30 giây quá giờ chuẩn

    private const string bestKeyPrefix = "BrandNEU_GPA_Man";

    public float ElapsedTime { get; private set; }
    public int Deaths { get; private set; }
    public int DamageTaken { get; private set; }
    public int Kills { get; private set; }
    public int Level { get; private set; }

    private bool finished;

    // ===== tự tạo khi vào màn =====
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (GameProgress.CurrentLevel <= 0) return; // menu, màn thoát: không đo

        GameObject session = new GameObject("PhienChoiMan");
        session.AddComponent<LevelStats>();
        session.AddComponent<StoryIntro>();
        session.AddComponent<TutorialHints>();
    }

    private void Awake()
    {
        Instance = this;
        Level = GameProgress.CurrentLevel;
        Damageable.AnyHit += OnAnyHit;
        Damageable.AnyDied += OnAnyDied;
    }

    private void OnDestroy()
    {
        Damageable.AnyHit -= OnAnyHit;
        Damageable.AnyDied -= OnAnyDied;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (finished) return;
        ElapsedTime += Time.deltaTime; // tạm dừng / đọc cốt truyện thì timeScale = 0 nên không tính
    }

    private void OnAnyHit(Damageable target, int damage, Vector2 knockback)
    {
        if (finished || target == null) return;
        if (target.GetComponent<PlayerController>() != null) DamageTaken += damage;
    }

    private void OnAnyDied(Damageable target)
    {
        if (finished || target == null) return;
        if (target.GetComponent<PlayerController>() != null) Deaths++;
        else Kills++;
    }

    // ===== GPA =====
    public static float ParTime(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, parTimes.Length - 1);
        return parTimes[index];
    }

    public float CurrentGpa
    {
        get
        {
            float overTime = Mathf.Max(0f, ElapsedTime - ParTime(Level));
            float gpa = 4f
                        - deathPenalty * Deaths
                        - damagePenaltyPer100 * DamageTaken / 100f
                        - timePenaltyPer30s * overTime / 30f;
            return Mathf.Round(Mathf.Clamp(gpa, 0f, 4f) * 100f) / 100f;
        }
    }

    // xếp loại theo thang điểm 4 của đại học
    public static string Classify(float gpa)
    {
        if (gpa >= 3.6f) return "Xuất sắc";
        if (gpa >= 3.2f) return "Giỏi";
        if (gpa >= 2.5f) return "Khá";
        if (gpa >= 2.0f) return "Trung bình";
        if (gpa >= 1.0f) return "Yếu";
        return "Kém";
    }

    public static float BestGpa(int level)
    {
        return PlayerPrefs.GetFloat(bestKeyPrefix + level, -1f); // -1 = chưa qua màn này lần nào
    }

    // GPA tích lũy: trung bình GPA cao nhất các màn đã có điểm. -1 nếu chưa có màn nào
    public static float CumulativeGpa()
    {
        float sum = 0f;
        int count = 0;
        for (int level = 1; level <= GameProgress.LevelCount; level++)
        {
            float best = BestGpa(level);
            if (best < 0f) continue;
            sum += best;
            count++;
        }
        return count == 0 ? -1f : Mathf.Round(sum / count * 100f) / 100f;
    }

    public static void ResetAll()
    {
        for (int level = 1; level <= GameProgress.LevelCount; level++)
        {
            PlayerPrefs.DeleteKey(bestKeyPrefix + level);
        }
    }

    // qua màn: dừng đồng hồ, lưu kỷ lục, trả về đoạn chữ tổng kết để hiện lên bảng
    public string Finish(out bool newRecord)
    {
        finished = true;
        float gpa = CurrentGpa;
        float best = BestGpa(Level);
        newRecord = gpa > best;
        if (newRecord)
        {
            PlayerPrefs.SetFloat(bestKeyPrefix + Level, gpa);
            PlayerPrefs.Save();
        }
        return Describe(gpa, newRecord);
    }

    private string Describe(float gpa, bool newRecord)
    {
        string text = "Thời gian: " + FormatTime(ElapsedTime) + "  (chuẩn " + FormatTime(ParTime(Level)) + ")\n"
                    + "Số lần ngã: " + Deaths + "     Máu đã mất: " + DamageTaken + "\n\n"
                    + "GPA màn này: <color=#FFE066>" + gpa.ToString("0.00") + "</color> - " + Classify(gpa);
        if (newRecord) text += "  <color=#7CFC9A>(kỷ lục mới!)</color>";

        float cumulative = CumulativeGpa();
        if (cumulative >= 0f)
        {
            text += "\nGPA tích lũy: " + cumulative.ToString("0.00") + " - " + Classify(cumulative);
        }
        return text;
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
