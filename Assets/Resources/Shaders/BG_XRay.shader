// Raio-X: desenha por cima de tudo (sem teste de profundidade), com sombreamento simples.
// Equivale ao no_depth_test + render_priority do Godot. _Prio ordena dentro da fila Overlay.
Shader "BG/XRay"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _UseVC ("Usa cor do vértice", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        ZTest Always
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            half _UseVC;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; fixed4 col : COLOR; };
            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.col = _UseVC > 0.5 ? v.color * _Color : _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float l = 0.45 + 0.55 * saturate(dot(normalize(i.n), normalize(float3(0.3, 0.8, 0.5))));
                return fixed4(i.col.rgb * l, i.col.a);
            }
            ENDCG
        }
    }
}
