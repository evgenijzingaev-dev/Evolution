Shader "Evolution/VolumeSuspension"
{
    Properties { _WaterFade("Water visibility guard",Vector)=(20,36,0,0) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:256
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VolumeWater.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4,_InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Phenotype)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float blur:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
            Varyings Vert(Attributes i) {
                Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
                float3 center=TransformObjectToWorld(float3(0,0,0));float d=distance(center,GetCameraPositionWS());
                float3 axis=TransformObjectToWorldDir(float3(1,0,0),false);
                float size=length(axis);float kind=UNITY_ACCESS_INSTANCED_PROP(Props,_Phenotype).x;
                o.blur=1+4*(1-smoothstep(.3,2.8,d));
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20,up=UNITY_MATRIX_I_V._m01_m11_m21;
                if(kind>2.5&&kind<3.5) {
                    float width=length(TransformObjectToWorldDir(float3(0,1,0),false));
                    float3 across=normalize(cross(normalize(GetCameraPositionWS()-center),normalize(axis))+float3(.0001,0,0));
                    o.world=center+axis*i.positionOS.x+across*i.positionOS.y*width;
                } else o.world=center+(right*i.positionOS.x+up*i.positionOS.y)*size*o.blur;
                o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);float4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
                float kind=UNITY_ACCESS_INSTANCED_PROP(Props,_Phenotype).x;
                float r=length(i.uv-.5)*2;float alpha=exp(-r*r*5)*(1-smoothstep(.65,1,r));
                if(kind>.5&&kind<1.5) {float2 q=(i.uv-.5)*float2(1.2,2.8);float e=dot(q,q);alpha=exp(-e*4)*(1-smoothstep(.5,1,e));}
                if(kind>2.5&&kind<3.5) {float2 q=i.uv-.5;alpha=exp(-q.y*q.y*35)*smoothstep(-.5,-.1,q.x)*(1-smoothstep(.3,.5,q.x));alpha*=.3+.7*i.uv.x;}
                float nearFade=smoothstep(.12,.45,distance(i.world,GetCameraPositionWS()));
                return half4(VolumeFog(c.rgb,i.world),c.a*alpha*nearFade*VolumeGuard(distance(i.world,GetCameraPositionWS()))/sqrt(i.blur));
            }
            ENDHLSL
        }
    }
}
