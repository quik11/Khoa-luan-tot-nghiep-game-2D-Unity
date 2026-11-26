using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Nâng cấp nhân vật bằng thanh kiến thức:
// - hạ 1 kẻ địch được +1 kiến thức, nhặt quyển sách (UpgradePickUp) được +4
// - mỗi con quái / quyển sách chỉ cho kiến thức 1 lần trong cả game: chơi lại màn cũ thì quái vẫn có
//   (để mở cửa qua màn) nhưng hạ không được thêm kiến thức, sách đã nhặt thì không hiện lại -> không cày được
// - đầy thanh thì dừng game, hiện 3 thẻ nâng cấp ngẫu nhiên cho chọn 1 (thẻ nào tối đa rồi thì không hiện)
// - mỗi lần nâng cấp xong thì thanh sau dài hơn, kiến thức dư giữ lại cho thanh sau
// - vào màn nào cũng áp lại mọi nâng cấp đã lưu
// Nằm trong prefab Assets/UI/GameHUD.prefab cùng GameHUD và PauseMenu. Số liệu chỉnh ngay trong Inspector
// hoặc trong cửa sổ Brand NEU day > Bảng chỉnh thông số.
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Thanh kiến thức")]
    [Tooltip("Hạ 1 kẻ địch (kể cả địch rơi xuống vực) được bao nhiêu kiến thức")]
    [TenHienThi("Mỗi quái")] public int knowledgePerKill = 1;
    [Tooltip("Nhặt 1 quyển sách được bao nhiêu kiến thức (4 = bằng hạ 4 kẻ địch)")]
    [TenHienThi("Mỗi sách")] public int knowledgePerBook = 4;
    // thanh đầu 4 quái, từ thanh 2 trở đi lần nào cũng 6 quái (thanh dài dần thì lên cấp quá lâu, người chơi chán)
    [Tooltip("Kiến thức cần để đầy thanh, xếp theo số lần đã nâng cấp: ô đầu là lần nâng cấp thứ 1, ô sau là lần thứ 2...")]
    [TenHienThi("Cần để đầy")] public int[] knowledgeThresholds = { 4, 6 };
    [Tooltip("Dùng hết các ô ở trên rồi thì mỗi thanh sau cần nhiều hơn thanh trước bấy nhiêu")]
    [TenHienThi("Tăng thêm sau đó")] public int knowledgeStepAfterLast = 0;
    [Tooltip("Bảng vừa hiện lên thì chưa nhận bấm trong bấy nhiêu giây, tránh đang bấm nhảy/đánh lỡ tay chọn luôn")]
    [TenHienThi("Chờ trước khi chọn")] public float choiceInputDelay = 0.4f;

    [Header("Thẻ Hút máu")]
    [TenHienThi("Mỗi cấp")] public int lifestealPerLevel = 5;
    [TenHienThi("Cấp tối đa")] public int lifestealMaxLevel = 2;

    [Header("Thẻ Đấm mạnh")]
    [TenHienThi("Mỗi cấp")] public int meleeDamagePerLevel = 3;
    [TenHienThi("Cấp tối đa")] public int meleeDamageMaxLevel = 2;

    [Header("Thẻ Băng tơ lớn")]
    [TenHienThi("Mỗi cấp")] public int webAmmoPerLevel = 1;
    [TenHienThi("Cấp tối đa")] public int webAmmoMaxLevel = 5;

    [Header("Thẻ Hồi tơ khi hạ địch")]
    [Tooltip("Cứ hạ bấy nhiêu kẻ địch thì được hồi tơ 1 lần")]
    [TenHienThi("Số quái cần hạ")] public int[] ammoOnKillKillsNeeded = { 2, 1, 1 };
    [Tooltip("Mỗi lần được hồi thì hồi bấy nhiêu viên tơ")]
    [TenHienThi("Số tơ hồi")] public int[] ammoOnKillAmount = { 1, 1, 2 };
    [Tooltip("Muốn thêm cấp thì thêm ô vào 2 dãy ở trên, không thì cấp cao hơn dùng lại số của ô cuối")]
    [TenHienThi("Cấp tối đa")] public int ammoOnKillMaxLevel = 3;

    [Header("Thẻ Sát thương đòn mạnh")]
    [TenHienThi("Mỗi cấp")] public int heavyDamagePerLevel = 6;
    [TenHienThi("Cấp tối đa")] public int heavyDamageMaxLevel = 3;

    [Header("Thẻ Hất văng đòn mạnh")]
    [TenHienThi("Mỗi cấp")] public float heavyKnockbackPerLevel = 3f;
    [TenHienThi("Cấp tối đa")] public int heavyKnockbackMaxLevel = 1;

    [Header("Thẻ Trói lâu hơn")]
    [TenHienThi("Mỗi cấp (giây)")] public float stunTimePerLevel = 0.2f;
    [TenHienThi("Cấp tối đa")] public int stunTimeMaxLevel = 1;

    [Header("Thẻ Né hoàn hảo hồi tơ")]
    [TenHienThi("Mỗi cấp")] public int perfectDodgeAmmoPerLevel = 1;
    [TenHienThi("Cấp tối đa")] public int perfectDodgeAmmoMaxLevel = 1;

    [Header("Nhận biết đòn mạnh")]
    [Tooltip("Chưa mở khóa đòn mạnh (I) thì không hiện 2 thẻ chỉ dành cho đòn mạnh, tránh chọn phí")]
    [TenHienThi("Ẩn khi chưa mở")] public bool hideHeavyCardsUntilUnlocked = true;
    [Tooltip("Hitbox đòn mạnh = hitbox có tích 'Stun On Hit', hoặc có đúng tên này")]
    [TenHienThi("Tên hitbox")] public string heavyHitboxName = "playerAttack4";

    [Header("Thẻ Tơ kéo xa (đang tắt)")]
    [TenHienThi("Mỗi cấp")] public float webPullRangePerLevel = 2f;
    [TenHienThi("Cấp tối đa")] public int webPullRangeMaxLevel = 0;

    [Header("Bảng nâng cấp")]
    [TenHienThi("Bảng")] public GameObject choicePanel;
    [TenHienThi("Các thẻ")] public Button[] choiceButtons; // 3 nút, nút nào thừa (hết loại để chọn) thì ẩn đi

    [Header("Âm thanh")]
    [TenHienThi("Âm thanh chọn")] public AudioClip chooseSound;

    // HUD hiện "+1" cạnh thanh kiến thức trong khoảng này, nhận liền nhau thì gộp lại thành "+2"...
    private const float gainShowTime = 1.2f;

    private PlayerController player;
    private Damageable playerDamageable;
    private readonly List<UpgradeType> offered = new List<UpgradeType>();
    private readonly HashSet<Damageable> countedDeaths = new HashSet<Damageable>();
    private readonly Dictionary<Damageable, string> enemyIds = new Dictionary<Damageable, string>(); // mã chống cày của từng quái
    private bool replayMessagePending;

    private int knowledge;
    private int upgradesTaken; // tổng số cấp đã có = số lần đã nâng cấp, quyết định độ dài thanh hiện tại
    private bool allMaxed;
    private bool offerCheckNeeded;
    private float choiceOpenedAt;
    private int killsTowardAmmo;
    private int recentGain;
    private float recentGainTime = -100f;

    // cho GameHUD vẽ thanh kiến thức
    public int Knowledge { get { return knowledge; } }
    public int KnowledgeNeeded { get { return KnowledgeThreshold(upgradesTaken); } }
    public bool AllMaxed { get { return allMaxed; } }
    public int RecentGain { get { return RecentGainVisible ? recentGain : 0; } }
    private bool RecentGainVisible { get { return Time.unscaledTime - recentGainTime < gainShowTime; } }

    private void Awake()
    {
        Instance = this;
        knowledge = PlayerUpgrades.Knowledge;
        RefreshUpgradeCount();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (choicePanel != null) choicePanel.SetActive(false);

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            playerDamageable = playerObject.GetComponent<Damageable>();
        }

        // áp lại mọi nâng cấp đã lưu từ các màn trước (mỗi màn nhân vật là bản mới từ prefab, chưa có gì).
        // Phần cộng thêm thời gian trói là biến static nên phải xóa về 0 trước, không thì cộng dồn qua mỗi màn.
        Damageable.playerStunBonus = 0f;
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            int level = EffectiveLevel(type);
            if (level > 0) ApplyLevels(type, level);
        }

        // nghe sự kiện chết của mọi quái trong màn: cộng kiến thức, hút máu, hồi tơ.
        // Lấy mã chống cày ngay lúc này (quái còn đứng đúng chỗ đặt trong scene, chưa chạy vật lý bước nào)
        string sceneName = gameObject.scene.name;
        int alreadyCounted = 0;
        foreach (Damageable damageable in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
        {
            if (damageable.GetComponent<PlayerController>() == null)
            {
                Damageable enemy = damageable;
                string id = PlayerUpgrades.MakeId(sceneName, enemy.gameObject.name, enemy.transform.position);
                enemyIds[enemy] = id;
                if (PlayerUpgrades.IsCollected(id)) alreadyCounted++;
                enemy.damageableDeath.AddListener(() => OnEnemyDeath(enemy));
            }
        }
        // chơi lại màn đã qua: báo trước cho người chơi biết quái cũ không cho thêm kiến thức
        replayMessagePending = alreadyCounted > 0;

        // màn trước lỡ đầy thanh mà chưa kịp chọn (vd vừa qua cửa) thì vào màn mở bảng luôn
        offerCheckNeeded = true;
    }

    private void Update()
    {
        // báo ở Update đầu tiên chứ không ở Start cho chắc GameHUD đã sẵn sàng (Start của 2 script không biết cái nào chạy trước)
        if (replayMessagePending)
        {
            replayMessagePending = false;
            if (GameHUD.Instance != null)
            {
                GameHUD.Instance.ShowMessage("Chơi lại: quái đã hạ trước đây không cho thêm kiến thức");
            }
        }

        if (choicePanel != null && choicePanel.activeSelf)
        {
            if (choiceButtons != null && choiceButtons.Length > 0 && choiceButtons[0] != null)
            {
                MenuSelection.ReselectOnKeyPress(choiceButtons[0].gameObject);
            }
            return;
        }

        // không mở bảng ngay trong lúc quái chết (đang giữa đòn đánh/khựng hình) mà đợi tới đây,
        // và đợi tới lúc yên ổn: không tạm dừng, nhân vật còn sống, không đang hồi sinh
        if (offerCheckNeeded && CanOpenOfferNow())
        {
            offerCheckNeeded = false;
            if (!allMaxed && knowledge >= KnowledgeNeeded)
            {
                OfferChoices();
            }
        }
    }

    private bool CanOpenOfferNow()
    {
        if (PauseMenu.IsPaused) return false; // đang mở menu tạm dừng hoặc 1 bảng khác
        if (playerDamageable != null && !playerDamageable.isAlive) return false;
        if (CheckpointManager.Instance != null && CheckpointManager.Instance.IsRespawning) return false;
        return true;
    }

    // ===== Thanh kiến thức =====

    // kiến thức cần cho lần nâng cấp thứ (upgradeCount + 1)
    public int KnowledgeThreshold(int upgradeCount)
    {
        if (knowledgeThresholds == null || knowledgeThresholds.Length == 0)
        {
            return Mathf.Max(1, knowledgeStepAfterLast * (upgradeCount + 1));
        }

        if (upgradeCount < knowledgeThresholds.Length)
        {
            return Mathf.Max(1, knowledgeThresholds[upgradeCount]);
        }

        int last = knowledgeThresholds[knowledgeThresholds.Length - 1];
        int extra = upgradeCount - knowledgeThresholds.Length + 1;
        return Mathf.Max(1, last + knowledgeStepAfterLast * extra);
    }

    public void AddKnowledge(int amount)
    {
        if (amount <= 0 || allMaxed) return; // mọi thẻ đã tối đa thì tích thêm cũng không dùng vào đâu

        knowledge += amount;
        PlayerUpgrades.Knowledge = knowledge;

        // nhận liền nhau (vd 1 đòn hạ 2 quái) thì HUD hiện gộp "+2" cho đỡ nhảy chữ
        recentGain = RecentGainVisible ? recentGain + amount : amount;
        recentGainTime = Time.unscaledTime;

        offerCheckNeeded = true; // đủ thanh thì Update mở bảng
    }

    // quyển sách gọi khi được nhặt. Trả về false nếu mọi nâng cấp đã tối đa (sách không còn tác dụng)
    public bool AddBookKnowledge()
    {
        if (allMaxed) return false;

        AddKnowledge(knowledgePerBook);
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowMessage("+" + knowledgePerBook + " kiến thức (sách)");
        }
        return true;
    }

    // cho bảng chỉnh thông số: đổ đầy thanh hiện tại để mở bảng chọn ngay
    public void FillKnowledgeBar()
    {
        if (allMaxed) return;
        AddKnowledge(Mathf.Max(1, KnowledgeNeeded - knowledge));
    }

    // ===== Thông tin từng loại nâng cấp =====

    public int MaxLevel(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.HutMau: return lifestealMaxLevel;
            case UpgradeType.SatThuongDam: return meleeDamageMaxLevel;
            case UpgradeType.BangDanTo: return webAmmoMaxLevel;
            case UpgradeType.TamToKeo: return webPullRangeMaxLevel;
            case UpgradeType.HoiToKhiHaDich: return ammoOnKillMaxLevel;
            case UpgradeType.SatThuongDonManh: return heavyDamageMaxLevel;
            case UpgradeType.HatVangDonManh: return heavyKnockbackMaxLevel;
            case UpgradeType.ThoiGianTroi: return stunTimeMaxLevel;
            case UpgradeType.NeHoanHaoHoiTo: return perfectDodgeAmmoMaxLevel;
        }
        return 0;
    }

    // cấp thực sự được tính: cấp đã lưu nhưng không vượt quá tối đa hiện tại
    // (hạ tối đa xuống 0 trong Inspector là tắt hẳn loại đó, kể cả cấp đã lưu từ trước)
    public int EffectiveLevel(UpgradeType type)
    {
        return Mathf.Clamp(PlayerUpgrades.GetLevel(type), 0, Mathf.Max(0, MaxLevel(type)));
    }

    public string DisplayName(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.HutMau: return "Hút máu";
            case UpgradeType.SatThuongDam: return "Đấm mạnh";
            case UpgradeType.BangDanTo: return "Băng tơ lớn";
            case UpgradeType.TamToKeo: return "Tơ kéo xa";
            case UpgradeType.HoiToKhiHaDich: return "Hồi tơ khi hạ địch";
            case UpgradeType.SatThuongDonManh: return "Sát thương đòn mạnh";
            case UpgradeType.HatVangDonManh: return "Hất văng đòn mạnh";
            case UpgradeType.ThoiGianTroi: return "Trói lâu hơn";
            case UpgradeType.NeHoanHaoHoiTo: return "Né hoàn hảo hồi tơ";
        }
        return type.ToString();
    }

    // hiệu ứng khi lên tới cấp nextLevel (ghi trên thẻ)
    public string Description(UpgradeType type, int nextLevel)
    {
        switch (type)
        {
            case UpgradeType.HutMau:
                return "Hạ 1 địch hồi " + lifestealPerLevel * nextLevel + " máu";

            case UpgradeType.SatThuongDam:
                return "Đòn đánh thường +" + meleeDamagePerLevel + " sát thương (tổng +" + meleeDamagePerLevel * nextLevel + ")";

            case UpgradeType.BangDanTo:
                if (player != null)
                {
                    return "Băng tơ +" + webAmmoPerLevel + " viên (" + player.maxWebAmmo + " → " + (player.maxWebAmmo + webAmmoPerLevel) + ")";
                }
                return "Băng tơ +" + webAmmoPerLevel + " viên";

            case UpgradeType.HoiToKhiHaDich:
                return "Hạ " + AmmoOnKillKills(nextLevel) + " địch hồi " + AmmoOnKillAmount(nextLevel) + " tơ";

            case UpgradeType.SatThuongDonManh:
                return "Đòn mạnh (I) +" + heavyDamagePerLevel + " sát thương (tổng +" + heavyDamagePerLevel * nextLevel + ")";

            case UpgradeType.HatVangDonManh:
                return "Đòn mạnh (I) hất văng xa hơn (+" + FormatNumber(heavyKnockbackPerLevel * nextLevel) + ")";

            case UpgradeType.ThoiGianTroi:
                return "Trói địch lâu thêm " + FormatNumber(stunTimePerLevel * nextLevel) + " giây (đòn mạnh, tơ đạn)";

            case UpgradeType.TamToKeo:
                return "Tơ kéo xa thêm " + FormatNumber(webPullRangePerLevel) + " ô";

            case UpgradeType.NeHoanHaoHoiTo:
                return "Lộn né đúng lúc quái ra đòn (né hoàn hảo) hồi " + perfectDodgeAmmoPerLevel * nextLevel + " tơ";
        }
        return "";
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }

    private static bool IsHeavyOnly(UpgradeType type)
    {
        return type == UpgradeType.SatThuongDonManh || type == UpgradeType.HatVangDonManh;
    }

    private bool CanOffer(UpgradeType type)
    {
        if (EffectiveLevel(type) >= MaxLevel(type)) return false;
        if (hideHeavyCardsUntilUnlocked && IsHeavyOnly(type) && player != null && !player.HeavyUnlocked) return false;
        return true;
    }

    // đếm lại tổng cấp đã có + xem còn thẻ nào chưa tối đa không (gọi khi vào màn và sau mỗi lần chọn)
    public void RefreshUpgradeCount()
    {
        upgradesTaken = 0;
        allMaxed = true;
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            int level = EffectiveLevel(type);
            upgradesTaken += level;
            if (level < MaxLevel(type)) allMaxed = false;
        }
    }

    // ===== Bảng chọn 3 thẻ =====

    // Trả về false nếu không còn thẻ nào để chọn (không mở bảng)
    private bool OfferChoices()
    {
        offered.Clear();

        List<UpgradeType> available = new List<UpgradeType>();
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            if (CanOffer(type)) available.Add(type);
        }

        if (available.Count == 0 || choicePanel == null || choiceButtons == null) return false;

        // bốc ngẫu nhiên tối đa 3 loại khác nhau
        int count = Mathf.Min(choiceButtons.Length, available.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, available.Count);
            offered.Add(available[pick]);
            available.RemoveAt(pick);
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;

            bool used = i < offered.Count;
            button.gameObject.SetActive(used);
            if (!used) continue;

            UpgradeType type = offered[i];
            int level = EffectiveLevel(type);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true); // bảng còn đang tắt nên phải tìm cả object tắt
            if (label != null)
            {
                label.text = "<b>" + DisplayName(type) + "</b>\n<size=70%>" + Description(type, level + 1) +
                             "\nCấp " + level + " → " + (level + 1) + " (tối đa " + MaxLevel(type) + ")</size>";
            }
        }

        if (PauseMenu.Instance != null) PauseMenu.Instance.OpenModal();
        choicePanel.SetActive(true);
        MenuSelection.Select(choiceButtons[0] != null ? choiceButtons[0].gameObject : null);
        choiceOpenedAt = Time.unscaledTime;
        return true;
    }

    // 3 nút trong bảng chọn gọi hàm này với số 0, 1, 2 (điền ở ô On Click () của từng nút)
    public void Choose(int index)
    {
        if (index < 0 || index >= offered.Count) return;
        if (Time.unscaledTime < choiceOpenedAt + choiceInputDelay) return; // vừa hiện bảng, bỏ qua cú bấm lỡ tay

        UpgradeType type = offered[index];

        // trừ đúng độ dài thanh vừa đầy (tính trước khi lên cấp), phần dư để dành cho thanh sau
        knowledge = Mathf.Max(0, knowledge - KnowledgeNeeded);
        PlayerUpgrades.AddLevel(type);
        ApplyLevels(type, 1);
        RefreshUpgradeCount();
        if (allMaxed) knowledge = 0;
        PlayerUpgrades.Knowledge = knowledge;

        if (chooseSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(chooseSound, Camera.main.transform.position);
        }

        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowMessage("Đã nhận: " + DisplayName(type) + " (cấp " + EffectiveLevel(type) + ")");
        }

        offered.Clear();
        if (choicePanel != null) choicePanel.SetActive(false);
        if (PauseMenu.Instance != null) PauseMenu.Instance.CloseModal();

        offerCheckNeeded = true; // kiến thức dư vẫn đủ thanh sau thì khung hình sau mở bảng tiếp
    }

    // ===== Áp chỉ số =====

    // hitbox của đòn mạnh phím I (tách riêng khỏi đòn đánh thường)
    private bool IsHeavyHitbox(tanCong hitbox)
    {
        return hitbox.stunOnHit || hitbox.gameObject.name == heavyHitboxName;
    }

    // cộng chỉ số cho nhân vật theo số cấp truyền vào (vào màn thì truyền cả số cấp đã lưu, chọn mới thì truyền 1)
    private void ApplyLevels(UpgradeType type, int levels)
    {
        if (player == null) return;

        switch (type)
        {
            case UpgradeType.SatThuongDam:
                // mọi hitbox đánh gần nằm trong nhân vật (kể cả đang tắt) đều có tanCong - trừ hitbox đòn mạnh
                foreach (tanCong hitbox in player.GetComponentsInChildren<tanCong>(true))
                {
                    if (!IsHeavyHitbox(hitbox)) hitbox.attackDamage += meleeDamagePerLevel * levels;
                }
                break;

            case UpgradeType.SatThuongDonManh:
                foreach (tanCong hitbox in player.GetComponentsInChildren<tanCong>(true))
                {
                    if (IsHeavyHitbox(hitbox)) hitbox.attackDamage += heavyDamagePerLevel * levels;
                }
                break;

            case UpgradeType.HatVangDonManh:
                foreach (tanCong hitbox in player.GetComponentsInChildren<tanCong>(true))
                {
                    if (!IsHeavyHitbox(hitbox)) continue;
                    // tanCong tự đảo dấu x theo hướng mặt, ở đây chỉ làm lực ngang lớn thêm (giữ nguyên dấu gốc)
                    float sign = hitbox.knockback.x < 0f ? -1f : 1f;
                    hitbox.knockback = new Vector2(hitbox.knockback.x + sign * heavyKnockbackPerLevel * levels, hitbox.knockback.y);
                }
                break;

            case UpgradeType.ThoiGianTroi:
                foreach (tanCong hitbox in player.GetComponentsInChildren<tanCong>(true))
                {
                    if (IsHeavyHitbox(hitbox)) hitbox.stunDuration += stunTimePerLevel * levels;
                }
                Damageable.playerStunBonus += stunTimePerLevel * levels; // trói do 2 phát tơ đạn
                break;

            case UpgradeType.BangDanTo:
                player.maxWebAmmo += webAmmoPerLevel * levels;
                player.RefillWebAmmo(webAmmoPerLevel * levels);
                break;

            case UpgradeType.TamToKeo:
                player.webPullRange += webPullRangePerLevel * levels;
                break;

            case UpgradeType.NeHoanHaoHoiTo:
                player.perfectDodgeAmmoRefund += perfectDodgeAmmoPerLevel * levels;
                break;

            case UpgradeType.HutMau:
            case UpgradeType.HoiToKhiHaDich:
                break; // không đổi chỉ số, xử lý lúc quái chết (OnEnemyDeath)
        }
    }

    private int AmmoOnKillKills(int level)
    {
        if (ammoOnKillKillsNeeded == null || ammoOnKillKillsNeeded.Length == 0) return 1;
        return Mathf.Max(1, ammoOnKillKillsNeeded[Mathf.Clamp(level - 1, 0, ammoOnKillKillsNeeded.Length - 1)]);
    }

    private int AmmoOnKillAmount(int level)
    {
        if (ammoOnKillAmount == null || ammoOnKillAmount.Length == 0) return 1;
        return Mathf.Max(0, ammoOnKillAmount[Mathf.Clamp(level - 1, 0, ammoOnKillAmount.Length - 1)]);
    }

    private void OnEnemyDeath(Damageable enemy)
    {
        // mỗi con chỉ tính 1 lần (phòng sự kiện chết bị gọi lặp)
        if (enemy == null || !countedDeaths.Add(enemy)) return;

        // kiến thức: chỉ lần đầu hạ con này trong cả game (chơi lại màn thì không có). Hút máu, hồi tơ bên dưới vẫn có
        string id;
        if (!enemyIds.TryGetValue(enemy, out id) || PlayerUpgrades.MarkCollected(id))
        {
            AddKnowledge(knowledgePerKill);
        }

        int lifesteal = EffectiveLevel(UpgradeType.HutMau);
        if (lifesteal > 0 && playerDamageable != null)
        {
            playerDamageable.Heal(lifestealPerLevel * lifesteal); // Heal tự bỏ qua nếu người chơi đã chết
        }

        int ammoLevel = EffectiveLevel(UpgradeType.HoiToKhiHaDich);
        if (ammoLevel > 0 && player != null && player.isAlive)
        {
            killsTowardAmmo++;
            if (killsTowardAmmo >= AmmoOnKillKills(ammoLevel))
            {
                killsTowardAmmo = 0;
                player.RefillWebAmmo(AmmoOnKillAmount(ammoLevel)); // đầy băng thì thôi, không cộng dồn
            }
        }
    }
}
