using System.Collections.Generic;
using UnityEngine;

// Hiệu ứng vẽ bằng code (không cần ảnh), dùng chung 1 kho object để khỏi tạo/xóa liên tục:
//  - Ring: vòng sóng loang ra đúng bằng vùng gây sát thương (kén tơ nổ, đòn dậm chạm đất) -> nhìn là biết đứng trong vòng sẽ ăn đòn;
//  - Ghost: dư ảnh mờ của nhân vật (đòn dậm lao xuống) - chép đúng khung hình đang hiện rồi mờ dần tại chỗ.
// Chạy theo giờ game (khựng hình thì đứng lại cùng mọi thứ). Không cần gắn vào đâu: gọi BurstEffects.Ring / Ghost là tự tạo.
public class BurstEffects : MonoBehaviour
{
    private const int maxEffects = 64;
    private const int ringTextureSize = 64;
    private const float ringPixelsPerUnit = 32f; // ảnh vòng 64 điểm ảnh = đường kính 2 ô ở cỡ 1 -> phóng to bằng đúng bán kính

    private class Effect
    {
        public SpriteRenderer renderer;
        public bool active;
        public bool isRing;
        public float age;
        public float life;
        public Color color;
        public float radius;
        public float squashY;
    }

    private static BurstEffects instance;
    private static Sprite ringSprite;

    private readonly List<Effect> effects = new List<Effect>();
    private Material material;

    // vòng loang từ nhỏ ra tới radius trong 'duration' giây rồi tắt dần; squashY < 1 thì dẹt lại (sóng chạy trên mặt đất)
    public static void Ring(Vector2 center, float radius, Color color, float duration, float squashY = 1f)
    {
        if (radius <= 0f || duration <= 0f) return;
        Effect effect = Instance().Take();
        if (effect == null) return;

        effect.isRing = true;
        effect.radius = radius;
        effect.squashY = squashY;
        effect.life = duration;
        effect.color = color;
        effect.renderer.sprite = RingSprite();
        effect.renderer.flipX = false;
        effect.renderer.sortingLayerName = "Player";
        effect.renderer.sortingOrder = 95; // trên nhân vật và quái, dưới hạt văng
        effect.renderer.transform.position = new Vector3(center.x, center.y, 0f);
        effect.renderer.transform.rotation = Quaternion.identity;
        effect.renderer.transform.localScale = Vector3.zero;
        Instance().UpdateEffect(effect, 0f);
    }

    // dư ảnh: chép khung hình đang hiện của source (cả hướng mặt, cỡ) rồi mờ dần trong 'life' giây
    public static void Ghost(SpriteRenderer source, Color color, float life)
    {
        if (source == null || source.sprite == null || life <= 0f) return;
        Effect effect = Instance().Take();
        if (effect == null) return;

        effect.isRing = false;
        effect.life = life;
        effect.color = color;
        effect.renderer.sprite = source.sprite;
        effect.renderer.flipX = source.flipX;
        effect.renderer.sortingLayerID = source.sortingLayerID;
        effect.renderer.sortingOrder = source.sortingOrder - 1; // nằm sau lưng nhân vật
        Transform from = source.transform;
        effect.renderer.transform.position = from.position;
        effect.renderer.transform.rotation = from.rotation;
        effect.renderer.transform.localScale = from.lossyScale; // nhân vật lật mặt bằng scale âm -> dư ảnh lật theo
        Instance().UpdateEffect(effect, 0f);
    }

    private static BurstEffects Instance()
    {
        if (instance == null) instance = new GameObject("HieuUngVongSong").AddComponent<BurstEffects>();
        return instance;
    }

    private Effect Take()
    {
        foreach (Effect effect in effects)
        {
            if (!effect.active)
            {
                Activate(effect);
                return effect;
            }
        }
        if (effects.Count >= maxEffects) return null; // đầy kho thì thôi

        GameObject go = new GameObject("HieuUng");
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        if (material == null) material = FindSpriteMaterial(renderer);
        if (material != null) renderer.sharedMaterial = material;

        Effect created = new Effect { renderer = renderer };
        effects.Add(created);
        Activate(created);
        return created;
    }

    private static void Activate(Effect effect)
    {
        effect.active = true;
        effect.age = 0f;
        effect.renderer.enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (Effect effect in effects)
        {
            if (effect.active) UpdateEffect(effect, dt);
        }
    }

    private void UpdateEffect(Effect effect, float dt)
    {
        effect.age += dt;
        float t = effect.life > 0f ? effect.age / effect.life : 1f;
        if (t >= 1f)
        {
            effect.active = false;
            effect.renderer.enabled = false;
            return;
        }

        Color c = effect.color;
        if (effect.isRing)
        {
            // loang nhanh lúc đầu rồi chậm lại sát mép vùng gây sát thương, mờ dần ở nửa sau
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            float size = effect.radius * Mathf.Lerp(0.25f, 1f, grow);
            effect.renderer.transform.localScale = new Vector3(size, size * effect.squashY, 1f);
            c.a = effect.color.a * Mathf.Clamp01((1f - t) * 2f);
        }
        else
        {
            c.a = effect.color.a * (1f - t);
        }
        effect.renderer.color = c;
    }

    // vòng trắng: viền dày 3 điểm ảnh, ruột mờ nhẹ để thấy rõ cả vùng bên trong
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;

        int size = ringTextureSize;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float outer = size * 0.5f - 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = 0f;
                if (distance <= outer && distance >= outer - 3f) alpha = 1f;        // viền
                else if (distance < outer - 3f) alpha = 0.22f;                      // ruột
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ringPixelsPerUnit);
        return ringSprite;
    }

    // SpriteRenderer tạo bằng code thường đã có sẵn material sprite mặc định; thiếu thì mượn của nhân vật
    // (lúc nhân vật đang nháy trắng thì không mượn, kẻo hiệu ứng nào cũng trắng xóa)
    private static Material FindSpriteMaterial(SpriteRenderer fresh)
    {
        if (fresh.sharedMaterial != null) return fresh.sharedMaterial;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        HitFlash flash = player.GetComponent<HitFlash>();
        if (flash != null && (flash.IsFlashing || flash.IsTelegraphing)) return null;
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        return playerRenderer != null ? playerRenderer.sharedMaterial : null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
