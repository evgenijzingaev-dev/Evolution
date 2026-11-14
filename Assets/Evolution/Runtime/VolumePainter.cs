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
            readonly Mesh mesh;readonly Material material;readonly Matrix4x4[] matrices;readonly Vector4[] colors,traits,motions,responses,deaths;readonly float[] fissions;public float Fission;public WaterCellPose Motion;public float Rupture;
            readonly MaterialPropertyBlock block=new MaterialPropertyBlock();readonly float[] visibility;int used;public float Opacity=1;
            public Batch(Mesh mesh,Material material,int count) {this.mesh=mesh;this.material=material;matrices=new Matrix4x4[count];colors=new Vector4[count];traits=new Vector4[count];motions=new Vector4[count];responses=new Vector4[count];deaths=new Vector4[count];visibility=new float[count];fissions=new float[count];}
            public void Add(Vector3 p,Quaternion r,Vector3 s,Color c,Vector4 phenotype=default)
            {matrices[used]=Matrix4x4.TRS(p,r,s);colors[used]=c;traits[used]=phenotype;motions[used]=new Vector4(Motion.Phase,Motion.Drive,Motion.Activity,Motion.Amoeba);responses[used]=new Vector4(Motion.Turn,Motion.Feed,Motion.Damage,Motion.Shell);deaths[used]=new Vector4(Rupture,0,0,0);fissions[used]=Fission;visibility[used]=Opacity;if(++used==matrices.Length) Flush();}
            public void Flush() {if(used==0)return;block.SetVectorArray("_InstanceColor",colors);block.SetVectorArray("_Phenotype",traits);block.SetFloatArray("_Visibility",visibility);block.SetFloatArray("_Fission",fissions);
                block.SetVectorArray("_Death",deaths);block.SetVectorArray("_Motion",motions);block.SetVectorArray("_Response",responses);Graphics.DrawMeshInstanced(mesh,0,material,matrices,used,block,ShadowCastingMode.Off,false);used=0;}
        }
        readonly Mesh sphere,thread,quad;readonly Material solid,shell,dust,backdrop;
        readonly Mesh[] high=new Mesh[6],low=new Mesh[6];
        readonly Batch[] membranes=new Batch[6],farMembranes=new Batch[6];
        readonly Batch[] cytoplasm=new Batch[6];
        readonly Vector3[][] pickVertices=new Vector3[6][];readonly int[][] pickTriangles=new int[6][];
        readonly Batch bodies,threads,vesicles,particles;
        sealed class Visual {public CellAnimation Animation;public Quaternion Rotation;public WaterCellPose Pose;}
        WaterCellPose cellPose;
        static Vector3 Deform(Vector3 point,WaterCellPose pose) {pose.Deform(ref point.x,ref point.y,ref point.z);return point;}
        readonly Dictionary<string,Visual> states=new Dictionary<string,Visual>();
        readonly HashSet<string> present=new HashSet<string>();readonly List<string> expired=new List<string>();
        double lastDrawTime=-1;
        LifeJournal journal;long consumed;
        readonly LifeEvent[] divisions=new LifeEvent[32],deaths=new LifeEvent[32];int divisionSlot,deathSlot;
        readonly Dictionary<string,int> indices=new Dictionary<string,int>();
        void ObserveLife(VolumeWorld world,double time)
        {
            if(!object.ReferenceEquals(journal,world.Life)) {
                journal=world.Life;consumed=journal.Oldest-1;
                System.Array.Clear(divisions,0,divisions.Length);System.Array.Clear(deaths,0,deaths.Length);divisionSlot=deathSlot=0;
            }
            for(long sequence=System.Math.Max(consumed+1,journal.Oldest);sequence<=journal.Latest;sequence++)
                if(journal.TryRead(sequence,out var e)) {
                    if(e.Kind==LifeEventKind.Division&&time-e.Time<VolumeWorld.DivisionSeconds)divisions[divisionSlot++%divisions.Length]=e;
                    if(e.Kind==LifeEventKind.Death&&time-e.Time<4)deaths[deathSlot++%deaths.Length]=e;
                }
            consumed=journal.Latest;
        }
        bool Division(VolumeAgent agent,out LifeEvent result)
        {
            if(agent.DivisionAt>0)for(int i=0;i<divisions.Length;i++) {
                var e=divisions[i];if(e.Sequence!=0&&e.Time==agent.DivisionAt&&(e.Agent.Identity==agent.Identity||e.Child.Identity==agent.Identity)) {result=e;return true;}
            }
            result=default;return false;
        }
        bool PairAlive(VolumeWorld world,LifeEvent e)=>indices.TryGetValue(e.Agent.Identity,out int parent)&&indices.TryGetValue(e.Child.Identity,out int child)&&world.Get(parent).Alive&&world.Get(child).Alive;
        public VolumePainter(CrystalWaterLook look)
        {
            sphere=MicroMeshes.Cell(12,20);thread=MicroMeshes.Cell(2,6);quad=MicroMeshes.LightSheet();
            var sphereBounds=sphere.bounds;sphereBounds.Expand(.45f);sphere.bounds=sphereBounds;
            solid=Material("VolumeBody");shell=Material("VolumeMembrane");dust=Material("VolumeSuspension");backdrop=Material("VolumeBackdrop");
            foreach(var material in new[] {solid,shell,dust,backdrop})look.Apply(material);
            bodies=new Batch(sphere,solid,128);threads=new Batch(thread,solid,128);vesicles=new Batch(sphere,shell,128);particles=new Batch(quad,dust,256);
            for(int i=0;i<6;i++) {
                high[i]=CrystalMeshes.Create((CrystalForm)i,24,48);low[i]=CrystalMeshes.Create((CrystalForm)i,8,16);
                var highBounds=high[i].bounds;highBounds.Expand(2f);high[i].bounds=highBounds;
                var lowBounds=low[i].bounds;lowBounds.Expand(2f);low[i].bounds=lowBounds;
                membranes[i]=new Batch(high[i],shell,128);farMembranes[i]=new Batch(low[i],shell,128);
                cytoplasm[i]=new Batch(low[i],shell,128);
                pickVertices[i]=high[i].vertices;pickTriangles[i]=high[i].triangles;
            }
        }
        static Material Material(string name)=>new Material(Resources.Load<Shader>(name)) {enableInstancing=true};
        public void Draw(VolumeWorld world,Suspension suspension,Camera camera,float t,bool organisms,bool flow,bool heat,int selected=-1,bool foodHints=false,bool strategic=false,int slice=int.MinValue)
        {
            Vector3 eye=camera.transform.position;
            double time=System.Math.Max(0,world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds);
            float dt=lastDrawTime<0?0:(float)System.Math.Max(0,time-lastDrawTime);lastDrawTime=time;
            solid.SetFloat("_WaterTime",(float)time);shell.SetFloat("_WaterTime",(float)time);dust.SetFloat("_WaterTime",(float)time);backdrop.SetFloat("_WaterTime",(float)time);
            var dataCenter=new Vector4(world.ActiveCenter.X,world.ActiveCenter.Y,world.ActiveCenter.Z,strategic?1:0);solid.SetVector("_OceanDataCenter",dataCenter);shell.SetVector("_OceanDataCenter",dataCenter);
            present.Clear();ObserveLife(world,time);
            indices.Clear();for(int i=0;i<world.Count;i++)indices[world.Get(i).Identity]=i;
            Graphics.DrawMesh(sphere,Matrix4x4.TRS(eye,Quaternion.identity,Vector3.one*240),backdrop,0,camera,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<suspension.Count;i++) {
                Vector3 p=V(suspension.Get(i,t)),d=p-eye;float half=i<suspension.Count/3?12:32;
                float edge=d.magnitude/half;
                float alpha=(1-WaterOptics.SmoothRange(.75f,1,edge))*(i%9==0?.55f:.28f);
                if(alpha<=.001f)continue;
                bool flake=i%29==0;float size=(.045f+i%7*.014f)*(flake?2.2f:1);
                Color color=i%9==0?new Color(1.1f,1.45f,1.6f,alpha):new Color(.72f,1.05f,1.18f,alpha);
                particles.Add(p,Quaternion.identity,Vector3.one*size,color,new Vector4(flake?1:0,0,0,0));
            }
            for(int k=0;k<world.FoodCount;k++) {
                var food=world.GetFood(k);if(slice!=int.MinValue&&ChunkKey.At(food.Position).Y!=slice)continue;float dataFade=strategic?1-WaterOptics.SmoothRange(26,34,(food.Position-world.ActiveCenter).Length):1;if(dataFade<.001f)continue;float mass=food.Substrate+food.Organic;
                float foodDistance=(V(food.Position)-eye).sqrMagnitude;if(foodDistance>38*38)continue;
                int grains=Mathf.Min(128,Mathf.CeilToInt(mass*2));
                if(foodDistance>18*18)grains=Mathf.Min(24,grains);
                float size=.125f*Mathf.Pow(Mathf.Max(1,mass*2/Mathf.Max(1,grains)),1f/3);
                if(foodHints&&selected>=0&&world.FoodPotential(k,world.Get(selected).Biology)>0) {
                    Color halo=food.Organic>food.Substrate?new Color(1,.68f,.25f,.18f):new Color(.45f,1,.65f,.18f);
                    halo.a*=dataFade;particles.Add(V(food.Position),Quaternion.identity,Vector3.one*food.Radius*2.2f,halo,new Vector4(4,0,0,0));
                }
                for(int j=0;j<grains;j++) {
                    float a=j*2.39996f+food.Pattern%997,vertical;
                    // Stable sample IDs, not an arrangement rebuilt from remaining grain count.
                    vertical=2*Mathf.Repeat(j*.7548777f+(food.Pattern%997)*.131f,1)-1;
                    Vector3 local=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-vertical*vertical),vertical,Mathf.Sin(a)*Mathf.Sqrt(1-vertical*vertical));
                    local*=food.Radius*Mathf.Pow(Mathf.Repeat(j*.618034f+.13f,1),1f/3);
                    var tint=Color.Lerp(new Color(.5f,.93f,.68f,.65f),new Color(.98f,.72f,.3f,.7f),food.Organic/Mathf.Max(.001f,mass));
                    tint.a*=dataFade;particles.Add(V(food.Position)+local,Quaternion.identity,Vector3.one*size,tint,new Vector4(2,0,0,0));
                }
            }
            if(organisms) {
                int near=0;
                for(int i=0;i<world.Count;i++) {
                    var a=world.Get(i);if(slice!=int.MinValue&&ChunkKey.At(a.Position).Y!=slice)continue;if(!a.Alive)continue;
                    Pose(world,a,t,out Vector3 p,out Quaternion rotation,out float size);
                    float distance=Vector3.Distance(eye,p);if(distance>36)continue;
                    bodies.Opacity=threads.Opacity=vesicles.Opacity=1;
                    bool detailed=distance<14&&(i==selected||near<12);if(detailed&&i!=selected)near++;
                    float detail=detailed?1-WaterOptics.SmoothRange(10,14,distance):0;
                    var c=a.Biology;var g=c.Genes;int phaseId=(c.ColonyId!=0?c.ColonyId:c.Id)%997;var form=CrystalForms.From(g);
                    var traits=new Vector4(g.Photosynthesis,g.Predation,g.Membrane,Mathf.Repeat(phaseId*.618f,1));
                    present.Add(a.Identity);
                    var target=AnimationTargets.From(c,world.Sample(a.Position),c.SwimSpeed,VolumeWorld.StepSeconds);
                    float propulsion=(a.Velocity-world.Current(a.Position)).Length;
                    target.Drive=AnimationTargets.Clamp(propulsion/System.Math.Max(.1f,Ecology.Mobility(g)));
                    target.Turn=Mathf.Clamp(Vector3.Dot(Vector3.Cross(V(a.PreviousDirection),V(a.Direction)),rotation*Vector3.up)*12,-1,1);
                    target.Frequency=(.7f+2.7f*target.Drive)*target.Activity/(float)System.Math.Sqrt(g.Size);
                    if(!states.TryGetValue(a.Identity,out var visual)) {
                        var animation=new CellAnimation(phaseId,target);animation.Advance(time);
                        visual=new Visual {Animation=animation,Rotation=rotation};states.Add(a.Identity,visual);
                    } else {visual.Animation.Target=target;visual.Animation.Advance(dt);}
                    visual.Animation.Observe(c,time,0);
                    Vector3 actual=V(a.Velocity-world.Current(a.Position));
                    if(actual.sqrMagnitude>.0025f&&(a.DivisionAt<=0||a.DivisionAt+VolumeWorld.DivisionSeconds<time))
                        rotation=Quaternion.LookRotation(actual.normalized,Mathf.Abs(actual.normalized.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
                    visual.Rotation=Quaternion.Slerp(visual.Rotation,rotation,1-Mathf.Exp(-8*dt));rotation=visual.Rotation;
                    cellPose=WaterCellPose.From(visual.Animation,c,time);visual.Pose=cellPose;
                    membranes[(int)form].Motion=farMembranes[(int)form].Motion=cytoplasm[(int)form].Motion=cellPose;
                    float phase=cellPose.Phase;
                    Color membrane=new Color(.66f,.86f,1,.30f+.12f*g.Membrane);
                    if(world.Niches)membrane=Color.Lerp(membrane,new Color(1,.74f,.45f,membrane.a),g.ThermalTolerance*.24f*Mathf.InverseLerp(25,43,g.PreferredTemperature));
                    if(c.Specialization>0) {
                        Color role=c.Role==CellRole.Feeding?new Color(.45f,1,.65f,membrane.a):c.Role==CellRole.Movement?new Color(.4f,.8f,1,membrane.a):c.Role==CellRole.Defense?new Color(.9f,.72f,.42f,membrane.a):membrane;
                        membrane=Color.Lerp(membrane,role,c.Specialization*.65f);
                    }
                    int parent=world.BondParent(i);
                    if(a.Alive&&parent>=0) {
                        Pose(world,world.Get(parent),t,out Vector3 anchor,out _,out _);
                        Segment(anchor,p,.065f*size,Color.Lerp(new Color(.35f,.65f,.85f),new Color(.6f,1,.8f),Mathf.Clamp01(Mathf.Abs(c.TransportLastStep)*10)));
                    }
                    if(c.DamageLastStep>0)membrane=Color.Lerp(membrane,new Color(1,.42f,.48f,.3f),.45f);
                    float divisionAge=a.DivisionAt>0?(float)((time-a.DivisionAt)/VolumeWorld.DivisionSeconds):-1;
                    bool dividing=Division(a,out var division)&&PairAlive(world,division)&&divisionAge>=0&&divisionAge<1;
                    float shellWeight=dividing?WaterCellPose.ShellSeparation(divisionAge):1;
                    float anatomyWeight=dividing&&a.DivisionChild?WaterCellPose.DaughterVisibility(divisionAge):1;
                    bodies.Opacity=threads.Opacity=vesicles.Opacity=anatomyWeight;
                    // A paid pair shares one continuous envelope before the two real shells separate.
                    membranes[(int)form].Fission=farMembranes[(int)form].Fission=cytoplasm[(int)form].Fission=0;
                    if(dividing)DrawDivisionBridge(world,a,division,t,divisionAge,p,size,traits,detailed,membrane,rotation);
                    if(detail>0){membranes[(int)form].Opacity=detail*shellWeight;membranes[(int)form].Add(p,rotation,Vector3.one*size,membrane,traits);}
                    if(detail<1){farMembranes[(int)form].Opacity=(1-detail)*shellWeight;farMembranes[(int)form].Add(p,rotation,Vector3.one*size,membrane,traits);}
                    membranes[(int)form].Opacity=farMembranes[(int)form].Opacity=1;
                    cytoplasm[(int)form].Opacity=shellWeight;
                    cytoplasm[(int)form].Add(p,rotation,Vector3.one*size*.84f,
                        Color.Lerp(new Color(.16f,.26f,.39f,.20f),new Color(.13f,.34f,.32f,.20f),g.Photosynthesis),
                        new Vector4(g.Photosynthesis,g.Predation,g.Membrane,-2));
                    cytoplasm[(int)form].Opacity=1;
                    Vector3 nucleusScale=form==CrystalForm.Diatom?new Vector3(.35f,.13f,.2f):form==CrystalForm.Spindle?new Vector3(.32f,.17f,.2f):new Vector3(.29f,.24f,.27f);
                    Vector3 nucleus=CrystalMeshes.Point(form,new Vector3(-.045f,.015f,.015f));
                    bodies.Add(p+rotation*Deform(nucleus,cellPose)*size,rotation,nucleusScale*size,new Color(.43f,.27f,.65f),traits);
                    if(detail<1||!a.Alive) {
                        bodies.Opacity=(1-detail)*anatomyWeight;
                        // Real residents keep a small internal silhouette at medium distance.
                        if(a.Alive&&distance<30)for(int j=0;j<3;j++) {
                            Vector3 local=Deform(CrystalMeshes.Point(form,Fibonacci(j,3)*.22f),cellPose)*size;
                            Color pigment=Color.Lerp(new Color(.88f,.61f,.32f),new Color(.4f,.72f,.40f),g.Photosynthesis);
                            bodies.Add(p+rotation*local,rotation,Vector3.one*size*.14f,pigment,traits);
                        }
                        bodies.Opacity=1;if(!detailed||!a.Alive)continue;
                    }
                    bodies.Opacity=threads.Opacity=vesicles.Opacity=detail*anatomyWeight;
                    DrawInterior(world,a,form,p,rotation,size,traits,phase);
                    Color fibre=new Color(.55f,.82f,.96f);
                    if(target.Flagellum||c.Role==CellRole.Movement&&c.Specialization>.1f) {
                        Vector3 root=Deform(CrystalMeshes.Point(form,new Vector3(-.5f,0,0)),cellPose),last=p+rotation*root*size;
                        for(int j=1;j<=18;j++) {
                            float u=j/18f;
                            MotorCurves.Flagellum(u,phase,cellPose.Drive,cellPose.Activity,cellPose.Turn,out float x,out float z);
                            Vector3 next=p+rotation*(root+new Vector3((x+.5f)*(1+.25f*g.Speed),Mathf.Cos(phase-u*8)*u*.025f*cellPose.Activity,z))*size;
                            Segment(last,next,.014f*size*(1-u*.72f),fibre);last=next;
                        }
                    }
                    if(target.Cilia)DrawCilia(form,p,rotation,size,phase,cellPose.Drive,fibre);
                    if(form==CrystalForm.Diatom)DrawDiatomRibs(p,rotation,size);
                    if(form==CrystalForm.Radiolarian||c.Role==CellRole.Defense&&c.Specialization>.3f)for(int j=0;j<14;j++) {
                        Vector3 n=Fibonacci(j,14);Vector3 root=CrystalMeshes.Point(form,n*.5f)*size;
                        Segment(p+rotation*Deform(root/size,cellPose)*size,p+rotation*(Deform(root/size,cellPose)+n*.22f)*size,.014f*size,new Color(.8f,.9f,1));
                    }
                    if(g.Predation>.5f)for(int side=-1;side<=1;side+=2) {
                        var root=CrystalMeshes.Point(form,new Vector3(.45f,0,side*.08f))*size;
                        Segment(p+rotation*Deform(root/size,cellPose)*size,p+rotation*(Deform(root/size,cellPose)+new Vector3(.13f,.025f,side*.06f))*size,.035f*size,new Color(1,.67f,.31f));
                    }
                    float age=(float)(time-a.FedAt);
                    if(age>=0&&age<.8f&&world.FoodCount>0) {
                        var source=world.GetFood(Mathf.Clamp(a.FoodCloud,0,world.FoodCount-1));Vector3 start=p+(V(source.Position)-p).normalized*size*.65f;
                        Color food=c.Budget.Organic>c.Budget.Substrate?new Color(1,.73f,.3f,.85f):new Color(.55f,1,.68f,.8f);
                        particles.Add(Vector3.Lerp(start,p,Mathf.SmoothStep(0,1,age/.8f)),Quaternion.identity,Vector3.one*.13f*(1-age/.8f),food);
                    }
                }
            }
            cellPose=default;
            if(organisms)DrawDeaths(world,eye,time,slice);
            bodies.Opacity=threads.Opacity=vesicles.Opacity=1;
            expired.Clear();foreach(var id in states.Keys)if(!present.Contains(id))expired.Add(id);foreach(var id in expired)states.Remove(id);
            for(int form=0;form<6;form++){membranes[form].Fission=farMembranes[form].Fission=cytoplasm[form].Fission=0;membranes[form].Motion=farMembranes[form].Motion=cytoplasm[form].Motion=default;}
            foreach(var obstacle in world.Obstacles)if((V(obstacle)-eye).sqrMagnitude<36*36) {
                var center=V(obstacle);
                if(world.Niches&&NicheEnvironment.Vent(obstacle,world.FlowOffset)>.8f) {
                    // This outcrop uses the actual Core collision/source position. VFX creates no food.
                    bodies.Add(center,Quaternion.identity,new Vector3(2.1f,1.5f,2.0f),new Color(.25f,.20f,.17f));
                    bodies.Add(center+Vector3.up*.45f,Quaternion.Euler(8,17,0),new Vector3(.85f,1.65f,.8f),new Color(.31f,.23f,.17f));
                    float fuel=Mathf.Clamp01(world.EnvironmentAt(obstacle+new V3(0,1.8f,0)).Chemicals/25);
                    for(int jet=0;jet<16;jet++) {
                        float age=Mathf.Repeat((float)time*.22f+jet*.0618f,1);
                        Vector3 drift=new Vector3(Mathf.Sin(jet*2.4f+age*2)*age*.65f,1.1f+age*4,Mathf.Cos(jet*2.4f+age*2)*age*.65f);
                        particles.Add(center+drift,Quaternion.identity,Vector3.one*(.12f+age*.25f),new Color(1,.55f,.23f,(1-age)*(.12f+.14f*fuel)),new Vector4(4,0,0,0));
                    }
                    continue;
                }
                // Existing collision objects become porous organic/silica flocs, not invented organisms or food.
farMembranes[(int)CrystalForm.Radiolarian].Add(center,Quaternion.identity,Vector3.one*2.45f,new Color(.56f,.77f,.85f,.10f));
                for(int j=0;j<12;j++) {
                    Vector3 n=Fibonacci(j,12);var knot=center+n*(.65f+.12f*Mathf.Sin(j*2.4f));
                    bodies.Add(knot,Quaternion.Euler(j*17,j*31,0),new Vector3(.28f,.18f,.23f),new Color(.52f,.68f,.67f));
                    Segment(knot,center+n*1.22f,.035f,new Color(.6f,.79f,.8f));
                    if(j>0)Segment(knot,center+Fibonacci(j-1,12)*.68f,.028f,new Color(.42f,.61f,.65f));
                }
            }
            bool lightHints=foodHints&&selected>=0&&world.Get(selected).Biology.Genes.Photosynthesis>.5f;
            if(flow||heat||lightHints)for(int y=-24;y<=24;y+=6)for(int z=-24;z<=24;z+=6)for(int x=-24;x<=24;x+=6) {
                var p=new V3(x+Mathf.Floor(eye.x/6)*6,y+Mathf.Floor(eye.y/6)*6,z+Mathf.Floor(eye.z/6)*6);
                p+=new V3(Mathf.Sin(p.Y*.31f+p.Z)*1.1f,Mathf.Sin(p.Z*.27f+p.X)*1.1f,Mathf.Sin(p.X*.37f+p.Y)*1.1f);
                float fade=1-WaterOptics.SmoothRange(14,18,(V(p)-eye).magnitude);if(fade<=0)continue;
                if(heat) {float temp=world.Temperature(p);Color c=Color.Lerp(new Color(.2f,.48f,1,.17f),new Color(1,.38f,.16f,.17f),Mathf.InverseLerp(9,world.Niches?50:35,temp));
                    c.a*=fade;particles.Add(V(p),Quaternion.identity,Vector3.one*7.2f,c,new Vector4(4,0,0,0));}
                if(lightHints) {float light=world.Sample(p).Light;
                    particles.Add(V(p),Quaternion.identity,Vector3.one*2.5f,new Color(.65f,.91f,1,.07f*light*fade),new Vector4(4,0,0,0));}
                if(flow) {var current=world.Current(p);Vector3 d=V(current);float phase=Mathf.Repeat((float)time*current.Length*.45f+p.X*.13f+p.Y*.17f,1);
                    float pulse=WaterOptics.SmoothRange(0,.15f,phase)*(1-WaterOptics.SmoothRange(.8f,1,phase));
                    Color c=Color.Lerp(new Color(.35f,.80f,.91f),new Color(.75f,1,1.15f),current.Length/.35f);c.a=fade*.9f*pulse;
                    particles.Add(V(p)+d*(phase-.5f)*5,Quaternion.FromToRotation(Vector3.right,d),new Vector3(.6f+d.magnitude*5,.12f,.12f),c,new Vector4(3,0,0,0));}
            }
            bodies.Flush();threads.Flush();vesicles.Flush();particles.Flush();
            for(int i=0;i<6;i++){cytoplasm[i].Flush();membranes[i].Flush();farMembranes[i].Flush();}
        }
        void DrawDeaths(VolumeWorld world,Vector3 eye,double time,int slice)
        {
            for(int eventIndex=0;eventIndex<deaths.Length;eventIndex++) {
                var e=deaths[eventIndex];if(e.Sequence==0)continue;
                float age=(float)(time-e.Time);if(age<0||age>=4)continue;
                var a=e.Agent;if(slice!=int.MinValue&&ChunkKey.At(a.Position).Y!=slice)continue;
                Vector3 center=V(a.Position);if((center-eye).sqrMagnitude>36*36)continue;
                bool attacked=e.Cause==DeathAppearance.Damage;
                float rupture=WaterCellPose.Rupture(age+(attacked?.3f:0));
                float fade=WaterCellPose.Remains(age),size=a.Biology.Genes.Size;
                var form=CrystalForms.From(a.Biology.Genes);int f=(int)form;
                Vector3 forward=V(a.Direction);var rotation=Quaternion.LookRotation(forward,Mathf.Abs(forward.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
                // A terminal snapshot follows the surrounding fluid without retaining biological data.
                center+=V(world.Current(a.Position))*age*.25f;
                var collapse=new Vector3(1+.12f*rupture,1-.5f*Mathf.Clamp01(age/2),1-.18f*rupture)*size;
                var motion=new WaterCellPose {Phase=(float)(a.Biology.Id%997)+age*2,Activity=1-Mathf.Clamp01(age/1.3f),Damage=attacked?1:0,Shell=a.Biology.Genes.Membrane};
                farMembranes[f].Motion=cytoplasm[f].Motion=motion;
                farMembranes[f].Opacity=cytoplasm[f].Opacity=fade;
                farMembranes[f].Rupture=cytoplasm[f].Rupture=rupture;
                farMembranes[f].Add(center,rotation,collapse,attacked?new Color(.85f,.62f,.85f,.32f):new Color(.49f,.68f,.78f,.28f));
                cytoplasm[f].Add(center,rotation,collapse*.84f,new Color(.27f,.44f,.57f,.3f),new Vector4(0,0,0,-2));
                bodies.Opacity=fade;threads.Opacity=fade*(1-Mathf.Clamp01(age/.9f));
                bodies.Add(center+rotation*new Vector3(-.04f,.02f,0)*size,rotation,new Vector3(.29f,.20f,.25f)*size,new Color(.48f,.34f,.60f));
                // Motile structures visibly lose their stroke before membrane rupture.
                if(age<.9f)for(int j=0;j<12;j++) {
                    Vector3 n=Fibonacci(j,12),root=CrystalMeshes.Point(form,n*.5f)*size;
                    Segment(center+rotation*root,center+rotation*(root+n*.12f*size*(1-age/.9f)),.008f*size,new Color(.5f,.7f,.8f));
                }
                // Fixed procedural slots: no GameObjects, ParticleSystems, food or ledger writes.
                for(int j=0;j<24;j++) {
                    float release=.8f+j*.035f-(attacked?.3f:0),elapsed=age-release;if(elapsed<=0)continue;
                    Vector3 spread=Fibonacci(j,24),exit=rotation*new Vector3(.42f,.03f,0)*size;
                    Vector3 jet=rotation*(Vector3.right*(.25f+(attacked?.35f:0))+spread*.19f)*size;
                    Vector3 point=center+exit+jet*elapsed+Vector3.up*(elapsed*elapsed*.035f);
                    float opacity=fade*Mathf.Clamp01(elapsed*4)*(1-.6f*Mathf.Clamp01(elapsed/3));
                    Color gel=j%4==0?new Color(.65f,.48f,.78f,opacity*.58f):new Color(.57f,.81f,.91f,opacity*.32f);
                    particles.Add(point,Quaternion.identity,Vector3.one*size*(.16f+j%3*.035f),gel,new Vector4(4,0,0,0));
                    if(j%4==0) {
                        vesicles.Opacity=opacity;vesicles.Add(point,rotation,Vector3.one*size*.09f,new Color(.7f,.85f,1,.25f));
                    }
                }
                farMembranes[f].Motion=cytoplasm[f].Motion=default;
                farMembranes[f].Opacity=cytoplasm[f].Opacity=1;farMembranes[f].Rupture=cytoplasm[f].Rupture=0;
            }
            bodies.Opacity=threads.Opacity=vesicles.Opacity=1;
        }
        void DrawDivisionBridge(VolumeWorld world,VolumeAgent parent,LifeEvent division,float interpolation,float age,Vector3 p,float size,Vector4 traits,bool detailed,Color membrane,Quaternion parentRotation)
        {
            if(!parent.Alive||parent.DivisionChild||!indices.TryGetValue(division.Child.Identity,out int childIndex))return;
            var child=world.Get(childIndex);if(!child.Alive)return;
            Pose(world,child,interpolation,out var q,out _,out float childSize);
            var delta=q-p;if(delta.sqrMagnitude<.000001f)delta=V(parent.DivisionAxis)*.001f;
            float merge=1-WaterCellPose.ShellSeparation(age);
            // The shared envelope starts as the parent's body, forms two lobes, then a narrow neck.
            // Its endpoints follow the true pair. Permanent connections are drawn by BondParent.
            int form=(int)CrystalForms.From(parent.Biology.Genes);
            var envelope=detailed?membranes[form]:farMembranes[form];
            float pinch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.82f,age));
            var scale=new Vector3((delta.magnitude+Mathf.Max(size,childSize))/(1+pinch*.38f),Mathf.Max(size,childSize),Mathf.Max(size,childSize));
            var facing=Quaternion.Slerp(parentRotation,Quaternion.FromToRotation(Vector3.right,delta),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.6f,age)));
            // Preserve the parent's inherited outline at onset instead of replacing an amoeba by a sphere.
            envelope.Opacity=merge;envelope.Fission=pinch;
            envelope.Add((p+q)*.5f,facing,scale,membrane,traits);
            cytoplasm[form].Opacity=merge;cytoplasm[form].Fission=pinch;
            cytoplasm[form].Add((p+q)*.5f,facing,scale*.84f,new Color(.16f,.30f,.38f,.20f),new Vector4(0,0,0,-2));
            envelope.Fission=cytoplasm[form].Fission=0;envelope.Opacity=cytoplasm[form].Opacity=1;
        }
        static Vector3 Fibonacci(int i,int count)
        {float y=1-2*(i+.5f)/count,r=Mathf.Sqrt(1-y*y),angle=i*2.39996f;return new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);}
        void DrawInterior(VolumeWorld world,VolumeAgent agent,CrystalForm form,Vector3 p,Quaternion rotation,float size,Vector4 traits,float phase)
        {
            var g=agent.Biology.Genes;
            Vector3 nucleus=Deform(CrystalMeshes.Point(form,new Vector3(-.045f,.015f,.015f)),cellPose)*size;
            bodies.Add(p+rotation*(nucleus+Vector3.up*.04f*size),rotation,Vector3.one*.085f*size,new Color(.72f,.43f,.78f));
            Vector3 envelope=form==CrystalForm.Diatom?new Vector3(.44f,.16f,.27f):form==CrystalForm.Spindle?new Vector3(.41f,.23f,.27f):new Vector3(.39f,.32f,.37f);
            vesicles.Add(p+rotation*nucleus,rotation,envelope*size,new Color(.73f,.63f,.91f,.16f));
            for(int band=0;band<3;band++)for(int j=0;j<9;j++) {
                float a=j*.43f+band*.4f,b=(j+1)*.43f+band*.4f,r=.17f+band*.025f;
                Vector3 aa=new Vector3(Mathf.Cos(a)*r,-.08f+band*.055f,Mathf.Sin(a)*r);
                Vector3 bb=new Vector3(Mathf.Cos(b)*r,-.08f+band*.055f,Mathf.Sin(b)*r);
                Segment(p+rotation*Deform(CrystalMeshes.Point(form,aa),cellPose)*size,p+rotation*Deform(CrystalMeshes.Point(form,bb),cellPose)*size,.012f*size,new Color(.43f,.64f,.72f));
            }
            // Intracellular granules are anatomy only: never counted or presented as edible resources.
            // A stable, bounded distribution creates internal depth without changing the organism's genes.
            for(int granule=0;granule<36;granule++) {
                float radius=.22f+.17f*Mathf.Repeat(granule*.381966f,1);
                Vector3 point=Deform(CrystalMeshes.Point(form,Fibonacci(granule,36)*radius)+new Vector3(.012f*Mathf.Sin(phase-granule),.008f*Mathf.Cos(phase*.6f+granule),0),cellPose)*size;
                float grainSize=(.025f+.018f*Mathf.Repeat(granule*.618034f,1))*size;
                Color tint=Color.Lerp(new Color(.40f,.65f,.70f),new Color(.86f,.70f,.39f),granule%4==0?1:g.OrganicSpecialization);
                bodies.Add(p+rotation*point,rotation,Vector3.one*grainSize,tint,traits);
            }
            int count=8+(int)(g.Photosynthesis*7);
            for(int j=0;j<count;j++) {
                Vector3 v=Fibonacci(j,count)*.30f+new Vector3(.03f,.012f*Mathf.Sin(phase*.12f+j),0);
                Vector3 local=Deform(CrystalMeshes.Point(form,v),cellPose)*size;
                bool plastid=g.Photosynthesis>.35f&&j%3!=0;
                Color color=plastid?Color.Lerp(new Color(.28f,.56f,.31f),new Color(.52f,.78f,.34f),Mathf.Clamp01(agent.Biology.Budget.Light*20)):new Color(.94f,.65f,.32f);
                Vector3 scale=(plastid?new Vector3(.15f,.095f,.12f):new Vector3(.09f,.1f,.09f))*size;
                if(form==CrystalForm.Diatom)scale.y*=.65f;
                bodies.Add(p+rotation*local,rotation*Quaternion.Euler(j*27,j*41,15),scale,color,traits);
                vesicles.Add(p+rotation*local,rotation,Vector3.one*.19f*size,new Color(.72f,.85f,1,.12f));
                Vector3 anchor=Deform(CrystalMeshes.Point(form,v*.4f),cellPose)*size;
                Segment(p+rotation*anchor,p+rotation*local,.009f*size,new Color(.62f,.8f,1));
            }
        }
        void DrawCilia(CrystalForm form,Vector3 p,Quaternion rotation,float size,float phase,float drive,Color color)
        {
            int rings=form==CrystalForm.Ciliate?8:3,around=form==CrystalForm.Ciliate?14:10;
            for(int ring=0;ring<rings;ring++)for(int j=0;j<around;j++) {
                float x=-.75f+1.5f*(ring+.5f)/rings,radial=Mathf.Sqrt(1-x*x),angle=j*Mathf.PI*2/around;
                Vector3 n=new Vector3(x,Mathf.Cos(angle)*radial,Mathf.Sin(angle)*radial);
                Vector3 root=Deform(CrystalMeshes.Point(form,n*.5f),cellPose);
                float wave=phase-ring*.72f-j*.35f;
                float stroke=Mathf.Sin(wave+.45f*Mathf.Sin(wave));
                float reach=(.045f+.13f*drive)*cellPose.Activity;
                Vector3 tip=root+n*(.04f+reach*(.65f+.35f*Mathf.Cos(wave)));
                Vector3 bend=(Vector3.right-n*n.x)*stroke*reach*(1+cellPose.Turn*n.z*.25f);
                Vector3 first=Vector3.Lerp(root,tip,.34f)+bend*.25f;
                Vector3 second=Vector3.Lerp(root,tip,.72f)+bend*.75f;tip+=bend;
                Segment(p+rotation*root*size,p+rotation*first*size,.007f*size,color);
                Segment(p+rotation*first*size,p+rotation*second*size,.0045f*size,color);
                Segment(p+rotation*second*size,p+rotation*tip*size,.0025f*size,color);
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
        void Pose(VolumeWorld world,VolumeAgent a,float t,out Vector3 p,out Quaternion rotation,out float size)
        {
            float blend=Mathf.Clamp01((float)((world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds-a.RenderTime)/System.Math.Max(.05,a.RenderDuration)));
            p=V(V3.Lerp(a.Previous,a.Position,blend));Vector3 forward=V(V3.Lerp(a.PreviousDirection,a.Direction,blend)).normalized;
            rotation=Quaternion.LookRotation(forward,Mathf.Abs(forward.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
            double time=System.Math.Max(0,world.Time-VolumeWorld.StepSeconds+t*VolumeWorld.StepSeconds);
            size=a.Biology.Genes.Size*(a.Biology.Generation==0?1:Mathf.Lerp(.72f,1,Mathf.Clamp01(a.Biology.Growth)))*(1+.008f*Mathf.Sin((float)time*1.8f+a.Biology.Id%997));
            if(Division(a,out var division)&&PairAlive(world,division)&&time-a.DivisionAt>=0&&time-a.DivisionAt<VolumeWorld.DivisionSeconds) {
                float age=Mathf.Clamp01((float)((time-a.DivisionAt)/VolumeWorld.DivisionSeconds));
                float emerge=WaterCellPose.Separation(age);
                p=Vector3.Lerp(V(a.DivisionOrigin),p,emerge);
                size*=a.DivisionChild?Mathf.Lerp(.78f,1,emerge):1+.12f*Mathf.Sin(age*Mathf.PI);
                Vector3 axis=V(a.DivisionAxis);
                var divisionRotation=Quaternion.LookRotation(axis,Mathf.Abs(axis.y)>.98f?Vector3.forward:Vector3.up)*Quaternion.Euler(0,-90,0);
                float alignment=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.2f,age))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,age)));
                rotation=Quaternion.Slerp(rotation,divisionRotation,alignment);
            }

        }
        public int Pick(VolumeWorld world,Ray ray,float interpolation,int slice=int.MinValue)
        {
            int selected=-1;float closest=36;
            for(int i=0;i<world.Count;i++) {
                var a=world.Get(i);if(slice!=int.MinValue&&ChunkKey.At(a.Position).Y!=slice)continue;if(!a.Alive)continue;Pose(world,a,interpolation,out var p,out var rotation,out float size);
                WaterCellPose pose=default;if(states.TryGetValue(a.Identity,out var visual)){rotation=visual.Rotation;pose=visual.Pose;}
                Vector3 offset=ray.origin-p;
                if(float.IsPositiveInfinity(Observation.RaySphere(offset.x,offset.y,offset.z,ray.direction.x,ray.direction.y,ray.direction.z,CrystalForms.BoundingRadius*size*1.4f)))continue;
                Quaternion inverse=Quaternion.Inverse(rotation);Vector3 origin=inverse*offset/size,direction=inverse*ray.direction;
                int form=(int)CrystalForms.From(a.Biology.Genes);var vertices=pickVertices[form];var triangles=pickTriangles[form];
                for(int j=0;j<triangles.Length;j+=3) {
                    Vector3 v0=Deform(vertices[triangles[j]],pose),edge1=Deform(vertices[triangles[j+1]],pose)-v0,edge2=Deform(vertices[triangles[j+2]],pose)-v0;
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

