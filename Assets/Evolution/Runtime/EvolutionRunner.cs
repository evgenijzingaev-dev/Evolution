using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner : MonoBehaviour
    {
        public int Seed = 42;
        public bool LivingOcean = true;
        public bool DynamicEcosystem = true;
        public float OceanSpan = 120;
        public bool DetailedOrganisms = true;
        public bool HudVisible = true;
        public float SoundVolume = .3f;
        public SimulationConfig Settings = new SimulationConfig();
        Simulation simulation;
        OrganismRenderer organismRenderer;
        SimulationConfig activeSettings;
        Mesh mesh, habitat, membraneMesh;
        Material cellsMaterial, membraneMaterial;
        OceanRenderer ocean;
        OceanLook oceanLook;
        OceanAudio sound;
        Camera view;
        MicroscopeCamera rig;
        GameObject ownedCamera;
        AudioListener ownedListener;
        double accumulator, visualTime;
        bool paused;
        int colorMode = 3, selectedId;
        float speed = 1, pendingTemperature, pendingFood;
        static readonly float[] Speeds = { 1f, 2f, 5f, 10f };
        bool environmentPending;
        public Simulation World => simulation;
        public int SelectedId => selectedId;
        public EvolutionJourney Journey { get; private set; }
        public bool IsControlling => simulation != null && simulation.ControlledId != 0;
        public bool PlaySelected()
        {
            if (!simulation.TryControl(selectedId)) return false;
            Journey.Begin(simulation, selectedId); FocusSelected(true); speed = 1; return true;
        }
        public void ObserveWorld() { simulation.ReleaseControl(); rig.StopFollowing(); }
        public bool PlayOffspring()
        {
            int child = simulation.LastOffspringId;
            if (!Observation.TryFind(simulation, child, out _)) return false;
            SelectCell(child); return PlaySelected();
        }
        public MicroscopeCamera CameraRig => rig;
        public bool IsPaused { get => paused; set => paused = value; }
        void Start()
        {
            mesh = MicroMeshes.Cell(); membraneMesh = MicroMeshes.Cell(16, 24); habitat = MicroMeshes.Habitat();
            cellsMaterial = MakeMaterial("InstancedCells"); membraneMaterial = MakeMaterial("CellMembrane");
            view = Camera.main;
            if (!view) { ownedCamera = new GameObject("Microscope Camera", typeof(Camera)); view = ownedCamera.GetComponent<Camera>(); view.tag = "MainCamera"; }
            if (!FindFirstObjectByType<AudioListener>()) ownedListener = view.gameObject.AddComponent<AudioListener>();
            view.backgroundColor = new Color(.035f, .19f, .23f); view.clearFlags = CameraClearFlags.SolidColor;
            oceanLook = new OceanLook(transform, view); oceanLook.Apply(cellsMaterial); oceanLook.Apply(membraneMaterial);
            sound = new OceanAudio(transform);
            rig = gameObject.AddComponent<MicroscopeCamera>();
            ResetWorld();
        }
        static Material MakeMaterial(string resource) => new Material(Resources.Load<Shader>(resource)) { enableInstancing = true };
        public void ResetWorld()
        {
            activeSettings = Settings.Copy(); activeSettings.OceanZones = LivingOcean;
            if (LivingOcean && OceanSpan > activeSettings.WorldSize) {
                float ratio = OceanSpan / activeSettings.WorldSize;
                activeSettings.WorldSize = OceanSpan;
                activeSettings.GridSize = Mathf.Clamp(Mathf.RoundToInt(activeSettings.GridSize * ratio), 2, 128);
                if (activeSettings.InitialPopulation > 1) activeSettings.InitialPopulation = Mathf.Clamp(Mathf.RoundToInt(activeSettings.InitialPopulation * ratio * ratio),
                    activeSettings.InitialPopulation, Mathf.Max(activeSettings.InitialPopulation, activeSettings.Capacity / 2));
            }
            activeSettings.Ecosystem = DynamicEcosystem; activeSettings.NaturalEvents = DynamicEcosystem;
            simulation = new Simulation(activeSettings, unchecked((uint)Seed)); Journey = new EvolutionJourney(); accumulator = visualTime = 0;
            organismRenderer = new OrganismRenderer(mesh, cellsMaterial, membraneMaterial, activeSettings.Capacity, membraneMesh);
            ocean?.Dispose(); ocean = new OceanRenderer(activeSettings.WorldSize, activeSettings.GridSize, habitat, mesh, activeSettings.NutrientCapacity);
            selectedId = 0; rig.Initialize(view, activeSettings.WorldSize);
            pendingTemperature = activeSettings.Temperature; pendingFood = activeSettings.NutrientRegeneration; environmentPending = false;
        }
        public bool SelectCell(int id)
        {
            if (!Observation.TryFind(simulation, id, out _)) return false;
            if (IsControlling && selectedId != id) ObserveWorld();
            selectedId = id; if (!IsControlling) rig.StopFollowing(); return true;
        }
        public void ClearSelection() { ObserveWorld(); selectedId = 0; rig.StopFollowing(); }
        public void FocusSelected(bool follow)
        {
            if (Observation.TryFind(simulation, selectedId, out var c)) { OrganismFrame(c, out var center, out float radius); rig.FocusOn(center, follow, Mathf.Max(3, radius * 3.2f)); }
        }
        void OrganismFrame(Cell anchor, out Vector3 center, out float radius)
        {
            Vector3 origin = organismRenderer.Position(anchor), sum = Vector3.zero; int count = 0;
            float world = activeSettings.WorldSize;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); if (!Observation.SameOrganism(anchor, c)) continue;
                Vector3 d = organismRenderer.Position(c) - origin;
                d.x = Observation.PeriodicDelta(d.x, world); d.z = Observation.PeriodicDelta(d.z, world);
                sum += d; count++;
            }
            Vector3 mean = sum / Mathf.Max(1, count); radius = .5f;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); if (!Observation.SameOrganism(anchor, c)) continue;
                Vector3 d = organismRenderer.Position(c) - origin;
                d.x = Observation.PeriodicDelta(d.x, world); d.z = Observation.PeriodicDelta(d.z, world);
                radius = Mathf.Max(radius, (d - mean).magnitude + c.Genes.Size * .55f);
            }
            center = origin + mean; center.x = Mathf.Repeat(center.x, world); center.z = Mathf.Repeat(center.z, world);
        }
        public int Pick(Vector2 screenPoint)
        {
            Ray ray = view.ScreenPointToRay(screenPoint); float nearest = float.PositiveInfinity; int id = 0;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); Vector3 point = organismRenderer.Position(c);
                Vector3 center = rig.NearestImage(point), offset = ray.origin - center;
                float hit = Observation.RaySphere(offset.x, offset.y, offset.z, ray.direction.x, ray.direction.y, ray.direction.z, c.Genes.Size * .6f);
                if (hit < nearest) { nearest = hit; id = c.Id; }
            }
            return id;
        }
        void Update()
        {
            if (simulation == null) return;
            bool overUI = PointerOverUI(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.H)) HudVisible = !HudVisible;
            if (Input.GetKeyDown(KeyCode.Tab) && simulation.Count > 0) {
                int next = 0;
                for (int i = 0; i < simulation.Count; i++) if (simulation.GetCell(i).Id == selectedId) { next = (i + 1) % simulation.Count; break; }
                SelectCell(simulation.GetCell(next).Id); FocusSelected(false);
            }
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.F)) FocusSelected(IsControlling);
            if (Input.GetKeyDown(KeyCode.G)) FocusSelected(true);
            if (Input.GetKeyDown(KeyCode.Escape)) { if (IsControlling) ObserveWorld(); else ClearSelection(); }
            if (IsControlling && Input.GetKeyDown(KeyCode.Home)) { ObserveWorld(); rig.Overview(); }
            if (!overUI && Input.GetMouseButtonDown(0)) { int id = Pick(Input.mousePosition); if (id == 0) ClearSelection(); else SelectCell(id); }
            if (IsControlling) {
                float x = 0, z = 0;
                if (!overUI && Application.isFocused && !paused) {
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x++;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x--;
                    if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z++;
                    if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z--;
                }
                Vector3 direction = rig.ControlDirection(new Vector2(x,z)); simulation.SetControlInput(direction.x,direction.z);
                bool input = !overUI && Application.isFocused && !paused;
                simulation.SetControlActions(input && Input.GetKey(KeyCode.E), input && Input.GetKey(KeyCode.LeftShift), input && Input.GetKey(KeyCode.Q));
                if (input && Input.GetKeyDown(KeyCode.R)) simulation.RequestDivision();
                if (input && Input.GetKeyDown(KeyCode.T)) PlayOffspring();
            }
            if (!paused) { accumulator += Time.unscaledDeltaTime * speed; visualTime += Time.unscaledDeltaTime * speed; }
            int budget = 0;
            while (!paused && accumulator >= activeSettings.StepSeconds && budget++ < 256) {
                if (environmentPending) { simulation.SetEnvironment(pendingTemperature, pendingFood); environmentPending = false; }
                organismRenderer.Capture(simulation);
                int controlled = simulation.ControlledId; simulation.Step(); Journey.RecordStep(simulation, controlled); accumulator -= activeSettings.StepSeconds;
            }
            // Backlog is retained: frame rate cannot silently discard simulation ticks.
            if (selectedId != 0 && !Observation.TryFind(simulation, selectedId, out _)) ClearSelection();
            if (rig.Following && Observation.TryFind(simulation, selectedId, out var selected)) { OrganismFrame(selected, out var center, out _); rig.FollowPoint(center); }
            rig.Tick(overUI, IsControlling);
            organismRenderer.Draw(simulation, colorMode, activeSettings.WorldSize, selectedId, (float)visualTime,
                paused ? 1 : Mathf.Clamp01((float)(accumulator / activeSettings.StepSeconds)), rig.Distance, rig.RenderedFocus, DetailedOrganisms);
            ocean.Draw(simulation, view, (float)visualTime, rig.RenderedFocus);
            if (LivingOcean) ocean.DrawVents(simulation, rig.RenderedFocus, (float)visualTime);
            sound.Tick(simulation, selectedId, SoundVolume, paused);
        }
        void OnDestroy()
        {
            Destroy(mesh); Destroy(membraneMesh); Destroy(habitat); Destroy(cellsMaterial); Destroy(membraneMaterial); ocean?.Dispose(); oceanLook?.Dispose(); sound?.Dispose();
            if (ownedListener) Destroy(ownedListener);
            Destroy(panelTexture); Destroy(hudSkin); if (ownedCamera) Destroy(ownedCamera);
        }
    }
}
