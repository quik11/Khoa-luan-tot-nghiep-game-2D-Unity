using UnityEngine;
using UnityEngine.Events;

// Nháy trắng cả hình trong chớp mắt mỗi lần trúng đòn - cho cảm giác đánh "trúng thật".
// Không cần vẽ hình trắng: tạm đổi material của SpriteRenderer sang shader SpriteHitFlash (Assets/Resources) rồi trả lại.
// Cũng lo luôn việc nháy đỏ báo đòn (Telegraph): quái sắp ra đòn thì ửng đỏ nhấp nháy để người chơi kịp né.
// Gộp 2 kiểu nháy vào 1 chỗ vì cùng đổi material - để 2 script cùng đổi thì dễ trả nhầm material của nhau.
// HiepSi (Quỷ gối), quái bay và BossController tự gắn component này lúc vào màn nên không phải kéo thả;
// muốn chỉnh số riêng cho 1 loại quái thì Add Component HitFlash thẳng vào prefab đó.
[RequireComponent(typeof(Damageable))]
public class HitFlash : MonoBehaviour
{
    [Header("Nháy trắng")]
    [Tooltip("Thời gian nháy (giây, tính theo giờ thật nên lúc khựng hình vẫn thấy nháy). 0 = không nháy trắng (quái bay có hình trúng đòn riêng)")]
    [TenHienThi("Thời gian")] public float flashDuration = 0.1f;
    [Tooltip("Màu nháy")]
    [TenHienThi("Màu")] public Color flashColor = Color.white;
    [Tooltip("1 = trắng hẳn, nhỏ hơn thì chỉ ửng trắng, vẫn thấy nét vẽ")]
    [Range(0f, 1f)] [TenHienThi("Độ đậm")] public float flashStrength = 1f;
    [Tooltip("Hình cần nháy - để trống thì tự lấy SpriteRenderer trên object (hoặc object con)")]
    [TenHienThi("Hình")] public SpriteRenderer targetRenderer;

    [Header("Nháy đỏ báo đòn")]
    [TenHienThi("Màu")] public Color telegraphColor = new Color(1f, 0.12f, 0.08f, 1f);
    [Tooltip("Mức ửng đỏ lúc đậm nhất (1 = đỏ hẳn)")]
    [Range(0f, 1f)] [TenHienThi("Độ đậm")] public float telegraphStrength = 0.6f;
    [Tooltip("Số lần chớp mỗi giây")]
    [TenHienThi("Chớp/giây")] public float telegraphBlinkRate = 8f;

    private const string shaderResourceName = "SpriteHitFlash";
    private static Shader flashShader;
    private static bool warnedMissingShader;

    private Damageable damageable;
    private Material flashMaterial;
    private Material originalMaterial;
    private bool materialSwapped;
    private float flashTimer;
    private bool flashing;
    private float telegraphTimer;
    private float telegraphAge;

    public bool IsFlashing { get { return flashing; } }
    public bool IsTelegraphing { get { return telegraphTimer > 0f; } }

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        // Damageable gắn bằng code thì sự kiện có thể chưa được tạo sẵn
        if (damageable.damageableHit == null) damageable.damageableHit = new UnityEvent<int, Vector2>();
        damageable.damageableHit.AddListener(OnHit);
    }

    private void OnHit(int damage, Vector2 knockback)
    {
        Flash();
    }

    public void Flash()
    {
        if (targetRenderer == null || flashDuration <= 0f) return;
        if (!EnsureMaterial()) return;

        flashing = true;
        flashTimer = flashDuration;
        Refresh();
    }

    // Ửng đỏ chớp chớp trong 'duration' giây (tính theo giờ game: khựng hình thì đứng lại cùng quái).
    // Gọi lại khi đang nháy thì lấy mốc kết thúc muộn hơn.
    public void Telegraph(float duration)
    {
        if (targetRenderer == null || duration <= 0f) return;
        if (!EnsureMaterial()) return;

        if (telegraphTimer <= 0f) telegraphAge = 0f;
        telegraphTimer = Mathf.Max(telegraphTimer, duration);
        Refresh();
    }

    public void StopTelegraph()
    {
        telegraphTimer = 0f;
        Refresh();
    }

    private void Update()
    {
        if (flashing)
        {
            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) flashing = false;
        }

        if (telegraphTimer > 0f)
        {
            telegraphTimer -= Time.deltaTime;
            telegraphAge += Time.deltaTime;
        }

        if (flashing || telegraphTimer > 0f || materialSwapped) Refresh();
    }

    // chọn kiểu nháy đang ưu tiên: trắng (vừa trúng đòn) > đỏ (sắp ra đòn) > hình gốc
    private void Refresh()
    {
        if (targetRenderer == null) return;

        if (flashing)
        {
            SwapIn();
            flashMaterial.SetColor("_FlashColor", flashColor);
            flashMaterial.SetFloat("_FlashAmount", flashStrength);
        }
        else if (telegraphTimer > 0f)
        {
            SwapIn();
            // chớp sáng - tối liên tục cho dễ thấy kể cả quái màu tối, không bao giờ tắt hẳn
            float blink = 0.55f + 0.45f * Mathf.Cos(telegraphAge * telegraphBlinkRate * Mathf.PI * 2f);
            flashMaterial.SetColor("_FlashColor", telegraphColor);
            flashMaterial.SetFloat("_FlashAmount", telegraphStrength * blink);
        }
        else
        {
            SwapOut();
        }
    }

    private void SwapIn()
    {
        if (materialSwapped) return;
        originalMaterial = targetRenderer.sharedMaterial;
        targetRenderer.sharedMaterial = flashMaterial;
        materialSwapped = true;
    }

    private void SwapOut()
    {
        if (!materialSwapped) return;
        materialSwapped = false;
        if (targetRenderer != null) targetRenderer.sharedMaterial = originalMaterial;
    }

    private bool EnsureMaterial()
    {
        if (flashMaterial != null) return true;

        if (flashShader == null) flashShader = Resources.Load<Shader>(shaderResourceName);
        if (flashShader == null)
        {
            if (!warnedMissingShader)
            {
                Debug.LogWarning("HitFlash: không thấy shader Assets/Resources/" + shaderResourceName + ".shader nên quái không nháy trắng.");
                warnedMissingShader = true;
            }
            return false;
        }

        // mỗi con 1 material riêng: nhiều con cùng chớp đỏ lệch nhịp nhau thì không ảnh hưởng nhau
        flashMaterial = new Material(flashShader);
        flashMaterial.name = "NhayTrang (tự tạo)";
        return true;
    }

    private void OnDisable()
    {
        flashing = false;
        telegraphTimer = 0f;
        SwapOut();
    }

    private void OnDestroy()
    {
        if (damageable != null && damageable.damageableHit != null) damageable.damageableHit.RemoveListener(OnHit);
        if (flashMaterial != null) Destroy(flashMaterial);
    }
}
