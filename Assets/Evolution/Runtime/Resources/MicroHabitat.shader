Shader "Evolution/MicroHabitat"
{
    Properties { _InstanceColor("Color", Color) = (0.1,0.3,0.3,1) }
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
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float _Temperature;
            Varyings Vert(Attributes input) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.world = TransformObjectToWorld(input.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.positionOS.xz + .5; return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 data = UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
                float2 p = i.uv * 2 - 1;
                float pool = saturate(1 - dot(p,p) * .7);
                float caustic = pow(saturate(sin(i.world.x * 1.8 + sin(i.world.z * 1.3)) * sin(i.world.z * 2.1 - i.world.x * .4)), 5);
                half3 water = lerp(half3(.014,.055,.10), half3(.09,.045,.065), saturate(_Temperature / 50));
                half3 nutrient = half3(.035,.22,.17) * data.r * (.35 + pool * .65);
                half3 remains = half3(.2,.10,.035) * data.g * pool;
                return half4(water + nutrient + remains + caustic * .025, 1);
            }
            ENDHLSL
        }
    }
}
