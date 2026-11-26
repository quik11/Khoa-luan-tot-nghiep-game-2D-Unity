using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Công cụ trong Unity Editor: đọc 1 file chữ trong Assets/Levels (vd Man2.txt) rồi dựng thành scene màn chơi.
// Menu trên cùng: Brand NEU day > Dựng màn từ file chữ...
//
// Cách làm: sao chép Màn 1 (GameplayScene) làm khung để giữ nguyên camera, ánh sáng, nền, HUD, nhân vật, nhạc,
// xóa hết ô gạch + quái + vật phẩm cũ, rồi vẽ lại theo từng ký tự trong file. Dựng xong vẫn sửa tay bằng Tile Palette được.
//
// Ký hiệu trong file (mỗi ký tự = 1 ô, dòng trên cùng của bản đồ = hàng cao nhất):
//   #  đất / tường (layer Ground - đứng được, bật tường được, tơ kéo bám được)
//   .  khoảng trống
//   P  chỗ nhân vật xuất phát
//   E  Quỷ Gối (quái đi bộ), S  Quỷ gối bọc thép (đỡ đòn trước mặt), N  Quỷ gối mất ngủ (nhanh, hay lao húc)
//   F  quái bay tuần tra ngang, V  quái bay tuần tra dọc
//   +  đùi gà hồi máu, a  đạn tơ (nhặt 1 lần), A  trạm hồi tơ (nhặt xong vài giây sau tự hiện lại)
//   B  quyển sách nâng cấp, D  cửa qua màn
//   C  điểm lưu (checkpoint) - chết thì hồi sinh ở điểm lưu chạm gần nhất
//   X  vùng chết (đáy vực) - chạm vào là chết ngay; đặt thấp hơn mép vực ít nhất 3 hàng để người chơi kịp thấy mình rơi
//   O  boss màn cuối "Deadline cận kề" (đặt ở ô ngang chân, chỉ 1 con) - chưa có prefab boss thì tool tự tạo
//   |  cổng sân boss: ô trống lúc đầu, người chơi đi vào sân thì hóa thành đất chặn lối về, hạ boss thì mở lại
//   h  chỗ đồng hồ cát lơ lửng lúc boss bật khiên (đặt ở chính ô đó, không tự hạ xuống đất)
//   P, E, S, N, D, C đặt ở ô ngang chân (ô ngay trên mặt đất) - tool tự nâng lên cho chân chạm đất, không bị lún
// Rơi khỏi map (thấp hơn hàng đất thấp nhất) cũng chết, không cần ký hiệu gì.
// Dòng bắt đầu bằng // là ghi chú. Dòng bắt đầu bằng @ là cài đặt:
//   @scene Man2          tên scene sẽ tạo (phải trùng tên trong GameProgress.levelScenes)
//   @tuanTra 3           quái bay đi xa bao nhiêu ô mỗi bên
//   @hoiTo 4             trạm hồi tơ (A) hiện lại sau bao nhiêu giây
//   @tileMat <đường dẫn tile>   ô gạch mặt trên (để trống thì tự lấy ô hay dùng nhất ở Màn 1)
//   @tileTrong <đường dẫn tile> ô gạch phần đất bên dưới
//   @hoiSinh 3           số lượt hồi sinh mỗi lần vào màn (-1 = không giới hạn); không ghi thì giữ như Màn 1
//   @nhacBoss <đường dẫn nhạc>  nhạc khi vào sân boss (để trống thì dùng nhạc mặc định)
public static class LevelBuilder
{
    private const string templateScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string levelsFolder = "Assets/Levels";
    private const string scenesFolder = "Assets/Scenes";

    private const string hiepSiPrefabPath = "Assets/Characters/Enemies/HiepSi/HiepSi.prefab";
    private const string flyingEyePrefabPath = "Assets/Characters/Enemies/FlyingEye/FlyingEye.prefab";
    private const string healthPrefabPath = "Assets/Objects/HealthPickUp.prefab";
    private const string ammoPrefabPath = "Assets/Objects/WebAmmoPickUp.prefab";
    private const string bookPrefabPath = "Assets/Objects/UpgradePickUp.prefab";
    private const string doorPrefabPath = "Assets/Objects/LevelDoor.prefab";
    private const string checkpointPrefabPath = "Assets/Objects/Checkpoint.prefab";
    // vị trí nền (object Background) so với chỗ xuất phát ở Màn 1: Background (1.70, 0.00) - nhân vật (4.7, -2.2)
    private static readonly Vector2 backgroundOffsetFromSpawn = new Vector2(-2.99522f, 2.19833f);
    private const string defaultBossMusicPath = "Assets/music/sounds efect/Legendary_JRPG_Battle_Music_Pack/01 Furious Battle/Assets_for_Unity/Battle-Furious-Gt_loop.ogg";
    private const float bossCameraHeight = 6.5f; // camera sân boss nhìn cao hơn sàn bao nhiêu ô (khung hình cao 16 ô)

