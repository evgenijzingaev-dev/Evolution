using System.Collections.Generic;
using Evolution.Core;
using Evolution.Presentation;
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
        readonly Mesh sphere,thread,quad;readonly Material solid,shell,dust,backdrop;
        readonly Mesh[] high=new Mesh[6],low=new Mesh[6];
        readonly Batch[] membranes=new Batch[6],farMembranes=new Batch[6];
        readonly Vector3[][] pickVertices=new Vector3[6][];readonly int[][] pickTriangles=new int[6][];
        readonly Batch bodies,threads,vesicles,particles;
        sealed class Visual {public double Phase;public float Drive;}
        readonly Dictionary<string,Visual> states=new Dictionary<string,Visual>();
        readonly HashSet<string> present=new HashSet<string>();readonly List<string> expired=new List<string>();
        double lastDrawTime=-1;
        public VolumePainter(CrystalWaterLook look)
        {
            sphere=MicroMeshes.Cell();thread=MicroMeshes.Cell(2,6);quad=MicroMeshes.LightSheet();
            solid=Material("VolumeBody");shell=Material("VolumeMembrane");dust=Material("VolumeSuspension");backdrop=Material("VolumeBackdrop");
            foreach(var material in new[] {solid,shell,dust,backdrop})look.Apply(material);
            bodies=new Batch(sphere,solid,128);threads=new Batch(thread,solid,128);vesicles=new Batch(sphere,shell,128);particles=new Batch(quad,dust,256);
            for(int i=0;i<6;i++) {
                high[i]=CrystalMeshes.Create((CrystalForm)i,24,48);low[i]=CrystalMeshes.Create((CrystalForm)i,8,16);
                membranes[i]=new Batch(high[i],shell,128);farMembranes[i]=new Batch(low[i],shell,128);
                pickVertices[i]=high[i].vertices;pickTriangles[i]=high[i].triangles;
            }
        }
        static Material Material(string name)=>new Material(Resources.Load<Shader>(name)) {enableInstancing=true};
        public void Draw(VolumeWorld world,Suspension suspension,Camera camera,float t,bool organisms,bool flow,bool heat,int selected=-1)
        {
            Vector3 eye=camera.transform.position;
            double time=System.Math.Max(0,world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds);
            float dt=lastDrawTime<0?0:(float)System.Math.Max(0,time-lastDrawTime);lastDrawTime=time;
            solid.SetFloat("_WaterTime",(float)time);shell.SetFloat("_WaterTime",(float)time);dust.SetFloat("_WaterTime",(float)time);backdrop.SetFloat("_WaterTime",(float)time);
            present.Clear();
            Graphics.DrawMesh(sphere,Matrix4x4.TRS(eye,Quaternion.identity,Vector3.one*240),backdrop,0,camera,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<suspension.Count;i++) {
                Vector3 p=V(suspension.Get(i,t)),d=p-eye;float half=i<suspension.Count/3?12:32;
                float edge=d.magnitude/half;
                float alpha=(1-Mathf.SmoothStep(.75f,1,edge))*(i%9==0?.5f:.27f);
                if(alpha<=.001f)continue;
                particles.Add(p,Quaternion.identity,Vector3.one*(.035f+i%7*.014f),new Color(.6f,.79f,.98f,alpha));
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
                    var tint=Color.Lerp(new Color(.5f,.93f,.68f,.65f),new Color(.98f,.72f,.3f,.7f),food.Organic/Mathf.Max(.001f,mass));
                    particles.Add(V(food.Position)+local,Quaternion.identity,Vector3.one*size,tint);
                }
            }
            if(organisms) {
                int near=0;
                for(int i=0;i<world.Count;i++) {
                    var a=world.Get(i);if(!a.Alive&&world.Time-a.DiedAt>1.6)continue;
                    Pose(world,a,t,out Vector3 p,out Quaternion rotation,out float size);
                    float distance=Vector3.Distance(eye,p);if(distance>36)continue;
                    bool detailed=distance<13&&(i==selected||near<12);if(detailed&&i!=selected)near++;
                    var c=a.Biology;var g=c.Genes;int phaseId=c.Id%997;var form=CrystalForms.From(g);
                    var traits=new Vector4(g.Photosynthesis,g.Predation,g.Membrane,Mathf.Repeat(phaseId*.618f,1));
                    Color membrane=new Color(.66f,.86f,1,.18f+.1f*g.Membrane);
                    if(c.DamageLastStep>0)membrane=Color.Lerp(membrane,new Color(1,.42f,.48f,.3f),.45f);
                    (detailed?membranes[(int)form]:farMembranes[(int)form]).Add(p,rotation,Vector3.one*size,membrane,traits);
                    Vector3 nucleusScale=form==CrystalForm.Diatom?new Vector3(.35f,.13f,.2f):form==CrystalForm.Spindle?new Vector3(.32f,.17f,.2f):new Vector3(.29f,.24f,.27f);
                    Vector3 nucleus=CrystalMeshes.Point(form,new Vector3(-.045f,.015f,.015f));
                    bodies.Add(p+rotation*nucleus*size,rotation,nucleusScale*size,new Color(.57f,.25f,.79f),traits);
                    if(!detailed||!a.Alive)continue;
                    present.Add(a.Identity);
                    float drive=(a.Velocity-world.Current(a.Position)).Length;
                    if(!states.TryGetValue(a.Identity,out var visual)){visual=new Visual {Phase=phaseId+time*3,Drive=drive};states.Add(a.Identity,visual);}
                    visual.Drive=Mathf.Lerp(visual.Drive,drive,1-Mathf.Exp(-6*dt));visual.Phase+=(3+visual.Drive*8)*dt;
                    float phase=(float)(visual.Phase%(Mathf.PI*40));
                    DrawInterior(world,a,form,p,rotation,size,traits,phase);
                    Color fibre=new Color(.65f,.86f,1);
                    if(g.Speed>.8f) {
                        Vector3 root=CrystalMeshes.Point(form,new Vector3(-.5f,0,0)),last=p+rotation*root*size;
                        for(int j=1;j<=14;j++) {float u=j/14f;Vector3 next=p+rotation*(root+new Vector3(-u*.95f,
                            Mathf.Cos(phase-u*8)*u*.035f,Mathf.Sin(phase-u*8)*u*(.015f+visual.Drive*.08f)))*size;
                            Segment(last,next,.016f*size*(1-u*.65f),fibre);last=next;}
                    }
                    if(g.Uptake>1.05f)DrawCilia(form,p,rotation,size,phase,visual.Drive,fibre);
                    if(form==CrystalForm.Diatom)DrawDiatomRibs(p,rotation,size);
                    if(form==CrystalForm.Radiolarian)for(int j=0;j<14;j++) {
                        Vector3 n=Fibonacci(j,14);Vector3 root=CrystalMeshes.Point(form,n*.5f)*size;
                        Segment(p+rotation*root,p+rotation*(root+n*.22f*size),.014f*size,new Color(.8f,.9f,1));
                    }
                    if(g.Predation>.5f)for(int side=-1;side<=1;side+=2) {
                        var root=CrystalMeshes.Point(form,new Vector3(.45f,0,side*.08f))*size;
                        Segment(p+rotation*root,p+rotation*(root+new Vector3(.13f,.025f,side*.06f)*size),.035f*size,new Color(1,.67f,.31f));
                    }
                    float age=(float)(time-a.FedAt);
                    if(age>=0&&age<.8f&&world.FoodCount>0) {
                        var source=world.GetFood(Mathf.Clamp(a.FoodCloud,0,world.FoodCount-1));Vector3 start=p+(V(source.Position)-p).normalized*size*.65f;
                        Color food=c.Budget.Organic>c.Budget.Substrate?new Color(1,.73f,.3f,.85f):new Color(.55f,1,.68f,.8f);
                        particles.Add(Vector3.Lerp(start,p,Mathf.SmoothStep(0,1,age/.8f)),Quaternion.identity,Vector3.one*.13f*(1-age/.8f),food);
                    }
                }
            }
            expired.Clear();foreach(var id in states.Keys)if(!present.Contains(id))expired.Add(id);foreach(var id in expired)states.Remove(id);
            foreach(var obstacle in world.Obstacles)if((V(obstacle)-eye).sqrMagnitude<36*36) {
                var center=V(obstacle);farMembranes[(int)CrystalForm.Radiolarian].Add(center,Quaternion.identity,Vector3.one*2.45f,new Color(.62f,.81f,.97f,.16f));
                if((center-eye).sqrMagnitude<15*15)for(int j=0;j<10;j++) {
                    Vector3 n=Fibonacci(j,10);Segment(center+n*.45f,center+n*1.22f,.025f,new Color(.72f,.85f,.93f));
                }
            }
            if(flow||heat)for(int y=-24;y<=24;y+=6)for(int z=-24;z<=24;z+=6)for(int x=-24;x<=24;x+=6) {
                var p=new V3(x+Mathf.Floor(eye.x/6)*6,y+Mathf.Floor(eye.y/6)*6,z+Mathf.Floor(eye.z/6)*6);
                float fade=1-Mathf.SmoothStep(14,18,(V(p)-eye).magnitude);if(fade<=0)continue;
                if(heat) {float temp=world.Temperature(p);Color c=Color.Lerp(new Color(.2f,.43f,.9f,.055f),new Color(.95f,.34f,.16f,.055f),Mathf.InverseLerp(9,35,temp));
                    c.a*=fade;vesicles.Add(V(p),Quaternion.identity,Vector3.one*3,c,new Vector4(0,0,0,-1));}
                if(flow) {var current=world.Current(p);Vector3 d=V(current);Vector3 tip=V(p)+d*5;
                    Color c=Color.Lerp(new Color(.3f,.55f,.6f),new Color(.75f,.9f,.78f),current.Length/.35f);
                    Segment(V(p),tip,.035f,c);Vector3 side=Vector3.Cross(d.normalized,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;
                    Segment(tip,tip-d.normalized*.24f+side*.12f,.04f,c);Segment(tip,tip-d.normalized*.24f-side*.12f,.04f,c);}
            }
            bodies.Flush();threads.Flush();vesicles.Flush();particles.Flush();
            for(int i=0;i<6;i++){membranes[i].Flush();farMembranes[i].Flush();}
        }
        static Vector3 Fibonacci(int i,int count)
        {float y=1-2*(i+.5f)/count,r=Mathf.Sqrt(1-y*y),angle=i*2.39996f;return new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);}
        void DrawInterior(VolumeWorld world,VolumeAgent agent,CrystalForm form,Vector3 p,Quaternion rotation,float size,Vector4 traits,float phase)
        {
            var g=agent.Biology.Genes;
            Vector3 nucleus=CrystalMeshes.Point(form,new Vector3(-.045f,.015f,.015f))*size;
            bodies.Add(p+rotation*(nucleus+Vector3.up*.04f*size),rotation,Vector3.one*.085f*size,new Color(.88f,.48f,.91f));
            int count=5+(int)(g.Photosynthesis*5);
            for(int j=0;j<count;j++) {
                Vector3 v=Fibonacci(j,count)*.23f+new Vector3(.03f,.012f*Mathf.Sin(phase*.12f+j),0);
                Vector3 local=CrystalMeshes.Point(form,v)*size;
                bool plastid=g.Photosynthesis>.35f&&j%3!=0;
                Color color=plastid?Color.Lerp(new Color(.46f,.67f,.17f),new Color(.68f,.94f,.27f),Mathf.Clamp01(agent.Biology.Budget.Light*20)):new Color(.97f,.66f,.26f);
                Vector3 scale=(plastid?new Vector3(.15f,.095f,.12f):new Vector3(.09f,.1f,.09f))*size;
                if(form==CrystalForm.Diatom)scale.y*=.65f;
                bodies.Add(p+rotation*local,rotation*Quaternion.Euler(j*27,j*41,15),scale,color,traits);
                vesicles.Add(p+rotation*local,rotation,Vector3.one*.19f*size,new Color(.72f,.85f,1,.12f));
                Vector3 anchor=CrystalMeshes.Point(form,v*.4f)*size;
                Segment(p+rotation*anchor,p+rotation*local,.009f*size,new Color(.62f,.8f,1));
            }
        }
        void DrawCilia(CrystalForm form,Vector3 p,Quaternion rotation,float size,float phase,float drive,Color color)
        {
            int rings=form==CrystalForm.Ciliate?6:3;
            for(int ring=0;ring<rings;ring++)for(int j=0;j<10;j++) {
                float x=-.75f+1.5f*(ring+.5f)/rings,radial=Mathf.Sqrt(1-x*x),angle=j*Mathf.PI*.2f;
                Vector3 n=new Vector3(x,Mathf.Cos(angle)*radial,Mathf.Sin(angle)*radial);
                Vector3 root=CrystalMeshes.Point(form,n*.5f),tip=root+n*(.11f+drive*.07f);
                Vector3 bend=Vector3.Cross(n,Vector3.right)*Mathf.Sin(phase-ring*.7f-j*.35f)*(.02f+drive*.025f);
                Vector3 mid=Vector3.Lerp(root,tip,.55f)+bend*.35f;tip+=bend;
                Segment(p+rotation*root*size,p+rotation*mid*size,.01f*size,color);
                Segment(p+rotation*mid*size,p+rotation*tip*size,.006f*size,color);
            }
        }
        void DrawDiatomRibs(Vector3 p,Quaternion rotation,float size)
        {
            Color gold=new Color(.9f,.77f,.46f);
            for(int rib=0;rib<11;rib++) {
                float x=-.88f+rib*.176f,r=Mathf.Sqrt(1-x*x);Vector3 last=default;
                for(int j=0;j<=12;j++) {
                    float angle=j*Mathf.PI/6;
                    Vector3 next=p+rotation*CrystalMeshes.Point(CrystalForm.Diatom,new Vector3(x,r*Mathf.Cos(angle),r*Mathf.Sin(angle))*.505f)*size;
                    if(j>0)Segment(last,next,.008f*size,gold);last=next;
                }
            }
        }
        static void Pose(VolumeWorld world,VolumeAgent a,float t,out Vector3 p,out Quaternion rotation,out float size)
        {
            float blend=Mathf.Clamp01((float)((world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds-a.RenderTime)/System.Math.Max(.05,a.RenderDuration)));
            p=V(V3.Lerp(a.Previous,a.Position,blend));Vector3 forward=V(V3.Lerp(a.PreviousDirection,a.Direction,blend)).normalized;
            rotation=Quaternion.LookRotation(forward,Mathf.Abs(forward.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
            double time=System.Math.Max(0,world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds);
            size=a.Biology.Genes.Size*(1+.008f*Mathf.Sin((float)time*1.8f+a.Biology.Id%997));
            if(!a.Alive)size*=Mathf.Max(.02f,1-(float)(world.Time-a.DiedAt)/1.6f);
        }
        public int Pick(VolumeWorld world,Ray ray,float interpolation)
        {
            int selected=-1;float closest=36;
            for(int i=0;i<world.Count;i++) {
                var a=world.Get(i);if(!a.Alive)continue;Pose(world,a,interpolation,out var p,out var rotation,out float size);
                Vector3 offset=ray.origin-p;
                if(float.IsPositiveInfinity(Observation.RaySphere(offset.x,offset.y,offset.z,ray.direction.x,ray.direction.y,ray.direction.z,CrystalForms.BoundingRadius*size)))continue;
                Quaternion inverse=Quaternion.Inverse(rotation);Vector3 origin=inverse*offset/size,direction=inverse*ray.direction;
                int form=(int)CrystalForms.From(a.Biology.Genes);var vertices=pickVertices[form];var triangles=pickTriangles[form];
                for(int j=0;j<triangles.Length;j+=3) {
                    Vector3 v0=vertices[triangles[j]],edge1=vertices[triangles[j+1]]-v0,edge2=vertices[triangles[j+2]]-v0;
                    Vector3 h=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,h);if(Mathf.Abs(determinant)<.0000001f)continue;
                    float inv=1/determinant;Vector3 s=origin-v0;float u=Vector3.Dot(s,h)*inv;if(u<0||u>1)continue;
                    Vector3 q=Vector3.Cross(s,edge1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)continue;
                    float distance=Vector3.Dot(edge2,q)*inv*size;if(distance>=0&&distance<closest){closest=distance;selected=i;}
                }
            }
            return selected;
        }
        void Segment(Vector3 a,Vector3 b,float width,Color color) {var d=b-a;threads.Add((a+b)*.5f,Quaternion.FromToRotation(Vector3.right,d),new Vector3(d.magnitude,width,width),color);}
        public void Dispose()
        {
            Object.Destroy(sphere);Object.Destroy(thread);Object.Destroy(quad);Object.Destroy(solid);Object.Destroy(shell);Object.Destroy(dust);Object.Destroy(backdrop);
            for(int i=0;i<6;i++){Object.Destroy(high[i]);Object.Destroy(low[i]);}
        }
    }
}
