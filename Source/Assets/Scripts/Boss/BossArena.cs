using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

// Sân đấu boss: người chơi đi qua vạch bắt đầu -> cổng đóng (vẽ thêm ô đất chặn lối về), camera khóa giữa sân, đổi nhạc boss,
// boss bắt đầu đánh. Hạ boss -> mở cổng, camera bám người chơi lại, trả nhạc cũ.
// Công cụ dựng màn tự tạo object này từ ký hiệu | (cổng), O (boss), h (đồng hồ cát) trong file màn chữ - không cần chỉnh tay.
public class BossArena : MonoBehaviour
{
    [Header("Boss")]
    [TenHienThi("Boss")] public BossController boss;

    [Header("Kích thước sân")]
    [Tooltip("Người chơi đi qua x này là bắt đầu đánh")]
    [TenHienThi("Mốc bắt đầu")] public float startX;
    [TenHienThi("Mép trái")] public float leftX;   // mép trong tường trái (cổng)
    [TenHienThi("Mép phải")] public float rightX;  // mép trong tường phải
    [TenHienThi("Mặt sàn")] public float floorY;  // mặt sàn
    [TenHienThi("Chỗ đồng hồ cát")] public Transform[] hourglassPoints;

    [Header("Cổng")]
    [TenHienThi("Tilemap đất")] public Tilemap groundTilemap;
    [TenHienThi("Ô cổng")] public TileBase gateTile;
    [TenHienThi("Các ô cổng")] public Vector3Int[] gateCells;

    [Header("Camera, nhạc")]
    [TenHienThi("Điểm camera")] public Transform cameraPoint;
    [Tooltip("0 = giữ nguyên độ rộng khung hình camera")]
    [TenHienThi("Cỡ camera")] public float cameraOrthoSize = 0f;
    [TenHienThi("Nhạc boss")] public AudioClip bossMusic;

    public bool Started { get; private set; }
    public float CenterX { get { return (leftX + rightX) * 0.5f; } }

    private Transform player;
    private Damageable playerDamageable;
    private CinemachineCamera cinemachineCamera;
    private float originalOrthoSize;
    private AudioSource musicSource;
    private AudioClip originalMusic;

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerDamageable = playerObject.GetComponent<Damageable>();
        }

        cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        MusicVolume music = FindFirstObjectByType<MusicVolume>();
        if (music != null) musicSource = music.GetComponent<AudioSource>();

        if (boss == null) boss = FindFirstObjectByType<BossController>();
        if (boss == null) Debug.LogWarning("BossArena: sân boss không có boss (thiếu ký tự O trong file màn).");
    }

    private void Update()
    {
        if (Started || player == null || boss == null) return;
        if (playerDamageable != null && !playerDamageable.isAlive) return;

        if (player.position.x > startX)
        {
            StartFight();
        }
    }

    private void StartFight()
    {
        Started = true;
        EnsureRespawnInside();
        SetGate(true);
        LockCamera(true);
        SwitchMusic(bossMusic);
        boss.BeginFight(this);
    }

    // nhảy qua điểm lưu trong sân mà không chạm vào thì chết sẽ hồi sinh ở ngoài cổng đã đóng -> kẹt.
    // Bắt đầu đánh là tự bật điểm lưu nằm trong sân (gần cổng nhất)
    private void EnsureRespawnInside()
    {
        if (CheckpointManager.Instance == null) return;

        Checkpoint best = null;
        foreach (Checkpoint checkpoint in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
        {
            float x = checkpoint.transform.position.x;
            if (x < leftX || x > rightX) continue;
            if (best == null || x < best.transform.position.x) best = checkpoint;
        }

        if (best != null) CheckpointManager.Instance.Activate(best);
        else Debug.LogWarning("BossArena: sân boss không có điểm lưu (C) - chết sẽ hồi sinh ngoài cổng.");
    }

    // BossController gọi khi boss chết
    public void OnBossDefeated()
    {
        SetGate(false);
        LockCamera(false);
        SwitchMusic(originalMusic);
    }

    private void SetGate(bool closed)
    {
        if (groundTilemap == null || gateCells == null) return;

        foreach (Vector3Int cell in gateCells)
        {
            groundTilemap.SetTile(cell, closed ? gateTile : null);
        }

        // ô đất mới vẽ phải tính lại va chạm ngay, không thì người chơi đi xuyên cổng
        TilemapCollider2D tilemapCollider = groundTilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null) TilemapColliderRefresh.Refresh(tilemapCollider);
    }

    private void LockCamera(bool locked)
    {
        Transform target = locked && cameraPoint != null ? cameraPoint : player;

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.cameraFollowOverride = locked ? cameraPoint : null;
        }

        if (cinemachineCamera == null) return;
        cinemachineCamera.Follow = target; // lia từ từ sang giữa sân (không nhảy cóc)

        if (cameraOrthoSize > 0f)
        {
            LensSettings lens = cinemachineCamera.Lens;
            if (locked) originalOrthoSize = lens.OrthographicSize;
            lens.OrthographicSize = locked ? cameraOrthoSize : originalOrthoSize;
            cinemachineCamera.Lens = lens;
        }
    }

    private void SwitchMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (originalMusic == null) originalMusic = musicSource.clip;
        if (musicSource.clip == clip) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    // vẽ khung sân trong cửa sổ Scene cho dễ nhìn (không hiện trong game)
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawLine(new Vector3(leftX, floorY), new Vector3(rightX, floorY));
        Gizmos.DrawLine(new Vector3(startX, floorY), new Vector3(startX, floorY + 6f));
    }
}
