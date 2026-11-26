using UnityEngine;
using UnityEngine.InputSystem;

// Nền menu trôi nhẹ theo chuột - layer càng gần (offsetMultiplier càng lớn) thì trôi càng nhiều, tạo cảm giác chiều sâu
public class MenuParallax : MonoBehaviour
{
    [TenHienThi("Độ trôi theo chuột")] public float offsetMultiplier = 1f;
    [TenHienThi("Độ mượt")] public float smoothTime = .3f;
    [Tooltip("Tự trôi ngang (ô/giây), dùng cho mây. 0 = không tự trôi")]
    [TenHienThi("Tự trôi")] public float autoScrollSpeed = 0f;

    private Vector2 startPosition;
    private Vector3 velocity;
    private Vector3 smoothPosition; // vị trí đã làm mượt theo chuột, chưa cộng phần mây tự trôi
    private float scrollOffset;
    private float tileWidth;        // bề rộng 1 ô lặp của ảnh, trôi hết 1 ô thì quay về 0 (ảnh lặp liền nên không thấy)

    private void Start()
    {
        startPosition = transform.position;
        smoothPosition = transform.position;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            tileWidth = spriteRenderer.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
        }
    }

    private void Update()
    {
        // project chỉ bật Input System mới nên Input.mousePosition sẽ báo lỗi - đọc chuột qua Mouse.current
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 offset = Camera.main.ScreenToViewportPoint(Mouse.current.position.ReadValue());

        // lấy giữa màn hình làm gốc (chuột ở giữa thì nền nằm đúng chỗ cũ), chuột ra ngoài cửa sổ thì không trôi quá đà
        offset -= new Vector2(0.5f, 0.5f);
        offset.x = Mathf.Clamp(offset.x, -0.5f, 0.5f);
        offset.y = Mathf.Clamp(offset.y, -0.5f, 0.5f);

        Vector2 target = startPosition + offset * offsetMultiplier;

        // giữ nguyên z gốc, khỏi làm lệch thứ tự các lớp nền
        smoothPosition = Vector3.SmoothDamp(smoothPosition, new Vector3(target.x, target.y, smoothPosition.z), ref velocity, smoothTime);

        // mây tự trôi cộng sau bước làm mượt, không thì lúc quay vòng về 0 sẽ thấy mây trượt ngược lại
        if (autoScrollSpeed != 0f && tileWidth > 0f)
        {
            scrollOffset = Mathf.Repeat(scrollOffset + autoScrollSpeed * Time.deltaTime, tileWidth);
        }
        transform.position = smoothPosition + new Vector3(scrollOffset, 0f, 0f);
    }
}
