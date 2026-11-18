Shader "Evolution/SurveyMarker"
{
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay-20" "RenderType"="Transparent"}
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off ZTest Always Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma instancing_options forcemaxcount:256
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4,_InstanceColor)
    UNITY_DEFINE_INSTANCED_PROP(float4,_MarkerData)
   UNITY_INSTANCING_BUFFER_END(Props)
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   V Vert(A i) {V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
    float4 data=UNITY_ACCESS_INSTANCED_PROP(Props,_MarkerData);
    o.p=TransformWorldToHClip(TransformObjectToWorld(float3(0,0,0)));
    float2 q=i.p.xy;
    if(data.y>2.5&&data.y<3.5){float3 axis=TransformWorldToViewDir(TransformObjectToWorldDir(float3(1,0,0)),true);float2 dir=normalize(axis.xy+float2(.00001,0));q=float2(dir.x*q.x-dir.y*q.y,dir.y*q.x+dir.x*q.y);}
    o.p.xy+=q*data.x*2/_ScaledScreenParams.xy*o.p.w;o.uv=i.uv;return o;}
   half4 Frag(V i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);
    float4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceColor),d=UNITY_ACCESS_INSTANCED_PROP(Props,_MarkerData);
    float2 q=(i.uv-.5)*2;float r=length(q),a=1-smoothstep(.62,.95,r);
    if(d.y>.5&&d.y<1.5){a=(1-smoothstep(.08,.16,abs(r-.62)))*.65+exp(-r*r*8)*.35;}
    if(d.y>1.5&&d.y<2.5){r=abs(q.x)+abs(q.y);a=1-smoothstep(.65,.95,r);}
    if(d.y>2.5&&d.y<3.5){float shaft=(1-smoothstep(.08,.18,abs(q.y)))*(1-smoothstep(.55,.8,abs(q.x)));float head=(1-smoothstep(.08,.16,abs(abs(q.y)-(0.8-q.x)) ))*step(.15,q.x)*step(q.x,.82);a=max(shaft,head);}
    if(d.y>3.5)a=exp(-r*r*3)*(1-smoothstep(.65,1,r));
    return half4(c.rgb,c.a*saturate(a));}
   ENDHLSL
  }
 }
}
