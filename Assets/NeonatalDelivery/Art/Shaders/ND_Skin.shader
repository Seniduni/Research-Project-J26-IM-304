// NeonatalDelivery/Skin - URP 17 (Unity 6) forward-lit skin for the mother, newborn, birth canal and umbilical cord.
// * wrap-lighting subsurface approximation (cheap, Quest-friendly)
// * newborn layers: wetness (amniotic fluid), vernix caseosa, blood streaks, cyanosis -> pink transition
// * censorship: gore desaturation + stylisation (FR8)
// * clip cone at the introitus: hides the parts of the newborn still inside the mother (no poke-through)
// * stencil write (mother = 1) so the privacy veil only blurs maternal skin
// Single-pass instanced stereo ready.
Shader "NeonatalDelivery/Skin"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _MetalRoughMap ("glTF Metal(B) Rough(G)", 2D) = "white" {}
        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 0
        [Toggle] _UseMetalRough ("Use Metal/Rough map", Float) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.42
        _Metallic ("Metallic", Range(0,1)) = 0
        _SSSColor ("Subsurface Colour", Color) = (0.85,0.35,0.28,1)
        _SSSWrap ("Subsurface Wrap", Range(0,1)) = 0.45
        _SSSStrength ("Subsurface Strength", Range(0,1)) = 0.55
        _Wetness ("Wetness", Range(0,1)) = 0
        _Vernix ("Vernix", Range(0,1)) = 0
        _Blood ("Blood", Range(0,1)) = 0
        _Cyanosis ("Cyanosis", Range(0,1)) = 0
        _CensorGore ("Censor Gore (desaturate)", Range(0,1)) = 0
        _Stylize ("Stylise (minimal realism)", Range(0,1)) = 0
        _ClipEnabled ("Clip Enabled", Float) = 0
        _ClipOrigin ("Clip Origin (world)", Vector) = (0,0,0,0)
        _ClipNormal ("Clip Outward Normal", Vector) = (0,0,1,0)
        _ClipRadius ("Clip Radius", Float) = 0.05
        _ClipSlope ("Clip Cone Slope", Float) = 1.2
        [IntRange] _StencilRef ("Stencil Ref (mother=1)", Range(0,255)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half _BumpScale; half _UseMetalRough; half _Smoothness; half _Metallic; half _OcclusionStrength;
            half4 _SSSColor; half _SSSWrap; half _SSSStrength; half _Wetness; half _Vernix; half _Blood; half _Cyanosis;
            half _CensorGore; half _Stylize; float _ClipEnabled; float4 _ClipOrigin; float4 _ClipNormal; float _ClipRadius; float _ClipSlope; float _StencilRef; float _Cull;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetalRoughMap); SAMPLER(sampler_MetalRoughMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);

        // Discard newborn fragments that are still inside the mother: behind the introitus plane, or just in front of it but
        // outside a cone growing from the ring (prevents limbs poking through maternal skin near the perineum).
        void ClipCone(float3 positionWS)
        {
            if (_ClipEnabled < 0.5) return;
            float3 n = normalize(_ClipNormal.xyz);
            float3 d = positionWS - _ClipOrigin.xyz;
            float h = dot(d, n);
            float lateral = length(d - n * h);
            if (h < -0.002) clip(-1);
            if (h < 0.08 && lateral > _ClipRadius + max(h, 0) * _ClipSlope) clip(-1);
        }
        float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
        float Noise(float2 p) { float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
            return lerp(lerp(Hash21(i), Hash21(i+float2(1,0)), f.x), lerp(Hash21(i+float2(0,1)), Hash21(i+float2(1,1)), f.x), f.y); }
        float Fbm(float2 p) { float a = 0.5, s = 0; for (int k = 0; k < 4; k++) { s += a * Noise(p); p *= 2.03; a *= 0.5; } return s; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3; half fog : TEXCOORD4; float3 positionOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.fog = ComputeFogFactor(p.positionCS.z); o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                ClipCone(i.positionWS);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                albedo.rgb *= lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).r, _OcclusionStrength);
                half smooth = _Smoothness, metal = _Metallic;
                if (_UseMetalRough > 0.5) { half4 mr = SAMPLE_TEXTURE2D(_MetalRoughMap, sampler_MetalRoughMap, i.uv); smooth = 1 - mr.g; metal = mr.b * _Metallic; }
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);

                // ---- newborn surface layers (object-space noise so they stick to the skin)
                float2 q = i.positionOS.xy * 38.0 + i.positionOS.z * 21.0;
                float vern = saturate((Fbm(q * 0.9) - (1.0 - _Vernix * 0.85)) * 3.0) * _Vernix;
                float streak = saturate((Fbm(float2(q.x * 0.35, q.y * 2.6)) - 0.62) * 4.0) * _Blood;
                float3 cyan = lerp(albedo.rgb, albedo.rgb * half3(0.62, 0.68, 1.05) + half3(-0.03, 0.0, 0.05), 0.85);
                albedo.rgb = lerp(albedo.rgb, cyan, _Cyanosis * 0.75);
                albedo.rgb = lerp(albedo.rgb, half3(0.93, 0.90, 0.80), vern * 0.85);
                half3 bloodCol = lerp(half3(0.42, 0.03, 0.04), half3(0.35, 0.33, 0.34), _CensorGore);
                albedo.rgb = lerp(albedo.rgb, bloodCol, streak * (1 - _CensorGore * 0.7));
                smooth = lerp(smooth, 0.88, _Wetness * (1 - vern));
                albedo.rgb *= lerp(1.0, 0.86, _Wetness * 0.6);
                // ---- censorship stylisation (desaturate + flatten) for minimal-realism level
                half lum = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                albedo.rgb = lerp(albedo.rgb, lum.xxx * half3(1.0, 0.96, 0.93), _Stylize);
                nTS = lerp(nTS, half3(0, 0, 1), _Stylize);

                float sgn = i.tangentWS.w; float3 bit = sgn * cross(i.normalWS.xyz, i.tangentWS.xyz);
                half3 nWS = NormalizeNormalPerPixel(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bit, i.normalWS.xyz)));

                InputData id = (InputData)0;
                id.positionWS = i.positionWS; id.normalWS = nWS; id.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                id.shadowCoord = TransformWorldToShadowCoord(i.positionWS); id.fogCoord = i.fog; id.bakedGI = SampleSH(nWS);
                id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); id.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb; s.metallic = metal; s.smoothness = smooth; s.normalTS = nTS; s.occlusion = 1; s.alpha = 1; s.specular = half3(0, 0, 0);
                half4 col = UniversalFragmentPBR(id, s);

                // wrap-lighting subsurface term (main light)
                Light L = GetMainLight(id.shadowCoord);
                half ndl = dot(nWS, L.direction);
                half wrap = saturate((ndl + _SSSWrap) / (1 + _SSSWrap)) - saturate(ndl);
                col.rgb += albedo.rgb * _SSSColor.rgb * wrap * L.color * L.shadowAttenuation * _SSSStrength * (1 - _Stylize);
                col.rgb = MixFog(col.rgb, i.fog);
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A v)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz); float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pw);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs; o.positionWS = pw; return o;
            }
            half4 frag(V i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); ClipCone(i.positionWS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }
            half frag(V i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); ClipCone(i.positionWS); return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
