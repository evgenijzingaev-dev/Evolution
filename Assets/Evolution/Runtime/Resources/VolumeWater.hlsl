#ifndef EVOLUTION_VOLUME_WATER
#define EVOLUTION_VOLUME_WATER
TEXTURE2D(_WaterCaustics);SAMPLER(sampler_WaterCaustics);
float _WaterTime;float4 _WaterFade;
float WaterCookie(float3 world) {
    float2 p=(world.xz+world.y*float2(.28,-.16))*.027;
    return SAMPLE_TEXTURE2D(_WaterCaustics,sampler_WaterCaustics,p+float2(_WaterTime*.002,-_WaterTime*.0013)).r;
}
float3 VolumeHaze(float3 direction) {
    float height=saturate(.5+GetCameraPositionWS().y*.018);
    float sun=pow(saturate(dot(direction,normalize(float3(-.28,.88,.36)))*.5+.5),8);
    return lerp(float3(.012,.065,.15),float3(.025,.25,.39),height)+sun*float3(.025,.13,.18);
}
float3 VolumeEnvironment(float3 direction) {
    float3 eye=GetCameraPositionWS();float shafts=0;
    [unroll]for(int step=0;step<4;step++) {
        float distance=5+step*8;
        shafts+=WaterCookie(eye+direction*distance)*exp(-distance*.032)*.25;
    }
    float directionality=pow(saturate(dot(direction,normalize(float3(-.28,.88,.36)))*.5+.5),3);
    return VolumeHaze(direction)+shafts*directionality*float3(.055,.17,.22);
}
float VolumeGuard(float distance) {return 1-smoothstep(_WaterFade.x,_WaterFade.y,distance);}
float3 VolumeFog(float3 color,float3 world) {
    float3 delta=world-GetCameraPositionWS(); float distance=length(delta);
    float scatter=exp(-distance*.022)*VolumeGuard(distance);
    float3 absorption=exp(-distance*float3(.018,.006,.002));
    return color*absorption*scatter+VolumeEnvironment(normalize(delta))*(1-scatter);
}
#endif
