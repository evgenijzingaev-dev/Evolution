using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner : MonoBehaviour
    {
        public int Seed = 42;
        public SimulationConfig Settings = new SimulationConfig();
        Simulation simulation;
        OrganismRenderer organismRenderer;
        SimulationConfig activeSettings;
        Mesh mesh, habitat;
        Material cellsMaterial, membraneMaterial, foodMaterial;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] colors = new Vector4[1023];
        MaterialPropertyBlock properties;
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
            mesh = MicroMeshes.Cell(); habitat = MicroMeshes.Habitat();
            cellsMaterial = MakeMaterial("InstancedCells"); membraneMaterial = MakeMaterial("CellMembrane"); foodMaterial = MakeMaterial("MicroHabitat");
            properties = new MaterialPropertyBlock();
            view = Camera.main;
            if (!view) { ownedCamera = new GameObject("Microscope Camera", typeof(Camera)); view = ownedCamera.GetComponent<Camera>(); view.tag = "MainCamera"; }
            view.backgroundColor = new Color(.012f, .038f, .065f); view.clearFlags = CameraClearFlags.SolidColor;
            rig = gameObject.AddComponent<MicroscopeCamera>();
            ResetWorld();
        }
        static Material MakeMaterial(string resource) => new Material(Resources.Load<Shader>(resource)) { enableInstancing = true };
        public void ResetWorld()
        {
            activeSettings = Settings.Copy();
            simulation = new Simulation(activeSettings, unchecked((uint)Seed)); accumulator = visualTime = 0;
            organismRenderer = new OrganismRenderer(mesh, cellsMaterial, membraneMaterial, activeSettings.Capacity);
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
            rig.Tick(overUI); RenderHabitat(); RenderSuspension();
        }
        void RenderHabitat()
        {
            int total = activeSettings.GridSize * activeSettings.GridSize;
            float width = activeSettings.WorldSize / activeSettings.GridSize;
            properties.SetFloat("_Temperature", simulation.Temperature);
            for (int start = 0; start < total; start += matrices.Length) {
                int count = Mathf.Min(matrices.Length, total - start);
                for (int j = 0; j < count; j++) {
                    int i = start + j;
                    matrices[j] = Matrix4x4.TRS(new Vector3((i % activeSettings.GridSize + .5f) * width, -.15f, (i / activeSettings.GridSize + .5f) * width),
                        Quaternion.identity, new Vector3(width, 1, width));
                    colors[j] = new Vector4(simulation.GetNutrient(i) / activeSettings.NutrientCapacity, 1 - Mathf.Exp(-simulation.GetOrganicEnergy(i) / 8), 0, 1);
                }
                properties.SetVectorArray("_InstanceColor", colors);
                Graphics.DrawMeshInstanced(habitat, 0, foodMaterial, matrices, count, properties, ShadowCastingMode.Off, false);
            }
        }
        void RenderSuspension()
        {
            const int count = 128;
            float world = activeSettings.WorldSize, time = (float)visualTime;
            for (int i = 0; i < count; i++) {
                float x = Mathf.Repeat(i * 7.137f + time * .03f, world), z = Mathf.Repeat(i * 11.319f + time * .015f, world);
                float height = .2f + Mathf.Repeat(i * .719f, 5);
                matrices[i] = Matrix4x4.TRS(new Vector3(x, height, z), Quaternion.identity, Vector3.one * (.018f + (i % 3) * .012f));
                colors[i] = new Color(.16f, .35f, .4f);
            }
            properties.SetVectorArray("_InstanceColor", colors); properties.SetFloat("_WorldSize", world);
            Graphics.DrawMeshInstanced(mesh, 0, cellsMaterial, matrices, count, properties, ShadowCastingMode.Off, false);
        }
        void OnDestroy()
        {
            Destroy(mesh); Destroy(habitat); Destroy(cellsMaterial); Destroy(membraneMaterial); Destroy(foodMaterial);
            Destroy(panelTexture); Destroy(hudSkin); if (ownedCamera) Destroy(ownedCamera);
        }
    }
}
