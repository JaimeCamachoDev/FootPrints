// FootprintsHelper.hlsl
// Helpers for Custom Function nodes in Shader Graph (URP).

void DarkenAlbedo_float(float3 baseAlbedo, float mask, float darkenFactor, out float3 outColor)
{
    float3 darkened = baseAlbedo * darkenFactor;
    outColor = lerp(baseAlbedo, darkened, saturate(mask));
}

void MultiplySmoothness_float(float smoothness, float mask, float mulUnderFoot, out float outSmoothness)
{
    float mul = lerp(1.0, mulUnderFoot, saturate(mask));
    outSmoothness = saturate(smoothness * mul);
}

float3 UnpackNormal(float4 n)
{
    float3 t = n.xyz * 2.0 - 1.0;
    t.z = sqrt(saturate(1.0 - dot(t.xy, t.xy)));
    return t;
}

void NormalStrength_float(float4 normalTex, float intensity, out float3 outNormalTS)
{
    float3 n = UnpackNormal(normalTex);
    n.xy *= intensity;
    n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
    outNormalTS = normalize(n);
}

void VertexOffsetOS_float(float3 normalOS, float mask, float heightStrength, out float3 offsetOS)
{
    offsetOS = normalOS * (mask * heightStrength);
}
