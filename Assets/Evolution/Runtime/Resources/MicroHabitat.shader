Shader "Evolution/MicroHabitat"
{
    Properties { _Resources("Resource field", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OceanWater.hlsl"
            TEXTURE2D(_Resources); SAMPLER(sampler_Resources);
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            float _Temperature, _WorldSize;
            Varyings Vert(Attributes input) {
                Varyings o; o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world); return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                float2 p = i.world.xz, flow = float2(_OceanTime * .018, _OceanTime * .011);
                half3 food = SAMPLE_TEXTURE2D(_Resources, sampler_Resources, p / _WorldSize).rgb;
                float n = OceanNoise(p * .14 + flow), detail = OceanNoise(p * .37 - flow);
                float light = pow(saturate(sin(p.x * .21 + sin(p.y * .19) + _OceanTime * .09)), 12);
                half3 water = lerp(half3(.025,.10,.17),half3(.045,.24,.27), n);
                water += half3(.03,.09,.055) * food.r * detail + half3(.10,.065,.018) * food.g;
                water = lerp(water, water * half3(1.25,1,.8), food.b * .4);
                water += light * half3(.035,.075,.065) + saturate(_Temperature / 50) * half3(.015,.012,0);
                return half4(OceanFog(water, i.world),1);
            }
            ENDHLSL
        }
    }
}