    [MenuItem("Brand NEU day/Dựng màn từ file chữ...")]
    private static void BuildFromFileMenu()
    {
        string path = EditorUtility.OpenFilePanel("Chọn file màn (.txt) trong Assets/Levels", Path.GetFullPath(levelsFolder), "txt");
        if (string.IsNullOrEmpty(path)) return;

        Build(path, true);
    }

    [MenuItem("Brand NEU day/Dựng lại tất cả màn trong Assets/Levels")]
    private static void BuildAllMenu()
    {
        if (!Directory.Exists(levelsFolder)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string file in Directory.GetFiles(levelsFolder, "*.txt"))
        {
            Build(file, false);
        }
    }

    private class LevelFile
    {
        public string sceneName;
        public int patrol = 3;
        public float ammoRespawn = 4f;
        public string surfaceTilePath;
        public string fillTilePath;
        public int? respawns;
        public string bossMusicPath;
        public readonly List<string> rows = new List<string>();
    }

    public static void Build(string txtPath, bool askBeforeOverwrite)
    {
        LevelFile level = Parse(txtPath);
        if (level == null) return;

        if (askBeforeOverwrite && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string scenePath = scenesFolder + "/" + level.sceneName + ".unity";
        if (scenePath == templateScenePath)
        {
            Debug.LogError("LevelBuilder: @scene không được trùng Màn 1 (GameplayScene).");
            return;
        }

        if (File.Exists(scenePath))
        {
            if (askBeforeOverwrite && !EditorUtility.DisplayDialog("Dựng màn", "Scene " + scenePath + " đã có. Dựng lại sẽ ghi đè mọi chỉnh sửa tay trong scene đó. Tiếp tục?", "Ghi đè", "Hủy"))
            {
                return;
            }
            AssetDatabase.DeleteAsset(scenePath);
        }

        if (!AssetDatabase.CopyAsset(templateScenePath, scenePath))
        {
            Debug.LogError("LevelBuilder: không sao chép được " + templateScenePath + " sang " + scenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // ----- ô gạch -----
        Tilemap ground = FindGroundTilemap();
        if (ground == null)
        {
            Debug.LogError("LevelBuilder: không tìm thấy Tilemap layer Ground trong scene khung.");
            return;
        }

        TileBase surfaceTile, fillTile;
        PickTiles(ground, level, out surfaceTile, out fillTile);

        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tilemap.ClearAllTiles();
        }

        // ----- xóa quái, vật phẩm, cửa, điểm tuần tra của Màn 1 -----
        RemoveAll<HiepSi>();
        RemoveAll<FlyingEye>();
        RemoveAll<HealthPickup>();
        RemoveAll<WebAmmoPickUp>();
        RemoveAll<UpgradePickUp>();
        RemoveAll<LevelDoor>();
        RemoveAll<Checkpoint>();
        RemoveAll<KillZone>();
        RemoveAll<BossController>();
        RemoveAll<BossArena>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Waypoints" || root.name == killZoneRootName) Object.DestroyImmediate(root);
        }

        GameObject waypointRoot = new GameObject("Waypoints");

        GameObject hiepSiPrefab = Load(hiepSiPrefabPath);
        GameObject flyingEyePrefab = Load(flyingEyePrefabPath);
        GameObject healthPrefab = Load(healthPrefabPath);
        GameObject ammoPrefab = Load(ammoPrefabPath);
        GameObject bookPrefab = Load(bookPrefabPath);
        GameObject doorPrefab = Load(doorPrefabPath);
        GameObject checkpointPrefab = Load(checkpointPrefabPath);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 oldPlayerPosition = player != null ? player.transform.position : Vector3.zero;
        Vector3? spawn = null;
        int enemyCount = 0, doorCount = 0, checkpointCount = 0;

        int height = level.rows.Count;
        HashSet<Vector2Int> killCells = new HashSet<Vector2Int>();
        Vector2Int? bossCell = null;
        List<Vector3Int> gateCells = new List<Vector3Int>();
        List<Vector3> hourglassPoints = new List<Vector3>();
        for (int r = 0; r < height; r++)
        {
            string row = level.rows[r];
            int y = height - 1 - r;

            for (int x = 0; x < row.Length; x++)
            {
                char c = row[x];
                Vector3Int cell = new Vector3Int(x, y, 0);
                Vector3 world = ground.GetCellCenterWorld(cell);
                float floorY = ground.CellToWorld(cell).y; // mép dưới của ô = mặt đất ngay bên dưới

                switch (c)
                {
                    case '#':
                        // ô trên cùng của khối đất dùng ô "mặt đất", bên dưới dùng ô "đất trong"
                        bool surface = r == 0 || x >= level.rows[r - 1].Length || level.rows[r - 1][x] != '#';
                        ground.SetTile(cell, surface ? surfaceTile : fillTile);
                        break;
                    case 'P':
                        spawn = new Vector3(world.x, floorY + (player != null ? HalfHeightBelowPivot(player) : 1.25f), world.z);
                        break;
                    case 'E':
                    case 'S':
                    case 'N':
                        GameObject walker = Spawn(hiepSiPrefab, world, scene);
                        if (walker != null)
                        {
                            walker.transform.position = new Vector3(world.x, floorY + HalfHeightBelowPivot(walker), world.z);
                            if (c != 'E')
                            {
                                // biến thể: chỉ đổi 1 ô trong Inspector, màu/cỡ/máu do HiepSi tự đổi lúc vào màn
                                HiepSi variant = walker.GetComponent<HiepSi>();
                                variant.bienThe = c == 'S' ? HiepSi.BienThe.BocThep : HiepSi.BienThe.MatNgu;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(variant);
                                walker.name = c == 'S' ? "HiepSi_BocThep" : "HiepSi_MatNgu";
                            }
                            enemyCount++;
                        }
                        break;
                    case 'F':
                    case 'V':
                        GameObject eye = Spawn(flyingEyePrefab, world, scene);
                        if (eye != null)
                        {
                            Vector3 offset = c == 'F' ? new Vector3(level.patrol, 0, 0) : new Vector3(0, level.patrol, 0);
                            SetWaypoints(eye, waypointRoot.transform, world - offset, world + offset);
                            enemyCount++;
                        }
                        break;
                    case '+':
                        Spawn(healthPrefab, world, scene);
                        break;
                    case 'a':
                        Spawn(ammoPrefab, world, scene);
                        break;
                    case 'A':
                        GameObject station = Spawn(ammoPrefab, world, scene);
                        WebAmmoPickUp ammo = station != null ? station.GetComponent<WebAmmoPickUp>() : null;
                        if (ammo != null)
                        {
                            SerializedObject serializedAmmo = new SerializedObject(ammo);
                            serializedAmmo.FindProperty("respawnTime").floatValue = level.ammoRespawn;
                            serializedAmmo.ApplyModifiedPropertiesWithoutUndo();
                        }
                        break;
                    case 'B':
                        Spawn(bookPrefab, world, scene);
                        break;
                    case 'D':
                        Spawn(doorPrefab, world + Vector3.up, scene); // cửa tự rơi xuống mặt đất khi vào màn
                        doorCount++;
                        break;
                    case 'C':
                        if (Spawn(checkpointPrefab, world + Vector3.up, scene) != null) checkpointCount++; // cọc tự rơi xuống mặt đất khi vào màn
                        break;
                    case 'X':
                        killCells.Add(new Vector2Int(x, y));
                        break;
                    case 'O':
                        if (bossCell.HasValue) Debug.LogWarning("LevelBuilder: có nhiều hơn 1 ký tự O - chỉ dùng cái đầu tiên.");
                        else bossCell = new Vector2Int(x, y);
                        break;
                    case '|':
                        gateCells.Add(cell);
                        break;
                    case 'h':
                        hourglassPoints.Add(world);
                        break;
                }
            }
        }

        ground.CompressBounds();

        int killZoneCount = BuildKillZones(ground, killCells);

        bool hasBoss = bossCell.HasValue && BuildBossArena(scene, ground, level, bossCell.Value, gateCells, hourglassPoints, fillTile != null ? fillTile : surfaceTile);
        if (!bossCell.HasValue && gateCells.Count > 0) Debug.LogWarning("LevelBuilder: có cổng | nhưng không có boss O - cổng sẽ không bao giờ đóng.");

        if (level.respawns.HasValue)
        {
            CheckpointManager checkpointManager = Object.FindFirstObjectByType<CheckpointManager>(FindObjectsInactive.Include);
            if (checkpointManager != null)
            {
                SerializedObject serializedManager = new SerializedObject(checkpointManager);
                serializedManager.FindProperty("maxRespawns").intValue = level.respawns.Value;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("LevelBuilder: không thấy CheckpointManager (trong GameHUD) để đặt @hoiSinh.");
            }
        }

        // ----- đặt nhân vật, camera về đúng chỗ xuất phát; nền đặt cách chỗ xuất phát đúng như Màn 1 -----
        // (nền parallax tính theo vị trí camera lúc bắt đầu. Trước đây nền dời theo độ dời của nhân vật, mà nhân vật trong
        //  GameplayScene lỡ bị kéo đi chỗ khác rồi lưu thì nền các màn dựng ra lệch theo -> Màn 2-5 từng lệch (94.36, 1.56))
        if (spawn.HasValue && player != null)
        {
            player.transform.position = spawn.Value;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == player) continue;
                bool isCamera = root.GetComponent<Camera>() != null || root.name.Contains("Cinemachine") || root.name.Contains("Camera");
                if (isCamera)
                {
                    root.transform.position = new Vector3(spawn.Value.x, spawn.Value.y, root.transform.position.z);
                }
                else if (root.name == "Background")
                {
                    Vector3 templateOffset = root.transform.position - oldPlayerPosition;
                    if (Vector2.Distance(templateOffset, backgroundOffsetFromSpawn) > 0.5f)
                    {
                        Debug.LogWarning("LevelBuilder: trong GameplayScene nhân vật không ở chỗ xuất phát cũ (4.7, -2.2) " +
                                         "nên nền lệch so với Màn 1 - đã tự đặt nền theo vị trí chuẩn của Màn 1.");
                    }
                    root.transform.position = new Vector3(spawn.Value.x + backgroundOffsetFromSpawn.x,
                                                          spawn.Value.y + backgroundOffsetFromSpawn.y,
                                                          root.transform.position.z);
                }
            }
        }
        else
        {
            Debug.LogWarning("LevelBuilder: file không có ký tự P - nhân vật giữ nguyên chỗ cũ.");
        }

