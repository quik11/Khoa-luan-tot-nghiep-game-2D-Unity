using UnityEngine;

// Nháy đỏ báo trước mỗi nhát đánh của đòn đánh cũ trong Animator (Quỷ gối 2 nhát, quái bay cắn + quật).
// Không sửa animation: chỉ đọc state đang chạy và thời điểm từng nhát (tính theo phần của clip, 0 = đầu, 1 = cuối),
// tới trước nhát đó leadTime giây thì gọi HitFlash.Telegraph. HiepSi / FlyingEye tự gắn và điền số lúc vào màn.
[RequireComponent(typeof(HitFlash))]
public class EnemyAttackTelegraph : MonoBehaviour
{
    [Tooltip("Tên state ra đòn trong Animator")]
    [TenHienThi("State ra đòn")] public string attackStateName = "";
    [Tooltip("Thời điểm từng nhát trúng trong clip, tính theo phần clip (0..1)")]
    [TenHienThi("Thời điểm các nhát")] public float[] hitMoments = new float[0];
    [Tooltip("Ửng đỏ trước nhát đánh bao nhiêu giây")]
    [TenHienThi("Báo trước")] public float leadTime = 0.45f;

    private Animator animator;
    private HitFlash hitFlash;
    private int nextMoment;
    private bool wasInState;
    private int lastLoop = -1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        hitFlash = GetComponent<HitFlash>();
    }

    private void Update()
    {
        if (animator == null || hitFlash == null || string.IsNullOrEmpty(attackStateName) || hitMoments.Length == 0) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inState = state.IsName(attackStateName);
        if (!inState)
        {
            if (wasInState) hitFlash.StopTelegraph(); // bị đánh ngắt đòn giữa chừng thì thôi nháy
            wasInState = false;
            return;
        }

        float normalized = state.normalizedTime;
        int loop = Mathf.FloorToInt(normalized);
        float inLoop = normalized - loop;
        if (!wasInState || loop != lastLoop)
        {
            nextMoment = 0; // vừa vào đòn (hoặc đòn lặp lại) thì đếm lại từ nhát đầu
            lastLoop = loop;
        }
        wasInState = true;

        // đổi leadTime (giây thật) ra phần clip: clip Quỷ gối dài 1.38 s nhưng state chạy tốc độ 0.55 -> thật ra 2.5 s
        float duration = RealDuration(state);
        float leadFraction = duration > 0.01f ? leadTime / duration : 0f;
        while (nextMoment < hitMoments.Length && inLoop >= hitMoments[nextMoment] - leadFraction)
        {
            float remaining = (hitMoments[nextMoment] - inLoop) * duration;
            if (remaining > -0.05f) hitFlash.Telegraph(Mathf.Max(0.1f, remaining + 0.05f));
            nextMoment++;
        }
    }

    private float RealDuration(AnimatorStateInfo state)
    {
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        float clipLength = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.length : state.length;
        float speed = Mathf.Abs(state.speed * state.speedMultiplier) * animator.speed;
        return speed > 0.001f ? clipLength / speed : state.length;
    }
}
