using UnityEngine;

// Hình nhỏ trên đầu quái, vẽ bằng code nên không cần ảnh:
//  - dấu "!" khi quái phát hiện người chơi và sắp lao tới (Quỷ gối lao húc, quái bay bổ nhào);
//  - vòng sao quay quanh đầu khi quái bị choáng (Quỷ gối húc vào tường, quái bay cắm xuống đất).
// HiepSi / FlyingEye tự gắn component này lúc vào màn.
public class EnemyIcons : MonoBehaviour
{
    [Tooltip("Hình cao hơn đỉnh thân quái bấy nhiêu ô")]
    [TenHienThi("Cao hơn đầu")] public float heightAboveHead = 0.55f;
    [TenHienThi("Màu dấu !")] public Color alertColor = new Color(1f, 0.82f, 0.2f, 1f);
    [TenHienThi("Màu sao")] public Color starColor = new Color(1f, 0.95f, 0.45f, 1f);
    [TenHienThi("Số sao")] public int starCount = 3;
    [Tooltip("Sao quay mấy vòng mỗi giây")]
    [TenHienThi("Tốc quay sao")] public float starSpinSpeed = 0.8f;

    // mỗi chuỗi là 1 hàng điểm ảnh từ trên xuống: '#' màu chính, 'o' viền tối, '.' trong suốt
    private static readonly string[] alertRows =
    {
        ".ooo.",
        "o###o",
        "o###o",
        "o###o",
        "o###o",
        ".o#o.",
        ".o#o.",
        "..o..",
        ".ooo.",
        "o###o",
        "o###o",
        ".ooo.",
    };

    private static readonly string[] starRows =
    {
        "..o..",
        ".o#o.",
        "o###o",
        ".o#o.",
        "o.o.o",
    };

    private static Sprite alertSprite;
    private static Sprite starSprite;

    private Collider2D body;
    private SpriteRenderer reference;
    private Material material;

    private SpriteRenderer alert;
    private SpriteRenderer[] stars;
    private float alertTimer;
    private float alertAge;
    private float dizzyTimer;
    private float dizzyAge;

    private void Awake()
    {
        // thân va chạm (không phải vùng phát hiện/hitbox) để biết đỉnh đầu nằm đâu
        foreach (Collider2D candidate in GetComponents<Collider2D>())
        {
            if (!candidate.isTrigger) { body = candidate; break; }
        }
        reference = GetComponent<SpriteRenderer>();
        // lấy material lúc chưa nháy trắng/đỏ (HitFlash đổi material tạm thời)
        if (reference != null) material = reference.sharedMaterial;
    }

    // hiện dấu "!" trong 'duration' giây
    public void ShowAlert(float duration)
    {
        if (alert == null) alert = CreateIcon("DauChamThan", AlertSprite(), alertColor);
        alertTimer = duration;
        alertAge = 0f;
        alert.enabled = true;
        Tick(0f);
    }

    // hiện sao quay quanh đầu trong 'duration' giây
    public void ShowDizzy(float duration)
    {
        if (stars == null)
        {
            stars = new SpriteRenderer[Mathf.Max(1, starCount)];
            for (int i = 0; i < stars.Length; i++) stars[i] = CreateIcon("SaoChoang" + i, StarSprite(), starColor);
        }
        dizzyTimer = duration;
        dizzyAge = 0f;
        foreach (SpriteRenderer star in stars) star.enabled = true;
        Tick(0f);
    }

    public void HideAll()
    {
        alertTimer = 0f;
        dizzyTimer = 0f;
        if (alert != null) alert.enabled = false;
        if (stars != null) foreach (SpriteRenderer star in stars) star.enabled = false;
    }

    // chạy sau khi quái đã di chuyển xong trong khung hình -> hình bám đúng đầu quái
    private void LateUpdate()
    {
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        Vector2 head = HeadPosition();

        if (alert != null && alert.enabled)
        {
            alertTimer -= deltaTime;
            alertAge += deltaTime;
            if (alertTimer <= 0f)
            {
                alert.enabled = false;
            }
            else
            {
                // bật ra to rồi co về cỡ thường cho dễ để ý
                float pop = 1f + 0.6f * Mathf.Clamp01(1f - alertAge / 0.15f);
                alert.transform.position = new Vector3(head.x, head.y + 0.4f, 0f);
                alert.transform.localScale = Vector3.one * 1.25f * pop;
            }
        }

        if (stars != null && stars.Length > 0 && stars[0].enabled)
        {
            dizzyTimer -= deltaTime;
            dizzyAge += deltaTime;
            bool visible = dizzyTimer > 0f;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].enabled = visible;
                if (!visible) continue;

                // vòng elip dẹt quanh đỉnh đầu, sao phía sau nhỏ hơn cho có chiều sâu
                float angle = (dizzyAge * starSpinSpeed + (float)i / stars.Length) * Mathf.PI * 2f;
                float depth = Mathf.Sin(angle);
                stars[i].transform.position = new Vector3(head.x + Mathf.Cos(angle) * 0.55f, head.y + 0.1f + depth * 0.15f, 0f);
                stars[i].transform.localScale = Vector3.one * (0.85f + 0.25f * depth);
            }
        }
    }

    private Vector2 HeadPosition()
    {
        if (body != null)
        {
            Bounds bounds = body.bounds;
            return new Vector2(bounds.center.x, bounds.max.y + heightAboveHead);
        }
        return (Vector2)transform.position + Vector2.up * (1f + heightAboveHead);
    }

    // không làm con của quái: quái lật mặt bằng scale âm, hình con sẽ bị lật/co theo
    private SpriteRenderer CreateIcon(string name, Sprite sprite, Color color)
    {
        GameObject icon = new GameObject(name);
        if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(icon, gameObject.scene);

        SpriteRenderer renderer = icon.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        if (material != null) renderer.sharedMaterial = material;
        if (reference != null)
        {
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = reference.sortingOrder + 20;
        }
        renderer.enabled = false;
        return renderer;
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        if (alert != null) Destroy(alert.gameObject);
        if (stars != null) foreach (SpriteRenderer star in stars) if (star != null) Destroy(star.gameObject);
    }

    private static Sprite AlertSprite()
    {
        if (alertSprite == null) alertSprite = BuildSprite(alertRows);
        return alertSprite;
    }

    private static Sprite StarSprite()
    {
        if (starSprite == null) starSprite = BuildSprite(starRows);
        return starSprite;
    }

    // '#' trắng (tô màu bằng SpriteRenderer.color), 'o' viền gần đen, 16 điểm ảnh mỗi ô như mọi hình trong game
    private static Sprite BuildSprite(string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
        {
            string row = rows[height - 1 - y]; // ảnh tính từ dưới lên
            for (int x = 0; x < width; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Color pixel = c == '#' ? Color.white : c == 'o' ? new Color(0.12f, 0.05f, 0.05f, 1f) : Color.clear;
                texture.SetPixel(x, y, pixel);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
    }
}
