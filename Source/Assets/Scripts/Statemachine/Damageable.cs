using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Damageable : MonoBehaviour
{
    [TenHienThi("Khi trúng đòn")] public UnityEvent<int, Vector2> damageableHit;
    [TenHienThi("Khi chết")] public UnityEvent damageableDeath;
    [TenHienThi("Khi đổi máu")] public UnityEvent<int, int> healthChanged;

    // sự kiện chung cho mọi đối tượng (hiệu ứng rung màn hình, hạt văng ra... nghe ở GameFeel, khỏi gắn từng con)
    public static event System.Action<Damageable, int, Vector2> AnyHit;  // (ai trúng, sát thương, lực đẩy)
    public static event System.Action<Damageable> AnyDied;

    Animator animator;
    Rigidbody2D rb;

    [Header("Khựng hình")]
    [TenHienThi("Thời gian")] public float hitstopDuration = 0.06f; // khoảng khựng hình cực ngắn đúng lúc trúng đòn, giúp đòn đánh có cảm giác nặng tay hơn

    [Header("Trói bằng tơ đạn")]
    [TenHienThi("Số phát để trói")] public int webHitsToStun = 2;
    [TenHienThi("Trong vòng")] public float webHitWindow = 5f; // trong khoảng này mới tính dồn, quá giờ chưa đủ 2 phát thì reset về 0
    [TenHienThi("Trói trong")] public float stunDuration = 2f;

    private int webHitCount = 0;
    private float webHitWindowTimer = 0f;

    // nâng cấp "Thời gian trói" của người chơi: cộng thêm vào thời gian trói do 2 phát tơ đạn (UpgradeManager gán lúc vào màn).
    // Để static vì tơ đạn trói quái (stunDuration là của từng con quái), còn nâng cấp là của người chơi.
    public static float playerStunBonus = 0f;

    [Header("Dành cho boss")]
    [Tooltip("Trúng đòn không giật mình, không bị khựng/hất văng - boss vẫn mất máu nhưng không bị ngắt đòn")]
    [TenHienThi("Không giật mình")] public bool superArmor = false;
    [Tooltip("Bị trói thì tơ kéo (Q) giật được về phía người chơi. Boss to quá thì tắt: Q trúng boss là người chơi bay tới")]
    [TenHienThi("Tơ kéo giật được")] public bool canBePulled = true;

    // boss bật khiên (đòn đánh, tơ đạn đều không ăn) - BossController tự bật/tắt, không cần chỉnh tay
    public bool IsShielded { get; set; }

    [Header("Giáp mặt trước")]
    [Tooltip("Bật thì đòn cận chiến đánh từ phía trước bị đỡ, không mất máu. Đánh sau lưng, đòn mạnh (I), đòn dậm, tơ đạn vẫn ăn; đang bị trói thì không đỡ được. Đòn mạnh trúng mặt trước thì vỡ giáp luôn")]
    [TenHienThi("Có giáp")] public bool frontGuard = false;
    public event System.Action<Vector2> guardBlocked; // đỡ được 1 đòn (vị trí người đánh) - để kêu "keng", tóe lửa
    public event System.Action guardBroken;           // đòn mạnh đập vỡ giáp

    // trúng đòn đúng lúc đang bất tử (vd đang lộn né) - PlayerController nghe để tính né hoàn hảo
    public event System.Action hitWhileInvincible;

    // quái bị trói đang quấn kén tơ: đánh trúng lần đầu thì kén vỡ (WebCombo lo phần nổ). Hết trói thì kén cũng hết.
    private bool cocoonIntact = false;
    public bool IsCocooned { get { return isStunned && cocoonIntact; } }
    // đếm số lần quấn kén mới (kể cả trói lại ngay khi đang bị trói) - Quỷ gối nhìn số này để chạy lại dáng quấn tơ
    public int CocoonCount { get; private set; }

    private bool lockVelocityNoAnimator; // object không có Animator (boss, đồng hồ cát) thì giữ cờ khóa ở đây

    [SerializeField]
    [TenHienThi("Đang bị trói")] private bool _isStunned = false;
    private float stunTimer = 0f;

    [SerializeField]
    [TenHienThi("Máu tối đa")] private int _maxHealth = 100;

    public int MaxHealth
    {
        get { return _maxHealth; }
        set { _maxHealth = value; }
    }
    
    [SerializeField] 
    [TenHienThi("Máu")] private int _health = 100;

    public int Health
    {
        get { return _health; }
        set
        {
            _health = value;
            healthChanged?.Invoke(_health, MaxHealth);
           
            if(_health <= 0)
            {
               isAlive = false;
            }
        }
    }

    [SerializeField]
    [TenHienThi("Còn sống")] private bool _isAlive = true;
    
    [SerializeField]
    [TenHienThi("Đang bất tử")] private bool isInvincible = false;
    [TenHienThi("Bất tử sau khi trúng")] public float invincibilityTime = 0.25f; // thời gian bất tử mặc định sau khi trúng đòn
    private float invincibilityTimer = 0f; // đếm ngược dùng chung cho mọi nguồn gây bất tử (trúng đòn, né tránh...)

    public bool isAlive
    {
        get { return _isAlive; }
        set
        {
            _isAlive = value;
            if (animator != null) animator.SetBool(AnimationStrings.isAlive, value);
            Debug.Log("isAlive set " + value);
            
            if(!value)
            {
                damageableDeath?.Invoke();
                AnyDied?.Invoke(this);
            }
        }
    }

    public bool lockVelocity
    {
        get { return animator != null ? animator.GetBool(AnimationStrings.lockVelocity) : lockVelocityNoAnimator; }
        set
        {
            if (animator != null) animator.SetBool(AnimationStrings.lockVelocity, value);
            else lockVelocityNoAnimator = value;
        }
    }

    public bool isStunned
    {
        get { return _isStunned; }
        private set { _isStunned = value; }
    }
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        UpdateHitstop();

        // rơi khỏi map (thấp hơn mốc vực chết của màn) thì chết ngay - mốc do CheckpointManager tính lúc vào màn
        if (isAlive && transform.position.y < CheckpointManager.FallKillY)
        {
            Kill();
        }

        if(isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if(invincibilityTimer <= 0f)
            {
                isInvincible = false;
            }
        } 

        // hết cửa sổ dồn tơ mà chưa đủ 2 phát thì reset lại, không cộng dồn vô thời hạn
        if (webHitWindowTimer > 0f)
        {
            webHitWindowTimer -= Time.deltaTime;
            if (webHitWindowTimer <= 0f)
            {
                webHitCount = 0;
            }
        }

        // hết giờ bị trói thì tự gỡ
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                cocoonIntact = false;
            }
        }
    } 

    public bool Hit(int damage, Vector2 knockback, bool bypassInvincibility = false)
    {
        if (IsShielded) return false; // đang có khiên: đòn không ăn

        if(isAlive && (!isInvincible || bypassInvincibility))
        {
            Health -= damage;
           
            isInvincible = true; 
            invincibilityTimer = invincibilityTime;
            if (!superArmor)
            {
                if (animator != null) animator.SetTrigger(AnimationStrings.hitTrigger);
                lockVelocity = true;
            }
            damageableHit?.Invoke(damage, knockback);
            AnyHit?.Invoke(this, damage, knockback);
            
            CharacterEvents.characterDamaged?.Invoke(gameObject, damage);

            TriggerHitstop();

            return true;
        }

        if (isAlive && isInvincible && !bypassInvincibility)
        {
            hitWhileInvincible?.Invoke();
        }
        return false;
    }

    // Đòn cận chiến biết người đánh đứng đâu (tanCong gọi) - để giáp mặt trước biết đỡ hay không.
    // breaksGuard = đòn mạnh: trúng mặt trước thì đập vỡ giáp (từ đó là quái thường) rồi ăn đòn như thường.
    // bypassInvincibility = nhát phá kén: luôn ăn đòn kể cả quái còn bất tử 0.25 s do nhát trước vừa trúng.
    public bool Hit(int damage, Vector2 knockback, Vector2 attackerPosition, bool breaksGuard, bool bypassInvincibility = false)
    {
        if (frontGuard && isAlive && !isStunned && !IsShielded && IsInFront(attackerPosition))
        {
            if (!breaksGuard)
            {
                guardBlocked?.Invoke(attackerPosition);
                return false;
            }
            frontGuard = false;
            guardBroken?.Invoke();
        }
        return Hit(damage, knockback, bypassInvincibility);
    }

    // quy ước của Quỷ gối: scale x dương = quay mặt sang phải
    private bool IsInFront(Vector2 attackerPosition)
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return (attackerPosition.x - transform.position.x) * facing > 0f;
    }

    // Khựng hình cực ngắn toàn bộ game đúng lúc có đòn trúng.
    // Dùng chung 1 mốc kết thúc (static): mỗi con tự lưu/trả timeScale thì 1 nhát trúng 2 quái sẽ trả nhầm về 0, game đứng hình.

    private static bool hitstopActive = false;
    private static float hitstopEndTime = 0f; // tính theo Time.unscaledTime nên không bị chính timeScale = 0 làm đứng

    // Editor có thể tắt "Reload Domain" khi bấm Play -> biến static giữ giá trị của lần chơi trước, phải tự xóa
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hitstopActive = false;
        hitstopEndTime = 0f;
        playerStunBonus = 0f;
        AnyHit = null;
        AnyDied = null;
    }

    private void TriggerHitstop()
    {
        if (hitstopDuration <= 0f) return;

        // nhiều đòn trúng gần nhau thì lấy mốc kết thúc muộn nhất, không cộng dồn
        float endTime = Time.unscaledTime + hitstopDuration;
        hitstopEndTime = hitstopActive ? Mathf.Max(hitstopEndTime, endTime) : endTime;
        hitstopActive = true;
        Time.timeScale = 0f;
    }

    // Update vẫn chạy khi timeScale = 0, đối tượng nào còn sống cũng có thể trả lại tốc độ game
    // (kể cả quái vừa trúng đòn bị hủy giữa chừng thì người chơi vẫn trả lại được)
    private static void UpdateHitstop()
    {
        if (hitstopActive && Time.unscaledTime >= hitstopEndTime)
        {
            hitstopActive = false;

            // đang mở menu tạm dừng thì để nguyên timeScale = 0, PauseMenu tự trả lại khi chơi tiếp.
            // Đang chậm thời gian (né hoàn hảo) thì trả về tốc độ chậm, không về 1
            if (!PauseMenu.IsPaused)
            {
                Time.timeScale = SlowMotion.TargetScale;
            }
        }
    }

    // Cho PlayerController gọi khi né tránh - cấp bất tử trong đúng khoảng thời gian chỉ định, không kèm hiệu ứng trúng đòn/khóa velocity như Hit()
    public void SetTemporaryInvincibility(float duration)
    {
        if (!isAlive) return;

        // đang bất tử lâu hơn (vd vừa hồi sinh) thì giữ thời gian dài hơn, né không làm mất bất tử hồi sinh
        invincibilityTimer = isInvincible ? Mathf.Max(invincibilityTimer, duration) : duration;
        isInvincible = true;
    }

    // gọi riêng khi trúng tơ đạn (Projectile.cs tự gọi lúc isWebPellet=true) - đếm dồn đủ webHitsToStun phát trong webHitWindow giây thì bị trói
    public void RegisterWebHit()
    {
        if (!isAlive) return;

        webHitCount++;
        webHitWindowTimer = webHitWindow;

        if (webHitCount >= webHitsToStun)
        {
            isStunned = true;
            cocoonIntact = true;
            CocoonCount++;
            stunTimer = stunDuration + playerStunBonus;
            webHitCount = 0;
        }
    }

    // chết ngay lập tức, bỏ qua bất tử (rơi xuống vực). Khác Hit(): không hiện số sát thương, không khựng hình, không hất văng.
    public void Kill()
    {
        if (!isAlive) return; // xác đã chết rơi tiếp vào vực thì thôi, không gọi sự kiện chết thêm lần nữa
        Health = 0;
    }

    // hồi sinh đầy máu - CheckpointManager gọi khi đưa người chơi về điểm lưu (PlayerController.RespawnAt lo phần Animator, vị trí)
    public void Revive()
    {
        isInvincible = false;
        invincibilityTimer = 0f;
        webHitCount = 0;
        webHitWindowTimer = 0f;
        isStunned = false;
        cocoonIntact = false;
        stunTimer = 0f;

        isAlive = true;
        Health = MaxHealth; // gán qua setter để thanh máu tự đầy lại
        lockVelocity = false; // đòn kết liễu bật khóa này mà không có gì tắt (state chết không có lối ra)
    }

    // trói ngay lập tức (đòn mạnh phím I) - đang bị trói lâu hơn thì giữ thời gian dài hơn
    public void Stun(float duration)
    {
        if (!isAlive) return;

        isStunned = true;
        cocoonIntact = true;
        CocoonCount++;
        stunTimer = Mathf.Max(stunTimer, duration);
    }

    // kén tơ bị đánh vỡ: hết kén, trói chỉ còn tối đa 'stunLeft' giây (đủ cho dáng trúng đòn chạy xong)
    public void BurstCocoon(float stunLeft)
    {
        cocoonIntact = false;
        if (isStunned) stunTimer = Mathf.Min(stunTimer, stunLeft);
    }

    // cho tơ kéo gọi để giật thẳng object này theo 1 vector velocity, không tính là sát thương/knockback thường
    public void ApplyPull(Vector2 velocity)
    {
        if (rb != null)
        {
            // phát tơ đạn thứ 2 vừa trúng thì quái còn đang khựng (lockVelocity) -> script quái không hãm tốc độ giật,
            // quái bay vọt qua vực đáp lên bờ bên mình. Bỏ khựng để nó chậm dần và rơi đúng xuống vực.
            lockVelocity = false;
            rb.linearVelocity = velocity;
        }
    }
    
    public bool Heal(int healthRestored)
    {
        if(isAlive && Health < MaxHealth)
        {
            int maxHeal = Mathf.Max(MaxHealth - Health, 0);
            int actualHeal = Mathf.Min(maxHeal, healthRestored);
            
            Health += actualHeal;
            
            CharacterEvents.characterHealed?.Invoke(gameObject, actualHeal);
            
            return true;
        }
        return false;
    }
}