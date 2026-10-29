// Starlife Town's surfaces (Resources/Town, exported from the web town by Tools/town/export.js).
// Lit like Unity's Standard shader, with the town's extras: vertex colours, two-sided or back faces, a depth offset for
// road paint and decals, and emission that follows the time of day (_TownNight: 0 by day, 1 at night).
Shader "Starlife/Town" {
    Properties {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Base", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission map", 2D) = "white" {}
        _EmA ("Emission by day", Float) = 1
        _EmB ("Extra emission at night", Float) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0
        _VC ("Use vertex colours", Float) = 0
        _OffF ("Depth offset factor", Float) = 0
        _OffU ("Depth offset units", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_Cull]
        Offset [_OffF], [_OffU]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        sampler2D _MainTex, _EmissionMap;
        fixed4 _Color, _EmissionColor; half _Glossiness, _Metallic, _Cutoff, _VC, _EmA, _EmB, _TownNight;
        struct Input { float2 uv_MainTex; float4 color : COLOR; };
        void vert (inout appdata_full v) { v.color = lerp(float4(1,1,1,1), v.color, _VC); }
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * IN.color;
            clip(c.a - _Cutoff);
            o.Albedo = c.rgb; o.Metallic = _Metallic; o.Smoothness = _Glossiness; o.Alpha = 1;
            o.Emission = _EmissionColor.rgb * tex2D(_EmissionMap, IN.uv_MainTex).rgb * (_EmA + _EmB * _TownNight);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
