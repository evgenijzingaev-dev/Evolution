#ifndef EVOLUTION_VOLUME_WATER
#define EVOLUTION_VOLUME_WATER
float3 VolumeHaze(float3 direction) {
    float height=saturate(.5+GetCameraPositionWS().y*.018);
    float sun=pow(saturate(dot(direction,normalize(float3(-.3,.8,.4)))*.5+.5),4);
    return lerp(float3(.025,.065,.11),float3(.09,.21,.24),height)+sun*float3(.025,.045,.038);
}
float3 VolumeFog(float3 color,float3 world) {
    float3 delta=world-GetCameraPositionWS(); float distance=length(delta);
    float scatter=exp(-distance*.055)*(1-smoothstep(28,36,distance));
    float3 absorption=exp(-distance*float3(.045,.018,.009));
    return color*absorption*scatter+VolumeHaze(normalize(delta))*(1-scatter);
}
#endif
