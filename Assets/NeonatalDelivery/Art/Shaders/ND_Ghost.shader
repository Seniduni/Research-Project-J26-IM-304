// NeonatalDelivery/Ghost - translucent fresnel-rim unlit used for ghost demonstration hands, landmark cues and markers.
Shader "NeonatalDelivery/Ghost"
{
    Properties { _BaseColor ("Colour", Color) = (0.3,0.85,1,0.45) _Rim ("Rim", Range(0,4)) = 1.6 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Ghost"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; half _Rim; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half3 n : TEXCOORD0; float3 pw : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o = (V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pw = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.pw); o.n = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 vd = normalize(GetWorldSpaceViewDir(i.pw));
                half f = pow(1 - saturate(abs(dot(normalize(i.n), vd))), 2.0) * _Rim;
                return half4(_BaseColor.rgb * (0.7 + f), saturate(_BaseColor.a * (0.55 + f)));
            }
            ENDHLSL
        }
    }
}
