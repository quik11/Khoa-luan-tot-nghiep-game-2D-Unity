// Shader nháy trắng khi trúng đòn (HitFlash.cs tạm thay vào SpriteRenderer khoảng 0.1 s rồi trả lại material cũ).
// Dựng theo khung shader Sprites/Default có sẵn của Unity (dùng chung UnitySprites.cginc) nên lật hình (flipX),
// màu SpriteRenderer, độ trong suốt khi xác mờ dần vẫn đúng như cũ; chỉ khác là phần màu bị đè thành màu nháy.
// Để trong thư mục Resources để Unity luôn đóng gói kèm khi build (code nạp bằng Resources.Load).
Shader "BrandNEU/SpriteHitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Màu nháy", Color) = (1,1,1,1)
        _FlashAmount ("Mức nháy (0 = hình gốc, 1 = trắng hẳn)", Range(0, 1)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FlashFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;
            fixed _FlashAmount;

            fixed4 FlashFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount); // giữ nguyên hình dáng (alpha), chỉ đè màu
                c.rgb *= c.a; // blend kiểu nhân sẵn alpha giống Sprites/Default
                return c;
            }
        ENDCG
        }
    }
}
