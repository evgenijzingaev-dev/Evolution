#ifndef EVOLUTION_VOLUME_WATER
#define EVOLUTION_VOLUME_WATER
TEXTURE2D(_WaterCaustics);SAMPLER(sampler_WaterCaustics);
TEXTURE2D(_WaterDensity);SAMPLER(sampler_WaterDensity);
float _WaterTime;float4 _WaterFade;
float WaterCookie(float3 world) {
    float2 p=(world.xz+world.y*float2(.28,-.16))*.027;
    return SAMPLE_TEXTURE2D(_WaterCaustics,sampler_WaterCaustics,p+float2(_WaterTime*.002,-_WaterTime*.0013)).r;
}
// Continuous world-space samples: no camera-facing fog planes or per-chunk light tiles.
float WaterDensity(float3 p) {
    float2 drift=float2(_WaterTime*.0018,-_WaterTime*.0011);
    float a=SAMPLE_TEXTURE2D(_WaterDensity,sampler_WaterDensity,(p.xz+p.y*float2(.31,-.19))*.016+drift).r;
    float b=SAMPLE_TEXTURE2D(_WaterDensity,sampler_WaterDensity,(p.xy+p.z*float2(-.23,.37))*.011-drift*.7).b;
    return .12+.60*smoothstep(.28,.78,a*.6+b*.4);
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
    // Eight broad scattering depths, also used by object fog so silhouettes dissolve
    // into exactly the same environment at the existing streaming visibility guard.
    [unroll]for(int step=0;step<8;step++) {
        float travel=3+step*5;
        float3 p=eye+direction*travel;
        float density=WaterDensity(p);
        // Project along the actual sun direction. Irregular light wells, not enlarged caustic stripes.
        float2 column=float2(p.x+p.y*.318,p.z-p.y*.409)*.038;
        float mask=SAMPLE_TEXTURE2D(_WaterDensity,sampler_WaterDensity,column+float2(_WaterTime*.0008,0)).g;
        float illumination=smoothstep(.40,.60,mask);
        float weight=exp(-travel*.026)*.24;
        scatter+=(float3(.02,.065,.10)+pow(illumination,3)*float3(.48,1.10,1.18))*density*weight;
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
