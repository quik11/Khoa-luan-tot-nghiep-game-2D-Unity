using UnityEngine;

// Vật phẩm hồi đạn tơ (bình tơ) - nhấp nhô lên xuống tại chỗ, chạm vào thì hồi đạn cho người chơi
public class WebAmmoPickUp : MonoBehaviour
{
    [Tooltip("Nhặt 1 bình hồi bấy nhiêu viên tơ (không vượt quá băng tơ tối đa)")]
    [TenHienThi("Hồi tơ")] public int ammoRestore = 5;

    [TenHienThi("Nhấp nhô nhanh")] public float floatSpeed = 2f; // tốc độ nhấp nhô
    [TenHienThi("Nhấp nhô cao")] public float floatHeight = 0.1f; // biên độ nhấp nhô lên xuống
    private Vector3 startPos;

    [Header("Trạm hồi tơ")]
    [Tooltip("Lớn hơn 0: nhặt xong ẩn đi, sau bấy nhiêu giây tự hiện lại. Đặt trước chỗ bắt buộc dùng tơ kéo để không bao giờ kẹt màn vì hết tơ.")]
    [TenHienThi("Hiện lại sau")] public float respawnTime = 0f;

    AudioSource pickupSource;
    private Renderer[] renderers;
    private Collider2D[] colliders;
    private float respawnTimer = 0f;
    private bool consumed = false;

    private void Awake()
    {
        pickupSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider2D>();
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    // đứng sẵn trên bình lúc đầy tơ rồi mới bắn bớt thì Enter không gọi lại nữa -> kiểm tra cả lúc đang đứng đè lên
    private void OnTriggerStay2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    private void TryPickUp(Collider2D collision)
    {
        if (consumed || respawnTimer > 0f) return; // đã nhặt / trạm hồi tơ đang ẩn chờ hiện lại

        PlayerController player = collision.GetComponent<PlayerController>();

        if (player != null && player.isAlive) // xác rơi ngang qua thì không nhặt
        {
            bool wasRefilled = player.RefillWebAmmo(ammoRestore);

            if (wasRefilled)
            {
                if (pickupSource != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSource.clip, gameObject.transform.position, pickupSource.volume);
                }

                if (respawnTime > 0f)
                {
                    // trạm hồi tơ: chỉ ẩn đi rồi hiện lại, không xóa
                    SetVisible(false);
                    respawnTimer = respawnTime;
                }
                else
                {
                    consumed = true; // Destroy tới cuối khung mới xóa, chặn nhặt 2 lần trong cùng khung
                    Destroy(gameObject);
                }
            }
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
        foreach (Collider2D c in colliders) c.enabled = visible;
    }

    private void Update()
    {
        if (respawnTimer > 0f)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) SetVisible(true);
        }

        transform.position = startPos + new Vector3(0, Mathf.Sin(Time.time * floatSpeed) * floatHeight, 0);
    }
}
