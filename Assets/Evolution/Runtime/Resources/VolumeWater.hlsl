#ifndef EVOLUTION_VOLUME_WATER
#define EVOLUTION_VOLUME_WATER
TEXTURE2D(_WaterCaustics);SAMPLER(sampler_WaterCaustics);
float _WaterTime;float4 _WaterFade;
float WaterCookie(float3 world) {
    float2 p=(world.xz+world.y*float2(.28,-.16))*.027;
    return SAMPLE_TEXTURE2D(_WaterCaustics,sampler_WaterCaustics,p+float2(_WaterTime*.002,-_WaterTime*.0013)).r;
}
// Continuous world-space samples: no camera-facing fog planes or per-chunk light tiles.
float WaterDensity(float3 p) {
    float2 drift=float2(_WaterTime*.0018,-_WaterTime*.0011);
    float a=SAMPLE_TEXTURE2D(_WaterCaustics,sampler_WaterCaustics,(p.xz+p.y*float2(.31,-.19))*.012+drift).r;
    float b=SAMPLE_TEXTURE2D(_WaterCaustics,sampler_WaterCaustics,(p.xy+p.z*float2(-.23,.37))*.019-drift*.7).r;
    return .18+.82*smoothstep(.025,.5,a*.65+b*.55);
}
float3 VolumeHaze(float3 direction) {
    float height=saturate(.5+GetCameraPositionWS().y*.018);
    float upper=smoothstep(-.8,.7,direction.y);
    float sun=pow(saturate(dot(direction,normalize(float3(-.28,.88,.36)))*.5+.5),5);
    float3 deep=lerp(float3(.018,.055,.115),float3(.025,.13,.22),height);
    return deep+upper*float3(.035,.17,.22)+sun*float3(.10,.25,.28);
}
float3 VolumeEnvironment(float3 direction) {
    float3 eye=GetCameraPositionWS();float3 scatter=0;
    // Three broad scattering depths, also used by object fog so silhouettes dissolve
    // into exactly the same environment at the existing streaming visibility guard.
    [unroll]for(int step=0;step<3;step++) {
        float travel=7+step*12;
        float3 p=eye+direction*travel;
        float density=WaterDensity(p);
        float illumination=.3+.7*WaterCookie(p+float3(0,_WaterTime*.04,0));
        float weight=exp(-travel*.026)*.55;
        scatter+=lerp(float3(.025,.105,.16),float3(.22,.62,.68),illumination)*density*weight;
    }
    return VolumeHaze(direction)+scatter;
}
float VolumeGuard(float distance) {return 1-smoothstep(_WaterFade.x,_WaterFade.y,distance);}
float3 VolumeFog(float3 color,float3 world) {
    float3 delta=world-GetCameraPositionWS(); float distance=length(delta);
    float scatter=exp(-distance*.014)*VolumeGuard(distance);
    float3 absorption=exp(-distance*float3(.014,.004,.0015));
    return color*absorption*scatter+VolumeEnvironment(normalize(delta))*(1-scatter);
}
#endif
