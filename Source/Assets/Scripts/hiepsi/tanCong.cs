using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tanCong : MonoBehaviour
{
    [TenHienThi("Sát thương")] public int attackDamage = 10;


    [TenHienThi("Hất văng")] public Vector2 knockback = Vector2.zero;

    [Header("Trói địch (đòn mạnh)")]
    [TenHienThi("Trói khi trúng")] public bool stunOnHit = false;
    [TenHienThi("Trói trong")] public float stunDuration = 2f;

    [Header("Phá giáp")]
    [Tooltip("Đánh trúng mặt trước Quỷ gối bọc thép thì đập vỡ giáp. Hitbox có tick Trói Địch (đòn mạnh I) tự tính là phá giáp")]
    [TenHienThi("Phá giáp")] public bool breaksGuard = false;

    [Header("Phá kén")]
    [Tooltip("Trúng quái đang bị trói thì kén tơ nổ, quái đứng gần cũng ăn đòn. Đang bật cho đòn 3 (hất tung). Hitbox có tick Trói Địch (đòn mạnh I) tự tính là phá kén, nổ xong trói lại ngay")]
    [TenHienThi("Phá kén")] public bool burstsCocoon = false;

    // đánh trúng ai đó (Quỷ gối lao húc nghe để dừng lại khi húc trúng người chơi)
    public event System.Action<Damageable> hitLanded;

    // hitbox của người chơi (đấm, đòn mạnh) thì có thêm: nhát x2 sau né hoàn hảo; đòn 3 + đòn mạnh còn phá kén tơ
    private PlayerController ownerPlayer;

    private void Awake()
    {
        ownerPlayer = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Damageable damageable = collision.GetComponent<Damageable>();

        if(damageable != null)
        {
            Vector2 deliveredknockback = transform.parent.localScale.x > 0 ? knockback : new Vector2(-knockback.x, knockback.y);

            int damage = attackDamage;
            bool willBurst = false;
            if (ownerPlayer != null)
            {
                damage = Mathf.RoundToInt(damage * ownerPlayer.NextHitMultiplier);
                // chỉ đòn 3 (hất tung) và đòn mạnh I phá được kén, các nhát khác đánh quái đang trói như thường
                willBurst = (burstsCocoon || stunOnHit) && WebCombo.CanBurst(damageable);
                if (willBurst) damage += ownerPlayer.cocoonBurstBonusDamage;
            }

            // gửi kèm chỗ người đánh đứng để giáp mặt trước biết đỡ hay không.
            // nhát phá kén bỏ qua bất tử 0.25 s của nhát trước (đòn 2, phát tơ thứ 2), không thì lúc nổ lúc không

            bool gotHit = damageable.Hit(damage, deliveredknockback, transform.parent.position, breaksGuard || stunOnHit, willBurst);


            if(gotHit)
            {
                if (ownerPlayer != null)
                {
                    ownerPlayer.ConsumeHitBonus();
                    if (willBurst) WebCombo.BurstCocoon(damageable, ownerPlayer);
                }

                // đòn mạnh: trói (nếu vừa nổ kén thì trói lại ngay bằng kén mới, đủ thời gian trói)
                if (stunOnHit)
                {
                    damageable.Stun(stunDuration);
                }

                hitLanded?.Invoke(damageable);

                Debug.Log(collision.name + " hit for " + damage);
            }
        }
    }
}
