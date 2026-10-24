// NeonatalDelivery/Veil - frosted privacy veil (censorship levels 2-3). Blurs only maternal pixels (stencil == 1),
// so the newborn's head, the hands and the instruments stay sharp. Requires the camera opaque texture.
Shader "NeonatalDelivery/Veil"
{
    Properties { _Tint ("Tint", Color) = (0.55,0.75,0.8,1) _VeilOpacity ("Opacity", Range(0,1)) = 0.6 _Blur ("Blur", Range(0,0.03)) = 0.012 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Veil"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
            Stencil { Ref 1 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Tint; half _VeilOpacity; half _Blur; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS); half3 c = 0;
                [unroll] for (int k = 0; k < 8; k++) { float a = k * 0.785398; c += SampleSceneColor(uv + float2(cos(a), sin(a)) * _Blur); }
                c = c / 8.0;
                half lum = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(c, lum * _Tint.rgb, 0.6);
                float2 d = i.uv - 0.5; half edge = saturate(1 - dot(d, d) * 3.5);
                return half4(c, _VeilOpacity * edge);
            }
            ENDHLSL
        }
    }
}
