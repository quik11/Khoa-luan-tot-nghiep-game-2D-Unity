using System.Collections.Generic;
using UnityEngine;

// 2 mẹo kết hợp tơ, chỉ áp cho quái thường (Quỷ gối, quái bay) - boss và đồng hồ cát giữ nguyên cách chơi cũ:
//  1) Phá kén: quái đang bị trói (quấn kén tơ) trúng đòn 3 (hất tung) hoặc đòn mạnh I thì kén nổ - nhát đó mạnh hơn,
//     quái khác đứng gần cũng ăn đòn và bị hất ra. Đòn 3: quái vỡ kén sớm thoát trói; đòn mạnh: nổ xong trói lại ngay.
//  2) Giật va chạm: quái bị tơ kéo (Q) giật về mà bay trúng quái khác thì cả hai cùng mất máu, con bị đâm văng ra.
// Số chỉnh trên PlayerController (mục "Phá kén, giật va chạm"). tanCong gọi BurstCocoon (hitbox tick "Phá kén" hoặc
// "Trói khi trúng"), PlayerController gọi TrackPull;
// component này chỉ tự gắn vào con quái đang bị giật, bay xong thì tự tắt.
public class WebCombo : MonoBehaviour
{
    private static readonly Color webColor = new Color(0.95f, 0.95f, 1f, 0.95f);
    private static readonly Color impactColor = new Color(1f, 0.9f, 0.6f, 1f);
    private static readonly Color burstRingColor = new Color(0.85f, 0.97f, 1f, 0.95f);

    private Damageable self;
    private Collider2D body;
    private PlayerController player;
    private float trackTimer;
    private readonly Collider2D[] overlaps = new Collider2D[8];
    private ContactFilter2D enemyFilter;
    private readonly HashSet<Damageable> ignored = new HashSet<Damageable>(); // quái đứng chồng sẵn lúc bắt đầu giật

    public static bool IsRegularEnemy(Damageable target)
    {
        return target != null && (target.GetComponent<HiepSi>() != null || target.GetComponent<FlyingEye>() != null);
    }

    public static bool CanBurst(Damageable target)
    {
        return target != null && target.isAlive && target.IsCocooned && IsRegularEnemy(target);
    }

