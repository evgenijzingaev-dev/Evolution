Shader "Evolution/OceanCloud"
{
    Properties { _Resources("Resource field", 2D) = "white" {} }
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
            float _Layer, _WorldSize;
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(float4 positionOS : POSITION) {
                Varyings o; o.world = TransformObjectToWorld(positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world); return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                float2 p = i.world.xz;
                half3 food = SAMPLE_TEXTURE2D(_Resources, sampler_Resources, p / _WorldSize).rgb;
                float n = OceanNoise(p * .17 + float2(_OceanTime * .025, _Layer * 7));
                float wisps = smoothstep(.35,.8,n) * (.15 + food.r * .3 + food.g * .2);
                half3 tint = lerp(half3(.075,.42,.34), half3(.36,.24,.10),food.g);
                tint = lerp(tint, tint * half3(1.3,1,.8), food.b * .4);
                return half4(OceanFog(tint,i.world),wisps);
            }
            ENDHLSL
        }
    }
}
