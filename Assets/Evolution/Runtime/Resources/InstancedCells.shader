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
            #include "OceanWater.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); return output;
            }
            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_WorldSize > 0) {
                    clip(input.positionWS.x); clip(input.positionWS.z);
                    clip(_WorldSize - input.positionWS.x); clip(_WorldSize - input.positionWS.z);
                }
                half3 n = normalize(input.normalWS);
                half diffuse = saturate(dot(n, normalize(float3(.3,1,-.4))));
                half lighting = .32 + .55 * smoothstep(.05, .75, diffuse);
                half3 eye = normalize(GetCameraPositionWS() - input.positionWS);
                half rim = pow(1 - saturate(dot(n, eye)), 3) * .22;
                half shine = pow(saturate(dot(n, normalize(eye + normalize(float3(.3,1,-.4))))), 28) * .25;
                return half4(OceanFog(UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor).rgb * (lighting + rim) + shine, input.positionWS), 1);
            }
            ENDHLSL
        }
    }
}
