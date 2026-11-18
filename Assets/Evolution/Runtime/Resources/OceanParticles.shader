Shader "Evolution/OceanParticles"
{
    Properties { _InstanceColor("Color", Color) = (0.3,0.8,0.7,0.3) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o;
            }
            half4 Frag(Varyings i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); return UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor); }
            ENDHLSL
        }
    }
}
