using UnityEngine;

// Vùng chết ở đáy vực: nhân vật nào chạm vào (người chơi hay quái) là chết ngay, bỏ qua bất tử.
// Công cụ dựng màn tự tạo từ ký tự X trong file màn chữ. Để layer Default: chạm được thân người chơi và thân quái,
// không bị tơ kéo bám vào (tơ chỉ bắn trúng Ground/Enemy), hitbox đòn đánh và vật phẩm cũng không chạm.
[RequireComponent(typeof(Collider2D))]
public class KillZone : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        KillOwner(collision);
    }

    // phòng trường hợp đang đứng sẵn trong vùng (vd vừa hồi sinh xong mà điểm lưu đặt sai chỗ) thì vẫn tính
    private void OnTriggerStay2D(Collider2D collision)
    {
        KillOwner(collision);
    }

    private void KillOwner(Collider2D collision)
    {
        // collider có thể nằm ở object con (vd chân của quái bay) nên lấy theo Rigidbody2D gốc
        Damageable damageable = collision.attachedRigidbody != null
            ? collision.attachedRigidbody.GetComponent<Damageable>()
            : collision.GetComponentInParent<Damageable>();

        if (damageable != null)
        {
            damageable.Kill(); // đã chết rồi thì Kill tự bỏ qua
        }
    }
}
