using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Cửa sổ gom mọi thông số hay phải thử về 1 chỗ: nhân vật, quái, nâng cấp, vật phẩm... khỏi phải lục từng object/prefab.
// Mở: menu trên cùng Brand NEU day > Bảng chỉnh thông số (kéo tab cửa sổ ghép cạnh Inspector cho tiện).
// Sửa ở đây = sửa đúng ô đó trong Inspector (có Ctrl+Z), chỉ là gom lại và ghi chú tiếng Việt.
// Có thêm các nút thử nhanh lúc đang Play (cộng kiến thức, mở bảng nâng cấp, hạ hết quái...) và nút xóa/đặt tiến độ.
public class BangChinhThongSo : EditorWindow
{
    private const string hiepSiPrefabPath = "Assets/Characters/Enemies/HiepSi/HiepSi.prefab";
    private const string flyingEyePrefabPath = "Assets/Characters/Enemies/FlyingEye/FlyingEye.prefab";
    private const string gameHudPrefabPath = "Assets/UI/GameHUD.prefab";
    private const string webPelletPrefabPath = "Assets/Objects/WebPellet.prefab";
    private const string webAmmoPrefabPath = "Assets/Objects/WebAmmoPickUp.prefab";
    private const string healthPrefabPath = "Assets/Objects/HealthPickUp.prefab";

    // mỗi dòng: { tên ô trong code, chữ hiện trong bảng }. Ô nào code không còn thì tự bỏ qua, không lỗi.
    private static readonly string[,] playerMoveFields =
    {
        { "walkSpeed", "Tốc độ đi trên đất" },
        { "airWalkSpeed", "Tốc độ di chuyển trên không" },
        { "jumpImpulse", "Lực nhảy" },
        { "fallMultiplier", "Rơi nhanh (nhân trọng lực khi rơi)" },
        { "lowJumpMultiplier", "Nhảy thấp khi nhả nút sớm" },
        { "coyoteTime", "Rời mép vẫn nhảy được (giây)" },
        { "jumpBufferTime", "Bấm nhảy sớm vẫn nhận (giây)" },
        { "wallJumpHorizontalForce", "Bật tường: lực ngang" },
        { "wallJumpVerticalForce", "Bật tường: lực lên" },
        { "wallJumpLockDuration", "Bật tường: khóa phím (giây)" },
    };

    private static readonly string[,] rigidbodyFields =
    {
        { "m_GravityScale", "Trọng lực (Gravity Scale)" },
    };

    private static readonly string[,] playerMeleeFields =
    {
        { "attackDashForce", "Lực lướt khi đấm dưới đất" },
        { "airAttackLockOnRange", "Trên không: tầm tự lao vào quái (0 = tắt)" },
        { "airAttackLungeForce", "Trên không: tốc độ lao" },
        { "airAttackHopForce", "Trên không: lực hất lên đòn đầu" },
        { "heavyCooldown", "Đòn mạnh (I): hồi chiêu (giây)" },
        { "heavyLockDuration", "Đòn mạnh (I): đứng yên bao lâu (giây)" },
        { "heavyUnlockLevel", "Đòn mạnh (I): mở khóa khi tới màn số" },
    };

    private static readonly string[,] playerWebFields =
    {
        { "maxWebAmmo", "Băng tơ tối đa (viên)" },
        { "webFireCooldown", "Giãn cách 2 phát tơ (giây)" },
        { "webCastLockDuration", "Khựng khi bắn tơ dưới đất (giây)" },
        { "webCastSpawnDelay", "Trễ từ lúc bấm tới lúc tơ bay ra (giây)" },
        { "webPullRange", "Tơ kéo: tầm bắn (ô)" },
        { "webPullFlightSpeed", "Tơ kéo: tốc độ bay tới" },
        { "webPullMaxDuration", "Tơ kéo: bay tối đa (giây)" },
        { "enemyPullForce", "Tơ kéo: lực giật quái đang bị trói" },
    };

    private static readonly string[,] playerDodgeFields =
    {
        { "dodgeSpeed", "Tốc độ lộn né" },
        { "dodgeDuration", "Thời gian cả cú né (giây)" },
        { "dodgeInvincibleDuration", "Bất tử ở đầu cú né (giây)" },
        { "dodgeCooldown", "Hồi chiêu né (giây)" },
    };

