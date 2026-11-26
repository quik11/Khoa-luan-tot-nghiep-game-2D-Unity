using UnityEngine;

// 1 bộ nền parallax nhiều lớp (rừng, núi tuyết, hoàng hôn...).
// File bộ nền nằm trong Assets/Resources/NenManChoi. Màn nào dùng bộ nào + ghi công tác giả: file chữ DanhSach.txt
// cùng thư mục. Lúc vào màn, LevelBackground tự ẩn nền cũ (object "Background") rồi dựng các lớp ở đây -> không sửa scene.
[CreateAssetMenu(fileName = "NenMoi", menuName = "Brand NEU day/Bộ nền parallax")]
public class BackgroundTheme : ScriptableObject
{
    [Header("Kích thước, vị trí")]
    [Tooltip("Phóng to cả bộ nền bao nhiêu lần")]
    [TenHienThi("Phóng to")] public float scale = 1f;
    [Tooltip("Lúc vào màn, đáy bộ nền nằm thấp hơn đáy khung hình bao nhiêu ô")]
    [TenHienThi("Đáy thấp hơn khung")] public float bottomBelowView = 2f;
    [Tooltip("Bộ nền rộng bao nhiêu ô (ảnh tự lặp ngang cho đủ), nên rộng hơn màn dài nhất")]
    [TenHienThi("Bề rộng")] public float width = 480f;
    [Tooltip("Màu nền camera, phòng lỡ mép ảnh lọt vào khung thì thấy màu trời chứ không phải màu xanh đậm")]
    [TenHienThi("Màu nền camera")] public Color cameraColor = Color.black;

    [Header("Các lớp (xa trước)")]
    [TenHienThi("Các lớp")] public Layer[] layers = new Layer[0];

    [System.Serializable]
    public class Layer
    {
        public Sprite sprite;
        [Tooltip("Đi theo camera bao nhiêu phần theo chiều ngang: 1 = đứng yên trên màn hình (rất xa), 0 = đứng yên theo bản đồ (rất gần)")]
        [Range(0f, 1f)] public float followX = 0.5f;
        [Tooltip("Như trên nhưng theo chiều dọc. Gần 1 thì leo cao/rơi xuống nền gần như không đổi")]
        [Range(0f, 1f)] public float followY = 0.9f;
        [Tooltip("Tự trôi ngang (ô/giây), dùng cho mây. 0 = không tự trôi")]
        public float autoScrollSpeed = 0f;
        [Tooltip("Đáy lớp này cao hơn đáy bộ nền bao nhiêu ô (tính trước khi phóng)")]
        public float yOffset = 0f;
        [Tooltip("Lệch ngang so với giữa khung hình (ô, tính trước khi phóng) - dùng cho vật đứng riêng như mặt trăng")]
        public float xOffset = 0f;
        [Tooltip("Lặp ngang cho đủ bề rộng. Tắt cho vật chỉ có 1 cái (mặt trăng)")]
        public bool tiled = true;
        [Tooltip("Giữ mép ảnh không lọt vào khung: trời giữ cả 2 mép, lớp chạm đáy (đồi, rừng) giữ mép dưới, mây lơ lửng không giữ")]
        public ParallaxEffect.EdgeClampMode edgeClamp = ParallaxEffect.EdgeClampMode.BottomOnly;
    }
}
