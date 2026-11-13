using Evolution.Core;
using Evolution.Localization;
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
        public int SelectedIndex=>selected;
        public bool Paused {get;set;}
        public Camera Lens {get;private set;}
        public ChunkOcean Ocean {get;private set;}
        VolumeAgent Selection=>selected>=0&&selected<World.Count?World.Get(selected):default;
        VolumePainter painter;CrystalWaterLook look;GameObject cameraObject;
        AudioSource ambient,effects;AudioClip ambience,feedSound;
        readonly FlowBody[] flowBodies=new FlowBody[12];readonly float[] flowDistances=new float[12];
        int selected;bool following=true;double accumulator,lastSound=double.NegativeInfinity;
        GUISkin skin;GUIStyle panelStyle,heading,hint;Texture2D panel,button;
        bool details;
        Vector2 scroll;
        readonly int[] foodCandidates=new int[4];readonly float[] foodScores=new float[4];readonly Rect[] foodBoxes=new Rect[4];
        float Scale=>Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.7f,1.5f);
        Rect Panel=>new Rect(18,18,282,details?Mathf.Min(700,Screen.height/Scale-74):(ShowCurrents&&ShowTemperature?292:258));
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
        }
        public void Observe() {World.Controlled=-1;World.Input=default;World.Boost=World.Attack=false;following=false;}
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
            Vector2 pointer=new Vector2(Input.mousePosition.x/Scale,(Screen.height-Input.mousePosition.y)/Scale);
            bool over=HudVisible&&((Overview.Active?Overview.OverUi(Input.mousePosition):Panel.Contains(pointer)));
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.H))HudVisible=!HudVisible;
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.P))Paused=!Paused;
            if(Input.GetKeyDown(KeyCode.Escape)){if(Overview.Typing)Overview.ClearSearchFocus();else if(World.Controlled>=0||Overview.Active)Overview.Back();else Observe();}
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Home))Overview.Home();
            if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Tab)&&World.Controlled<0){if(!Overview.Active)Overview.Enter();Overview.SetMode(!Overview.Analytical);}
            else if(!Overview.Typing&&Input.GetKeyDown(KeyCode.Tab))for(int n=1;n<=World.Count;n++) {int candidate=(selected+n)%World.Count;if(World.Get(candidate).Alive){Select(candidate);break;}}
            if(!over&&Input.GetMouseButtonDown(0)&&OrganismsVisible) {
                if(Overview.Active)Overview.Click(Input.mousePosition);
                else {int picked=PickOrganism(Input.mousePosition);if(picked>=0)Select(picked);}
            }
            World.Input=default;World.Boost=World.Attack=false;
            if(World.Controlled>=0&&!Paused&&!over&&Application.isFocused) {
                Vector3 command=Lens.transform.right*Input.GetAxisRaw("Horizontal")+Lens.transform.forward*Input.GetAxisRaw("Vertical");
                if(Input.GetKey(KeyCode.E))command+=Vector3.up;if(Input.GetKey(KeyCode.Q))command-=Vector3.up;
                World.Input=VolumePainter.V(Vector3.ClampMagnitude(command,1));World.Boost=Input.GetKey(KeyCode.LeftShift);World.Attack=Input.GetKey(KeyCode.Space);
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
        void PrepareUi()
        {
            if(skin)return;skin=Instantiate(GUI.skin);skin.label.fontSize=14;skin.label.fontStyle=FontStyle.Normal;skin.label.wordWrap=true;
            skin.label.normal.textColor=new Color(.85f,.94f,.95f);
            var font=Resources.Load<Font>("WaterUI/OpenSans-Regular");skin.font=font;skin.label.font=font;skin.button.font=font;
            panel=new Texture2D(1,1);panel.SetPixel(0,0,new Color(.012f,.04f,.07f,.63f));panel.Apply();
            button=new Texture2D(1,1);button.SetPixel(0,0,new Color(1,1,1,.11f));button.Apply();
            skin.button.fontSize=13;skin.button.fontStyle=FontStyle.Normal;skin.button.padding=new RectOffset(6,6,5,5);skin.button.wordWrap=true;
            skin.button.border=new RectOffset();
            skin.button.normal.background=skin.button.hover.background=skin.button.active.background=button;
            skin.button.normal.textColor=new Color(.82f,.93f,.96f);skin.button.hover.textColor=Color.white;
            panelStyle=new GUIStyle(skin.box) {padding=new RectOffset(12,12,10,10)};panelStyle.normal.background=panel;
            heading=new GUIStyle(skin.label) {fontStyle=FontStyle.Bold,fontSize=16};
            hint=new GUIStyle(skin.label) {fontSize=12,alignment=TextAnchor.MiddleRight};hint.normal.textColor=new Color(.72f,.86f,.91f,.85f);
        }
        bool LayerButton(TextKey key,bool enabled)
        {
            Color before=GUI.backgroundColor;GUI.backgroundColor=enabled?new Color(.35f,.92f,1):new Color(.65f,.78f,.84f);
            bool clicked=GUILayout.Button(GameText.Get(key));GUI.backgroundColor=before;return clicked;
        }
        void OnGUI()
        {
            if(World==null||!HudVisible)return;if(Overview.Active){Overview.DrawUi();return;}PrepareUi();var matrix=GUI.matrix;var old=GUI.skin;GUI.skin=skin;GUI.matrix=Matrix4x4.Scale(Vector3.one*Scale);
            GUILayout.BeginArea(Panel,panelStyle);
            GUILayout.Label(GameText.Get(TextKey.WaterCompactTitle),heading);
            var agent=Selection;V3 point=OrganismsVisible&&agent.Alive?agent.Position:VolumePainter.V(Lens.transform.position);
            var sample=World.Sample(point);var current=World.Current(point);
            GUILayout.Label(GameText.Format(TextKey.WaterBrief,sample.Temperature,sample.Light,current.Length));
            GUILayout.BeginHorizontal();
            if(LayerButton(TextKey.WaterFlow,ShowCurrents))ShowCurrents=!ShowCurrents;
            if(LayerButton(TextKey.WaterHeat,ShowTemperature))ShowTemperature=!ShowTemperature;
            if(LayerButton(TextKey.WaterFood,FindFood))FindFood=!FindFood;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(Paused?TextKey.WaterRun:TextKey.Pause)))Paused=!Paused;
            if(GUILayout.Button(GameText.Get(TextKey.WaterOverview))) {Observe();Rig.FocusOn(Rig.Focus,10);}
            if(GUILayout.Button(GameText.Get(details?TextKey.WaterCollapse:TextKey.WaterDetails)))details=!details;
            GUILayout.EndHorizontal();
            if(OrganismsVisible&&selected>=0) {
                var c=agent.Biology;
                GUILayout.Label(GameText.Format(TextKey.WaterCellBrief,c.Id,c.Energy,Biology.Capacity(c.Genes)));
                if(World.Controlled>=0) {if(GUILayout.Button(GameText.Get(TextKey.ReturnObserve)))Observe();}
                else if(agent.Alive&&GUILayout.Button(GameText.Get(TextKey.PlayOrganism)))PlaySelected();
            }
            if(ShowCurrents)GUILayout.Label(GameText.Format(TextKey.VolumeVector,current.X,current.Y,current.Z));
            if(ShowTemperature) {var probe=VolumePainter.V(Rig.Focus);GUILayout.Label(GameText.Format(TextKey.VolumeProbe,World.Temperature(probe),World.Current(probe).Length));}
            if(details) {
            scroll.x=0;scroll=GUILayout.BeginScrollView(scroll,false,true,GUIStyle.none,skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(Panel.width-panelStyle.padding.horizontal-skin.verticalScrollbar.fixedWidth-8));
            if(GUILayout.Button(GameText.Get(OrganismsVisible?TextKey.VolumeWaterOnly:TextKey.VolumeLife))) {OrganismsVisible=!OrganismsVisible;if(!OrganismsVisible)Observe();}
            if(GUILayout.Button(GameText.Get(CinematicFocus?TextKey.CrystalFocusOn:TextKey.CrystalFocusOff)))CinematicFocus=!CinematicFocus;
            GUILayout.Label(GameText.Format(TextKey.VolumeSample,sample.Temperature,sample.Light,current.Length));
            if(OrganismsVisible&&selected>=0) {
                var c=agent.Biology;float budgetSeconds=(float)System.Math.Max(VolumeWorld.StepSeconds,agent.RenderDuration);
                GUILayout.Label(GameText.Format(TextKey.VolumeCell,c.Id,agent.Position.Y,c.Energy,Biology.Capacity(c.Genes)),heading);
                GUILayout.Label(GameText.Format(TextKey.VolumeIncome,c.Budget.Substrate/budgetSeconds,c.Budget.Organic/budgetSeconds,c.Budget.Light/budgetSeconds,c.Budget.Prey/budgetSeconds));
                GUILayout.Label(GameText.Format(TextKey.VolumeNet,c.Budget.Net/budgetSeconds));
                GUILayout.Label(GameText.Get(c.Genes.Photosynthesis>.5f?TextKey.VolumePhotoDiet:TextKey.VolumeDiet));
            }
            GUILayout.Label(GameText.Format(TextKey.VolumeParticles,Water.Count));
            GUILayout.Label(GameText.Format(TextKey.VolumeChunks,Ocean.LoadedCount,World.Count));
            GUILayout.Label(GameText.Get(TextKey.SoundSetting));SoundVolume=GUILayout.HorizontalSlider(SoundVolume,0,.6f);
            GUILayout.Label(GameText.Get(TextKey.VolumeScope));
            GUILayout.Label(GameText.Get(TextKey.VolumeControls));
            GUILayout.EndVertical();GUILayout.EndScrollView();}
            GUILayout.EndArea();
            if(FindFood)DrawFoodLabels(agent);
            GUI.Label(new Rect(Screen.width/Scale-448,Screen.height/Scale-34,430,24),GameText.Get(TextKey.WaterHint),hint);
            GUI.matrix=matrix;GUI.skin=old;
        }
        void DrawFoodLabels(VolumeAgent observer)
        {
            int count=0;
            for(int k=0;k<World.FoodCount;k++) {
                var food=World.GetFood(k);if(food.Substrate+food.Organic<.01f)continue;
                Vector3 s=Lens.WorldToScreenPoint(VolumePainter.V(food.Position));if(s.z<=0||s.z>30)continue;
                Rect rect=new Rect(s.x/Scale-106,(Screen.height-s.y)/Scale-42,212,106);
                if(rect.x<10||rect.xMax>Screen.width/Scale-10||rect.y<10||rect.yMax>Screen.height/Scale-44||rect.Overlaps(Panel))continue;
                float potential=World.FoodPotential(k,observer.Biology);if(potential<=0)continue;
                float score=potential/(1+(food.Position-observer.Position).Length*.1f);
                if(count==4&&score<=foodScores[3])continue;
                int at=Mathf.Min(count,3);while(at>0&&foodScores[at-1]<score){foodScores[at]=foodScores[at-1];foodCandidates[at]=foodCandidates[at-1];foodBoxes[at]=foodBoxes[at-1];at--;}
                foodScores[at]=score;foodCandidates[at]=k;foodBoxes[at]=rect;count=Mathf.Min(4,count+1);
            }
            for(int i=0;i<count;i++) {
                var food=World.GetFood(foodCandidates[i]);Rect rect=foodBoxes[i];
                GUI.Box(rect,GUIContent.none,panelStyle);
                GUI.Label(new Rect(rect.x+6,rect.y+4,rect.width-12,rect.height-8),GameText.Format(TextKey.VolumeFoodLabel,food.Substrate,food.Organic,food.Concentration,World.FoodPotential(foodCandidates[i],observer.Biology)));
            }
            int threat=-1;float nearest=Ecology.SenseRadius(observer.Biology.Genes);
            for(int i=0;i<World.Count;i++){var a=World.Get(i);float distance=(a.Position-observer.Position).Length;
                if(a.Alive&&distance<nearest&&Ecology.CanHunt(a.Biology,observer.Biology)&&
                    Ecology.Attack(a.Biology)/(observer.Biology.Genes.Size*(1+Ecology.Defense(observer.Biology,0)))>.28f){nearest=distance;threat=i;}}
            if(threat>=0){Vector3 p=Lens.WorldToScreenPoint(VolumePainter.V(World.Get(threat).Position));
                Rect tag=new Rect(p.x/Scale-75,(Screen.height-p.y)/Scale-42,150,28);
                if(p.z>0&&!tag.Overlaps(Panel)){Color previous=GUI.color;GUI.color=new Color(1,.58f,.5f);GUI.Box(tag,GameText.Format(TextKey.CrystalThreat,nearest));GUI.color=previous;}}
        }
        void OnDestroy()
        {
            painter?.Dispose();look?.Dispose();if(ambient)ambient.Stop();if(effects)effects.Stop();
            Ocean?.Save();
            Destroy(ambience);Destroy(feedSound);Destroy(cameraObject);Destroy(panel);Destroy(button);Destroy(skin);
        }
    }
}
