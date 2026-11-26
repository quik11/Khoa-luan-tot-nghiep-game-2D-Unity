using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TouchingDirections), typeof(Damageable))]
public class HiepSi : MonoBehaviour
{
    [TenHienThi("Tốc độ tối đa")] public float maxSpeed = 3f;
    [TenHienThi("Tăng tốc")] public float walkAcceleration = 30f;
    [TenHienThi("Hãm")] public float walkStopRate = 0.05f;
    [TenHienThi("Vùng đánh")] public vungPhatHien attackZone;
    [TenHienThi("Vùng phát hiện vực")] public vungPhatHien cliffDetectionZone;

    [Header("Âm thanh bị trói")]
    [TenHienThi("Âm thanh")] public AudioClip stunSound;
    [Range(0f, 1f)] [TenHienThi("Âm lượng")] public float stunSoundVolume = 0.7f;
    private bool wasStunned;

    // Biến thể: cùng hình Quỷ gối, khác màu + cỡ + cách đánh. LevelBuilder đặt theo ký tự trong file màn (E / S / N).
    public enum BienThe
    {
        Thuong,
        BocThep, // giáp mặt trước: đánh trước mặt bị đỡ; đánh sau lưng, đòn mạnh I (vỡ giáp), trói bằng tơ thì ăn đòn
        MatNgu   // nhanh, máu ít, lao húc liên tục ở mọi màn
    }

    [Header("Biến thể")]
    [TenHienThi("Biến thể")] public BienThe bienThe = BienThe.Thuong;

    [Header("Lao húc")]
    [Tooltip("Quỷ gối thường biết lao húc từ màn này (Màn 1 để làm quen). Mất ngủ thì màn nào cũng lao, bọc thép không lao")]
    [TenHienThi("Từ màn")] public int chargeFromLevel = 2;
    [Tooltip("Người chơi đứng trước mặt, cách xa từ bấy nhiêu ô trở lên mới lao (gần hơn thì đánh thường)")]
    [TenHienThi("Cách từ")] public float chargeMinDistance = 3f;
    [TenHienThi("Cách tới")] public float chargeMaxDistance = 8f;
    [Tooltip("Chênh lệch độ cao chân người chơi với chân quái tối đa (ô) - đứng trên bệ cao quá thì không lao")]
    [TenHienThi("Lệch độ cao tối đa")] public float chargeMaxHeightDiff = 1.5f;
    [Tooltip("Gồng bao lâu (giây) rồi mới lao - đây là lúc người chơi chuẩn bị né")]
    [TenHienThi("Gồng")] public float chargeWindup = 0.6f;
    [TenHienThi("Tốc độ lao")] public float chargeSpeed = 10f;
    [Tooltip("Lao tối đa bao lâu (giây)")]
    [TenHienThi("Lao tối đa")] public float chargeDuration = 0.9f;
    [Tooltip("Nghỉ giữa 2 lần lao (giây)")]
    [TenHienThi("Nghỉ")] public float chargeCooldown = 4f;
    [TenHienThi("Sát thương")] public int chargeDamage = 15;
    [TenHienThi("Hất văng")] public Vector2 chargeKnockback = new Vector2(8f, 4f);
    [Tooltip("Lao đâm vào tường thì choáng bấy nhiêu giây (đứng yên, không đánh) - cơ hội đánh trả")]
    [TenHienThi("Choáng khi đâm tường")] public float wallDazeDuration = 1.6f;
    [Tooltip("Lao xong (húc trúng người, tới mép vực hoặc hết đà) đứng thở bấy nhiêu giây")]
    [TenHienThi("Thở sau khi lao")] public float chargeRecovery = 0.5f;

    [Header("Âm thanh lao húc, giáp")]
    [TenHienThi("Cảnh báo")] public AudioClip alertSound;      // lúc gồng
    [TenHienThi("Lao")] public AudioClip chargeSound;     // lúc lao
    [TenHienThi("Đâm tường")] public AudioClip wallBonkSound;   // đâm tường
    [TenHienThi("Đỡ đòn")] public AudioClip guardBlockSound; // giáp đỡ đòn
    [TenHienThi("Vỡ giáp")] public AudioClip guardBreakSound; // giáp vỡ
    [Range(0f, 1f)] [TenHienThi("Âm lượng")] public float actionSoundVolume = 0.6f;

    // cho TutorialHints: đã có con nào gồng lao trong màn này chưa
    public static bool AnyChargeWarned { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AnyChargeWarned = false;
    }

