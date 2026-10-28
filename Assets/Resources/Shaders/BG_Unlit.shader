// Sem iluminação: clarão do tiro, traçante, LED, partículas (névoa de sangue, fogo, fumaça).
// _SrcBlend/_DstBlend escolhem normal (5,10) ou aditivo (5,1); _ZWrite 0 para transparentes.
Shader "BG/Unlit"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 col : COLOR; };
            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.col = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.col; }
            ENDCG
        }
    }
}
