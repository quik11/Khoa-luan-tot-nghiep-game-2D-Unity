using UnityEngine;

// Chạy sau Cinemachine (camera đã tới chỗ mới) và sau rung màn hình -> nền khớp đúng khung hình đang vẽ, không trễ 1 nhịp
[DefaultExecutionOrder(10001)]
public class ParallaxEffect : MonoBehaviour
{
    public enum EdgeClampMode
    {
        [InspectorName("Tự động (trời giữ cả 2 mép, lớp trước nhất giữ mép dưới)")] Auto,
        [InspectorName("Giữ cả mép trên và mép dưới")] TopAndBottom,
        [InspectorName("Chỉ giữ mép dưới")] BottomOnly,
        [InspectorName("Không giữ (như bản cũ)")] None
    }

    [Header("Phủ kín màn hình")]
    [Tooltip("Màn cao hoặc dài (leo lên, rơi xuống, đi tới cuối màn) thì mép ảnh nền có thể lọt vào khung hình, lộ khoảng trống. " +
             "Chỉ khi mép sắp lọt vào mới chặn lại, còn lại trôi y như cũ. Lớp trời (nằm sau cùng) giữ cả 2 mép; " +
             "lớp đồi (trước nhất) giữ mép dưới; lớp giữa đã có đồi che nên chỉ giữ 2 mép ngang. " +
             "Bỏ tick script này vẫn được bật lại lúc Play (đứng yên theo bản đồ nhưng giữ mép); muốn tắt hẳn thì chọn Không giữ.")]
    [TenHienThi("Giữ mép")] public EdgeClampMode edgeClamp = EdgeClampMode.Auto;
    [Tooltip("Chừa thêm bao nhiêu ô ngoài khung hình (chỉ để tránh sai số, càng lớn càng chặn sớm)")]
    [TenHienThi("Lề")] public float edgeMargin = 0.1f;

    [Header("Độ trôi (-1 = tính theo Z)")]
    [Tooltip("Lớp đi theo camera bao nhiêu phần theo chiều ngang: 1 = đứng yên trên màn hình (như ở rất xa), " +
             "0 = đứng yên theo bản đồ (như sát mặt đất). -1 = tính theo Z như cũ")]
    [TenHienThi("Trôi ngang")] public float followX = -1f;
    [Tooltip("Như trên nhưng theo chiều dọc. -1 = lấy giống chiều ngang")]
    [TenHienThi("Trôi dọc")] public float followY = -1f;
    [Tooltip("Tự trôi ngang (ô/giây), dùng cho mây. 0 = không tự trôi")]
    [TenHienThi("Tự trôi")] public float autoScrollSpeed = 0f;

    // nền theo màn gán camera của chính scene đó (lúc nạp màn kiểu chạy nền, Camera.main có thể còn là camera scene cũ)
    [HideInInspector] public Camera cameraOverride;

    private Transform cam;
    private Camera viewCamera;
    private Vector3 camStartPos;
    private Vector3 startPos;
    private float parallaxFactorX;
    private float parallaxFactorY;
    private float scrollOffset;
    private float tileWidth;        // bề rộng 1 ô lặp của ảnh, để mây tự trôi vòng lại không bị giật

    // script bị tắt sẵn trong scene (lớp trời BG1 ở mọi màn) = lớp đứng yên theo bản đồ; lúc Play vẫn bật lên để giữ mép
    private bool fixedInWorld;

    private SpriteRenderer spriteRenderer;
    private Vector3 boundsOffset;   // tâm ảnh lệch so với vị trí object
    private Vector3 boundsExtents;  // nửa chiều rộng / cao của ảnh (đã tính kích thước lát + scale)
    private bool clampTop;
    private bool clampBottom;

    // Awake vẫn được gọi khi script đang bị tắt (miễn object bật), và chỉ chạy lúc Play -> không sửa nhầm scene đang mở
    private void Awake()
    {
        if (!enabled && edgeClamp != EdgeClampMode.None)
        {
            fixedInWorld = true;
            enabled = true;
        }
    }

    private void Start()
    {
        viewCamera = cameraOverride != null ? cameraOverride : Camera.main;
        cam = viewCamera.transform;
        camStartPos = cam.position;
        startPos = transform.position;

        // Z càng lớn thì layer càng bám theo bản đồ (lướt qua màn hình nhanh, trông gần);
        // Z gần 0 thì layer trôi gần như theo camera (đứng yên trên màn hình, trông rất xa).
        // Lớp đứng yên theo bản đồ thì hệ số 0. Nền theo màn (LevelBackground) đặt thẳng hệ số qua followX/followY.
        float zFactor = fixedInWorld ? 0f : 1f / (1f + Mathf.Abs(transform.position.z));
        parallaxFactorX = followX >= 0f ? followX : zFactor;
        parallaxFactorY = followY >= 0f ? followY : parallaxFactorX;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            boundsOffset = bounds.center - transform.position;
            boundsExtents = bounds.extents;
            if (spriteRenderer.sprite != null) tileWidth = spriteRenderer.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
        }

