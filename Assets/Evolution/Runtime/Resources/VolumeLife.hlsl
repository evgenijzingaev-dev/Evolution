#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "VolumeWater.hlsl"
#ifdef VOLUME_SHELL
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#endif
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4,_InstanceColor)
    UNITY_DEFINE_INSTANCED_PROP(float4,_Phenotype)
UNITY_INSTANCING_BUFFER_END(Props)
struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
Varyings Vert(Attributes i) {
    Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
    o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
    o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;
}
half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);half4 color=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
    float4 genes=UNITY_ACCESS_INSTANCED_PROP(Props,_Phenotype);
    float3 n=normalize(i.normal),eye=normalize(GetCameraPositionWS()-i.world);
    Light light=GetMainLight();float diffuse=saturate(dot(n,light.direction))*.55+.45;
    float rim=pow(1-saturate(dot(n,eye)),2.4);
    float cookie=WaterCookie(i.world);
    float highlight=pow(saturate(dot(reflect(-light.direction,n),eye)),72);
    float u=i.uv.x*6.2831853,v=i.uv.y*3.1415927;
    float3 pigment=color.rgb;
    #ifdef VOLUME_SHELL
        // Diagnostic probes retain their real blue/red values; the beauty material is not a data remap.
        if(genes.w<0)return half4(VolumeFog(color.rgb,i.world),color.a);
        float lattice=abs(sin(u*8+sin(v*10+genes.w*5))*sin(v*14+sin(u*6)));
        float pores=smoothstep(.91,.98,lattice)*sin(v);
        float veins=pow(1-abs(sin(u*4+sin(v*6)*2)),14)*sin(v);
        float3 reflection=VolumeHaze(reflect(-eye,n))*1.8+float3(.42,.68,.95)*(rim*.8+pores*.15+veins*.1);
        reflection+=highlight*light.color*1.8+cookie*rim*float3(.25,.6,.75);
        float2 screen=GetNormalizedScreenSpaceUV(i.positionCS);
        float2 bend=TransformWorldToViewDir(n).xy*.002*(1-rim);
        float3 through=SampleSceneColor(clamp(screen+bend,.001,.999))*lerp(float3(1,1,1),pigment,.07);
        float3 pearlescence=lerp(float3(.58,.8,1),float3(.92,.72,.96),.5+.5*sin(i.uv.y*9+genes.w*6));
        float3 surface=reflection+pearlescence*(rim*.25+veins*.025);
        float distance=length(i.world-GetCameraPositionWS());
        float alpha=(color.a*(.48+rim*.65)+rim*.34+pores*.06+veins*.045)*VolumeGuard(distance);
        return half4(lerp(through,VolumeFog(surface,i.world),.28+rim*.55),saturate(alpha));
    #else
        // Bright hydrated internal tissue, rather than uniformly opaque olive cytoplasm.
        float grains=.5+.5*sin(u*7+sin(v*6))*sin(v*12+genes.w*5);
        float3 lit=pigment*(.55+diffuse*.75)*light.color*(.9+grains*.18);
        lit+=highlight*float3(.75,.9,1)*.75+cookie*pigment*.2;
        return half4(VolumeFog(lit,i.world),color.a);
    #endif
}
