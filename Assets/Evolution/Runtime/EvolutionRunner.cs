using Evolution.Core;
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
        bool paused;
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
            RenderCells(); RenderFood();
        }
        void RenderCells()
        {
            for (int start = 0; start < simulation.Count; start += matrices.Length) {
                int count = Mathf.Min(matrices.Length, simulation.Count - start);
                for (int j = 0; j < count; j++) {
                    var c = simulation.GetCell(start + j);
                    matrices[j] = Matrix4x4.TRS(new Vector3(c.X, .5f, c.Z), Quaternion.identity, Vector3.one * c.Genes.Size * .65f);
                    colors[j] = Color.Lerp(new Color(.15f, .5f, 1), new Color(1, .3f, .2f), c.Genes.PreferredTemperature / 50);
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
        void OnGUI()
        {
            if (simulation == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, 420), GUI.skin.box);
            GUILayout.Label("EVOLUTION / cellular laboratory");
            GUILayout.Label($"Seed {activeSeed} | Tick {simulation.Tick} | {simulation.Tick * activeSettings.StepSeconds:F1} s");
            GUILayout.Label($"Cells {simulation.Count}/{activeSettings.Capacity} | Births {simulation.Births} | Deaths {simulation.Deaths}");
            float meanSpeed = 0, meanTemperature = 0, meanEnergy = 0; int generation = 0;
            for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); meanSpeed += c.Genes.Speed; meanTemperature += c.Genes.PreferredTemperature; meanEnergy += c.Energy; generation = Mathf.Max(generation, c.Generation); }
            int n = Mathf.Max(1, simulation.Count);
            GUILayout.Label($"Generation {generation} | Mean energy {meanEnergy / n:F1}");
            GUILayout.Label($"Mean speed {meanSpeed / n:F2} | Thermal optimum {meanTemperature / n:F1} C");
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
            GUILayout.BeginHorizontal();
            foreach (float value in Speeds) if (GUILayout.Button(value + "x")) speed = value;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Temperature {pendingTemperature:F1} C");
            float t = GUILayout.HorizontalSlider(pendingTemperature, 0, 50);
            GUILayout.Label($"Nutrient regeneration {pendingFood:F2}/s");
            float f = GUILayout.HorizontalSlider(pendingFood, 0, .5f);
            if (t != pendingTemperature || f != pendingFood) { pendingTemperature = t; pendingFood = f; environmentPending = true; }
            if (GUILayout.Button("Restart same seed")) ResetWorld();
            if (GUILayout.Button("Zoom in")) view.transform.position += view.transform.forward * 3;
            if (GUILayout.Button("Zoom out")) view.transform.position -= view.transform.forward * 3;
            GUILayout.Label("Blue: cold adapted / Red: heat adapted\nGreen substrate: local nutrient concentration");
            if (simulation.Count == 0) GUILayout.Label("Population extinct. Restart or adjust conditions.");
            GUILayout.EndArea();
        }
        void OnDestroy() { Destroy(mesh); Destroy(cellsMaterial); Destroy(foodMaterial); }
    }
}
