using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Đồng hồ cát lơ lửng trong lúc boss bật khiên. Phá đủ hết đồng hồ thì khiên vỡ.
// Cách phá: đánh thẳng cho hết máu, hoặc bắn 2 phát tơ đạn cho bị trói rồi tơ kéo (Q) giật xuống -> rơi chạm đất là vỡ.
// Layer Enemy nên tơ đạn, đòn đánh, tơ kéo đều trúng; Enemy không va chạm người chơi nên không chặn đường.
// BossController tạo bằng code (Create), không cần prefab.
[RequireComponent(typeof(Rigidbody2D), typeof(Damageable))]
public class BossHourglass : MonoBehaviour
{
    [TenHienThi("Nhấp nhô cao")] public float bobHeight = 0.3f;
    [TenHienThi("Nhấp nhô nhanh")] public float bobSpeed = 2f;
    [TenHienThi("Trôi ngang")] public float driftRange = 0.8f;  // trôi qua lại quanh điểm đặt
    [TenHienThi("Tốc trôi")] public float driftSpeed = 0.6f;
    [TenHienThi("Trọng lực rơi")] public float fallGravity = 3f;   // bị giật xuống thì rơi nhanh cỡ này
    [TenHienThi("Màu bị trói")] public Color stunnedColor = new Color(0.6f, 0.8f, 1f, 1f);
    [TenHienThi("Âm thanh vỡ")] public AudioClip breakSound;

    public System.Action<BossHourglass> onBroken;

    public bool IsBroken { get; private set; }

    private Rigidbody2D rb;
    private Damageable damageable;
    private SpriteRenderer spriteRenderer;
    private Vector2 anchor;
    private float time;
    private bool falling;
    private float flashTimer;

    public static BossHourglass Create(Vector2 position, Sprite sprite, Material material, int health, float stunTime)
    {
        GameObject root = new GameObject("DongHoCat");
        root.layer = LayerMask.NameToLayer("Enemy");
        root.transform.position = position;

        // Rigidbody2D phải có trước Damageable (Damageable lấy Rigidbody2D ngay lúc thêm vào)
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic; // lúc lơ lửng tự điều khiển vị trí, không bị trọng lực
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Enemies";
        renderer.sortingOrder = 5;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        // khung trúng cao hơn hình: tơ kéo (ngang hông) và tơ đạn (ngang vai) bắn từ trên bệ đều trúng
        box.size = new Vector2(0.9f, 1.6f);

        Damageable damage = root.AddComponent<Damageable>();
        damage.MaxHealth = health;
        damage.Health = health;
        damage.superArmor = true;     // không có Animator, không cần giật mình
        damage.canBePulled = true;
        damage.invincibilityTime = 0.15f;
        damage.webHitsToStun = 2;
        damage.stunDuration = stunTime; // trói lâu cho kịp bấm Q

        BossHourglass hourglass = root.AddComponent<BossHourglass>();
        hourglass.anchor = position;
        return hourglass;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        damageable = GetComponent<Damageable>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anchor = transform.position;
    }

    private void Start()
    {
        // Damageable thêm bằng code lúc chạy thì sự kiện có thể chưa được tạo sẵn như khi kéo thả trong Inspector
        if (damageable.damageableDeath == null) damageable.damageableDeath = new UnityEvent();
        if (damageable.damageableHit == null) damageable.damageableHit = new UnityEvent<int, Vector2>();
        damageable.damageableDeath.AddListener(Break);
        damageable.damageableHit.AddListener(OnHit);
        time = Random.value * 10f; // mỗi cái trôi lệch nhịp nhau
    }

    private void OnHit(int damage, Vector2 knockback)
    {
        flashTimer = 0.1f;
    }

    private void Update()
    {
        if (IsBroken) return;

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        Color color = damageable.isStunned || falling ? stunnedColor : Color.white;
        if (flashTimer > 0f) color.a = 0.5f;
        spriteRenderer.color = color;
    }

    private void FixedUpdate()
    {
        if (IsBroken || falling) return;

        // tơ kéo giật (Damageable.ApplyPull đặt thẳng vận tốc) -> chuyển sang rơi theo trọng lực
        if (damageable.isStunned && rb.linearVelocity.sqrMagnitude > 9f) // trôi lơ lửng chỉ ~1 ô/s, giật bằng tơ thì vài chục ô/s
        {
            falling = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = fallGravity;
            return;
        }

        if (damageable.isStunned)
        {
            rb.linearVelocity = Vector2.zero; // bị trói thì đứng im giữa không trung, chờ người chơi kéo
            return;
        }

        time += Time.fixedDeltaTime;
        Vector2 target = anchor + new Vector2(Mathf.Sin(time * driftSpeed) * driftRange, Mathf.Sin(time * bobSpeed) * bobHeight);
        rb.MovePosition(target);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // đang rơi mà chạm đất (hoặc bệ) là vỡ
        if (falling && collision.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            damageable.Kill(); // Kill -> damageableDeath -> Break
        }
    }

    private void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().enabled = false;

        BossController.PlaySound(breakSound);
        if (onBroken != null) onBroken(this);
        StartCoroutine(BreakEffect());
    }

    // phồng lên rồi mờ dần cho thấy vỡ, khỏi cần hình động
    private IEnumerator BreakEffect()
    {
        float t = 0f;
        Vector3 startScale = transform.localScale;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float k = t / 0.25f;
            transform.localScale = startScale * (1f + 0.6f * k);
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
