using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Cửa qua màn: hạ hết quái trong map thì cửa mở, đứng trong cửa bấm nút đánh thường (U) để qua màn.
// Qua màn -> mở khóa màn kế tiếp -> về menu chính, mở sẵn bảng chọn màn.
// Prefab: Assets/Objects/LevelDoor.prefab - thả vào màn chơi, đặt ở đâu cũng được (tự rơi xuống mặt đất).
[RequireComponent(typeof(Collider2D))]
public class LevelDoor : MonoBehaviour
{
    [Header("Hình cửa")]
    [TenHienThi("Hình cửa")] public SpriteRenderer doorRenderer;
    [TenHienThi("Màu đóng")] public Color closedColor = new Color(0.45f, 0.28f, 0.18f, 1f);
    [TenHienThi("Màu mở")] public Color openColor = new Color(0f, 1f, 0.86f, 1f);
    [TenHienThi("Hình đóng")] public Sprite closedSprite; // để trống thì giữ nguyên hình, chỉ đổi màu
    [TenHienThi("Hình mở")] public Sprite openSprite;

    [Header("Âm thanh")]
    [TenHienThi("Mở cửa")] public AudioClip openSound;
    [TenHienThi("Vào cửa")] public AudioClip enterSound;

    [TenHienThi("Chờ khi vào")] public float enterDelay = 0.4f; // giây chờ trước khi về menu cho tiếng vào cửa kịp phát

    [Header("Đặt cửa")]
    [TenHienThi("Tự đặt xuống đất")] public bool snapToGround = true; // lúc vào màn tự rơi xuống mặt đất bên dưới, khỏi canh tay cho khớp ô gạch
    [TenHienThi("Tìm đất trong")] public float snapMaxDistance = 30f;

    [Header("Chữ hướng dẫn")]
    [TenHienThi("Tên phím")] public string attackKeyName = "U";
    [TenHienThi("Đếm quái mỗi")] public float checkInterval = 0.25f; // bao lâu đếm lại số quái một lần

    private readonly List<Damageable> enemies = new List<Damageable>();
    private Collider2D doorCollider;
    private Collider2D playerCollider;
    private Damageable playerDamageable;
    private InputAction attackAction;
    private bool isOpen;
    private bool entering;
    private bool playerWasInside;
    private int aliveCount = -1;
    private float checkTimer;

    private void Awake()
    {
        doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true; // cửa chỉ để đi xuyên qua, không chặn đường
    }

    private void Start()
    {
        if (snapToGround) SnapToGround();

        // quái = mọi thứ có máu trừ người chơi (không phụ thuộc layer, quái mới thêm sau này cũng tự tính)
        foreach (Damageable damageable in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
        {
            if (damageable.GetComponent<PlayerController>() == null)
            {
                enemies.Add(damageable);
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerCollider = player.GetComponent<CapsuleCollider2D>();
            playerDamageable = player.GetComponent<Damageable>();
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();

            // nghe thẳng nút Attack của Player Input, khỏi phải nối thêm event trong Inspector
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                attackAction = playerInput.actions.FindAction("Attack");
            }
        }

        if (attackAction != null)
        {
            attackAction.started += OnAttackPressed;
        }
        else
        {
            Debug.LogWarning("LevelDoor: không tìm thấy nút Attack trên Player Input của Player - cửa sẽ không vào được.");
        }

        ApplyLook();
        CountEnemies();
    }

    private void OnDestroy()
    {
        if (attackAction != null)
        {
            attackAction.started -= OnAttackPressed;
        }
    }

    private void Update()
    {
        if (entering) return;

        if (!isOpen)
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = checkInterval;
                CountEnemies();
            }
            return;
        }

