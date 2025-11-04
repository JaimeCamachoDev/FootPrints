Shader "Hidden/Footprints/Fade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Fade ("Fade", Range(0,1)) = 0.1
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
            Name "Fade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _Fade;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
                float faded = saturate(current * (1.0f - saturate(_Fade)));
                return float4(faded, faded, faded, 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
