using UnityEngine;

// Điểm lưu (checkpoint): người chơi chạm vào thì cọc sáng lên, chết sau đó sẽ hồi sinh ở đây (CheckpointManager lo phần hồi sinh).
// Chạm cái mới thì cái cũ tắt - luôn hồi sinh ở điểm lưu chạm gần nhất.
// Prefab: Assets/Objects/Checkpoint.prefab - thả vào màn chơi, đặt ở đâu cũng được (tự rơi xuống mặt đất), hoặc dùng ký tự C trong file màn chữ.
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Hình cọc")]
    [TenHienThi("Hình cọc")] public SpriteRenderer flagRenderer;
    [TenHienThi("Màu chưa lưu")] public Color unlitColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    [TenHienThi("Màu đã lưu")] public Color litColor = new Color(1f, 0.85f, 0.2f, 1f);
    [TenHienThi("Hình chưa lưu")] public Sprite unlitSprite; // để trống thì giữ nguyên hình, chỉ đổi màu
    [TenHienThi("Hình đã lưu")] public Sprite litSprite;

    [Header("Âm thanh")]
    [TenHienThi("Âm thanh lưu")] public AudioClip activateSound;

    [Header("Đặt cọc")]
    [TenHienThi("Tự đặt xuống đất")] public bool snapToGround = true; // lúc vào màn tự rơi xuống mặt đất bên dưới, khỏi canh tay cho khớp ô gạch
    [TenHienThi("Tìm đất trong")] public float snapMaxDistance = 30f;

    private Collider2D area;
    private Vector2 footPosition; // chỗ chân người chơi đứng khi hồi sinh (mặt đất ngay dưới cọc)
    private bool isLit;

    public Vector2 FootPosition
    {
        get { return footPosition; }
    }

    private void Awake()
    {
        area = GetComponent<Collider2D>();
        area.isTrigger = true; // chỉ để đi xuyên qua, không chặn đường
    }

    private void Start()
    {
        footPosition = new Vector2(transform.position.x, area.bounds.min.y);
        if (snapToGround) SnapToGround();
        ApplyLook();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // chỉ tính thân người chơi còn sống (xác rơi ngang qua không tính); layer PickUp chỉ chạm được thân người chơi
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null || !player.isAlive) return;

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.Activate(this);
        }
    }

    public void SetLit(bool lit)
    {
        isLit = lit;
        ApplyLook();
    }

    public void PlayActivateSound()
    {
        // phát 2D qua CheckpointManager cho nghe rõ, chạy tiếp cũng không nhỏ dần
        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.PlaySfx(activateSound);
        }
    }

    private void ApplyLook()
    {
        if (flagRenderer == null) return;

        flagRenderer.color = isLit ? litColor : unlitColor;

        Sprite sprite = isLit ? litSprite : unlitSprite;
        if (sprite != null) flagRenderer.sprite = sprite;
    }

    // bắn tia xuống layer Ground, đặt đáy cọc lên đúng mặt đất (giống cửa qua màn)
    private void SnapToGround()
    {
        int groundMask = LayerMask.GetMask("Ground");
        if (groundMask == 0) return;

        float halfHeight = area.bounds.extents.y;
        // bắt đầu tia từ giữa cọc: lỡ đặt đáy hơi lọt xuống đất thì tia vẫn chạm mặt trên của đất
        Vector2 start = new Vector2(transform.position.x, area.bounds.center.y);
        RaycastHit2D hit = Physics2D.Raycast(start, Vector2.down, snapMaxDistance, groundMask);
        if (hit.collider == null) return;

        float centerOffset = transform.position.y - area.bounds.center.y; // phòng khi pivot không nằm giữa
        transform.position = new Vector3(transform.position.x, hit.point.y + halfHeight + centerOffset, transform.position.z);
        footPosition = new Vector2(transform.position.x, hit.point.y);
    }
}
