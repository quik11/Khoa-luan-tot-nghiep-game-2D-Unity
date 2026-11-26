using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Chữ trên màn hình khi chơi: dòng thông báo (quái còn lại / cửa đã mở), số đạn tơ, đòn mạnh và thanh kiến thức.
// Nằm trong prefab Assets/UI/GameHUD.prefab - màn chơi nào cũng thả 1 cái vào là có.
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("Chữ")]
    [TenHienThi("Dòng thông báo")] public TMP_Text statusText;
    [TenHienThi("Chữ số tơ")] public TMP_Text ammoText;
    [TenHienThi("Chữ đòn mạnh")] public TMP_Text heavyText; // trạng thái đòn mạnh phím I (chưa mở khóa thì để trống)

    [Header("Thông báo")]
    [TenHienThi("Hiện trong")] public float messageDuration = 2.5f;

    [Header("Thanh kiến thức")]
    [TenHienThi("Hiện thanh")] public bool showKnowledgeBar = true;
    [Tooltip("Góc trên phải của thanh, tính từ góc trên phải màn hình (khung chuẩn 800x600)")]
    [TenHienThi("Vị trí")] public Vector2 knowledgeBarPosition = new Vector2(-15f, -78f);
    [Tooltip("Rộng x cao của cả cụm (chữ ở trên, thanh ở dưới)")]
    [TenHienThi("Kích thước")] public Vector2 knowledgeBarSize = new Vector2(200f, 42f);
    [TenHienThi("Màu thanh")] public Color knowledgeFillColor = new Color(1f, 0.78f, 0.25f, 1f);
    [TenHienThi("Màu nền")] public Color knowledgeBackColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Dòng lượt hồi sinh")]
    [Tooltip("Tâm dòng chữ, tính từ góc trên phải màn hình (khung chuẩn 800x600)")]
    [TenHienThi("Vị trí")] public Vector2 livesTextPosition = new Vector2(-130f, -140f);

    private PlayerController player;
    private string baseStatus = "";
    private float messageEndTime = -1f;
    private int shownAmmo = -1;
    private int shownMaxAmmo = -1;
    private string shownHeavy = null;

    private TMP_Text knowledgeLabel;
    private RectTransform knowledgeFill;
    private Image knowledgeFillImage;
    private int shownKnowledge = -1;
    private int shownKnowledgeNeeded = -1;
    private int shownKnowledgeGain = -1;
    private bool shownKnowledgeMaxed = false;

    private TMP_Text livesText;

    // dòng chữ hướng dẫn (TutorialHints gọi), tạo bằng code: nhân bản dòng thông báo rồi đặt cao hơn 1 chút
    private TMP_Text hintText;
    private float hintEndTime = -1f;
    private float hintDuration = 1f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
        }

        if (player == null && ammoText != null)
        {
            ammoText.text = "";
        }

        // trong prefab dòng đòn mạnh (HeavyText) nằm cuối nên bị vẽ đè lên bảng tạm dừng/chọn nâng cấp
        // -> dời về ngay sau dòng đạn tơ (đứng trước các bảng), bảng mở ra sẽ che lên nó
        if (heavyText != null && ammoText != null && heavyText.transform.parent == ammoText.transform.parent)
        {
            heavyText.transform.SetSiblingIndex(ammoText.transform.GetSiblingIndex() + 1);
        }

        if (showKnowledgeBar && UpgradeManager.Instance != null)
        {
            CreateKnowledgeBar();
        }
    }

    // Thanh kiến thức dựng bằng code cho khỏi phải sửa prefab: 1 dòng chữ "Kiến thức: x/y" + khung nền + phần đầy.
    // Phần đầy không cần hình, chỉ là ô màu co giãn theo tỉ lệ (anchorMax.x = x/y).
    private void CreateKnowledgeBar()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("ThanhKienThuc", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        // nằm ngay sau dòng đòn mạnh/đạn tơ (2 dòng này đã được dời lên trước các bảng ở Start),
        // tức là dưới các bảng tạm dừng/nâng cấp: bảng mở thì che thanh, không bị thanh đè lên
        TMP_Text anchorText = heavyText != null && heavyText.transform.parent == canvas.transform ? heavyText : ammoText;
        if (anchorText != null && anchorText.transform.parent == canvas.transform)
        {
            root.transform.SetSiblingIndex(anchorText.transform.GetSiblingIndex() + 1);
        }

        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = knowledgeBarPosition;
        rootRect.sizeDelta = knowledgeBarSize;

        // chữ ở nửa trên, căn phải cho thẳng hàng với dòng "Tơ: 6/6"
        GameObject labelObject = new GameObject("Chu", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, knowledgeBarSize.y - 14f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        // mượn font của dòng đạn tơ (LiberationSans SDF, có chữ tiếng Việt có dấu); font pixel không có dấu
        TMP_FontAsset font = ammoText != null ? ammoText.font : TMP_Settings.defaultFontAsset;
        GameFont.Setup(label, font, 18f); // font pixel VT323 nếu đã tạo, không thì font cũ
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Right;
        label.color = new Color(1f, 0.92f, 0.7f, 1f);
        label.raycastTarget = false;
        label.richText = true;
        knowledgeLabel = label;

        // khung nền tối ở đáy cụm
        GameObject backObject = new GameObject("Nen", typeof(RectTransform), typeof(Image));
        backObject.transform.SetParent(root.transform, false);
        RectTransform backRect = (RectTransform)backObject.transform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 2f);
        backRect.sizeDelta = new Vector2(0f, 10f);
        Image backImage = backObject.GetComponent<Image>();
        backImage.color = knowledgeBackColor;
        backImage.raycastTarget = false;

        // phần đầy nằm trong khung, chừa viền 2px
        GameObject fillObject = new GameObject("PhanDay", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backObject.transform, false);
        knowledgeFill = (RectTransform)fillObject.transform;
        knowledgeFill.anchorMin = new Vector2(0f, 0f);
        knowledgeFill.anchorMax = new Vector2(0f, 1f);
        knowledgeFill.offsetMin = new Vector2(2f, 2f);
        knowledgeFill.offsetMax = new Vector2(-2f, -2f);
        knowledgeFillImage = fillObject.GetComponent<Image>();
        knowledgeFillImage.color = knowledgeFillColor;
        knowledgeFillImage.raycastTarget = false;

        UpdateKnowledgeBar();
    }

    private void UpdateKnowledgeBar()
    {
        if (knowledgeLabel == null || knowledgeFill == null) return;

        UpgradeManager upgrades = UpgradeManager.Instance;
        if (upgrades == null) return;

        bool maxed = upgrades.AllMaxed;
        int needed = upgrades.KnowledgeNeeded;
        int current = Mathf.Min(upgrades.Knowledge, needed); // dư thì để dành cho thanh sau, thanh này chỉ hiện đầy
        int gain = upgrades.RecentGain;

        // chỉ dựng lại chữ khi có gì thay đổi
        if (maxed == shownKnowledgeMaxed && current == shownKnowledge && needed == shownKnowledgeNeeded && gain == shownKnowledgeGain) return;

        shownKnowledgeMaxed = maxed;
        shownKnowledge = current;
        shownKnowledgeNeeded = needed;
        shownKnowledgeGain = gain;

        float fraction = maxed ? 1f : Mathf.Clamp01((float)current / Mathf.Max(1, needed));
        knowledgeFill.anchorMax = new Vector2(fraction, 1f);
        knowledgeFillImage.enabled = fraction > 0f; // thanh rỗng mà vẫn vẽ thì lòi ra 1 vạch nhỏ do phần viền 2px

        string text = maxed ? "Kiến thức: Tối đa" : "Kiến thức: " + current + "/" + needed;
        if (gain > 0 && !maxed)
        {
            text += "  <color=#FFE066>+" + gain + "</color>";
        }
        knowledgeLabel.text = text;

        // vừa nhận kiến thức thì thanh sáng lên chốc lát cho dễ để ý
        knowledgeFillImage.color = gain > 0 ? Color.white : knowledgeFillColor;
    }

    private void Update()
    {
        // hết giờ thông báo tạm thì trả lại dòng trạng thái thường (tính theo giờ thật vì lúc chọn nâng cấp game đang dừng)
        if (messageEndTime > 0f && Time.unscaledTime >= messageEndTime)
        {
            messageEndTime = -1f;
            if (statusText != null) statusText.text = baseStatus;
        }

        UpdateKnowledgeBar();
        UpdateHint();

        if (player == null || ammoText == null) return;

        // chỉ đổi chữ khi số đạn thay đổi, khỏi tạo chuỗi mới mỗi khung hình
        if (player.CurrentWebAmmo != shownAmmo || player.MaxWebAmmo != shownMaxAmmo)
        {
            shownAmmo = player.CurrentWebAmmo;
            shownMaxAmmo = player.MaxWebAmmo;
            ammoText.text = "Tơ: " + shownAmmo + "/" + shownMaxAmmo;
        }

        if (heavyText != null)
        {
            string heavy = "";
            if (player.HeavyUnlocked)
            {
                float remaining = player.HeavyCooldownRemaining;
                heavy = remaining > 0f ? "Đòn mạnh (I): " + Mathf.CeilToInt(remaining) + "s" : "Đòn mạnh (I): sẵn sàng";
            }

            if (heavy != shownHeavy)
            {
                shownHeavy = heavy;
                heavyText.text = heavy;
            }
        }
    }

    // CheckpointManager gọi mỗi khi số lượt hồi sinh thay đổi. max < 0 = không giới hạn thì ẩn dòng này
    public void SetLives(int left, int max)
    {
        if (max < 0)
        {
            if (livesText != null) livesText.gameObject.SetActive(false);
            return;
        }

        if (livesText == null) livesText = CreateLivesText();
        if (livesText == null) return;

        livesText.gameObject.SetActive(true);
        livesText.text = "Lượt hồi sinh: " + Mathf.Max(0, left) + "/" + max;
        // lượt cuối thì đổi màu đỏ cho người chơi để ý
        livesText.color = left <= 0 ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(0.85f, 0.87f, 0.92f, 1f);
    }

    // nhân bản dòng đòn mạnh (cùng font có dấu, cùng cỡ chữ, căn phải) rồi dời xuống dưới thanh kiến thức
    private TMP_Text CreateLivesText()
    {
        TMP_Text template = heavyText != null ? heavyText : ammoText;
        if (template == null) return null;

        TMP_Text text = Instantiate(template, template.transform.parent);
        text.name = "LuotHoiSinh";
        text.raycastTarget = false;
        // đặt ngay sau dòng đạn tơ (dòng này luôn đứng trước các bảng tạm dừng/nâng cấp trong prefab) -> bảng mở ra sẽ che lên.
        // Không đặt theo dòng đòn mạnh vì GameHUD.Start mới dời dòng đó lên, mà Start của CheckpointManager có thể chạy trước
        Transform anchor = ammoText != null ? ammoText.transform : template.transform;
        text.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
        ((RectTransform)text.transform).anchoredPosition = livesTextPosition;
        return text;
    }

    // hiện 1 câu hướng dẫn trong duration giây (tính theo giờ trong game: lúc tạm dừng thì câu đứng yên chờ)
    public void ShowHint(string text, float duration)
    {
        if (hintText == null) hintText = CreateHintText();
        if (hintText == null) return;

        hintText.text = text;
        hintText.gameObject.SetActive(true);
        hintDuration = Mathf.Max(0.5f, duration);
        hintEndTime = Time.time + hintDuration;
    }

    private void UpdateHint()
    {
        if (hintText == null || !hintText.gameObject.activeSelf) return;

        float left = hintEndTime - Time.time;
        if (left <= 0f)
        {
            hintText.gameObject.SetActive(false);
            return;
        }

        // nửa giây cuối mờ dần
        Color c = hintText.color;
        c.a = Mathf.Clamp01(left / 0.5f);
        hintText.color = c;
    }

    private TMP_Text CreateHintText()
    {
        if (statusText == null) return null;

        TMP_Text text = Instantiate(statusText, statusText.transform.parent);
        text.name = "HuongDan";
        text.raycastTarget = false;
        // đứng ngay sau dòng thông báo (cùng lớp với chữ HUD, dưới các bảng tạm dừng/nâng cấp)
        text.transform.SetSiblingIndex(statusText.transform.GetSiblingIndex() + 1);
        RectTransform rect = (RectTransform)text.transform;
        rect.anchoredPosition = ((RectTransform)statusText.transform).anchoredPosition + new Vector2(0f, 40f);
        text.color = new Color(1f, 0.92f, 0.55f, 1f);
        text.text = "";
        return text;
    }

    public void SetStatus(string text)
    {
        baseStatus = text;
        if (statusText != null && messageEndTime < 0f) statusText.text = text;
    }

    // hiện 1 câu trong vài giây rồi tự trả lại dòng trạng thái thường
    public void ShowMessage(string text)
    {
        messageEndTime = Time.unscaledTime + messageDuration;
        if (statusText != null) statusText.text = text;
    }
}
