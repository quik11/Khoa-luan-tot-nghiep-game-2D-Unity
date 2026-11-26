using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss màn cuối "Deadline cận kề" - máy trạng thái hữu hạn (FSM) điều khiển bằng code.
// Vòng lặp: nghỉ -> chọn đòn (chém / phép bàn tay / dịch chuyển rồi chém / đi tới rồi chém) -> ra đòn -> nghỉ...
// Mỗi đòn đều có lúc báo trước (vung tay chậm, đổi màu đỏ, vạch đỏ dưới đất) để người chơi kịp né.
// 3 phase theo máu: hết 40% máu -> phase 2, còn 30% -> phase 3. Đầu phase 2 và 3 boss bật khiên, thả đồng hồ cát
// (phase 2: 2 cái, phase 3: 3 cái): phá hết trong thời hạn thì khiên vỡ + boss choáng, hết giờ thì "Quá hạn!" - sóng quét cả sàn.
// Phase 3 vẫn dáng phase 2 nhưng dồn đòn: nghỉ ngắn, niệm nhiều bàn tay hơn, chém xong giáng tay luôn, dịch chuyển nhiều hơn
// và có chuỗi dịch chuyển chém liên tiếp (đoạn phải tập trung né), hết chuỗi boss đứng thở - lúc đánh trả.
// Người chơi chết: hồi sinh ở điểm lưu trong sân, boss quay về đầu phase đang đánh (không phải đánh lại từ đầu).
// Hạ boss: rơi ra Tấm bằng Cử nhân xuất sắc, nhặt là hết game.
// Hình động phát bằng code theo danh sách khung hình (không dùng Animator) để canh đúng khung nào gây sát thương.
[RequireComponent(typeof(Rigidbody2D), typeof(Damageable))]
public class BossController : MonoBehaviour
{
    public enum BossState { Waiting, Intro, Idle, Walk, Slash, Cast, Teleport, Shield, Stunned, PlayerDown, Dead, BlinkChain }

    [Header("Boss")]
    [TenHienThi("Tên boss")] public string bossName = "DEADLINE CẬN KỀ";
    [Tooltip("Hình gốc quay mặt sang phải thì tick, quay sang trái thì bỏ tick (Bringer of Death quay trái)")]
    [TenHienThi("Hình quay phải")] public bool artFacesRight = false;
    [TenHienThi("Hình")] public SpriteRenderer artRenderer;

    [Header("Khung hình")]
    [TenHienThi("Đứng")] public Sprite[] idleFrames;
    [TenHienThi("Đi")] public Sprite[] walkFrames;
    [TenHienThi("Chém")] public Sprite[] attackFrames;
    [TenHienThi("Trúng đòn")] public Sprite[] hurtFrames;
    [TenHienThi("Gục")] public Sprite[] deathFrames;
    [TenHienThi("Niệm phép")] public Sprite[] castFrames;
    [TenHienThi("Bàn tay")] public Sprite[] spellFrames;
    [Tooltip("Số khung hình mỗi giây ở phase 1 (phase sau tự nhanh hơn)")]
    [TenHienThi("Khung/giây")] public float frameRate = 10f;

    [Header("Phase")]
    [Tooltip("Còn bao nhiêu phần máu thì sang phase 2 (0.6 = 60%)")]
    [Range(0.1f, 0.95f)] [TenHienThi("Sang phase 2")] public float phase2At = 0.6f;
    [Range(0.05f, 0.9f)] [TenHienThi("Sang phase 3")] public float phase3At = 0.3f;
    [Tooltip("Phase 2 nhanh hơn bao nhiêu lần (đi, ra đòn, nghỉ)")]
    [TenHienThi("Tốc phase 2")] public float phase2Speed = 1.2f;
    [TenHienThi("Tốc phase 3")] public float phase3Speed = 1.35f;

    [Header("Di chuyển")]
    [TenHienThi("Tốc độ đi")] public float walkSpeed = 3f;
    [TenHienThi("Đi tối đa")] public float maxWalkTime = 2.5f;
    [Tooltip("Giây nghỉ giữa 2 đòn ở phase 1")]
    [TenHienThi("Nghỉ giữa đòn")] public float idleTime = 1f;

    [Header("Đòn chém")]
    [TenHienThi("Vùng chém")] public BoxCollider2D slashHitbox;
    [Tooltip("Người chơi gần hơn khoảng này thì boss chém")]
    [TenHienThi("Tầm chém")] public float slashRange = 6f;
    [Tooltip("Khung hình (tính từ 0) bắt đầu và kết thúc gây sát thương")]
    [TenHienThi("Khung bắt đầu")] public int slashHitStartFrame = 4;
    [TenHienThi("Khung kết thúc")] public int slashHitEndFrame = 6;
    [Tooltip("Giây dừng tay giơ lưỡi hái (đổi màu đỏ) trước khi chém - để người chơi kịp né")]
    [TenHienThi("Báo trước")] public float slashTelegraph = 0.4f;
    [TenHienThi("Màu báo trước")] public Color telegraphColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Phép bàn tay")]
    [TenHienThi("Sát thương")] public int handDamage = 20;
    [TenHienThi("Hất văng")] public Vector2 handKnockback = new Vector2(0f, 7f);
    [TenHienThi("Cỡ vùng đánh")] public Vector2 handHitboxSize = new Vector2(2.4f, 4.5f);
    [Tooltip("Giây vạch đỏ hiện trước khi bàn tay bắt đầu thò xuống")]
    [TenHienThi("Vạch đỏ hiện trước")] public float handWarningTime = 0.45f;
    [TenHienThi("Khung/giây")] public float spellFrameRate = 12f;
    [TenHienThi("Khung bắt đầu")] public int handHitStartFrame = 7;
    [TenHienThi("Khung kết thúc")] public int handHitEndFrame = 11;
    [Tooltip("Phase 2: số bàn tay đuổi theo người chơi mỗi lần niệm")]
    [TenHienThi("Số tay phase 2")] public int phase2HandCount = 3;
    [TenHienThi("Cách nhau")] public float handInterval = 0.5f;
    [Tooltip("Hàng bàn tay to phủ sân, chừa khe để đứng (chỉ phase 3)")]
    [TenHienThi("Cỡ tay to")] public float giantHandScale = 1.5f;
    [TenHienThi("Khe tay to")] public float giantHandSpacing = 6f;

