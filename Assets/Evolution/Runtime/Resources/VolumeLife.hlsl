#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "VolumeWater.hlsl"
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
    float rim=pow(1-saturate(dot(n,eye)),3);
    float3 pigment=color.rgb;
    #ifdef VOLUME_SHELL
    float bands=smoothstep(.65,.9,.5+.5*sin(i.uv.y*44+sin(i.uv.x*25)+genes.w*5));
    pigment=lerp(pigment,float3(.16,.4,.22),bands*genes.x*.4);
    float seams=smoothstep(.94,.99,abs(sin(i.uv.x*37.7)))*genes.z;
    pigment=lerp(pigment,float3(.67,.62,.4),seams*.4);
    color.a*=.8+rim*.5;
    #endif
    float specular=pow(saturate(dot(reflect(-light.direction,n),eye)),38)*.14;
    float3 lit=pigment*(diffuse*light.color*.65+float3(.22,.3,.32))+specular+pigment*rim*.08;
    return half4(VolumeFog(lit,i.world),color.a);
}
