using Evolution.Core;
using Evolution.Localization;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    public sealed class EvolutionRunner : MonoBehaviour
    {
        public int Seed = 42;
        public SimulationConfig Settings = new SimulationConfig();
        Simulation simulation;
        SimulationConfig activeSettings;
        Mesh mesh;
        Material cellsMaterial, foodMaterial;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] colors = new Vector4[1023];
        MaterialPropertyBlock properties;
        Camera view;
        double accumulator;
        bool paused, showDiet;
        Vector2 menuScroll;
        GUIStyle labelStyle;
        float speed = 1, pendingTemperature, pendingFood;
        int activeSeed;
        static readonly float[] Speeds = { 1f, 2f, 5f, 10f };
        bool environmentPending;
        public Simulation World => simulation;
        void Start()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); Destroy(primitive);
            var shader = Resources.Load<Shader>("InstancedCells");
            cellsMaterial = new Material(shader) { enableInstancing = true };
            foodMaterial = new Material(shader) { enableInstancing = true };
            properties = new MaterialPropertyBlock();
            view = Camera.main;
            if (!view) { view = new GameObject("Microscope Camera", typeof(Camera)).GetComponent<Camera>(); view.tag = "MainCamera"; }
            view.transform.position = new Vector3(Settings.WorldSize / 2, Settings.WorldSize * 1.05f, -Settings.WorldSize * .12f);
            view.transform.LookAt(new Vector3(Settings.WorldSize / 2, 0, Settings.WorldSize / 2));
            view.backgroundColor = new Color(.015f, .025f, .055f); view.clearFlags = CameraClearFlags.SolidColor;
            ResetWorld();
        }
        void ResetWorld()
        {
            activeSettings = Settings.Copy(); activeSeed = Seed;
            simulation = new Simulation(activeSettings, unchecked((uint)activeSeed)); accumulator = 0;
            pendingTemperature = Settings.Temperature; pendingFood = Settings.NutrientRegeneration; environmentPending = false;
        }
        void Update()
        {
            if (simulation == null) return;
            if (!paused) accumulator += Time.unscaledDeltaTime * speed;
            int budget = 0;
            while (!paused && accumulator >= activeSettings.StepSeconds && budget++ < 256) {
                if (environmentPending) { simulation.SetEnvironment(pendingTemperature, pendingFood); environmentPending = false; }
                simulation.Step(); accumulator -= activeSettings.StepSeconds;
            }
            // Backlog is retained: frame rate cannot silently discard simulation ticks.
            RenderCells(); RenderFood(); RenderOrganic();
        }
        void RenderCells()
        {
            for (int start = 0; start < simulation.Count; start += matrices.Length) {
                int count = Mathf.Min(matrices.Length, simulation.Count - start);
                for (int j = 0; j < count; j++) {
                    var c = simulation.GetCell(start + j);
                    matrices[j] = Matrix4x4.TRS(new Vector3(c.X, .5f, c.Z), Quaternion.identity, Vector3.one * c.Genes.Size * .65f);
                    colors[j] = showDiet
                        ? Color.Lerp(new Color(.2f, .8f, .45f), new Color(1, .55f, .2f), c.Genes.OrganicSpecialization)
                        : Color.Lerp(new Color(.15f, .5f, 1), new Color(1, .3f, .2f), c.Genes.PreferredTemperature / 50);
                }
                properties.SetVectorArray("_InstanceColor", colors);
                Graphics.DrawMeshInstanced(mesh, 0, cellsMaterial, matrices, count, properties, ShadowCastingMode.Off, false);
            }
        }
        void RenderFood()
        {
            int total = activeSettings.GridSize * activeSettings.GridSize;
            float width = activeSettings.WorldSize / activeSettings.GridSize;
            for (int start = 0; start < total; start += matrices.Length) {
                int count = Mathf.Min(matrices.Length, total - start);
                for (int j = 0; j < count; j++) {
                    int i = start + j;
                    float abundance = simulation.GetNutrient(i) / activeSettings.NutrientCapacity;
                    matrices[j] = Matrix4x4.TRS(new Vector3((i % activeSettings.GridSize + .5f) * width, -.15f, (i / activeSettings.GridSize + .5f) * width),
                        Quaternion.identity, new Vector3(width * .85f, .06f, width * .85f));
                    colors[j] = Color.Lerp(new Color(.02f, .04f, .06f), new Color(.08f, .38f, .23f), abundance);
                }
                properties.SetVectorArray("_InstanceColor", colors);
                Graphics.DrawMeshInstanced(mesh, 0, foodMaterial, matrices, count, properties, ShadowCastingMode.Off, false);
            }
        }
        void RenderOrganic()
        {
            int total = activeSettings.GridSize * activeSettings.GridSize;
            float width = activeSettings.WorldSize / activeSettings.GridSize;
            for (int start = 0; start < total; start += matrices.Length) {
                int count = 0, end = Mathf.Min(start + matrices.Length, total);
                for (int i = start; i < end; i++) {
                    float abundance = 1 - Mathf.Exp(-simulation.GetOrganicEnergy(i) / 8);
                    // Empty tiles must not submit singular zero-scale transforms to the lit shader.
                    if (abundance <= 0) continue;
                    matrices[count] = Matrix4x4.TRS(new Vector3((i % activeSettings.GridSize + .5f) * width, 0,
                        (i / activeSettings.GridSize + .5f) * width), Quaternion.identity,
                        new Vector3(width * .55f, .2f, width * .55f) * abundance);
                    colors[count++] = new Color(.6f, .28f, .08f);
                }
                if (count == 0) continue;
                properties.SetVectorArray("_InstanceColor", colors);
                Graphics.DrawMeshInstanced(mesh, 0, foodMaterial, matrices, count, properties, ShadowCastingMode.Off, false);
            }
        }
        void OnGUI()
        {
            if (simulation == null) return;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(380, Screen.width - 24), Mathf.Min(620, Screen.height - 24)), GUI.skin.box);
            menuScroll = GUILayout.BeginScrollView(menuScroll);
            GUILayout.Label(GameText.Get(TextKey.Title), labelStyle);
            GUILayout.Label(GameText.Format(TextKey.Clock, activeSeed, simulation.Tick, simulation.Tick * activeSettings.StepSeconds), labelStyle);
            GUILayout.Label(GameText.Format(TextKey.Population, simulation.Count, activeSettings.Capacity, simulation.Births, simulation.Deaths), labelStyle);
            float meanSpeed = 0, meanTemperature = 0, meanEnergy = 0, meanDiet = 0; int generation = 0;
            for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); meanSpeed += c.Genes.Speed; meanTemperature += c.Genes.PreferredTemperature; meanEnergy += c.Energy; meanDiet += c.Genes.OrganicSpecialization; generation = Mathf.Max(generation, c.Generation); }
            int n = Mathf.Max(1, simulation.Count);
            GUILayout.Label(GameText.Format(TextKey.GenerationEnergy, generation, meanEnergy / n), labelStyle);
            GUILayout.Label(GameText.Format(TextKey.Speed, meanSpeed / n), labelStyle);
            GUILayout.Label(GameText.Format(TextKey.ThermalOptimum, meanTemperature / n), labelStyle);
            double organicTotal = 0;
            for (int i = 0; i < activeSettings.GridSize * activeSettings.GridSize; i++) organicTotal += simulation.GetOrganicEnergy(i);
            GUILayout.Label(GameText.Format(TextKey.Diet, meanDiet / n), labelStyle);
            GUILayout.Label(GameText.Format(TextKey.Remains, organicTotal), labelStyle);
            if (GUILayout.Button(GameText.Get(showDiet ? TextKey.DietColors : TextKey.TemperatureColors))) showDiet = !showDiet;
            if (GUILayout.Button(GameText.Get(paused ? TextKey.Resume : TextKey.Pause))) paused = !paused;
            GUILayout.BeginHorizontal();
            foreach (float value in Speeds) if (GUILayout.Button(GameText.Format(TextKey.TimeScale, value))) speed = value;
            GUILayout.EndHorizontal();
            GUILayout.Label(GameText.Format(TextKey.Temperature, pendingTemperature), labelStyle);
            float t = GUILayout.HorizontalSlider(pendingTemperature, 0, 50);
            GUILayout.Label(GameText.Format(TextKey.Regeneration, pendingFood), labelStyle);
            float f = GUILayout.HorizontalSlider(pendingFood, 0, .5f);
            if (t != pendingTemperature || f != pendingFood) { pendingTemperature = t; pendingFood = f; environmentPending = true; }
            if (GUILayout.Button(GameText.Get(TextKey.Restart))) ResetWorld();
            if (GUILayout.Button(GameText.Get(TextKey.ZoomIn))) view.transform.position += view.transform.forward * 3;
            if (GUILayout.Button(GameText.Get(TextKey.ZoomOut))) view.transform.position -= view.transform.forward * 3;
            GUILayout.Label(GameText.Get(showDiet ? TextKey.DietLegend : TextKey.TemperatureLegend), labelStyle);
            GUILayout.Label(GameText.Get(TextKey.FoodLegend), labelStyle);
            if (simulation.Count == 0) GUILayout.Label(GameText.Get(TextKey.Extinction), labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void OnDestroy() { Destroy(mesh); Destroy(cellsMaterial); Destroy(foodMaterial); }
    }
}
