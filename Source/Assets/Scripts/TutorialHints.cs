using System.Collections.Generic;
using UnityEngine;

// Chữ hướng dẫn hiện đúng lúc cần (lần đầu tới gần Quỷ gối, lần đầu áp tường...) thay vì gắn theo tọa độ trên bản đồ,
// nên sửa bản đồ thế nào cũng không lệch. Nội dung + điều kiện nằm trong Assets/Resources/HuongDan.txt.
// LevelStats tự gắn component này khi vào màn.
public class TutorialHints : MonoBehaviour
{
    private const float checkInterval = 0.2f;
    private const float hintDuration = 5f;
    private const float nearDistance = 7f;      // "tới gần" quái = trong bấy nhiêu ô
    private const float nearBookDistance = 6f;

    private class ActiveHint
    {
        public string condition;
        public string text;
        public bool done;
    }

    private readonly List<ActiveHint> hints = new List<ActiveHint>();
    private readonly Queue<string> waiting = new Queue<string>();

    private PlayerController player;
    private Damageable playerHealth;
    private TouchingDirections playerSensors;
    private HiepSi[] pillows;
    private FlyingEye[] flyers;
    private UpgradePickUp[] books;
    private BossController boss;

    private float sinceStart = -1f; // đếm từ lúc bảng cốt truyện đóng
    private float checkTimer;
    private float hintEndTime;

    private void Start()
    {
        foreach (LevelTexts.Hint hint in LevelTexts.HintsFor(GameProgress.CurrentLevel))
        {
            if (!IsKnown(hint.condition))
            {
                Debug.LogWarning("HuongDan.txt: không hiểu điều kiện \"" + hint.condition + "\" - câu \"" + hint.text + "\" sẽ không hiện.");
                continue;
            }
            hints.Add(new ActiveHint { condition = hint.condition, text = hint.text });
        }
        if (hints.Count == 0)
        {
            enabled = false;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            playerHealth = playerObject.GetComponent<Damageable>();
            playerSensors = playerObject.GetComponent<TouchingDirections>();
        }

        pillows = FindObjectsByType<HiepSi>(FindObjectsSortMode.None);
        flyers = FindObjectsByType<FlyingEye>(FindObjectsSortMode.None);
        books = FindObjectsByType<UpgradePickUp>(FindObjectsSortMode.None);
        boss = FindFirstObjectByType<BossController>();
    }

    private void Update()
    {
        if (player == null || PauseMenu.IsPaused || !StoryIntro.Finished) return;

        if (sinceStart < 0f) sinceStart = 0f;
        sinceStart += Time.deltaTime;

        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = checkInterval;
            foreach (ActiveHint hint in hints)
            {
                if (!hint.done && IsMet(hint.condition))
                {
                    hint.done = true;
                    waiting.Enqueue(hint.text);
                }
            }
        }

        // câu trước hiện đủ lâu rồi mới tới câu sau
        if (waiting.Count > 0 && Time.time >= hintEndTime && GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowHint(waiting.Dequeue(), hintDuration);
            hintEndTime = Time.time + hintDuration;
        }
    }

    private bool IsMet(string condition)
    {
        if (condition == "batdau") return sinceStart >= 0.6f;
        if (condition.StartsWith("giay_")) return sinceStart >= ParseNumber(condition, "giay_");
        if (condition.StartsWith("ha_quai_")) return LevelStats.Instance != null && LevelStats.Instance.Kills >= ParseNumber(condition, "ha_quai_");

        Vector2 position = player.transform.position;
        switch (condition)
        {
            case "gan_goi":
                foreach (HiepSi pillow in pillows)
                {
                    if (pillow != null && IsAlive(pillow) && Vector2.Distance(position, pillow.transform.position) <= nearDistance) return true;
                }
                return false;
            case "gan_quaibay":
                foreach (FlyingEye flyer in flyers)
                {
                    if (flyer != null && IsAlive(flyer) && Vector2.Distance(position, flyer.transform.position) <= nearDistance) return true;
                }
                return false;
            case "gan_sach":
                foreach (UpgradePickUp book in books)
                {
                    if (book != null && book.gameObject.activeInHierarchy && Vector2.Distance(position, book.transform.position) <= nearBookDistance) return true;
                }
                return false;
            case "cham_tuong":
                return playerSensors != null && playerSensors.isOnWall && !playerSensors.isGrounded;
            case "mau_thap":
                return playerHealth != null && playerHealth.isAlive && playerHealth.Health * 2 <= playerHealth.MaxHealth;
            case "het_to":
                return player.CurrentWebAmmo <= 0;
            case "khien_boss":
                return boss != null && boss.State == BossController.BossState.Shield;
            case "goi_lao":
                return HiepSi.AnyChargeWarned;
            case "quaibay_bonhao":
                return FlyingEye.AnyDiveWarned;
            case "gan_bocthep":
                return NearPillow(position, HiepSi.BienThe.BocThep);
            case "gan_matngu":
                return NearPillow(position, HiepSi.BienThe.MatNgu);
        }

        return false;
    }

    private static readonly string[] knownConditions =
        { "batdau", "gan_goi", "gan_quaibay", "gan_sach", "cham_tuong", "mau_thap", "het_to", "khien_boss",
          "goi_lao", "quaibay_bonhao", "gan_bocthep", "gan_matngu" };

    private bool NearPillow(Vector2 position, HiepSi.BienThe variant)
    {
        foreach (HiepSi pillow in pillows)
        {
            if (pillow != null && pillow.bienThe == variant && IsAlive(pillow) && Vector2.Distance(position, pillow.transform.position) <= nearDistance) return true;
        }
        return false;
    }

    private static bool IsKnown(string condition)
    {
        if (condition.StartsWith("giay_") || condition.StartsWith("ha_quai_")) return true;
        return System.Array.IndexOf(knownConditions, condition) >= 0;
    }

    private static bool IsAlive(Component component)
    {
        Damageable damageable = component.GetComponent<Damageable>();
        return damageable == null || damageable.isAlive;
    }

    private static float ParseNumber(string condition, string prefix)
    {
        float value;
        return float.TryParse(condition.Substring(prefix.Length), System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0f;
    }
}
