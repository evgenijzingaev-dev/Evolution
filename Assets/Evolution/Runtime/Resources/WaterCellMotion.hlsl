// Keep the pure C# map WaterCellPose.Deform in sync. Applied to all surface layers.
float3 WaterCellDeform(float3 p,float4 motion,float4 response) {
    float radius=length(p),nx=radius>.00001?p.x/radius:0,nz=radius>.00001?p.z/radius:0;
    float angle=atan2(p.z,p.x),phase=motion.x;
    float compliance=(1-.8*response.w)*motion.z;
    float reach=motion.w*motion.y*compliance;
    float front=pow(max(0,nx),4),side=pow(max(0,nz),4);
    float radial=1+.012*sin(phase*.37)+.023*sin(3*angle-phase)*compliance;
    radial+=reach*(front*.22*(.5+.5*sin(phase))+side*.08*sin(phase-1.1));
    radial+=front*response.y*.07-response.z*.035*sin(phase*7+angle*4);
    float original=p.x;
    p.x=p.x*radial*(1+.055*motion.y*compliance)+reach*.045*sin(phase-.8);
    p.y*=radial*rsqrt(1+.055*motion.y*compliance);
    p.z=p.z*radial+response.x*.035*original*original;
    return p;
}