    private static readonly string[,] playerComboFields =
    {
        { "perfectDodgeSlowScale", "Né hoàn hảo: game chậm còn (phần tốc độ)" },
        { "perfectDodgeSlowTime", "Né hoàn hảo: chậm bao lâu (giây thật)" },
        { "perfectDodgeDamageMultiplier", "Né hoàn hảo: nhát kế tiếp nhân sát thương" },
        { "perfectDodgeBonusTime", "Né hoàn hảo: giữ thưởng bao lâu (giây)" },
        { "perfectDodgeAmmoRefund", "Né hoàn hảo: hoàn tơ (viên, thẻ nâng cấp tự cộng)" },
        { "airSlamSpeed", "Đòn dậm (S + U trên không): tốc độ lao xuống" },
        { "airSlamDamage", "Đòn dậm: sát thương" },
        { "airSlamArea", "Đòn dậm: vùng ăn đòn (rộng, cao)" },
        { "airSlamKnockback", "Đòn dậm: hất văng" },
        { "airSlamRecovery", "Đòn dậm: đứng khựng sau khi chạm đất (giây)" },
        { "airSlamCooldown", "Đòn dậm: hồi chiêu (giây)" },
        { "cocoonBurstBonusDamage", "Phá kén: nhát đánh cộng thêm" },
        { "cocoonBurstDamage", "Phá kén: quái đứng gần mất máu" },
        { "cocoonBurstRadius", "Phá kén: bán kính nổ (ô)" },
        { "cocoonBurstKnockback", "Phá kén: hất văng quái gần" },
        { "cocoonBurstStunLeft", "Phá kén: quái vỡ kén còn bị trói (giây)" },
        { "pullImpactDamage", "Giật va chạm: mỗi con mất máu" },
        { "pullImpactKnockback", "Giật va chạm: hất văng con bị đâm" },
    };

    private static readonly string[,] playerDamageableFields =
    {
        { "invincibilityTime", "Bất tử sau khi trúng đòn (giây)" },
        { "hitstopDuration", "Khựng hình khi trúng đòn (giây)" },
    };

    private static readonly string[,] enemyDamageableFields =
    {
        { "invincibilityTime", "Bất tử sau khi trúng đòn (giây)" },
        { "webHitsToStun", "Trúng bao nhiêu phát tơ đạn thì bị trói" },
        { "webHitWindow", "...trong vòng bao nhiêu giây" },
        { "stunDuration", "Bị trói bao lâu (giây)" },
    };

    private static readonly string[,] hitboxFields =
    {
        { "attackDamage", "Sát thương" },
        { "knockback", "Hất văng (x ngang, y lên)" },
        { "stunOnHit", "Trúng là trói luôn" },
        { "stunDuration", "Trói bao lâu (giây)" },
        { "burstsCocoon", "Phá kén tơ (quái đang bị trói)" },
    };

    private static readonly string[,] webPelletFields =
    {
        { "moveSpeed", "Tốc độ bay (x)" },
        { "damage", "Sát thương" },
        { "knockback", "Hất văng" },
    };

    private static readonly string[,] hiepSiFields =
    {
        { "maxSpeed", "Tốc độ đi tối đa" },
        { "walkAcceleration", "Tăng tốc" },
        { "walkStopRate", "Hãm lại khi dừng" },
        { "chargeFromLevel", "Lao húc: Quỷ gối thường biết lao từ màn số" },
        { "chargeMinDistance", "Lao húc: người chơi cách từ (ô)" },
        { "chargeMaxDistance", "Lao húc: ...tới (ô)" },
        { "chargeWindup", "Lao húc: gồng bao lâu (giây)" },
        { "chargeSpeed", "Lao húc: tốc độ lao" },
        { "chargeDuration", "Lao húc: lao tối đa (giây)" },
        { "chargeCooldown", "Lao húc: nghỉ giữa 2 lần (giây)" },
        { "chargeDamage", "Lao húc: sát thương" },
        { "chargeKnockback", "Lao húc: hất văng" },
        { "wallDazeDuration", "Húc tường: choáng bao lâu (giây)" },
        { "chargeRecovery", "Lao xong: đứng thở (giây)" },
    };

    private static readonly string[,] flyingEyeFields =
    {
        { "flightSpeed", "Tốc độ bay" },
        { "waypointReachedDistance", "Cách điểm tuần tra bao nhiêu thì coi là tới" },
        { "diveFromLevel", "Bổ nhào: biết bổ nhào từ màn số" },
        { "diveWindup", "Bổ nhào: gồng bao lâu (giây)" },
        { "diveSpeed", "Bổ nhào: tốc độ lao" },
        { "diveCooldown", "Bổ nhào: nghỉ giữa 2 lần (giây)" },
        { "diveDamage", "Bổ nhào: sát thương" },
        { "diveKnockback", "Bổ nhào: hất văng" },
        { "stuckDuration", "Cắm xuống đất: kẹt bao lâu (giây)" },
        { "retreatDuration", "Đánh xong 1 lượt: lùi ra bao lâu (giây)" },
        { "retreatSpeed", "Lùi ra: tốc độ" },
    };

