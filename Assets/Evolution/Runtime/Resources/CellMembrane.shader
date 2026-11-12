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
            #pragma instancing_options forcemaxcount:256
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "CellSurface.hlsl"
            #include "LivingMembrane.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Shape)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Motor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Response)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Contact)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Vital)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Anatomy)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o);
                float4 shape = UNITY_ACCESS_INSTANCED_PROP(Props,_Shape);
                float4 motor = UNITY_ACCESS_INSTANCED_PROP(Props,_Motor);
                float4 response = UNITY_ACCESS_INSTANCED_PROP(Props,_Response);
                float4 contact = UNITY_ACCESS_INSTANCED_PROP(Props,_Contact);
                float4 anatomy = UNITY_ACCESS_INSTANCED_PROP(Props,_Anatomy);
                float taper = UNITY_ACCESS_INSTANCED_PROP(Props,_Vital).w;
                float3 p = LivingPoint(input.positionOS.xyz,shape,motor,response,contact,anatomy,taper);
                // Differentiate the displaced surface instead of shading the original rigid sphere.
                float3 t = input.tangentOS.xyz;
                float3 b = cross(input.normalOS,t);
                float3 pt = LivingPoint(normalize(input.positionOS.xyz+t*.003)*.5,shape,motor,response,contact,anatomy,taper);
                float3 pb = LivingPoint(normalize(input.positionOS.xyz+b*.003)*.5,shape,motor,response,contact,anatomy,taper);
                float3 normal = normalize(cross(pt-p,pb-p));
                o.positionWS = TransformObjectToWorld(p); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(normal);
                o.tangentWS = float4(TransformObjectToWorldDir(normalize(pt-p)),input.tangentOS.w * GetOddNegativeScale());
                o.uv = input.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 color = UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
                float decay = UNITY_ACCESS_INSTANCED_PROP(Props,_Vital).z;
                // Stable surface erosion only on confirmed dead-cell remnants.
                float erosion = sin(i.uv.x*83)*sin(i.uv.y*61)*.25+.5;
                clip(erosion-decay*.8);
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
