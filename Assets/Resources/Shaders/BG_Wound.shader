// Decalque de ferida sobre a pele: textura RGBA com alfa, brilho úmido e "crescimento" (_Grow) para o filete
// de sangue que escorre (descarta o que estiver além de 1 - uv.y > _Grow). Desenha por cima da pele (Offset).
Shader "BG/Wound"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Cor", Color) = (1, 1, 1, 1)
        _Gloss ("Brilho", Range(0, 1)) = 0.7
        _Grow ("Crescimento", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+20" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 200
        Cull Off
        ZWrite Off
        Offset -2, -2
        CGPROGRAM
        #pragma surface surf Standard decal:blend
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Gloss;
        float _Grow;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            clip(_Grow - (1.0 - IN.uv_MainTex.y));   // uv.y = 1 na ferida, 0 na ponta do filete
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Smoothness = _Gloss * c.a;
            o.Metallic = 0;
        }
        ENDCG
    }
}
