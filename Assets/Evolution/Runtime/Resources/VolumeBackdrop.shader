Shader "Evolution/VolumeBackdrop"
{
    Properties { _WaterFade("Water visibility guard",Vector)=(20,36,0,0) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass {
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VolumeWater.hlsl"
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            Varyings Vert(float4 p:POSITION) { Varyings o;o.world=TransformObjectToWorld(p.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
            half4 Frag(Varyings i):SV_Target {
                float3 ray=normalize(i.world-GetCameraPositionWS());
                return half4(VolumeEnvironment(ray),1);
            }
            ENDHLSL
        }
    }
}