        bool backmost = fixedInWorld || IsBackOrFrontLayer(true);
        clampTop = edgeClamp == EdgeClampMode.TopAndBottom || (edgeClamp == EdgeClampMode.Auto && backmost);
        clampBottom = edgeClamp == EdgeClampMode.TopAndBottom || edgeClamp == EdgeClampMode.BottomOnly ||
                      (edgeClamp == EdgeClampMode.Auto && (backmost || IsBackOrFrontLayer(false)));
    }

    private void LateUpdate()
    {
        Vector3 camDelta = cam.position - camStartPos;
        if (autoScrollSpeed != 0f && tileWidth > 0f)
        {
            // trôi đúng 1 ô lặp thì quay về 0: ảnh lặp liền nên nhìn không thấy chỗ quay lại
            scrollOffset = Mathf.Repeat(scrollOffset + autoScrollSpeed * Time.deltaTime, tileWidth);
        }
        Vector3 newPos = startPos + new Vector3(camDelta.x * parallaxFactorX + scrollOffset, camDelta.y * parallaxFactorY, 0f);
        newPos.z = startPos.z; // giữ nguyên z gốc, khỏi ảnh hưởng thứ tự render

        if (spriteRenderer != null && viewCamera.orthographic && edgeClamp != EdgeClampMode.None)
        {
            newPos = KeepCoveringView(newPos);
        }
        transform.position = newPos;
    }

    // Đẩy ảnh nền vừa đủ để mép không lọt vào khung hình. Mép còn xa thì không đụng tới -> chỗ nào vốn đẹp vẫn y như cũ.
    private Vector3 KeepCoveringView(Vector3 position)
    {
        Vector3 camPos = cam.position;
        float viewHalfHeight = viewCamera.orthographicSize + edgeMargin;
        float viewHalfWidth = viewCamera.orthographicSize * viewCamera.aspect + edgeMargin;
        Vector3 center = position + boundsOffset;

        // ảnh nhỏ hơn khung hình thì có chặn cũng không phủ kín được -> bỏ qua chiều đó
        if (boundsExtents.x > viewHalfWidth)
        {
            center.x = Mathf.Clamp(center.x, camPos.x + viewHalfWidth - boundsExtents.x, camPos.x - viewHalfWidth + boundsExtents.x);
        }
        // mép trên chỉ giữ được khi ảnh cao hơn khung; mép dưới thì lớp thấp cũng giữ được (đáy luôn ở dưới đáy khung)
        if (clampTop && boundsExtents.y > viewHalfHeight)
        {
            center.y = Mathf.Max(center.y, camPos.y + viewHalfHeight - boundsExtents.y);
        }
        if (clampBottom)
        {
            center.y = Mathf.Min(center.y, camPos.y - viewHalfHeight + boundsExtents.y);
        }

        Vector3 result = center - boundsOffset;
        result.z = position.z;
        return result;
    }

    // back = true: có phải lớp nằm sau cùng (vẽ trước tiên, thường là bầu trời kín cả ảnh) -> giữ cả mép trên.
    // back = false: có phải lớp trước nhất (đồi) -> giữ mép dưới. Lớp giữa (núi) mép dưới đã có đồi che, mép trên
    // trong suốt -> không cần giữ dọc, khỏi bị dời vô ích.
    private bool IsBackOrFrontLayer(bool back)
    {
        if (spriteRenderer == null) return false;
        ParallaxEffect[] layers = transform.parent != null
            ? transform.parent.GetComponentsInChildren<ParallaxEffect>(true)
            : new[] { this };

        int myLayer = SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID);
        foreach (ParallaxEffect other in layers)
        {
            if (other == this) continue;
            SpriteRenderer otherRenderer = other.GetComponent<SpriteRenderer>();
            if (otherRenderer == null) continue;
            int otherLayer = SortingLayer.GetLayerValueFromID(otherRenderer.sortingLayerID);
            int order = otherLayer != myLayer ? otherLayer - myLayer : otherRenderer.sortingOrder - spriteRenderer.sortingOrder;
            if (back ? order < 0 : order > 0) return false;
        }
        return true;
    }
}
