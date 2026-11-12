Shader "Evolution/VolumeBody"
{
    Properties { _WaterFade("Water visibility guard",Vector)=(20,36,0,0) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:128
            #include "VolumeLife.hlsl"
            ENDHLSL
        }
    }
}