    // kén nổ quanh quái vừa bị đánh (nhát đánh đã cộng thêm sát thương ở tanCong)
    public static void BurstCocoon(Damageable target, PlayerController player)
    {
        if (target == null || player == null) return;

        target.BurstCocoon(player.cocoonBurstStunLeft);

        // phải nhìn ra ngay là "nổ, gây sát thương": vòng sóng loang đúng bằng vùng ăn đòn (đứng trong vòng là trúng),
        // lớp mảnh tơ dày bắn văng tới mép vòng, vài mảnh to rơi chậm, rung màn hình mạnh hơn đòn thường
        Vector2 center = CenterOf(target);
        BurstEffects.Ring(center, player.cocoonBurstRadius, burstRingColor, 0.3f);
        BurstEffects.Ring(center, player.cocoonBurstRadius * 0.6f, Color.white, 0.18f);
        HitParticles.Burst(center, webColor, 40, 5f, 14f, 90f, 180f, 6f, 0.55f, 2);
        HitParticles.Burst(center, Color.white, 12, 1.5f, 5f, 90f, 180f, 3f, 0.8f, 3);
        ScreenShake.Shake(0.22f, 0.22f);
        player.PlayComboSound(player.cocoonBurstSound);

        // mỗi con quanh đó ăn 1 lần (1 con có thể có nhiều collider), bị hất ra xa tâm nổ
        HashSet<Damageable> done = new HashSet<Damageable> { target };
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, player.cocoonBurstRadius, LayerMask.GetMask("Enemy")))
        {
            Damageable other = hit.GetComponent<Damageable>();
            if (other == null || done.Contains(other) || !other.isAlive || !IsRegularEnemy(other)) continue;
            done.Add(other);

            float side = other.transform.position.x >= center.x ? 1f : -1f;
            other.Hit(player.cocoonBurstDamage, new Vector2(side * player.cocoonBurstKnockback, player.cocoonBurstKnockback * 0.5f), true);
        }
    }

    // người chơi vừa giật quái đang trói về phía mình: theo dõi quãng bay, đâm trúng quái khác thì cả hai cùng ăn đòn
    public static void TrackPull(Damageable target, PlayerController player)
    {
        if (!IsRegularEnemy(target) || player == null) return;

        WebCombo combo = target.GetComponent<WebCombo>();
        if (combo == null) combo = target.gameObject.AddComponent<WebCombo>();
        combo.player = player;
        combo.trackTimer = player.pullImpactTrackTime;
        combo.enabled = true;
        combo.RememberStartingOverlaps();
    }

    // quái hay đi xuyên qua nhau: con nào đang đứng chồng sẵn lúc giật thì không tính là "đâm trúng",
    // không thì giật 1 con đứng cạnh con khác là va ngay tại chỗ
    private void RememberStartingOverlaps()
    {
        ignored.Clear();
        if (body == null) return;
        int count = body.Overlap(enemyFilter, overlaps);
        for (int i = 0; i < count; i++)
        {
            Damageable other = overlaps[i].GetComponent<Damageable>();
            if (other != null) ignored.Add(other);
        }
    }

    private void Awake()
    {
        self = GetComponent<Damageable>();
        foreach (Collider2D candidate in GetComponents<Collider2D>())
        {
            if (!candidate.isTrigger) { body = candidate; break; }
        }
        enemyFilter = new ContactFilter2D();
        enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
        enemyFilter.useTriggers = false;
    }

    private void FixedUpdate()
    {
        trackTimer -= Time.fixedDeltaTime;
        if (trackTimer <= 0f || self == null || !self.isAlive || body == null || player == null)
        {
            enabled = false;
            return;
        }

        // quái với quái không va chạm vật lý (đi xuyên nhau) nên phải tự dò chồng lấn
        int count = body.Overlap(enemyFilter, overlaps);
        for (int i = 0; i < count; i++)
        {
            Damageable other = overlaps[i].GetComponent<Damageable>();
            if (other == null || other == self || ignored.Contains(other) || !other.isAlive || !IsRegularEnemy(other)) continue;

            Impact(other);
            enabled = false; // mỗi lần giật chỉ đâm 1 lần
            return;
        }
    }

    private void Impact(Damageable other)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float direction = rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f ? Mathf.Sign(rb.linearVelocity.x)
                        : (other.transform.position.x >= transform.position.x ? 1f : -1f);

        Vector2 point = (CenterOf(self) + CenterOf(other)) * 0.5f;
        HitParticles.Burst(point, impactColor, 16, 3f, 8f, 90f, 180f, 12f, 0.4f, 2);
        ScreenShake.Shake(0.15f, 0.18f);
        player.PlayComboSound(player.pullImpactSound);

        // con bị đâm văng ra theo hướng bay
        other.Hit(player.pullImpactDamage, new Vector2(direction * player.pullImpactKnockback, player.pullImpactKnockback * 0.5f), true);

        // con bị giật cũng mất máu nhưng vẫn bay tiếp đúng đà cũ (giật xuống vực vẫn rơi xuống vực).
        // Đánh kiểu "không giật mình" (như boss): không chạy dáng trúng đòn nên không bật khựng (lockVelocity) -
        // khựng là quái mất phanh, bay vọt qua vực. Script quái vẫn tự đặt lại vận tốc lúc trúng đòn -> trả lại vận tốc cũ
        Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;
        bool armor = self.superArmor;
        self.superArmor = true;
        self.Hit(player.pullImpactDamage, Vector2.zero, true);
        self.superArmor = armor;
        if (rb != null) rb.linearVelocity = velocity;
    }

    private static Vector2 CenterOf(Damageable target)
    {
        Collider2D collider = target.GetComponent<Collider2D>();
        return collider != null ? (Vector2)collider.bounds.center : (Vector2)target.transform.position;
    }
}
