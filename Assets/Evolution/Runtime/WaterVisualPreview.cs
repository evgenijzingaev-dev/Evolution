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
        RenderTexture target;
        int renderedFrames;
        float yaw, pitch = 15;
        readonly List<Shot> shots = new List<Shot>();

        [Serializable] public sealed class Shot
        {
            public string file;
            public Vector3 cameraPosition, cameraEuler;
            public long tick;
            public int visibleOrganisms, cameraFrames;
            public float nearestDepth, farthestDepth, luminanceRange, luminanceDeviation;
            public bool currents, temperature, food, hudVisible;
            public float temperatureC, currentSpeed, foodSubstrate, foodOrganic;
        }
        [Serializable] sealed class Report
        {
            public string unity, pipeline, graphicsApi, graphicsDevice, graphicsVendor, graphicsVersion, revision;
            public string scene = "WaterPrototype3D";
            public int width = Width, height = Height, seed = 42;
            public bool passed;
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
            Debug.Log("Visual Preview phase: game loop running");
            Require(Application.unityVersion == "6000.6.9f1", "wrong Unity version");
            Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "URP is not active");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.graphicsShaderLevel >= 45,
                "no usable graphics device (never run the player with -nographics)");
            foreach (var name in new[] { "Evolution/VolumeBody", "Evolution/VolumeMembrane", "Evolution/VolumeSuspension", "Evolution/VolumeBackdrop" }) {
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
            int selected = water.ScenicSelection(); float nearest;
            Require(selected >= 0, "no living organism");
            water.Select(selected);
            string identity = water.World.Get(selected).Identity;
            var focus = ToVector(water.World.Get(selected).Position);

            float openingYaw = 25, openingScore = -1;
            for(int angle=0;angle<360;angle+=45) {
                SetView(focus,10,angle,8,false,false,false);
                var view=Measure("opening-probe");
                float side=Mathf.Abs(Vector3.Dot(water.Lens.transform.forward,ToVector(water.World.Get(selected).Direction).normalized));
                float score=view.visibleOrganisms*2+(view.nearestDepth<9?2:0)+(view.farthestDepth>20?2:0)+(side<.6f?2:0);
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
            for (int angle = 0; angle < 360; angle += 45) {
                SetView(focus, 20, angle, 12, false, false, false);
                var measure = Measure("probe");
                if (measure.visibleOrganisms < 3 || measure.farthestDepth - measure.nearestDepth < 3) continue;
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
            File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonUtility.ToJson(Context(shots.ToArray(), true), true));
            // A separate evidence frame for the water itself; not an invented population or backdrop fixture.
            water.OrganismsVisible = false;
            SetView(focus, 23, 25, 22, false, false, false);
            yield return Capture("06-water-only", false);
            Application.Quit(0);
        }
        Report Context(Shot[] frames, bool passed)
        {
            return new Report {
                unity = Application.unityVersion, pipeline = GraphicsSettings.currentRenderPipeline.GetType().Name,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsVendor = SystemInfo.graphicsDeviceVendor, graphicsVersion = SystemInfo.graphicsDeviceVersion,
                revision = Environment.GetEnvironmentVariable("EVOLUTION_PREVIEW_SHA") ?? "local",
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
            water.Rig.Orbit(heading - yaw, tilt - pitch); yaw = heading; pitch = tilt;
            water.Rig.ApplyImmediate();
        }
        Shot Measure(string file)
        {
            var shot = new Shot { file = file, tick = water.World.Tick, cameraPosition = water.Lens.transform.position,
                cameraEuler = water.Lens.transform.eulerAngles, nearestDepth = float.PositiveInfinity,
                currents = water.ShowCurrents, temperature = water.ShowTemperature, food = water.FindFood,
                hudVisible = water.HudVisible };
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
            for (int frame = 0; frame < 4; frame++) yield return new WaitForEndOfFrame();
            Require(renderedFrames - before >= 2, "URP camera did not render");
            Texture2D image;
            if (includeHud) {
                Require(Screen.width == Width && Screen.height == Height, "unexpected game framebuffer resolution");
                image = ScreenCapture.CaptureScreenshotAsTexture();
                Require(image && image.width == Width && image.height == Height, "missing game framebuffer");
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
