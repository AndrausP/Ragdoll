// Superfície PBR com textura real (cor + normal + rugosidade) em projeção triplanar no mundo, para o mapa
// (blocos sem UV contínua). Normal triplanar com mistura "whiteout"; precisa de tangentes na malha.
Shader "BG/Pbr"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Cor (textura)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _RoughMap ("Rugosidade", 2D) = "white" {}
        _TriScale ("Escala (repetições por metro)", Float) = 0.5
        _NormalStrength ("Força da normal", Float) = 1
        _RoughMul ("Multiplica rugosidade", Float) = 1
        _Metallic ("Metal", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.5
        sampler2D _MainTex, _BumpMap, _RoughMap;
        fixed4 _Color;
        half _TriScale, _NormalStrength, _RoughMul, _Metallic;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = normalize(WorldNormalVector(IN, float3(0, 0, 1)));
            float3 bw = pow(abs(wn), 4);
            bw /= (bw.x + bw.y + bw.z);
            float3 p = IN.worldPos * _TriScale;
            float2 ux = p.zy, uy = p.xz, uz = p.xy;
            fixed4 c = tex2D(_MainTex, ux) * bw.x + tex2D(_MainTex, uy) * bw.y + tex2D(_MainTex, uz) * bw.z;
            half r = tex2D(_RoughMap, ux).r * bw.x + tex2D(_RoughMap, uy).r * bw.y + tex2D(_RoughMap, uz).r * bw.z;
            half3 tx = UnpackNormal(tex2D(_BumpMap, ux));
            half3 ty = UnpackNormal(tex2D(_BumpMap, uy));
            half3 tz = UnpackNormal(tex2D(_BumpMap, uz));
            tx.xy *= _NormalStrength; ty.xy *= _NormalStrength; tz.xy *= _NormalStrength;
            // whiteout: a normal de cada projeção vira normal no mundo
            tx = half3(tx.xy + wn.zy, abs(tx.z) * wn.x);
            ty = half3(ty.xy + wn.xz, abs(ty.z) * wn.y);
            tz = half3(tz.xy + wn.xy, abs(tz.z) * wn.z);
            float3 nw = normalize(tx.zyx * bw.x + ty.xzy * bw.y + tz.xyz * bw.z);
            // mundo -> espaço tangente (o Standard espera a normal no espaço tangente)
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            o.Normal = normalize(float3(dot(nw, T), dot(nw, B), dot(nw, N)));
            o.Albedo = c.rgb * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(1.0 - r * _RoughMul);
            o.Alpha = 1;
        }
        ENDCG
    }
}
