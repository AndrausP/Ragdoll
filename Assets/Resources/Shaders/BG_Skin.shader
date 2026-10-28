// Pele contínua do boneco (equivale ao human_skin.gdshader): cada vértice sabe a parte dominante
// (UV2.x * 32) e escurece com o sangue dela. A cor base vem do vértice (roupa/pele) ou de _SkinColor.
// Detalhe: trama de tecido (textura real) na roupa e poros na pele, projetados na posição de REPOUSO do vértice
// (UV3 = posição, UV4 = normal, gravados pelo Dummy), para não escorregar quando o corpo dobra.
Shader "BG/Skin"
{
    Properties
    {
        _SkinColor ("Pele", Color) = (0.80, 0.62, 0.52, 1)
        _BloodColor ("Sangue", Color) = (0.30, 0.015, 0.02, 1)
        _UseVC ("Usa cor do vértice", Float) = 0
        _Fabric ("Tecido (textura)", 2D) = "gray" {}
        _FabricScale ("Repetições do tecido por metro", Float) = 9
        _Pores ("Poros (ruído)", 2D) = "gray" {}
        _Detail ("Usa detalhe", Float) = 0
        _Push ("Empurrar ao longo da normal (m) - camada de roupa", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        fixed4 _SkinColor, _BloodColor;
        half _UseVC, _FabricScale, _Detail, _Push;
        sampler2D _Fabric, _Pores;
        float _Blood[17];
        float _Char[17];
        struct Input { float4 vcol; float blood; float charred; float3 rest; float3 restN; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // roupa: afasta da pele pela normal JÁ deformada (o deslocamento em repouso encolhia nas articulações)
            v.vertex.xyz += v.normal * _Push;
            int part = clamp((int)(v.texcoord1.x * 32.0), 0, 16);
            o.blood = _Blood[part];
            o.charred = _Char[part];
            o.vcol = _UseVC > 0.5 ? v.color : _SkinColor;
            o.rest = v.texcoord2.xyz;
            o.restN = v.texcoord3.xyz;
        }
        fixed4 Tri(sampler2D t, float3 p, float3 n, float s)
        {
            float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z + 1e-5);
            p *= s;
            return tex2D(t, p.zy) * w.x + tex2D(t, p.xz) * w.y + tex2D(t, p.xy) * w.z;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 col = IN.vcol.rgb;
            float rough = 0.6;
            if (_Detail > 0.5)
            {
                // roupa x pele pela cor: pele é bem mais vermelha que azul
                float cloth = saturate(1.0 - (IN.vcol.r - IN.vcol.b) * 5.0);
                float3 n = normalize(IN.restN + 1e-4);
                float weave = dot(Tri(_Fabric, IN.rest, n, _FabricScale).rgb, float3(0.33, 0.33, 0.34));
                float pore = Tri(_Pores, IN.rest, n, 40.0).r;
                col *= lerp(lerp(1.0, 0.82 + pore * 0.36, 0.6), 0.45 + weave * 1.1, cloth);
                rough = lerp(0.55 + pore * 0.12, 0.9, cloth);
            }
            float b = saturate(IN.blood);
            float ch = saturate(IN.charred);
            // queimadura: pele avermelhada -> preta e seca
            float3 burnt = lerp(float3(0.42, 0.12, 0.08), float3(0.05, 0.035, 0.03), saturate(ch * 1.6 - 0.3));
            col = lerp(col, burnt, saturate(ch * 1.4));
            o.Albedo = lerp(col, _BloodColor.rgb, b);
            o.Smoothness = lerp(lerp(1.0 - rough, 0.1, ch), 0.75, b);
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
}
