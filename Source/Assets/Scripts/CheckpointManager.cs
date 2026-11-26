using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Hố chết + điểm lưu (checkpoint).
// - Rơi khỏi map (thấp hơn đáy Tilemap đất) hoặc rơi vào vùng chết (KillZone) là chết ngay, cả người chơi lẫn quái.
// - Người chơi chết (rơi vực hay hết máu đều vậy) thì chờ 1 lúc, màn tối lại, hồi sinh ở điểm lưu chạm gần nhất
//   (chưa chạm cái nào thì về chỗ xuất phát), đầy máu, đầy tơ. Quái đã hạ vẫn chết, vật phẩm đã nhặt không hiện lại.
// - Mỗi lần vào màn chỉ được hồi sinh maxRespawns lần (mặc định 3). Hết lượt mà chết nữa là thua:
//   hiện bảng "Hết lượt hồi sinh" (Chơi lại màn / Chọn màn / Về menu), chơi lại thì màn bắt đầu lại từ đầu.
// Nằm trong prefab Assets/UI/GameHUD.prefab cùng GameHUD/PauseMenu/UpgradeManager nên màn nào có GameHUD là có.
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    // thấp hơn mốc này là chết ngay - Damageable của mọi nhân vật tự so mỗi khung hình.
    // Mặc định âm vô cực = không có mốc (menu chính, scene không có GameHUD) nên không ảnh hưởng gì.
    public static float FallKillY { get; private set; } = float.NegativeInfinity;

    [Header("Rơi khỏi bản đồ")]
    [Tooltip("Rơi thấp hơn hàng đất thấp nhất của màn bao nhiêu ô thì chết")]
    [TenHienThi("Sâu hơn đáy")] public float fallDeathDepth = 3f;

    [Header("Hồi sinh")]
    [Tooltip("Giây chờ sau khi chết (để kịp thấy animation chết) rồi mới hồi sinh")]
    [TenHienThi("Chờ trước khi hồi sinh")] public float respawnDelay = 1f;
    [Tooltip("Giây màn hình tối dần / sáng dần lúc đưa nhân vật về điểm lưu")]
    [TenHienThi("Thời gian tối màn")] public float fadeDuration = 0.25f;
    [Tooltip("Giây bất tử (nhấp nháy) sau khi hồi sinh")]
    [TenHienThi("Bất tử sau hồi sinh")] public float respawnInvincibility = 1.5f;
    [Tooltip("Hồi sinh là đầy tơ - tránh kẹt ở hố bắt buộc dùng tơ kéo")]
    [TenHienThi("Nạp đầy tơ")] public bool refillAmmoOnRespawn = true;

    [Header("Lượt hồi sinh")]
    [Tooltip("Mỗi lần vào màn được hồi sinh bao nhiêu lần. Hết lượt mà chết nữa là thua, phải chơi lại màn từ đầu. Đặt -1 = không giới hạn")]
    [TenHienThi("Số lượt (-1 = vô hạn)")] public int maxRespawns = 3;

    [Header("Âm thanh")]
    [TenHienThi("Âm thanh hồi sinh")] public AudioClip respawnSound;

    private PlayerController player;
    private Damageable playerDamageable;
    private float playerFeetToCenter = 1.3f; // từ chân tới tâm nhân vật, tính lại theo collider ở Start
    private Vector2 startPosition;
    private Checkpoint activeCheckpoint;
    private bool respawning;
    private int respawnsLeft;

    public int RespawnsLeft { get { return respawnsLeft; } }
    private bool LimitedRespawns { get { return maxRespawns >= 0; } }

    // báo cho boss (và ai cần) biết người chơi vừa hồi sinh xong, để đặt lại trận đánh
    public static event System.Action PlayerRespawned;

    // sân boss khóa camera vào giữa sân: hồi sinh xong camera bám điểm này thay vì bám người chơi
    [HideInInspector] public Transform cameraFollowOverride;

    // đang trong cả quá trình chết -> màn tối -> hồi sinh -> sáng lại; UpgradeManager chờ xong mới mở bảng nâng cấp
    public bool IsRespawning { get; private set; }
    private CinemachineCamera cinemachineCamera;
    private Image fadeImage;
    private float fadeAlpha;
    private AudioSource sfxSource;

    // Editor có thể tắt "Reload Domain" khi bấm Play -> biến static giữ giá trị của lần chơi trước, phải tự xóa
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        FallKillY = float.NegativeInfinity;
        PlayerRespawned = null;
    }

    private void Awake()
    {
        Instance = this;

        // nguồn âm 2D (không nhỏ đi theo khoảng cách): camera nhảy chỗ lúc hồi sinh nên phát theo vị trí sẽ gần như không nghe thấy
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    // phát tiếng hiệu ứng nghe rõ ở mọi chỗ (Checkpoint cũng dùng)
    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            FallKillY = float.NegativeInfinity; // rời màn thì bỏ mốc, scene sau tự tính lại
        }

        if (playerDamageable != null)
        {
            playerDamageable.damageableDeath.RemoveListener(OnPlayerDeath);
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            playerDamageable = playerObject.GetComponent<Damageable>();
            startPosition = playerObject.transform.position;

            CapsuleCollider2D body = playerObject.GetComponent<CapsuleCollider2D>();
            if (body != null)
            {
                playerFeetToCenter = (body.size.y * 0.5f - body.offset.y) * Mathf.Abs(playerObject.transform.lossyScale.y) + 0.05f;
            }
        }

        if (player == null || playerDamageable == null)
        {
            Debug.LogWarning("CheckpointManager: không tìm thấy Player (tag Player) - màn này sẽ không hồi sinh được.");
            return;
        }

        playerDamageable.damageableDeath.AddListener(OnPlayerDeath);

        respawnsLeft = maxRespawns;
        UpdateLivesHud();

        FallKillY = FindLowestGround() - fallDeathDepth;
        cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        CreateFadeImage();
    }

    // đáy của hàng đất thấp nhất trong mọi Tilemap layer Ground; không có Tilemap nào thì không đặt mốc
    private float FindLowestGround()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        float lowest = float.PositiveInfinity;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.layer != groundLayer) continue;

            tilemap.CompressBounds(); // bó khung lại đúng chỗ có ô gạch (khung cũ có thể rộng hơn do từng vẽ rồi xóa)
            BoundsInt cells = tilemap.cellBounds;
            if (cells.size.x <= 0 || cells.size.y <= 0) continue;

            float bottom = tilemap.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0)).y;
            lowest = Mathf.Min(lowest, bottom);
        }

        return float.IsPositiveInfinity(lowest) ? float.NegativeInfinity : lowest;
    }

    // màn đen phủ toàn màn hình, tạo bằng code cho khỏi phải sửa prefab; nằm dưới chữ HUD và menu tạm dừng
    private void CreateFadeImage()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject fadeObject = new GameObject("ManDenHoiSinh", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvas.transform, false);
        fadeObject.transform.SetAsFirstSibling();

        RectTransform rect = (RectTransform)fadeObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.raycastTarget = false; // không chặn chuột bấm nút
        SetFade(0f);
    }

    // Checkpoint gọi khi người chơi (còn sống) chạm vào
    public void Activate(Checkpoint checkpoint)
    {
        if (checkpoint == null || checkpoint == activeCheckpoint) return;

        if (activeCheckpoint != null)
        {
            activeCheckpoint.SetLit(false);
        }

        activeCheckpoint = checkpoint;
        checkpoint.SetLit(true);
        checkpoint.PlayActivateSound();

        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowMessage("Đã lưu điểm hồi sinh");
        }
    }

    private void OnPlayerDeath()
    {
        if (respawning) return; // damageableDeath có thể bị gọi lặp, chỉ hồi sinh 1 lần

        respawning = true;
        IsRespawning = true;
        StopAllCoroutines(); // đang sáng dần sau lần hồi sinh trước thì dừng, làm lại từ đầu

        if (LimitedRespawns && respawnsLeft <= 0)
        {
            StartCoroutine(GameOverRoutine()); // hết lượt -> thua, không hồi sinh nữa
            return;
        }

        if (LimitedRespawns) respawnsLeft--;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = null;
        }

        // vẫn cho xem animation chết rồi tối màn như lúc hồi sinh, sau đó mới hiện bảng
        yield return new WaitForSeconds(respawnDelay);
        yield return FadeTo(1f);

        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.ShowGameOver(maxRespawns);
        }
        else
        {
            // scene không có menu tạm dừng: chơi lại màn luôn
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }
    }

    private void UpdateLivesHud()
    {
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.SetLives(respawnsLeft, maxRespawns);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // camera đứng yên ở chỗ vừa chết, không rơi theo xác xuống vực
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = null;
        }

        // tính theo thời gian game: đang tạm dừng (Esc) thì cũng đứng chờ
        yield return new WaitForSeconds(respawnDelay);
        yield return FadeTo(1f);

        player.RespawnAt(CurrentRespawnPoint(), respawnInvincibility, refillAmmoOnRespawn);
        respawning = false; // từ đây chết nữa thì hồi sinh lại bình thường

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = cameraFollowOverride != null ? cameraFollowOverride : player.transform;
            cinemachineCamera.PreviousStateIsValid = false; // camera nhảy thẳng tới chỗ mới, không lia chậm qua cả map
        }

        PlaySfx(respawnSound);
        PlayerRespawned?.Invoke();

        UpdateLivesHud();
        if (GameHUD.Instance != null)
        {
            if (!LimitedRespawns)
            {
                GameHUD.Instance.ShowMessage("Hồi sinh ở điểm lưu");
            }
            else if (respawnsLeft > 0)
            {
                GameHUD.Instance.ShowMessage("Hồi sinh ở điểm lưu (còn " + respawnsLeft + " lượt)");
            }
            else
            {
                GameHUD.Instance.ShowMessage("Lượt hồi sinh cuối cùng! Chết nữa là phải chơi lại màn");
            }
        }

        yield return null; // chờ 1 khung cho camera và nền kịp tới chỗ mới rồi mới sáng lại
        yield return FadeTo(0f);
        IsRespawning = false;
    }

    private Vector2 CurrentRespawnPoint()
    {
        if (activeCheckpoint != null)
        {
            Vector2 feet = activeCheckpoint.FootPosition;
            return new Vector2(feet.x, feet.y + playerFeetToCenter);
        }
        return startPosition;
    }

    private IEnumerator FadeTo(float target)
    {
        if (fadeImage == null) yield break;

        if (fadeDuration <= 0f)
        {
            SetFade(target);
            yield break;
        }

        while (!Mathf.Approximately(fadeAlpha, target))
        {
            SetFade(Mathf.MoveTowards(fadeAlpha, target, Time.deltaTime / fadeDuration));
            yield return null;
        }
    }

    private void SetFade(float alpha)
    {
        fadeAlpha = alpha;
        if (fadeImage == null) return;

        fadeImage.color = new Color(0f, 0f, 0f, alpha);
        fadeImage.enabled = alpha > 0f;
    }
}
