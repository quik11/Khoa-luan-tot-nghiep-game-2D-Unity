using UnityEngine;

// Đùi gà hồi máu - nhấp nhô tại chỗ. Đầy máu thì không ăn (để dành), bị đánh mất máu rồi quay lại ăn được.
public class HealthPickup : MonoBehaviour
{
    [TenHienThi("Hồi máu")] public int healthRestore = 20;


    [TenHienThi("Nhấp nhô nhanh")] public float floatSpeed = 2f;
    [TenHienThi("Nhấp nhô cao")] public float floatHeight = 0.1f;
    private Vector3 startPos;

    AudioSource pickupSource;

    private void Awake()
    {
        pickupSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
    
        startPos = transform.position;
    }

    private bool taken = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    // đứng sẵn trên đùi gà lúc đầy máu rồi mới bị đánh thì Enter không gọi lại nữa -> kiểm tra cả lúc đang đứng đè lên
    private void OnTriggerStay2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    private void TryPickUp(Collider2D collision)
    {
        if (taken) return;

        // chỉ thân người chơi còn sống mới ăn được (đầy máu thì để lại, không ăn phí)
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null || !player.isAlive) return;

        Damageable damageable = player.GetComponent<Damageable>();
        if (damageable == null || damageable.Health >= damageable.MaxHealth) return;

        if (damageable.Heal(healthRestore))
        {
            taken = true;

            if (pickupSource != null)
            {
                AudioSource.PlayClipAtPoint(pickupSource.clip, gameObject.transform.position, pickupSource.volume);
            }

            Destroy(gameObject);
        }
    }

    private void Update()
    {

        transform.position = startPos + new Vector3(0, Mathf.Sin(Time.time * floatSpeed) * floatHeight, 0);
    }
}