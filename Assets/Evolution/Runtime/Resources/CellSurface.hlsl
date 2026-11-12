#ifndef EVOLUTION_CELL_SURFACE
#define EVOLUTION_CELL_SURFACE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "OceanWater.hlsl"
TEXTURE2D(_MicroTex); SAMPLER(sampler_MicroTex);
TEXTURE2D(_MicroNormal); SAMPLER(sampler_MicroNormal);
half3 CellNormal(float2 uv, half3 normalWS, half4 tangentWS) {
    half3 micro = SAMPLE_TEXTURE2D(_MicroNormal, sampler_MicroNormal, uv * float2(3,2)).xyz * 2 - 1;
    half3 n = normalize(normalWS), t = normalize(tangentWS.xyz), b = normalize(cross(n,t)) * tangentWS.w;
    return normalize(n * micro.z + (t * micro.x + b * micro.y) * .28);
}
half4 CellLighting(float3 positionWS, half3 normalWS, half3 albedo, half smoothness, half emission) {
    InputData lighting = (InputData)0;
    lighting.positionWS = positionWS; lighting.normalWS = normalWS;
    lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    lighting.shadowCoord = TransformWorldToShadowCoord(positionWS);
    lighting.bakedGI = SampleSH(normalWS);
    lighting.shadowMask = half4(1,1,1,1);
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo; surface.metallic = 0; surface.specular = half3(.04,.04,.04);
    surface.smoothness = smoothness; surface.occlusion = 1; surface.alpha = 1;
    surface.normalTS = half3(0,0,1); surface.emission = albedo * emission;
    return UniversalFragmentPBR(lighting,surface);
}
#endif
