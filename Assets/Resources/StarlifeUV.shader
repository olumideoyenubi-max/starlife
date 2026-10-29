// Draws, for every pixel, where it is on the head's skin texture (red, green) and whether it is the head at all (blue).
// The makeup studio uses this to turn a brush stroke on the mirror into paint on her skin.
Shader "Starlife/UV" {
    Properties { _IsHead ("", Float) = 0 }
    SubShader {
        Pass {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _IsHead;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float4 frag (v2f i) : SV_Target { return float4(i.uv, _IsHead, 1); }
            ENDCG
        }
    }
}