    private static readonly string[,] knowledgeFields =
    {
        { "knowledgePerKill", "Hạ 1 địch được (kiến thức)" },
        { "knowledgePerBook", "Nhặt 1 sách được (kiến thức)" },
        { "knowledgeThresholds", "Độ dài thanh: lần nâng cấp 1, 2, 3..." },
        { "knowledgeStepAfterLast", "Hết danh sách trên: mỗi thanh dài thêm" },
        { "choiceInputDelay", "Bảng vừa hiện: chưa nhận bấm trong (giây)" },
    };

    private static readonly string[,] upgradeCardFields =
    {
        { "webAmmoPerLevel", "Băng tơ lớn: thêm viên / cấp" },
        { "webAmmoMaxLevel", "Băng tơ lớn: cấp tối đa" },
        { "lifestealPerLevel", "Hút máu: hồi máu / cấp" },
        { "lifestealMaxLevel", "Hút máu: cấp tối đa" },
        { "ammoOnKillKillsNeeded", "Hồi tơ khi hạ địch: số địch cần hạ (theo cấp)" },
        { "ammoOnKillAmount", "Hồi tơ khi hạ địch: số tơ hồi (theo cấp)" },
        { "ammoOnKillMaxLevel", "Hồi tơ khi hạ địch: cấp tối đa" },
        { "meleeDamagePerLevel", "Đấm mạnh: + sát thương / cấp" },
        { "meleeDamageMaxLevel", "Đấm mạnh: cấp tối đa" },
        { "heavyDamagePerLevel", "Sát thương đòn mạnh: + / cấp" },
        { "heavyDamageMaxLevel", "Sát thương đòn mạnh: cấp tối đa" },
        { "heavyKnockbackPerLevel", "Hất văng đòn mạnh: + lực ngang / cấp" },
        { "heavyKnockbackMaxLevel", "Hất văng đòn mạnh: cấp tối đa" },
        { "stunTimePerLevel", "Trói lâu hơn: + giây / cấp" },
        { "stunTimeMaxLevel", "Trói lâu hơn: cấp tối đa" },
        { "perfectDodgeAmmoPerLevel", "Né hoàn hảo hồi tơ: tơ / cấp" },
        { "perfectDodgeAmmoMaxLevel", "Né hoàn hảo hồi tơ: cấp tối đa" },
        { "hideHeavyCardsUntilUnlocked", "Ẩn thẻ đòn mạnh khi chưa mở khóa đòn mạnh" },
        { "heavyHitboxName", "Tên hitbox đòn mạnh" },
    };

    private static readonly string[,] disabledCardFields =
    {
        { "webPullRangePerLevel", "Tơ kéo xa: + ô / cấp" },
        { "webPullRangeMaxLevel", "Tơ kéo xa: cấp tối đa (0 = tắt)" },
    };

    private static readonly string[,] checkpointFields =
    {
        { "fallDeathDepth", "Rơi thấp hơn đất thấp nhất bao nhiêu ô thì chết" },
        { "respawnDelay", "Chết xong chờ bao lâu mới hồi sinh (giây)" },
        { "fadeDuration", "Màn tối dần / sáng dần (giây)" },
        { "respawnInvincibility", "Bất tử sau khi hồi sinh (giây)" },
        { "refillAmmoOnRespawn", "Hồi sinh là đầy tơ" },
        { "maxRespawns", "Số lần hồi sinh mỗi lần vào màn (-1 = không giới hạn)" },
    };

    private static readonly string[,] webAmmoFields =
    {
        { "ammoRestore", "Bình tơ: hồi bao nhiêu viên" },
        { "respawnTime", "Hiện lại sau (giây, 0 = nhặt 1 lần)" },
    };

    private static readonly string[,] healthFields =
    {
        { "healthRestore", "Đùi gà: hồi bao nhiêu máu" },
    };

    [SerializeField] private bool showTest = true;
    [SerializeField] private bool showPlayer = true;
    [SerializeField] private bool showEnemies = false;
    [SerializeField] private bool showUpgrades = false;
    [SerializeField] private bool showItems = false;
    [SerializeField] private bool showSave = false;
    [SerializeField] private Vector2 scroll;

    private static bool assetsDirty = false; // có prefab vừa chỉnh mà chưa ghi ra file

    [MenuItem("Brand NEU day/Bảng chỉnh thông số")]
    private static void Open()
    {
        BangChinhThongSo window = GetWindow<BangChinhThongSo>();
        window.titleContent = new GUIContent("Chỉnh thông số");
        window.minSize = new Vector2(380f, 300f);
        window.Show();
    }

    // đang Play thì vẽ lại liên tục để số kiến thức, cấp... cập nhật theo game
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    // rời cửa sổ là ghi prefab ra file luôn (không ghi theo từng lần kéo số cho đỡ giật máy)
    private void OnLostFocus()
    {
        SavePendingAssets();
    }

