Shader "Evolution/OceanLightVolume"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-15" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:32
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OceanWater.hlsl"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4,_InstanceColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes i) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o);
                o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor);
                float spread=.17+i.uv.y*.3;
                float edge=exp(-pow((i.uv.x-.5)/spread,2)*6);
                float ends=pow(saturate(sin(i.uv.y*3.14159)),1.4);
                float mist=.65+.35*OceanNoise(i.world.xz*.7+_OceanTime*.03);
                float distanceFade=1-exp(-length(i.world-GetCameraPositionWS())*.12);
                return half4(tint.rgb,tint.a*edge*ends*mist*distanceFade);
            }
            ENDHLSL
        }
    }
}
