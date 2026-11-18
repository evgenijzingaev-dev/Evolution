using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Evolution.Runtime
{
    // Owned by Cell Prototype at runtime, including when its scene is opened directly.
    internal sealed class OceanLook
    {
        readonly GameObject root;
        readonly VolumeProfile profile;
        readonly Texture2D surface, normal;
        readonly AmbientMode oldAmbient;
        readonly Color oldSky, oldEquator, oldGround;
        public OceanLook(Transform parent, Camera camera)
        {
            oldAmbient = RenderSettings.ambientMode; oldSky = RenderSettings.ambientSkyColor;
            oldEquator = RenderSettings.ambientEquatorColor; oldGround = RenderSettings.ambientGroundColor;
            root = new GameObject("Living ocean lighting and post processing"); root.transform.SetParent(parent, false);
            var lightObject = new GameObject("Scattered sunlight"); lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(42, -28, 0);
            var sunlight = lightObject.AddComponent<Light>(); sunlight.type = LightType.Directional;
            sunlight.color = new Color(.68f, .92f, 1); sunlight.intensity = 1.25f; sunlight.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.12f, .32f, .38f);
            RenderSettings.ambientEquatorColor = new Color(.045f, .16f, .18f);
            RenderSettings.ambientGroundColor = new Color(.025f, .07f, .1f);
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresColorTexture = true; data.renderPostProcessing = true;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.threshold.value = 1.05f; bloom.intensity.value = .18f; bloom.scatter.value = .6f;
            var tone = profile.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.Neutral;
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.value = .2f; color.contrast.value = 7; color.saturation.value = -4;
            var vignette = profile.Add<Vignette>(true); vignette.intensity.value = .13f; vignette.smoothness.value = .65f;
            var volume = root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
            const int resolution = 128;
            surface = Texture("Procedural cytoplasm", resolution); normal = Texture("Procedural membrane microrelief", resolution);
            var density = new float[resolution * resolution];
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) {
                float a = x * Mathf.PI * 2 / resolution, b = y * Mathf.PI * 2 / resolution;
                // Periodic harmonics, no seams and no external assets or random simulation calls.
                density[y * resolution + x] = .5f + .17f * Mathf.Sin(a * 7 + Mathf.Sin(b * 5))
                    + .12f * Mathf.Cos(b * 11 - Mathf.Cos(a * 3)) + .08f * Mathf.Sin(a * 17 + b * 13);
            }
            var pigment = new Color[density.Length]; var normals = new Color[density.Length];
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) {
                int i = y * resolution + x; float d = density[i];
                float dx = density[y * resolution + (x + 1) % resolution] - density[y * resolution + (x + resolution - 1) % resolution];
                float dy = density[((y + 1) % resolution) * resolution + x] - density[((y + resolution - 1) % resolution) * resolution + x];
                Vector3 n = new Vector3(-dx * 1.8f, -dy * 1.8f, 1).normalized;
                pigment[i] = new Color(d, Mathf.SmoothStep(0, 1, d), 1 - Mathf.Abs(d - .5f) * 2, 1);
                normals[i] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            surface.SetPixels(pigment); surface.Apply(true, true); normal.SetPixels(normals); normal.Apply(true, true);
        }
        static Texture2D Texture(string name, int resolution) => new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true) {
            name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2
        };
        public void Apply(Material material) { material.SetTexture("_MicroTex", surface); material.SetTexture("_MicroNormal", normal); }
        public void Dispose()
        {
            Object.Destroy(root); Object.Destroy(surface); Object.Destroy(normal);
            foreach (var component in profile.components) Object.Destroy(component);
            Object.Destroy(profile);
            RenderSettings.ambientMode = oldAmbient; RenderSettings.ambientSkyColor = oldSky;
            RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
        }
    }
}