    [Header("Phase 3")]
    [Tooltip("Giây nghỉ giữa 2 đòn ở phase 3 (phase 1 là Idle Time)")]
    [TenHienThi("Nghỉ giữa đòn")] public float phase3IdleTime = 0.45f;
    [Tooltip("Phase 3: số bàn tay đuổi theo người chơi mỗi lần niệm")]
    [TenHienThi("Số tay")] public int phase3HandCount = 5;
    [Tooltip("Phase 3: mỗi lần niệm có bao nhiêu phần trăm ra hàng bàn tay to thay vì bàn tay đuổi")]
    [Range(0f, 1f)] [TenHienThi("Tỉ lệ hàng tay")] public float phase3RowChance = 0.4f;
    [Tooltip("Phase 3: chém xong giáng thêm mấy bàn tay vào chỗ người chơi (0 = không)")]
    [TenHienThi("Tay sau khi chém")] public int phase3SlashFollowUpHands = 2;

    [Header("Dịch chuyển")]
    [TenHienThi("Thời gian mờ")] public float teleportFadeTime = 0.3f;
    [Tooltip("Hiện ra cách người chơi bao xa (phía bên kia)")]
    [TenHienThi("Khoảng cách hiện ra")] public float teleportBehindDistance = 3.5f;

    [Header("Chuỗi dịch chuyển chém")]
    [Tooltip("Mỗi lần chọn đòn ở phase 3 có bao nhiêu phần trăm ra chuỗi dịch chuyển chém liên tiếp (không ra 2 chuỗi liền nhau)")]
    [Range(0f, 1f)] [TenHienThi("Tỉ lệ ra chuỗi")] public float phase3BlinkChainChance = 0.3f;
    [Tooltip("Số lần dịch chuyển + chém liên tiếp trong 1 chuỗi (0 = tắt chuỗi)")]
    [TenHienThi("Số nhát")] public int phase3BlinkCount = 4;
    [Tooltip("Giây mờ đi / hiện lại mỗi lần dịch chuyển trong chuỗi (nhanh hơn dịch chuyển thường)")]
    [TenHienThi("Thời gian mờ")] public float blinkFadeTime = 0.15f;
    [Tooltip("Hết chuỗi boss đứng thở bao lâu - lúc người chơi đánh trả")]
    [TenHienThi("Nghỉ sau chuỗi")] public float blinkChainRecovery = 1.2f;

    [Header("Khiên đồng hồ cát")]
    [Tooltip("Giây để phá hết đồng hồ cát ở phase 2, hết giờ là Quá hạn")]
    [TenHienThi("Thời hạn phase 2")] public float shieldTimeLimit = 15f;
    [Tooltip("Phase 3 phải phá nhiều đồng hồ hơn nên cho thêm giờ")]
    [TenHienThi("Thời hạn phase 3")] public float phase3ShieldTimeLimit = 20f;
    [Tooltip("Số đồng hồ cát phải phá ở phase 2 / phase 3")]
    [TenHienThi("Số đồng hồ phase 2")] public int phase2Hourglasses = 2;
    [TenHienThi("Số đồng hồ phase 3")] public int phase3Hourglasses = 3;
    [TenHienThi("Máu đồng hồ")] public int hourglassHealth = 20;
    [Tooltip("Đồng hồ bị 2 phát tơ đạn thì đứng im bao lâu (để kịp bấm Q giật xuống)")]
    [TenHienThi("Đồng hồ bị trói")] public float hourglassStunTime = 4f;
    [Tooltip("Phá xong khiên thì boss choáng bao lâu (lúc đánh thoải mái)")]
    [TenHienThi("Choáng khi vỡ khiên")] public float shieldBreakStun = 3f;
    [Tooltip("Trong lúc có khiên, mấy giây boss giáng 1 bàn tay xuống người chơi (phase 2)")]
    [TenHienThi("Giáng tay mỗi")] public float shieldHandInterval = 3.5f;
    [TenHienThi("Giáng tay mỗi (phase 3)")] public float phase3ShieldHandInterval = 2.5f;
    [TenHienThi("Màu khiên")] public Color shieldColor = new Color(0.55f, 0.85f, 1f, 1f);
    [Tooltip("Để trống = đồng hồ cát vẽ bằng code")]
    [TenHienThi("Hình đồng hồ")] public Sprite hourglassSprite;
    [Tooltip("Vỡ khiên thì rơi ra 1 cái này (đùi gà hồi máu)")]
    [TenHienThi("Vật phẩm rơi")] public GameObject healthDropPrefab;

    [Header("Sóng quá hạn")]
    [TenHienThi("Sát thương")] public int overdueDamage = 25;
    [TenHienThi("Báo trước")] public float overdueWarning = 1.2f;
    [TenHienThi("Thời gian sóng")] public float overdueActive = 0.35f;
    [Tooltip("Sóng cao bao nhiêu ô tính từ sàn - đứng trên bệ hoặc nhảy lên là tránh được")]
    [TenHienThi("Độ cao sóng")] public float overdueHeight = 1.6f;

    [Header("Bị trói")]
    [TenHienThi("Màu bị trói")] public Color stunnedColor = new Color(0.6f, 0.75f, 1f, 1f);

