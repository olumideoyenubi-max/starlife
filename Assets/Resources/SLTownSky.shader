// Starlife Town's sky (a skybox): a gradient from horizon to zenith, the sun's disc and glow, and stars at night.
// The same sky as the web town; SLTownWorld sets the colours for the hour.
Shader "Starlife/TownSky" {
    Properties {
        _Top ("Zenith", Color) = (0.24,0.5,0.84,1)
        _Horizon ("Horizon", Color) = (0.74,0.84,0.95,1)
        _Ground ("Below the horizon", Color) = (0.43,0.42,0.4,1)
        _SunDir ("Sun direction", Vector) = (0,1,0,0)
        _SunCol ("Sun colour", Color) = (1,1,1,1)
        _Night ("Night", Range(0,1)) = 0
    }
    SubShader {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top, _Horizon, _Ground, _SunCol; float4 _SunDir; float _Night;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            float h (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            fixed4 frag (v2f i) : SV_Target {
                float3 d = normalize(i.dir); float y = d.y;
                float3 c = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.55)) : lerp(_Horizon.rgb, _Ground.rgb, saturate(-y * 4));
                float s = max(dot(d, normalize(_SunDir.xyz)), 0);
                c += _SunCol.rgb * (pow(s, 900) * 6 + pow(s, 12) * 0.35);
                if (_Night > 0.01 && y > 0) { float2 g = floor(float2(atan2(d.z, d.x), asin(y)) * 260); float st = step(0.9965, h(g)); c += st * _Night * 0.9 * smoothstep(0, 0.25, y); }
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
