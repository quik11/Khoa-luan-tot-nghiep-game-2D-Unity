using UnityEngine;

// Vật phẩm hiếm (quyển sách, giấu trong map): nhặt vào được cộng kiến thức (mặc định 4 = bằng hạ 4 kẻ địch,
// chỉnh ở ô Knowledge Per Book của UpgradeManager). Đầy thanh kiến thức thì UpgradeManager tự mở bảng chọn nâng cấp.
// Mỗi quyển chỉ nhặt được 1 lần trong cả game: chơi lại màn thì sách đã nhặt không hiện nữa (chống cày).
// Nhấp nhô lên xuống bằng Mathf.Sin giống các vật phẩm khác, không xoay.
public class UpgradePickUp : MonoBehaviour
{
    [Header("Nhấp nhô")]
    [TenHienThi("Nhấp nhô nhanh")] public float floatSpeed = 2f;
    [TenHienThi("Nhấp nhô cao")] public float floatHeight = 0.15f;

    [Header("Âm thanh")]
    [TenHienThi("Âm thanh nhặt")] public AudioClip pickupSound;

    private Vector3 startPos;
    private bool taken = false;
    private string bookId;

    private void Start()
    {
        startPos = transform.position;

        // mã theo scene + tên + chỗ đặt sách (lấy trước khi nhấp nhô làm lệch vị trí)
        bookId = PlayerUpgrades.MakeId(gameObject.scene.name, gameObject.name, startPos);
        if (PlayerUpgrades.IsCollected(bookId))
        {
            // đã nhặt ở lần chơi trước. Destroy tới cuối khung mới xóa, nên tắt luôn + đánh dấu taken
            // kẻo người chơi đứng sẵn đè lên sách thì bước vật lý đầu tiên vẫn kịp nhặt
            taken = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        transform.position = startPos + new Vector3(0, Mathf.Sin(Time.time * floatSpeed) * floatHeight, 0);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // chỉ tính thân người chơi còn sống (hitbox đòn đánh là object con, không có PlayerController; xác rơi ngang qua không tính)
        PlayerController player = collision.GetComponent<PlayerController>();
        if (taken || player == null || !player.isAlive) return;

        taken = true;
        if (bookId != null && !PlayerUpgrades.MarkCollected(bookId))
        {
            Destroy(gameObject); // đã nhặt từ trước, không cho kiến thức nữa
            return;
        }

        if (pickupSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, Camera.main.transform.position); // phát tại camera cho nghe rõ
        }

        // UpgradeManager tự báo "+4 kiến thức (sách)" lên HUD
        bool used = UpgradeManager.Instance != null && UpgradeManager.Instance.AddBookKnowledge();
        if (!used && GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowMessage("Mọi nâng cấp đã đạt tối đa!");
        }

        Destroy(gameObject);
    }
}
