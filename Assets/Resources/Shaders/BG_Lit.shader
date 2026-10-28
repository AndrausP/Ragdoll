// Material padrão do jogo (equivale ao StandardMaterial3D usado no Godot).
// Cor, rugosidade/metal, cor por vértice opcional, textura com projeção triplanar opcional e emissão.
Shader "BG/Lit"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        _Glossiness ("Lisura", Range(0,1)) = 0.5
        _Metallic ("Metal", Range(0,1)) = 0
        _UseVC ("Usa cor do vértice", Float) = 0
        _Tri ("Triplanar", Float) = 0
        _TriScale ("Escala triplanar", Float) = 1
        _RoughTex ("Rugosidade pela textura", Float) = 0
        _Emission ("Emissão", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _UseVC, _Tri, _TriScale, _RoughTex;
        fixed4 _Emission;
        struct Input { float2 uv_MainTex; float4 vcol; float3 worldPos; float3 worldNormal; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex;
            if (_Tri > 0.5)
            {
                float3 n = abs(normalize(IN.worldNormal));
                n /= (n.x + n.y + n.z);
                float3 p = IN.worldPos * _TriScale;
                tex = tex2D(_MainTex, p.yz) * n.x + tex2D(_MainTex, p.xz) * n.y + tex2D(_MainTex, p.xy) * n.z;
            }
            else tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 c = tex * _Color;
            if (_UseVC > 0.5) c *= IN.vcol;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _RoughTex > 0.5 ? _Glossiness * (0.55 + 0.9 * (1.0 - tex.r)) : _Glossiness;
            o.Emission = _Emission.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
}
