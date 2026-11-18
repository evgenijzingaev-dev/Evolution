Shader "Evolution/VolumeMembrane"
{
    Properties { _WaterFade("Water visibility guard",Vector)=(20,36,0,0) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:128
            #define VOLUME_SHELL 1
            #include "VolumeLife.hlsl"
            ENDHLSL
        }
    }
}
