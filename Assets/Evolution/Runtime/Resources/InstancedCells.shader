Shader "Evolution/InstancedCells"
{
    Properties {
        _InstanceColor("Color", Color) = (0.2,0.8,0.5,1)
        _MicroTex("Cytoplasm", 2D) = "gray" {}
        _MicroNormal("Microrelief", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:256
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CellSurface.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale()); o.uv = input.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 structure = SAMPLE_TEXTURE2D(_MicroTex,sampler_MicroTex,i.uv * float2(3,2)).rgb;
                half3 n = CellNormal(i.uv,i.normalWS,i.tangentWS);
                half3 albedo = UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor).rgb * (.7 + structure.r * .55);
                half4 lit = CellLighting(i.positionWS,n,albedo,.55 + structure.g * .25,.04);
                return half4(OceanFog(lit.rgb,i.positionWS),1);
            }
            ENDHLSL
        }
    }
}
