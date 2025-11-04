Shader "Universal Render Pipeline/Footprints/InstancedDecal"
{
    Properties
    {
        _Diffuse ("Diffuse", 2D) = "white" {}
        _AlphaTex ("Alpha", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
                float _GlobalAlpha;
            CBUFFER_END

            TEXTURE2D(_Diffuse);
            SAMPLER(sampler_Diffuse);

            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UvRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0));
                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.uv = input.uv;

                return output;
            }

            float2 SampleAtlasUV(float2 uv)
            {
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UvRect);
                return rect.xy + uv * rect.zw;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 atlasUV = SampleAtlasUV(input.uv);
                half4 diffuse = SAMPLE_TEXTURE2D(_Diffuse, sampler_Diffuse, atlasUV);
                half alphaTex = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, atlasUV).r;

                float4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                half alpha = alphaTex * diffuse.a * instanceColor.a * _GlobalAlpha;

                clip(alpha - _Cutoff);

                half3 color = diffuse.rgb * instanceColor.rgb;
                color = MixFog(color, ComputeFogFactor(input.positionCS.z));

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
