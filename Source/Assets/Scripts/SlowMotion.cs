using UnityEngine;
using UnityEngine.SceneManagement;

// Chậm thời gian ngắn (né hoàn hảo). Đếm theo giờ thật nên không tự kéo dài chính nó.
// Phối hợp với 2 thứ khác cũng đổi Time.timeScale:
//  - khựng hình khi trúng đòn (Damageable) hết giờ thì trả về TargetScale thay vì 1 -> đang chậm thì vẫn chậm tiếp;
//  - menu tạm dừng: đang dừng thì không đụng tới timeScale, chơi tiếp mà còn giờ chậm thì tự chậm lại.
// Không cần gắn vào đâu: gọi SlowMotion.Begin(...) là tự tạo.
public class SlowMotion : MonoBehaviour
{
    private static SlowMotion instance;
    private static bool active;
    private static float scale = 1f;
    private static float endRealTime;
    private static float baseFixedDeltaTime = 0.02f;

    // tốc độ game "bình thường" lúc này: 1, hoặc tốc độ chậm nếu đang chậm thời gian
    public static float TargetScale { get { return active ? scale : 1f; } }
    public static bool IsActive { get { return active; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active = false;
        scale = 1f;
    }

    public static void Begin(float timeScale, float realDuration)
    {
        if (realDuration <= 0f) return;

        if (instance == null)
        {
            GameObject runner = new GameObject("ChamThoiGian");
            DontDestroyOnLoad(runner);
            instance = runner.AddComponent<SlowMotion>();
        }

        if (!active) baseFixedDeltaTime = Time.fixedDeltaTime;
        active = true;
        scale = Mathf.Clamp(timeScale, 0.05f, 1f);
        endRealTime = Time.unscaledTime + realDuration;
        Apply();
    }

    public static void Stop()
    {
        if (!active) return;
        active = false;
        Time.fixedDeltaTime = baseFixedDeltaTime;
        // chỉ trả về 1 khi tốc độ vẫn là của mình; đang khựng hình (0), tạm dừng, hay chỗ khác đang tự làm chậm
        // (boss gục ngã) thì để chỗ đó tự trả lại
        if (!PauseMenu.IsPaused && Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // đổi màn giữa lúc đang chậm thì thôi, không mang sang màn mới
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Stop();
    }

    private void Update()
    {
        if (!active) return;
        if (Time.unscaledTime >= endRealTime)
        {
            Stop();
            return;
        }
        Apply();
    }

    private static void Apply()
    {
        // timeScale = 0 là đang khựng hình hoặc tạm dừng / bảng chọn nâng cấp -> không chen vào.
        // Chỉ đè lên tốc độ bình thường (1, vd vừa chơi tiếp sau tạm dừng); chỗ khác đang làm chậm kiểu riêng thì nhường
        if (PauseMenu.IsPaused || Time.timeScale <= 0f) return;
        if (Time.timeScale != scale && !Mathf.Approximately(Time.timeScale, 1f)) return;
        Time.timeScale = scale;
        // nhịp vật lý chậm theo cho mượt (để nguyên thì lúc chậm nhân vật nhảy cóc từng nấc)
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }
}
