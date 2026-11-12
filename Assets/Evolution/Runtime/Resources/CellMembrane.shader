Shader "Evolution/CellMembrane"
{
    Properties {
        _InstanceColor("Color", Color) = (0.2,0.8,0.7,0.22)
        _MicroTex("Membrane structure", 2D) = "gray" {}
        _MicroNormal("Microrelief", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "CellSurface.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Shape)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
                float4 shape = UNITY_ACCESS_INSTANCED_PROP(Props,_Shape); float3 p = input.positionOS.xyz;
                float angle = atan2(p.z,p.x);
                float wave = sin(_OceanTime * 2.7 + angle * 3 + shape.z) * .012;
                float lobes = 1 + (shape.x * cos(angle * shape.y + shape.z) + wave * saturate(shape.y)) * (1 - abs(input.normalOS.y));
                p.xz *= lobes; p.yz *= 1 - shape.w * .25 * exp(-p.x * p.x * 80);
                o.positionWS = TransformObjectToWorld(p); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w * GetOddNegativeScale());
                o.uv = input.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(i.positionWS.x); clip(i.positionWS.z); clip(_WorldSize - i.positionWS.x); clip(_WorldSize - i.positionWS.z);
                half4 color = UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
                half3 n = CellNormal(i.uv,i.normalWS,i.tangentWS);
                half3 eye = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1 - saturate(dot(n,eye)),2.5);
                half structure = SAMPLE_TEXTURE2D(_MicroTex,sampler_MicroTex,i.uv * float2(3,2)).r;
                half3 albedo = color.rgb * (.7 + structure * .4);
                half4 lit = CellLighting(i.positionWS,n,albedo,.88 - structure * .12,rim * .16);
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 bend = TransformWorldToViewDir(n).xy * .006 * (1 - rim);
                half3 transmitted = SampleSceneColor(clamp(uv + bend,.001,.999));
                half3 result = lerp(lit.rgb, transmitted * lerp(half3(1,1,1),color.rgb,.15),.32 * (1 - rim));
                return half4(OceanFog(result,i.positionWS),saturate(color.a * (1 + rim * 2.5)));
            }
            ENDHLSL
        }
    }
}
