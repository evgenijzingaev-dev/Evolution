Shader "Evolution/VolumeSuspension"
{
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
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float blur:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
            Varyings Vert(Attributes i) {
                Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
                float3 center=TransformObjectToWorld(float3(0,0,0));float d=distance(center,GetCameraPositionWS());
                float size=length(TransformObjectToWorldDir(float3(1,0,0),false));
                o.blur=1+4*(1-smoothstep(.3,2.8,d));
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20,up=UNITY_MATRIX_I_V._m01_m11_m21;
                o.world=center+(right*i.positionOS.x+up*i.positionOS.y)*size*o.blur;
                o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);float4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
                float r=length(i.uv-.5)*2;float alpha=exp(-r*r*5)*(1-smoothstep(.65,1,r));
                float nearFade=smoothstep(.12,.45,distance(i.world,GetCameraPositionWS()));
                return half4(VolumeFog(c.rgb,i.world),c.a*alpha*nearFade/i.blur);
            }
            ENDHLSL
        }
    }
}
