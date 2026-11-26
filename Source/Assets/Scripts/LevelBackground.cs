using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Đổi nền parallax theo từng màn mà không sửa scene: lúc nạp scene, đọc Resources/NenManChoi/DanhSach.txt xem scene này
// dùng bộ nền nào (BackgroundTheme cùng thư mục), ẩn nền cũ (object gốc "Background") rồi dựng từng lớp của bộ nền.
// Màn chơi: lớp nền trôi theo camera (ParallaxEffect). Menu (nền cũ dùng MenuParallax): lớp nền trôi nhẹ theo chuột.
// Scene không có trong danh sách thì giữ nguyên nền cũ. Không cần gắn vào đâu.
public static class LevelBackground
{
    private const string resourceFolder = "NenManChoi";
    private const string listFile = "NenManChoi/DanhSach";
    private const string creditKey = "ghicong";
    private const string oldBackgroundName = "Background";
    private const string newBackgroundName = "NenTheoMan";
    private const int firstSortingOrder = -100; // dưới mọi thứ khác cùng lớp vẽ

    // menu: lớp gần nhất trôi theo chuột tối đa bấy nhiêu ô (như nền menu cũ 0.3 / 0.6 / 1.0), lớp xa nhất ít nhất bấy nhiêu
    private const float menuNearestOffset = 1f;
    private const float menuFarthestOffset = 0.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded chạy sau Awake, trước Start -> các lớp mới kịp chạy Start cùng lượt với mọi thứ khác trong màn
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying) return;

        // chỉ đọc file chữ nhỏ; scene không có trong danh sách (vd QuitScene) thì không nạp gì
        BackgroundTheme theme = FindTheme(scene.name);
        if (theme == null || theme.layers == null || theme.layers.Length == 0) return;

        Camera camera = FindSceneCamera(scene);
        if (camera == null || !camera.orthographic) return;

        Build(scene, theme, camera);
    }

    // chỉ nạp đúng bộ nền của màn này (nạp bộ nền là nạp luôn ảnh của nó, bộ rừng khá nặng)
    public static BackgroundTheme FindTheme(string sceneName)
    {
        foreach (string[] entry in ReadList())
        {
            if (entry[0] == sceneName) return Resources.Load<BackgroundTheme>(resourceFolder + "/" + entry[1]);
        }
        return null;
    }

    // ghi công tác giả các bộ nền, cho bảng kết thúc game (đọc từ file chữ, không phải nạp ảnh)
    public static string Credits()
    {
        List<string> credits = new List<string>();
        foreach (string[] entry in ReadList())
        {
            if (entry[0].ToLower() == creditKey && !credits.Contains(entry[1])) credits.Add(entry[1]);
        }
        return string.Join(", ", credits);
    }

    // mỗi dòng "tên scene | tên bộ nền" hoặc "ghicong | lời ghi công"; dòng trống và dòng bắt đầu bằng // bỏ qua
    private static List<string[]> ReadList()
    {
        List<string[]> entries = new List<string[]>();
        TextAsset asset = Resources.Load<TextAsset>(listFile);
        if (asset == null) return entries;

        foreach (string raw in asset.text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            string key = line.Substring(0, bar).Trim();
            string value = line.Substring(bar + 1).Trim();
            if (key.Length > 0 && value.Length > 0) entries.Add(new[] { key, value });
        }
        return entries;
    }

    // lấy camera ngay trong màn vừa nạp: lúc nạp màn kiểu chạy nền (màn hình chờ), Camera.main có thể còn là camera scene cũ
    private static Camera FindSceneCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>())
            {
                if (camera.isActiveAndEnabled && camera.CompareTag("MainCamera")) return camera;
            }
        }
        return Camera.main;
    }

    private static void Build(Scene scene, BackgroundTheme theme, Camera camera)
    {
        // lấy lớp vẽ + material của nền cũ để nền mới vẽ y cùng chỗ, cùng kiểu ánh sáng 2D, rồi ẩn nền cũ đi
        int sortingLayerId = SortingLayer.NameToID("BACKGROUND");
        Material material = null;
        bool menuStyle = false; // nền cũ trôi theo chuột (menu chính) -> nền mới cũng trôi theo chuột
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != oldBackgroundName) continue;
            SpriteRenderer oldRenderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (oldRenderer != null)
            {
                sortingLayerId = oldRenderer.sortingLayerID;
                material = oldRenderer.sharedMaterial;
            }
            if (root.GetComponentInChildren<MenuParallax>(true) != null) menuStyle = true;
            root.SetActive(false);
        }

        // lớp gần nhất (đi theo camera ít nhất) là mốc để chia độ trôi theo chuột ở menu
        float nearestFollow = 1f;
        foreach (BackgroundTheme.Layer layer in theme.layers)
        {
            if (layer != null && layer.sprite != null) nearestFollow = Mathf.Min(nearestFollow, layer.followX);
        }

        GameObject parent = new GameObject(newBackgroundName);
        SceneManager.MoveGameObjectToScene(parent, scene);

        float scale = theme.scale > 0f ? theme.scale : 1f;
        Vector3 cameraPosition = camera.transform.position;
        float bottom = cameraPosition.y - camera.orthographicSize - theme.bottomBelowView;

        for (int i = 0; i < theme.layers.Length; i++)
        {
            BackgroundTheme.Layer layer = theme.layers[i];
            if (layer == null || layer.sprite == null) continue;

            GameObject layerObject = new GameObject("Lop" + i + "_" + layer.sprite.name);
            layerObject.transform.SetParent(parent.transform, false);
            layerObject.transform.position = new Vector3(cameraPosition.x + layer.xOffset * scale,
                                                         bottom + layer.yOffset * scale, 0f);
            layerObject.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = layer.sprite;
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = firstSortingOrder + i;
            if (layer.tiled)
            {
                // ảnh lặp ngang đủ bề rộng bộ nền; số ô lẻ để giữa khung hình luôn là 1 ô nguyên
                Vector2 tileSize = layer.sprite.bounds.size;
                int tiles = Mathf.Max(1, Mathf.CeilToInt(theme.width / (tileSize.x * scale)));
                if (tiles % 2 == 0) tiles++;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(tiles * tileSize.x, tileSize.y);
            }

            if (menuStyle)
            {
                // càng gần (followX nhỏ) trôi theo chuột càng nhiều; đặt trước Start của MenuParallax
                float depth = nearestFollow < 1f ? (1f - layer.followX) / (1f - nearestFollow) : 1f;
                MenuParallax menuParallax = layerObject.AddComponent<MenuParallax>();
                menuParallax.offsetMultiplier = Mathf.Lerp(menuFarthestOffset, menuNearestOffset, depth);
                menuParallax.autoScrollSpeed = layer.autoScrollSpeed;
                continue;
            }

            // thông số đặt trước Start của ParallaxEffect (Start chạy ở khung hình đầu)
            ParallaxEffect parallax = layerObject.AddComponent<ParallaxEffect>();
            parallax.cameraOverride = camera;
            parallax.followX = layer.followX;
            parallax.followY = layer.followY;
            parallax.autoScrollSpeed = layer.autoScrollSpeed;
            parallax.edgeClamp = layer.edgeClamp;
        }

        camera.backgroundColor = theme.cameraColor;
    }
}
