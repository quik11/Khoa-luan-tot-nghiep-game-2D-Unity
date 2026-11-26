using System.Collections;
using UnityEngine;

// "Tấm bằng Cử nhân xuất sắc" rơi ra khi hạ boss màn cuối. Nhặt -> mở khóa màn kế (Màn 6 hiện "sắp có") -> bảng kết thúc game.
// BossController tạo bằng code (Create), không cần prefab.
public class DiplomaPickUp : MonoBehaviour
{
    [TenHienThi("Tốc rơi")] public float fallSpeed = 3f;
    [TenHienThi("Nhấp nhô nhanh")] public float floatSpeed = 2f;
    [TenHienThi("Nhấp nhô cao")] public float floatHeight = 0.15f;
    [TenHienThi("Âm thanh nhặt")] public AudioClip pickUpSound;
    [TenHienThi("Tiêu đề kết thúc")] public string endingTitle;
    [TenHienThi("Lời kết thúc")] public string endingText;

    private float restY;
    private bool landed;
    private float landTime;
    private bool taken;

    public static DiplomaPickUp Create(Vector2 startPosition, float restY, Sprite sprite, Material material, float scale)
    {
        GameObject root = new GameObject("TamBang");
        root.layer = LayerMask.NameToLayer("PickUp"); // layer vật phẩm: chỉ chạm người chơi
        root.transform.position = startPosition;
        root.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Player";
        renderer.sortingOrder = 20;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        if (sprite != null) box.size = sprite.bounds.size;

        DiplomaPickUp diploma = root.AddComponent<DiplomaPickUp>();
        diploma.restY = restY;
        return diploma;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        if (!landed)
        {
            // từ từ hạ xuống chỗ nghỉ rồi nhấp nhô như mọi vật phẩm khác
            position.y = Mathf.MoveTowards(position.y, restY, fallSpeed * Time.deltaTime);
            if (Mathf.Approximately(position.y, restY))
            {
                landed = true;
                landTime = Time.time;
            }
        }
        else
        {
            position.y = restY + Mathf.Sin((Time.time - landTime) * floatSpeed) * floatHeight;
        }
        transform.position = position;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryPickUp(collision);
    }

    private void TryPickUp(Collider2D collision)
    {
        if (taken) return;

        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null || !player.isAlive) return; // xác không nhặt được

        taken = true;
        BossController.PlaySound(pickUpSound);
        GameProgress.CompleteLevel(GameProgress.CurrentLevel);
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.SetStatus("");
            GameHUD.Instance.ShowMessage("Đã nhận Tấm bằng Cử nhân xuất sắc!");
        }
        StartCoroutine(ShowEndingSoon());
    }

    // chờ chút cho tiếng nhặt kịp phát và người chơi kịp thấy dòng chữ rồi mới hiện bảng kết thúc
    private IEnumerator ShowEndingSoon()
    {
        GetComponent<SpriteRenderer>().enabled = false;
        yield return new WaitForSecondsRealtime(1.2f);

        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ShowEnding(endingTitle, endingText);
        }
        else
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameProgress.mainMenuScene);
        }
    }
}
