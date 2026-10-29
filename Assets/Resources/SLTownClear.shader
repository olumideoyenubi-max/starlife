// Starlife Town's see-through surfaces: shop glass, the mall's glass roof, water and the faint stains on the pavement.
Shader "Starlife/TownClear" {
    Properties {
        _Color ("Colour", Color) = (1,1,1,0.3)
        _MainTex ("Base", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0
        _VC ("Use vertex colours", Float) = 0
        _OffF ("Depth offset factor", Float) = 0
        _OffU ("Depth offset units", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("Write depth", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Cull [_Cull] ZWrite [_ZWrite]
        Offset [_OffF], [_OffU]
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color; half _Glossiness, _Metallic, _VC;
        struct Input { float2 uv_MainTex; float4 color : COLOR; };
        void vert (inout appdata_full v) { v.color = lerp(float4(1,1,1,1), v.color, _VC); }
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * IN.color;
            o.Albedo = c.rgb; o.Metallic = _Metallic; o.Smoothness = _Glossiness; o.Alpha = c.a;
        }
        ENDCG
    }
}