    private void OnDisable()
    {
        SavePendingAssets();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox("Sửa ở đây là sửa thẳng vào scene/prefab, Ctrl+Z để hoàn tác. " +
                                "Đang Play mà sửa ô của nhân vật thì thoát Play sẽ mất (Unity vẫn vậy) - thử thấy ổn thì ghi số lại, thoát Play rồi nhập lại.",
                                MessageType.Info);

        if (assetsDirty && GUILayout.Button("Lưu ngay các prefab vừa chỉnh (rời cửa sổ cũng tự lưu)"))
        {
            SavePendingAssets();
        }

        showTest = Section(showTest, "Thử nhanh (lúc đang Play)");
        if (showTest) DrawTestSection();

        showPlayer = Section(showPlayer, "1. Nhân vật chính (scene đang mở)");
        if (showPlayer) DrawPlayerSection();

        showEnemies = Section(showEnemies, "2. Quỷ gối và quái vật bay (prefab)");
        if (showEnemies) DrawEnemySection();

        showUpgrades = Section(showUpgrades, "3. Nâng cấp và thanh kiến thức (prefab GameHUD)");
        if (showUpgrades) DrawUpgradeSection();

        showItems = Section(showItems, "4. Vật phẩm, hồi sinh");
        if (showItems) DrawItemSection();

        showSave = Section(showSave, "5. Tiến độ đã lưu (màn đã mở, cấp nâng cấp, kiến thức)");
        if (showSave) DrawSaveSection();

        EditorGUILayout.EndScrollView();
    }

    // ===== Các phần trong cửa sổ =====

