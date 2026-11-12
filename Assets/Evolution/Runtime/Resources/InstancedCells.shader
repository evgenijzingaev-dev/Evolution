Shader "Evolution/InstancedCells"
{
    Properties { _InstanceColor("Color", Color) = (0.2,0.8,0.5,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes input) {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); return output;
            }
            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                half lighting = .35 + .65 * saturate(dot(normalize(input.normalWS), normalize(float3(.3,1,-.4))));
                return half4(UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor).rgb * lighting, 1);
            }
            ENDHLSL
        }
    }
}
