using Evolution.Core;
using Evolution.Volume;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evolution.Runtime
{
    internal sealed class VolumePainter
    {
        internal static Vector3 V(V3 p)=>new Vector3(p.X,p.Y,p.Z);
        internal static V3 V(Vector3 p)=>new V3(p.x,p.y,p.z);
        sealed class Batch
        {
            readonly Mesh mesh;readonly Material material;readonly Matrix4x4[] matrices;readonly Vector4[] colors,traits;
            readonly MaterialPropertyBlock block=new MaterialPropertyBlock();int used;
            public Batch(Mesh mesh,Material material,int count) {this.mesh=mesh;this.material=material;matrices=new Matrix4x4[count];colors=new Vector4[count];traits=new Vector4[count];}
            public void Add(Vector3 p,Quaternion r,Vector3 s,Color c,Vector4 phenotype=default)
            {matrices[used]=Matrix4x4.TRS(p,r,s);colors[used]=c;traits[used]=phenotype;if(++used==matrices.Length) Flush();}
            public void Flush() {if(used==0)return;block.SetVectorArray("_InstanceColor",colors);block.SetVectorArray("_Phenotype",traits);
                Graphics.DrawMeshInstanced(mesh,0,material,matrices,used,block,ShadowCastingMode.Off,false);used=0;}
        }
        readonly Mesh sphere,detail,quad;readonly Material solid,shell,dust,backdrop;
        readonly Batch bodies,membranes,farMembranes,particles;
        public VolumePainter()
        {
            sphere=MicroMeshes.Cell();detail=MicroMeshes.Cell(16,24);quad=MicroMeshes.LightSheet();
            solid=Material("VolumeBody");shell=Material("VolumeMembrane");dust=Material("VolumeSuspension");backdrop=Material("VolumeBackdrop");
            bodies=new Batch(sphere,solid,128);membranes=new Batch(detail,shell,128);farMembranes=new Batch(sphere,shell,128);particles=new Batch(quad,dust,256);
        }
        static Material Material(string name)=>new Material(Resources.Load<Shader>(name)) {enableInstancing=true};
        public void Draw(VolumeWorld world,Suspension suspension,Camera camera,float t,bool organisms,bool flow,bool heat)
        {
            Vector3 eye=camera.transform.position;
            Graphics.DrawMesh(sphere,Matrix4x4.TRS(eye,Quaternion.identity,Vector3.one*240),backdrop,0,camera,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<suspension.Count;i++) {
                Vector3 p=V(suspension.Get(i,t)),d=p-eye;float half=i<suspension.Count/3?12:32;
                float edge=d.magnitude/half;
                float alpha=(1-Mathf.SmoothStep(.75f,1,edge))*(i%9==0?.5f:.27f);
                if(alpha<=.001f)continue;
                particles.Add(p,Quaternion.identity,Vector3.one*(.035f+i%7*.014f),new Color(.57f,.66f,.65f,alpha));
            }
            for(int k=0;k<world.FoodCount;k++) {
                var food=world.GetFood(k);float mass=food.Substrate+food.Organic;
                float foodDistance=(V(food.Position)-eye).sqrMagnitude;if(foodDistance>38*38)continue;
                int grains=Mathf.Min(128,Mathf.CeilToInt(mass*2));
                if(foodDistance>18*18)grains=Mathf.Min(24,grains);
                float size=.105f*Mathf.Pow(Mathf.Max(1,mass*2/Mathf.Max(1,grains)),1f/3);
                for(int j=0;j<grains;j++) {
                    float a=j*2.39996f+food.Pattern%997,vertical;
                    // Stable sample IDs, not an arrangement rebuilt from remaining grain count.
                    vertical=Mathf.Sin(j*1.713f+(food.Pattern%997)*.7f);
                    Vector3 local=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-vertical*vertical),vertical,Mathf.Sin(a)*Mathf.Sqrt(1-vertical*vertical));
                    local*=food.Radius*Mathf.Pow(Mathf.Repeat(j*.618034f+.13f,1),1f/3);
                    var tint=Color.Lerp(new Color(.38f,.75f,.45f,.65f),new Color(.79f,.58f,.26f,.7f),food.Organic/Mathf.Max(.001f,mass));
                    particles.Add(V(food.Position)+local,Quaternion.identity,Vector3.one*size,tint);
                }
            }
            if(organisms) {
                int near=0;
                for(int i=0;i<world.Count;i++) {
                    var a=world.Get(i);if(!a.Alive && world.Time-a.DiedAt>1.6)continue;
                    float blend=Mathf.Clamp01((float)((world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds-a.RenderTime)/System.Math.Max(.05,a.RenderDuration)));
                    var c=a.Biology;var g=c.Genes;Vector3 p=V(V3.Lerp(a.Previous,a.Position,blend));
                    int phaseId=c.Id%997;
                    float distance=Vector3.Distance(eye,p);if(distance>65)continue;
                    bool detailed=distance<12&&near++<24;
                    Vector3 forward=V(V3.Lerp(a.PreviousDirection,a.Direction,blend)).normalized;
                    var rotation=Quaternion.LookRotation(forward,Mathf.Abs(forward.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
                    float size=g.Size*(1+.008f*Mathf.Sin((float)world.Time*1.8f+phaseId));
                    if(!a.Alive) size*=Mathf.Max(.02f,1-(float)(world.Time-a.DiedAt)/1.6f);
                    Vector3 shape=new Vector3(1+.45f*Mathf.Clamp01(g.Speed/3)+.25f*g.Predation,.9f-.22f*g.Photosynthesis,.85f+.15f*g.Membrane)*size;
                    Color color=Color.Lerp(new Color(.35f,.64f,.66f),new Color(.36f,.62f,.31f),g.Photosynthesis);
                    color=Color.Lerp(color,new Color(.8f,.46f,.3f),g.Predation*.8f);color=Color.Lerp(color,new Color(.7f,.63f,.44f),g.Membrane*.5f);
                    if(c.DamageLastStep>0)color=Color.Lerp(color,new Color(.9f,.24f,.15f),.4f);
                    var traits=new Vector4(g.Photosynthesis,g.Predation,g.Membrane,Mathf.Repeat(phaseId*.618f,1));
                    bodies.Add(p,rotation,new Vector3(.22f,.23f,.18f)*size,Color.Lerp(color,new Color(.33f,.29f,.42f),.6f));
                    color.a=.48f+.16f*g.Membrane;(detailed?membranes:farMembranes).Add(p,rotation,shape,color,traits);
                    if(!detailed || !a.Alive)continue;
                    for(int j=0;j<3+(int)(g.Photosynthesis*4);j++) {
                        float angle=j*2.39996f+phaseId;Vector3 local=new Vector3(Mathf.Cos(angle)*.22f,.08f*Mathf.Sin(angle),Mathf.Sin(angle)*.23f);
                        local=Vector3.Scale(local,shape);
                        Color organelle=g.Photosynthesis>.4f?Color.Lerp(new Color(.15f,.4f,.17f),new Color(.4f,.75f,.22f),Mathf.Clamp01(c.Budget.Light*20)):Color.Lerp(color,Color.white,.2f);
                        organelle.a=1;bodies.Add(p+rotation*local,rotation,new Vector3(.14f,.06f,.09f)*size,organelle);
                    }
                    float drive=(a.Velocity-world.Current(a.Position)).Length;
                    float phase=(float)world.Time*(3+drive*8)+phaseId;
                    if(g.Speed>.8f) {
                        Vector3 last=p+rotation*new Vector3(-shape.x*.5f,0,0);
                        for(int j=1;j<=10;j++) {float u=j/10f;Vector3 next=p+rotation*new Vector3(-shape.x*.5f-u*.9f*size,
                            Mathf.Cos(phase-u*7)*u*.035f*size,Mathf.Sin(phase-u*7)*u*(.02f+drive*.09f)*size);
                            Segment(last,next,.023f*size,color);last=next;}
                    }
                    if(g.Uptake>1.05f)for(int j=0;j<10;j++) {
                        float angle=j*Mathf.PI/5;Vector3 root=new Vector3(Mathf.Cos(angle)*shape.x*.5f,0,Mathf.Sin(angle)*shape.z*.5f);
                        Vector3 tip=root+new Vector3(Mathf.Cos(angle)*.12f,Mathf.Sin(phase-angle)*.05f,Mathf.Sin(angle)*.12f)*size;
                        Segment(p+rotation*root,p+rotation*tip,.014f*size,color);
                    }
                    float age=(float)(world.Time-a.FedAt);
                    if(age>=0&&age<.8f&&world.FoodCount>0) {var source=world.GetFood(Mathf.Clamp(a.FoodCloud,0,world.FoodCount-1));Vector3 start=p+(V(source.Position)-p).normalized*size*.65f;
                        particles.Add(Vector3.Lerp(start,p,Mathf.SmoothStep(0,1,age/.8f)),Quaternion.identity,Vector3.one*.12f*(1-age/.8f),new Color(.6f,.87f,.4f,.8f));}
                }
            }
            foreach(var obstacle in world.Obstacles)if((V(obstacle)-eye).sqrMagnitude<40*40) bodies.Add(V(obstacle),Quaternion.identity,Vector3.one*VolumeWorld.ObstacleRadius*2,new Color(.26f,.3f,.28f));
            if(flow||heat)for(int y=-24;y<=24;y+=6)for(int z=-24;z<=24;z+=6)for(int x=-24;x<=24;x+=6) {
                var p=new V3(x+Mathf.Floor(eye.x/6)*6,y+Mathf.Floor(eye.y/6)*6,z+Mathf.Floor(eye.z/6)*6);
                float fade=1-Mathf.SmoothStep(14,18,(V(p)-eye).magnitude);if(fade<=0)continue;
                if(heat) {float temp=world.Temperature(p);Color c=Color.Lerp(new Color(.2f,.43f,.9f,.055f),new Color(.95f,.34f,.16f,.055f),Mathf.InverseLerp(9,35,temp));
                    c.a*=fade;farMembranes.Add(V(p),Quaternion.identity,Vector3.one*3,c);}
                if(flow) {var current=world.Current(p);Vector3 d=V(current);Vector3 tip=V(p)+d*5;
                    Color c=Color.Lerp(new Color(.3f,.55f,.6f),new Color(.75f,.9f,.78f),current.Length/.35f);
                    Segment(V(p),tip,.035f,c);Vector3 side=Vector3.Cross(d.normalized,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;
                    Segment(tip,tip-d.normalized*.24f+side*.12f,.04f,c);Segment(tip,tip-d.normalized*.24f-side*.12f,.04f,c);}
            }
            bodies.Flush();membranes.Flush();farMembranes.Flush();particles.Flush();
        }
        void Segment(Vector3 a,Vector3 b,float width,Color color) {var d=b-a;bodies.Add((a+b)*.5f,Quaternion.FromToRotation(Vector3.right,d),new Vector3(d.magnitude,width,width),color);}
        public void Dispose() {Object.Destroy(sphere);Object.Destroy(detail);Object.Destroy(quad);Object.Destroy(solid);Object.Destroy(shell);Object.Destroy(dust);Object.Destroy(backdrop);}
    }
}
