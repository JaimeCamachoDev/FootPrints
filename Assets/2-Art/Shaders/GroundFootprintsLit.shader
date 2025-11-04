Shader "Universal Render Pipeline/Footprints/Ground"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _FootMask ("Footprint Mask", 2D) = "black" {}
        _FootNormalMap ("Footprint Normal", 2D) = "bump" {}
        _FootNormalIntensity ("Normal Intensity", Range(0,2)) = 1
        _FootAlbedoDarken ("Albedo Multiplier", Range(0,1)) = 0.85
        _FootSmoothnessMul ("Smoothness Multiplier", Range(0,1)) = 0.6
        _Smoothness ("Base Smoothness", Range(0,1)) = 0.4
        _SpecularColor ("Specular Color", Color) = (0.2,0.2,0.2,1)
        _FootHeightStrength ("Height Offset", Range(0,0.05)) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Fog.hlsl"



            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _FootNormalIntensity;
                float _FootAlbedoDarken;
                float _FootSmoothnessMul;
                float _Smoothness;
                float4 _SpecularColor;
                float _FootHeightStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_FootMask);
            SAMPLER(sampler_FootMask);

            TEXTURE2D(_FootNormalMap);
            SAMPLER(sampler_FootNormalMap);

            float4 _FootTileOriginSize;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            float SampleMask(float3 positionWS)
            {
                float2 tileUV = (positionWS.xz - _FootTileOriginSize.xy) / _FootTileOriginSize.zw;
                return SAMPLE_TEXTURE2D(_FootMask, sampler_FootMask, tileUV).r;
            }

            float3 SampleFootNormal(float3 normalWS, float3 tangentWS, float3 bitangentWS, float2 uv, float mask)
            {
                if (mask <= 0.0001f)
                {
                    return normalize(normalWS);
                }

                float3 normalTS = SAMPLE_TEXTURE2D(_FootNormalMap, sampler_FootNormalMap, uv).xyz * 2.0f - 1.0f;
                normalTS.xy *= _FootNormalIntensity;

                float3x3 tbn = float3x3(normalize(tangentWS), normalize(bitangentWS), normalize(normalWS));
                float3 detailWS = mul(normalTS, tbn);
                return normalize(lerp(normalWS, detailWS, saturate(mask)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float mask = saturate(SampleMask(input.positionWS));

                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = baseSample.rgb * _BaseColor.rgb;
                float3 footprintAlbedo = lerp(albedo, albedo * _FootAlbedoDarken, mask);

                float3 normalWS = SampleFootNormal(input.normalWS, input.tangentWS, input.bitangentWS, input.uv, mask);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS + float3(0, -mask * _FootHeightStrength, 0));
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float3 diffuse = footprintAlbedo * mainLight.color * (NdotL * shadow);

                float smoothness = lerp(_Smoothness, _Smoothness * _FootSmoothnessMul, mask);
                float specPower = lerp(8.0f, 32.0f, smoothness);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), specPower);
                float3 specular = _SpecularColor.rgb * spec * shadow;

                float3 ambient = footprintAlbedo * SampleSH(normalWS);

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    float NdotLAdd = saturate(dot(normalWS, light.direction));
                    diffuse += footprintAlbedo * light.color * (NdotLAdd * light.distanceAttenuation);

                    float3 halfDirAdd = normalize(light.direction + viewDir);
                    float specAdd = pow(saturate(dot(normalWS, halfDirAdd)), specPower);
                    specular += _SpecularColor.rgb * specAdd * light.distanceAttenuation;
                }
                #endif

                float fogFactor = ComputeFogFactor(input.positionCS.z);
                float3 color = ambient + diffuse + specular;
                color = MixFog(color, fogFactor);

                return half4(color, 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
