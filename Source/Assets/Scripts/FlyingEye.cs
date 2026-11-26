using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingEye : MonoBehaviour
{
    [TenHienThi("Tốc độ bay")] public float flightSpeed = 2f;
    [TenHienThi("Tới điểm mốc khi cách")] public float waypointReachedDistance = 0.1f;
    [TenHienThi("Vùng phát hiện")] public vungPhatHien biteDetectionZone;
    [TenHienThi("Điểm mốc")] public List<Transform> waypoints;
    [TenHienThi("Va chạm khi chết")] public Collider2D deathCollider;
    [TenHienThi("Vùng cắn")] public Collider2D biteAttackHitbox; // mặc định tắt, chỉ bật đúng lúc cắn qua Animation Event trong FlyingEye_Attack.anim

    [Header("Bổ nhào")]
    [Tooltip("Biết bổ nhào từ màn này (Màn 1 để làm quen)")]
    [TenHienThi("Từ màn")] public int diveFromLevel = 2;
    [Tooltip("Người chơi thấp hơn quái từ bấy nhiêu ô...")]
    [TenHienThi("Thấp hơn từ")] public float diveMinHeight = 1.5f;
    [Tooltip("...tới bấy nhiêu ô")]
    [TenHienThi("Thấp hơn tới")] public float diveMaxHeight = 7f;
    [Tooltip("Lệch ngang tối đa (ô)")]
    [TenHienThi("Lệch ngang tối đa")] public float diveMaxHorizontal = 5f;
    [Tooltip("Gồng bao lâu (giây) rồi mới lao - lúc người chơi chuẩn bị né")]
    [TenHienThi("Gồng")] public float diveWindup = 0.5f;
    [TenHienThi("Tốc độ lao")] public float diveSpeed = 13f;
    [Tooltip("Lao tối đa bao lâu (giây) - chưa chạm đất thì thôi, bay lên lại")]
    [TenHienThi("Lao tối đa")] public float diveMaxDuration = 1.2f;
    [Tooltip("Nghỉ giữa 2 lần bổ nhào (giây)")]
    [TenHienThi("Nghỉ")] public float diveCooldown = 5f;
    [TenHienThi("Sát thương")] public int diveDamage = 15;
    [TenHienThi("Hất văng")] public Vector2 diveKnockback = new Vector2(5f, 4f);
    [Tooltip("Cắm xuống đất thì kẹt bấy nhiêu giây (đứng yên, không đánh) - cơ hội đánh trả")]
    [TenHienThi("Kẹt đất")] public float stuckDuration = 1.5f;

    [Header("Lùi sau khi đánh")]
    [Tooltip("Cắn + quật xong 1 lượt thì bay lùi ra xa bấy nhiêu giây rồi mới quay lại tuần tra")]
    [TenHienThi("Thời gian lùi")] public float retreatDuration = 0.7f;
    [TenHienThi("Tốc độ lùi")] public float retreatSpeed = 4f;

    [Header("Âm thanh bổ nhào")]
    [TenHienThi("Cảnh báo")] public AudioClip alertSound; // lúc gồng
    [TenHienThi("Lao")] public AudioClip diveSound;  // lúc lao
    [TenHienThi("Kẹt")] public AudioClip stuckSound; // cắm xuống đất
    [Range(0f, 1f)] [TenHienThi("Âm lượng")] public float actionSoundVolume = 0.6f;

    // cho TutorialHints: đã có con nào gồng bổ nhào trong màn này chưa
    public static bool AnyDiveWarned { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AnyDiveWarned = false;
    }

    Animator animator;
    Rigidbody2D rb;
    Damageable damageable;
    HitFlash hitFlash;
    EnemyIcons icons;
    AudioSource audioSource;
    Collider2D body;

    Transform nextWaypoint;
    int waypointNum = 0;

    private enum DivePhase { None, Windup, Diving, Stuck, Retreat }
    private DivePhase divePhase = DivePhase.None;
    private float diveTimer;
    private float diveCooldownTimer;
    private float diveCheckTimer;
    private Vector2 diveDirection;
    private bool touchedGround;
    private Collider2D diveHitbox;
    private Transform player;
    private Damageable playerHealth;
    private Collider2D playerBody;
    private int groundLayer;
    private bool diveEnabled;

    // theo dõi lượt đánh cũ (cắn + quật) để biết lúc nào đánh xong 1 lượt
    private bool wasAttacking;
    private float lastAttackTime;

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

    // đang gồng / bổ nhào / kẹt dưới đất / lùi ra: tự lo di chuyển, không tuần tra, không cắn
    public bool IsBusy { get { return divePhase != DivePhase.None; } }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        damageable = GetComponent<Damageable>();
        groundLayer = LayerMask.NameToLayer("Ground");
        foreach (Collider2D candidate in GetComponents<Collider2D>())
        {
            if (!candidate.isTrigger) { body = candidate; break; }
        }

        // nháy đỏ báo đòn (không nháy trắng: quái bay có sẵn hình trúng đòn riêng)
        hitFlash = GetComponent<HitFlash>();
        if (hitFlash == null)
        {
            hitFlash = gameObject.AddComponent<HitFlash>();
            hitFlash.flashDuration = 0f;
        }
        icons = GetComponent<EnemyIcons>();
        if (icons == null) icons = gameObject.AddComponent<EnemyIcons>();

        // nháy đỏ trước nhát cắn và nhát quật của đòn đánh cũ (thời điểm lấy theo animation FlyingEye_Attack)
        EnemyAttackTelegraph telegraph = GetComponent<EnemyAttackTelegraph>();
        if (telegraph == null)
        {
            telegraph = gameObject.AddComponent<EnemyAttackTelegraph>();
            telegraph.attackStateName = AnimationStrings.flyingEyeAttackState;
            telegraph.hitMoments = new float[] { 0.382f, 0.809f };
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        CreateDiveHitbox();
    }

    private void Start()
    {
        nextWaypoint = waypoints[waypointNum];

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<Damageable>();
            playerBody = playerObject.GetComponent<CapsuleCollider2D>();
        }

        diveEnabled = GameProgress.CurrentLevel >= diveFromLevel;
        // lệch nhịp mỗi con 1 chút để không cùng lao 1 lúc
        diveCooldownTimer = Random.Range(1f, 2.5f);

        damageable.damageableHit.AddListener(OnDamaged);
    }

    private void OnEnable()
    {
        damageable.damageableDeath.AddListener(OnDeath);
    }

    // hitbox bổ nhào: con của quái, layer EnemyHitBox (chỉ chạm người chơi), chỉ bật lúc đang lao
    private void CreateDiveHitbox()
    {
        GameObject hitboxObject = new GameObject("BoNhao");
        hitboxObject.layer = LayerMask.NameToLayer("EnemyHitBox");
        hitboxObject.transform.SetParent(transform, false);

        BoxCollider2D box = hitboxObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        // gần bằng thân quái bay (thân: tâm (0.33, -0.21), rộng 1.21, cao 1.61)
        Vector2 center = new Vector2(0.33f, -0.21f);
        Vector2 size = new Vector2(1.2f, 1.6f);
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            center = capsule.offset;
            size = capsule.size;
        }
        box.offset = center;
        box.size = size * 0.9f;
        box.enabled = false;

        tanCong hit = hitboxObject.AddComponent<tanCong>();
        hit.attackDamage = diveDamage;
        hit.knockback = diveKnockback;
        diveHitbox = box;
    }

    private void Update()
    {
        // Bị trói (Stun) thì coi như không phát hiện được mục tiêu nữa, không thể ra đòn; đang bổ nhào/lùi cũng không cắn
        HasTarget = !damageable.isStunned && !IsBusy && biteDetectionZone.detectedColliders.Count > 0;

        TrackAttackRound();
    }

    // Đòn cắn cũ chạy hết 1 lượt (cắn + quật) thì lùi ra. Animator tự quay lại đầu đòn khi người chơi còn trong tầm,
    // nên "hết lượt" = thời điểm đòn tụt về đầu, hoặc vừa rời đòn khi đã gần cuối (bị đánh giữa chừng thì không tính)
    private void TrackAttackRound()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool attacking = state.IsName(AnimationStrings.flyingEyeAttackState);
        float time = state.normalizedTime;

        bool finished = false;
        if (attacking && wasAttacking)
        {
            finished = time < lastAttackTime - 0.5f || Mathf.Floor(time) > Mathf.Floor(lastAttackTime);
        }
        else if (!attacking && wasAttacking)
        {
            finished = lastAttackTime - Mathf.Floor(lastAttackTime) >= 0.9f;
        }

        wasAttacking = attacking;
        lastAttackTime = time;

        if (finished && damageable.isAlive && !damageable.isStunned && divePhase == DivePhase.None)
        {
            StartRetreat(attacking);
        }
    }

    private void FixedUpdate()
    {
        if (damageable.isAlive)
        {
            if (diveCooldownTimer > 0f) diveCooldownTimer -= Time.fixedDeltaTime;

            if (IsBusy)
            {
                UpdateDive();
            }
            // Bị trói thì đứng khựng tại chỗ trên không, không bay tuần tra tiếp - tự bay lại route cũ ngay khi hết trói
            else if (CanMove && !damageable.isStunned)
            {
                Flight();
                TryStartDive();
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        touchedGround = false;
    }

    private void Flight()
    {
        Vector2 directionToWaypoint = (nextWaypoint.position - transform.position).normalized;
        float distance = Vector2.Distance(nextWaypoint.position, transform.position);

        rb.linearVelocity = directionToWaypoint * flightSpeed;
        UpdateDirection();

        if (distance <= waypointReachedDistance)
        {
            waypointNum++;
            if (waypointNum >= waypoints.Count)
            {
                waypointNum = 0;
            }
            nextWaypoint = waypoints[waypointNum];
        }
    }

    private void UpdateDirection()
    {
        Vector3 locScale = transform.localScale;
        if (locScale.x > 0)
        {
            if (rb.linearVelocity.x < 0)
            {
                transform.localScale = new Vector3(-1 * locScale.x, locScale.y, locScale.z);
            }
        }
        else
        {
            if (rb.linearVelocity.x > 0)
            {
                transform.localScale = new Vector3(-1 * locScale.x, locScale.y, locScale.z);
            }
        }
    }

    private void FaceTowards(float directionX)
    {
        Vector3 scale = transform.localScale;
        if (directionX * scale.x < 0f) transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
    }

    // ---------- Bổ nhào ----------

    private void TryStartDive()
    {
        if (!diveEnabled || diveCooldownTimer > 0f || player == null) return;

        diveCheckTimer -= Time.fixedDeltaTime;
        if (diveCheckTimer > 0f) return;
        diveCheckTimer = 0.15f;

        if (damageable.lockVelocity || biteDetectionZone.detectedColliders.Count > 0) return; // sát người rồi thì cắn thường
        if (playerHealth != null && !playerHealth.isAlive) return;

        Vector2 from = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        Vector2 to = playerBody != null ? (Vector2)playerBody.bounds.center : (Vector2)player.position;
        float below = from.y - to.y;
        if (below < diveMinHeight || below > diveMaxHeight || Mathf.Abs(to.x - from.x) > diveMaxHorizontal) return;

        // có đất chắn giữa thì không thấy người chơi
        if (Physics2D.Linecast(from, to, LayerMask.GetMask("Ground")).collider != null) return;

        SetPhase(DivePhase.Windup, diveWindup);
        rb.linearVelocity = Vector2.zero;
        FaceTowards(to.x - from.x);
        icons.ShowAlert(diveWindup + 0.1f);
        hitFlash.Telegraph(diveWindup);
        PlaySound(alertSound);
        AnyDiveWarned = true;
    }

    private void UpdateDive()
    {
        if (damageable.isStunned)
        {
            EndDive(false);
            return;
        }

        diveTimer -= Time.fixedDeltaTime;

        switch (divePhase)
        {
            case DivePhase.Windup:
                // lùi lên 1 chút lấy đà
                rb.linearVelocity = new Vector2(0f, 1f);
                if (diveTimer <= 0f) StartDiving();
                break;

            case DivePhase.Diving:
                rb.linearVelocity = diveDirection * diveSpeed;
                // bỏ qua nhịp đầu: lúc bắt đầu lao mà đang sát tường/trần thì không tính là cắm đầu xuống đất
                if (touchedGround && diveTimer < diveMaxDuration - 0.1f)
                {
                    GetStuck();
                }
                else if (diveTimer <= 0f)
                {
                    StartRetreat(false);
                }
                break;

            case DivePhase.Stuck:
                rb.linearVelocity = Vector2.zero;
                if (diveTimer <= 0f)
                {
                    // rút đầu lên rồi lùi ra như sau lượt cắn
                    StartRetreat(false);
                }
                break;

            case DivePhase.Retreat:
                if (!CanMove || damageable.lockVelocity)
                {
                    rb.linearVelocity = Vector2.zero; // đang dáng trúng đòn thì đứng yên như cũ
                }
                else
                {
                    Vector2 away = Vector2.up;
                    if (player != null)
                    {
                        float side = transform.position.x >= player.position.x ? 1f : -1f;
                        away = new Vector2(side, 0.8f).normalized;
                    }
                    rb.linearVelocity = away * retreatSpeed;
                    UpdateDirection();
                }
                if (diveTimer <= 0f) EndDive(false);
                break;
        }
    }

    private void StartDiving()
    {
        Vector2 from = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        Vector2 to = player != null ? (playerBody != null ? (Vector2)playerBody.bounds.center : (Vector2)player.position) : from + Vector2.down;
        // nhắm chỗ người chơi đứng lúc hết gồng; luôn chúi xuống, không lao ngang
        diveDirection = (to - from).normalized;
        if (diveDirection.y > -0.35f) diveDirection = new Vector2(diveDirection.x, -0.35f).normalized;

        SetPhase(DivePhase.Diving, diveMaxDuration);
        FaceTowards(diveDirection.x);
        hitFlash.StopTelegraph();
        if (diveHitbox != null) diveHitbox.enabled = true;
        if (audioSource != null) audioSource.Stop();
        PlaySound(diveSound);
    }

    private void GetStuck()
    {
        SetPhase(DivePhase.Stuck, stuckDuration);
        rb.linearVelocity = Vector2.zero;
        icons.ShowDizzy(stuckDuration);
        ScreenShake.Shake(0.08f, 0.12f);
        Vector2 point = body != null ? new Vector2(body.bounds.center.x, body.bounds.min.y) : (Vector2)transform.position;
        HitParticles.Burst(point, new Color(0.75f, 0.68f, 0.55f, 1f), 12, 2f, 6f, 90f, 70f, 15f, 0.4f);
        PlaySound(stuckSound);
    }

    // lùi ra khỏi người chơi; leaveAttack = đang ở đầu lượt cắn mới (Animator tự lặp) thì cắt sang dáng bay luôn
    private void StartRetreat(bool leaveAttack)
    {
        if (leaveAttack)
        {
            animator.Play("FlyingEye_Flying");
            // Play() nhảy thẳng ra khỏi nhánh đánh, không qua lối thoát nên phải tự mở lại canMove
            animator.SetBool(AnimationStrings.canMove, true);
            hitFlash.StopTelegraph();
        }
        SetPhase(DivePhase.Retreat, retreatDuration);
        if (audioSource != null && leaveAttack) audioSource.Stop();
    }

    private void SetPhase(DivePhase phase, float duration)
    {
        divePhase = phase;
        diveTimer = duration;
        if (phase != DivePhase.Diving && diveHitbox != null) diveHitbox.enabled = false;
        animator.speed = phase == DivePhase.Stuck ? 0.35f : 1f; // kẹt dưới đất thì vỗ cánh uể oải
    }

    private void EndDive(bool interrupted)
    {
        if (divePhase == DivePhase.None) return;

        bool fromDive = divePhase != DivePhase.Retreat;
        bool wasWindup = divePhase == DivePhase.Windup;
        SetPhase(DivePhase.None, 0f);
        hitFlash.StopTelegraph();
        icons.HideAll();
        if ((interrupted || wasWindup) && audioSource != null) audioSource.Stop(); // tắt tiếng gồng dở

        if (fromDive) diveCooldownTimer = interrupted ? diveCooldown * 0.5f : diveCooldown;
        else if (diveCooldownTimer < 1f) diveCooldownTimer = 1f; // vừa lùi ra xong thì đừng bổ nhào ngay
    }

    // bị đánh lúc đang gồng / đang lao thì mất đà; đang kẹt dưới đất thì cứ kẹt tiếp (lúc đánh trả)
    private void OnDamaged(int damage, Vector2 knockback)
    {
        if (divePhase == DivePhase.Windup || divePhase == DivePhase.Diving)
        {
            EndDive(true);
        }
        else if (divePhase == DivePhase.Stuck)
        {
            animator.speed = 1f;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, actionSoundVolume);
    }

    // chỉ đếm va chạm với đất lúc đang lao (quái bay va chạm đất như vật thường, đi xuyên người chơi và quái khác)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == groundLayer) touchedGround = true;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.layer == groundLayer) touchedGround = true;
    }

    public void OnDeath()
    {
        EndDive(false);
        rb.gravityScale = 2f;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        deathCollider.enabled = true;
    }

    // Animation Event trong FlyingEye_Attack.anim gọi 2 hàm này: hitbox cắn chỉ bật đúng lúc cắn, chạm vào lúc bay thường không mất máu
    public void EnableBiteAttackHitbox()
    {
        if (biteAttackHitbox != null) biteAttackHitbox.enabled = true;
    }

    public void DisableBiteAttackHitbox()
    {
        if (biteAttackHitbox != null) biteAttackHitbox.enabled = false;
    }
}