    [Header("Thanh máu boss")]
    [TenHienThi("Cỡ thanh")] public Vector2 barSize = new Vector2(460f, 14f);
    [TenHienThi("Màu thanh")] public Color barColor = new Color(0.8f, 0.12f, 0.18f, 1f);
    [TenHienThi("Màu nền thanh")] public Color barBackColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Âm thanh")]
    [TenHienThi("Mở đầu")] public AudioClip introSound;
    [TenHienThi("Chém")] public AudioClip slashSound;
    [TenHienThi("Niệm phép")] public AudioClip castSound;
    [TenHienThi("Bàn tay")] public AudioClip handSound;
    [TenHienThi("Dịch chuyển")] public AudioClip teleportSound;
    [TenHienThi("Khiên")] public AudioClip shieldSound;
    [TenHienThi("Vỡ khiên")] public AudioClip shieldBreakSound;
    [TenHienThi("Vỡ đồng hồ")] public AudioClip hourglassBreakSound;
    [TenHienThi("Quá hạn")] public AudioClip overdueSound;
    [TenHienThi("Bị trói")] public AudioClip stunSound;
    [TenHienThi("Gục")] public AudioClip deathSound;
    [TenHienThi("Tấm bằng")] public AudioClip diplomaSound;

    [Header("Kết thúc")]
    [Tooltip("Để trống = tấm bằng vẽ bằng code")]
    [TenHienThi("Hình tấm bằng")] public Sprite diplomaSprite;
    [TenHienThi("Tiêu đề kết thúc")] public string endingTitle = "TỐT NGHIỆP XUẤT SẮC!";
    [TextArea(4, 14)]
    [TenHienThi("Lời kết thúc")] public string endingText =
        "Bạn đã vượt qua Deadline cận kề và nhận Tấm bằng Cử nhân xuất sắc.\n" +
        "Cảm ơn bạn đã chơi Brand NEU day!\n\n" +
        "Khóa luận tốt nghiệp - Trường Công nghệ, Đại học Kinh tế Quốc dân\n" +
        "Sinh viên thực hiện: Trần Quang Khánh - 11236132\n" +
        "Giảng viên hướng dẫn: Lưu Minh Tuấn\n\n" +
        "Hình ảnh: Clembod (Bringer of Death), Luiz Melo, RVROS, A.A Matt Nieks, Kyrise\n" +
        "Âm thanh: Leo Phaz, YouFulca";
    [Tooltip("Lúc boss gục game chậm lại bao nhiêu (1 = không chậm)")]
    [TenHienThi("Chậm khi gục")] public float deathSlowMotion = 0.35f;
    [TenHienThi("Thời gian chậm")] public float deathSlowMotionTime = 1.2f;

    public BossState State { get; private set; }
    public int Phase { get; private set; }

    private Rigidbody2D rb;
    private Damageable damageable;
    private Transform player;
    private Damageable playerDamageable;
    private BossArena arena;

    private Coroutine brain;
    private float speed = 1f;
    private float walkDirection;
    private bool fighting;
    private bool resetting;
    private BossState lastAction;
    private Vector3 homePosition;
    private bool giantRowOffset;

    // hình động lặp (đứng/đi) chạy trong Update; đòn đánh tự đặt từng khung trong coroutine
    private Sprite[] loopFrames;
    private float loopTime;
    private bool actionAnimating;

    // màu: đỏ lúc giơ đòn, xanh lúc có khiên/bị trói, mờ lúc dịch chuyển, nháy lúc trúng đòn
    private bool telegraphing;
    private float artAlpha = 1f;
    private Material artMaterial; // material gốc của hình boss, lấy 1 lần lúc đầu - lúc nháy trắng thì artRenderer đang mang material nháy

    private readonly List<GameObject> hazards = new List<GameObject>();
    private readonly List<BossHourglass> hourglasses = new List<BossHourglass>();

    private RectTransform barFill;
    private GameObject barRoot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        damageable = GetComponent<Damageable>();
        if (artRenderer == null) artRenderer = GetComponentInChildren<SpriteRenderer>();
        if (artRenderer != null) artMaterial = artRenderer.sharedMaterial;

        // trúng đòn thì nháy trắng (thay cho kiểu mờ đi cũ)
        HitFlash hitFlash = GetComponent<HitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<HitFlash>();
        hitFlash.targetRenderer = artRenderer;

        // boss lướt ngang trên sàn bằng code, không theo trọng lực/va chạm (đỡ kẹt dưới bệ)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // boss: trúng đòn không bị ngắt đòn, không bị tơ kéo giật đi
        damageable.superArmor = true;
        damageable.canBePulled = false;
        damageable.IsShielded = true; // chưa vào sân thì chưa đánh được

        // gốc boss quay phải khi scale.x > 0 (hitbox chém nằm bên phải); hình gốc quay trái thì lật lại cho khớp
        if (artRenderer != null) artRenderer.flipX = !artFacesRight;
        if (slashHitbox != null) slashHitbox.enabled = false;

