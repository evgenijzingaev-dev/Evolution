using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evolution.Runtime
{
    // Continuous, bilinearly sampled resources. No geometry or pattern at simulation tile edges.
    internal sealed class OceanRenderer
    {
        readonly Mesh plane, mote;
        readonly Material water, cloud, particles, minerals;
        readonly Texture2D resources, ecology;
        readonly Color[] pixels, ecologyPixels;
        readonly Matrix4x4[] matrices = new Matrix4x4[512];
        readonly Vector4[] colors = new Vector4[512];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly float world;
        readonly Vector3[] motes = new Vector3[512];
        float lastTime;
        long uploadedTick = -1;
        readonly Vector3[] vents = new Vector3[64];
        int ventCount;
        readonly int grid;
        readonly float nutrientCapacity;
        public OceanRenderer(float world, int grid, Mesh plane, Mesh mote, float nutrientCapacity)
        {
            this.world = world; this.grid = grid; this.nutrientCapacity = nutrientCapacity; this.plane = plane; this.mote = mote;
            minerals = Create("InstancedCells"); water = Create("MicroHabitat"); cloud = Create("OceanCloud"); particles = Create("OceanParticles");
            resources = new Texture2D(grid, grid, TextureFormat.RGBA32, false, true) {
                name = "Living ocean resources", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat
            };
            ecology = new Texture2D(grid, grid, TextureFormat.RGBA32, false, true) {
                name = "Ocean ecological zones", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat
            };
            pixels = new Color[grid * grid]; ecologyPixels = new Color[pixels.Length];
            for (int i = 0; i < motes.Length; i++) motes[i] = new Vector3(Mathf.Repeat(i * 7.137f, world), -8 + Mathf.Repeat(i * .719f, 14), Mathf.Repeat(i * 11.319f, world));
            for (int z = 0; z < 16; z++) for (int x = 0; x < 16 && ventCount < vents.Length; x++) {
                var point = new Vector3((x + .5f)*world/16,-1.8f,(z + .5f)*world/16);
                if (OceanEnvironment.Sample(point.x,point.z,world,0,22,true).Zone == OceanZone.Hydrothermal)
                    vents[ventCount++] = point;
            }
            water.SetTexture("_Resources", resources); cloud.SetTexture("_Resources", resources);
            water.SetTexture("_Ecology", ecology); cloud.SetTexture("_Ecology", ecology);
        }
        static Material Create(string shader) => new Material(Resources.Load<Shader>(shader)) { enableInstancing = true };
        public void Draw(Simulation simulation, Camera camera, float time, Vector3 focus)
        {
            if (uploadedTick < 0 || simulation.Tick >= uploadedTick + 4) {
                float capacity = nutrientCapacity;
                for (int i = 0; i < pixels.Length; i++) {
                    var sample = simulation.EnvironmentAt((i % grid + .5f) * world / grid,
                        (i / grid + .5f) * world / grid);
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
            Graphics.DrawMesh(plane, Matrix4x4.TRS(new Vector3(focus.x, -12, focus.z), Quaternion.identity,
                new Vector3(world * 9, 1, world * 9)), water, 0, camera, 0, null, ShadowCastingMode.Off, false);
            for (int layer = 0; layer < 3; layer++) {
                properties.SetFloat("_Layer", layer);
                Graphics.DrawMesh(plane, Matrix4x4.TRS(new Vector3(focus.x, -1.5f - layer * 3.3f, focus.z),
                    Quaternion.identity, new Vector3(world * 7, 1, world * 7)), cloud, 0, camera, 0, properties, ShadowCastingMode.Off, false);
            }
            // A deterministic visual field: never consumes the simulation's random source.
            for (int i = 0; i < matrices.Length; i++) {
                var point = motes[i]; var flow = simulation.EnvironmentAt(point.x, point.z);
                point.x = Mathf.Repeat(point.x + flow.CurrentX * (time - lastTime), world);
                point.z = Mathf.Repeat(point.z + flow.CurrentZ * (time - lastTime), world);
                motes[i] = point;
                int tile = Mathf.Min(grid-1,(int)(point.z/world*grid))*grid+Mathf.Min(grid-1,(int)(point.x/world*grid));
                float food = i % 7 == 0 ? 1 - Mathf.Exp(-simulation.GetOrganicEnergy(tile)/4) : Mathf.Clamp01(simulation.GetNutrient(tile)/nutrientCapacity);
                float size = (.025f + i % 5 * .018f) * Mathf.Sqrt(food);
                if (i % 3 == 0) point.y = .4f + Mathf.Sin(i) * .1f;
                point.x = focus.x + Observation.PeriodicDelta(point.x-focus.x,world);
                point.z = focus.z + Observation.PeriodicDelta(point.z-focus.z,world);
                matrices[i] = Matrix4x4.TRS(point + Vector3.up * (Mathf.Sin(time * .3f + i) * .08f), Quaternion.identity, new Vector3(size, size * .6f, size));
                colors[i] = i % 7 == 0 ? new Color(.9f, .62f, .3f, .36f) : new Color(.34f, .82f, .73f, .3f);
            }
            lastTime = time;
            properties.Clear(); properties.SetVectorArray("_InstanceColor", colors);
            Graphics.DrawMeshInstanced(mote, 0, particles, matrices, matrices.Length, properties, ShadowCastingMode.Off, false, 0, camera);
        }
        public void DrawVents(Simulation simulation, Vector3 focus, float time)
        {
            int used = 0;
            for (int i = 0; i < ventCount; i++) {
                Vector3 p = vents[i]; p.x = focus.x + Observation.PeriodicDelta(p.x-focus.x,world); p.z = focus.z + Observation.PeriodicDelta(p.z-focus.z,world);
                matrices[used] = Matrix4x4.TRS(p,Quaternion.Euler(10,i*47,15),new Vector3(.7f,1.2f+i%3*.3f,.6f));
                colors[used++] = new Color(.19f,.27f,.3f,1);
            }
            if (used > 0) {
                properties.Clear(); properties.SetVectorArray("_InstanceColor",colors);
                Graphics.DrawMeshInstanced(mote,0,minerals,matrices,used,properties,ShadowCastingMode.Off,false);
            }
            used = 0;
            for (int i = 0; i < ventCount; i++) {
                Vector3 p = vents[i];
                p.x = focus.x + Observation.PeriodicDelta(p.x-focus.x,world);
                p.z = focus.z + Observation.PeriodicDelta(p.z-focus.z,world);
                for (int j = 0; j < 4; j++) {
                    float phase = Mathf.Repeat(time * .16f + j * .25f + i,1);
                    matrices[used] = Matrix4x4.TRS(p + new Vector3(Mathf.Sin(i+j)*.2f,phase*2,Mathf.Cos(i+j)*.2f),
                        Quaternion.Euler(0,i*33,15),new Vector3(.3f+phase*.6f,.5f+phase,.3f+phase*.6f));
                    colors[used++] = new Color(.45f,.63f,.59f,(1-phase)*.12f);
                }
            }
            if (used == 0) return;
            properties.Clear(); properties.SetVectorArray("_InstanceColor",colors);
            Graphics.DrawMeshInstanced(mote,0,particles,matrices,used,properties,ShadowCastingMode.Off,false);
        }
        public void Dispose()
        {
            Object.Destroy(minerals); Object.Destroy(water); Object.Destroy(cloud); Object.Destroy(particles); Object.Destroy(resources); Object.Destroy(ecology);
        }
    }
}
