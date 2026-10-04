#ifndef UKCITY_COMMON_INCLUDED
#define UKCITY_COMMON_INCLUDED

// Set from C# via Shader.SetGlobal* (see GameManager).
float4 _UKFogColor;
float4 _UKFogParams;   // x = start distance, y = end distance, z = strength (0 disables)
float _UKDaylight;     // 1 = full daylight

float UKFogFactor(float3 worldPos)
{
    float d = distance(worldPos, _WorldSpaceCameraPos);
    float f = saturate((d - _UKFogParams.x) / max(0.001, _UKFogParams.y - _UKFogParams.x));
    return f * _UKFogParams.z;
}

#endif