        homePosition = transform.position;
        Phase = 1;
        State = BossState.Waiting;
        SetLoop(idleFrames);
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerDamageable = playerObject.GetComponent<Damageable>();
            if (playerDamageable != null) playerDamageable.damageableDeath.AddListener(OnPlayerDeath);
        }

        damageable.healthChanged.AddListener(OnHealthChanged);
        damageable.damageableDeath.AddListener(OnDeath);
        CheckpointManager.PlayerRespawned += OnPlayerRespawned;
    }

    private void OnDestroy()
    {
        CheckpointManager.PlayerRespawned -= OnPlayerRespawned;
        if (playerDamageable != null) playerDamageable.damageableDeath.RemoveListener(OnPlayerDeath);
        ClearHazards();
        if (barRoot != null) Destroy(barRoot);
    }

    // ===================== bắt đầu trận (BossArena gọi khi người chơi bước vào sân) =====================

    public void BeginFight(BossArena fightArena)
    {
        if (fighting || State == BossState.Dead) return;

        arena = fightArena;
        fighting = true;
        CreateHealthBar();
        StartBrain(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        State = BossState.Intro;
        FacePlayer();
        PlaySound(introSound);
        if (GameHUD.Instance != null) GameHUD.Instance.ShowMessage(bossName);

        yield return PlayFrames(castFrames, 0, LastIndex(castFrames, 8), frameRate);

        damageable.IsShielded = false;
        yield return Brain();
    }

    // ===================== vòng lặp chọn đòn =====================

    private IEnumerator Brain()
    {
        while (true)
        {
            State = BossState.Idle;
            SetLoop(idleFrames);
            FacePlayer();
            yield return new WaitForSeconds((Phase >= 3 ? phase3IdleTime : idleTime) / speed);

            if (player == null) continue;

            float distance = Mathf.Abs(player.position.x - transform.position.x);
            float roll = Random.value;
            BossState next;

            if (Phase >= 3 && phase3BlinkCount > 0 && lastAction != BossState.BlinkChain && Random.value < phase3BlinkChainChance)
            {
                next = BossState.BlinkChain;
            }
            else if (Phase >= 3)
            {
                // phase 3 dịch chuyển nhiều hơn, được dịch chuyển liền 2 lần
                // gần: 40% chém / 30% niệm / 30% dịch chuyển ra sau lưng rồi chém; xa: 40% niệm / 40% dịch chuyển / 20% đi tới
                if (distance <= slashRange) next = roll < 0.4f ? BossState.Slash : roll < 0.7f ? BossState.Cast : BossState.Teleport;
                else next = roll < 0.4f ? BossState.Cast : roll < 0.8f ? BossState.Teleport : BossState.Walk;
            }
            else if (distance <= slashRange)
            {
                next = roll < 0.65f ? BossState.Slash : BossState.Cast;
            }
            else if (roll < 0.4f)
            {
                next = BossState.Cast;
            }
            else if (roll < 0.7f && lastAction != BossState.Teleport)
            {
                next = BossState.Teleport; // phase 1-2 không dịch chuyển 2 lần liền, khó chịu
            }
            else
            {
                next = BossState.Walk;
            }
            lastAction = next;

            switch (next)
            {
                case BossState.Slash:
                    yield return SlashRoutine();
                    break;
                case BossState.Cast:
                    yield return CastRoutine();
                    break;
                case BossState.Teleport:
                    yield return TeleportRoutine(teleportFadeTime / speed);
                    yield return SlashRoutine();
                    break;
                case BossState.BlinkChain:
                    yield return BlinkChainRoutine(phase3BlinkCount);
                    break;
                case BossState.Walk:
                    yield return WalkRoutine();
                    if (player != null && Mathf.Abs(player.position.x - transform.position.x) <= slashRange) yield return SlashRoutine();
                    break;
            }
        }
    }

    private IEnumerator WalkRoutine()
    {
        State = BossState.Walk;
        SetLoop(walkFrames);
        float timer = 0f;

        while (timer < maxWalkTime && player != null)
        {
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= slashRange * 0.8f) break;

            walkDirection = Mathf.Sign(dx);
            Face(walkDirection);
            timer += Time.deltaTime;
            yield return null;
        }

        walkDirection = 0f;
    }

    // followUpHands = false: không giáng tay sau nhát chém (dùng trong chuỗi dịch chuyển, kẻo quá rối)
    private IEnumerator SlashRoutine(bool followUpHands = true)
    {
        State = BossState.Slash;
        FacePlayer();

        int last = LastIndex(attackFrames, 9);
        float fps = frameRate * speed;

        // vung tay lên
        yield return PlayFrames(attackFrames, 0, slashHitStartFrame - 1, fps);

        // dừng tay giơ lưỡi hái, đỏ lên = sắp chém
        telegraphing = true;
        yield return new WaitForSeconds(slashTelegraph / speed);
        telegraphing = false;

        PlaySound(slashSound);
        if (slashHitbox != null) slashHitbox.enabled = true;
        yield return PlayFrames(attackFrames, slashHitStartFrame, slashHitEndFrame, fps);
        if (slashHitbox != null) slashHitbox.enabled = false;

        // thu tay - lúc hở để người chơi đánh trả
        yield return PlayFrames(attackFrames, slashHitEndFrame + 1, last, fps);

        // phase 3: chém xong giáng tay luôn vào chỗ người chơi vừa né tới, không cho thở
        if (Phase >= 3 && followUpHands)
        {
            for (int i = 0; i < phase3SlashFollowUpHands; i++)
            {
                if (player != null) SpawnHand(player.position.x, 1f);
                yield return new WaitForSeconds(handInterval / speed);
            }
        }
    }

    private IEnumerator CastRoutine()
    {
        State = BossState.Cast;
        FacePlayer();
        PlaySound(castSound);
        yield return PlayFrames(castFrames, 0, LastIndex(castFrames, 8), frameRate * speed);

        if (Phase >= 3 && Random.value < phase3RowChance)
        {
            SpawnGiantHandRow();
            yield return new WaitForSeconds(0.6f);
        }
        else
        {
            int count = Phase >= 3 ? phase3HandCount : Phase >= 2 ? phase2HandCount : 1;
            for (int i = 0; i < count; i++)
            {
                if (player != null) SpawnHand(player.position.x, 1f);
                yield return new WaitForSeconds(handInterval / speed);
            }
        }
    }

    private IEnumerator TeleportRoutine(float fadeTime)
    {
        State = BossState.Teleport;
        PlaySound(teleportSound);
        SetLoop(idleFrames);

        yield return Fade(1f, 0f, fadeTime);

        if (player != null)
        {
            // hiện ra phía bên kia người chơi; sát tường quá thì hiện bên này
            float side = Mathf.Sign(player.position.x - transform.position.x);
            if (side == 0f) side = 1f;
            float target = player.position.x + side * teleportBehindDistance;
            if (target < MinX || target > MaxX) target = player.position.x - side * teleportBehindDistance;
            MoveTo(Mathf.Clamp(target, MinX, MaxX));
        }

        FacePlayer();
        yield return Fade(0f, 1f, fadeTime);
    }

    // phase 3: dịch chuyển + chém liên tiếp, mỗi lần hiện ra sát người chơi ở phía bên kia -> phải tập trung né từng nhát
    // (lộn né xuyên qua boss ra sau lưng, vì lưỡi hái chỉ chém phía trước). Hết chuỗi boss đứng thở - lúc đánh trả.
    private IEnumerator BlinkChainRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return TeleportRoutine(blinkFadeTime);
            yield return SlashRoutine(false);
        }

        State = BossState.Idle;
        SetLoop(idleFrames);
        yield return new WaitForSeconds(blinkChainRecovery);
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            artAlpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        artAlpha = to;
    }

    // ===================== phép bàn tay =====================

    private void SpawnHand(float x, float scale)
    {
        if (arena == null) return;

        float clampedX = Mathf.Clamp(x, arena.leftX + handHitboxSize.x * 0.5f * scale, arena.rightX - handHitboxSize.x * 0.5f * scale);
        BossStrike strike = BossStrike.Create(new Vector2(clampedX, arena.floorY), "BanTayHanChot");
        strike.frames = spellFrames;
        strike.frameRate = spellFrameRate * speed;
        strike.hitStartFrame = handHitStartFrame;
        strike.hitEndFrame = handHitEndFrame;
        strike.activeTime = 0.3f;
        strike.warningTime = handWarningTime / speed;
        strike.artScale = ArtScale * scale;
        strike.hitboxSize = new Vector2(handHitboxSize.x * scale, handHitboxSize.y * scale);
        strike.damage = handDamage;
        strike.knockback = handKnockback;
        strike.hitSound = handSound;
        strike.material = artMaterial;
        hazards.Add(strike.gameObject);
    }

    // hàng bàn tay to cách đều nhau, mỗi lần lệch nửa khoảng so với lần trước -> khe an toàn đổi chỗ
    private void SpawnGiantHandRow()
    {
        if (arena == null) return;

        float start = arena.leftX + (giantRowOffset ? giantHandSpacing * 0.5f : 0f) + handHitboxSize.x * giantHandScale * 0.5f;
        giantRowOffset = !giantRowOffset;
        for (float x = start; x < arena.rightX; x += giantHandSpacing)
        {
            SpawnHand(x, giantHandScale);
        }
    }

    // ===================== khiên đồng hồ cát (đầu phase 2, 3) =====================

    private IEnumerator ShieldRoutine(int newPhase)
    {
        State = BossState.Shield;
        Phase = newPhase;
        speed = newPhase >= 3 ? phase3Speed : phase2Speed;
        damageable.IsShielded = true;
        PlaySound(shieldSound);

        // về giữa sân niệm phép
        yield return Fade(1f, 0f, teleportFadeTime);
        if (arena != null) MoveTo(arena.CenterX);
        FacePlayer();
        yield return Fade(0f, 1f, teleportFadeTime);

        SpawnHourglasses(newPhase >= 3 ? phase3Hourglasses : phase2Hourglasses);
        SetLoop(castFrames);

        float shieldHandEvery = newPhase >= 3 ? phase3ShieldHandInterval : shieldHandInterval;
        float timeLeft = newPhase >= 3 ? phase3ShieldTimeLimit : shieldTimeLimit;
        float handTimer = shieldHandEvery;
        int shownSeconds = -1;

        while (timeLeft > 0f && HourglassesLeft() > 0)
        {
            timeLeft -= Time.deltaTime;
            handTimer -= Time.deltaTime;

            int seconds = Mathf.CeilToInt(timeLeft);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                SetStatus("Phá " + HourglassesLeft() + " đồng hồ cát để vỡ khiên! Hạn chót: " + Mathf.Max(0, seconds) + "s");
            }

            if (handTimer <= 0f && player != null)
            {
                handTimer = shieldHandEvery;
                SpawnHand(player.position.x, 1f);
            }

            FacePlayer();
            yield return null;
        }

        if (HourglassesLeft() == 0)
        {
            // khiên vỡ: boss choáng, rơi đùi gà
            damageable.IsShielded = false;
            PlaySound(shieldBreakSound);
            SetStatus("");
            if (GameHUD.Instance != null) GameHUD.Instance.ShowMessage("Khiên vỡ! Đánh đi!");
            DropHealth();

            State = BossState.Stunned;
            yield return HoldFrames(hurtFrames, shieldBreakStun);
        }
        else
        {
            // hết giờ: dọn đồng hồ còn lại, sóng "Quá hạn" quét cả sàn
            ClearHourglasses();
            SetStatus("");
            if (GameHUD.Instance != null) GameHUD.Instance.ShowMessage("QUÁ HẠN! Nhảy lên!");
            PlaySound(overdueSound);
            SpawnOverdueWave();
            yield return PlayFrames(castFrames, 0, LastIndex(castFrames, 8), frameRate);
            yield return new WaitForSeconds(overdueWarning + overdueActive);
            damageable.IsShielded = false;
        }

        yield return Brain();
    }

    private void SpawnHourglasses(int count)
    {
        ClearHourglasses();
        if (arena == null || count <= 0) return;

        Sprite sprite = hourglassSprite != null ? hourglassSprite : PixelArt.Hourglass();
        Material material = artMaterial;

        List<Vector2> points = new List<Vector2>();
        if (arena.hourglassPoints != null)
        {
            foreach (Transform point in arena.hourglassPoints)
            {
                if (point != null) points.Add(point.position);
            }
        }
        points.Sort((a, b) => a.x.CompareTo(b.x));

        // cần ít đồng hồ hơn số chỗ đặt (phase 2: 2 cái, Màn 5 có 3 chỗ): lấy cách đều từ trái sang phải, 2 cái thì lấy 2 chỗ ngoài cùng
        List<Vector2> chosen = new List<Vector2>();
        if (points.Count >= count)
        {
            for (int i = 0; i < count; i++)
            {
                int index = count == 1 ? points.Count / 2 : Mathf.RoundToInt(i * (points.Count - 1) / (float)(count - 1));
                chosen.Add(points[index]);
            }
        }
        else
        {
            // file màn đặt thiếu ký tự h (vd scene Màn 5 dựng từ bản cũ chỉ có 2 chỗ): thêm vào giữa khoảng trống rộng nhất, cao 5.5 ô
            chosen.AddRange(points);
            while (chosen.Count < count)
            {
                float bestGap = -1f, bestX = arena.CenterX;
                float previous = arena.leftX;
                for (int i = 0; i <= chosen.Count; i++)
                {
                    float next = i < chosen.Count ? chosen[i].x : arena.rightX;
                    if (next - previous > bestGap)
                    {
                        bestGap = next - previous;
                        bestX = (previous + next) * 0.5f;
                    }
                    previous = next;
                }
                chosen.Add(new Vector2(bestX, arena.floorY + 5.5f));
                chosen.Sort((a, b) => a.x.CompareTo(b.x));
            }
        }

        foreach (Vector2 point in chosen)
        {
            BossHourglass hourglass = BossHourglass.Create(point, sprite, material, hourglassHealth, hourglassStunTime);
            hourglass.breakSound = hourglassBreakSound;
            hourglasses.Add(hourglass);
        }
    }

    private int HourglassesLeft()
    {
        int count = 0;
        foreach (BossHourglass hourglass in hourglasses)
        {
            if (hourglass != null && !hourglass.IsBroken) count++;
        }
        return count;
    }

    private void ClearHourglasses()
    {
        foreach (BossHourglass hourglass in hourglasses)
        {
            if (hourglass != null) Destroy(hourglass.gameObject);
        }
        hourglasses.Clear();
    }

    private void SpawnOverdueWave()
    {
        if (arena == null) return;

        BossStrike wave = BossStrike.Create(new Vector2(arena.CenterX, arena.floorY), "SongQuaHan");
        wave.frames = null; // không có hình: dải màu tím phủ sàn
        wave.warningTime = overdueWarning;
        wave.activeTime = overdueActive;
        wave.hitboxSize = new Vector2(arena.rightX - arena.leftX, overdueHeight);
        wave.damage = overdueDamage;
        wave.knockback = new Vector2(0f, 8f);
        wave.hitSound = handSound;
        wave.shakeAmount = 0.3f;
        wave.material = artMaterial;
        hazards.Add(wave.gameObject);
    }

    private void DropHealth()
    {
        if (healthDropPrefab == null || arena == null) return;
        GameObject drop = Instantiate(healthDropPrefab, new Vector3(arena.CenterX, arena.floorY + 1.2f, 0f), Quaternion.identity);
        hazards.Add(drop); // chết giữa chừng thì dọn luôn cho khỏi ăn nhiều lần
    }

    // ===================== bị trói / trúng đòn =====================

    private IEnumerator StunnedRoutine()
    {
        State = BossState.Stunned;
        PlaySound(stunSound);
        walkDirection = 0f;
        actionAnimating = true; // giữ khung ôm đầu, không để hình đứng lặp đè lên

        while (damageable.isStunned)
        {
            ShowFrame(hurtFrames, LastIndex(hurtFrames, 2));
            yield return null;
        }

        yield return Brain();
    }

    // ===================== đổi phase / chết / người chơi chết =====================

    private void OnHealthChanged(int health, int maxHealth)
    {
        UpdateHealthBar();
        if (!fighting || resetting || health <= 0 || State == BossState.Dead || State == BossState.PlayerDown) return;

        float ratio = (float)health / Mathf.Max(1, maxHealth);
        int target = ratio <= phase3At ? 3 : ratio <= phase2At ? 2 : 1;
        if (target > Phase && State != BossState.Shield)
        {
            damageable.IsShielded = true; // chặn ngay các đòn đang bay tới, không cho lố sang phase sau
            StartBrain(ShieldRoutine(target));
        }
    }

    private void OnDeath()
    {
        if (State == BossState.Dead) return;

        StopBrain();
        State = BossState.Dead;
        fighting = false;
        ClearHazards();
        ClearHourglasses();
        SetStatus("");
        PlaySound(deathSound);
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // chậm hình chốc lát cho cú kết liễu "đã" hơn (đang tạm dừng thì thôi)
        if (deathSlowMotion < 1f)
        {
            yield return new WaitForSecondsRealtime(0.1f); // để khựng hình trúng đòn chạy xong trước
            if (!PauseMenu.IsPaused) Time.timeScale = deathSlowMotion;
        }

        actionAnimating = true;
        int last = LastIndex(deathFrames, 9);
        for (int i = 0; i <= last; i++)
        {
            ShowFrame(deathFrames, i);
            yield return new WaitForSecondsRealtime(deathSlowMotionTime / (last + 1));
        }

        if (!PauseMenu.IsPaused) Time.timeScale = 1f;

        if (arena != null) arena.OnBossDefeated();
        if (barRoot != null) barRoot.SetActive(false);

        // tấm bằng từ trên rơi xuống giữa sân
        Vector2 center = arena != null ? new Vector2(arena.CenterX, arena.floorY) : (Vector2)transform.position;
        DiplomaPickUp diploma = DiplomaPickUp.Create(center + Vector2.up * 7f, center.y + 1.4f,
            diplomaSprite != null ? diplomaSprite : PixelArt.Diploma(),
            artMaterial, 2f);
        diploma.pickUpSound = diplomaSound;
        diploma.endingTitle = endingTitle;
        diploma.endingText = endingText;
        SetStatus("Nhặt Tấm bằng Cử nhân xuất sắc!");

        yield return new WaitForSeconds(1f);
        if (artRenderer != null) artRenderer.enabled = false; // xác boss biến mất
    }

    private void OnPlayerDeath()
    {
        if (!fighting || State == BossState.Dead) return;

        StopBrain();
        State = BossState.PlayerDown;
        damageable.IsShielded = true; // tơ đạn còn bay dở không được làm boss đổi phase/chết lúc người chơi đã nằm
        ClearHazards();
        ClearHourglasses();
        SetStatus("");
        SetLoop(idleFrames);
    }

    // CheckpointManager báo người chơi vừa hồi sinh: boss về chỗ cũ, máu về đầu phase đang đánh
    private void OnPlayerRespawned()
    {
        if (!fighting || State == BossState.Dead) return;
        StartBrain(RestartPhaseRoutine());
    }

    private IEnumerator RestartPhaseRoutine()
    {
        State = BossState.PlayerDown;
        resetting = true;
        float ratio = Phase >= 3 ? phase3At : Phase >= 2 ? phase2At : 1f;
        damageable.Health = Mathf.Max(1, Mathf.RoundToInt(damageable.MaxHealth * ratio));
        resetting = false;

        MoveTo(homePosition.x);
        artAlpha = 1f;
        damageable.IsShielded = true;
        FacePlayer();
        SetLoop(idleFrames);

        yield return new WaitForSeconds(1.5f); // cho người chơi kịp định thần

        if (Phase >= 2)
        {
            yield return ShieldRoutine(Phase); // phase 2, 3 bắt đầu bằng khiên đồng hồ cát
        }
        else
        {
            damageable.IsShielded = false;
            yield return Brain();
        }
    }

    // ===================== điều khiển coroutine =====================

    private void StartBrain(IEnumerator routine)
    {
        StopBrain();
        brain = StartCoroutine(routine);
    }

    private void StopBrain()
    {
        // dừng hết coroutine của boss: đòn con chạy bằng "yield return" là coroutine riêng, chỉ dừng coroutine ngoài
        // thì đòn con vẫn chạy tiếp (bật lại hitbox, gọi thêm bàn tay) song song với não mới
        StopAllCoroutines();
        brain = null;

        // dọn những thứ đòn đang làm dở
        if (slashHitbox != null) slashHitbox.enabled = false;
        telegraphing = false;
        actionAnimating = false;
        walkDirection = 0f;
        artAlpha = 1f;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void ClearHazards()
    {
        foreach (GameObject hazard in hazards)
        {
            if (hazard != null) Destroy(hazard);
        }
        hazards.Clear();
    }

    // ===================== mỗi khung hình =====================

    private void Update()
    {
        // bị trói (đủ phát tơ đạn / đòn mạnh) thì ngắt đòn đang dở
        if (fighting && damageable.isStunned && damageable.isAlive
            && State != BossState.Stunned && State != BossState.Shield && State != BossState.Intro
            && State != BossState.PlayerDown && State != BossState.Dead)
        {
            StartBrain(StunnedRoutine());
        }

        hazards.RemoveAll(h => h == null);

        // hình lặp (đứng / đi)
        if (!actionAnimating && loopFrames != null && loopFrames.Length > 0)
        {
            loopTime += Time.deltaTime;
            int index = (int)(loopTime * frameRate * speed) % loopFrames.Length;
            ShowFrame(loopFrames, index);
        }

        UpdateColor();
        UpdateHealthBar();
    }

    private void FixedUpdate()
    {
        if (State == BossState.Walk && walkDirection != 0f)
        {
            float x = rb.position.x + walkDirection * walkSpeed * speed * Time.fixedDeltaTime;
            if (x < MinX || x > MaxX) walkDirection = 0f;
            rb.linearVelocity = new Vector2(walkDirection * walkSpeed * speed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // đứng yên, không trôi theo đòn đánh
        }
    }

    private void UpdateColor()
    {
        if (artRenderer == null) return;

        Color color = Color.white;
        if (State == BossState.Dead) color = Color.white;
        else if (damageable.IsShielded && State == BossState.Shield) color = Color.Lerp(Color.white, shieldColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        else if (telegraphing) color = telegraphColor;
        else if (State == BossState.Stunned) color = stunnedColor;

        color.a = artAlpha;
        artRenderer.color = color;
    }

    // ===================== hình động =====================

    private void SetLoop(Sprite[] frames)
    {
        actionAnimating = false;
        if (loopFrames == frames) return;
        loopFrames = frames;
        loopTime = 0f;
    }

    // chạy lần lượt các khung from..to, mỗi khung 1/fps giây (không có hình thì vẫn chờ đủ thời gian)
    private IEnumerator PlayFrames(Sprite[] frames, int from, int to, float fps)
    {
        actionAnimating = true;
        for (int i = from; i <= to; i++)
        {
            ShowFrame(frames, i);
            yield return new WaitForSeconds(1f / Mathf.Max(1f, fps));
        }
        actionAnimating = false;
    }

    // đứng ở khung cuối của 1 hình (vd ôm đầu choáng) trong 1 khoảng thời gian
    private IEnumerator HoldFrames(Sprite[] frames, float time)
    {
        actionAnimating = true;
        int last = LastIndex(frames, 2);
        yield return PlayFrames(frames, 0, last, frameRate);
        actionAnimating = true;
        ShowFrame(frames, last);
        yield return new WaitForSeconds(time);
        actionAnimating = false;
    }

    private void ShowFrame(Sprite[] frames, int index)
    {
        if (artRenderer == null || frames == null || frames.Length == 0) return;
        artRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    // khung cuối của 1 bộ hình; chưa có hình thì giả sử có (fallback) khung để giữ nhịp đòn
    private static int LastIndex(Sprite[] frames, int fallback)
    {
        return frames != null && frames.Length > 0 ? frames.Length - 1 : fallback;
    }

    // ===================== hướng mặt, vị trí =====================

    // dịch chuyển tức thời: đặt cả Rigidbody2D lẫn Transform. Chỉ đặt rb.position thì Transform tới nhịp vật lý sau mới theo kịp
    // -> FacePlayer đọc vị trí cũ nên boss hiện ra quay lưng về người chơi, và lệnh lật mặt (đổi localScale) còn có thể
    // kéo thân boss về chỗ cũ (Unity chép Transform vừa đổi vào Rigidbody2D trước nhịp vật lý)
    private void MoveTo(float x)
    {
        Vector2 position = new Vector2(x, rb.position.y);
        rb.position = position;
        transform.position = new Vector3(x, position.y, transform.position.z);
        rb.linearVelocity = Vector2.zero;
    }

    private void FacePlayer()
    {
        if (player != null) Face(player.position.x - transform.position.x);
    }

    private void Face(float direction)
    {
        if (direction == 0f) return;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        transform.localScale = scale;
    }

    private float ArtScale
    {
        get { return artRenderer != null ? Mathf.Abs(artRenderer.transform.localScale.y) : 1f; }
    }

    // boss đứng cách tường ít nhất 2 ô
    private float MinX { get { return arena != null ? arena.leftX + 2f : float.NegativeInfinity; } }
    private float MaxX { get { return arena != null ? arena.rightX - 2f : float.PositiveInfinity; } }

    // ===================== thanh máu boss (dựng bằng code trong GameHUD) =====================

    private void CreateHealthBar()
    {
        if (barRoot != null)
        {
            barRoot.SetActive(true);
            return;
        }
        if (GameHUD.Instance == null) return;

        Canvas canvas = GameHUD.Instance.GetComponent<Canvas>();
        if (canvas == null) canvas = GameHUD.Instance.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        barRoot = new GameObject("ThanhMauBoss", typeof(RectTransform));
        barRoot.transform.SetParent(canvas.transform, false);
        // ngay sau dòng đạn tơ: các bảng tạm dừng/nâng cấp vẽ sau nên che lên thanh máu
        TMP_Text ammoText = GameHUD.Instance.ammoText;
        if (ammoText != null && ammoText.transform.parent == canvas.transform)
        {
            barRoot.transform.SetSiblingIndex(ammoText.transform.GetSiblingIndex() + 1);
        }

        RectTransform rootRect = (RectTransform)barRoot.transform;
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -12f);
        rootRect.sizeDelta = new Vector2(barSize.x, barSize.y + 26f);

        // tên boss
        GameObject labelObject = new GameObject("TenBoss", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(barRoot.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 24f);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = ammoText != null ? ammoText.font : TMP_Settings.defaultFontAsset;
        GameFont.Setup(label, font, 20f); // font pixel VT323 nếu đã tạo, không thì font cũ
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.85f, 0.8f, 1f);
        label.raycastTarget = false;
        label.text = bossName;

        // khung nền
        GameObject backObject = new GameObject("Nen", typeof(RectTransform), typeof(Image));
        backObject.transform.SetParent(barRoot.transform, false);
        RectTransform backRect = (RectTransform)backObject.transform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = Vector2.zero;
        backRect.sizeDelta = new Vector2(0f, barSize.y);
        Image backImage = backObject.GetComponent<Image>();
        backImage.color = barBackColor;
        backImage.raycastTarget = false;

        // phần máu còn lại (co theo tỉ lệ, không cần hình)
        GameObject fillObject = new GameObject("Mau", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backObject.transform, false);
        barFill = (RectTransform)fillObject.transform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.offsetMin = new Vector2(2f, 2f);
        barFill.offsetMax = new Vector2(-2f, -2f);
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = barColor;
        fillImage.raycastTarget = false;

        // vạch đánh dấu chỗ đổi phase
        AddPhaseMark(backObject.transform, phase2At);
        AddPhaseMark(backObject.transform, phase3At);

        UpdateHealthBar();
    }

    private static void AddPhaseMark(Transform parent, float at)
    {
        GameObject mark = new GameObject("VachPhase", typeof(RectTransform), typeof(Image));
        mark.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)mark.transform;
        rect.anchorMin = new Vector2(at, 0f);
        rect.anchorMax = new Vector2(at, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(2f, 0f);
        Image image = mark.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.8f);
        image.raycastTarget = false;
    }

    private void UpdateHealthBar()
    {
        if (barFill == null) return;
        float fraction = Mathf.Clamp01((float)damageable.Health / Mathf.Max(1, damageable.MaxHealth));
        barFill.anchorMax = new Vector2(fraction, 1f);
    }

    // ===================== tiện ích =====================

    private static void SetStatus(string text)
    {
        if (GameHUD.Instance != null) GameHUD.Instance.SetStatus(text);
    }

    // phát tiếng nghe rõ ở mọi chỗ (camera khóa giữa sân, phát theo vị trí thì tiếng ở xa nhỏ đi)
    public static void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.PlaySfx(clip);
        }
        else if (Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
    }
}
