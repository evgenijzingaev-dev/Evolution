using Evolution.Core;
using Evolution.Presentation;
using Evolution.Volume;
using System.IO;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed class WaterPrototype : MonoBehaviour
    {
        public int ParticleCount=8192,Seed=42;
        public bool OrganismsVisible=true,ShowCurrents,ShowTemperature,FindFood,HudVisible=true;
        public bool PersistRegions=true;
        public bool CinematicFocus=true;
        public float SoundVolume=.2f;
        public VolumeWorld World {get;private set;}
        public Suspension Water {get;private set;}
        public WaterCamera Rig {get;private set;}
        public OceanOverview Overview {get;private set;}
        public FriendlyOceanHud Hud {get;private set;}
        Vector3? moveTarget;
        public int SelectedIndex=>selected;
        public bool Paused {get;set;}
        public Camera Lens {get;private set;}
        public ChunkOcean Ocean {get;private set;}
        VolumeAgent Selection=>selected>=0&&selected<World.Count?World.Get(selected):default;
        VolumePainter painter;CrystalWaterLook look;GameObject cameraObject;
        AudioSource ambient,effects;AudioClip ambience,feedSound;
        readonly FlowBody[] flowBodies=new FlowBody[12];readonly float[] flowDistances=new float[12];
        int selected;bool following=true;double accumulator,lastSound=double.NegativeInfinity;
        void Start()
        {
            IRegionArchive archive=PersistRegions?(IRegionArchive)new BinaryRegionArchive(Path.Combine(Application.persistentDataPath,"ExperimentalWater","v1-seed-"+Seed)):new MemoryRegionArchive();
            Ocean=new ChunkOcean(unchecked((uint)Seed),archive);World=Ocean.World;selected=World.Count>0?Ocean.Nearest(default):-1;
            selected=ScenicSelection();
            Water=new Suspension(Mathf.Clamp(ParticleCount,512,8192),unchecked((uint)Seed)^0x8913u);
            cameraObject=new GameObject("Volume water camera",typeof(Camera),typeof(AudioListener));Lens=cameraObject.GetComponent<Camera>();
            Lens.clearFlags=CameraClearFlags.SolidColor;Lens.backgroundColor=new Color(.012f,.07f,.15f);
            Rig=gameObject.AddComponent<WaterCamera>();Rig.Initialize(Lens);Rig.FocusOn(VolumePainter.V(Selection.Position));Rig.ApplyImmediate();
            look=new CrystalWaterLook(transform,Lens);painter=new VolumePainter(look);
            ambient=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();ambient.playOnAwake=effects.playOnAwake=false;
            const int rate=24000;int frames=(int)(OceanSound.LoopSeconds*rate);var samples=new float[frames*2];
            for(int i=0;i<frames;i++)for(int c=0;c<2;c++)samples[i*2+c]=OceanSound.Ambience(i/(double)rate,c);
            ambience=AudioClip.Create("Volume water ambience",frames,2,rate,false);ambience.SetData(samples,0);
            ambient.clip=ambience;ambient.loop=true;ambient.volume=0;ambient.Play();
            var pop=new float[4800];for(int i=0;i<pop.Length;i++)pop[i]=OceanSound.Interaction(i/(double)rate,.2,240,-80);
            feedSound=AudioClip.Create("Volume confirmed intake",pop.Length,1,rate,false);feedSound.SetData(pop,0);
            Water.Step(0,World.Time,VolumePainter.V(Lens.transform.position),flowBodies,0,World.FlowOffset);
            Overview=gameObject.AddComponent<OceanOverview>();Overview.Initialize(this);Observe();
            Hud=gameObject.AddComponent<FriendlyOceanHud>();Hud.Initialize(this);FindFood=true;PlaySelected();
        }
        public void Observe() {moveTarget=null;World.Controlled=-1;World.Input=default;World.Boost=World.Attack=false;following=false;}
        // Choose a real nearby resident in a naturally populated neighbourhood. No spawning,
        // repositioning, resource injection or mutation is performed for the opening view.
        public int ScenicSelection()
        {
            // Linear scan over cached regional density, then a nearest lookup; no all-pairs search.
            SurveyRegion best=null;foreach(var r in Ocean.Survey.Regions)if(r.Loaded&&r.Living>0&&r.Center.Sqr<38*38&&(best==null||r.Living>best.Living||(r.Living==best.Living&&r.Center.Sqr<best.Center.Sqr)))best=r;
            int result=World.Count>0?Ocean.Nearest(best==null?default:best.Center):-1;
            return result;
        }
        public bool PlaySelected() {if(!Selection.Alive)return false;bool overview=Overview!=null&&Overview.Active;Overview?.EnterCellPlay();World.Controlled=selected;following=true;if(overview)Rig.FocusOn(VolumePainter.V(Selection.Position),Mathf.Clamp(Selection.Biology.Genes.Size*3,2.8f,8));return true;}
        public void Select(int index)=>Choose(index,true);
        public void Choose(int index,bool zoom) {if(index<0||index>=World.Count||!World.Get(index).Alive)return;Observe();selected=index;following=zoom;if(zoom)Rig.FocusOn(VolumePainter.V(World.Get(index).Position));}
        public int PickOrganism(Vector2 screen)=>painter.Pick(World,Lens.ScreenPointToRay(screen),Mathf.Clamp01((float)(accumulator/VolumeWorld.StepSeconds)),Overview.Active&&!Overview.FullDepth?Overview.SliceIndex:int.MinValue);
        void Update()
        {
            if(World==null)return;
            bool over=HudVisible&&Hud!=null&&Hud.OverUi(Input.mousePosition);
            if(Input.GetKeyDown(KeyCode.F1))Hud.HelpVisible=!Hud.HelpVisible;
            if(Input.GetKeyDown(KeyCode.F2))Hud.CycleDetail();
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.H))HudVisible=!HudVisible;
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.P))Paused=!Paused;
            if(Input.GetKeyDown(KeyCode.Escape)){if(Hud.CloseOverlay()){}else if(Overview.Typing)Overview.ClearSearchFocus();else if(World.Controlled>=0||Overview.Active)Overview.Back();else Observe();}
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Home))Overview.Home();
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Tab)&&World.Controlled<0){if(!Overview.Active)Overview.Enter();Overview.SetMode(!Overview.Analytical);}
            else if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Tab))for(int n=1;n<=World.Count;n++) {int candidate=(selected+n)%World.Count;if(World.Get(candidate).Alive){Select(candidate);break;}}
            if(!over&&Input.GetMouseButtonDown(0)&&OrganismsVisible) {
                if(Overview.Active)Overview.Click(Input.mousePosition);
                else {int picked=PickOrganism(Input.mousePosition);if(picked>=0){Select(picked);moveTarget=null;}
                    else if(World.Controlled>=0) {var plane=new Plane(-Lens.transform.forward,VolumePainter.V(Selection.Position));var ray=Lens.ScreenPointToRay(Input.mousePosition);if(plane.Raycast(ray,out float along))moveTarget=ray.GetPoint(along);}}
            }
            World.Input=default;World.Boost=World.Attack=false;
            if(!Overview.Typing&&!Paused&&Input.GetKeyDown(KeyCode.R))World.RequestDivision();
            if(World.Controlled>=0&&!Paused&&!over&&Application.isFocused) {
                Vector3 command=Lens.transform.right*Input.GetAxisRaw("Horizontal")+Lens.transform.forward*Input.GetAxisRaw("Vertical");
                if(Input.GetKey(KeyCode.E))command+=Vector3.up;if(Input.GetKey(KeyCode.Q))command-=Vector3.up;
                if(command.sqrMagnitude>.001f)moveTarget=null;
                else if(moveTarget.HasValue){command=moveTarget.Value-VolumePainter.V(Selection.Position);if(command.sqrMagnitude<.16f){moveTarget=null;command=Vector3.zero;}}
                World.Input=VolumePainter.V(Vector3.ClampMagnitude(command,1));World.Boost=Input.GetKey(KeyCode.LeftShift);World.Attack=Input.GetKey(KeyCode.Space)||(Hud!=null&&Hud.AttackRequested);
            }
            Overview.BeforeCamera(over);
            if(!Selection.Alive)following=false;
            if(following&&OrganismsVisible&&Selection.Alive)Rig.Follow(VolumePainter.V(Selection.Position));
            Rig.Tick(over||Overview.Typing,following);
            string identity=Selection.Identity;
            if(Ocean.Stream(VolumePainter.V(Overview.Active?Rig.Focus:Lens.transform.position))) {World=Ocean.World;selected=identity!=null?Ocean.Find(identity):-1;if(selected<0){selected=World.Count>0?Ocean.Nearest(VolumePainter.V(Lens.transform.position)):-1;following=false;}}
            if(!Paused)accumulator+=Time.unscaledDeltaTime;
            int steps=0;
            while(!Paused&&accumulator>=VolumeWorld.StepSeconds&&steps++<8) {
                World.Step();int count=OrganismsVisible?NearestFlows():0;
                Water.Step(VolumeWorld.StepSeconds,World.Time,VolumePainter.V(Lens.transform.position),flowBodies,count,World.FlowOffset);accumulator-=VolumeWorld.StepSeconds;
            }
            if(Paused)Water.Step(0,World.Time,VolumePainter.V(Lens.transform.position),flowBodies,0,World.FlowOffset);
            look.SetFocus(Vector3.Distance(Lens.transform.position,Rig.Focus),CinematicFocus&&following&&OrganismsVisible);
            painter.Draw(World,Water,Lens,Mathf.Clamp01((float)(accumulator/VolumeWorld.StepSeconds)),OrganismsVisible,ShowCurrents&&HudVisible&&!Overview.Active,ShowTemperature&&HudVisible&&!Overview.Active,selected,FindFood&&HudVisible&&!Overview.Active,Overview.Active,Overview.Active&&!Overview.FullDepth?Overview.SliceIndex:int.MinValue);
            Overview.Render();
            ambient.volume=SoundVolume<=0?0:Mathf.MoveTowards(ambient.volume,Mathf.Clamp01(SoundVolume)*(Paused?.1f:.6f),Time.unscaledDeltaTime*.5f);
            effects.volume=Mathf.Clamp01(SoundVolume);
            var cell=Selection;
            if(!Paused&&cell.FedAt>lastSound) {lastSound=cell.FedAt;effects.PlayOneShot(feedSound,.18f);}
        }
        int NearestFlows()
        {
            int count=0;var eye=VolumePainter.V(Lens.transform.position);
            for(int i=0;i<World.Count;i++) {
                var a=World.Get(i);if(!a.Alive)continue;float d=(a.Position-eye).Sqr;if(count==12&&d>=flowDistances[11])continue;
                int at=Mathf.Min(count,11);while(at>0&&flowDistances[at-1]>d) {if(at<12){flowDistances[at]=flowDistances[at-1];flowBodies[at]=flowBodies[at-1];}at--;}
                flowDistances[at]=d;flowBodies[at]=new FlowBody {Position=a.Position,Direction=a.Direction,Radius=.55f*a.Biology.Genes.Size,
                    Speed=(a.Velocity-World.Current(a.Position)).Length};count=Mathf.Min(12,count+1);
            }
            return count;
        }
        void OnGUI()
        {
            if(World!=null&&HudVisible&&Hud!=null)Hud.Draw();
        }
        void OnDestroy()
        {
            painter?.Dispose();look?.Dispose();if(ambient)ambient.Stop();if(effects)effects.Stop();
            Ocean?.Save();
            Destroy(ambience);Destroy(feedSound);Destroy(cameraObject);
        }
    }
}

