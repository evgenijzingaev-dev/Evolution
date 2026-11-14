using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Evolution.Volume;
using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Evolution.Runtime
{
    // Dormant in ordinary games. No alternate renderer, fixture organisms or biology changes.
    public sealed partial class WaterVisualPreview : MonoBehaviour
    {
        const int Width = 1920, Height = 1080;
        static string destination;
        WaterPrototype water;
        bool failed,navigationChecked;
        RenderTexture target;
        int renderedFrames;
        readonly List<Shot> shots = new List<Shot>();

        [Serializable] public sealed class Shot
        {
            public string file;
            public Vector3 cameraPosition, cameraEuler;
            public long tick;
            public int visibleOrganisms, cameraFrames;
            public bool analytical,fullDepth,depthColours;
            public int organismMarkers,regionClusters,activePopulation,knownRegions,surveyLayer,sliceIndex,selectedOrganism,selectedLineage;
            public float markerMinY,markerMaxY;
            public float nearestDepth, farthestDepth, projectedCoverage, luminanceRange, luminanceDeviation;
            public bool currents, temperature, food, hudVisible;
            public int width=Width,height=Height,hudDetail,hudPage,threatIndex=-1,foodIndex=-1,notificationCount;
            public bool friendlyHud,layoutFits;
            public bool threatMarkerVisible;
            public float threatConnectorLength;
            public string divisionBlock;
            public float temperatureC, currentSpeed, foodSubstrate, foodOrganic;
        }
        [Serializable] sealed class Report
        {
            public string unity, pipeline, graphicsApi, graphicsDevice, graphicsVendor, graphicsVersion, revision;
            public string scene = "WaterPrototype3D";
            public int width = Width, height = Height, seed = 42;
            public bool passed,navigationChecked;
            public Shot[] screenshots;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-evolution-visual-preview");
            if (at < 0) return;
            if (at + 1 >= args.Length || args[at + 1].StartsWith("-", StringComparison.Ordinal)) {
                Application.Quit(1); return;
            }
            // Xvfb and service-owned GPU displays need no keyboard focus to advance the game loop.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            destination = Path.GetFullPath(args[at + 1]);
            Debug.Log("Visual Preview phase: bootstrap");
            SceneManager.sceneLoaded += PrepareScene;
        }
        static void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            // The verification player retains CellPrototype as its ordinary
            // startup scene. Only an explicit preview request enters the 3D game.
            if (scene.name != "WaterPrototype3D") {
                if (!Application.CanStreamedLevelBeLoaded("WaterPrototype3D")) {
                    SceneManager.sceneLoaded -= PrepareScene;
                    Debug.LogError("Visual Preview: WaterPrototype3D is missing from the build");
                    Application.Quit(1); return;
                }
                SceneManager.LoadScene("WaterPrototype3D", LoadSceneMode.Single);
                return;
            }
            SceneManager.sceneLoaded -= PrepareScene;
            var prototype = UnityEngine.Object.FindFirstObjectByType<WaterPrototype>();
            if (scene.name != "WaterPrototype3D" || !prototype) { Application.Quit(1); return; }
            // sceneLoaded runs before Start: never read/write a developer's region archive.
            prototype.PersistRegions = false;
            prototype.Seed = 42;
            prototype.SoundVolume = 0;
            var runner = new GameObject("Opt-in visual preview").AddComponent<WaterVisualPreview>();
            runner.water = prototype;
            Debug.Log("Visual Preview phase: scene ready");
        }
        void OnEnable() { Application.logMessageReceived += CheckLog; RenderPipelineManager.endCameraRendering += CameraRendered; }
        void OnDisable()
        {
            Application.logMessageReceived -= CheckLog;
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            if (water && water.Lens) water.Lens.targetTexture = null;
            if (target) RenderTexture.ReleaseTemporary(target);
        }
        void CameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (water && camera == water.Lens) renderedFrames++;
        }
        void CheckLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
                failed = true; Application.Quit(1);
            }
        }
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Visual Preview: " + message);
        }
        IEnumerator Start()
        {
            yield return null;
            water.Overview.LeaveForLegacy();
            water.Rig.Restore(new WaterView {Focus=water.Rig.Focus,Distance=7,Yaw=0,Pitch=15});
            Debug.Log("Visual Preview phase: game loop running");
            Require(Application.unityVersion == "6000.6.9f1", "wrong Unity version");
            Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "URP is not active");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.graphicsShaderLevel >= 45,
                "no usable graphics device (never run the player with -nographics)");
            foreach (var name in new[] { "Evolution/VolumeBody", "Evolution/VolumeMembrane", "Evolution/VolumeSuspension", "Evolution/VolumeBackdrop", "Evolution/SurveyMarker" }) {
                var shader = Shader.Find(name);
                Require(shader && shader.isSupported, "unsupported or missing shader: " + name);
            }
            Directory.CreateDirectory(destination);
            // Read the actual URP camera output, independent of X11 backbuffer/frontbuffer behavior.
            // No manual Camera.Render call: the ordinary player loop drives URP and post processing.
            target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            water.Lens.targetTexture = target;
            // Let the real simulation and procedural presentation initialize, then freeze its actual state.
            while (water.World == null || water.World.Tick < 20) yield return null;
            Debug.Log("Visual Preview phase: simulation initialized");
            water.Paused = true;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-evolution-ecology")>=0) {
                yield return CaptureNicheEcology();Application.Quit(failed?1:0);yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-evolution-living-cells")>=0) {
                yield return CaptureLivingCells();Application.Quit(failed?1:0);yield break;
            }
            int selected = water.ScenicSelection(); float nearest;
            Require(selected >= 0, "no living organism");
            water.Select(selected);
            string identity = water.World.Get(selected).Identity;
            var focus = ToVector(water.World.Get(selected).Position);

            float openingYaw = 25, openingScore = -1;
            for(int angle=0;angle<360;angle+=15) {
                SetView(focus,10,angle,8,false,false,false);
                var view=Measure("opening-probe");
                float side=Mathf.Abs(Vector3.Dot(water.Lens.transform.forward,ToVector(water.World.Get(selected).Direction).normalized));
                float score=view.projectedCoverage*100+view.visibleOrganisms*.25f+(view.farthestDepth>20?.5f:0)+(side<.6f?.5f:0);
                if(score>openingScore){openingScore=score;openingYaw=angle;}
            }
            SetView(focus, 10, openingYaw, 8, false, false, false);
            yield return Capture("01-ocean");
            water.Select(water.Ocean.Find(identity));
            var direction=ToVector(water.World.Get(water.Ocean.Find(identity)).Direction);
            float profileHeading=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg+90;
            SetView(focus, 3.6f, profileHeading, 10, false, false, false, true);
            yield return Capture("02-cell-close");

            // Find a view of existing organisms with actual camera-space depth separation.
            float best = -1, bestYaw = 0;
            for (int angle = 0; angle < 360; angle += 15) {
                SetView(focus, 20, angle, 12, false, false, false);
                var measure = Measure("probe");
                if (measure.visibleOrganisms < 3 || measure.farthestDepth - measure.nearestDepth < 3) continue;
                float score = measure.projectedCoverage*100+measure.visibleOrganisms*.25f+Mathf.Min(15,measure.farthestDepth-measure.nearestDepth)*.05f;
                if (score > best) { best = score; bestYaw = angle; }
            }
            SetView(focus, 20, bestYaw, 12, false, false, false);
            yield return Capture("03-depth-ensemble");
            SetView(focus, 16, bestYaw, 12, true, false, false);
            yield return Capture("04-currents");

            int food = -1; nearest = float.PositiveInfinity;
            for (int k = 0; k < water.World.FoodCount; k++) {
                var f = water.World.GetFood(k); float distance = (ToVector(f.Position) - focus).sqrMagnitude;
                if (f.Substrate + f.Organic > .1f && distance < nearest) { food = k; nearest = distance; }
            }
            Require(food >= 0, "no actual food resources");
            SetView(ToVector(water.World.GetFood(food).Position), 13, bestYaw + 35, 20, false, true, true);
            yield return Capture("05-temperature-food");

            Require(!failed && shots.Count == 5, "incomplete capture");
            // A separate evidence frame for the water itself; not an invented population or backdrop fixture.
            water.OrganismsVisible = false;
            SetView(focus, 23, 25, 22, false, false, false);
            yield return Capture("06-water-only", false);
            water.OrganismsVisible=true;water.HudVisible=true;
            var overview=water.Overview;overview.Enter();overview.SetMode(false);overview.AutoFrame=false;overview.AutoOrbit=false;
            overview.Layer=SurveyLayer.Lineage;overview.FullDepth=true;water.Rig.ApplyImmediate();
            CheckNavigation(identity);
            yield return Capture("07-cinematic-overview");
            var activeFocus=water.Rig.Focus;
            overview.SetMode(true);overview.AutoFrame=false;overview.AutoOrbit=false;
            water.Rig.FocusOn(activeFocus,72);water.Rig.ApplyImmediate();
            yield return Capture("08-analytical-overview");
            overview.Layer=SurveyLayer.Density;water.Rig.FocusOn(activeFocus,100);water.Rig.Orbit(15,25);water.Rig.ApplyImmediate();
            yield return Capture("09-density-map");
            overview.Layer=SurveyLayer.Lineage;overview.DepthColours=true;water.Rig.FocusOn(activeFocus,75);water.Rig.Orbit(-15,-25);water.Rig.ApplyImmediate();
            yield return Capture("10-depth-distribution");
            overview.DepthColours=false;overview.SetMode(false);overview.AutoOrbit=false;overview.AutoFrame=false;
            int chosen=water.Ocean.Find(identity);Require(chosen>=0,"selected actual organism left the active region");
            overview.ChooseOrganism(chosen,false);overview.ZoomSelection();water.Rig.ApplyImmediate();
            yield return Capture("11-selected-organism");
            overview.SetMode(true);overview.AutoOrbit=false;overview.AutoFrame=false;overview.DepthColours=true;
            overview.SetSlice(false,ChunkKey.At(ToV3(activeFocus)).Y);water.Rig.FocusOn(activeFocus,70);water.Rig.ApplyImmediate();
            yield return Capture("12-horizontal-slice");
            Require(!failed&&shots.Count==11,"incomplete strategic capture");
            overview.FullDepth=true;overview.DepthColours=false;
            chosen=water.Ocean.Find(identity);Require(chosen>=0,"HUD organism missing");
            water.Select(chosen);water.Hud.SetPage(HudPage.Cell);water.Hud.Detail=HudDetail.Normal;
            water.HudVisible=true;water.FindFood=true;water.ShowTemperature=water.ShowCurrents=false;
            water.Rig.ApplyImmediate();water.Hud.RefreshNow();
            yield return Capture("13-cell-play-ui");
            water.Rig.FocusOn(ToVector(water.World.Get(chosen).Position),3.6f);water.Rig.ApplyImmediate();
            yield return Capture("14-cell-info-ui");
            water.Hud.SetPage(HudPage.Overview);overview.AutoOrbit=false;overview.AutoFrame=false;water.Rig.ApplyImmediate();
            yield return Capture("15-cinematic-ui");
            overview.SetMode(true);overview.AutoFrame=false;overview.AutoOrbit=false;overview.Layer=SurveyLayer.Density;
            water.Rig.FocusOn(water.Rig.Focus,72);water.Rig.ApplyImmediate();water.Hud.Detail=HudDetail.Observer;
            yield return Capture("16-analytical-ui");
            // Find a factual threat. If necessary advance the unchanged model, with a bounded
            // search; never create, move, feed or mutate an organism for the evidence.
            int danger=FindThreat();
            for(int step=0;danger<0&&step<400;step++){
                water.World.Step();
                if(step%20==19){danger=FindThreat();yield return null;}
            }
            Require(danger>=0,"no factual threat for HUD regression capture");water.Select(danger);
            water.Hud.SetPage(HudPage.Cell);water.Hud.Detail=HudDetail.Normal;water.Hud.RefreshNow();
            water.Observe();
            var threatPoint=ToVector(water.World.Get(water.Hud.Readout.Threat).Position);
            // Frame the existing predator itself, without changing model state.
            water.Rig.FocusOn(threatPoint,7);
            water.Rig.ApplyImmediate();
            yield return Capture("17-danger-food-ui");
            water.Hud.Detail=HudDetail.Scientific;
            yield return Capture("18-scientific-ui");
            water.Hud.Detail=HudDetail.Normal;
            Screen.SetResolution(1366,768,FullScreenMode.Windowed);
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            Require(Screen.width==1366&&Screen.height==768,"1366 framebuffer resize failed");
            yield return Capture("19-ui-1366x768");
            Screen.SetResolution(2560,1440,FullScreenMode.Windowed);
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            Require(Screen.width==2560&&Screen.height==1440,"1440p framebuffer resize failed");
            yield return Capture("20-ui-2560x1440");
            Screen.SetResolution(Width,Height,FullScreenMode.Windowed);
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            water.Hud.SetPage(HudPage.Development);
            yield return Capture("21-development-ui");
            Screen.SetResolution(1366,768,FullScreenMode.Windowed);
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            yield return Capture("22-development-1366x768");
            water.Hud.SetPage(HudPage.Cell);water.Observe();
            water.Rig.FocusOn(threatPoint,7);water.Rig.ApplyImmediate();
            // The selected cell still senses the real threat, now behind the camera.
            // Look away from the focus instead of relocating any organism.
            var away=water.Rig.View;away.Focus=threatPoint-water.Lens.transform.forward*14;
            away.Yaw+=180;away.Pitch=-away.Pitch;water.Rig.Restore(away);water.Rig.ApplyImmediate();
            yield return Capture("23-offscreen-threat-1366x768");
            int safe=-1;
            for(int i=0;i<water.World.Count;i++)if(water.World.Get(i).Alive&&PlayerReadout.Read(water.World,i).Threat<0){safe=i;break;}
            Require(safe>=0,"no real threat-free resident for HUD regression");
            water.Select(safe);water.Hud.SetPage(HudPage.Cell);water.Observe();water.Hud.RefreshNow();water.Rig.ApplyImmediate();
            yield return Capture("24-no-threat-1366x768");
            Screen.SetResolution(Width,Height,FullScreenMode.Windowed);
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            Require(!failed&&shots.Count==23,"incomplete friendly HUD capture");
            File.WriteAllText(Path.Combine(destination,"manifest.json"),JsonUtility.ToJson(Context(shots.ToArray(),true),true));
            yield return CaptureDivision();
            Application.Quit(0);
        }
        [Serializable] sealed class DivisionEvidence
        {
            public string unity,pipeline,parent,child,revision;
            public long birthTick;
            public int lineage,generation,mutationMask;
            public float parentBefore,parentAfter,childEnergy,divisionCost,construction;
            public bool paid,inheritance,independent;
        }
        IEnumerator CaptureDivision(bool showHud=true)
        {
            water.Hud.SetPage(HudPage.Cell);water.Hud.Detail=HudDetail.Normal;
            water.OrganismsVisible=true;water.Paused=true;
            // Find an existing resident able to earn a surplus at its real food cloud.
            // No fixtures, teleports, genome edits or energy/resource injection.
            int candidate=-1;float best=-1;
            for(int i=0;i<water.World.Count;i++) {
                var a=water.World.Get(i);if(!a.Alive||a.Biology.Generation>0)continue;
                for(int k=0;k<water.World.FoodCount;k++) {
                    var f=water.World.GetFood(k);float d=(f.Position-a.Position).Length;
                    float gain=water.World.FoodPotential(k,a.Biology)-Biology.Cost(a.Biology.Genes,water.World.Temperature(a.Position));
                    float score=gain/(1+d);if(score>best){best=score;candidate=i;}
                }
            }
            Require(candidate>=0,"no feeding resident for paid division");water.Select(candidate);water.PlaySelected();
            string parent=water.World.Get(candidate).Identity;
            int ticks=0;
            while(ticks++<2400) {
                candidate=water.Ocean.Find(parent);Require(candidate>=0,"division resident streamed away");
                if(water.World.DivisionBlock(candidate)==null)break;
                var a=water.World.Get(candidate);Require(a.Alive,"division resident died before provisioning");
                var read=PlayerReadout.Read(water.World,candidate);
                Require(read.Food>=0,"division resident has no sensed edible resources");
                var food=water.World.GetFood(read.Food);V3 delta=food.Position-a.Position;
                water.World.Input=delta.Length>food.Radius*.45f?delta.Unit:default;
                water.World.Step();if(ticks%20==0)yield return null;
            }
            Require(water.World.DivisionBlock(candidate)==null,"resident did not earn division energy");
            water.World.Input=default;
            var before=water.World.Get(candidate);var focus=ToVector(before.Position);
            float heading=Mathf.Atan2(before.Direction.X,before.Direction.Z)*Mathf.Rad2Deg+90;
            SetView(focus,4.5f,heading,12,false,false,false,true);water.HudVisible=showHud;water.Hud.RefreshNow();
            yield return Capture("division-before",false);
            candidate=water.Ocean.Find(parent);Require(candidate>=0,"division parent missing after camera move");
            Require(water.World.RequestDivision(),"paid division request rejected");water.World.Step();
            int childIndex=water.Ocean.Find(water.World.LastOffspringIdentity);
            Require(childIndex>=0,"model did not create a daughter");
            var daughter=water.World.Get(childIndex);string childIdentity=daughter.Identity;var after=water.World.Get(candidate);
            var evidence=new DivisionEvidence {unity=Application.unityVersion,pipeline=GraphicsSettings.currentRenderPipeline.GetType().Name,
                revision=Context(new Shot[0],true).revision,parent=parent,child=daughter.Identity,birthTick=water.World.Tick,
                lineage=daughter.Biology.LineageId,generation=daughter.Biology.Generation,mutationMask=daughter.Biology.BirthMutationMask,
                parentBefore=after.Biology.Budget.Start,parentAfter=after.Biology.Energy,childEnergy=daughter.Biology.Energy,
                divisionCost=after.Biology.Budget.Division,construction=Biology.StructureEnergy(daughter.Biology.Genes)+Biology.BirthOverhead(daughter.Biology.Genes),
                paid=after.Biology.Budget.Division>daughter.Biology.Energy,
                inheritance=daughter.Biology.ParentId==after.Biology.Id&&daughter.Biology.LineageId==after.Biology.LineageId&&daughter.Biology.ParentGenes.Equals(after.Biology.Genes)};
            Require(evidence.paid&&evidence.inheritance,"division model accounting/inheritance failed");
            V3 childStart=daughter.Position;
            // Every PNG is an ordinary rendered frame after exactly one real fixed model tick.
            for(int frame=0;frame<49;frame++) {
                water.Hud.RefreshNow();
                yield return Capture("division-"+frame.ToString("D3"),false);
                water.World.Step();
            }
            childIndex=water.Ocean.Find(childIdentity);Require(childIndex>=0,"daughter missing after capture");
            evidence.independent=(water.World.Get(childIndex).Position-childStart).Length>.01f;
            Require(evidence.independent&&water.World.Get(childIndex).Alive,"daughter did not begin independent life");
            File.WriteAllText(Path.Combine(destination,"division.json"),JsonUtility.ToJson(evidence,true));
        }
        int FindThreat()
        {
            for(int i=0;i<water.World.Count;i++)if(water.World.Get(i).Alive&&PlayerReadout.Read(water.World,i).Threat>=0)return i;
            return -1;
        }
        void CheckNavigation(string identity)
        {
            var saved=water.Rig.View;var facing=water.Lens.transform.forward;
            water.Rig.Orbit(360,0);water.Rig.ApplyImmediate();Require(Vector3.Dot(facing,water.Lens.transform.forward)>.9999f,"360-degree orbit changed orientation");
            water.Rig.Restore(saved);water.Rig.ApplyImmediate();
            int index=water.Ocean.Find(identity);Require(index>=0,"navigation target missing");
            water.Overview.SetSlice(false,999);water.Overview.ChooseOrganism(index,false);
            Require(water.Overview.SliceIndex==ChunkKey.At(water.World.Get(index).Position).Y,"selection did not switch to the target depth slice");
            water.Overview.ZoomSelection();water.Rig.ApplyImmediate();
            Require(water.Rig.Distance<=8,"organism zoom did not approach");
            water.Overview.Back();water.Rig.ApplyImmediate();
            Require(Mathf.Abs(water.Rig.Distance-saved.Distance)<.001f&&Vector3.Distance(water.Rig.Focus,saved.Focus)<.001f,"previous camera view was not restored");
            Require(!water.Overview.FullDepth&&water.Overview.SliceIndex==999,"previous depth slice was not restored");
            Require(water.PlaySelected()&&!water.Overview.Active&&water.World.Controlled==index,"cell play control was not preserved");
            water.Overview.Back();water.Rig.ApplyImmediate();
            Require(water.World.Controlled<0&&water.Overview.Active,"return from cell play failed");
            water.Overview.FullDepth=true;navigationChecked=true;
        }
        static V3 ToV3(Vector3 p)=>new V3(p.x,p.y,p.z);
        Report Context(Shot[] frames, bool passed)
        {
            return new Report {
                unity = Application.unityVersion, pipeline = GraphicsSettings.currentRenderPipeline.GetType().Name,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsVendor = SystemInfo.graphicsDeviceVendor, graphicsVersion = SystemInfo.graphicsDeviceVersion,
                revision = Environment.GetEnvironmentVariable("EVOLUTION_PREVIEW_SHA") ?? "local",navigationChecked=navigationChecked,
                screenshots = frames, passed = passed
            };
        }
        static Vector3 ToVector(V3 p) => new Vector3(p.X, p.Y, p.Z);
        void SetView(Vector3 point, float distance, float heading, float tilt, bool currents, bool temperature, bool food, bool follow = false)
        {
            if (!follow) water.Observe();
            water.HudVisible = currents || temperature || food;
            water.ShowCurrents = currents; water.ShowTemperature = temperature; water.FindFood = food;
            water.Rig.FocusOn(point, distance);
            var view=water.Rig.View;water.Rig.Orbit(heading-view.Yaw,tilt-view.Pitch);
            water.Rig.ApplyImmediate();
        }
        Shot Measure(string file)
        {
            var shot = new Shot { file = file, tick = water.World.Tick, cameraPosition = water.Lens.transform.position,
                cameraEuler = water.Lens.transform.eulerAngles, nearestDepth = float.PositiveInfinity,
                currents = water.ShowCurrents, temperature = water.ShowTemperature, food = water.FindFood,
                hudVisible = water.HudVisible, width=Screen.width,height=Screen.height,
                friendlyHud=water.HudVisible&&water.Hud!=null,layoutFits=water.Hud!=null&&water.Hud.LayoutFits,
                hudDetail=(int)water.Hud.Detail,hudPage=(int)water.Hud.Page,threatIndex=water.Hud.Readout.Threat,
                foodIndex=water.Hud.Readout.Food,notificationCount=water.Hud.NotificationCount,
                threatMarkerVisible=water.Hud.ThreatMarkerVisible,threatConnectorLength=water.Hud.ThreatConnectorLength,
                divisionBlock=water.Paused?"Продолжи симуляцию":water.World.DivisionBlock(water.SelectedIndex) };
            for (int i = 0; i < water.World.Count; i++) {
                var a = water.World.Get(i); if (!a.Alive) continue;
                var v = water.Lens.WorldToViewportPoint(ToVector(a.Position));
                if (v.z < 1 || v.z > 30 || v.x < .05f || v.x > .95f || v.y < .05f || v.y > .95f) continue;
                shot.projectedCoverage += a.Biology.Genes.Size*a.Biology.Genes.Size/(v.z*v.z);
                shot.visibleOrganisms++; shot.nearestDepth = Mathf.Min(shot.nearestDepth, v.z);
                shot.farthestDepth = Mathf.Max(shot.farthestDepth, v.z);
            }
            if (shot.visibleOrganisms == 0) shot.nearestDepth = 0;
            var point = water.Rig.Focus;
            shot.temperatureC = water.World.Temperature(new V3(point.x, point.y, point.z));
            shot.currentSpeed = water.World.Current(new V3(point.x, point.y, point.z)).Length;
            for (int k = 0; k < water.World.FoodCount; k++) {
                var f = water.World.GetFood(k);
                if ((ToVector(f.Position) - point).sqrMagnitude < 100) { shot.foodSubstrate += f.Substrate; shot.foodOrganic += f.Organic; }
            }
            if(water.Overview.Active) {
                var overview=water.Overview;shot.analytical=overview.Analytical;shot.fullDepth=overview.FullDepth;shot.depthColours=overview.DepthColours;
                shot.organismMarkers=overview.VisibleMarkers;shot.regionClusters=overview.VisibleClusters;shot.activePopulation=overview.Survey.ActiveLiving;
                shot.knownRegions=overview.Survey.Regions.Count;shot.surveyLayer=(int)overview.Layer;shot.sliceIndex=overview.SliceIndex;
                shot.selectedOrganism=overview.HasSelectedOrganism?water.World.Get(water.SelectedIndex).Biology.Id:0;shot.selectedLineage=overview.SelectedLineage;
                shot.markerMinY=float.PositiveInfinity;shot.markerMaxY=float.NegativeInfinity;
                foreach(var a in overview.Survey.Agents) {
                    if(!overview.FullDepth&&ChunkKey.At(a.Position).Y!=overview.SliceIndex)continue;
                    var v=water.Lens.WorldToViewportPoint(ToVector(a.Position));if(v.z<=0||v.x<0||v.x>1||v.y<0||v.y>1)continue;
                    shot.markerMinY=Mathf.Min(shot.markerMinY,a.Position.Y);shot.markerMaxY=Mathf.Max(shot.markerMaxY,a.Position.Y);
                }
                if(float.IsInfinity(shot.markerMinY))shot.markerMinY=shot.markerMaxY=0;
            }
            return shot;
        }
        IEnumerator Capture(string name,bool record=true)
        {
            // Ordinary URP camera frames, including post effects and 3D diagnostic layers.
            // WaitForEndOfFrame is deliberately used in a windowed player, never a batch-mode Editor.
            Debug.Log("Visual Preview phase: rendering " + name);
            // Diagnostic views include the existing Russian IMGUI panel from the real game framebuffer.
            // Natural views read the camera target directly, without UI or any image compositing.
            bool includeHud = water.HudVisible;
            water.Lens.targetTexture = includeHud ? null : target;
            int before = renderedFrames;
            for (int frame = 0; frame < (water.Overview.Active?8:4); frame++) yield return new WaitForEndOfFrame();
            Require(renderedFrames - before >= 2, "URP camera did not render");
            Texture2D image;
            if (includeHud) {
                Require(Screen.width >= 1280 && Screen.height >= 720, "unexpected game framebuffer resolution");
                image = ScreenCapture.CaptureScreenshotAsTexture();
                Require(image && image.width == Screen.width && image.height == Screen.height, "missing game framebuffer");
            } else {
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    image.Apply(false, false);
                } finally { RenderTexture.active = previous; }
            }
            var shot = Measure(name + ".png");
            shot.cameraFrames = renderedFrames - before;
            var pixels = image.GetPixels32(); double sum = 0, squares = 0; float low = 1, high = 0; int magenta = 0;
            for (int p = 0; p < pixels.Length; p += 7) {
                var c = pixels[p];
                if (c.r > 230 && c.b > 230 && c.g < 20) magenta++;
                float l = (c.r * .2126f + c.g * .7152f + c.b * .0722f) / 255;
                low = Mathf.Min(low, l); high = Mathf.Max(high, l); sum += l; squares += l * l;
            }
            int samples = (pixels.Length + 6) / 7;
            shot.luminanceRange = high - low;
            shot.luminanceDeviation = (float)Math.Sqrt(Math.Max(0, squares / samples - sum * sum / samples / samples));
            // Sparse organisms over a wide ocean have low global variance even with bright detail.
            // This rejects empty/constant output, not the artistic composition of a valid frame.
            string error = magenta >= samples / 1000 ? "magenta shader-error pixels" :
                shot.luminanceRange <= .12f || shot.luminanceDeviation <= .005f ?
                "blank/flat game framebuffer: range=" + shot.luminanceRange + ", deviation=" + shot.luminanceDeviation :
                name == "03-depth-ensemble" && (shot.visibleOrganisms < 3 || shot.farthestDepth - shot.nearestDepth < 3) ?
                "ensemble must contain three organisms at different real depths" : null;
            if(water.Overview.Active&&error==null) {
                if(!shot.hudVisible||shot.activePopulation<1||shot.knownRegions<1)error="missing actual ecosystem overview data";
                else if(name=="08-analytical-overview"&&(!shot.analytical||shot.organismMarkers<3))error="analytical markers missing";
                else if(name=="09-density-map"&&(!shot.analytical||shot.surveyLayer!=(int)SurveyLayer.Density||shot.regionClusters<3))error="density regions missing";
                else if(name=="10-depth-distribution"&&(!shot.depthColours||shot.markerMaxY-shot.markerMinY<10))error="actual vertical distribution missing";
                else if(name=="11-selected-organism"&&shot.selectedOrganism==0)error="actual selected organism missing";
                else if(name=="12-horizontal-slice"&&(shot.fullDepth||shot.organismMarkers<1||shot.markerMinY<shot.sliceIndex*20-.01f||shot.markerMaxY>(shot.sliceIndex+1)*20+.01f))error="slice contains wrong depth data";
            }
            if(record&&name.CompareTo("13-")>=0&&(!shot.friendlyHud||!shot.layoutFits))error="friendly HUD is missing or layout overlaps";
            if((name=="17-danger-food-ui"||name=="19-ui-1366x768"||name=="20-ui-2560x1440")&&
                (shot.threatIndex<0||!shot.threatMarkerVisible||Mathf.Abs(shot.threatConnectorLength-18)>.01f))error="real short threat marker missing";
            if(name=="23-offscreen-threat-1366x768"&&(shot.threatIndex<0||shot.threatMarkerVisible))error="offscreen threat must not draw a marker";
            if(name=="24-no-threat-1366x768"&&(shot.threatIndex>=0||shot.threatMarkerVisible))error="threat-free cell has false warning marker";
            if (error != null) {
                var diagnostic = Path.Combine(destination, "diagnostics"); Directory.CreateDirectory(diagnostic);
                File.WriteAllBytes(Path.Combine(diagnostic, shot.file), image.EncodeToPNG());
                File.WriteAllText(Path.Combine(diagnostic, "manifest.json"), JsonUtility.ToJson(Context(new[] { shot }, false), true));
                Destroy(image);
                Require(false, error);
            }
            File.WriteAllBytes(Path.Combine(destination, shot.file), image.EncodeToPNG());
            Destroy(image); if(record)shots.Add(shot);
            Debug.Log("Visual Preview phase: saved " + name);
        }
    }
}


