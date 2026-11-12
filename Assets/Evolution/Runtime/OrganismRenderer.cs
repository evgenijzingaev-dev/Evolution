using System.Collections.Generic;
using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    // A few instanced passes, no per-cell objects/materials. Visual state never enters biology.
    internal sealed class OrganismRenderer
    {
        readonly Mesh mesh;
        readonly Material body, membrane;
        Material current;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] shapes = new Vector4[1023];
        Vector4 shape;
        struct Remnant { public Vector3 Position; public Color Color; public float Time, Size; }
        readonly Remnant[] remnants = new Remnant[128];
        int remnantCursor;
        readonly Dictionary<int, Cell> lastCells = new Dictionary<int, Cell>();
        readonly Dictionary<int, float> divisions = new Dictionary<int, float>();
        readonly Vector4[] colors = new Vector4[1023];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly Dictionary<int, Cell> byId;
        readonly Dictionary<int, Vector3> positions;
        readonly List<int> expired = new List<int>();
        int used;
        float world, visualTime;
        public OrganismRenderer(Mesh mesh, Material body, Material membrane, int capacity)
        {
            this.mesh = mesh; this.body = body; this.membrane = membrane;
            byId = new Dictionary<int, Cell>(capacity); positions = new Dictionary<int, Vector3>(capacity);
        }
        public static Vector3 Center(Cell c) => new Vector3(c.X, .54f, c.Z);
        public Vector3 Position(Cell c) => positions.TryGetValue(c.Id, out var p) ? p : Center(c);
        public static Color RoleColor(CellRole role)
        {
            switch (role) {
                case CellRole.Feeding: return new Color(.3f, .95f, .65f);
                case CellRole.Movement: return new Color(.22f, .75f, 1);
                case CellRole.Defense: return new Color(1, .72f, .3f);
                case CellRole.Reproduction: return new Color(.95f, .35f, .7f);
                default: return new Color(.4f, .85f, .85f);
            }
        }
        Color Tint(Cell c, int mode) => mode == 1 ? Color.Lerp(new Color(.18f, .9f, .55f), new Color(1, .52f, .24f), c.Genes.OrganicSpecialization)
            : mode == 2 ? Color.Lerp(RoleColor(CellRole.Generalist), RoleColor(c.Role), c.Specialization)
            : Color.Lerp(new Color(.2f, .5f, 1), new Color(1, .3f, .22f), c.Genes.PreferredTemperature / 50);
        public void Draw(Simulation simulation, int mode, float worldSize, int selectedId, float time, float frameSeconds)
        {
            world = worldSize; visualTime = time; used = 0; byId.Clear();
            for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); byId.Add(c.Id, c); }
            foreach (var entry in lastCells) {
                if (byId.ContainsKey(entry.Key)) continue;
                var c = entry.Value;
                remnants[remnantCursor++ % remnants.Length] = new Remnant { Position = Position(c), Color = Tint(c, mode), Time = time, Size = c.Genes.Size };
            }
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i);
                if (!lastCells.ContainsKey(c.Id) && c.ParentId > 0 && c.Age < .7f) divisions[c.ParentId] = time;

                Vector3 point = Center(c);
                if (positions.TryGetValue(c.Id, out var previous)) {
                    Vector3 delta = point - previous;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    point = previous + delta * (1 - Mathf.Exp(-18 * frameSeconds));
                    point.x = Mathf.Repeat(point.x, world); point.z = Mathf.Repeat(point.z, world);
                }
                positions[c.Id] = point;
            }
            expired.Clear(); foreach (int id in positions.Keys) if (!byId.ContainsKey(id)) expired.Add(id);
            foreach (int id in expired) { positions.Remove(id); divisions.Remove(id); }
            lastCells.Clear(); foreach (var pair in byId) lastCells.Add(pair.Key, pair.Value);
            byId.TryGetValue(selectedId, out var selected);
            int grid = simulation.Config.GridSize;
            // Opaque organelles first, translucent membranes last; transparent depth writes are disabled.
            current = body; shape = Vector4.zero;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); Vector3 p = Position(c);
                var r = Quaternion.Euler(0, -c.Heading * Mathf.Rad2Deg, 0);
                float growth = c.Generation == 0 ? 1 : Mathf.Lerp(.55f, 1, Mathf.Clamp01(c.Age / .7f));
                float s = c.Genes.Size * growth;
                Color color = Tint(c, mode);
                Add(p, r, new Vector3(.26f, .29f, .27f) * s, Color.Lerp(color, new Color(.35f, .2f, .55f), .65f));
                Add(p + r * new Vector3(.03f, .08f, .04f) * s, r, Vector3.one * .09f * s, new Color(.8f, .8f, 1));
                for (int j = 0; j < 3 + (int)(c.Genes.Uptake * 1.4f); j++) {
                    float angle = j * Mathf.PI * .5f + c.Id;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * .22f, .02f, Mathf.Sin(angle) * .2f) * s;
                    Add(p + r * offset, r * Quaternion.Euler(0, j * 45, 20), new Vector3(.12f, .08f, .065f) * s, Color.Lerp(color, Color.white, .35f));
                }
                // Motility structures are inherited; tissue development adds further specialization.
                if (c.Genes.Speed > .8f) {
                    for (int j = 0; j < 5; j++) {
                        float wave = Mathf.Sin(visualTime * (3 + c.Genes.Speed) - j * .9f + c.Id) * .08f * j;
                        Add(p + r * new Vector3(-.38f - j * .11f, wave * .25f, wave) * s,
                            r * Quaternion.Euler(0, -wave * 100, 0), new Vector3(.18f, .025f, .027f) * s, color);
                    }
                }
                if (c.Genes.Uptake > 1.05f) {
                    for (int j = 0; j < 6; j++) {
                        float angle = j * Mathf.PI / 3, wave = Mathf.Sin(visualTime * 7 + j + c.Id) * .035f;
                        Add(p + r * new Vector3(Mathf.Cos(angle) * .43f, wave, Mathf.Sin(angle) * .4f) * s,
                            r * Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 15), new Vector3(.16f, .018f, .018f) * s, color);
                    }
                }
                float d = c.Specialization;
                if (d > .15f) {
                    if (c.Role == CellRole.Movement) {
                        for (int j = 0; j < 3; j++) {
                            float wave = Mathf.Sin(visualTime * 5 + c.Id + j * .8f) * .07f;
                            Add(p + r * new Vector3(-.4f - j * .12f, wave, wave) * s, r * Quaternion.Euler(0, wave * 90, 0), new Vector3(.22f, .035f, .04f) * s, color);
                        }
                    } else if (c.Role == CellRole.Defense) {
                        for (int j = 0; j < 5; j++) {
                            float angle = j * Mathf.PI * .4f;
                            Add(p + r * new Vector3(Mathf.Cos(angle) * .25f, .14f, Mathf.Sin(angle) * .24f) * s, r,
                                new Vector3(.19f, .08f, .18f) * s, color);
                        }
                    } else if (c.Role == CellRole.Feeding) {
                        Add(p + r * new Vector3(.25f, .05f, 0) * s, r, new Vector3(.18f, .21f, .24f) * s, color * .65f);
                    } else if (c.Role == CellRole.Reproduction) {
                        Add(p + r * new Vector3(-.19f, .09f, .14f) * s, r, Vector3.one * .17f * s, color);
                    }
                }
                // Nutrient motes move into the membrane only while local food and reserve room exist.
                int tile = Mathf.Min(grid - 1, (int)(c.Z / world * grid)) * grid + Mathf.Min(grid - 1, (int)(c.X / world * grid));
                if (c.Energy < Biology.Capacity(c.Genes) * .97f && (simulation.GetNutrient(tile) > .01f || simulation.GetOrganicEnergy(tile) > .01f)) {
                    float phase = Mathf.Repeat(visualTime * .8f + c.Id * .37f, 1);
                    float angle = c.Id * 2.4f;
                    Add(p + new Vector3(Mathf.Cos(angle), .25f, Mathf.Sin(angle)) * (.65f * (1 - phase) + .16f) * s,
                        r, Vector3.one * .045f * s, new Color(.45f, 1, .65f));
                }
                if (selected.Id > 0 && Observation.SameOrganism(selected, c)) {
                    // A ring of beads remains visible from every orbit angle, without covering organelles.
                    for (int j = 0; j < 12; j++) {
                        float angle = j * Mathf.PI / 6;
                        Add(p + new Vector3(Mathf.Cos(angle) * .48f, .02f, Mathf.Sin(angle) * .45f) * s,
                            r, Vector3.one * .045f * s, new Color(.55f, 1, 1));
                    }
                }
            }
            Flush(); current = membrane;
            for (int i = 0; i < remnants.Length; i++) {
                var remnant = remnants[i]; float age = time - remnant.Time;
                if (remnant.Size <= 0 || age < 0 || age > 1.4f) continue;
                var faded = remnant.Color; faded.a = .2f * (1 - age / 1.4f);
                for (int j = 0; j < 4; j++) {
                    float angle = j * Mathf.PI * .5f;
                    Add(remnant.Position + new Vector3(Mathf.Cos(angle), .3f, Mathf.Sin(angle)) * age * .2f,
                        Quaternion.identity, Vector3.one * remnant.Size * (.2f + age * .2f), faded);
                }
            }
            Flush();
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); var p = Position(c);
                var r = Quaternion.Euler(0, -c.Heading * Mathf.Rad2Deg, 0);
                float growth = c.Generation == 0 ? 1 : Mathf.Lerp(.55f, 1, Mathf.Clamp01(c.Age / .7f));
                float s = c.Genes.Size * growth * (1 + .025f * Mathf.Sin(visualTime * 2 + c.Id));
                float stretch = 1 + (c.Genes.Speed - .6f) * .10f + c.Specialization * (c.Role == CellRole.Movement ? .2f : -.08f);
                float pinch = divisions.TryGetValue(c.Id, out float dividedAt) ? Mathf.Clamp01(1 - (time - dividedAt) / .8f) : 0;
                shape = new Vector4(c.Genes.Branching * .085f, 2 + Mathf.Floor(c.Genes.Adhesion * 4), c.Genes.RolePattern * 6.28f, pinch);
                membrane.SetFloat("_OceanTime", time);
                Color color = Tint(c, mode); color.a = .22f;
                Add(p, r, new Vector3(.82f * stretch, .65f, .76f) * s, color);
                shape = Vector4.zero;
                if (c.BondParentId > 0 && byId.TryGetValue(c.BondParentId, out var parent)) {
                    Vector3 delta = Position(parent) - p;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    if (delta.sqrMagnitude > .0001f) {
                        float thickness = .25f * Mathf.Min(s, parent.Genes.Size);
                        Add(p + delta * .5f, Quaternion.FromToRotation(Vector3.right, delta.normalized),
                            new Vector3(delta.magnitude + thickness, thickness, thickness), color);
                    }
                }
            }
            Flush();
        }
        void Add(Vector3 p, Quaternion rotation, Vector3 scale, Color color)
        {
            float radius = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * .5f;
            int minX = p.x + radius > world ? -1 : 0, maxX = p.x - radius < 0 ? 1 : 0;
            int minZ = p.z + radius > world ? -1 : 0, maxZ = p.z - radius < 0 ? 1 : 0;
            for (int x = minX; x <= maxX; x++) for (int z = minZ; z <= maxZ; z++) {
                matrices[used] = Matrix4x4.TRS(p + new Vector3(x * world, 0, z * world), rotation, scale);
                shapes[used] = shape; colors[used++] = color; if (used == matrices.Length) Flush();
            }
        }
        void Flush()
        {
            if (used == 0) return;
            properties.SetVectorArray("_Shape", shapes); properties.SetVectorArray("_InstanceColor", colors); properties.SetFloat("_WorldSize", world);
            Graphics.DrawMeshInstanced(mesh, 0, current, matrices, used, properties, ShadowCastingMode.Off, false);
            used = 0;
        }
    }
}