    // số riêng của 2 biến thể (nhân với số gốc ở trên), màu nhuộm và cỡ
    private static readonly Color armoredTint = new Color(0.62f, 0.72f, 0.92f, 1f);
    private static readonly Color insomniacTint = new Color(0.95f, 0.62f, 0.85f, 1f);
    private const float armoredScale = 1.15f;
    private const float insomniacScale = 0.9f;

    Rigidbody2D rb;
    TouchingDirections touchingDirections;
    Animator animator;
    Damageable damageable;
    SpriteRenderer spriteRenderer;
    HitFlash hitFlash;
    EnemyIcons icons;
    AudioSource audioSource;

    private enum ChargePhase { None, Windup, Charging, Recover, Dazed }
    private ChargePhase chargePhase = ChargePhase.None;
    private float chargeTimer;
    private float chargeCooldownTimer;
    private float chargeDistance;
    private float chargeCheckTimer;
    private float dazeWobbleTimer;
    private bool chargeEnabled;
    private Collider2D chargeHitbox;
    private Transform player;
    private Damageable playerHealth;
    private Collider2D playerBody;
    private Collider2D body;
    private Color normalTint = Color.white;

    public enum TamDiChuyen
    {
        Right,
        Left
    }

    private TamDiChuyen _walkDirection;
    private Vector2 walkDirectionVector = Vector2.right;

    public TamDiChuyen walkDirection
    {
        get { return _walkDirection; }
        set
        {
            if (_walkDirection != value)
            {
                gameObject.transform.localScale = new Vector2(gameObject.transform.localScale.x * -1, gameObject.transform.localScale.y);

                if(value == TamDiChuyen.Right)
                {
                    walkDirectionVector = Vector2.right;
                }
                else if(value == TamDiChuyen.Left)
                {
                    walkDirectionVector = Vector2.left;
                }
            }
            _walkDirection = value;
        }
    }

    [TenHienThi("Thấy người chơi")] public bool _hasTarget = false;
    public bool HasTarget
    {
        get { return _hasTarget; }
        private set
        {
            _hasTarget = value;
            animator.SetBool(AnimationStrings.hasTarget, value);
        }
    }

    public bool CanMove
    {
        get { return animator.GetBool(AnimationStrings.canMove); }
    }

    public float AttackCooldown
    {
        get
        {
            return animator.GetFloat(AnimationStrings.attackCooldown);
        }
        private set
        {
            animator.SetFloat(AnimationStrings.attackCooldown, Mathf.Max(0, value));
        }
    }

    // đang gồng / lao / thở / choáng sau cú lao: tự lo di chuyển, không tuần tra, không đánh thường
    public bool IsBusyCharging { get { return chargePhase != ChargePhase.None; } }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        touchingDirections = GetComponent<TouchingDirections>();
        animator = GetComponent<Animator>();
        damageable = GetComponent<Damageable>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        foreach (Collider2D candidate in GetComponents<Collider2D>())
        {
            if (!candidate.isTrigger) { body = candidate; break; }
        }

        // nháy trắng khi trúng đòn + nháy đỏ báo đòn - prefab chưa gắn sẵn thì tự gắn
        hitFlash = GetComponent<HitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<HitFlash>();
        icons = GetComponent<EnemyIcons>();
        if (icons == null) icons = gameObject.AddComponent<EnemyIcons>();

        // nháy đỏ trước 2 nhát của đòn đánh cũ (thời điểm 2 nhát lấy theo animation HiepSi_attack)
        EnemyAttackTelegraph telegraph = GetComponent<EnemyAttackTelegraph>();
        if (telegraph == null)
        {
            telegraph = gameObject.AddComponent<EnemyAttackTelegraph>();
            telegraph.attackStateName = AnimationStrings.hiepSiAttackState;
            telegraph.hitMoments = new float[] { 0.229f, 0.855f };
        }

