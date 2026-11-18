Shader "Evolution/CellMembrane"
{
    Properties { _InstanceColor("Color", Color) = (0.2,0.8,0.7,0.22) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
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
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(i.positionWS.x); clip(i.positionWS.z); clip(_WorldSize - i.positionWS.x); clip(_WorldSize - i.positionWS.z);
                half3 n = normalize(i.normalWS), eye = normalize(GetCameraPositionWS() - i.positionWS);
                half rim = pow(1 - saturate(dot(n, eye)), 2.5);
                half spec = pow(saturate(dot(n, normalize(eye + normalize(float3(.3,1,-.4))))), 48);
                half4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                return half4(color.rgb * (.6 + rim * .8) + spec * .6, saturate(color.a + rim * .55 + spec * .2));
            }
            ENDHLSL
        }
    }
}
