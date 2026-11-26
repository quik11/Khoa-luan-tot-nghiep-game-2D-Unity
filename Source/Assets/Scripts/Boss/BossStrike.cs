using System.Collections;
using UnityEngine;

// Một đòn "từ trên trời giáng xuống" của boss: vạch đỏ cảnh báo dưới đất -> (hình động nếu có) -> hitbox bật trong 1 khoảng -> tự biến mất.
// Dùng cho phép Bàn tay hạn chót (có hình bàn tay) và sóng Quá hạn phủ cả sàn (không có hình, chỉ là dải màu).
// BossController tạo bằng code lúc đánh (Create), không cần prefab.
public class BossStrike : MonoBehaviour
{
    // cấu hình - BossController gán ngay sau khi tạo, trước khi Start chạy
    [TenHienThi("Khung hình")] public Sprite[] frames;          // để trống = không có hình động, hiện dải màu lúc hitbox bật
    [TenHienThi("Khung/giây")] public float frameRate = 12f;
    [TenHienThi("Khung bắt đầu")] public int hitStartFrame = 7;    // khung hình bắt đầu gây sát thương (tính từ 0)
    [TenHienThi("Khung kết thúc")] public int hitEndFrame = 11;
    [TenHienThi("Thời gian đánh")] public float activeTime = 0.3f;  // dùng khi không có hình động
    [TenHienThi("Báo trước")] public float warningTime = 0.5f; // vạch đỏ hiện trước khi hình bắt đầu chạy
    [TenHienThi("Cỡ hình")] public float artScale = 1f;
    [TenHienThi("Cỡ vùng đánh")] public Vector2 hitboxSize = new Vector2(2f, 4f);
    [TenHienThi("Sát thương")] public int damage = 20;
    [TenHienThi("Hất văng")] public Vector2 knockback = new Vector2(0f, 7f);
    [TenHienThi("Màu vạch đỏ")] public Color warningColor = new Color(1f, 0.15f, 0.15f, 0.55f);
    [TenHienThi("Màu dải")] public Color bandColor = new Color(0.25f, 0f, 0.3f, 0.75f);
    [TenHienThi("Âm thanh")] public AudioClip hitSound;
    [TenHienThi("Rung màn hình")] public float shakeAmount = 0.12f; // rung màn hình lúc giáng xuống (ô)
    [TenHienThi("Vật liệu")] public Material material;
    [TenHienThi("Lớp vẽ")] public string sortingLayer = "Enemies";

    private SpriteRenderer warning;
    private SpriteRenderer art;
    private BoxCollider2D hitbox;
    private float warningAlphaBase;

    // tạo đòn tại 1 điểm trên mặt đất (x = giữa đòn, y = mặt đất)
    public static BossStrike Create(Vector2 groundPoint, string name)
    {
        GameObject root = new GameObject(name);
        root.transform.position = groundPoint;
        return root.AddComponent<BossStrike>();
    }

    private void Start()
    {
        int hitLayer = LayerMask.NameToLayer("EnemyHitBox");

        // vạch đỏ sát mặt đất, rộng bằng hitbox
        warning = MakeRenderer("CanhBao", PixelArt.White, 20);
        warning.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        warning.transform.localScale = new Vector3(hitboxSize.x, 0.3f, 1f);
        warning.color = warningColor;
        warningAlphaBase = warningColor.a;

        art = MakeRenderer("Hinh", null, 15);
        art.transform.localScale = new Vector3(artScale, artScale, 1f);

        // hitbox dùng lại tanCong (giống đòn của Quỷ gối): layer EnemyHitBox chỉ chạm người chơi
        GameObject hitObject = new GameObject("Hitbox");
        hitObject.layer = hitLayer;
        hitObject.transform.SetParent(transform, false);
        hitObject.transform.localPosition = new Vector3(0f, hitboxSize.y * 0.5f, 0f);
        hitbox = hitObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = hitboxSize;
        hitbox.enabled = false;
        tanCong attack = hitObject.AddComponent<tanCong>();
        attack.attackDamage = damage;
        attack.knockback = knockback;

        StartCoroutine(Run());
    }

    private SpriteRenderer MakeRenderer(string childName, Sprite sprite, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        return renderer;
    }

    private IEnumerator Run()
    {
        // 1) vạch cảnh báo nhấp nháy
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            Blink(t);
            yield return null;
        }

        bool hasFrames = frames != null && frames.Length > 0;
        if (hasFrames)
        {
            // 2) chạy hình bàn tay, bật hitbox đúng mấy khung đập xuống đất
            for (int i = 0; i < frames.Length; i++)
            {
                art.sprite = frames[i];
                if (i == hitStartFrame) StrikeOn();
                if (i == hitEndFrame + 1) StrikeOff();
                if (i < hitStartFrame) Blink(t += 1f / frameRate);
                yield return new WaitForSeconds(1f / frameRate);
            }
            StrikeOff();
        }
        else
        {
            // 2') không có hình: dải màu cao bằng hitbox hiện trong lúc gây sát thương
            art.sprite = PixelArt.White;
            art.transform.localScale = new Vector3(hitboxSize.x, hitboxSize.y, 1f);
            art.transform.localPosition = new Vector3(0f, hitboxSize.y * 0.5f, 0f);
            art.color = bandColor;
            StrikeOn();
            yield return new WaitForSeconds(activeTime);
            StrikeOff();
            art.enabled = false;
        }

        Destroy(gameObject);
    }

    private void Blink(float time)
    {
        if (warning == null) return;
        Color c = warningColor;
        c.a = warningAlphaBase * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 12f)));
        warning.color = c;
    }

    private void StrikeOn()
    {
        if (hitbox.enabled) return;
        hitbox.enabled = true;
        if (warning != null) warning.enabled = false;
        BossController.PlaySound(hitSound);
        ScreenShake.Shake(shakeAmount, 0.2f);
        // bụi tung lên ở chỗ giáng xuống
        HitParticles.Burst(transform.position, new Color(0.75f, 0.65f, 0.8f, 1f), Mathf.Clamp(Mathf.RoundToInt(hitboxSize.x * 4f), 6, 40),
                           2f, 6f, 90f, 70f, 12f, 0.45f);
    }

    private void StrikeOff()
    {
        if (hitbox != null) hitbox.enabled = false;
    }
}
