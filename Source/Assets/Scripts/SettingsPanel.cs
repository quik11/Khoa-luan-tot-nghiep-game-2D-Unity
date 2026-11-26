using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bảng Cài đặt (dùng chung ở menu chính và menu tạm dừng): nút - / + chỉnh âm lượng, tự lưu lại.
// Thêm 3 dòng tạo bằng code (nhân bản dòng "Nhạc nền", khỏi sửa prefab/scene): toàn màn hình, độ phân giải, rung màn hình.
// Việc mở/đóng bảng do MainMenu hoặc PauseMenu lo.
public class SettingsPanel : MonoBehaviour
{
    [Header("Chữ âm lượng")]
    [TenHienThi("Âm lượng tổng")] public TMP_Text masterValueText;
    [TenHienThi("Nhạc nền")] public TMP_Text musicValueText;

    [Header("Bước chỉnh")]
    [TenHienThi("Bước")] public float step = 0.1f;

    [Header("Dòng tạo thêm")]
    [Tooltip("Khoảng cách dọc giữa 2 dòng (khung chuẩn 800x600)")]
    [TenHienThi("Khoảng cách dòng")] public float rowSpacing = 45f;
    [TenHienThi("Dòng đầu (y)")] public float firstRowY = 92f;

    private TMP_Text fullscreenValueText;
    private TMP_Text resolutionValueText;
    private TMP_Text shakeValueText;
    private bool extraRowsBuilt;

    private void OnEnable()
    {
        if (!extraRowsBuilt) BuildExtraRows();
        Refresh();
    }

    public void MasterUp()
    {
        AudioVolumeSettings.Master = Snap(AudioVolumeSettings.Master + step);
        Refresh();
    }

    public void MasterDown()
    {
        AudioVolumeSettings.Master = Snap(AudioVolumeSettings.Master - step);
        Refresh();
    }

    public void MusicUp()
    {
        AudioVolumeSettings.Music = Snap(AudioVolumeSettings.Music + step);
        Refresh();
    }

    public void MusicDown()
    {
        AudioVolumeSettings.Music = Snap(AudioVolumeSettings.Music - step);
        Refresh();
    }

    public void ToggleFullscreen()
    {
        DisplaySettings.Fullscreen = !DisplaySettings.Fullscreen;
        Refresh();
    }

    public void NextResolution()
    {
        DisplaySettings.ResolutionIndex = DisplaySettings.ResolutionIndex + 1;
        Refresh();
    }

    public void PreviousResolution()
    {
        DisplaySettings.ResolutionIndex = DisplaySettings.ResolutionIndex - 1;
        Refresh();
    }

    public void ToggleShake()
    {
        ScreenShake.Enabled = !ScreenShake.Enabled;
        Refresh();
    }

    // làm tròn để cộng trừ nhiều lần không bị lệch kiểu 0.30000004
    private float Snap(float value)
    {
        return Mathf.Clamp01(Mathf.Round(value * 100f) / 100f);
    }

    private void Refresh()
    {
        if (masterValueText != null) masterValueText.text = Percent(AudioVolumeSettings.Master);
        if (musicValueText != null) musicValueText.text = Percent(AudioVolumeSettings.Music);
        if (fullscreenValueText != null) fullscreenValueText.text = DisplaySettings.Fullscreen ? "Bật" : "Tắt";
        if (resolutionValueText != null) resolutionValueText.text = DisplaySettings.CurrentResolutionLabel;
        if (shakeValueText != null) shakeValueText.text = ScreenShake.Enabled ? "Bật" : "Tắt";
    }

    private string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    // ===== dựng thêm dòng bằng code =====

