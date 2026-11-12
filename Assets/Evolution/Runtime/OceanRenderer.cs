using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evolution.Runtime
{
    // Continuous, bilinearly sampled resources. No geometry or pattern at simulation tile edges.
    internal sealed class OceanRenderer
    {
        readonly Mesh plane, mote;
        readonly Material water, cloud, particles;
        readonly Texture2D resources, ecology;
        readonly Color[] pixels, ecologyPixels;
        readonly Matrix4x4[] matrices = new Matrix4x4[512];
        readonly Vector4[] colors = new Vector4[512];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly float world;
        readonly Vector3[] motes = new Vector3[512];
        float lastTime;
        long uploadedTick = -1;
        public OceanRenderer(float world, int grid, Mesh plane, Mesh mote)
        {
            this.world = world; this.plane = plane; this.mote = mote;
            water = Create("MicroHabitat"); cloud = Create("OceanCloud"); particles = Create("OceanParticles");
            resources = new Texture2D(grid, grid, TextureFormat.RGBA32, false, true) {
                name = "Living ocean resources", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat
            };
            ecology = new Texture2D(grid, grid, TextureFormat.RGBA32, false, true) {
                name = "Ocean ecological zones", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat
            };
            pixels = new Color[grid * grid]; ecologyPixels = new Color[pixels.Length];
            for (int i = 0; i < motes.Length; i++) motes[i] = new Vector3(Mathf.Repeat(i * 7.137f, world), -8 + Mathf.Repeat(i * .719f, 14), Mathf.Repeat(i * 11.319f, world));
            water.SetTexture("_Resources", resources); cloud.SetTexture("_Resources", resources);
            water.SetTexture("_Ecology", ecology); cloud.SetTexture("_Ecology", ecology);
        }
        static Material Create(string shader) => new Material(Resources.Load<Shader>(shader)) { enableInstancing = true };
        public void Draw(Simulation simulation, Camera camera, float time)
        {
            if (uploadedTick != simulation.Tick) {
                var config = simulation.Config; float capacity = config.NutrientCapacity;
                for (int i = 0; i < pixels.Length; i++) {
                    var sample = simulation.EnvironmentAt((i % config.GridSize + .5f) * world / config.GridSize,
                        (i / config.GridSize + .5f) * world / config.GridSize);
                    pixels[i] = new Color(simulation.GetNutrient(i) / capacity,
                        1 - Mathf.Exp(-simulation.GetOrganicEnergy(i) / 8), Mathf.Clamp01(sample.Temperature / 50), 1);
                    ecologyPixels[i] = new Color(Mathf.Clamp01(sample.Light), Mathf.Clamp01(sample.Toxicity), 0, 1);
                }
                ecology.SetPixels(ecologyPixels); ecology.Apply(false);
                resources.SetPixels(pixels); resources.Apply(false); uploadedTick = simulation.Tick;
            }
            water.SetFloat("_OceanTime", time); cloud.SetFloat("_OceanTime", time);
            water.SetFloat("_WorldSize", world); cloud.SetFloat("_WorldSize", world);
            water.SetFloat("_Temperature", simulation.Temperature);
            Graphics.DrawMesh(plane, Matrix4x4.TRS(new Vector3(world * .5f, -12, world * .5f), Quaternion.identity,
                new Vector3(world * 9, 1, world * 9)), water, 0, camera, 0, null, ShadowCastingMode.Off, false);
            for (int layer = 0; layer < 3; layer++) {
                properties.SetFloat("_Layer", layer);
                Graphics.DrawMesh(plane, Matrix4x4.TRS(new Vector3(world * .5f, -1.5f - layer * 3.3f, world * .5f),
                    Quaternion.identity, new Vector3(world * 7, 1, world * 7)), cloud, 0, camera, 0, properties, ShadowCastingMode.Off, false);
            }
            // A deterministic visual field: never consumes the simulation's random source.
            for (int i = 0; i < matrices.Length; i++) {
                var point = motes[i]; var flow = simulation.EnvironmentAt(point.x, point.z);
                point.x = Mathf.Repeat(point.x + flow.CurrentX * (time - lastTime), world);
                point.z = Mathf.Repeat(point.z + flow.CurrentZ * (time - lastTime), world);
                motes[i] = point;
                float size = .018f + i % 5 * .012f;
                matrices[i] = Matrix4x4.TRS(point + Vector3.up * (Mathf.Sin(time * .3f + i) * .08f), Quaternion.identity, new Vector3(size, size * .6f, size));
                colors[i] = i % 7 == 0 ? new Color(.9f, .62f, .3f, .36f) : new Color(.34f, .82f, .73f, .3f);
            }
            lastTime = time;
            properties.Clear(); properties.SetVectorArray("_InstanceColor", colors);
            Graphics.DrawMeshInstanced(mote, 0, particles, matrices, matrices.Length, properties, ShadowCastingMode.Off, false, 0, camera);
        }
        public void Dispose()
        {
            Object.Destroy(water); Object.Destroy(cloud); Object.Destroy(particles); Object.Destroy(resources); Object.Destroy(ecology);
        }
    }
}
