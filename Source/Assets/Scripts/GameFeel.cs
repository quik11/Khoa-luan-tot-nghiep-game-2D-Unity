using UnityEngine;

// Gom hiệu ứng "đánh có lực" vào 1 chỗ: nghe sự kiện chung của Damageable và Projectile rồi rung màn hình + bắn hạt.
// Không gắn vào object nào: Unity tự chạy Register() lúc khởi động game. Chỉnh số ở đây nếu thấy rung/hạt quá nhiều.
public static class GameFeel
{
    // rung màn hình (ô; 1 ô = 16 điểm ảnh)
    private const float playerHitShake = 0.15f;
    private const float heavyHitShake = 0.25f;  // đòn từ 40 sát thương trở lên (đòn mạnh I)
    private const float playerDeathShake = 0.3f;
    private const float enemyDeathShake = 0.08f;
    private const int heavyDamage = 40;

    private static readonly Color sparkColor = new Color(1f, 0.95f, 0.7f, 1f);        // tia sáng khi đánh trúng
    private static readonly Color playerHurtColor = new Color(1f, 0.35f, 0.3f, 1f);   // người chơi trúng đòn
    private static readonly Color pillowFluffColor = new Color(1f, 1f, 1f, 1f);       // Quỷ gối chết: lông gối bay ra
    private static readonly Color flyingEyeColor = new Color(0.6f, 0.35f, 0.65f, 1f);
    private static readonly Color hourglassColor = new Color(0.95f, 0.8f, 0.45f, 1f); // cát vỡ ra
    private static readonly Color defaultDeathColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color webColor = new Color(0.95f, 0.95f, 1f, 0.9f);

    // sau bước xóa sự kiện static (SubsystemRegistration) mới đăng ký, Play lại trong Editor không bị đăng ký đôi
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        Damageable.AnyHit -= OnAnyHit;
        Damageable.AnyDied -= OnAnyDied;
        Projectile.Impacted -= OnProjectileImpact;
        Damageable.AnyHit += OnAnyHit;
        Damageable.AnyDied += OnAnyDied;
        Projectile.Impacted += OnProjectileImpact;
    }

    private static Vector2 CenterOf(Damageable target)
    {
        Collider2D body = target.GetComponent<Collider2D>();
        return body != null ? (Vector2)body.bounds.center : (Vector2)target.transform.position + Vector2.up;
    }

    private static void OnAnyHit(Damageable target, int damage, Vector2 knockback)
    {
        if (target == null) return;
        Vector2 center = CenterOf(target);
        bool isPlayer = target.GetComponent<PlayerController>() != null;

        // hạt bay theo hướng bị đánh văng ra (không có lực đẩy ngang thì tỏa đều lên trên)
        float direction = knockback.x > 0.01f ? 20f : knockback.x < -0.01f ? 160f : 90f;
        float spread = Mathf.Abs(knockback.x) > 0.01f ? 50f : 80f;

        if (isPlayer)
        {
            ScreenShake.Shake(playerHitShake, 0.18f);
            HitParticles.Burst(center, playerHurtColor, 6, 3f, 7f, direction, spread, 18f, 0.3f);
            return;
        }

        bool heavy = damage >= heavyDamage;
        HitParticles.Burst(center, sparkColor, heavy ? 16 : 7, heavy ? 5f : 3f, heavy ? 12f : 8f, direction, spread, 15f, 0.3f);
        if (heavy) ScreenShake.Shake(heavyHitShake, 0.22f);
    }

    private static void OnAnyDied(Damageable target)
    {
        if (target == null) return;
        Vector2 center = CenterOf(target);

        if (target.GetComponent<PlayerController>() != null)
        {
            ScreenShake.Shake(playerDeathShake, 0.3f);
            return;
        }

        if (target.GetComponent<BossController>() != null) return; // boss có màn gục ngã riêng

        Color color = defaultDeathColor;
        float gravity = 10f;
        if (target.GetComponent<HiepSi>() != null) { color = pillowFluffColor; gravity = 3f; } // lông gối bay lả tả, rơi chậm
        else if (target.GetComponent<FlyingEye>() != null) color = flyingEyeColor;
        else if (target.GetComponent<BossHourglass>() != null) color = hourglassColor;

        HitParticles.Burst(center, color, 18, 2f, 7f, 90f, 180f, gravity, 0.8f, 2);
        ScreenShake.Shake(enemyDeathShake, 0.15f);
    }

    private static void OnProjectileImpact(Vector2 point, bool isWebPellet)
    {
        if (!isWebPellet) return;
        HitParticles.Burst(point, webColor, 5, 1.5f, 4f, 90f, 180f, 6f, 0.25f);
    }
}
