// Versão transparente do BG/Lit (pele no raio-X, respingos, poças, lesões de órgão).
Shader "BG/LitFade"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        _Glossiness ("Lisura", Range(0,1)) = 0.5
        _Metallic ("Metal", Range(0,1)) = 0
        _UseVC ("Usa cor do vértice", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _UseVC;
        struct Input { float2 uv_MainTex; float4 vcol; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            if (_UseVC > 0.5) c *= IN.vcol;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
}