        // cửa mở rồi thì đổi chữ hướng dẫn khi người chơi bước vào/ra cửa
        bool inside = IsPlayerInside();
        if (inside != playerWasInside)
        {
            playerWasInside = inside;
            UpdateStatusText();
        }
    }

    private void CountEnemies()
    {
        int alive = 0;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null) { enemies.RemoveAt(i); continue; } // xác quái đã biến mất
            if (enemies[i].isAlive) alive++;
        }

        if (alive != aliveCount)
        {
            aliveCount = alive;
            if (alive == 0)
            {
                Open();
            }
            else
            {
                UpdateStatusText();
            }
        }
    }

    private void Open()
    {
        isOpen = true;
        ApplyLook();
        PlaySound(openSound);
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (GameHUD.Instance == null) return;

        if (!isOpen)
        {
            GameHUD.Instance.SetStatus("Quái còn lại: " + aliveCount + " - hạ hết để mở cửa");
        }
        else if (playerWasInside)
        {
            GameHUD.Instance.SetStatus("Bấm " + attackKeyName + " để qua màn");
        }
        else
        {
            GameHUD.Instance.SetStatus("Cửa đã mở! Tới cửa và bấm " + attackKeyName + " để qua màn");
        }
    }

    private void OnAttackPressed(InputAction.CallbackContext context)
    {
        if (!isOpen || entering || PauseMenu.IsPaused) return;
        if (playerDamageable != null && !playerDamageable.isAlive) return; // xác nằm trong cửa thì không qua màn được
        if (!IsPlayerInside()) return;

        Enter();
    }

    private void Enter()
    {
        entering = true;
        PlaySound(enterSound);

        GameProgress.CompleteLevel(GameProgress.CurrentLevel);
        GameProgress.openLevelSelectOnMenu = true;

        StartCoroutine(LoadMenuAfterSound());
    }

    // chờ một chút cho tiếng vào cửa kịp phát (chuyển scene ngay thì tiếng bị cắt mất)
    private IEnumerator LoadMenuAfterSound()
    {
        if (GameHUD.Instance != null) GameHUD.Instance.ShowMessage("Qua màn!");
        yield return new WaitForSecondsRealtime(enterDelay);

        // bảng kết quả (GPA) - nút nào cũng tự chuyển scene; scene không có bảng tạm dừng thì về menu như cũ
        if (ShowResultPanel()) yield break;

        Time.timeScale = 1f;
        SceneManager.LoadScene(GameProgress.mainMenuScene);
    }

    private bool ShowResultPanel()
    {
        PauseMenu menu = PauseMenu.Instance;
        if (menu == null) return false;

        int level = GameProgress.CurrentLevel;
        bool newRecord = false;
        string body = LevelStats.Instance != null ? LevelStats.Instance.Finish(out newRecord) : "";

        int next = level + 1;
        bool hasNext = next <= GameProgress.LevelCount && GameProgress.IsLevelBuilt(next);

        if (hasNext)
        {
            return menu.ShowTextPanel("QUA MÀN " + level + "!", body,
                new PauseMenu.PanelButton("MÀN TIẾP THEO", () => menu.GoToLevel(next)),
                new PauseMenu.PanelButton("CHỌN MÀN", menu.GoToLevelSelect),
                new PauseMenu.PanelButton("VỀ MENU CHÍNH", menu.GoToMainMenu));
        }

        return menu.ShowTextPanel("QUA MÀN " + level + "!", body,
            new PauseMenu.PanelButton("CHỌN MÀN", menu.GoToLevelSelect),
            new PauseMenu.PanelButton("VỀ MENU CHÍNH", menu.GoToMainMenu));
    }

    // so khung va chạm của người chơi với khung cửa (đơn giản, không phụ thuộc ma trận va chạm giữa các layer)
    private bool IsPlayerInside()
    {
        return playerCollider != null && doorCollider.bounds.Intersects(playerCollider.bounds);
    }

    private void ApplyLook()
    {
        if (doorRenderer == null) return;

        doorRenderer.color = isOpen ? openColor : closedColor;

        Sprite sprite = isOpen ? openSprite : closedSprite;
        if (sprite != null) doorRenderer.sprite = sprite;
    }

    // bắn tia xuống layer Ground, đặt đáy cửa lên đúng mặt đất
    private void SnapToGround()
    {
        int groundMask = LayerMask.GetMask("Ground");
        if (groundMask == 0) return;

        float halfHeight = doorCollider.bounds.extents.y;
        // bắt đầu tia từ giữa cửa: lỡ đặt đáy cửa hơi lọt xuống đất thì tia vẫn chạm mặt trên của đất, không xuyên xuống mặt dưới
        Vector2 start = new Vector2(transform.position.x, doorCollider.bounds.center.y);
        RaycastHit2D hit = Physics2D.Raycast(start, Vector2.down, snapMaxDistance, groundMask);
        if (hit.collider == null) return;

        float centerOffset = transform.position.y - doorCollider.bounds.center.y; // phòng khi pivot không nằm giữa
        transform.position = new Vector3(transform.position.x, hit.point.y + halfHeight + centerOffset, transform.position.z);
    }

    // phát ngay tại camera cho nghe rõ (phát ở chỗ cửa thì xa tai nghe nên gần như không nghe thấy)
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
    }
}
