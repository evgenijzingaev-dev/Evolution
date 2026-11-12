#ifndef EVOLUTION_LIVING_MEMBRANE
#define EVOLUTION_LIVING_MEMBRANE
// Keep the point map aligned with Presentation/MembranePose.Deform.
// Shape: radial amplitude, integrated phase, axial stretch, cytokinetic neck.
float3 LivingPoint(float3 p, float4 shape, float4 motor, float4 response, float4 contact)
{
    float len = length(p);
    float3 n = len > .00001 ? p / len : float3(1,0,0);
    float equator = len > .00001 ? dot(p.xz,p.xz) / (len*len) : 0;
    float angle = atan2(p.z,p.x);
    float local = pow(max(0,dot(n.xz,contact.xy)),6) * equator;
    float radial = 1 + equator * shape.x * (sin(angle*3-shape.y) + .45*sin(angle*5+shape.y*.7));
    radial += motor.w*.085*pow(max(0,n.x),6)*(.5+.5*sin(shape.y));
    float injury = pow(max(0,dot(n.xz,contact.zw)),6) * equator;
    radial += local*.065*response.x-injury*.055*response.y;
    radial = clamp(radial,.76,1.24);
    float originalX = p.x;
    p *= radial * float3(1+shape.z,1,rsqrt(1+shape.z));
    p.yz *= 1-.32*shape.w*exp(-originalX*originalX*65);
    p.z += motor.y*.09*originalX*originalX + motor.z*.025*originalX;
    return p;
}
#endif
