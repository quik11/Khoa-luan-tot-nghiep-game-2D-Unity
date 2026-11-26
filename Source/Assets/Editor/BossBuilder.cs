using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Công cụ trong Unity Editor: nạp hình Bringer of Death + tạo prefab boss "Deadline cận kề".
// Menu trên cùng: Brand NEU day > Tạo / cập nhật prefab boss Deadline
// - Chưa có prefab: tạo mới với đủ thành phần (thân, hitbox chém, máu, script boss, âm thanh).
// - Đã có prefab: chỉ nạp lại hình (giữ nguyên các số đã chỉnh trong Inspector).
// Công cụ dựng màn tự gọi công cụ này nếu gặp ký tự O mà chưa có prefab.
public static class BossBuilder
{
    public const string prefabPath = "Assets/Characters/Enemies/Boss/DeadlineBoss.prefab";
    private const string artFolder = "Assets/Art/BringerOfDeath/Individual Sprite";
    private const string sfxFolder = "Assets/music/sounds efect/";
    private const string healthPrefabPath = "Assets/Objects/HealthPickUp.prefab";
    private const string materialSourcePath = "Assets/Characters/Enemies/HiepSi/HiepSi.prefab";

    // Khung hình Bringer of Death 140x93: thân boss nằm lệch phải (cột ~104), chân ở hàng dưới cùng (chừa 1 px trống).
    // Đặt tâm hình ngay chân boss -> lật mặt trái/phải không bị nhảy chỗ, đặt xuống đất là chân chạm đất.
    private static readonly Vector2 bodyPivot = new Vector2(104f / 140f, 1f / 93f);
    // Phép bàn tay nằm giữa khung (cột ~67), cũng chạm đáy khung
    private static readonly Vector2 spellPivot = new Vector2(67f / 140f, 1f / 93f);

    private const float artScale = 1.6f; // thân boss cao ~5.6 ô (người chơi 2.5 ô)

    [MenuItem("Brand NEU day/Tạo - cập nhật prefab boss Deadline")]
    private static void BuildMenu()
    {
        GameObject prefab = EnsurePrefab(true);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }

