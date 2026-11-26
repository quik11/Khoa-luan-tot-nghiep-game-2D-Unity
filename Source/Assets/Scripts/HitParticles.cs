using System.Collections.Generic;
using UnityEngine;

// Hạt văng ra khi đánh trúng, khi quái chết, khi tơ đạn tan - toàn bộ vẽ bằng code (ô vuông 1-2 điểm ảnh), không cần ảnh.
// Dùng lại 1 kho hạt (pool) để không tạo/xóa object liên tục (đỡ bộ thu gom rác làm khựng game).
// Không cần gắn vào đâu: gọi HitParticles.Burst(...) là tự tạo.
public class HitParticles : MonoBehaviour
{
    private const int maxParticles = 160;

    private class Particle
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float gravity;
        public float life;
        public float maxLife;
        public Color color;
        public bool active;
    }

    private static HitParticles instance;
    private static Sprite pixelSprite;

    private readonly List<Particle> particles = new List<Particle>();
    private Material material;

    // count hạt, bay ra theo hướng direction (độ) ± spread (độ), tốc độ ngẫu nhiên trong [speedMin, speedMax] ô/s
    public static void Burst(Vector2 position, Color color, int count, float speedMin, float speedMax,
                             float direction = 90f, float spread = 180f, float gravity = 20f, float life = 0.35f, int size = 1)
    {
        if (instance == null)
        {
            instance = new GameObject("HatHieuUng").AddComponent<HitParticles>();
        }
        instance.Spawn(position, color, count, speedMin, speedMax, direction, spread, gravity, life, size);
    }

    private void Spawn(Vector2 position, Color color, int count, float speedMin, float speedMax,
                       float direction, float spread, float gravity, float life, int size)
    {
        for (int i = 0; i < count; i++)
        {
            Particle p = GetFreeParticle();
            if (p == null) return; // đầy kho thì thôi, không tạo thêm

            float angle = (direction + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            float speed = Random.Range(speedMin, speedMax);
            p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            p.gravity = gravity;
            p.maxLife = life * Random.Range(0.7f, 1.3f);
            p.life = p.maxLife;
            p.color = color;
            p.transform.position = new Vector3(position.x, position.y, 0f);
            p.transform.localScale = Vector3.one * Random.Range(size, size + 2); // 1 điểm ảnh ~ 1/16 ô
            p.renderer.color = color;
            p.renderer.enabled = true;
            p.active = true;
        }
    }

    private Particle GetFreeParticle()
    {
        foreach (Particle p in particles)
        {
            if (!p.active) return p;
        }
        if (particles.Count >= maxParticles) return null;

        if (pixelSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
        }

        GameObject go = new GameObject("Hat");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pixelSprite;
        if (material == null) material = FindSpriteMaterial(sr);
        if (material != null) sr.sharedMaterial = material;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 100; // nằm trên nhân vật và quái
        sr.enabled = false;

        Particle particle = new Particle { transform = go.transform, renderer = sr };
        particles.Add(particle);
        return particle;
    }

    // SpriteRenderer tạo bằng code thường đã có sẵn material sprite mặc định của URP 2D; thiếu thì mượn của nhân vật
    // (lúc nhân vật đang nháy trắng thì không mượn, kẻo hạt nào cũng trắng)
    private static Material FindSpriteMaterial(SpriteRenderer fresh)
    {
        if (fresh.sharedMaterial != null) return fresh.sharedMaterial;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        HitFlash flash = player.GetComponent<HitFlash>();
        if (flash != null && flash.IsFlashing) return null;
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        return playerRenderer != null ? playerRenderer.sharedMaterial : null;
    }

    private void Update()
    {
        float dt = Time.deltaTime; // khựng hình thì hạt cũng đứng lại giữa không trung
        foreach (Particle p in particles)
        {
            if (!p.active) continue;

            p.life -= dt;
            if (p.life <= 0f)
            {
                p.active = false;
                p.renderer.enabled = false;
                continue;
            }

            p.velocity.y -= p.gravity * dt;
            p.velocity *= 1f - 2.5f * dt; // lực cản không khí cho hạt chậm dần
            p.transform.position += (Vector3)(p.velocity * dt);

            Color c = p.color;
            c.a = p.color.a * Mathf.Clamp01(p.life / (p.maxLife * 0.5f)); // nửa đời sau mờ dần
            p.renderer.color = c;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
