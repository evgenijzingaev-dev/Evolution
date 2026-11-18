#ifndef EVOLUTION_OCEAN_WATER
#define EVOLUTION_OCEAN_WATER
float _OceanTime;
float OceanNoise(float2 p) {
    float2 a = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    float4 h = frac(sin(float4(dot(a,float2(127.1,311.7)),dot(a+float2(1,0),float2(127.1,311.7)),
        dot(a+float2(0,1),float2(127.1,311.7)),dot(a+1,float2(127.1,311.7)))) * 43758.5453);
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
}
half3 OceanFog(half3 color, float3 positionWS) {
    float distanceInWater = length(positionWS - GetCameraPositionWS());
    // Modest scattering retains readable organisms even at the overview zoom.
    half mist = 1 - exp(-distanceInWater * .0045);
    return lerp(color, half3(.055,.22,.25), min(.6,mist));
}
#endif
