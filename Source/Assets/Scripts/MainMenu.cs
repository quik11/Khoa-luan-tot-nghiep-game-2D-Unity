using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dùng chung cho MainMenuScene và QuitScene - các nút gọi hàm của script này qua ô On Click () trong Inspector
public class MainMenu : MonoBehaviour
{
    [Header("Tên scene")]
    [TenHienThi("Màn 1")] public string gameplaySceneName = "GameplayScene";
    [TenHienThi("Menu chính")] public string mainMenuSceneName = "MainMenuScene";
    [TenHienThi("Màn thoát")] public string quitSceneName = "QuitScene";

    [Header("Bảng hướng dẫn")]
    [TenHienThi("Bảng")] public GameObject controlsPanel;
    [TenHienThi("Nút đóng")] public GameObject controlsCloseButton; // mở bảng thì chọn sẵn nút này, bấm Enter là đóng luôn

    [Header("Nút chọn sẵn")]
    [TenHienThi("Nút")] public GameObject firstSelectedButton; // để chơi menu bằng bàn phím: mũi tên chọn nút, Enter để bấm

    [Header("Bảng chọn màn")]
    [TenHienThi("Bảng")] public GameObject levelSelectPanel;
    [TenHienThi("Nút các màn")] public Button[] levelButtons; // nút Màn 1, Màn 2... theo thứ tự
    [TenHienThi("Nút quay lại")] public GameObject levelSelectBackButton;
    [TenHienThi("Chữ Chơi mới")] public TMP_Text newGameText; // chữ trên nút "Chơi mới", đổi thành câu hỏi xác nhận khi bấm lần 1

    [Header("Bảng cài đặt")]
    [TenHienThi("Bảng")] public GameObject settingsPanel;
    [TenHienThi("Nút chọn sẵn")] public GameObject settingsFirstButton;

    private bool confirmingNewGame = false;
    private string newGameDefaultText;

    private void Start()
    {
        // lỡ thoát ra đúng lúc đang khựng hình khi trúng đòn (timeScale = 0) thì trả lại tốc độ bình thường
        Time.timeScale = 1f;

        if (newGameText != null) newGameDefaultText = newGameText.text;

        HidePanel(controlsPanel);
        HidePanel(levelSelectPanel);
        HidePanel(settingsPanel);

        // vừa qua cửa xong (hoặc bấm "Chọn màn" trong menu tạm dừng) thì mở thẳng bảng chọn màn
        if (GameProgress.openLevelSelectOnMenu && levelSelectPanel != null)
        {
            GameProgress.openLevelSelectOnMenu = false;
            ShowLevelSelect();
        }
        else
        {
            MenuSelection.Select(firstSelectedButton);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Esc đóng bảng đang mở (cài đặt / chọn màn / hướng dẫn)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsOpen(settingsPanel)) { HideSettings(); return; }
            if (IsOpen(levelSelectPanel)) { HideLevelSelect(); return; }
            if (IsOpen(controlsPanel)) { HideControls(); return; }
        }

