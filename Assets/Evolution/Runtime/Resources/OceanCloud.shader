Shader "Evolution/OceanCloud"
{
    Properties { _Resources("Resource field", 2D) = "white" {} _Ecology("Ecological zones", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OceanWater.hlsl"
            TEXTURE2D(_Resources); SAMPLER(sampler_Resources);
            TEXTURE2D(_Ecology); SAMPLER(sampler_Ecology);
            float _Layer, _WorldSize;
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(float4 positionOS : POSITION) {
                Varyings o; o.world = TransformObjectToWorld(positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world); return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                float2 p = i.world.xz;
                half2 habitatData = SAMPLE_TEXTURE2D(_Ecology, sampler_Ecology, p / _WorldSize).rg;
                half3 food = SAMPLE_TEXTURE2D(_Resources, sampler_Resources, p / _WorldSize).rgb;
                float2 drift = float2(_OceanTime * .025, _Layer * 7);
                float n = OceanNoise(p * .08 + drift);
                float filaments = OceanNoise(p * float2(.11,.38) + n * 2 - drift * .4);
                float wisps = smoothstep(.25,.75,n*filaments) * (.12 + food.r * .3 + food.g * .26);
                half3 tint = lerp(half3(.10,.36,.29), half3(.37,.28,.15),food.g);
                tint = lerp(tint, tint * half3(1.3,1,.8), food.b * .4);
                tint *= .6 + .4 * habitatData.r;
                tint = lerp(tint, half3(.29,.12,.37), habitatData.g * .6);
                return half4(OceanFog(tint,i.world),wisps);
            }
            ENDHLSL
        }
    }
}
