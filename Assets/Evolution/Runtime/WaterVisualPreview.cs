using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Evolution.Volume;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Evolution.Runtime
{
    // Dormant in ordinary games. No alternate renderer, fixture organisms or biology changes.
    public sealed class WaterVisualPreview : MonoBehaviour
    {
        const int Width = 1920, Height = 1080;
        static string destination;
        WaterPrototype water;
        bool failed;
        float yaw, pitch = 15;
        readonly List<Shot> shots = new List<Shot>();

        [Serializable] public sealed class Shot
        {
            public string file;
            public Vector3 cameraPosition, cameraEuler;
            public long tick;
            public int visibleOrganisms;
            public float nearestDepth, farthestDepth, luminanceRange, luminanceDeviation;
            public bool currents, temperature, food;
            public float temperatureC, currentSpeed, foodSubstrate, foodOrganic;
        }
        [Serializable] sealed class Report
        {
            public string unity, pipeline, graphicsApi, graphicsDevice, graphicsVendor, graphicsVersion, revision;
            public string scene = "WaterPrototype3D";
            public int width = Width, height = Height, seed = 42;
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
            destination = Path.GetFullPath(args[at + 1]);
            SceneManager.sceneLoaded += PrepareScene;
        }
        static void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= PrepareScene;
            var prototype = UnityEngine.Object.FindFirstObjectByType<WaterPrototype>();
            if (scene.name != "WaterPrototype3D" || !prototype) { Application.Quit(1); return; }
            // sceneLoaded runs before Start: never read/write a developer's region archive.
            prototype.PersistRegions = false;
            prototype.Seed = 42;
            prototype.SoundVolume = 0;
            var runner = new GameObject("Opt-in visual preview").AddComponent<WaterVisualPreview>();
            runner.water = prototype;
        }
        void OnEnable() { Application.logMessageReceived += CheckLog; }
        void OnDisable() { Application.logMessageReceived -= CheckLog; }
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
            Require(Application.unityVersion == "6000.6.9f1", "wrong Unity version");
            Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "URP is not active");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.graphicsShaderLevel >= 45,
                "no usable graphics device (never run the player with -nographics)");
            foreach (var name in new[] { "Evolution/VolumeBody", "Evolution/VolumeMembrane", "Evolution/VolumeSuspension", "Evolution/VolumeBackdrop" }) {
                var shader = Shader.Find(name);
                Require(shader && shader.isSupported, "unsupported or missing shader: " + name);
            }
            Directory.CreateDirectory(destination);
            // Let the real simulation and procedural presentation initialize, then freeze its actual state.
            while (water.World == null || water.World.Tick < 20) yield return null;
            water.Paused = true;
            int selected = -1; float nearest = float.PositiveInfinity;
            for (int i = 0; i < water.World.Count; i++) {
                var a = water.World.Get(i);
                if (a.Alive && a.Position.Sqr < nearest) { selected = i; nearest = a.Position.Sqr; }
            }
            Require(selected >= 0, "no living organism");
            water.Select(selected);
            string identity = water.World.Get(selected).Identity;
            var focus = ToVector(water.World.Get(selected).Position);

            SetView(focus, 23, 25, 22, false, false, false);
            yield return Capture("01-ocean");
            water.Select(water.Ocean.Find(identity));
            SetView(focus, 3.6f, 55, 10, false, false, false, true);
            yield return Capture("02-cell-close");

            // Find a view of existing organisms with actual camera-space depth separation.
            float best = -1, bestYaw = 0;
            for (int angle = 0; angle < 360; angle += 45) {
                SetView(focus, 20, angle, 12, false, false, false);
                var measure = Measure("probe");
                float score = measure.visibleOrganisms + Mathf.Min(15, measure.farthestDepth - measure.nearestDepth);
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
            var report = new Report {
                unity = Application.unityVersion, pipeline = GraphicsSettings.currentRenderPipeline.GetType().Name,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsVendor = SystemInfo.graphicsDeviceVendor, graphicsVersion = SystemInfo.graphicsDeviceVersion,
                revision = Environment.GetEnvironmentVariable("EVOLUTION_PREVIEW_SHA") ?? "local",
                screenshots = shots.ToArray()
            };
            File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonUtility.ToJson(report, true));
            Application.Quit(0);
        }
        static Vector3 ToVector(V3 p) => new Vector3(p.X, p.Y, p.Z);
        void SetView(Vector3 point, float distance, float heading, float tilt, bool currents, bool temperature, bool food, bool follow = false)
        {
            if (!follow) water.Observe();
            water.HudVisible = currents || temperature || food;
            water.ShowCurrents = currents; water.ShowTemperature = temperature; water.FindFood = food;
            water.Rig.FocusOn(point, distance);
            water.Rig.Orbit(heading - yaw, tilt - pitch); yaw = heading; pitch = tilt;
            water.Rig.ApplyImmediate();
        }
        Shot Measure(string file)
        {
            var shot = new Shot { file = file, tick = water.World.Tick, cameraPosition = water.Lens.transform.position,
                cameraEuler = water.Lens.transform.eulerAngles, nearestDepth = float.PositiveInfinity,
                currents = water.ShowCurrents, temperature = water.ShowTemperature, food = water.FindFood };
            for (int i = 0; i < water.World.Count; i++) {
                var a = water.World.Get(i); if (!a.Alive) continue;
                var v = water.Lens.WorldToViewportPoint(ToVector(a.Position));
                if (v.z < 1 || v.z > 30 || v.x < .05f || v.x > .95f || v.y < .05f || v.y > .95f) continue;
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
            return shot;
        }
        IEnumerator Capture(string name)
        {
            // Ordinary player frames, including URP post effects and the existing Russian overlay.
            // WaitForEndOfFrame is deliberately used in a windowed player, never a batch-mode Editor.
            for (int frame = 0; frame < 16; frame++) yield return new WaitForEndOfFrame();
            Require(Screen.width == Width && Screen.height == Height, "unexpected render resolution");
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Require(image && image.width == Width && image.height == Height, "missing game framebuffer");
            var shot = Measure(name + ".png");
            var pixels = image.GetPixels32(); double sum = 0, squares = 0; float low = 1, high = 0; int magenta = 0;
            for (int p = 0; p < pixels.Length; p += 7) {
                var c = pixels[p];
                if (c.r > 230 && c.b > 230 && c.g < 20) magenta++;
                float l = (c.r * .2126f + c.g * .7152f + c.b * .0722f) / 255;
                low = Mathf.Min(low, l); high = Mathf.Max(high, l); sum += l; squares += l * l;
            }
            int samples = (pixels.Length + 6) / 7;
            Require(magenta < samples / 1000, "magenta shader-error pixels");
            shot.luminanceRange = high - low;
            shot.luminanceDeviation = (float)Math.Sqrt(Math.Max(0, squares / samples - sum * sum / samples / samples));
            Require(shot.luminanceRange > .12f && shot.luminanceDeviation > .015f, "blank/flat game framebuffer");
            if (name == "03-depth-ensemble") Require(shot.visibleOrganisms >= 3 && shot.farthestDepth - shot.nearestDepth >= 3,
                "ensemble must contain three organisms at different real depths");
            File.WriteAllBytes(Path.Combine(destination, shot.file), image.EncodeToPNG());
            Destroy(image); shots.Add(shot);
        }
    }
}