        if (doorCount == 0 && !hasBoss) Debug.LogWarning("LevelBuilder: file không có ký tự D - màn này không có cửa qua màn.");

        // Ô gạch vừa vẽ nhưng va chạm của đất chỉ được Unity tính lại ở nhịp sau -> phải ép tính ngay trước khi lưu,
        // nếu không file scene giữ nguyên hình va chạm của Màn 1 (Màn 2 từng bị rơi xuyên đất vì vậy)
        foreach (TilemapCollider2D tilemapCollider in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TilemapColliderRefresh.Refresh(tilemapCollider);
        }
        CheckGroundCollider(ground);

        AddToBuildSettings(scenePath);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("LevelBuilder: đã dựng " + scenePath + " (" + height + " hàng, " + enemyCount + " quái, " + doorCount + " cửa, "
                  + checkpointCount + " điểm lưu, " + killZoneCount + " vùng chết" + (hasBoss ? ", có sân boss" : "") + ").");
    }

    // Sân boss: đặt boss ở ô O; tường trái = cổng | (hoặc ô đất gần nhất bên trái boss), tường phải = ô đất gần nhất bên phải
    // trên cùng hàng chân boss. Tạo object "SanBoss" (BossArena) giữ kích thước sân, cổng, điểm đồng hồ cát, điểm khóa camera.
    private static bool BuildBossArena(Scene scene, Tilemap ground, LevelFile level, Vector2Int bossCell,
                                       List<Vector3Int> gateCells, List<Vector3> hourglassPoints, TileBase gateTile)
    {
        GameObject bossPrefab = BossBuilder.EnsurePrefab(false);
        if (bossPrefab == null)
        {
            Debug.LogError("LevelBuilder: không tạo được prefab boss (" + BossBuilder.prefabPath + ").");
            return false;
        }

        int height = level.rows.Count;
        string bossRow = level.rows[height - 1 - bossCell.y];
        Vector3Int cell = new Vector3Int(bossCell.x, bossCell.y, 0);
        float floorY = ground.CellToWorld(cell).y;

        GameObject boss = Spawn(bossPrefab, ground.GetCellCenterWorld(cell), scene);
        boss.transform.position = new Vector3(ground.GetCellCenterWorld(cell).x, floorY + HalfHeightBelowPivot(boss), 0f);

        // tường phải: ô # đầu tiên bên phải boss trên hàng chân boss
        int rightCell = bossCell.x + 1;
        while (rightCell < bossRow.Length && bossRow[rightCell] != '#') rightCell++;
        float rightX = ground.CellToWorld(new Vector3Int(rightCell, bossCell.y, 0)).x;

        // tường trái: cổng (cột lớn nhất của các ô |), không có cổng thì ô # đầu tiên bên trái boss
        int leftCell = -1;
        foreach (Vector3Int gate in gateCells) leftCell = Mathf.Max(leftCell, gate.x);
        if (leftCell < 0)
        {
            leftCell = bossCell.x - 1;
            while (leftCell >= 0 && (leftCell >= bossRow.Length || bossRow[leftCell] != '#')) leftCell--;
        }
        float leftX = ground.CellToWorld(new Vector3Int(leftCell + 1, bossCell.y, 0)).x;

        GameObject arenaObject = new GameObject("SanBoss");
        BossArena arena = arenaObject.AddComponent<BossArena>();
        arena.boss = boss.GetComponent<BossController>();
        arena.leftX = leftX;
        arena.rightX = rightX;
        arena.floorY = floorY;
        arena.startX = leftX + 1.5f; // qua cổng 1.5 ô mới đóng cổng, khỏi đóng đè lên người
        arena.groundTilemap = ground;
        arena.gateTile = gateTile;
        arena.gateCells = gateCells.ToArray();

        GameObject cameraPoint = new GameObject("DiemCamera");
        cameraPoint.transform.SetParent(arenaObject.transform, false);
        cameraPoint.transform.position = new Vector3((leftX + rightX) * 0.5f, floorY + bossCameraHeight, 0f);
        arena.cameraPoint = cameraPoint.transform;

        List<Transform> points = new List<Transform>();
        for (int i = 0; i < hourglassPoints.Count; i++)
        {
            GameObject point = new GameObject("DiemDongHoCat_" + (i + 1));
            point.transform.SetParent(arenaObject.transform, false);
            point.transform.position = hourglassPoints[i];
            points.Add(point.transform);
        }
        arena.hourglassPoints = points.ToArray();

        string musicPath = !string.IsNullOrEmpty(level.bossMusicPath) ? level.bossMusicPath : defaultBossMusicPath;
        arena.bossMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
        if (arena.bossMusic == null) Debug.LogWarning("LevelBuilder: không thấy nhạc boss " + musicPath + " - giữ nhạc màn chơi.");

        if (rightX - leftX > 28f) Debug.LogWarning("LevelBuilder: sân boss rộng " + (rightX - leftX) + " ô, hơn khung hình camera (~28 ô) - người chơi ở mép sẽ khuất.");
        Debug.Log("LevelBuilder: sân boss từ x " + leftX + " đến " + rightX + ", sàn y " + floorY + ", " + gateCells.Count + " ô cổng, " + points.Count + " chỗ đồng hồ cát.");
        return true;
    }

    // so khung va chạm của đất với vùng có ô gạch: lệch quá 1 ô là va chạm chưa được tính lại -> báo đỏ trong Console
    private static void CheckGroundCollider(Tilemap ground)
    {
        TilemapCollider2D tilemapCollider = ground.GetComponent<TilemapCollider2D>();
        CompositeCollider2D composite = tilemapCollider != null ? tilemapCollider.composite : null;
        if (composite == null) return;

        Bounds tiles = ground.localBounds;
        Vector3 tileMin = ground.transform.TransformPoint(tiles.min);
        Vector3 tileMax = ground.transform.TransformPoint(tiles.max);
        Bounds physics = composite.bounds;

        bool match = composite.pathCount > 0
                     && Mathf.Abs(physics.min.x - tileMin.x) <= 1f && Mathf.Abs(physics.max.x - tileMax.x) <= 1f
                     && Mathf.Abs(physics.min.y - tileMin.y) <= 1f && Mathf.Abs(physics.max.y - tileMax.y) <= 1f;

        if (match)
        {
            Debug.Log("LevelBuilder: va chạm đất khớp ô gạch (" + composite.pathCount + " đường viền).");
        }
        else
        {
            Debug.LogError("LevelBuilder: va chạm đất KHÔNG khớp ô gạch (ô gạch " + tileMin + " → " + tileMax + ", va chạm "
                           + physics.min + " → " + physics.max + "). Vào màn vẫn tự tạo lại va chạm, nhưng hãy báo lại lỗi này.");
        }
    }

    private const string killZoneRootName = "VucChet";

    // gộp các ô X liền nhau thành hình chữ nhật (ngang trước, rồi gộp các hàng giống hệt nhau chồng lên nhau),
    // mỗi hình 1 BoxCollider2D trigger layer Default + KillZone. Trả về số vùng đã tạo.
    private static int BuildKillZones(Tilemap ground, HashSet<Vector2Int> cells)
    {
        if (cells.Count == 0) return 0;

        // các đoạn ngang trên từng hàng: (hàng, cột đầu, cột cuối)
        List<RectInt> runs = new List<RectInt>();
        foreach (Vector2Int cell in cells)
        {
            if (cells.Contains(new Vector2Int(cell.x - 1, cell.y))) continue; // không phải ô đầu đoạn
            int end = cell.x;
            while (cells.Contains(new Vector2Int(end + 1, cell.y))) end++;
            runs.Add(new RectInt(cell.x, cell.y, end - cell.x + 1, 1));
        }

        // gộp đoạn ngang với đoạn ngay bên dưới nếu trùng cột đầu/cuối
        runs.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
        List<RectInt> zones = new List<RectInt>();
        foreach (RectInt run in runs)
        {
            int index = zones.FindIndex(z => z.x == run.x && z.width == run.width && z.y == run.y + 1);
            if (index >= 0)
            {
                RectInt merged = zones[index];
                zones[index] = new RectInt(merged.x, run.y, merged.width, merged.height + 1);
            }
            else
            {
                zones.Add(run);
            }
        }

        GameObject root = new GameObject(killZoneRootName);
        int defaultLayer = LayerMask.NameToLayer("Default");

        for (int i = 0; i < zones.Count; i++)
        {
            RectInt zone = zones[i];
            Vector3 bottomLeft = ground.CellToWorld(new Vector3Int(zone.x, zone.y, 0));
            Vector3 topRight = ground.CellToWorld(new Vector3Int(zone.x + zone.width, zone.y + zone.height, 0));

            GameObject zoneObject = new GameObject("VungChet_" + (i + 1));
            zoneObject.layer = defaultLayer;
            zoneObject.transform.SetParent(root.transform, false);
            zoneObject.transform.position = new Vector3((bottomLeft.x + topRight.x) * 0.5f, (bottomLeft.y + topRight.y) * 0.5f, 0f);

            BoxCollider2D box = zoneObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y));

            zoneObject.AddComponent<KillZone>();
        }

        return zones.Count;
    }

    private static LevelFile Parse(string txtPath)
    {
        if (!File.Exists(txtPath))
        {
            Debug.LogError("LevelBuilder: không thấy file " + txtPath);
            return null;
        }

        LevelFile level = new LevelFile();
        foreach (string raw in File.ReadAllLines(txtPath))
        {
            string line = raw.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0 || line.StartsWith("//")) continue;

            if (line.StartsWith("@"))
            {
                int space = line.IndexOf(' ');
                string key = space > 0 ? line.Substring(1, space - 1) : line.Substring(1);
                string value = space > 0 ? line.Substring(space + 1).Trim() : "";

                if (key == "scene") level.sceneName = value;
                else if (key == "tuanTra") int.TryParse(value, out level.patrol);
                else if (key == "hoiTo") float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out level.ammoRespawn);
                else if (key == "tileMat") level.surfaceTilePath = value;
                else if (key == "tileTrong") level.fillTilePath = value;
                else if (key == "hoiSinh")
                {
                    int respawns;
                    if (int.TryParse(value, out respawns)) level.respawns = respawns;
                }
                else if (key == "nhacBoss") level.bossMusicPath = value;
                continue;
            }

            level.rows.Add(line);
        }

        if (string.IsNullOrEmpty(level.sceneName))
        {
            Debug.LogError("LevelBuilder: file " + txtPath + " thiếu dòng @scene (vd: @scene Man2).");
            return null;
        }

        if (level.rows.Count == 0)
        {
            Debug.LogError("LevelBuilder: file " + txtPath + " không có bản đồ.");
            return null;
        }

        return level;
    }

    private static Tilemap FindGroundTilemap()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.layer == groundLayer) return tilemap;
        }
        return null;
    }

    // không chỉ định ô gạch thì lấy ô hay dùng nhất ở Màn 1: ô nằm trên cùng (không có gì phía trên) làm mặt đất, còn lại làm đất trong
    private static void PickTiles(Tilemap ground, LevelFile level, out TileBase surfaceTile, out TileBase fillTile)
    {
        Dictionary<TileBase, int> surfaceCount = new Dictionary<TileBase, int>();
        Dictionary<TileBase, int> fillCount = new Dictionary<TileBase, int>();

        foreach (Vector3Int position in ground.cellBounds.allPositionsWithin)
        {
            TileBase tile = ground.GetTile(position);
            if (tile == null) continue;

            Dictionary<TileBase, int> counter = ground.GetTile(position + Vector3Int.up) == null ? surfaceCount : fillCount;
            int count;
            counter.TryGetValue(tile, out count);
            counter[tile] = count + 1;
        }

        surfaceTile = !string.IsNullOrEmpty(level.surfaceTilePath) ? AssetDatabase.LoadAssetAtPath<TileBase>(level.surfaceTilePath) : MostUsed(surfaceCount);
        fillTile = !string.IsNullOrEmpty(level.fillTilePath) ? AssetDatabase.LoadAssetAtPath<TileBase>(level.fillTilePath) : MostUsed(fillCount);

        if (surfaceTile == null) surfaceTile = fillTile;
        if (fillTile == null) fillTile = surfaceTile;
    }

    private static TileBase MostUsed(Dictionary<TileBase, int> counter)
    {
        TileBase best = null;
        int bestCount = 0;
        foreach (KeyValuePair<TileBase, int> pair in counter)
        {
            if (pair.Value > bestCount)
            {
                best = pair.Key;
                bestCount = pair.Value;
            }
        }
        return best;
    }

    private static void RemoveAll<T>() where T : Component
    {
        foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component == null) continue;

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
            Object.DestroyImmediate(root != null ? root : component.gameObject);
        }
    }

    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogError("LevelBuilder: không thấy prefab " + path);
        return prefab;
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, Scene scene)
    {
        if (prefab == null) return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = position;
        return instance;
    }

    // khoảng từ pivot xuống đáy khung va chạm chính (nhân vật/quái có pivot ở giữa người) + 0.05 cho khỏi chạm sát
    private static float HalfHeightBelowPivot(GameObject obj)
    {
        float scaleY = Mathf.Abs(obj.transform.lossyScale.y);
        foreach (Collider2D collider in obj.GetComponents<Collider2D>())
        {
            if (collider.isTrigger) continue;

            CapsuleCollider2D capsule = collider as CapsuleCollider2D;
            if (capsule != null) return (capsule.size.y * 0.5f - capsule.offset.y) * scaleY + 0.05f;

            BoxCollider2D box = collider as BoxCollider2D;
            if (box != null) return (box.size.y * 0.5f - box.offset.y) * scaleY + 0.05f;
        }
        return 1.3f; // không đoán được thì nâng cỡ nửa người
    }

    private static void SetWaypoints(GameObject eye, Transform root, Vector3 a, Vector3 b)
    {
        FlyingEye flyingEye = eye.GetComponent<FlyingEye>();
        if (flyingEye == null) return;

        Transform first = new GameObject(eye.name + "_Diem1").transform;
        Transform second = new GameObject(eye.name + "_Diem2").transform;
        first.SetParent(root, false);
        second.SetParent(root, false);
        first.position = a;
        second.position = b;

        SerializedObject serialized = new SerializedObject(flyingEye);
        SerializedProperty waypoints = serialized.FindProperty("waypoints");
        waypoints.arraySize = 2;
        waypoints.GetArrayElementAtIndex(0).objectReferenceValue = first;
        waypoints.GetArrayElementAtIndex(1).objectReferenceValue = second;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene existing in scenes)
        {
            if (existing.path == scenePath) return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
