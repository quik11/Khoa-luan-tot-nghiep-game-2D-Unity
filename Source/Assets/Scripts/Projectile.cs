using UnityEngine;

public class Projectile : MonoBehaviour
{
    [TenHienThi("Tốc độ bay")] public Vector2 moveSpeed = new Vector2(15f, 0);
    [TenHienThi("Sát thương")] public int damage = 10;
    [TenHienThi("Hất văng")] public Vector2 knockback = new Vector2(0, 0);
    [TenHienThi("Là tơ đạn")] public bool isWebPellet = false; // bật cái này trên prefab tơ đạn - bỏ qua invincibility + tính dồn stun

    [Header("Tự biến mất")]
    [Tooltip("Bay quá bấy nhiêu giây mà chưa trúng gì thì tự biến mất (bắn hụt không để đạn bay mãi làm nặng game)")]
    [TenHienThi("Tồn tại tối đa")] public float maxLifetime = 1.5f;
    [Tooltip("Chạm tường/đất (layer Ground) thì tan luôn, không bay xuyên qua tường trúng quái phía sau")]
    [TenHienThi("Tan khi chạm đất")] public bool destroyOnGround = true;

    // đạn tan ở đâu (chạm tường hoặc trúng địch) thì báo ra đây - hiệu ứng mảnh tơ vụn nghe sự kiện này
    public static event System.Action<Vector2, bool> Impacted; // (vị trí, có phải tơ đạn không)

    // Editor có thể tắt "Reload Domain" khi bấm Play -> sự kiện static còn giữ người nghe của lần chơi trước
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Impacted = null;
    }

    Rigidbody2D rb;
    private float lifeTimer;
    private int groundMask;
    private bool impacted; // tan rồi thì thôi, Destroy chưa chạy hết khung hình vẫn có thể dính thêm va chạm

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        groundMask = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        rb.linearVelocity = new Vector2(moveSpeed.x * transform.localScale.x, moveSpeed.y);
        lifeTimer = maxLifetime;

        // đứng sát tường mà bắn thẳng vào tường thì viên đạn sinh ra nằm luôn trong tường -> tan ngay
        if (destroyOnGround && Physics2D.OverlapPoint(rb.position, groundMask) != null)
        {
            Impact(rb.position);
        }
    }

    private void FixedUpdate()
    {
        if (impacted) return;
        lifeTimer -= Time.fixedDeltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // layer của đạn (PlayerHitBox) không va chạm với Ground trong ma trận va chạm, nên tự dò trước quãng sắp bay trong nhịp này
        if (destroyOnGround)
        {
            Vector2 velocity = rb.linearVelocity;
            float distance = velocity.magnitude * Time.fixedDeltaTime;
            if (distance > 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(rb.position, velocity.normalized, distance, groundMask);
                if (hit.collider != null)
                {
                    Impact(hit.point);
                }
            }
        }
    }

    private void Impact(Vector2 point)
    {
        if (impacted) return;
        impacted = true;
        Impacted?.Invoke(point, isWebPellet);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (impacted) return;
        Damageable damageable = collision.GetComponent<Damageable>();

        if (damageable != null)
        {
            Vector2 deliveredKnockback = transform.localScale.x > 0 ? knockback : new Vector2(-knockback.x, knockback.y);

            bool gotHit = damageable.Hit(damage, deliveredKnockback, isWebPellet);

            if (gotHit)
            {
                if (isWebPellet)
                {
                    damageable.RegisterWebHit();
                }

                Impact(rb.position);
            }
        }
    }
}
