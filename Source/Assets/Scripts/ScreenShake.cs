using UnityEngine;

// Rung màn hình ngắn khi có đòn nặng (trúng đòn, đòn mạnh I, boss giáng tay...).
// Camera do Cinemachine điều khiển, mỗi khung hình nó đặt lại vị trí camera; script này chạy sau Cinemachine
// (thứ tự thực thi rất muộn) và cộng thêm 1 độ lệch nhỏ, nên khung hình sau Cinemachine tự đưa camera về chỗ đúng.
// Không cần gắn vào đâu: gọi ScreenShake.Shake(...) là tự tạo. Tắt trong bảng Cài đặt (PlayerPrefs BrandNEU_RungManHinh).
[DefaultExecutionOrder(10000)]
public class ScreenShake : MonoBehaviour
{
    public const string PrefKey = "BrandNEU_RungManHinh";

    private static ScreenShake instance;

    private float amplitude;   // độ lệch lớn nhất (ô)
    private float duration;
    private float timer;
    private Vector3 appliedOffset;
    private Vector3 shakenPosition;
    private Transform cameraTransform;

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
        set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // amplitude tính theo ô (1 ô = 16 điểm ảnh); rung đang chạy mà có rung mạnh hơn thì lấy cái mạnh hơn
    public static void Shake(float amplitude, float duration)
    {
        if (!Enabled || amplitude <= 0f || duration <= 0f) return;

        if (instance == null)
        {
            instance = new GameObject("RungManHinh").AddComponent<ScreenShake>();
        }

        if (instance.timer <= 0f || amplitude >= instance.amplitude * (instance.timer / instance.duration))
        {
            instance.amplitude = amplitude;
            instance.duration = duration;
            instance.timer = duration;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            if (Camera.main == null) return;
            cameraTransform = Camera.main.transform;
        }

        // khung hình trước đã lệch mà Cinemachine không đặt lại vị trí (camera đứng yên) thì tự trả về, tránh lệch dồn
        if (appliedOffset != Vector3.zero && cameraTransform.position == shakenPosition)
        {
            cameraTransform.position -= appliedOffset;
        }
        appliedOffset = Vector3.zero;

        if (timer <= 0f) return;

        // giờ thật: lúc khựng hình (timeScale = 0) vẫn rung
        timer -= Time.unscaledDeltaTime;
        float strength = amplitude * Mathf.Clamp01(timer / duration); // yếu dần về 0
        Vector2 random = Random.insideUnitCircle * strength;
        appliedOffset = new Vector3(random.x, random.y, 0f);

        cameraTransform.position += appliedOffset;
        shakenPosition = cameraTransform.position;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
