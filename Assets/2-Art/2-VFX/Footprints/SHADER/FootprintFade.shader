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

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float _Fade;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float current = tex2D(_MainTex, uv).r;
                float faded = saturate(current * (1.0f - saturate(_Fade)));
                return float4(faded, faded, faded, 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
