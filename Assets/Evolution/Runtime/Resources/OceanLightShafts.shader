Shader "Evolution/OceanLightShafts"
{
    Properties { _Ecology("Ecological zones", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OceanWater.hlsl"
            TEXTURE2D(_Ecology); SAMPLER(sampler_Ecology);
            float _WorldSize, _Layer;
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(float4 positionOS : POSITION) {
                Varyings o; o.world = TransformObjectToWorld(positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world); return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                float2 p = i.world.xz + float2(i.world.y*.7,i.world.y*.24);
                half light = SAMPLE_TEXTURE2D(_Ecology,sampler_Ecology,i.world.xz/_WorldSize).r;
                float drift = _OceanTime*.025;
                float broad = pow(saturate(sin(p.x*.31+p.y*.08+drift)),14);
                float fine = pow(saturate(sin(p.x*.83+p.y*.19-drift*.6)),24);
                float veil = OceanNoise(p*.09+drift);
                float fade = exp(-length(i.world-GetCameraPositionWS())*.018);
                return half4(.32,.64,.56,(broad+fine*.25)*veil*light*fade*.045);
            }
            ENDHLSL
        }
    }
}