    private void DrawTestSection()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Bấm Play (vào 1 màn chơi) để dùng các nút thử nhanh.", MessageType.None);
            return;
        }

        UpgradeManager upgrades = UpgradeManager.Instance;
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        Damageable playerHealth = player != null ? player.GetComponent<Damageable>() : null;

        if (upgrades != null)
        {
            string bar = upgrades.AllMaxed ? "Tối đa (mọi thẻ đã max)" : upgrades.Knowledge + " / " + upgrades.KnowledgeNeeded;
            EditorGUILayout.LabelField("Kiến thức đang có", bar);
        }
        if (player != null)
        {
            EditorGUILayout.LabelField("Tơ", player.CurrentWebAmmo + " / " + player.MaxWebAmmo);
        }
        if (playerHealth != null)
        {
            EditorGUILayout.LabelField("Máu", playerHealth.Health + " / " + playerHealth.MaxHealth);
        }

        EditorGUI.BeginDisabledGroup(upgrades == null);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 kiến thức")) upgrades.AddKnowledge(1);
        if (GUILayout.Button("+4 kiến thức")) upgrades.AddKnowledge(4);
        if (GUILayout.Button("Mở bảng nâng cấp ngay")) upgrades.FillKnowledgeBar();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(player == null);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Hồi đầy máu + tơ"))
        {
            if (playerHealth != null) playerHealth.Heal(playerHealth.MaxHealth);
            player.RefillWebAmmo(player.maxWebAmmo);
        }
        if (GUILayout.Button("Mở khóa đòn mạnh (I)")) player.ForceUnlockHeavy();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Hạ hết quái trong màn (mở cửa qua màn)"))
        {
            foreach (Damageable damageable in Object.FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (damageable.GetComponent<PlayerController>() == null) damageable.Kill();
            }
        }
        EditorGUILayout.LabelField("Quái hạ bằng nút này cũng tính là đã cho kiến thức (chơi lại không được nữa).", EditorStyles.wordWrappedMiniLabel);

        // mỗi quái/sách chỉ cho kiến thức 1 lần trong cả game -> thử đi thử lại 1 màn thì bấm nút này rồi vào lại màn
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Quái + sách đã cho kiến thức", PlayerUpgrades.CollectedCount.ToString());
        if (GUILayout.Button("Quên hết (vào lại màn để nhận lại)", GUILayout.Width(230f)))
        {
            PlayerUpgrades.ResetCollected();
        }
        EditorGUILayout.EndHorizontal();

        if (upgrades == null)
        {
            EditorGUILayout.HelpBox("Scene này không có GameHUD (UpgradeManager) nên không thử được nâng cấp.", MessageType.Warning);
        }
    }

    private void DrawPlayerSection()
    {
        // MainMenuScene cũng có 1 nhân vật trang trí (Player Input + Damageable đã tắt) -> bỏ qua con đó,
        // không thì chỉnh nhầm vào nhân vật của menu
        PlayerController player = null;
        foreach (PlayerController candidate in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Damageable candidateHealth = candidate.GetComponent<Damageable>();
            if (candidateHealth != null && !candidateHealth.enabled) continue;
            player = candidate;
            break;
        }
        if (player == null)
        {
            EditorGUILayout.HelpBox("Scene đang mở không có nhân vật. Mở Assets/Scenes/GameplayScene (Màn 1) để chỉnh.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Đang chỉnh", player.gameObject.scene.name + " / " + player.name);
        EditorGUILayout.HelpBox("Chỉnh ở đây là chỉnh trên scene đang mở (nhớ Ctrl+S lưu scene). " +
                                "Màn 2 trở đi sao chép nhân vật từ Màn 1 lúc dựng màn, nên chỉnh Màn 1 xong phải dựng lại Màn 2 mới ăn theo " +
                                "(menu Brand NEU day > Dựng lại tất cả màn).", MessageType.None);
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Đang Play: số bên dưới ĐÃ CỘNG các nâng cấp đang có (đạn tơ, sát thương, hất văng, trói...). " +
                                    "Chỉnh lúc này chỉ để thử, thoát Play là mất. Muốn sửa số gốc thì thoát Play rồi chỉnh, " +
                                    "đừng chép số đang thấy lúc Play sang.", MessageType.Warning);
        }

        SubHeader("Di chuyển và nhảy");
        DrawFields(player, playerMoveFields);
        // lúc Play code tự đổi trọng lực liên tục (rơi x2.5, nhả nút nhảy x2) nên ô này nhảy số và sửa không ăn -> khóa lại
        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        DrawFields(player.GetComponent<Rigidbody2D>(), rigidbodyFields);
        EditorGUI.EndDisabledGroup();
        if (Application.isPlaying) EditorGUILayout.LabelField("Trọng lực chỉ chỉnh được khi không Play.", EditorStyles.miniLabel);

        SubHeader("Cận chiến và đòn mạnh");
        DrawFields(player, playerMeleeFields);

        SubHeader("Tơ");
        DrawFields(player, playerWebFields);

        SubHeader("Né (Shift)");
        DrawFields(player, playerDodgeFields);

        SubHeader("Né hoàn hảo, đòn dậm, phá kén, giật va chạm");
        DrawFields(player, playerComboFields);

        SubHeader("Máu");
        DrawDamageable(player.GetComponent<Damageable>(), playerDamageableFields);

        SubHeader("Từng đòn đánh (hitbox)");
        foreach (tanCong hitbox in player.GetComponentsInChildren<tanCong>(true))
        {
            EditorGUILayout.LabelField(HitboxLabel(hitbox.gameObject.name), EditorStyles.miniBoldLabel);
            DrawFields(hitbox, hitboxFields);
        }
        EditorGUILayout.HelpBox("Nâng cấp Đấm mạnh cộng vào mọi đòn trừ đòn mạnh; 3 thẻ đòn mạnh chỉ cộng vào đòn mạnh. Lúc không Play, số ở đây là số gốc chưa có nâng cấp.", MessageType.None);

        SubHeader("Tơ đạn (prefab WebPellet)");
        GameObject pellet = LoadPrefab(webPelletPrefabPath);
        if (pellet != null) DrawFields(pellet.GetComponent<Projectile>(), webPelletFields);
    }

    private void DrawEnemySection()
    {
        EditorGUILayout.HelpBox("Chỉnh trên prefab: mọi màn đều ăn theo, trừ con nào trong scene có chỉnh riêng (sẽ báo ở dưới). " +
                                "Đang Play thì quái đã có trong màn không đổi theo, phải chơi lại mới thấy.", MessageType.None);

        GameObject hiepSi = LoadPrefab(hiepSiPrefabPath);
        if (hiepSi != null)
        {
            SubHeader("Quỷ gối (HiepSi)");
            HiepSi walker = hiepSi.GetComponent<HiepSi>();
            Damageable walkerHealth = hiepSi.GetComponent<Damageable>();
            DrawFields(walker, hiepSiFields);
            DrawDamageable(walkerHealth, enemyDamageableFields);
            foreach (tanCong hitbox in hiepSi.GetComponentsInChildren<tanCong>(true))
            {
                EditorGUILayout.LabelField(HitboxLabel(hitbox.gameObject.name), EditorStyles.miniBoldLabel);
                DrawFields(hitbox, hitboxFields);
            }

            List<Component> sceneWalkers = new List<Component>();
            List<Component> sceneWalkerHealth = new List<Component>();
            foreach (HiepSi instance in Object.FindObjectsByType<HiepSi>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sceneWalkers.Add(instance);
                sceneWalkerHealth.Add(instance.GetComponent<Damageable>());
            }
            DrawOverrideWarning(sceneWalkers, hiepSiFields, "con Quỷ gối");
            DrawOverrideWarning(sceneWalkerHealth, HealthFieldsWith(enemyDamageableFields), "con Quỷ gối (máu/trói)");
        }

        GameObject flyingEye = LoadPrefab(flyingEyePrefabPath);
        if (flyingEye != null)
        {
            SubHeader("Quái vật bay (FlyingEye)");
            DrawFields(flyingEye.GetComponent<FlyingEye>(), flyingEyeFields);
            DrawDamageable(flyingEye.GetComponent<Damageable>(), enemyDamageableFields);
            foreach (tanCong hitbox in flyingEye.GetComponentsInChildren<tanCong>(true))
            {
                EditorGUILayout.LabelField(HitboxLabel(hitbox.gameObject.name), EditorStyles.miniBoldLabel);
                DrawFields(hitbox, hitboxFields);
            }

            List<Component> sceneFlyers = new List<Component>();
            List<Component> sceneFlyerHealth = new List<Component>();
            foreach (FlyingEye instance in Object.FindObjectsByType<FlyingEye>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sceneFlyers.Add(instance);
                sceneFlyerHealth.Add(instance.GetComponent<Damageable>());
            }
            DrawOverrideWarning(sceneFlyers, flyingEyeFields, "con quái bay");
            DrawOverrideWarning(sceneFlyerHealth, HealthFieldsWith(enemyDamageableFields), "con quái bay (máu/trói)");
        }

        EditorGUILayout.HelpBox("Tốc độ ra đòn (animation) chỉnh trong Animator: chọn state tấn công > ô Speed. " +
                                "Quỷ gối bọc thép / mất ngủ dùng chung số ở trên rồi tự nhân thêm lúc vào màn (máu, tốc độ, lao húc).", MessageType.None);
    }

    private void DrawUpgradeSection()
    {
        GameObject hud = LoadPrefab(gameHudPrefabPath);
        UpgradeManager upgrades = hud != null ? hud.GetComponent<UpgradeManager>() : null;
        if (upgrades == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy UpgradeManager trong " + gameHudPrefabPath, MessageType.Warning);
            return;
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Đang Play: số sửa ở đây lưu vào prefab, có tác dụng từ lần vào màn sau.", MessageType.None);
        }

        SubHeader("Thanh kiến thức");
        DrawFields(upgrades, knowledgeFields);
        DrawKnowledgePreview(upgrades);

        SubHeader("Các thẻ nâng cấp");
        DrawFields(upgrades, upgradeCardFields);

        SubHeader("Thẻ đang tắt (đặt cấp tối đa > 0 để bật lại)");
        DrawFields(upgrades, disabledCardFields);

        List<Component> sceneManagers = new List<Component>();
        foreach (UpgradeManager instance in Object.FindObjectsByType<UpgradeManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sceneManagers.Add(instance);
        }
        DrawOverrideWarning(sceneManagers, knowledgeFields, "GameHUD");
        DrawOverrideWarning(sceneManagers, upgradeCardFields, "GameHUD");
        DrawOverrideWarning(sceneManagers, disabledCardFields, "GameHUD");
    }

    private void DrawItemSection()
    {
        SubHeader("Bình tơ (prefab WebAmmoPickUp)");
        GameObject ammo = LoadPrefab(webAmmoPrefabPath);
        if (ammo != null) DrawFields(ammo.GetComponent<WebAmmoPickUp>(), webAmmoFields);
        EditorGUILayout.HelpBox("Trạm hồi tơ (chữ A trong file màn) tự đặt 'Hiện lại sau' theo dòng @hoiTo của file màn lúc dựng.", MessageType.None);

        SubHeader("Đùi gà (prefab HealthPickUp)");
        GameObject health = LoadPrefab(healthPrefabPath);
        if (health != null) DrawFields(health.GetComponent<HealthPickup>(), healthFields);

        SubHeader("Chết và hồi sinh (prefab GameHUD)");
        GameObject hud = LoadPrefab(gameHudPrefabPath);
        if (hud != null) DrawFields(hud.GetComponent<CheckpointManager>(), checkpointFields);
    }

    private void DrawSaveSection()
    {
        EditorGUILayout.LabelField("Đang mở tới", "Màn " + GameProgress.UnlockedLevel + " / " + GameProgress.LevelCount);

        EditorGUI.BeginDisabledGroup(Application.isPlaying);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mở khóa tất cả màn"))
        {
            GameProgress.UnlockAllLevels();
        }
        if (GUILayout.Button("Xóa tiến độ + nâng cấp + kiến thức"))
        {
            if (EditorUtility.DisplayDialog("Xóa tiến độ", "Xóa màn đã mở, mọi cấp nâng cấp và kiến thức (giống nút Chơi mới)?", "Xóa", "Thôi"))
            {
                GameProgress.ResetProgress();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Quái + sách đã cho kiến thức", PlayerUpgrades.CollectedCount.ToString());
        if (GUILayout.Button("Quên hết (cho nhận lại)", GUILayout.Width(170f)))
        {
            PlayerUpgrades.ResetCollected();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Mỗi quái/sách chỉ cho kiến thức 1 lần trong cả game; thử đi thử lại 1 màn thì bấm \"Quên hết\".", EditorStyles.wordWrappedMiniLabel);

        int knowledge = EditorGUILayout.IntField("Kiến thức đang tích", PlayerUpgrades.Knowledge);
        if (knowledge != PlayerUpgrades.Knowledge)
        {
            PlayerUpgrades.Knowledge = knowledge;
        }

        GameObject hud = LoadPrefab(gameHudPrefabPath);
        UpgradeManager upgrades = hud != null ? hud.GetComponent<UpgradeManager>() : null;

        EditorGUILayout.LabelField("Cấp nâng cấp đã lưu (bấm - / + để đặt thử)", EditorStyles.miniBoldLabel);
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            int level = PlayerUpgrades.GetLevel(type);
            string name = upgrades != null ? upgrades.DisplayName(type) : type.ToString();
            string max = upgrades != null ? " / " + upgrades.MaxLevel(type) : "";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, "Cấp " + level + max);
            if (GUILayout.Button("-", GUILayout.Width(28f)) && level > 0)
            {
                PlayerUpgrades.SetLevel(type, level - 1);
            }
            if (GUILayout.Button("+", GUILayout.Width(28f)))
            {
                PlayerUpgrades.SetLevel(type, level + 1);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.HelpBox(Application.isPlaying
            ? "Đang Play: thoát Play rồi mới đặt được (game đang dùng số cũ)."
            : "Số này nằm trong PlayerPrefs của máy, dùng chung cho mọi lần Play. Cấp vượt tối đa thì game chỉ tính tới mức tối đa.",
            MessageType.None);
    }

    // ===== Vẽ ô =====

    // tiêu đề nhóm thu gọn được
    private static bool Section(bool open, string title)
    {
        EditorGUILayout.Space(4f);
        return EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
    }

    private static void SubHeader(string title)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    // vẽ các ô của 1 component qua SerializedObject (giống Inspector: có Ctrl+Z, chỉnh trong scene thành override đúng cách)
    private static void DrawFields(Object target, string[,] fields)
    {
        if (target == null) return;

        SerializedObject serialized = new SerializedObject(target);
        serialized.Update();

        for (int i = 0; i < fields.GetLength(0); i++)
        {
            SerializedProperty property = serialized.FindProperty(fields[i, 0]);
            if (property == null) continue;
            EditorGUILayout.PropertyField(property, new GUIContent(fields[i, 1], property.tooltip), true);
        }

        if (serialized.ApplyModifiedProperties())
        {
            MarkAssetDirty(target);
        }
    }

    // máu: sửa máu tối đa thì máu lúc bắt đầu (_health) cũng đổi theo, không thì vào màn quái/người chơi không đầy máu
    private static void DrawDamageable(Damageable target, string[,] otherFields)
    {
        if (target == null) return;

        SerializedObject serialized = new SerializedObject(target);
        serialized.Update();

        SerializedProperty maxHealth = serialized.FindProperty("_maxHealth");
        SerializedProperty health = serialized.FindProperty("_health");
        if (maxHealth != null)
        {
            // lúc Play sửa thẳng _health sẽ bỏ qua thanh máu (không báo healthChanged) và hồi máu ngoài ý muốn -> chỉ chỉnh khi không Play
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(maxHealth, new GUIContent("Máu tối đa" + (Application.isPlaying ? " (thoát Play để chỉnh)" : "")));
            if (EditorGUI.EndChangeCheck() && health != null)
            {
                health.intValue = maxHealth.intValue;
            }
            EditorGUI.EndDisabledGroup();
        }

        for (int i = 0; i < otherFields.GetLength(0); i++)
        {
            SerializedProperty property = serialized.FindProperty(otherFields[i, 0]);
            if (property == null) continue;
            EditorGUILayout.PropertyField(property, new GUIContent(otherFields[i, 1], property.tooltip), true);
        }

        if (serialized.ApplyModifiedProperties())
        {
            MarkAssetDirty(target);
        }
    }

    // thêm 2 ô máu vào danh sách để kiểm tra chỉnh riêng trong scene
    private static string[,] HealthFieldsWith(string[,] fields)
    {
        int count = fields.GetLength(0);
        string[,] result = new string[count + 2, 2];
        result[0, 0] = "_maxHealth";
        result[0, 1] = "Máu tối đa";
        result[1, 0] = "_health";
        result[1, 1] = "Máu lúc đầu";
        for (int i = 0; i < count; i++)
        {
            result[i + 2, 0] = fields[i, 0];
            result[i + 2, 1] = fields[i, 1];
        }
        return result;
    }

    // con nào trong scene đang mở có chỉnh riêng (override) thì số trong scene thắng prefab -> báo + nút bỏ chỉnh riêng
    private static void DrawOverrideWarning(List<Component> sceneObjects, string[,] fields, string what)
    {
        if (Application.isPlaying) return;

        List<SerializedProperty> overridden = new List<SerializedProperty>();
        List<string> names = new List<string>();
        int objectCount = 0;

        foreach (Component component in sceneObjects)
        {
            if (component == null || !PrefabUtility.IsPartOfPrefabInstance(component)) continue;

            SerializedObject serialized = new SerializedObject(component);
            bool any = false;
            for (int i = 0; i < fields.GetLength(0); i++)
            {
                SerializedProperty property = serialized.FindProperty(fields[i, 0]);
                if (property == null || !property.prefabOverride) continue;

                overridden.Add(property);
                if (!names.Contains(fields[i, 1])) names.Add(fields[i, 1]);
                any = true;
            }
            if (any) objectCount++;
        }

        if (overridden.Count == 0) return;

        EditorGUILayout.HelpBox(objectCount + " " + what + " trong scene đang mở có chỉnh riêng nên KHÔNG ăn theo số ở trên: " +
                                string.Join(", ", names.ToArray()) + ".", MessageType.Warning);
        if (GUILayout.Button("Bỏ chỉnh riêng trong scene, dùng số ở trên (" + what + ")"))
        {
            foreach (SerializedProperty property in overridden)
            {
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
            }
        }
    }

    // xem trước: lần nâng cấp thứ mấy cần bao nhiêu kiến thức, màn đang mở cho được bao nhiêu
    private static void DrawKnowledgePreview(UpgradeManager upgrades)
    {
        int totalLevels = 0;
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            totalLevels += Mathf.Max(0, upgrades.MaxLevel(type));
        }

        StringBuilder text = new StringBuilder();
        text.Append("Có tất cả " + totalLevels + " cấp nâng cấp. Cần (cộng dồn):\n");
        int sum = 0;
        for (int i = 0; i < totalLevels; i++)
        {
            int need = upgrades.KnowledgeThreshold(i);
            sum += need;
            text.Append("Lần " + (i + 1) + ": " + need + " (" + sum + ")");
            text.Append(i % 3 == 2 ? "\n" : "    ");
        }

        // đếm kẻ địch + sách trong scene đang mở để biết 1 màn cho được mấy lần nâng cấp
        int enemies = 0;
        foreach (Damageable damageable in Object.FindObjectsByType<Damageable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (damageable.GetComponent<PlayerController>() == null) enemies++;
        }
        int books = Object.FindObjectsByType<UpgradePickUp>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        if (enemies > 0 || books > 0)
        {
            int gained = enemies * upgrades.knowledgePerKill + books * upgrades.knowledgePerBook;
            int count = 0;
            int left = gained;
            while (count < totalLevels && left >= upgrades.KnowledgeThreshold(count))
            {
                left -= upgrades.KnowledgeThreshold(count);
                count++;
            }
            text.Append("\nScene đang mở: " + enemies + " địch + " + books + " sách = " + gained +
                        " kiến thức -> đủ " + count + " lần nâng cấp nếu bắt đầu từ 0.");
        }

        EditorGUILayout.HelpBox(text.ToString(), MessageType.None);
    }

    private static string HitboxLabel(string objectName)
    {
        switch (objectName)
        {
            case "playerAttack1": return "Đấm 1 (dưới đất)";
            case "playerAttack2": return "Đấm 2 (dưới đất)";
            case "playerAttack3": return "Đấm 3 (dưới đất)";
            case "playerAttack4": return "Đòn mạnh (phím I)";
            case "AirAttack1": return "Đòn trên không 1";
            case "AirAttack2": return "Đòn trên không 2";
            case "AirAttack3": return "Đòn trên không 3";
            case "BiteAttack": return "Cắn (BiteAttack)";
            case "SpinningAttack": return "Xoay (SpinningAttack - chưa dùng)";
        }
        return "Hitbox: " + objectName;
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy " + path, MessageType.Warning);
        }
        return prefab;
    }

    // object trong scene thì Unity tự đánh dấu scene cần lưu; prefab thì phải tự đánh dấu rồi ghi ra file
    private static void MarkAssetDirty(Object target)
    {
        if (!EditorUtility.IsPersistent(target)) return;

        EditorUtility.SetDirty(target);
        assetsDirty = true;
    }

    private static void SavePendingAssets()
    {
        if (!assetsDirty) return;

        assetsDirty = false;
        AssetDatabase.SaveAssets();
    }
}
