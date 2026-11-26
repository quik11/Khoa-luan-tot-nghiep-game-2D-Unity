using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Tạo lại khung va chạm của đất mỗi lần vào màn (không cần gắn vào object nào, tự chạy).
// Vì sao cần: đất dùng TilemapCollider2D gộp vào CompositeCollider2D, mà CompositeCollider2D lưu sẵn hình va chạm
// trong file scene. Màn dựng bằng công cụ (sao chép Màn 1 rồi vẽ lại ô gạch) từng bị lưu nhầm hình va chạm cũ của Màn 1
// -> ô gạch vẫn hiện nhưng nhân vật và quái rơi xuyên qua. Tạo lại lúc vào màn thì va chạm luôn khớp với ô gạch đang có.
public static class TilemapColliderRefresh
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // gỡ trước rồi mới gắn, để tắt tải lại code giữa các lần Play cũng không bị gọi 2 lần
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // chạy sau Awake/OnEnable của scene mới nhưng trước bước vật lý đầu tiên -> nhân vật chưa kịp rơi
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshScene(scene);
    }

    public static void RefreshScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TilemapCollider2D tilemapCollider in root.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                Refresh(tilemapCollider);
            }
        }
    }

    // dùng chung cho cả lúc chơi lẫn công cụ dựng màn trong Editor
    public static void Refresh(TilemapCollider2D tilemapCollider)
    {
        // collider đang tắt sẵn (vd lớp "wall" của Màn 1) thì bỏ qua, không được bật nó lên
        if (tilemapCollider == null || !tilemapCollider.enabled || !tilemapCollider.gameObject.activeInHierarchy) return;

        // ô gạch vừa đổi thường chờ tới cuối khung hình mới được xử lý -> ép xử lý ngay (không có gì chờ thì thôi)
        tilemapCollider.ProcessTilemapChanges();

        // tắt rồi bật lại: bỏ hình va chạm cũ (kể cả hình lưu sẵn trong file scene), dựng lại từ ô gạch đang có
        tilemapCollider.enabled = false;
        tilemapCollider.enabled = true;

        CompositeCollider2D composite = tilemapCollider.composite;
        if (composite != null) composite.GenerateGeometry();
    }
}
