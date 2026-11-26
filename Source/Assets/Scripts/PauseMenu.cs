using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu tạm dừng khi bấm Esc trong màn chơi (Esc đi qua UIManager.OnExitGame -> PauseMenu.HandleEscape).
// Nằm trong prefab Assets/UI/GameHUD.prefab cùng với GameHUD.
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    // đang dừng vì bảng khác (vd bảng chọn nâng cấp) - Esc không mở menu tạm dừng chồng lên
    public static bool IsModalOpen { get; private set; }

    [Header("Bảng tạm dừng")]
    [TenHienThi("Bảng")] public GameObject pausePanel;
    [TenHienThi("Nút chọn sẵn")] public GameObject pauseFirstButton; // nút "Tiếp tục", chọn sẵn để bấm Enter là chơi tiếp

    [Header("Bảng cài đặt")]
    [TenHienThi("Bảng")] public GameObject settingsPanel;
    [TenHienThi("Nút chọn sẵn")] public GameObject settingsFirstButton;

    [Header("Âm thanh")]
    [TenHienThi("Tạm dừng")] public AudioClip pauseSound;
    [TenHienThi("Tiếp tục")] public AudioClip resumeSound;

    private PlayerInput playerInput;

    // bảng "Hết lượt hồi sinh": nhân bản từ bảng tạm dừng lúc cần (khỏi phải sửa prefab), giữ đúng kiểu nút/chữ
    private GameObject gameOverPanel;
    private GameObject gameOverFirstButton;

    // nút "Hướng dẫn phím" thêm vào bảng tạm dừng + bảng phím (đều tạo bằng code lúc vào màn)
    private GameObject controlsPanel;
    private GameObject controlsButton;
    private GameObject controlsBackButton;

    private const string controlsText =
        "<color=#00FFDB>A / D</color> hoặc phím mũi tên: Di chuyển\n" +
        "<color=#00FFDB>Space</color>: Nhảy (giữ lâu để nhảy cao hơn)\n" +
        "Đang nhảy áp vào tường + <color=#00FFDB>Space</color>: Bật tường\n" +
        "<color=#00FFDB>U</color>: Đánh (bấm liên tục ra chuỗi 3 đòn), đứng trong cửa đã mở: qua màn\n" +
        "Trên không giữ <color=#00FFDB>S</color> + <color=#00FFDB>U</color>: Dậm xuống đất\n" +
        "<color=#00FFDB>I</color>: Đòn mạnh (mở khóa sau Màn 1, hồi chiêu 7 giây)\n" +
        "<color=#00FFDB>R</color>: Tơ đạn - trúng 2 phát liền sẽ trói địch\n" +
        "<color=#00FFDB>Q</color>: Tơ kéo - bay tới tường hoặc giật địch đang bị trói\n" +
        "<color=#00FFDB>Left Shift</color>: Lộn né (trên mặt đất) - né đúng lúc quái nháy đỏ là né hoàn hảo\n" +
        "<color=#00FFDB>Esc</color>: Tạm dừng";

    // bảng chữ dùng chung (kết quả qua màn, cốt truyện đầu màn) - cũng nhân bản từ bảng tạm dừng
    private GameObject textPanel;
    private GameObject textPanelFirstButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        IsPaused = false;
        IsModalOpen = false;
    }

    private void Awake()
    {
        Instance = this;
        IsPaused = false;
        IsModalOpen = false;
    }

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerInput = player.GetComponent<PlayerInput>();

        AddControlsButton();
    }

    // nhân bản nút "Cài đặt" thành nút "Hướng dẫn phím", dàn lại 5 nút cho cách đều
    private void AddControlsButton()
    {
        if (pausePanel == null) return;
        Transform settingsButton = pausePanel.transform.Find("Button_CaiDat");
        if (settingsButton == null) return;

        GameObject copy = Instantiate(settingsButton.gameObject, pausePanel.transform);
        copy.name = "Button_HuongDan";
        copy.transform.SetSiblingIndex(settingsButton.GetSiblingIndex() + 1);
        Button button = copy.GetComponent<Button>();
        if (button == null) { Destroy(copy); return; }
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenControls);
        TMPro.TMP_Text label = copy.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label != null) label.text = "HƯỚNG DẪN PHÍM";
        controlsButton = copy;

        string[] order = { "Button_TiepTuc", "Button_CaiDat", "Button_HuongDan", "Button_ChonMan", "Button_VeMenu" };
        float y = 72f;
        List<Button> buttons = new List<Button>();
        foreach (string name in order)
        {
            Transform t = pausePanel.transform.Find(name);
            if (t == null) continue;
            RectTransform rect = (RectTransform)t;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            y -= 48f;
            Button b = t.GetComponent<Button>();
            if (b != null) buttons.Add(b);
        }
        LinkVertical(buttons.ToArray());
    }

    public void OpenControls()
    {
        if (pausePanel == null) return;

        if (controlsPanel == null)
        {
            controlsPanel = Instantiate(pausePanel, pausePanel.transform.parent);
            controlsPanel.name = "BangPhim";

            Transform title = controlsPanel.transform.Find("TieuDe");
            if (title != null)
            {
                TMPro.TMP_Text titleText = title.GetComponent<TMPro.TMP_Text>();
                if (titleText != null) titleText.text = "HƯỚNG DẪN ĐIỀU KHIỂN";

                GameObject note = Instantiate(title.gameObject, controlsPanel.transform);
                note.name = "NoiDung";
                RectTransform noteRect = (RectTransform)note.transform;
                noteRect.sizeDelta = new Vector2(700f, 230f);
                noteRect.anchoredPosition = new Vector2(((RectTransform)title).anchoredPosition.x, 10f);
                TMPro.TMP_Text noteText = note.GetComponent<TMPro.TMP_Text>();
                if (noteText != null)
                {
                    noteText.fontSize = noteText.fontSize * 0.42f;
                    noteText.enableAutoSizing = false;
                    noteText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                    noteText.alignment = TMPro.TextAlignmentOptions.Left;
                    noteText.richText = true;
                    noteText.text = controlsText;
                }
            }

            Button back = SetupButton(controlsPanel, "Button_TiepTuc", "QUAY LẠI", CloseControls, -140f);
            foreach (string unused in new[] { "Button_CaiDat", "Button_HuongDan", "Button_ChonMan", "Button_VeMenu" })
            {
                Transform child = controlsPanel.transform.Find(unused);
                if (child != null) Destroy(child.gameObject);
            }
            controlsBackButton = back != null ? back.gameObject : null;
        }

        pausePanel.SetActive(false);
        controlsPanel.SetActive(true);
        controlsPanel.transform.SetAsLastSibling();
        MenuSelection.Select(controlsBackButton);
    }

    public void CloseControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (pausePanel != null && IsPaused) pausePanel.SetActive(true);
        MenuSelection.Select(controlsButton != null ? controlsButton : pauseFirstButton);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;

        // rời scene giữa lúc đang dừng thì trả lại tốc độ game cho scene sau
        IsModalOpen = false;
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (textPanel != null && textPanel.activeSelf)
        {
            MenuSelection.ReselectOnKeyPress(textPanelFirstButton);
            return;
        }

        if (gameOverPanel != null && gameOverPanel.activeSelf)
        {
            MenuSelection.ReselectOnKeyPress(gameOverFirstButton);
            return;
        }

        if (!IsPaused || IsModalOpen) return;

        if (controlsPanel != null && controlsPanel.activeSelf)
        {
            MenuSelection.ReselectOnKeyPress(controlsBackButton);
            return;
        }

        bool settingsOpen = settingsPanel != null && settingsPanel.activeSelf;
        MenuSelection.ReselectOnKeyPress(settingsOpen ? settingsFirstButton : pauseFirstButton);
    }

    // trả về false nếu scene không có menu tạm dừng (UIManager sẽ tự xử lý Esc kiểu cũ)
    public static bool HandleEscape()
    {
        if (Instance == null) return false;

        Instance.OnEscape();
        return true;
    }

    private void OnEscape()
    {
        if (IsModalOpen) return; // đang chọn nâng cấp thì bắt buộc chọn xong mới chơi tiếp

        if (controlsPanel != null && controlsPanel.activeSelf)
        {
            CloseControls();
        }
        else if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
        }
        else if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;

        // tắt điều khiển nhân vật để lúc dừng bấm phím không bị dồn lệnh (đánh, nhảy) tới lúc chơi tiếp
        if (playerInput != null) playerInput.DeactivateInput();

        if (pausePanel != null) pausePanel.SetActive(true);
        MenuSelection.Select(pauseFirstButton);
        PlaySound(pauseSound);
    }

    // Nút "Tiếp tục"
    public void Resume()
    {
        if (!IsPaused) return;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        IsPaused = false;
        Time.timeScale = 1f;

        if (playerInput != null) playerInput.ActivateInput();
        PlaySound(resumeSound);
    }

    // Dừng game cho bảng khác (UpgradeManager gọi khi mở bảng chọn nâng cấp), không hiện menu tạm dừng
    public void OpenModal()
    {
        if (IsModalOpen || IsPaused) return;

        IsModalOpen = true;
        IsPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null) playerInput.DeactivateInput();
    }

    public void CloseModal()
    {
        if (!IsModalOpen) return;

        IsModalOpen = false;
        IsPaused = false;
        Time.timeScale = 1f;
        if (playerInput != null) playerInput.ActivateInput();
    }

    // Nút "Cài đặt"
    public void OpenSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(true);
        MenuSelection.Select(settingsFirstButton);
    }

    // Nút "Đóng" trong bảng cài đặt
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        MenuSelection.Select(pauseFirstButton);
    }

    // Nút "Chọn màn": về menu chính, mở sẵn bảng chọn màn
    public void GoToLevelSelect()
    {
        GameProgress.openLevelSelectOnMenu = true;
        LeaveTo(GameProgress.mainMenuScene);
    }

    // Nút "Về menu chính"
    public void GoToMainMenu()
    {
        LeaveTo(GameProgress.mainMenuScene);
    }

    // Nút "Chơi lại màn" (bảng hết lượt hồi sinh): nạp lại màn đang chơi từ đầu.
    // Kiến thức và nâng cấp đã nhận vẫn giữ (đã lưu ngay lúc nhận); quái đã hạ trước đó không cho thêm kiến thức.
    public void RestartLevel()
    {
        LeaveTo(SceneManager.GetActiveScene().name);
    }

    // CheckpointManager gọi khi người chơi chết mà đã hết lượt hồi sinh
    public void ShowGameOver(int respawnsUsed)
    {
        if (gameOverPanel == null) gameOverPanel = BuildGameOverPanel(respawnsUsed);
        if (gameOverPanel == null)
        {
            RestartLevel(); // scene không có bảng tạm dừng để nhân bản thì chơi lại luôn
            return;
        }

        // dừng game như bảng chọn nâng cấp: Esc không mở menu tạm dừng chồng lên, phím nhân vật bị khóa
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        IsModalOpen = true;
        IsPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null) playerInput.DeactivateInput();

        gameOverPanel.SetActive(true);
        gameOverPanel.transform.SetAsLastSibling(); // vẽ trên cùng, đè cả màn đen hồi sinh
        MenuSelection.Select(gameOverFirstButton);
    }

    // ===== bảng chữ dùng chung: tiêu đề + đoạn chữ + tối đa 3 nút (nhãn, hàm) =====
    public struct PanelButton
    {
        public string label;
        public UnityEngine.Events.UnityAction action;
        public PanelButton(string label, UnityEngine.Events.UnityAction action) { this.label = label; this.action = action; }
    }

    // trả về false nếu scene không có bảng tạm dừng để nhân bản (khi đó nơi gọi tự làm cách khác)
    public bool ShowTextPanel(string title, string body, params PanelButton[] buttons)
    {
        if (pausePanel == null || buttons == null || buttons.Length == 0) return false;

        if (textPanel != null) Destroy(textPanel);
        textPanel = BuildTextPanel(title, body, buttons);
        if (textPanel == null) return false;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        IsModalOpen = true;
        IsPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null) playerInput.DeactivateInput();

        textPanel.SetActive(true);
        textPanel.transform.SetAsLastSibling();
        MenuSelection.Select(textPanelFirstButton);
        return true;
    }

    // đóng bảng chữ và chơi tiếp (nút "Bắt đầu" của bảng cốt truyện)
    public void CloseTextPanel()
    {
        if (textPanel != null) Destroy(textPanel);
        textPanel = null;
        textPanelFirstButton = null;
        CloseModal();
    }

    // nút "Màn tiếp theo" của bảng kết quả
    public void GoToLevel(int level)
    {
        IsPaused = false;
        IsModalOpen = false;
        Time.timeScale = 1f;
        if (!GameProgress.LoadLevel(level))
        {
            GoToLevelSelect(); // màn đó chưa có scene thì về bảng chọn màn
        }
    }

    private GameObject BuildTextPanel(string title, string body, PanelButton[] buttons)
    {
        GameObject panel = Instantiate(pausePanel, pausePanel.transform.parent);
        panel.name = "BangChu";

        Transform titleTransform = panel.transform.Find("TieuDe");
        if (titleTransform != null)
        {
            TMPro.TMP_Text titleText = titleTransform.GetComponent<TMPro.TMP_Text>();
            if (titleText != null) titleText.text = title;
            RectTransform titleRect = (RectTransform)titleTransform;
            titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, 160f);

            // đoạn chữ nhân bản từ tiêu đề rồi thu nhỏ chữ, nới khung cho nhiều dòng
            GameObject note = Instantiate(titleTransform.gameObject, panel.transform);
            note.name = "NoiDung";
            RectTransform noteRect = (RectTransform)note.transform;
            noteRect.sizeDelta = new Vector2(680f, 190f);
            noteRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, 42f);
            TMPro.TMP_Text noteText = note.GetComponent<TMPro.TMP_Text>();
            if (noteText != null)
            {
                noteText.fontSize = noteText.fontSize * 0.45f;
                noteText.enableAutoSizing = false;
                noteText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                noteText.alignment = TMPro.TextAlignmentOptions.Center;
                noteText.richText = true;
                noteText.text = body;
            }
        }

        // dùng lại các nút có sẵn của bảng tạm dừng theo thứ tự, nút thừa thì bỏ
        string[] buttonNames = { "Button_TiepTuc", "Button_ChonMan", "Button_VeMenu" };
        float[] buttonY = { -80f, -125f, -170f }; // trong khoảng ±170 để màn hình rộng 21:9 vẫn thấy đủ
        List<Button> created = new List<Button>();
        for (int i = 0; i < buttonNames.Length; i++)
        {
            if (i < buttons.Length)
            {
                Button b = SetupButton(panel, buttonNames[i], buttons[i].label, buttons[i].action, buttonY[i]);
                if (b != null) created.Add(b);
            }
            else
            {
                Transform unused = panel.transform.Find(buttonNames[i]);
                if (unused != null) Destroy(unused.gameObject);
            }
        }
        foreach (string unused in new[] { "Button_CaiDat", "Button_HuongDan" }) // nút Hướng dẫn phím cũng bị nhân bản theo
        {
            Transform extra = panel.transform.Find(unused);
            if (extra != null) Destroy(extra.gameObject);
        }

        LinkVertical(created.ToArray());
        textPanelFirstButton = created.Count > 0 ? created[0].gameObject : null;
        panel.SetActive(false);
        return panel;
    }

    // Hạ boss + nhặt Tấm bằng (màn cuối): bảng kết thúc game - chữ chúc mừng + ghi công, 2 nút Về menu / Chọn màn.
    // Cũng nhân bản từ bảng tạm dừng như bảng hết lượt hồi sinh.
    public void ShowEnding(string title, string body)
    {
        // màn cuối cũng tính GPA như các màn khác, ghi 1 dòng gọn lên đầu bảng kết thúc
        if (LevelStats.Instance != null)
        {
            bool newRecord;
            LevelStats.Instance.Finish(out newRecord);
            float cumulative = LevelStats.CumulativeGpa();
            string line = "GPA màn cuối: <color=#FFE066>" + LevelStats.Instance.CurrentGpa.ToString("0.00") + "</color>"
                          + (newRecord ? " (kỷ lục mới!)" : "")
                          + (cumulative >= 0f ? "   -   GPA tích lũy: " + cumulative.ToString("0.00") + " (" + LevelStats.Classify(cumulative) + ")" : "");
            body = line + "\n\n" + body;
        }

        // còn màn sau (phần 2) đã dựng thì nhắc người chơi vào chọn màn chơi tiếp
        int nextLevel = GameProgress.CurrentLevel + 1;
        if (GameProgress.CurrentLevel > 0 && GameProgress.IsLevelBuilt(nextLevel))
        {
            body += "\n<color=#FFE066>Phần tiếp theo đã mở: Màn " + nextLevel + " - vào CHỌN MÀN để chơi tiếp.</color>";
        }

        // ghi công tác giả ảnh nền các màn (giấy phép của vài bộ nền bắt buộc ghi tên)
        string backgroundCredits = LevelBackground.Credits();
        if (!string.IsNullOrEmpty(backgroundCredits)) body += "\nNền các màn: " + backgroundCredits;

        GameObject panel = BuildEndingPanel(title, body);
        if (panel == null)
        {
            GoToMainMenu(); // scene không có bảng tạm dừng để nhân bản thì về menu luôn
            return;
        }

        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        IsModalOpen = true;
        IsPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null) playerInput.DeactivateInput();

        gameOverPanel = panel; // dùng chung chỗ giữ bảng với bảng hết lượt: Update tự chọn lại nút khi bấm phím
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        MenuSelection.Select(gameOverFirstButton);
    }

    private GameObject BuildEndingPanel(string title, string body)
    {
        if (pausePanel == null) return null;

        GameObject panel = Instantiate(pausePanel, pausePanel.transform.parent);
        panel.name = "EndingPanel";

        Transform titleTransform = panel.transform.Find("TieuDe");
        if (titleTransform != null)
        {
            TMPro.TMP_Text titleText = titleTransform.GetComponent<TMPro.TMP_Text>();
            if (titleText != null) titleText.text = title;
            RectTransform titleRect = (RectTransform)titleTransform;
            titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, 165f);

            // đoạn chữ dài (cảm ơn + ghi công) nhân bản từ tiêu đề rồi thu nhỏ chữ, nới khung cho nhiều dòng
            GameObject note = Instantiate(titleTransform.gameObject, panel.transform);
            note.name = "NoiDung";
            RectTransform noteRect = (RectTransform)note.transform;
            noteRect.sizeDelta = new Vector2(700f, 230f);
            noteRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, 20f);
            TMPro.TMP_Text noteText = note.GetComponent<TMPro.TMP_Text>();
            if (noteText != null)
            {
                // chữ dài quá khung (thêm dòng ghi công) thì tự thu nhỏ, không tràn xuống nút
                noteText.fontSize = noteText.fontSize * 0.45f;
                noteText.fontSizeMax = noteText.fontSize;
                noteText.fontSizeMin = noteText.fontSize * 0.6f;
                noteText.enableAutoSizing = true;
                noteText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                noteText.alignment = TMPro.TextAlignmentOptions.Center;
                noteText.text = body;
            }
        }

        Button menu = SetupButton(panel, "Button_VeMenu", "VỀ MENU CHÍNH", GoToMainMenu, -130f);
        Button select = SetupButton(panel, "Button_ChonMan", "CHỌN MÀN", GoToLevelSelect, -180f);
        foreach (string unused in new[] { "Button_TiepTuc", "Button_CaiDat", "Button_HuongDan" })
        {
            Transform child = panel.transform.Find(unused);
            if (child != null) Destroy(child.gameObject);
        }
        LinkVertical(menu, select);

        gameOverFirstButton = menu != null ? menu.gameObject : null;
        panel.SetActive(false);
        return panel;
    }

    private GameObject BuildGameOverPanel(int respawnsUsed)
    {
        if (pausePanel == null) return null;

        GameObject panel = Instantiate(pausePanel, pausePanel.transform.parent);
        panel.name = "GameOverPanel";

        // tiêu đề + 1 dòng giải thích nhỏ ngay dưới (nhân bản tiêu đề rồi thu nhỏ chữ)
        Transform title = panel.transform.Find("TieuDe");
        if (title != null)
        {
            TMPro.TMP_Text titleText = title.GetComponent<TMPro.TMP_Text>();
            if (titleText != null) titleText.text = "HẾT LƯỢT HỒI SINH";

            GameObject note = Instantiate(title.gameObject, panel.transform);
            note.name = "GhiChu";
            RectTransform noteRect = (RectTransform)note.transform;
            noteRect.anchoredPosition = ((RectTransform)title).anchoredPosition + new Vector2(0f, -40f);
            TMPro.TMP_Text noteText = note.GetComponent<TMPro.TMP_Text>();
            if (noteText != null)
            {
                noteText.fontSize = noteText.fontSize * 0.5f;
                noteText.text = "Đã hồi sinh " + respawnsUsed + " lần. Kiến thức và nâng cấp vẫn được giữ.";
            }
        }

        // giữ 3 nút: Chơi lại màn / Chọn màn / Về menu chính; bỏ nút Cài đặt cho gọn
        Button retry = SetupButton(panel, "Button_TiepTuc", "CHƠI LẠI MÀN", RestartLevel, 20f);
        Button select = SetupButton(panel, "Button_ChonMan", "CHỌN MÀN", GoToLevelSelect, -35f);
        Button menu = SetupButton(panel, "Button_VeMenu", "VỀ MENU CHÍNH", GoToMainMenu, -90f);
        foreach (string unused in new[] { "Button_CaiDat", "Button_HuongDan" }) // nút Hướng dẫn phím cũng bị nhân bản theo
        {
            Transform extra = panel.transform.Find(unused);
            if (extra != null) Destroy(extra.gameObject);
        }

        // nút Cài đặt bị bỏ nên đặt lại đường đi phím mũi tên giữa 3 nút còn lại
        LinkVertical(retry, select, menu);

        gameOverFirstButton = retry != null ? retry.gameObject : null;
        panel.SetActive(false);
        return panel;
    }

    private static Button SetupButton(GameObject panel, string childName, string label, UnityEngine.Events.UnityAction action, float y)
    {
        Transform child = panel.transform.Find(childName);
        if (child == null) return null;

        Button button = child.GetComponent<Button>();
        if (button == null) return null;

        // thay cả sự kiện On Click (bỏ hàm cũ của bảng tạm dừng đã gắn trong Inspector) rồi gắn hàm mới
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);

        TMPro.TMP_Text text = child.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (text != null) text.text = label;

        RectTransform rect = (RectTransform)child;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        return button;
    }

    private static void LinkVertical(params Button[] buttons)
    {
        List<Button> list = new List<Button>();
        foreach (Button b in buttons) if (b != null) list.Add(b);

        for (int i = 0; i < list.Count; i++)
        {
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = list[(i - 1 + list.Count) % list.Count];
            nav.selectOnDown = list[(i + 1) % list.Count];
            list[i].navigation = nav;
        }
    }

    private void LeaveTo(string sceneName)
    {
        IsPaused = false;
        if (GameProgress.IsLevelScene(sceneName))
        {
            SceneLoader.LoadLevelScene(sceneName); // chơi lại màn: cũng qua màn hình chờ
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
    }
}
