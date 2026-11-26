using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Màn hình chờ khi vào 1 màn chơi: nền đen, tên màn (tiêu đề trong Resources/CotTruyen.txt) + 1 mẹo chơi ngẫu nhiên
// (Resources/MeoChoi.txt, mỗi dòng 1 mẹo). Nạp scene ở chế độ chạy nền, hiện ít nhất minShowTime giây rồi mờ dần.
// Gọi SceneLoader.LoadLevelScene(tên scene) thay cho SceneManager.LoadScene khi vào màn chơi.
public class SceneLoader : MonoBehaviour
{
    private const float minShowTime = 1.6f;  // đủ lâu để đọc tên màn + mẹo
    private const float fadeTime = 0.35f;

    private static SceneLoader current;

    private CanvasGroup group;

    public static bool IsLoading { get { return current != null; } }

    public static void LoadLevelScene(string sceneName)
    {
        if (current != null) return; // đang chuyển rồi thì bỏ qua bấm thêm

        // đóng băng scene cũ trong lúc chờ (đang ở bảng kết quả / tạm dừng thì quái không chạy tiếp sau màn đen);
        // nạp scene chạy nền vẫn chạy bình thường khi timeScale = 0
        Time.timeScale = 0f;
        GameObject root = new GameObject("ManHinhCho");
        DontDestroyOnLoad(root);
        current = root.AddComponent<SceneLoader>();
        current.StartCoroutine(current.Run(sceneName));
    }

    private IEnumerator Run(string sceneName)
    {
        int level = LevelOf(sceneName);
        BuildOverlay(level > 0 ? LevelTexts.LevelTitle(level) : "", RandomTip());

        float shownAt = Time.unscaledTime;
        // hiện chữ lên trước khi nạp (nạp scene nặng có thể làm khựng vài khung hình)
        yield return null;

        AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
        if (loading == null)
        {
            Time.timeScale = 1f;
            Destroy(gameObject);
            yield break;
        }
        loading.allowSceneActivation = false;

        // Unity dừng tiến độ ở 0.9 khi chưa cho kích hoạt scene
        while (loading.progress < 0.9f || Time.unscaledTime - shownAt < minShowTime)
        {
            yield return null;
        }
        loading.allowSceneActivation = true;
        while (!loading.isDone) yield return null;
        if (!PauseMenu.IsPaused) Time.timeScale = 1f; // bảng cốt truyện đã kịp mở (đang dừng game) thì để nguyên

        // scene mới đã vào: mờ dần màn hình chờ (bảng cốt truyện nếu có sẽ hiện ra bên dưới)
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - t / fadeTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private static int LevelOf(string sceneName)
    {
        for (int i = 0; i < GameProgress.levelScenes.Length; i++)
        {
            if (GameProgress.levelScenes[i] == sceneName) return i + 1;
        }
        return 0;
    }

    private static string RandomTip()
    {
        TextAsset asset = Resources.Load<TextAsset>("MeoChoi");
        if (asset == null) return "";

        System.Collections.Generic.List<string> tips = new System.Collections.Generic.List<string>();
        foreach (string raw in asset.text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith("//")) tips.Add(line);
        }
        return tips.Count == 0 ? "" : "Mẹo: " + tips[Random.Range(0, tips.Count)];
    }

    private void BuildOverlay(string title, string tip)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // trên mọi giao diện khác
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f); // cùng khung chuẩn với các menu
        scaler.matchWidthOrHeight = 0f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;

        GameObject background = new GameObject("Nen", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Color.black;

        CreateText("TenMan", title, 34f, FontStyles.Bold, new Color(1f, 0.92f, 0.6f, 1f), new Vector2(0f, 30f), new Vector2(700f, 60f));
        CreateText("Meo", tip, 18f, FontStyles.Normal, new Color(0.85f, 0.87f, 0.92f, 1f), new Vector2(0f, -40f), new Vector2(640f, 80f));
        CreateText("DangTai", "Đang tải...", 14f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.65f, 1f), new Vector2(0f, -130f), new Vector2(300f, 30f));
    }

    private void CreateText(string name, string text, float size, FontStyles style, Color color, Vector2 position, Vector2 boxSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        // font pixel VT323 nếu đã tạo, không thì font mặc định của TextMesh Pro (LiberationSans SDF, cũng có dấu)
        GameFont.Setup(tmp, TMP_Settings.defaultFontAsset, size);
        tmp.text = text;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
    }
}
