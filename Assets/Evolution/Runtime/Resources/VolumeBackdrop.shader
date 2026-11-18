Shader "Evolution/VolumeBackdrop"
{
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
            half4 Frag(Varyings i):SV_Target {return half4(VolumeHaze(normalize(i.world-GetCameraPositionWS())),1);}
            ENDHLSL
        }
    }
}