    // trả về prefab boss; chưa có thì tạo. refreshArt = true thì nạp lại hình cho prefab đã có
    public static GameObject EnsurePrefab(bool refreshArt)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null && !refreshArt) return existing;

        Dictionary<string, Sprite[]> art = LoadArt();

        if (existing == null)
        {
            return CreatePrefab(art);
        }

        // đã có prefab: chỉ thay hình, không đụng các số đã chỉnh
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        BossController boss = contents.GetComponent<BossController>();
        if (boss != null)
        {
            AssignArt(boss, art);
            FillMissingSounds(boss);
        }
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("BossBuilder: đã nạp lại hình cho " + prefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static GameObject CreatePrefab(Dictionary<string, Sprite[]> art)
    {
        EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int hitLayer = LayerMask.NameToLayer("EnemyHitBox");

        GameObject root = new GameObject("DeadlineBoss");
        root.layer = enemyLayer;

        // gốc boss đặt ở chân. Kinematic: boss chỉ lướt ngang trên sàn, không rơi, không vướng bệ (thân cao 5 ô lọt dưới bệ 4 ô vẫn đi qua được).
        // Người chơi vẫn đi xuyên boss như mọi quái (layer Enemy và Player không va chạm nhau)
        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D body = root.AddComponent<BoxCollider2D>();
        body.size = new Vector2(2.6f, 5.2f);
        body.offset = new Vector2(0f, 2.65f);

        Damageable damageable = root.AddComponent<Damageable>();
        damageable.MaxHealth = 800;
        damageable.Health = 800;
        damageable.superArmor = true;
        damageable.canBePulled = false;
        damageable.invincibilityTime = 0.1f;
        damageable.webHitsToStun = 6; // boss cần 6 phát tơ đạn trong 5 giây mới bị trói
        damageable.stunDuration = 1.5f;

        // hình
        GameObject artObject = new GameObject("Hinh");
        artObject.layer = enemyLayer;
        artObject.transform.SetParent(root.transform, false);
        artObject.transform.localScale = new Vector3(artScale, artScale, 1f);
        SpriteRenderer renderer = artObject.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Enemies";
        Material material = FindSpriteMaterial();
        if (material != null) renderer.sharedMaterial = material;

        // hitbox chém lưỡi hái: chỉ bật đúng mấy khung vung lưỡi hái (BossController bật/tắt)
        GameObject hitObject = new GameObject("ChemHitbox");
        hitObject.layer = hitLayer;
        hitObject.transform.SetParent(root.transform, false);
        BoxCollider2D hitbox = hitObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector2(10f, 4.8f);   // lưỡi hái trong hình quét xa ~10 ô trước mặt
        hitbox.offset = new Vector2(5.1f, 2.5f);
        hitbox.enabled = false;
        tanCong slash = hitObject.AddComponent<tanCong>();
        slash.attackDamage = 18;
        slash.knockback = new Vector2(7f, 4f);

        BossController boss = root.AddComponent<BossController>();
        boss.artRenderer = renderer;
        boss.slashHitbox = hitbox;
        boss.healthDropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(healthPrefabPath);
        AssignArt(boss, art);
        FillMissingSounds(boss);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("BossBuilder: đã tạo " + prefabPath);
        return prefab;
    }

    private static void AssignArt(BossController boss, Dictionary<string, Sprite[]> art)
    {
        boss.idleFrames = Pick(art, "Idle", boss.idleFrames);
        boss.walkFrames = Pick(art, "Walk", boss.walkFrames);
        boss.attackFrames = Pick(art, "Attack", boss.attackFrames);
        boss.hurtFrames = Pick(art, "Hurt", boss.hurtFrames);
        boss.deathFrames = Pick(art, "Death", boss.deathFrames);
        boss.castFrames = Pick(art, "Cast", boss.castFrames);
        boss.spellFrames = Pick(art, "Spell", boss.spellFrames);
        boss.artFacesRight = false; // Bringer of Death vẽ quay mặt sang trái

        if (boss.artRenderer != null && boss.idleFrames != null && boss.idleFrames.Length > 0)
        {
            boss.artRenderer.sprite = boss.idleFrames[0];
        }
        EditorUtility.SetDirty(boss);
    }

    private static Sprite[] Pick(Dictionary<string, Sprite[]> art, string key, Sprite[] current)
    {
        Sprite[] frames;
        return art.TryGetValue(key, out frames) && frames.Length > 0 ? frames : current;
    }

    // đọc từng thư mục Idle/Walk/Attack/Hurt/Death/Cast/Spell (bản có hiệu ứng, bỏ qua "No Effect Sprites"),
    // chỉnh cách nạp ảnh cho chuẩn pixel art rồi xếp khung theo số cuối tên file (_1, _2, ... _10)
    private static Dictionary<string, Sprite[]> LoadArt()
    {
        Dictionary<string, Sprite[]> result = new Dictionary<string, Sprite[]>();
        if (!AssetDatabase.IsValidFolder(artFolder))
        {
            Debug.LogWarning("BossBuilder: không thấy thư mục " + artFolder + " - boss sẽ không có hình (vẫn đánh được, để test).");
            return result;
        }

        foreach (string animation in new[] { "Idle", "Walk", "Attack", "Hurt", "Death", "Cast", "Spell" })
        {
            string folder = artFolder + "/" + animation;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("BossBuilder: thiếu thư mục " + folder);
                continue;
            }

            List<string> files = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') == folder) files.Add(path); // không lấy thư mục con
            }
            files.Sort((a, b) => FrameNumber(a).CompareTo(FrameNumber(b)));

            Vector2 pivot = animation == "Spell" ? spellPivot : bodyPivot;
            List<Sprite> sprites = new List<Sprite>();
            foreach (string path in files)
            {
                SetupImporter(path, pivot);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }

            result[animation] = sprites.ToArray();
            Debug.Log("BossBuilder: " + animation + " - " + sprites.Count + " khung hình");
        }

        return result;
    }

    private static int FrameNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // ảnh pixel: 16 pixel = 1 ô như cả game, không làm mờ, không nén (nén làm lem màu), tâm đặt ở chân
    private static void SetupImporter(string path, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed = importer.textureType != TextureImporterType.Sprite
                       || importer.spriteImportMode != SpriteImportMode.Single
                       || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelArtPPU)
                       || importer.filterMode != FilterMode.Point
                       || importer.textureCompression != TextureImporterCompression.Uncompressed
                       || importer.mipmapEnabled
                       || settings.spriteAlignment != (int)SpriteAlignment.Custom
                       || settings.spritePivot != pivot;
        if (!changed) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelArtPPU;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private const float PixelArtPPU = 16f;

    private static void FillMissingSounds(BossController boss)
    {
        if (boss.introSound == null) boss.introSound = Sfx("10_Battle_SFX/55_Encounter_02.wav");
        if (boss.slashSound == null) boss.slashSound = Sfx("10_Battle_SFX/22_Slash_04.wav");
        if (boss.castSound == null) boss.castSound = Sfx("8_Atk_Magic_SFX/45_Charge_05.wav");
        if (boss.handSound == null) boss.handSound = Sfx("8_Atk_Magic_SFX/30_Earth_02.wav");
        if (boss.teleportSound == null) boss.teleportSound = Sfx("8_Atk_Magic_SFX/25_Wind_01.wav");
        if (boss.shieldSound == null) boss.shieldSound = Sfx("8_Buffs_Heals_SFX/17_Def_buff_01.wav");
        if (boss.shieldBreakSound == null) boss.shieldBreakSound = Sfx("10_Battle_SFX/39_Block_03.wav");
        if (boss.hourglassBreakSound == null) boss.hourglassBreakSound = Sfx("8_Atk_Magic_SFX/13_Ice_explosion_01.wav");
        if (boss.overdueSound == null) boss.overdueSound = Sfx("8_Atk_Magic_SFX/18_Thunder_02.wav");
        if (boss.stunSound == null) boss.stunSound = Sfx("8_Buffs_Heals_SFX/21_Debuff_01.wav");
        if (boss.deathSound == null) boss.deathSound = Sfx("10_Battle_SFX/69_Enemy_death_01.wav");
        if (boss.diplomaSound == null) boss.diplomaSound = Sfx("8_Buffs_Heals_SFX/30_Revive_03.wav");
        if (boss.healthDropPrefab == null) boss.healthDropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(healthPrefabPath);
        EditorUtility.SetDirty(boss);
    }

    private static AudioClip Sfx(string relativePath)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxFolder + relativePath);
        if (clip == null) Debug.LogWarning("BossBuilder: không thấy âm thanh " + sfxFolder + relativePath + " (boss im lặng ở đòn này)");
        return clip;
    }

    // lấy đúng material sprite đang dùng cho quái (sprite có ánh sáng 2D của URP) để boss sáng tối giống quái khác
    private static Material FindSpriteMaterial()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(materialSourcePath);
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
