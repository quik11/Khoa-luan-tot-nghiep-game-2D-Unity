using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D), typeof(TouchingDirections), typeof(Damageable))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Di chuyển")]
    [TenHienThi("Tốc độ đi")] public float walkSpeed = 8f;
    [TenHienThi("Tốc độ trên không")] public float airWalkSpeed = 3f;
    [TenHienThi("Lực nhảy")] public float jumpImpulse = 10f;

    [Header("Đòn cận chiến")]
    // Lực lướt quán tính tự động đẩy nhân vật về phía trước khi đấm dưới đất (mặc định là 5f)
    [TenHienThi("Lực lướt khi đánh")] public float attackDashForce = 5f;

    [Header("Trọng lực khi nhảy")]
    // hệ số kéo trọng lực để nhảy dứt khoát, đỡ lơ lửng
    [TenHienThi("Hệ số khi rơi")] public float fallMultiplier = 2.5f;
    [TenHienThi("Hệ số khi nhả phím")] public float lowJumpMultiplier = 2.0f;

    [Header("Nhảy dễ hơn")]
    [TenHienThi("Nhảy trễ cho phép")] public float coyoteTime = 0.1f; // rời đất rồi vẫn nhảy được trong khoảng này
    [TenHienThi("Nhớ lệnh nhảy")] public float jumpBufferTime = 0.15f; // nhấn nhảy sớm trước khi chạm đất vẫn được ghi nhớ

    [Header("Bật tường")]
    [TenHienThi("Lực ngang")] public float wallJumpHorizontalForce = 8f;
    [TenHienThi("Lực dọc")] public float wallJumpVerticalForce = 12f;
    [TenHienThi("Khóa phím")] public float wallJumpLockDuration = 0.2f; // khoảng ngắn sau khi bật tường, không cho WASD ghi đè lực bật ra ngay

    [Header("Đòn trên không")]
    [TenHienThi("Tầm tự lao")] public float airAttackLockOnRange = 5f; // trong tầm này thì đòn air-attack tự lao vào địch gần nhất
    [TenHienThi("Lực lao")] public float airAttackLungeForce = 9f; // tốc độ lao vào địch khi có target
    [TenHienThi("Lực hất lên")] public float airAttackHopForce = 4f; // không có địch nào trong tầm thì hất nhẹ lên như phương án dự phòng

    [Header("Băng tơ")]
    [TenHienThi("Số tơ tối đa")] public int maxWebAmmo = 6;
    [TenHienThi("Cách nhau")] public float webFireCooldown = 0.4f; // giãn cách tối thiểu giữa 2 lần bắn tơ bất kỳ loại nào - phải lớn hơn invincibilityTime (0.25s) bên Damageable, không thì 2 phát tơ đạn liên tiếp sẽ không tính đủ dồn Stun

    [Header("Khựng khi bắn tơ")]
    [TenHienThi("Khựng")] public float webCastLockDuration = 0.2f; // nhảy lên bắn thì không bị khựng - chỉ áp dụng lúc đang đứng trên đất
    [TenHienThi("Trễ ra tơ")] public float webCastSpawnDelay = 0f; // khoảng thời gian từ lúc bấm bắn tới lúc tơ thực sự bắn ra - canh theo animation bắn tơ của bạn, để 0 là bắn ngay lập tức không chờ

    [SerializeField]
    [TenHienThi("Tơ hiện có")] private int currentWebAmmo;
    private float webFireCooldownTimer = 0f;
    private float webCastLockTimer = 0f;

    [Header("Tơ đạn (R)")]
    [TenHienThi("Viên tơ")] public GameObject webPelletPrefab; // gán prefab WebPellet vào đây (nếu để trống sẽ tự tái dùng projectilePrefab hiện có trên ProjectileLauncher)

    [Header("Tơ kéo (Q)")]
    [TenHienThi("Tầm")] public float webPullRange = 8f; // dài nhưng không vô tận
    [TenHienThi("Tốc độ bay")] public float webPullFlightSpeed = 22f; // tốc độ bay khi kéo người chơi tới tường/địch thường
    [TenHienThi("Bay tối đa")] public float webPullMaxDuration = 0.6f; // an toàn - lỡ chưa tới nơi cũng tự ngắt, không bay mãi
    [TenHienThi("Lực giật quái")] public float enemyPullForce = 16f; // lực giật khi kéo địch đang bị trói về phía mình
    [TenHienThi("Bám vào")] public LayerMask webPullLayerMask; // để trống - Awake() tự lấy layer Ground + Enemy theo tên, không cần tick tay trong Inspector (chỉ cần điền tay nếu muốn đổi khác)

    private bool isWebPulling = false;
    private Vector2 webPullTargetPosition;
    private float webPullTimer = 0f;

    [Header("Dây tơ kéo")]
    [Tooltip("Để trống thì game tự tạo dây. Chỉ kéo Line Renderer vào đây nếu muốn tự chỉnh dây (khi đó các ô ảnh/độ dày/màu bên dưới không dùng).")]
    [TenHienThi("Dây")] public LineRenderer webLineRenderer;
    [Tooltip("Ảnh sợi tơ tự vẽ: 1 đoạn ngắn nằm ngang (vd 16x4 px), game tự lặp lại theo chiều dài dây và lấy độ dày = chiều cao ảnh. Nhớ chỉnh ảnh: Wrap Mode = Repeat, Filter Mode = Point, Compression = None. Để trống thì dây là đường trắng.")]
    [TenHienThi("Ảnh dây")] public Texture2D webLineTexture;
    [Tooltip("Số điểm ảnh của ảnh dây trên 1 ô (để 16 giống mọi sprite trong game thì nét dây đúng cỡ pixel)")]
    [TenHienThi("Điểm ảnh/ô")] public float webLinePixelsPerUnit = 16f;
    [Tooltip("Độ dày dây (ô) khi KHÔNG có ảnh dây. Có ảnh thì tự lấy chiều cao ảnh / số điểm ảnh mỗi ô.")]
    [TenHienThi("Độ dày")] public float webLineWidth = 0.125f;
    [TenHienThi("Màu")] public Color webLineColor = Color.white;
    [Tooltip("Chỗ dây bắt đầu so với tâm nhân vật: x tính theo hướng mặt (dương = phía trước), y dương = cao hơn. Chỉnh cho trùng tay trong dáng bắn tơ.")]
    [TenHienThi("Chỗ tay")] public Vector2 webLineHandOffset = new Vector2(0.4f, 0.2f);
    [Tooltip("Giật quái hoặc bắn hụt: dây chỉ hiện chớp nhoáng trong bấy nhiêu giây")]
    [TenHienThi("Lóe")] public float webLineFlashTime = 0.15f;

    private bool webLineAutoCreated = false;
    private float webLineFlashTimer = 0f;
    private Vector2 webLineEnd;
    private Transform webLineFollow; // giật quái: đầu dây bám theo con quái đang bị kéo
    private Vector2 webLineFollowOffset;
    private readonly HashSet<string> animatorParameters = new HashSet<string>();

    ProjectileLauncher projectileLauncher;
    SpriteRenderer spriteRenderer;

    [Header("Lộn né (Shift)")]
    [TenHienThi("Tốc độ")] public float dodgeSpeed = 16f; // tốc độ lướt lúc né
    [TenHienThi("Thời gian")] public float dodgeDuration = 0.25f; // tổng thời gian cả pha né (gồm cả đoạn bất tử lẫn đoạn đuôi không còn bất tử)
    [TenHienThi("Bất tử")] public float dodgeInvincibleDuration = 0.15f; // thời gian thực sự bất tử, nằm ở đầu dodgeDuration - phần đuôi còn lại vẫn trúng đòn bình thường
    [TenHienThi("Hồi chiêu")] public float dodgeCooldown = 0.5f; // giãn cách tối thiểu giữa 2 lần né

    private bool isDodging = false;
    private float dodgeTimer = 0f;
    private float dodgeCooldownTimer = 0f;
    private float dodgeInvincibleTimer = 0f;
    private Vector2 dodgeDirection;

    [Header("Đòn mạnh (I)")]
    [TenHienThi("Hồi chiêu")] public float heavyCooldown = 7f; // giây chờ giữa 2 lần ra đòn mạnh
    [TenHienThi("Đứng yên")] public float heavyLockDuration = 1.25f; // đứng yên suốt đòn = độ dài animation attack4 (0.75s) / tốc độ state trong Animator (0.6)
    [TenHienThi("Mở khóa từ màn")] public int heavyUnlockLevel = 2; // mở khóa khi đã mở tới màn này (qua cửa Màn 1 là mở Màn 2)

    [Header("Âm thanh")]
    [TenHienThi("Nhảy")] public AudioClip jumpSound;
    [TenHienThi("Lộn né")] public AudioClip dodgeSound;
    [TenHienThi("Bắn tơ")] public AudioClip webShootSound; // bắn tơ đạn R
    [TenHienThi("Tơ kéo")] public AudioClip webPullSound;  // bắn tơ kéo Q
    [Range(0f, 1f)] [TenHienThi("Âm lượng")] public float actionSoundVolume = 0.6f;

    private AudioSource actionAudio;

    [Header("Né hoàn hảo")]
    [Tooltip("Game chậm lại còn bấy nhiêu phần tốc độ")]
    [Range(0.1f, 1f)] [TenHienThi("Chậm còn")] public float perfectDodgeSlowScale = 0.35f;
    [Tooltip("Chậm trong bấy nhiêu giây (giờ thật)")]
    [TenHienThi("Chậm trong")] public float perfectDodgeSlowTime = 0.45f;
    [Tooltip("Nhát đánh trúng kế tiếp nhân sát thương bấy nhiêu lần")]
    [TenHienThi("Nhân sát thương")] public float perfectDodgeDamageMultiplier = 2f;
    [Tooltip("Thưởng nhân sát thương giữ được bấy nhiêu giây, không đánh trúng ai thì mất")]
    [TenHienThi("Giữ thưởng")] public float perfectDodgeBonusTime = 3f;
    [Tooltip("Hoàn lại bấy nhiêu viên tơ. Để 0: chỉ có khi nhận thẻ nâng cấp \"Né hoàn hảo hồi tơ\" (thẻ tự cộng vào ô này lúc vào màn)")]
    [TenHienThi("Hồi tơ")] public int perfectDodgeAmmoRefund = 0;
    [TenHienThi("Âm thanh")] public AudioClip perfectDodgeSound;

    [Header("Đòn dậm (S + U trên không)")]
    [TenHienThi("Tốc độ lao")] public float airSlamSpeed = 20f; // tốc độ lao thẳng xuống
    [TenHienThi("Sát thương")] public int airSlamDamage = 20;
    [Tooltip("Vùng ăn đòn quanh chân lúc chạm đất: rộng x cao (ô)")]
    [TenHienThi("Vùng đánh")] public Vector2 airSlamArea = new Vector2(4f, 1.6f);
    [TenHienThi("Hất")] public float airSlamKnockback = 7f;
    [Tooltip("Chạm đất xong đứng khựng bấy nhiêu giây")]
    [TenHienThi("Khựng khi chạm đất")] public float airSlamRecovery = 0.25f;
    [TenHienThi("Hồi chiêu")] public float airSlamCooldown = 0.6f;
    [TenHienThi("Âm thanh")] public AudioClip airSlamSound;

    [Header("Phá kén, giật va chạm")]
    [Tooltip("Đòn 3 (hất tung) hoặc đòn mạnh I trúng quái đang bị trói thì kén nổ: nhát đó cộng thêm bấy nhiêu sát thương")]
    [TenHienThi("Thêm sát thương")] public int cocoonBurstBonusDamage = 10;
    [Tooltip("Quái khác đứng gần kén nổ mất bấy nhiêu máu")]
    [TenHienThi("Sát thương nổ")] public int cocoonBurstDamage = 15;
    [TenHienThi("Bán kính nổ")] public float cocoonBurstRadius = 2.5f;
    [TenHienThi("Hất")] public float cocoonBurstKnockback = 7f;
    [Tooltip("Quái vỡ kén còn bị trói thêm bấy nhiêu giây rồi thoát")]
    [TenHienThi("Trói còn lại")] public float cocoonBurstStunLeft = 0.3f;
    [TenHienThi("Âm thanh nổ")] public AudioClip cocoonBurstSound;
    [Tooltip("Quái bị tơ kéo giật về mà đâm trúng quái khác: mỗi con mất bấy nhiêu máu")]
    [TenHienThi("Sát thương va chạm")] public int pullImpactDamage = 15;
    [TenHienThi("Hất")] public float pullImpactKnockback = 8f;
    [Tooltip("Tính va chạm trong bấy nhiêu giây sau khi giật")]
    [TenHienThi("Theo dõi trong")] public float pullImpactTrackTime = 0.8f;
    [TenHienThi("Âm thanh va chạm")] public AudioClip pullImpactSound;

    private bool perfectDodgeUsed = false;      // mỗi cú lộn chỉ tính né hoàn hảo 1 lần
    private float perfectDodgeBonusTimer = 0f;  // còn thưởng x2 cho nhát kế tiếp
    private float perfectDodgeSparkTimer = 0f;
    private bool isAirSlamming = false;
    private static readonly Color slamGhostColor = new Color(0.55f, 0.9f, 1f, 0.55f);
    private static readonly Color slamStreakColor = new Color(1f, 1f, 1f, 0.8f);
    private const float slamGhostLife = 0.18f;
    private float airSlamCooldownTimer = 0f;
    private float airSlamRecoveryTimer = 0f;

    private float heavyCooldownTimer = 0f;
    private float heavyLockTimer = 0f;
    private bool heavyUnlocked = false;
    private InputAction heavyAttackAction;

    Vector2 moveInput;
    TouchingDirections touchingDirections;
    Damageable damageable;

    float originalGravityScale;

    // nút nhảy có đang giữ không để tính nhảy cao/thấp
    [SerializeField]
    [TenHienThi("Đang giữ nhảy")] private bool isJumpHeld = false;

    // khóa di chuyển riêng cho đu tơ/bị kéo sau này
    [SerializeField]
    [TenHienThi("Khóa khi bay tơ")] private bool externalMovementLock = false;

    // chờ áp lực lướt đấm ở FixedUpdate (làm luôn trong OnAttack thì canMove chưa kịp tắt, lực lướt bị đè mất)
    private bool attackDashQueued = false;

    // chỉ cho phép HandleAirAttackLanding ép state đúng 1 lần mỗi lần chạm đất, không lặp lại mỗi FixedUpdate
    [SerializeField]
    [TenHienThi("Đang sửa tiếp đất")] private bool airLandingCorrectionActive = false;

    private int stuckFallingTicks = 0; // đếm số nhịp đứng đất mà Animator vẫn kẹt dáng rơi

    [SerializeField]
    [TenHienThi("Đếm nhảy trễ")] private float coyoteTimeCounter = 0f;

    [SerializeField]
    [TenHienThi("Đếm nhớ lệnh nhảy")] private float jumpBufferCounter = 0f;

    // khóa ngắn sau khi wall-jump, không cho input thường ghi đè lực bật ra khỏi tường
    [SerializeField]
    [TenHienThi("Đếm khóa bật tường")] private float wallJumpLockTimer = 0f;

    // nhớ tên state air-attack lúc trước để biết lúc nào vừa chuyển sang 1 đòn mới (kể cả lặp vòng arielcb_3 -> arielcb), tránh hất lên liên tục mỗi frame
    private string lastAirAttackStateName = null;

    // lấy theo tên layer "Enemy" có sẵn trong project, không cần chỉnh gì thêm trong Inspector
    private LayerMask enemyLayerMask;

    // cho GameHUD đọc số đạn tơ để hiện lên màn hình
    public int CurrentWebAmmo
    {
        get { return currentWebAmmo; }
    }

    public int MaxWebAmmo
    {
        get { return maxWebAmmo; }
    }

    // cho GameHUD hiện trạng thái đòn mạnh
    public bool HeavyUnlocked
    {
        get { return heavyUnlocked; }
    }

    public float HeavyCooldownRemaining
    {
        get { return Mathf.Max(0f, heavyCooldownTimer); }
    }

    // cho bảng chỉnh thông số trong Editor mở khóa đòn mạnh ngay khi đang thử (không cần qua Màn 1)
    public void ForceUnlockHeavy()
    {
        heavyUnlocked = true;
        heavyCooldownTimer = 0f;
    }

    // hitbox của người chơi (tanCong) hỏi trước khi gây sát thương: vừa né hoàn hảo thì nhát này x2
    public float NextHitMultiplier
    {
        get { return perfectDodgeBonusTimer > 0f ? perfectDodgeDamageMultiplier : 1f; }
    }

    // đánh trúng rồi thì hết thưởng
    public void ConsumeHitBonus()
    {
        perfectDodgeBonusTimer = 0f;
    }

    // cho WebCombo phát tiếng kén nổ / va chạm qua nguồn âm của nhân vật (2D, không nhỏ đi theo khoảng cách)
    public void PlayComboSound(AudioClip clip)
    {
        PlayActionSound(clip);
    }

    public float CurrentMoveSpeed
    {
        get
        {
            // Chỉ cho phép di chuyển chủ động khi còn sống và có quyền di chuyển (canMove = true)
            if (isAlive && canMove)
            {
                if (isMoving && !touchingDirections.isOnWall)
                {
                    return touchingDirections.isGrounded ? walkSpeed : airWalkSpeed;
                }
            }
            return 0; // Trả về 0 khi đứng im, đã chết hoặc đang bị khóa di chuyển (ví dụ lúc đang đấm)
        }
    }

    [SerializeField]
    [TenHienThi("Đang di chuyển")] private bool _isMoving = false;

    public bool isMoving
    {
        get { return _isMoving; }
        private set
        {
            _isMoving = value;
            animator.SetBool(AnimationStrings.isMoving, value);
        }
    }

    [SerializeField]
    [TenHienThi("Quay phải")] private bool _isFacingRight = true;

    public bool isFacingRight
    {
        get { return _isFacingRight; }
        private set
        {
            if (_isFacingRight != value)
            {
                transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            }
            _isFacingRight = value;
        }
    }

    public bool canMove
    {
        get { return animator.GetBool(AnimationStrings.canMove); }
    }

    public bool isAlive
    {
        get { return animator.GetBool(AnimationStrings.isAlive); }
    }

    Rigidbody2D rb;
    Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // không cho thân "ngủ" khi đứng yên: thân ngủ thì Unity ngừng gọi OnTriggerStay2D, đứng sẵn trên đùi gà/bình tơ sẽ không nhặt được
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        animator = GetComponent<Animator>();
        touchingDirections = GetComponent<TouchingDirections>();
        damageable = GetComponent<Damageable>();

        originalGravityScale = rb.gravityScale; // cất gravity gốc đi để lát nhân hệ số
        enemyLayerMask = LayerMask.GetMask("Enemy"); // lấy đúng layer "Enemy" đã có sẵn trong project theo tên, khỏi phải kéo thả gì trong Inspector

        projectileLauncher = GetComponent<ProjectileLauncher>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentWebAmmo = maxWebAmmo;

        if (webPullLayerMask.value == 0)
        {
            webPullLayerMask = LayerMask.GetMask("Ground", "Enemy"); // tự lấy theo tên layer có sẵn trong project nếu chưa set tay
        }

        // nhớ Animator đang có tham số nào: animation chưa vẽ/chưa nối thì bỏ qua, khỏi báo vàng Console mỗi lần bắn tơ/né
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            animatorParameters.Add(parameter.name);
        }

        SetupWebLine();

        // trúng đòn thì nháy trắng như quái (prefab chưa gắn sẵn thì tự gắn)
        if (GetComponent<HitFlash>() == null) gameObject.AddComponent<HitFlash>();

        // nguồn phát riêng cho tiếng nhảy/né/bắn tơ, kiểu 2D (không nhỏ đi theo khoảng cách tới camera)
        actionAudio = gameObject.AddComponent<AudioSource>();
        actionAudio.playOnAwake = false;
        actionAudio.spatialBlend = 0f;

        // Animator thiếu tham số (chưa tạo hoặc gõ sai hoa/thường) thì animation tương ứng không chạy mà cũng không báo lỗi -> nhắc 1 lần
        foreach (string parameterName in new[] { AnimationStrings.webCastTrigger, AnimationStrings.dodgeTrigger, AnimationStrings.isWebPullFlying })
        {
            if (!animatorParameters.Contains(parameterName))
            {
                Debug.Log("Animator nhân vật chưa có tham số \"" + parameterName + "\" (đúng tên, phân biệt hoa thường) - animation tương ứng sẽ chưa chạy.");
            }
        }
    }

    private void Start()
    {
        heavyUnlocked = GameProgress.UnlockedLevel >= heavyUnlockLevel;

        damageable.hitWhileInvincible += OnHitWhileInvincible;

        // nghe thẳng nút HeavyAttack (phím I) của Player Input, khỏi phải nối thêm event trong Inspector.
        // Làm ở Start (không phải Awake) để Player Input đã chuẩn bị xong bộ phím của riêng nhân vật này.
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
        {
            heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");
            if (heavyAttackAction != null)
            {
                heavyAttackAction.started += OnHeavyAttackStarted;
            }
        }
    }

    private void OnDestroy()
    {
        if (heavyAttackAction != null)
        {
            heavyAttackAction.started -= OnHeavyAttackStarted;
        }

        if (damageable != null) damageable.hitWhileInvincible -= OnHitWhileInvincible;

        // material dây tơ tạo bằng code thì tự dọn
        if (webLineAutoCreated && webLineRenderer != null && webLineRenderer.sharedMaterial != null)
        {
            Destroy(webLineRenderer.sharedMaterial);
        }
    }

    private void FixedUpdate()
    {
        UpdateCoyoteTime();

        if (heavyCooldownTimer > 0f)
        {
            heavyCooldownTimer -= Time.fixedDeltaTime;
        }

        if (heavyLockTimer > 0f)
        {
            heavyLockTimer -= Time.fixedDeltaTime;
            if (heavyLockTimer <= 0f)
            {
                EndHeavyAttack();
            }
        }

        if (wallJumpLockTimer > 0f)
        {
            wallJumpLockTimer -= Time.fixedDeltaTime;
        }

        if (webCastLockTimer > 0f)
        {
            webCastLockTimer -= Time.fixedDeltaTime;
        }

        if (airSlamCooldownTimer > 0f) airSlamCooldownTimer -= Time.fixedDeltaTime;
        if (airSlamRecoveryTimer > 0f) airSlamRecoveryTimer -= Time.fixedDeltaTime;
        UpdatePerfectDodgeBonus();

        // Nếu không bị ăn đòn (lockVelocity), không bị khóa tơ (externalMovementLock), không đang trong khoảng khóa sau wall-jump, không đang khựng lại để bắn tơ, không đang né tránh và không đang ra đòn mạnh
        if (!damageable.lockVelocity && !externalMovementLock && wallJumpLockTimer <= 0f && webCastLockTimer <= 0f && !isDodging && heavyLockTimer <= 0f
            && !isAirSlamming && airSlamRecoveryTimer <= 0f)
        {
            if (canMove)
            {
                // Khi di chuyển bình thường, gán velocity theo input của người chơi
                rb.linearVelocity = new Vector2(moveInput.x * CurrentMoveSpeed, rb.linearVelocity.y);
            }
            else if (attackDashQueued)
            {
                // Đúng lúc canMove vừa tắt (vào animation đấm) mới áp lực lướt 1 lần duy nhất ở đây
                // Lấy max giữa tốc độ đang chạy và lực lướt mặc định - tránh bị tụt tốc đột ngột (khựng) nếu đang chạy nhanh sẵn mà ra đòn
                float direction = isFacingRight ? 1f : -1f;
                float dashSpeed = Mathf.Max(Mathf.Abs(rb.linearVelocity.x), attackDashForce);
                rb.linearVelocity = new Vector2(direction * dashSpeed, rb.linearVelocity.y);
                attackDashQueued = false;
            }
            else
            {
                // Khi đang chém (!canMove), chặn hoàn toàn điều khiển WASD của người chơi.
                // Đồng thời hãm phanh lực quán tính đấm trượt chậm dần về 0 để dừng mượt mà.
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0, walkSpeed * Time.fixedDeltaTime * 2.5f), rb.linearVelocity.y);
            }
        }

        HandleWebFireCooldown();
        HandleWebPullFlight();
        HandleDodge();
        HandleAirSlam();

        animator.SetFloat(AnimationStrings.yVelocity, rb.linearVelocity.y);

        ApplyJumpPhysics();
        HandleAirAttackLanding();
        HandleStuckFallingPose();
        HandleAirAttackFloat();
        HandleJumpBuffering();
    }

    // Chỉ còn đếm ngược giãn cách giữa 2 lần bắn tơ - ammo giờ không tự hồi theo giờ nữa, phải nhặt WebAmmoPickUp mới hồi
    private void HandleWebFireCooldown()
    {
        if (webFireCooldownTimer > 0f)
        {
            webFireCooldownTimer -= Time.fixedDeltaTime;
        }
    }

    // Đang giữa lúc kéo tơ (trúng tường/địch thường) thì tự lái nhân vật bay thẳng tới điểm đã trúng, không cho input WASD chen vào (externalMovementLock lo việc đó)
    private void HandleWebPullFlight()
    {
        if (!isWebPulling) return;

        webPullTimer -= Time.fixedDeltaTime;

        Vector2 toTarget = webPullTargetPosition - (Vector2)transform.position;

        if (toTarget.magnitude < 0.3f || webPullTimer <= 0f || !isAlive) // chết giữa đường thì thôi bay, xác rơi tự nhiên
        {
            isWebPulling = false;
            SetAnimatorBool(AnimationStrings.isWebPullFlying, false);
            SetExternalMovementLock(false);
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = toTarget.normalized * webPullFlightSpeed;
    }

    // Xử lý vận tốc trong lúc đang né, đếm ngược cooldown, và nhấp nháy đúng khoảng thời gian còn bất tử
    private void HandleDodge()
    {
        if (dodgeCooldownTimer > 0f)
        {
            dodgeCooldownTimer -= Time.fixedDeltaTime;
        }

        if (isDodging)
        {
            dodgeTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = dodgeDirection * dodgeSpeed;

            if (dodgeTimer <= 0f)
            {
                isDodging = false;
            }
        }

        if (dodgeInvincibleTimer > 0f)
        {
            dodgeInvincibleTimer -= Time.fixedDeltaTime;

            // nhấp nháy mờ/tỏ xen kẽ trong lúc còn bất tử, để người chơi cảm được lúc nào mình an toàn
            bool flickerOn = Mathf.FloorToInt(dodgeInvincibleTimer / 0.05f) % 2 == 0;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, flickerOn ? 1f : 0.3f);
            }
        }
        else if (spriteRenderer != null && spriteRenderer.color.a != 1f)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // hết bất tử thì trả lại bình thường
        }
    }

    private void UpdateCoyoteTime()
    {
        if (touchingDirections.isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;
        }
    }

    private void ApplyJumpPhysics()
    {
        if (externalMovementLock) return; // nhường gravity cho hệ thống đu dây sau này

        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = originalGravityScale * fallMultiplier;
        }
        else if (rb.linearVelocity.y > 0 && !isJumpHeld)
        {
            rb.gravityScale = originalGravityScale * lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = originalGravityScale;
        }
    }

    // Nhánh AirAttack trong Animator không có mũi tên nào xét isGrounded, chạm đất giữa combo vẫn tưởng đang bay
    // -> chạm đất thì ép về đứng yên ở đây.
    private void HandleAirAttackLanding()
    {
        if (externalMovementLock) return;

        if (!touchingDirections.isGrounded)
        {
            airLandingCorrectionActive = false; // rời đất thì reset, lần chạm đất sau tính lại từ đầu
            return;
        }

        if (airLandingCorrectionActive) return; // chỉ ép 1 lần, gọi Play() mỗi frame là kẹt animation

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool isStuckInAirCombo = state.IsName("player_arielcb") || state.IsName("player_arielcb_2") || state.IsName("player_arielcb_3");

        if (isStuckInAirCombo)
        {
            airLandingCorrectionActive = true;

            animator.ResetTrigger(AnimationStrings.attackTrigger);
            attackDashQueued = false; // canMove không tắt lúc air-attack nên cờ này có thể bị set nhầm mà không ai tiêu thụ, dọn cho sạch

            animator.Play("GroundStates.player_idle");
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    // Dáng rơi chỉ thoát khi vận tốc dọc nhích lên > 0 lúc chạm đất, sàn phẳng hẳn thì kẹt dáng rơi
    // -> đứng đất vài nhịp mà vẫn ở dáng rơi thì ép về đứng yên.


    private void HandleStuckFallingPose()
    {
        if (!isAlive || !touchingDirections.isGrounded || rb.linearVelocity.y > 0.1f || animator.IsInTransition(0)
            || !animator.GetCurrentAnimatorStateInfo(0).IsName("player_falling"))
        {
            stuckFallingTicks = 0;
            return;
        }

        stuckFallingTicks++;
        if (stuckFallingTicks >= 3) // 3 nhịp vật lý = 0.06 giây, đủ để không đụng lúc vừa chạm đất rồi nảy lên lại
        {
            stuckFallingTicks = 0;
            animator.Play("GroundStates.player_idle"); // đang giữ phím đi thì Animator tự sang dáng đi ở khung sau
        }
    }

    // Đòn đầu của combo trên không: có quái trong tầm thì lao tới, không có thì hất nhẹ lên


    private void HandleAirAttackFloat()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        string currentAirAttackName = null;
        if (state.IsName("player_arielcb")) currentAirAttackName = "player_arielcb";
        else if (state.IsName("player_arielcb_2")) currentAirAttackName = "player_arielcb_2";
        else if (state.IsName("player_arielcb_3")) currentAirAttackName = "player_arielcb_3";

        // chỉ đòn đầu (player_arielcb) mới lao/hất, đòn 2-3 rơi tự nhiên -> mỗi chuỗi lao đúng 1 lần, không lơ lửng mãi


        if (currentAirAttackName == "player_arielcb" && currentAirAttackName != lastAirAttackStateName)
        {
            Transform target = FindNearestEnemyInRange(airAttackLockOnRange);

            if (target != null)
            {
                Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = direction * airAttackLungeForce;
            }
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, airAttackHopForce);
            }
        }

        lastAirAttackStateName = currentAirAttackName;
    }

    // Quét quanh nhân vật trong bán kính range, trả về địch còn sống gần nhất trên layer Enemy (hoặc null nếu không có ai)
    private Transform FindNearestEnemyInRange(float range)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, enemyLayerMask);

        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            Damageable enemyDamageable = hit.GetComponent<Damageable>();
            if (enemyDamageable != null && !enemyDamageable.isAlive) continue; // bỏ qua địch đã chết

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    // Gộp về đúng 1 chỗ bắn nhảy - dùng chung cho nhảy thường/coyote/wall-jump, để bấm sớm (buffer) hay chạm đất/tường trễ đều nhận đúng như nhau
    private void HandleJumpBuffering()
    {
        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.fixedDeltaTime;
        }

        if (jumpBufferCounter <= 0f || !canMove || isDodging || isAirSlamming) return;

        if (touchingDirections.isOnWall && !touchingDirections.isGrounded)
        {
            PerformWallJump();
            jumpBufferCounter = 0f;
        }
        else if (touchingDirections.isGrounded || coyoteTimeCounter > 0f)
        {
            PerformJump();
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }
    }

    private void PlayActionSound(AudioClip clip)
    {
        if (clip != null && actionAudio != null) actionAudio.PlayOneShot(clip, actionSoundVolume);
    }

    private void PerformJump()
    {
        PlayActionSound(jumpSound);
        animator.SetTrigger(AnimationStrings.jump);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpImpulse);
    }

    private void PerformWallJump()
    {
        float awayFromWall = isFacingRight ? -1f : 1f; // TouchingDirections cast tường theo hướng mặt quay tới, nên bật thì phải ngược lại
        rb.linearVelocity = new Vector2(awayFromWall * wallJumpHorizontalForce, wallJumpVerticalForce);
        isFacingRight = !isFacingRight; // quay mặt sang hướng vừa bật tới, lỡ có tường đối diện thì bật tiếp được luôn, leo zích zắc giữa 2 tường
        animator.SetTrigger(AnimationStrings.jump);
        PlayActionSound(jumpSound);
        wallJumpLockTimer = wallJumpLockDuration;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();

        if (isAlive)
        {
            isMoving = moveInput != Vector2.zero;
            SetFacingDirection(moveInput);
        }
        else
        {
            isMoving = false;
        }
    }

    private void SetFacingDirection(Vector2 moveInput)
    {
        if (moveInput.x > 0 && !isFacingRight)
        {
            isFacingRight = true;
        }
        else if (moveInput.x < 0 && isFacingRight)
        {
            isFacingRight = false;
        }
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        // Để trống hoàn toàn vì không dùng phím Shift nữa, giữ hàm tránh lỗi liên kết
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isJumpHeld = true;
            jumpBufferCounter = jumpBufferTime; // chỉ ghi nhớ lệnh nhảy, HandleJumpBuffering() ở FixedUpdate mới thực sự bắn khi đủ điều kiện (đất/coyote/tường)
        }
        else if (context.canceled)
        {
            isJumpHeld = false;
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.started && isAlive && heavyLockTimer <= 0f && !isDodging) // Bỏ canMove ở đây để nhận phím nối combo liên tục! (đang ra đòn mạnh hoặc đang lộn né thì bỏ qua)
        {
            if (isAirSlamming || airSlamRecoveryTimer > 0f) return; // đang dậm thì dậm cho hết
            if (TryStartAirSlam()) return; // trên không + giữ S = dậm xuống thay vì đấm

            animator.SetTrigger(AnimationStrings.attackTrigger);

            // Chỉ đánh dấu cần lướt thôi, việc gán velocity thật dời xuống FixedUpdate (attackDashQueued) cho chắc ăn
            if (!externalMovementLock && touchingDirections.isGrounded)
            {
                attackDashQueued = true;
            }
        }
    }

    public void OnRangedAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            FireWebPellet();
        }
    }

    // Bắn tơ đạn - nếu đang giữa chừng đòn đấm dưới đất thì hủy đòn đấm đó luôn, không cho vừa đấm vừa bắn
    private void FireWebPellet()
    {
        if (!isAlive) return;
        if (heavyLockTimer > 0f) return; // đang ra đòn mạnh thì không chen ngang
        if (isDodging) return; // đang lộn né thì lộn cho hết, không cắt ngang dáng lộn
        if (isAirSlamming) return; // đang lao dậm xuống thì không bắn
        if (currentWebAmmo <= 0) return;
        if (webFireCooldownTimer > 0f) return;

        currentWebAmmo--;
        webFireCooldownTimer = webFireCooldown;

        // Đứng trên đất thì khựng lại như đấm thường - không cho vừa chạy/vừa đấm vừa bắn. Đang nhảy/rơi thì bỏ qua, giữ nguyên bắn thoải mái trên không.
        if (touchingDirections.isGrounded)
        {
            InterruptGroundComboIfNeeded();
            webCastLockTimer = webCastLockDuration;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        SetAnimatorTrigger(AnimationStrings.webCastTrigger);
        PlayActionSound(webShootSound);

        if (webCastSpawnDelay <= 0f)
        {
            LaunchWebPellet();
        }
        else
        {
            StartCoroutine(LaunchWebPelletAfterDelay(webCastSpawnDelay));
        }
    }

    private IEnumerator LaunchWebPelletAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LaunchWebPellet();
    }

    // Đổi tạm prefab của launcher sang tơ đạn rồi bắn, xong trả lại (dùng lại phần sinh đạn + lật hướng có sẵn)
    // chưa gán Web Pellet Prefab thì báo lỗi luôn cho dễ thấy

    private void LaunchWebPellet()
    {
        if (webPelletPrefab == null)
        {
            Debug.LogWarning("PlayerController: chưa gán 'Web Pellet Prefab' trong Inspector - tơ đạn sẽ không bắn ra gì cả. Kéo prefab WebPellet.prefab vào ô đó trên component PlayerController của Player.");
            return;
        }

        GameObject originalPrefab = projectileLauncher.projectilePrefab;
        projectileLauncher.projectilePrefab = webPelletPrefab;
        projectileLauncher.FireProjectile();
        projectileLauncher.projectilePrefab = originalPrefab;
    }

    public void OnWebPull(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            FireWebPull();
        }
    }

    // Bắn tơ kéo - cùng quy tắc với tơ đạn: đang đấm dở dưới đất thì hủy đòn đấm, khựng lại rồi mới bắn
    private void FireWebPull()
    {
        if (!isAlive) return;
        if (isAirSlamming) return; // đang lao dậm xuống thì không bắn
        if (heavyLockTimer > 0f) return; // đang ra đòn mạnh thì không chen ngang
        if (isDodging) return; // đang lộn né thì lộn cho hết, không cắt ngang dáng lộn
        if (currentWebAmmo <= 0) return;
        if (webFireCooldownTimer > 0f) return;
        if (isWebPulling) return; // đang bay dở dang thì thôi, không bắn chồng

        currentWebAmmo--;
        webFireCooldownTimer = webFireCooldown;

        if (touchingDirections.isGrounded)
        {
            InterruptGroundComboIfNeeded();
            webCastLockTimer = webCastLockDuration;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        SetAnimatorTrigger(AnimationStrings.webCastTrigger);
        PlayActionSound(webPullSound);

        if (webCastSpawnDelay <= 0f)
        {
            ExecuteWebPull();
        }
        else
        {
            StartCoroutine(ExecuteWebPullAfterDelay(webCastSpawnDelay));
        }
    }

    private IEnumerator ExecuteWebPullAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ExecuteWebPull();
    }

    // Phần bắn tơ kéo thật sự (raycast + áp lực kéo) - tách riêng để chờ đúng lúc animation nhả tơ (webCastSpawnDelay) rồi mới chạy
    private void ExecuteWebPull()
    {
        if (!isAlive) return; // tơ bắn trễ (webCastSpawnDelay) mà người chơi đã chết thì thôi

        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, webPullRange, webPullLayerMask);

        if (hit.collider == null)
        {
            // bắn hụt vào khoảng không: không có gì xảy ra thêm (tơ vẫn đã tốn từ lúc bấm, giống đòn đánh thường dù trượt vẫn mất lượt),
            // chỉ cho sợi tơ vụt ra hết tầm rồi biến mất để người chơi thấy mình bắn hụt
            FlashWebLine((Vector2)transform.position + direction * webPullRange, null);
            return;
        }

        Damageable targetDamageable = hit.collider.GetComponent<Damageable>();

        if (targetDamageable != null && targetDamageable.isStunned && targetDamageable.canBePulled)
        {
            // Địch đang bị tơ đạn trói -> kéo ngược địch về phía mình
            Vector2 pullDirection = ((Vector2)transform.position - hit.point).normalized;
            targetDamageable.ApplyPull(pullDirection * enemyPullForce);
            WebCombo.TrackPull(targetDamageable, this); // bay trúng quái khác trên đường thì cả hai cùng ăn đòn
            FlashWebLine(hit.point, hit.collider.transform);
        }
        else
        {
            // Trúng tường hoặc địch bình thường -> kéo người chơi tới đó như móc câu
            webPullTargetPosition = hit.point;
            isWebPulling = true;
            webPullTimer = webPullMaxDuration;
            SetExternalMovementLock(true);
            SetAnimatorBool(AnimationStrings.isWebPullFlying, true);
            webLineFollow = null;
            webLineEnd = hit.point;
            webLineFlashTimer = 0f;
        }
    }

    // Đang giữa chừng đòn đấm dưới đất (canMove đang khóa) thì hủy luôn đòn đó để chuyển sang bắn tơ - không cho vừa đấm vừa bắn
    private void InterruptGroundComboIfNeeded()
    {
        if (!canMove)
        {
            animator.ResetTrigger(AnimationStrings.attackTrigger);
            attackDashQueued = false; // dọn cờ lướt đấm dở dang, không để sót qua lần đấm sau
            animator.Play("GroundStates.player_idle");

            // Play() nhảy thẳng ra khỏi GroundAttack nên SetBoolBehaviour không kịp mở lại canMove, phải tự mở



            animator.SetBool(AnimationStrings.canMove, true);
        }
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            PerformDodge();
        }
    }

    // Né tránh - chỉ dùng được dưới đất. Có thể hủy đòn đang đánh dở để né (ưu tiên né hơn là đánh tiếp).
    private void PerformDodge()
    {
        if (!isAlive) return;
        if (!touchingDirections.isGrounded) return;
        if (isDodging) return;
        if (dodgeCooldownTimer > 0f) return;
        if (heavyLockTimer > 0f) return; // đòn mạnh đã ra thì phải đánh hết, không né hủy được
        if (damageable.lockVelocity) return; // đang bị ăn đòn thì không né được
        if (externalMovementLock) return; // đang bị hệ thống khác (vd tơ kéo) khóa thì không chen vào được

        InterruptGroundComboIfNeeded();

        // Né theo hướng đang giữ phím, không giữ gì thì né theo hướng đang quay mặt
        float direction = moveInput.x != 0 ? Mathf.Sign(moveInput.x) : (isFacingRight ? 1f : -1f);
        dodgeDirection = new Vector2(direction, 0f);

        isDodging = true;
        perfectDodgeUsed = false;
        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeCooldown;
        dodgeInvincibleTimer = Mathf.Max(dodgeInvincibleTimer, dodgeInvincibleDuration); // còn nhấp nháy hồi sinh thì giữ

        damageable.SetTemporaryInvincibility(dodgeInvincibleDuration);

        SetAnimatorTrigger(AnimationStrings.dodgeTrigger);
        PlayActionSound(dodgeSound);
    }

    // Đòn mạnh (phím I): gồng lâu, sát thương lớn, hất văng + trói địch (cấu hình trên hitbox playerAttack4: tanCong.stunOnHit).
    // Tạm dùng lại animation đòn 4 cũ (state chạy chậm 0.6 trong Animator) - vẽ animation riêng thì đổi AnimationStrings.heavyAttackState.
    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            PerformHeavyAttack();
        }
    }

    private void OnHeavyAttackStarted(InputAction.CallbackContext context)
    {
        PerformHeavyAttack();
    }

    private void PerformHeavyAttack()
    {
        if (!isAlive) return;

        if (!heavyUnlocked)
        {
            if (GameHUD.Instance != null)
            {
                GameHUD.Instance.ShowMessage("Đòn mạnh (I) mở khóa khi tới Màn " + heavyUnlockLevel);
            }
            return;
        }

        if (heavyCooldownTimer > 0f || heavyLockTimer > 0f) return;
        if (!touchingDirections.isGrounded) return; // chỉ ra được trên mặt đất
        if (isDodging || externalMovementLock || damageable.lockVelocity || webCastLockTimer > 0f) return;

        InterruptGroundComboIfNeeded(); // đang đấm dở thì hủy combo (hàm này cũng tự mở lại canMove)
        animator.ResetTrigger(AnimationStrings.attackTrigger);
        attackDashQueued = false;

        heavyCooldownTimer = heavyCooldown;
        heavyLockTimer = heavyLockDuration;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        // vào thẳng state bằng Play thì không đi qua lối vào của GroundAttack nên SetBoolBehaviour không khóa canMove -> tự khóa
        animator.SetBool(AnimationStrings.canMove, false);
        animator.Play(AnimationStrings.heavyAttackState);
    }

    // hết thời gian đòn mạnh: tự mở lại canMove (Play vào giữa GroundAttack không chắc SetBoolBehaviour mở lại lúc thoát,
    // không mở thì đứng kẹt 1 chỗ)
    private void EndHeavyAttack()
    {
        if (!isAlive) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inCombo = state.IsName("player_attack1") || state.IsName("player_attack2") || state.IsName("player_attack3");
        if (!inCombo)
        {
            animator.SetBool(AnimationStrings.canMove, true);
        }
    }

    public void onHit(int damage, Vector2 knockback)
    {
        isAirSlamming = false; // trúng đòn giữa lúc lao dậm thì mất đà, bị hất ra như thường
        rb.linearVelocity = new Vector2(knockback.x, rb.linearVelocity.y + knockback.y);
    }

    // ---------- Né hoàn hảo ----------

    // Damageable báo: có đòn trúng đúng lúc đang bất tử. Đang lộn né (khúc bất tử ở đầu cú lộn) thì là né hoàn hảo:
    // game chậm lại chốc lát, nhát đánh trúng kế tiếp x2, né lại được ngay (có thẻ "Né hoàn hảo hồi tơ" thì hồi thêm tơ).
    // Bất tử vì vừa hồi sinh hay vừa trúng đòn thì không tính.
    private void OnHitWhileInvincible()
    {
        if (!isDodging || perfectDodgeUsed || !isAlive) return;
        perfectDodgeUsed = true;

        perfectDodgeBonusTimer = perfectDodgeBonusTime;
        dodgeCooldownTimer = 0f;
        if (perfectDodgeAmmoRefund > 0) currentWebAmmo = Mathf.Min(currentWebAmmo + perfectDodgeAmmoRefund, maxWebAmmo); // thẻ nâng cấp

        SlowMotion.Begin(perfectDodgeSlowScale, perfectDodgeSlowTime);
        PlayActionSound(perfectDodgeSound);
        HitParticles.Burst(BodyCenter(), new Color(0.45f, 0.95f, 1f, 1f), 14, 2f, 6f, 90f, 180f, 0f, 0.45f);
        if (GameHUD.Instance != null)
        {
            string refund = perfectDodgeAmmoRefund > 0 ? ", +" + perfectDodgeAmmoRefund + " tơ" : "";
            GameHUD.Instance.ShowMessage("Né hoàn hảo! Nhát đánh kế tiếp x" + perfectDodgeDamageMultiplier.ToString("0.#") + refund);
        }
    }

    // còn thưởng x2 thì lấp lánh xanh quanh người để nhớ là đang có
    private void UpdatePerfectDodgeBonus()
    {
        if (perfectDodgeBonusTimer <= 0f) return;

        perfectDodgeBonusTimer -= Time.fixedDeltaTime;
        perfectDodgeSparkTimer -= Time.fixedDeltaTime;
        if (perfectDodgeSparkTimer <= 0f)
        {
            perfectDodgeSparkTimer = 0.12f;
            HitParticles.Burst(BodyCenter(), new Color(0.45f, 0.95f, 1f, 0.9f), 1, 0.5f, 1.5f, 90f, 180f, -2f, 0.4f);
        }
    }

    private Vector2 BodyCenter()
    {
        Collider2D body = GetComponent<CapsuleCollider2D>();
        return body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
    }

    // ---------- Đòn dậm trên không ----------

    // đang ở trên không mà giữ S (xuống) rồi bấm U: lao thẳng xuống, chạm đất thì dậm trúng mọi quái quanh chân
    private bool TryStartAirSlam()
    {
        if (touchingDirections.isGrounded || moveInput.y > -0.5f) return false;
        if (externalMovementLock || damageable.lockVelocity || airSlamCooldownTimer > 0f) return false;

        isAirSlamming = true;
        animator.ResetTrigger(AnimationStrings.attackTrigger);
        attackDashQueued = false;
        jumpBufferCounter = 0f;
        rb.linearVelocity = new Vector2(0f, -airSlamSpeed);

        // đang giữa đòn đánh trên không thì về dáng rơi (đòn trên không không khóa canMove nên không cần mở lại)
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("player_arielcb") || state.IsName("player_arielcb_2") || state.IsName("player_arielcb_3"))
        {
            animator.Play("AirStates.player_falling");
        }
        lastAirAttackStateName = null;
        return true;
    }

    private void HandleAirSlam()
    {
        if (!isAirSlamming) return;

        if (!isAlive || externalMovementLock)
        {
            isAirSlamming = false;
            return;
        }

        if (!touchingDirections.isGrounded)
        {
            rb.linearVelocity = new Vector2(0f, -airSlamSpeed);
            // dư ảnh xanh nhạt bám theo đường lao + vệt gió bay ngược lên -> nhìn ra là đang lao xuống rất nhanh
            BurstEffects.Ghost(spriteRenderer, slamGhostColor, slamGhostLife);
            Vector2 center = BodyCenter();
            HitParticles.Burst(center + new Vector2(Random.Range(-0.4f, 0.4f), 0.6f), slamStreakColor, 1, 8f, 12f, 90f, 3f, 0f, 0.12f);
            return;
        }

        // chạm đất: dậm
        isAirSlamming = false;
        airSlamRecoveryTimer = airSlamRecovery;
        airSlamCooldownTimer = airSlamCooldown;
        rb.linearVelocity = Vector2.zero;

        Collider2D body = GetComponent<CapsuleCollider2D>();
        Vector2 feet = body != null ? new Vector2(body.bounds.center.x, body.bounds.min.y) : (Vector2)transform.position;
        Vector2 areaCenter = feet + Vector2.up * (airSlamArea.y * 0.5f);

        bool anyHit = false;
        float multiplier = NextHitMultiplier;
        HashSet<Damageable> done = new HashSet<Damageable>();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(areaCenter, airSlamArea, 0f, enemyLayerMask))
        {
            Damageable target = hit.GetComponent<Damageable>();
            if (target == null || target == damageable || done.Contains(target) || !target.isAlive) continue;
            done.Add(target);

            int damage = Mathf.RoundToInt(airSlamDamage * multiplier);
            float side = target.transform.position.x >= feet.x ? 1f : -1f;
            // đòn từ trên đập xuống: gửi chỗ người đánh ngay trên đầu quái nên giáp mặt trước không đỡ được
            Vector2 above = (Vector2)target.transform.position + Vector2.up;
            if (target.Hit(damage, new Vector2(side * airSlamKnockback, airSlamKnockback * 0.6f), above, false))
            {
                anyHit = true;
            }
        }
        if (anyHit) ConsumeHitBonus();

        ScreenShake.Shake(0.22f, 0.22f);
        Color dust = new Color(0.8f, 0.75f, 0.65f, 1f);
        // sóng dẹt chạy trên mặt đất rộng đúng bằng vùng ăn đòn
        BurstEffects.Ring(feet + Vector2.up * 0.1f, airSlamArea.x * 0.5f, new Color(1f, 0.95f, 0.8f, 0.9f), 0.25f, 0.3f);
        HitParticles.Burst(feet, dust, 16, 4f, 10f, 10f, 15f, 12f, 0.45f, 2);
        HitParticles.Burst(feet, dust, 16, 4f, 10f, 170f, 15f, 12f, 0.45f, 2);
        PlayActionSound(airSlamSound);
    }

    // ---------- Sợi tơ kéo (chỉ là hình vẽ, không ảnh hưởng vật lý) ----------

    // Chưa kéo Line Renderer nào vào ô Web Line Renderer thì tự tạo 1 cái con của nhân vật,
    // dùng lại material của sprite nhân vật để dây được chiếu sáng giống nhân vật (khỏi bị hồng vì sai shader)
    private void SetupWebLine()
    {
        if (webLineRenderer == null)
        {
            GameObject lineObject = new GameObject("DayToKeo");
            lineObject.transform.SetParent(transform, false);
            webLineRenderer = lineObject.AddComponent<LineRenderer>();
            webLineAutoCreated = true;
        }

        webLineRenderer.positionCount = 2;
        webLineRenderer.useWorldSpace = true;
        webLineRenderer.enabled = false;

        if (!webLineAutoCreated) return; // dây tự gắn trong Inspector thì giữ nguyên

        webLineRenderer.startWidth = webLineWidth;
        webLineRenderer.endWidth = webLineWidth;
        webLineRenderer.startColor = webLineColor;
        webLineRenderer.endColor = webLineColor;
        webLineRenderer.numCapVertices = 0;
        webLineRenderer.alignment = LineAlignment.View;

        if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
        {
            Material lineMaterial = new Material(spriteRenderer.sharedMaterial);
            lineMaterial.mainTexture = webLineTexture != null ? webLineTexture : Texture2D.whiteTexture;
            webLineRenderer.sharedMaterial = lineMaterial;

            // vẽ cùng lớp với nhân vật, nằm ngay sau lưng nhân vật
            webLineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            webLineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        if (webLineTexture != null)
        {
            // ảnh nhập kiểu Sprite mặc định Wrap Mode = Clamp -> chế độ lặp chỉ hiện 1 đoạn rồi kéo dài cột pixel cuối, nên ép Repeat
            // (chỉ đổi trong bộ nhớ lúc chơi, không sửa file ảnh)
            webLineTexture.wrapModeU = TextureWrapMode.Repeat;
            // lặp ảnh theo chiều dài dây, mỗi lần lặp dài đúng (độ rộng ảnh / 16) ô -> nét dây đúng cỡ pixel như sprite
            webLineRenderer.textureMode = LineTextureMode.Tile;
            webLineRenderer.textureScale = new Vector2(webLinePixelsPerUnit / Mathf.Max(1, webLineTexture.width), 1f);
            float pixelWidth = webLineTexture.height / Mathf.Max(1f, webLinePixelsPerUnit);
            webLineRenderer.startWidth = pixelWidth;
            webLineRenderer.endWidth = pixelWidth;
        }
        else
        {
            webLineRenderer.textureMode = LineTextureMode.Stretch;
        }
    }

    // dây hiện chớp nhoáng (giật quái / bắn hụt); follow != null thì đầu dây bám theo object đó
    private void FlashWebLine(Vector2 endPoint, Transform follow)
    {
        webLineFollow = follow;
        webLineEnd = endPoint;
        webLineFollowOffset = follow != null ? endPoint - (Vector2)follow.position : Vector2.zero;
        webLineFlashTimer = webLineFlashTime;
    }

    // cập nhật dây sau khi mọi thứ đã di chuyển xong trong khung hình -> dây không bị trễ 1 nhịp so với nhân vật
    private void LateUpdate()
    {
        if (webLineRenderer == null) return;

        if (webLineFlashTimer > 0f)
        {
            webLineFlashTimer -= Time.deltaTime;
        }

        bool showLine = (isWebPulling || webLineFlashTimer > 0f) && isAlive;
        if (!showLine)
        {
            if (webLineRenderer.enabled) webLineRenderer.enabled = false;
            return;
        }

        Vector2 end = isWebPulling ? webPullTargetPosition : webLineEnd;
        if (!isWebPulling && webLineFollow != null)
        {
            end = (Vector2)webLineFollow.position + webLineFollowOffset;
        }

        float facing = isFacingRight ? 1f : -1f;
        Vector3 hand = transform.position + new Vector3(webLineHandOffset.x * facing, webLineHandOffset.y, 0f);

        // điểm 0 là đầu bám (ảnh dây lặp tính từ điểm 0) -> hoa văn sợi tơ đứng yên khi nhân vật bay lại gần, không bị trôi
        webLineRenderer.SetPosition(0, new Vector3(end.x, end.y, hand.z));
        webLineRenderer.SetPosition(1, hand);
        if (!webLineRenderer.enabled) webLineRenderer.enabled = true;
    }

    // Animator chưa có tham số này (chưa vẽ/chưa nối animation) thì bỏ qua, không báo lỗi
    private void SetAnimatorTrigger(string parameterName)
    {
        if (animatorParameters.Contains(parameterName)) animator.SetTrigger(parameterName);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animatorParameters.Contains(parameterName)) animator.SetBool(parameterName, value);
    }

    public void SetExternalMovementLock(bool isLocked)
    {
        externalMovementLock = isLocked;
    }

    // Gọi từ WebAmmoPickUp khi nhặt được vật phẩm hồi tơ - trả về true nếu thực sự hồi được (chưa đầy băng)
    public bool RefillWebAmmo(int amount)
    {
        if (currentWebAmmo >= maxWebAmmo) return false;

        currentWebAmmo = Mathf.Min(currentWebAmmo + amount, maxWebAmmo);
        return true;
    }

    // Hồi sinh ở điểm lưu (CheckpointManager gọi): dọn mọi trạng thái dở dang, đặt về chỗ mới, đầy máu, bất tử 1 lúc.
    // Dùng lại object cũ vì thanh máu, HUD, camera, cửa, nâng cấp đều đang giữ tham chiếu tới nó.

    public void RespawnAt(Vector2 position, float invincibleTime, bool refillAmmo)
    {
        StopAllCoroutines(); // tơ bắn trễ (webCastSpawnDelay) còn chờ thì hủy, kẻo vừa sống lại đã tự bắn/tự bay đi

        isWebPulling = false;
        webPullTimer = 0f;
        SetAnimatorBool(AnimationStrings.isWebPullFlying, false);
        webLineFlashTimer = 0f;
        webLineFollow = null;
        SetExternalMovementLock(false);
        isDodging = false;
        dodgeTimer = 0f;
        dodgeCooldownTimer = 0f;
        perfectDodgeBonusTimer = 0f;
        isAirSlamming = false;
        airSlamRecoveryTimer = 0f;
        airSlamCooldownTimer = 0f;
        heavyLockTimer = 0f;
        wallJumpLockTimer = 0f;
        webCastLockTimer = 0f;
        webFireCooldownTimer = 0f;
        attackDashQueued = false;
        airLandingCorrectionActive = false;
        lastAirAttackStateName = null;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f; // bấm Space ngay trước lúc hồi sinh thì không tự nhảy
        isJumpHeld = false;

        // AutoSyncTransforms đang tắt nên gán cả rb lẫn transform, để vật lý và camera cùng thấy vị trí mới ngay
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGravityScale;
        rb.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        // animation chết không có đường ra -> đưa Animator về trạng thái ban đầu (idle, mọi tham số mặc định,
        // xóa luôn trigger "hit" còn treo từ đòn kết liễu, không thì vừa sống lại đã bị giật như trúng đòn)
        animator.Rebind();
        animator.Update(0f);

        damageable.Revive();
        animator.SetBool(AnimationStrings.canMove, true);

        // đang giữ phím đi từ lúc chết thì đi được ngay, khỏi phải nhả ra bấm lại
        isMoving = moveInput != Vector2.zero;
        SetFacingDirection(moveInput);

        if (refillAmmo)
        {
            currentWebAmmo = maxWebAmmo;
        }

        // bất tử ngắn cho khỏi bị quái đứng chờ sẵn đánh ngay; dùng chung bộ đếm nhấp nháy của né tránh
        dodgeInvincibleTimer = invincibleTime;
        damageable.SetTemporaryInvincibility(invincibleTime);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }
}