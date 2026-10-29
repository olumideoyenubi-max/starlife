// The 3D starlet's surfaces. Besides a plain tint it can recolour by brightness, which is how black hair
// becomes blonde and a cream top becomes any wardrobe colour; _SatMask keeps white parts (the leggings) white.
Shader "Starlife/Starlet" {
    Properties {
        _MainTex ("Base", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Recolor ("Recolour by brightness", Range(0,1)) = 0
        _Lift ("Recolour lift", Range(0,1)) = 0
        _Gain ("Recolour gain", Range(0,6)) = 1
        _SatMask ("Recolour only coloured parts", Range(0,1)) = 0
        _Mean ("Main colour of the texture", Color) = (0.5,0.5,0.5,1)
        _Keep ("Keep details that are another colour when dyeing", Range(0,1)) = 0
        _Sheen ("Soft glow at grazing angles (cloth, skin)", Range(0,1)) = 0
        _SSS ("Light scattered under the skin", Range(0,1)) = 0
        _Hair ("Hair: strand highlights, depth shading, streaks", Range(0,1)) = 0
        _Detail ("Fine surface detail (hair coils, weave)", Range(0,2)) = 0
        _DetailScale ("Detail per metre", Float) = 400
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
        _Inset ("Shrink along the normal (m)", Float) = 0
        _Shrink ("Shrink towards the body axis (fraction)", Range(0,0.5)) = 0
        _ZPush ("Lose to anything nearly as close (depth units)", Float) = 0
        _MakeupTex ("Makeup painted on the skin", 2D) = "black" {}
        _Makeup ("Wears makeup", Float) = 0
        _IsHead ("Is the head (for the makeup studio)", Float) = 0
        _Shaped ("Part of a figure that can be reshaped", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull [_Cull]
        // A figure's build (weight, muscle, shoulders, hips, bust, leg length, head size) is applied here, to every piece she
        // is made of, in her own space: all pieces share one rest pose, so body and clothes change shape together.
        CGINCLUDE
        float4x4 _ObjToActor, _ActorToObj; float4 _Shape, _Shape2; float _Shaped;
        float bell (float v, float c, float w) { float d = (v - c) / w; return exp(-d * d); }
        float3 Reshape (float3 p) {          // p in metres: x across, y up from the floor, z to her front
            float w = _Shape.x, m = _Shape.y, sh = _Shape.z, hp = _Shape.w, bu = _Shape2.x, lg = _Shape2.y, hd = _Shape2.z, bm = _Shape2.w;
            float side = p.x < 0 ? -1.0 : 1.0, ax = abs(p.x), y = p.y;
            float bnd = lerp(0.208, 0.158, smoothstep(1.0, 1.3, y));                                   // where the torso ends and an arm begins
            float arm = smoothstep(bnd - 0.012, bnd + 0.012, ax) * smoothstep(0.70, 0.78, y) * (1 - smoothstep(1.46, 1.52, y));
            float head = smoothstep(1.455, 1.50, y);
            float leg = (1 - smoothstep(0.86, 0.97, y)) * (1 - arm), torso = smoothstep(0.86, 0.97, y) * (1 - head) * (1 - arm);
            float belly = bell(y, 1.07, 0.16), chest = smoothstep(1.2, 1.36, y), hips = bell(y, 0.95, 0.11), thigh = smoothstep(0.15, 0.75, y);
            float kx = 1 + w * (0.10 + 0.07 * belly) + m * 0.07 * chest + sh * 0.10 * smoothstep(1.26, 1.42, y) + hp * 0.13 * hips + bu * 0.035 * bell(y, 1.29, 0.08) + bm * 0.05 * hips;
            float kz = 1 + w * (0.16 + 0.16 * belly) + m * 0.05 * chest + hp * 0.10 * hips;
            float3 qt = float3(p.x * kx, p.y, p.z * kz);
            float kl = 1 + w * (0.08 + 0.10 * thigh) + m * (0.05 + 0.09 * thigh) + hp * 0.08 * smoothstep(0.6, 0.9, y), lx = side * 0.092;      // each leg about its own axis
            float3 ql = float3(lx + (p.x - lx) * kl + side * (hp * 0.012 + w * 0.010) * thigh, p.y, p.z * kl);
            float3 q = p + (qt - p) * torso + (ql - p) * leg;
            float t = saturate((1.44 - y) / 0.47), axx = side * lerp(0.172, 0.262, t), axz = -0.03;      // each arm about its own axis, carried out with the shoulders
            float ka = 1 + w * 0.10 + m * (0.22 - 0.08 * t), carry = 0.17 * (w * 0.10 + m * 0.07 + sh * 0.10);
            float3 qa = float3(axx + (p.x - axx) * ka + side * carry, p.y, axz + (p.z - axz) * ka);
            q = lerp(q, qa, arm);
            q.z += bu * 0.055 * bell(y, 1.295, 0.065) * bell(ax, 0.075, 0.075) * smoothstep(-0.01, 0.03, p.z) * (1 - arm) * (1 - head);     // bust: fuller at the front
            q.y -= max(bu, 0) * 0.012 * bell(y, 1.27, 0.05) * smoothstep(0.0, 0.04, p.z) * (1 - arm);                                     // and sitting a little lower when bigger
            q.z -= bm * 0.065 * bell(y, 0.90, 0.085) * bell(ax, 0.08, 0.075) * (1 - smoothstep(-0.02, 0.03, p.z)) * (1 - arm);           // bum: rounder at the back
            float3 neck = float3(0, 1.47, 0); q = lerp(q, neck + (q - neck) * (1 + hd * 0.08), head);
            float hipY = 0.93; q.y = q.y < hipY ? q.y * (1 + lg * 0.07) : q.y + hipY * lg * 0.07;         // longer or shorter legs
            return q;
        }
        float4 Shaped (float4 v) { if (_Shaped < 0.5) return v; return mul(_ActorToObj, float4(Reshape(mul(_ObjToActor, float4(v.xyz, 1)).xyz), 1)); }
        ENDCG
        Offset 0, [_ZPush]     // skin drawn only to fill gaps under clothes steps back, so a coarse garment lying close to it still covers it
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow finalcolor:solid vertex:vert
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _MakeupTex; half _Makeup;
        fixed4 _Tint, _Mean; half _Recolor, _Lift, _Gain, _SatMask, _Glossiness, _Keep, _Sheen, _Detail, _SSS, _Hair; float4 _HairLight; float _Inset, _Shrink, _DetailScale;
        struct Input { float2 uv_MainTex; float3 viewDir; float3 objPos; float3 actorPos; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        // smooth 3D noise, for surface detail on pieces that have no texture of their own
        float hash3 (float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
        float noise3 (float3 p) {
            float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        // skin worn under clothes is pulled in a little so it does not show through them
        // _Inset is in metres, whatever scale the model was imported at
        // Cloth has an inside and an outside face, so it cannot be pulled in along its normals: _Shrink draws it closer to the upright axis through the model's origin instead.
        void vert (inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.actorPos = mul(_ObjToActor, float4(v.vertex.xyz, 1)).xyz;
            v.vertex = Shaped(v.vertex);
            float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
            o.objPos = v.vertex.xyz * scale;
            v.vertex.xyz -= v.normal * (_Inset / scale);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz, piv = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            wp.xz = piv.xz + (wp.xz - piv.xz) * (1 - _Shrink); v.vertex = mul(unity_WorldToObject, float4(wp, 1));
        }
        // she is drawn into a transparent picture, so every pixel of her must be fully opaque
        void solid (Input IN, SurfaceOutputStandard o, inout fixed4 c) { c.a = 1; }
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            half3 W = half3(0.299, 0.587, 0.114); half lum = dot(c.rgb, W);
            half mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b));
            half sat = mx > 0.001 ? (mx - mn) / mx : 0;
            half3 re = _Tint.rgb * saturate(_Lift + lum * _Gain);
            // when dyeing, anything in the print that is not the garment's main colour (buttons, trims, a logo, the shirt under a jacket) keeps its own colour
            half3 hue = c.rgb / max(lum, 0.05), mainHue = _Mean.rgb / max(dot(_Mean.rgb, W), 0.05);
            half other = saturate((length(hue - mainHue) - 0.45) * 2.5) * _Keep;
            half k = _Recolor * (1 - other) * lerp(1, saturate(sat * 7 - 0.35), _SatMask);
            half3 albedo = lerp(c.rgb * lerp(_Tint.rgb, half3(1, 1, 1), _Recolor), re, k);
            fixed4 mk = tex2D(_MakeupTex, IN.uv_MainTex); albedo = lerp(albedo, mk.rgb, mk.a * _Makeup);      // makeup sits on top of the skin
            half3 nrm = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            if (_Detail > 0) {      // two sizes of fine bumps: coils and fibres on hair, weave on cloth
                float3 q = IN.objPos * _DetailScale; float e = 0.35;
                float n0 = noise3(q) * 0.65 + noise3(q * 2.7) * 0.35;
                float nx = noise3(q + float3(e, 0, 0)) * 0.65 + noise3((q + float3(e, 0, 0)) * 2.7) * 0.35, ny = noise3(q + float3(0, e, 0)) * 0.65 + noise3((q + float3(0, e, 0)) * 2.7) * 0.35;
                nrm = normalize(nrm + half3(n0 - nx, n0 - ny, 0) * (2.2 * _Detail));
                albedo *= 1 + (n0 - 0.5) * 0.5 * _Detail;
            }
            float3 hairGlow = 0;
            if (_Hair > 0) {
                // streaks: lighter and darker strands running down the hair
                float streak = noise3(IN.objPos * float3(520, 34, 520)) * 0.6 + noise3(IN.objPos * float3(1500, 60, 1500)) * 0.4;
                albedo *= lerp(1, 0.72 + 0.62 * streak, _Hair);
                // depth: hair deep inside the style, close to the scalp, sits in the shade of the hair around it
                if (_Shaped > 0.5) { float r = length((IN.actorPos - float3(0, 1.60, -0.035)) * float3(1, 0.85, 1)); albedo *= lerp(1, lerp(0.42, 1, saturate((r - 0.088) / 0.075)), _Hair); }
                // strand highlights: light catches hair in bands across the strands, not in round spots (the direction of the strands is taken as "down, along the surface")
                float3 N = normalize(WorldNormalVector(IN, nrm)), V = normalize(_WorldSpaceCameraPos - IN.worldPos), L = normalize(_HairLight.xyz + float3(0, 0.0001, 0));
                float3 T = normalize(cross(N, cross(float3(0.25, -1, 0.15), N)) + N * (streak - 0.5) * 0.7), H = normalize(L + V);
                float th = dot(T, H), band = sqrt(saturate(1 - th * th)), lit = saturate(dot(N, L) * 0.6 + 0.4);
                float3 T2 = normalize(T - N * 0.35); float th2 = dot(T2, H), band2 = sqrt(saturate(1 - th2 * th2));
                hairGlow = (pow(band, 110) * 0.30 + pow(band2, 22) * albedo * 1.6) * lit * _Hair;
            }
            o.Albedo = albedo; o.Normal = nrm;
            half rim = 1 - saturate(dot(normalize(IN.viewDir), nrm));
            o.Emission = albedo * pow(rim, 3) * _Sheen;     // the soft edge light of fabric and skin
            // skin is not solid: light that enters it comes back out redder, most where the surface turns away (ears, nostrils, the edge of a cheek) and in its shadows
            o.Emission += albedo * half3(1.0, 0.30, 0.20) * _SSS * (0.10 + 0.55 * rim * rim) + hairGlow;
            o.Metallic = 0; o.Smoothness = _Glossiness; o.Alpha = c.a;
        }
        ENDCG
        // The lit passes leave the picture's alpha untouched, so mark every pixel she covers as opaque here.
        Pass {
            ColorMask A ZWrite Off ZTest LEqual Cull [_Cull] Offset 0, [_ZPush]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed _Cutoff; float _Inset, _Shrink;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; v.vertex = Shaped(v.vertex); v.vertex.xyz -= v.normal * (_Inset / length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))));
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz, piv = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz; wp.xz = piv.xz + (wp.xz - piv.xz) * (1 - _Shrink);
                // placed exactly as the lit passes place it, or this pass loses the depth test in places
                v.vertex = mul(unity_WorldToObject, float4(wp, 1)); o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); return o; }
            fixed4 frag (v2f i) : SV_Target { clip(tex2D(_MainTex, i.uv).a - _Cutoff); return fixed4(0, 0, 0, 1); }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
