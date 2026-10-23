// Dibuja un sprite "template" usando la skin del spren.
//
// El template no tiene colores: cada pixel guarda en R y G la coordenada
// (x, y) de la skin multiplicada por SprenSkin.UVStep (4). El shader decodifica
// esa coordenada y pinta el color que hay en ese pixel de la skin. Asi una misma
// animacion sirve para todos los spren: lo unico que cambia es _SkinTex.
//
// Por lo demas es igual a Sprites/Default (tint, flip, pixel snap, mismo blend),
// para que se vea igual que los spren de siempre sobre la ventana transparente.
Shader "CosmereIdle/SprenSkin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Template (mapa UV)", 2D) = "white" {}
        [PerRendererData] _SkinTex ("Skin", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [Toggle(SKIN_SHOW_HOLES)] _ShowHoles ("Mostrar huecos de la skin en magenta", Float) = 0
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
            #pragma fragment SkinFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile_local _ SKIN_SHOW_HOLES
            #include "UnitySprites.cginc"

            // Tiene que coincidir con SprenSkin.UVStep.
            #define UV_STEP 4.0

            sampler2D _SkinTex;
            float4 _SkinTex_TexelSize;

            fixed4 SkinFrag(v2f IN) : SV_Target
            {
                fixed4 map = tex2D(_MainTex, IN.texcoord);
                clip(map.a - 0.5);

                // +0.5 para redondear: el valor llega como float y no como entero.
                float2 cell = floor(map.rg * 255.0 / UV_STEP + 0.5);
                float2 skinUV = (cell + 0.5) * _SkinTex_TexelSize.xy;
                fixed4 c = tex2Dlod(_SkinTex, float4(skinUV, 0, 0));

                #ifdef SKIN_SHOW_HOLES
                if (c.a < 0.5)
                    c = fixed4(1, 0, 1, 1);
                #endif

                c *= IN.color;
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