        MenuSelection.ReselectOnKeyPress(CurrentFallbackButton());
    }

    // nút nên được chọn lại khi lỡ mất chọn, tùy bảng nào đang mở
    private GameObject CurrentFallbackButton()
    {
        if (IsOpen(settingsPanel)) return settingsFirstButton;
        if (IsOpen(levelSelectPanel)) return BestLevelButton();
        if (IsOpen(controlsPanel)) return controlsCloseButton;
        return firstSelectedButton;
    }

    // Vào thẳng Màn 1 (giữ lại cho nút cũ, nút "Chơi" giờ mở bảng chọn màn)
    public void Play()
    {
        LoadScene(gameplaySceneName);
    }

    // Nút "Về Menu chính" ở QuitScene
    public void BackToMainMenu()
    {
        LoadScene(mainMenuSceneName);
    }

    // Nút "Thoát" ở menu chính - sang màn hình xác nhận thoát thay vì tắt game ngay
    public void OpenQuitScene()
    {
        LoadScene(quitSceneName);
    }

    // Nút "Thoát game" ở QuitScene - tắt game thật
    public void Quit()
    {
#if UNITY_EDITOR
        // trong Editor thì Application.Quit không có tác dụng, nên tắt Play mode thay thế
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Nút "Hướng dẫn"
    public void ShowControls()
    {
        if (controlsPanel == null) return;

        controlsPanel.SetActive(true);
        MenuSelection.Select(controlsCloseButton);
    }

    // Nút "Đóng" trong bảng hướng dẫn
    public void HideControls()
    {
        HidePanel(controlsPanel);
        MenuSelection.Select(firstSelectedButton);
    }

    // Nút "Chơi": mở bảng chọn màn
    public void ShowLevelSelect()
    {
        if (levelSelectPanel == null)
        {
            Play();
            return;
        }

        ResetNewGameConfirm();
        levelSelectPanel.SetActive(true); // bật bảng trước rồi mới đổi chữ trên nút (chữ trong object đang tắt không tìm thấy được)
        RefreshLevelButtons();
        MenuSelection.Select(BestLevelButton());
    }

    // Nút "Quay lại" trong bảng chọn màn
    public void HideLevelSelect()
    {
        ResetNewGameConfirm();
        HidePanel(levelSelectPanel);
        MenuSelection.Select(firstSelectedButton);
    }

    // Nút "Màn 1", "Màn 2"... (số màn điền ở ô On Click () của từng nút)
    public void PlayLevel(int level)
    {
        if (!GameProgress.IsUnlocked(level)) return;
        GameProgress.LoadLevel(level);
    }

    // Nút "Chơi mới": bấm lần 1 hỏi lại, bấm lần 2 mới xóa tiến độ (tránh lỡ tay)
    public void NewGame()
    {
        if (!confirmingNewGame)
        {
            confirmingNewGame = true;
            if (newGameText != null) newGameText.text = "BẤM LẠI ĐỂ XÓA"; // chữ ngắn cho vừa nút
            return;
        }

        GameProgress.ResetProgress();
        ResetNewGameConfirm();
        RefreshLevelButtons();
        MenuSelection.Select(BestLevelButton());
    }

    // Nút "Cài đặt"
    public void ShowSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(true);
        MenuSelection.Select(settingsFirstButton);
    }

    // Nút "Đóng" trong bảng cài đặt
    public void HideSettings()
    {
        HidePanel(settingsPanel);
        MenuSelection.Select(firstSelectedButton);
    }

    // khóa/mở từng nút màn theo tiến độ đã lưu
    private void RefreshLevelButtons()
    {
        if (levelButtons == null) return;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button button = levelButtons[i];
            if (button == null) continue;

            int level = i + 1;
            bool built = GameProgress.IsLevelBuilt(level);
            bool unlocked = GameProgress.IsUnlocked(level);
            button.interactable = built && unlocked;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string note = !built ? "\n<size=70%>(sắp có)</size>" : !unlocked ? "\n<size=70%>(khóa)</size>" : "";
            float best = LevelStats.BestGpa(level);
            if (built && unlocked && best >= 0f) note = "\n<size=70%>GPA " + best.ToString("0.00") + "</size>";
            label.text = "MÀN " + level + note;
        }

        ShowCumulativeGpa();

        RebuildLevelNavigation();
    }

    // dòng GPA tích lũy (trung bình GPA cao nhất các màn đã qua): nhân bản dòng ghi chú cuối bảng chọn màn,
    // đặt vào khoảng trống giữa tiêu đề và hàng nút Màn 1-3
    private TMP_Text cumulativeGpaText;

    private void ShowCumulativeGpa()
    {
        if (levelSelectPanel == null) return;

        if (cumulativeGpaText == null)
        {
            Transform noteTransform = levelSelectPanel.transform.Find("GhiChu");
            if (noteTransform == null) return;
            GameObject copy = Instantiate(noteTransform.gameObject, levelSelectPanel.transform);
            copy.name = "GpaTichLuy";
            RectTransform rect = (RectTransform)copy.transform;
            rect.anchoredPosition = new Vector2(0f, 100f);
            rect.sizeDelta = new Vector2(720f, 26f);
            cumulativeGpaText = copy.GetComponent<TMP_Text>();
            if (cumulativeGpaText == null) return;
            cumulativeGpaText.color = new Color(1f, 0.88f, 0.4f, 1f);
            cumulativeGpaText.alignment = TextAlignmentOptions.Center;
        }

        float cumulative = LevelStats.CumulativeGpa();
        cumulativeGpaText.text = cumulative < 0f ? "" : "GPA tích lũy: " + cumulative.ToString("0.00") + " - " + LevelStats.Classify(cumulative);
    }

    // mũi tên chỉ đi qua các màn chơi được + 2 nút dưới (đi vào nút màn bị khóa thì không thấy sáng, tưởng mất chọn)
    private void RebuildLevelNavigation()
    {
        List<Button> open = new List<Button>();
        foreach (Button button in levelButtons)
        {
            if (button != null && button.interactable) open.Add(button);
        }

        Button newGameButton = newGameText != null ? newGameText.GetComponentInParent<Button>(true) : null;
        Button backButton = levelSelectBackButton != null ? levelSelectBackButton.GetComponent<Button>() : null;
        Button lastOpen = open.Count > 0 ? open[open.Count - 1] : null;

        for (int i = 0; i < open.Count; i++)
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = i > 0 ? open[i - 1] : null;
            navigation.selectOnRight = i + 1 < open.Count ? open[i + 1] : null;
            navigation.selectOnDown = newGameButton != null ? newGameButton : backButton;
            open[i].navigation = navigation;
        }

        if (newGameButton != null)
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = lastOpen;
            navigation.selectOnRight = backButton;
            newGameButton.navigation = navigation;
        }

        if (backButton != null)
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = lastOpen;
            navigation.selectOnLeft = newGameButton;
            backButton.navigation = navigation;
        }
    }

    // chọn sẵn màn cao nhất đã mở (vừa qua màn thì màn kế tiếp được chọn sẵn)
    private GameObject BestLevelButton()
    {
        if (levelButtons != null)
        {
            for (int i = levelButtons.Length - 1; i >= 0; i--)
            {
                if (levelButtons[i] != null && levelButtons[i].interactable) return levelButtons[i].gameObject;
            }
        }
        return levelSelectBackButton;
    }

    private void ResetNewGameConfirm()
    {
        confirmingNewGame = false;
        if (newGameText != null && newGameDefaultText != null) newGameText.text = newGameDefaultText;
    }

    private bool IsOpen(GameObject panel)
    {
        return panel != null && panel.activeSelf;
    }

    private void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }

    private void LoadScene(string sceneName)
    {
        // scene chưa thêm vào Build Profiles thì báo rõ ràng thay vì để Unity báo lỗi khó hiểu
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("MainMenu: không tìm thấy scene '" + sceneName + "'. Vào File > Build Profiles > Scene List, kéo scene này vào danh sách rồi thử lại.");
            return;
        }

        if (GameProgress.IsLevelScene(sceneName))
        {
            SceneLoader.LoadLevelScene(sceneName); // vào màn chơi: qua màn hình chờ
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