    private void BuildExtraRows()
    {
        extraRowsBuilt = true;

        Transform label = transform.Find("Nhan_music");
        Transform down = transform.Find("Button_Giam_music");
        Transform value = transform.Find("GiaTri_music");
        Transform up = transform.Find("Button_Tang_music");
        Transform close = transform.Find("Button_Dong");
        if (label == null || down == null || value == null || up == null) return; // bảng khác mẫu thì thôi, giữ như cũ

        // dời 2 dòng âm lượng cũ lên cho đủ chỗ
        MoveRow("master", firstRowY);
        MoveRow("music", firstRowY - rowSpacing);

        Row fullscreen = CloneRow(label, down, value, up, "toanmanhinh", "Toàn màn hình", firstRowY - rowSpacing * 2, ToggleFullscreen, ToggleFullscreen);
        Row resolution = CloneRow(label, down, value, up, "dophangiai", "Độ phân giải", firstRowY - rowSpacing * 3, PreviousResolution, NextResolution);
        Row shake = CloneRow(label, down, value, up, "rung", "Rung màn hình", firstRowY - rowSpacing * 4, ToggleShake, ToggleShake);
        fullscreenValueText = fullscreen.value;
        resolutionValueText = resolution.value;
        shakeValueText = shake.value;

        // "1920x1080" dài hơn "100%": nới ô giá trị, đẩy nút + sang phải chút
        if (resolution.value != null)
        {
            RectTransform valueRect = (RectTransform)resolution.value.transform;
            valueRect.sizeDelta = new Vector2(valueRect.sizeDelta.x + 40f, valueRect.sizeDelta.y);
            valueRect.anchoredPosition += new Vector2(20f, 0f);
            RectTransform upRect = (RectTransform)resolution.up.transform;
            upRect.anchoredPosition += new Vector2(40f, 0f);
        }

        if (close != null)
        {
            RectTransform closeRect = (RectTransform)close;
            closeRect.anchoredPosition = new Vector2(closeRect.anchoredPosition.x, firstRowY - rowSpacing * 5 - 10f);
        }

        // đường đi phím mũi tên: lên/xuống giữa các dòng, trái/phải giữa nút - và +
        List<Row> rows = new List<Row> { FindRow("master"), FindRow("music"), fullscreen, resolution, shake };
        rows.RemoveAll(r => r.down == null || r.up == null);
        Button closeButton = close != null ? close.GetComponent<Button>() : null;
        for (int i = 0; i < rows.Count; i++)
        {
            Selectable above = i > 0 ? rows[i - 1].down : (Selectable)closeButton;
            Selectable aboveUp = i > 0 ? rows[i - 1].up : (Selectable)closeButton;
            Selectable below = i < rows.Count - 1 ? rows[i + 1].down : (Selectable)closeButton;
            Selectable belowUp = i < rows.Count - 1 ? rows[i + 1].up : (Selectable)closeButton;
            SetNavigation(rows[i].down, above, below, rows[i].up, rows[i].up);
            SetNavigation(rows[i].up, aboveUp, belowUp, rows[i].down, rows[i].down);
        }
        if (closeButton != null && rows.Count > 0)
        {
            SetNavigation(closeButton, rows[rows.Count - 1].down, rows[0].down, null, null);
        }
    }

    private struct Row
    {
        public Button down;
        public Button up;
        public TMP_Text value;
    }

    private Row FindRow(string key)
    {
        Transform down = transform.Find("Button_Giam_" + key);
        Transform up = transform.Find("Button_Tang_" + key);
        return new Row
        {
            down = down != null ? down.GetComponent<Button>() : null,
            up = up != null ? up.GetComponent<Button>() : null
        };
    }

    private void MoveRow(string key, float y)
    {
        foreach (string prefix in new[] { "Nhan_", "Button_Giam_", "GiaTri_", "Button_Tang_" })
        {
            Transform t = transform.Find(prefix + key);
            if (t == null) continue;
            RectTransform rect = (RectTransform)t;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }
    }

    private Row CloneRow(Transform label, Transform down, Transform value, Transform up, string key, string labelText, float y,
                         UnityEngine.Events.UnityAction onDown, UnityEngine.Events.UnityAction onUp)
    {
        Row row = new Row();

        GameObject labelCopy = Clone(label, "Nhan_" + key, y);
        TMP_Text labelTmp = labelCopy.GetComponentInChildren<TMP_Text>(true);
        if (labelTmp != null) labelTmp.text = labelText;

        row.down = CloneButton(down, "Button_Giam_" + key, y, onDown);
        row.value = Clone(value, "GiaTri_" + key, y).GetComponentInChildren<TMP_Text>(true);
        row.up = CloneButton(up, "Button_Tang_" + key, y, onUp);
        return row;
    }

    private GameObject Clone(Transform source, string name, float y)
    {
        GameObject copy = Instantiate(source.gameObject, transform);
        copy.name = name;
        RectTransform rect = (RectTransform)copy.transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        return copy;
    }

    private Button CloneButton(Transform source, string name, float y, UnityEngine.Events.UnityAction action)
    {
        Button button = Clone(source, name, y).GetComponent<Button>();
        if (button == null) return null;
        button.onClick = new Button.ButtonClickedEvent(); // bỏ hàm chỉnh nhạc nền nhân bản theo, gắn hàm mới
        button.onClick.AddListener(action);
        return button;
    }

    private static void SetNavigation(Selectable target, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (target == null) return;
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        target.navigation = nav;
    }
}
