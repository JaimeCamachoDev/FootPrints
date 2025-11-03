Shader "Hidden/Footprints/Stamp"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _StampTex ("Stamp", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero

        Pass
        {
            Name "Stamp"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_StampTex);
            SAMPLER(sampler_StampTex);

            float4 _FootTileOriginSize;
            float4 _StampCenterScale;
            float4 _StampRotationStrength; // xy = cos/sin, z = strength

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;

                float2 worldPos = _FootTileOriginSize.xy + uv * _FootTileOriginSize.zw;
                float2 delta = worldPos - _StampCenterScale.xy;

                float2 rotated;
                rotated.x = delta.x * _StampRotationStrength.x - delta.y * _StampRotationStrength.y;
                rotated.y = delta.x * _StampRotationStrength.y + delta.y * _StampRotationStrength.x;

                float2 stampUV = rotated / _StampCenterScale.zw + 0.5f;

                float stamp = 0.0f;
                if (all(stampUV >= 0.0f) && all(stampUV <= 1.0f))
                {
                    stamp = SAMPLE_TEXTURE2D(_StampTex, sampler_StampTex, stampUV).r;
                }

                float result = saturate(current + stamp * saturate(_StampRotationStrength.z));
                return float4(result, result, result, 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
