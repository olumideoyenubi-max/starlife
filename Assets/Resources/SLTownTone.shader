// Starlife Town's film look: the web town renders with ACES tone mapping and an exposure that rises at night.
// The project draws in gamma space, so the picture is taken to linear, tone mapped, and back.
Shader "Hidden/Starlife/TownTone" {
    Properties { _MainTex ("Frame", 2D) = "white" {} _Exposure ("Exposure", Float) = 1 }
    SubShader {
        Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Exposure;
            float3 Aces (float3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
            fixed4 frag (v2f_img i) : SV_Target {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c = pow(max(c, 0), 2.2) * _Exposure;
                c = pow(Aces(c), 1 / 2.2);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
