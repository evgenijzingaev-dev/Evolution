using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner : MonoBehaviour
    {
        public int Seed = 42;
        public bool LivingOcean = true;
        public SimulationConfig Settings = new SimulationConfig();
        Simulation simulation;
        OrganismRenderer organismRenderer;
        SimulationConfig activeSettings;
        Mesh mesh, habitat, membraneMesh;
        Material cellsMaterial, membraneMaterial;
        OceanRenderer ocean;
        OceanLook oceanLook;
        Camera view;
        MicroscopeCamera rig;
        GameObject ownedCamera;
        double accumulator, visualTime;
        bool paused;
        int colorMode = 2, selectedId;
        float speed = 1, pendingTemperature, pendingFood;
        static readonly float[] Speeds = { 1f, 2f, 5f, 10f };
        bool environmentPending;
        public Simulation World => simulation;
        public int SelectedId => selectedId;
        public MicroscopeCamera CameraRig => rig;
        public bool IsPaused { get => paused; set => paused = value; }
        void Start()
        {
            mesh = MicroMeshes.Cell(); membraneMesh = MicroMeshes.Cell(16, 24); habitat = MicroMeshes.Habitat();
            cellsMaterial = MakeMaterial("InstancedCells"); membraneMaterial = MakeMaterial("CellMembrane");
            view = Camera.main;
            if (!view) { ownedCamera = new GameObject("Microscope Camera", typeof(Camera)); view = ownedCamera.GetComponent<Camera>(); view.tag = "MainCamera"; }
            view.backgroundColor = new Color(.035f, .19f, .23f); view.clearFlags = CameraClearFlags.SolidColor;
            oceanLook = new OceanLook(transform, view); oceanLook.Apply(cellsMaterial); oceanLook.Apply(membraneMaterial);
            rig = gameObject.AddComponent<MicroscopeCamera>();
            ResetWorld();
        }
        static Material MakeMaterial(string resource) => new Material(Resources.Load<Shader>(resource)) { enableInstancing = true };
        public void ResetWorld()
        {
            activeSettings = Settings.Copy(); activeSettings.OceanZones = LivingOcean;
            simulation = new Simulation(activeSettings, unchecked((uint)Seed)); accumulator = visualTime = 0;
            organismRenderer = new OrganismRenderer(mesh, cellsMaterial, membraneMaterial, activeSettings.Capacity, membraneMesh);
            ocean?.Dispose(); ocean = new OceanRenderer(activeSettings.WorldSize, activeSettings.GridSize, habitat, mesh);
            selectedId = 0; rig.Initialize(view, activeSettings.WorldSize);
            pendingTemperature = activeSettings.Temperature; pendingFood = activeSettings.NutrientRegeneration; environmentPending = false;
        }
        public bool SelectCell(int id)
        {
            if (!Observation.TryFind(simulation, id, out _)) return false;
            selectedId = id; rig.StopFollowing(); return true;
        }
        public void ClearSelection() { selectedId = 0; rig.StopFollowing(); }
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
            float world = activeSettings.WorldSize;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); Vector3 point = organismRenderer.Position(c);
                // Match clipped periodic copies of the membranes at the dish boundary.
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) {
                    Vector3 center = point + new Vector3(x * world, 0, z * world), offset = ray.origin - center;
                    float hit = Observation.RaySphere(offset.x, offset.y, offset.z, ray.direction.x, ray.direction.y, ray.direction.z, c.Genes.Size * .6f);
                    if (hit >= nearest) continue;
                    Vector3 intersection = ray.GetPoint(hit);
                    if (intersection.x < 0 || intersection.z < 0 || intersection.x > world || intersection.z > world) continue;
                    nearest = hit; id = c.Id;
                }
            }
            return id;
        }
        void Update()
        {
            if (simulation == null) return;
            bool overUI = PointerOverUI(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.F)) FocusSelected(false);
            if (Input.GetKeyDown(KeyCode.G)) FocusSelected(true);
            if (Input.GetKeyDown(KeyCode.Escape)) ClearSelection();
            if (!overUI && Input.GetMouseButtonDown(0)) { int id = Pick(Input.mousePosition); if (id == 0) ClearSelection(); else SelectCell(id); }
            if (!paused) { accumulator += Time.unscaledDeltaTime * speed; visualTime += Time.unscaledDeltaTime * speed; }
            int budget = 0;
            while (!paused && accumulator >= activeSettings.StepSeconds && budget++ < 256) {
                if (environmentPending) { simulation.SetEnvironment(pendingTemperature, pendingFood); environmentPending = false; }
                simulation.Step(); accumulator -= activeSettings.StepSeconds;
            }
            // Backlog is retained: frame rate cannot silently discard simulation ticks.
            if (selectedId != 0 && !Observation.TryFind(simulation, selectedId, out _)) ClearSelection();
            organismRenderer.Draw(simulation, colorMode, activeSettings.WorldSize, selectedId, (float)visualTime, paused ? 0 : Time.unscaledDeltaTime);
            if (rig.Following && Observation.TryFind(simulation, selectedId, out var selected)) { OrganismFrame(selected, out var center, out _); rig.FollowPoint(center); }
            rig.Tick(overUI); ocean.Draw(simulation, view, (float)visualTime);
        }
        void OnDestroy()
        {
            Destroy(mesh); Destroy(membraneMesh); Destroy(habitat); Destroy(cellsMaterial); Destroy(membraneMaterial); ocean?.Dispose(); oceanLook?.Dispose();
            Destroy(panelTexture); Destroy(hudSkin); if (ownedCamera) Destroy(ownedCamera);
        }
    }
}