        // tiếng lao húc / đỡ giáp: phát 2D (không nhỏ dần theo khoảng cách tới camera), chỉ kêu khi người chơi ở gần
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Animator chưa có tham số isStunned (chưa chạy công cụ gắn dáng bị trói) thì thôi, không gửi để khỏi báo lỗi
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == AnimationStrings.isStunned) hasStunnedParameter = true;
        }

        // ô Cliff Detection Zone trên prefab đang để trống (vùng dò vực vẫn chạy nhờ sự kiện gọi OnCliffDetected) -> tự tìm
        if (cliffDetectionZone == null)
        {
            int cliffLayer = LayerMask.NameToLayer("phatHienVuc");
            foreach (vungPhatHien zone in GetComponentsInChildren<vungPhatHien>(true))
            {
                if (zone != attackZone && zone.gameObject.layer == cliffLayer) cliffDetectionZone = zone;
            }
        }

        CreateChargeHitbox();
        ApplyVariant();
    }

    private void Start()
    {
        chargeEnabled = bienThe == BienThe.MatNgu || (bienThe == BienThe.Thuong && GameProgress.CurrentLevel >= chargeFromLevel);

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<Damageable>();
            playerBody = playerObject.GetComponent<CapsuleCollider2D>();
        }

        // lệch nhịp mỗi con 1 chút để cả đàn không gồng cùng lúc
        chargeCooldownTimer = Random.Range(0.5f, 1.5f);

        damageable.guardBlocked += OnGuardBlocked;
        damageable.guardBroken += OnGuardBroken;
        damageable.damageableDeath.AddListener(OnDeath);
    }

    private void OnDestroy()
    {
        if (damageable != null)
        {
            damageable.guardBlocked -= OnGuardBlocked;
            damageable.guardBroken -= OnGuardBroken;
        }
    }

    // đổi màu, cỡ, máu, tốc độ theo biến thể. Chạy trong Awake để mọi script khác thấy số đã đổi ngay từ đầu
    private void ApplyVariant()
    {
        if (bienThe == BienThe.Thuong) return;

        float size = 1f;
        if (bienThe == BienThe.BocThep)
        {
            normalTint = armoredTint;
            size = armoredScale;
            maxSpeed *= 0.7f;
            damageable.MaxHealth = Mathf.RoundToInt(damageable.MaxHealth * 1.6f);
            damageable.frontGuard = true;
        }
        else if (bienThe == BienThe.MatNgu)
        {
            normalTint = insomniacTint;
            size = insomniacScale;
            maxSpeed *= 1.35f;
            damageable.MaxHealth = Mathf.RoundToInt(damageable.MaxHealth * 0.7f);
            chargeCooldown *= 0.5f;
            chargeWindup *= 0.7f;
            chargeSpeed *= 1.2f;
        }
        damageable.Health = damageable.MaxHealth;
        if (spriteRenderer != null) spriteRenderer.color = normalTint;

        // phóng/thu cả con (hình, thân, vùng đánh) quanh gốc rồi dời lên/xuống cho chân vẫn chạm đất như cũ
        Vector3 scale = transform.localScale;
        float feet = 0f;
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null) feet = (capsule.offset.y - capsule.size.y * 0.5f) * Mathf.Abs(scale.y);
        transform.localScale = new Vector3(scale.x * size, scale.y * size, scale.z);
        Vector3 position = transform.position + Vector3.up * (feet * (1f - size));
        transform.position = position;
        rb.position = position;
    }

    // hitbox lao húc: con của quái, layer EnemyHitBox (chỉ chạm người chơi), chỉ bật lúc đang lao
    private void CreateChargeHitbox()
    {
        GameObject hitboxObject = new GameObject("HucLao");
        hitboxObject.layer = LayerMask.NameToLayer("EnemyHitBox");
        hitboxObject.transform.SetParent(transform, false);

        BoxCollider2D box = hitboxObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        // nửa trước của thân (thân Quỷ gối: tâm (-0.08, -0.37), rộng 1.87, cao 2.15)
        Vector2 center = new Vector2(-0.08f, -0.37f);
        Vector2 size = new Vector2(1.9f, 2.1f);
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            center = capsule.offset;
            size = capsule.size;
        }
        box.offset = new Vector2(center.x + size.x * 0.3f, center.y);
        box.size = new Vector2(size.x * 0.75f, size.y * 0.85f);
        box.enabled = false;

        tanCong hit = hitboxObject.AddComponent<tanCong>();
        hit.attackDamage = chargeDamage;
        hit.knockback = chargeKnockback;
        hit.hitLanded += OnChargeHitLanded;
        chargeHitbox = box;
    }

    void Update()
    {
        // Bị trói (Stun) thì coi như không phát hiện được mục tiêu nữa, không thể ra đòn; đang lo cú lao cũng không đánh thường
        HasTarget = !damageable.isStunned && !IsBusyCharging && attackZone.detectedColliders.Count > 0;

        if(AttackCooldown > 0)
        {
             AttackCooldown -= Time.deltaTime;
        }

        UpdateStunnedPose();
    }

    // Dáng bị trói: lần đầu bị trói thì chạy hết các khung tơ bung ra rồi đứng ở khung cuối (clip không lặp).
    // Đang trói mà bị đánh thì Animator nhảy sang dáng trúng đòn 1 nhịp (để có tiếng đánh) rồi quay lại dáng trói -
    // lúc quay lại thì vào thẳng khung cuối, không bung tơ lại từ đầu.
    private bool hasStunnedParameter;
    private bool stunIntroShown;
    private bool wasInStunnedState;
    private int seenCocoonCount;

    private void UpdateStunnedPose()
    {
        // vừa bị trói (lần đầu của đợt trói này) thì kêu 1 tiếng
        if (damageable.isStunned && !wasStunned && stunSound != null)
        {
            // phát ngang hàng với camera để tiếng không bị nhỏ đi vì camera đặt lùi ra sau 10 đơn vị
            Vector3 soundPosition = transform.position;
            if (Camera.main != null) soundPosition.z = Camera.main.transform.position.z;
            AudioSource.PlayClipAtPoint(stunSound, soundPosition, stunSoundVolume);
        }

        // đang bị trói mà bị quấn kén mới (đòn mạnh I nổ kén rồi trói lại ngay): kêu lại + chạy lại dáng quấn tơ từ đầu
        bool rewrapped = damageable.isStunned && wasStunned && damageable.CocoonCount != seenCocoonCount;
        if (rewrapped && stunSound != null)
        {
            Vector3 soundPosition = transform.position;
            if (Camera.main != null) soundPosition.z = Camera.main.transform.position.z;
            AudioSource.PlayClipAtPoint(stunSound, soundPosition, stunSoundVolume);
        }
        seenCocoonCount = damageable.CocoonCount;
        wasStunned = damageable.isStunned;

        if (!hasStunnedParameter) return;

        animator.SetBool(AnimationStrings.isStunned, damageable.isStunned);

        if (!damageable.isStunned)
        {
            stunIntroShown = false;
            wasInStunnedState = false;
            return;
        }

        // kén mới: lần quay lại dáng bị trói sau nhịp trúng đòn sẽ chạy từ khung quấn tơ, không nhảy thẳng tới khung cuối
        if (rewrapped) stunIntroShown = false;

        bool inStunnedState = animator.GetCurrentAnimatorStateInfo(0).IsName(AnimationStrings.hiepSiStunnedState);
        if (inStunnedState && !wasInStunnedState && stunIntroShown)
        {
            animator.Play(AnimationStrings.hiepSiStunnedState, 0, 1f); // nhảy tới khung cuối (dáng đang bị trói)
        }
        if (inStunnedState) stunIntroShown = true;
        wasInStunnedState = inStunnedState;
    }

    private void FixedUpdate()
    {
        if (chargeCooldownTimer > 0f) chargeCooldownTimer -= Time.fixedDeltaTime;

        if (IsBusyCharging)
        {
            UpdateCharge();
            return;
        }

        if(touchingDirections.isGrounded && touchingDirections.isOnWall)
        {
            FlipDirection();
        }

        if(!damageable.lockVelocity)
        {
            // Bị trói thì không tuần tra/đuổi theo nữa - đứng khựng tại chỗ (chậm dần về 0 giống lúc không di chuyển được bình thường)
            if(!damageable.isStunned && CanMove && touchingDirections.isGrounded)
                rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocity.x + (walkAcceleration * walkDirectionVector.x * Time.fixedDeltaTime), -maxSpeed, maxSpeed), rb.linearVelocity.y);
            else
                rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0, walkStopRate), rb.linearVelocity.y);
        }

        TryStartCharge();
    }

    // ---------- Lao húc ----------

    private void TryStartCharge()
    {
        if (!chargeEnabled || chargeCooldownTimer > 0f || player == null) return;

        // dò người chơi vài lần mỗi giây là đủ, khỏi bắn tia mỗi nhịp vật lý
        chargeCheckTimer -= Time.fixedDeltaTime;
        if (chargeCheckTimer > 0f) return;
        chargeCheckTimer = 0.15f;

        if (!damageable.isAlive || damageable.isStunned || damageable.lockVelocity || !CanMove || !touchingDirections.isGrounded) return;
        if (attackZone.detectedColliders.Count > 0) return; // đứng sát rồi thì đánh thường
        if (playerHealth != null && !playerHealth.isAlive) return;

        Vector2 from = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        Vector2 to = playerBody != null ? (Vector2)playerBody.bounds.center : (Vector2)player.position;
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        float dx = (to.x - from.x) * facing; // dương = người chơi ở trước mặt
        if (dx < chargeMinDistance || dx > chargeMaxDistance) return;

        // so chân với chân: người chơi đứng ngang tầm (không trên bệ cao, không dưới hố)
        float myFeet = body != null ? body.bounds.min.y : transform.position.y;
        float playerFeet = playerBody != null ? playerBody.bounds.min.y : player.position.y;
        if (Mathf.Abs(playerFeet - myFeet) > chargeMaxHeightDiff) return;

        // có tường chắn giữa thì không thấy người chơi
        if (Physics2D.Linecast(from, to, LayerMask.GetMask("Ground")).collider != null) return;

        SetPhase(ChargePhase.Windup, chargeWindup);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        icons.ShowAlert(chargeWindup + 0.1f);
        hitFlash.Telegraph(chargeWindup);
        PlaySound(alertSound);
        AnyChargeWarned = true;
    }

    private void UpdateCharge()
    {
        // chết hoặc bị trói giữa chừng thì thôi lao
        if (!damageable.isAlive || damageable.isStunned)
        {
            EndCharge(false);
            return;
        }

        chargeTimer -= Time.fixedDeltaTime;
        float facing = transform.localScale.x >= 0f ? 1f : -1f;

        switch (chargePhase)
        {
            case ChargePhase.Windup:
                if (!damageable.lockVelocity) rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 0.3f), rb.linearVelocity.y);
                if (chargeTimer <= 0f) StartCharging();
                break;

            case ChargePhase.Charging:
                rb.linearVelocity = new Vector2(facing * chargeSpeed, rb.linearVelocity.y);
                chargeDistance += chargeSpeed * Time.fixedDeltaTime;

                if (touchingDirections.isOnWall && chargeDistance > 0.4f)
                {
                    HitWall(facing);
                }
                else if (touchingDirections.isGrounded && cliffDetectionZone != null && cliffDetectionZone.detectedColliders.Count == 0)
                {
                    // tới mép vực thì phanh gấp, không tự lao xuống
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    SetPhase(ChargePhase.Recover, chargeRecovery);
                }
                else if (chargeTimer <= 0f)
                {
                    SetPhase(ChargePhase.Recover, chargeRecovery);
                }
                break;

            case ChargePhase.Recover:
                if (!damageable.lockVelocity) rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 0.25f), rb.linearVelocity.y);
                if (chargeTimer <= 0f) EndCharge(false);
                break;

            case ChargePhase.Dazed:
                if (!damageable.lockVelocity) rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 0.1f), rb.linearVelocity.y);
                // lắc lư qua lại cho ra vẻ choáng (chỉ lật hình, không quay hướng thật)
                dazeWobbleTimer -= Time.fixedDeltaTime;
                if (dazeWobbleTimer <= 0f && spriteRenderer != null)
                {
                    dazeWobbleTimer = 0.25f;
                    spriteRenderer.flipX = !spriteRenderer.flipX;
                }
                if (chargeTimer <= 0f) EndCharge(false);
                break;
        }
    }

    private void StartCharging()
    {
        SetPhase(ChargePhase.Charging, chargeDuration);
        chargeDistance = 0f;
        hitFlash.StopTelegraph();
        if (chargeHitbox != null) chargeHitbox.enabled = true;
        if (audioSource != null) audioSource.Stop(); // tắt tiếng gồng
        PlaySound(chargeSound);
    }

    private void HitWall(float facing)
    {
        SetPhase(ChargePhase.Dazed, wallDazeDuration);
        rb.linearVelocity = new Vector2(-facing * 3f, 3f); // bật ngược ra 1 chút
        icons.ShowDizzy(wallDazeDuration);
        dazeWobbleTimer = 0.25f;
        ScreenShake.Shake(0.1f, 0.15f);
        HitParticles.Burst(body != null ? new Vector2(facing > 0 ? body.bounds.max.x : body.bounds.min.x, body.bounds.center.y) : (Vector2)transform.position,
                           new Color(0.85f, 0.8f, 0.7f, 1f), 10, 2f, 6f, facing > 0 ? 180f : 0f, 60f, 15f, 0.35f);
        PlaySound(wallBonkSound);
    }

    // húc trúng người chơi thì dừng lại thở, không lao xuyên qua
    private void OnChargeHitLanded(Damageable target)
    {
        if (chargePhase != ChargePhase.Charging) return;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetPhase(ChargePhase.Recover, chargeRecovery);
    }

    private void SetPhase(ChargePhase phase, float duration)
    {
        chargePhase = phase;
        chargeTimer = duration;

        if (phase != ChargePhase.Charging && chargeHitbox != null) chargeHitbox.enabled = false;
        if (phase != ChargePhase.Dazed && spriteRenderer != null) spriteRenderer.flipX = false;

        // chân bước chậm khi gồng, chạy nhanh khi lao, gần như đứng hình khi choáng
        float speed = 1f;
        if (phase == ChargePhase.Windup) speed = 0.35f;
        else if (phase == ChargePhase.Charging) speed = 1.8f;
        else if (phase == ChargePhase.Dazed) speed = 0.3f;
        animator.speed = speed;
    }

    private void EndCharge(bool interrupted)
    {
        if (chargePhase == ChargePhase.None) return;

        bool wasDazed = chargePhase == ChargePhase.Dazed;
        bool wasWindup = chargePhase == ChargePhase.Windup;
        SetPhase(ChargePhase.None, 0f);
        hitFlash.StopTelegraph();
        icons.HideAll();
        if ((interrupted || wasWindup) && audioSource != null) audioSource.Stop(); // tắt tiếng gồng dở

        // bị đánh ngắt lúc gồng thì sớm lao lại hơn chút; lao xong thì nghỉ đủ
        chargeCooldownTimer = interrupted ? chargeCooldown * 0.5f : chargeCooldown;
        if (wasDazed) chargeCooldownTimer = chargeCooldown;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, actionSoundVolume);
    }

    // ---------- Giáp (bọc thép) ----------

    private void OnGuardBlocked(Vector2 attackerPosition)
    {
        float side = attackerPosition.x >= transform.position.x ? 1f : -1f;
        Vector2 spark = body != null ? new Vector2(side > 0 ? body.bounds.max.x : body.bounds.min.x, body.bounds.center.y) : (Vector2)transform.position;
        HitParticles.Burst(spark, new Color(1f, 0.9f, 0.5f, 1f), 8, 4f, 9f, side > 0 ? 30f : 150f, 40f, 20f, 0.25f);
        PlaySound(guardBlockSound);
        if (GameHUD.Instance != null) GameHUD.Instance.ShowMessage("Giáp! Đánh sau lưng, dùng đòn mạnh (I) hoặc trói bằng tơ (R)");
    }

    private void OnGuardBroken()
    {
        // vỡ giáp: từ giờ là Quỷ gối thường (màu gốc, tốc độ gốc)
        normalTint = Color.white;
        if (spriteRenderer != null) spriteRenderer.color = normalTint;
        maxSpeed /= 0.7f;
        Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        HitParticles.Burst(center, new Color(0.62f, 0.72f, 0.92f, 1f), 20, 3f, 9f, 90f, 180f, 18f, 0.6f, 2);
        PlaySound(guardBreakSound);
    }

    private void OnDeath()
    {
        EndCharge(false);
    }

    private void FlipDirection()
    {
        if(walkDirection == TamDiChuyen.Right)
        {
            walkDirection = TamDiChuyen.Left;
        }
        else if(walkDirection == TamDiChuyen.Left)
        {
            walkDirection = TamDiChuyen.Right;
        }
        else
            {
            Debug.LogError("Sai hướng đi");
        }
    }

    public void onHit(int damage, Vector2 knockback)
    {
        // bị đánh lúc đang gồng hoặc đang lao thì mất đà, thôi lao (đang choáng/đang thở thì cứ choáng tiếp)
        if (chargePhase == ChargePhase.Windup || chargePhase == ChargePhase.Charging)
        {
            EndCharge(true);
        }
        else if (chargePhase == ChargePhase.Dazed)
        {
            animator.speed = 1f; // dáng trúng đòn chạy đúng tốc độ, không bị chậm theo dáng choáng
        }

        rb.linearVelocity = new Vector2(knockback.x , rb.linearVelocity.y + knockback.y);
    }

    public void OnCliffDetected()
    {
        // đang lo cú lao thì UpdateCharge tự phanh ở mép vực, không quay đầu
        if (IsBusyCharging) return;

        if (touchingDirections.isGrounded)
        {
            FlipDirection();
        }
    }
}
