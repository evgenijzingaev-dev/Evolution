using System.Collections.Generic;
using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    // A few instanced passes, no per-cell objects/materials. Visual state never enters biology.
    internal sealed class OrganismRenderer
    {
        readonly Mesh mesh, membraneMesh;
        readonly Material body, membrane;
        Material current;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] shapes = new Vector4[1023];
        Vector4 shape;
        struct Remnant { public Vector3 Position; public Color Color; public float Time, Size; }
        readonly Remnant[] remnants = new Remnant[128];
        int remnantCursor;
        readonly Dictionary<int, Cell> previousTick = new Dictionary<int, Cell>();
        readonly Dictionary<int, float> headings = new Dictionary<int, float>();
        readonly Dictionary<int, Cell> lastCells = new Dictionary<int, Cell>();
        readonly Dictionary<int, float> divisions = new Dictionary<int, float>();
        readonly Vector4[] colors = new Vector4[1023];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly Dictionary<int, Cell> byId;
        readonly Dictionary<int, Vector3> positions;
        readonly List<int> expired = new List<int>();
        int used;
        float world, visualTime;
        bool closeView;
        Vector3 cameraFocus;
        float visibleRadius;
        bool Visible(Cell c) => new Vector2(Observation.PeriodicDelta(c.X-cameraFocus.x,world), Observation.PeriodicDelta(c.Z-cameraFocus.z,world)).sqrMagnitude < visibleRadius * visibleRadius;
        public void Capture(Simulation simulation)
        {
            previousTick.Clear();
            for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); previousTick.Add(c.Id, c); }
        }
        Quaternion Rotation(Cell c) => Quaternion.Euler(Mathf.Sin(visualTime * (2 + c.SwimSpeed) + c.Id) * c.SwimSpeed * 3,
            -(headings.TryGetValue(c.Id, out float h) ? h : c.Heading) * Mathf.Rad2Deg, -c.AngularVelocity * 5);
        public OrganismRenderer(Mesh mesh, Material body, Material membrane, int capacity, Mesh membraneMesh)
        {
            this.mesh = mesh; this.membraneMesh = membraneMesh; this.body = body; this.membrane = membrane;
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
        static Color StrategyColor(Cell c)
        {
            var g = c.Genes;
            Color color = Color.Lerp(new Color(.2f, .65f, .85f), new Color(.2f, .95f, .35f), g.Photosynthesis);
            color = Color.Lerp(color, new Color(.95f, .35f, .16f), g.Predation);
            return Color.Lerp(color, new Color(.9f, .72f, .4f), g.Membrane * .5f);
        }
        Color Tint(Cell c, int mode) => mode == 3 ? Color.Lerp(StrategyColor(c), RoleColor(c.Role), c.Specialization * .55f) : mode == 1 ? Color.Lerp(new Color(.18f, .9f, .55f), new Color(1, .52f, .24f), c.Genes.OrganicSpecialization)
            : mode == 2 ? Color.Lerp(RoleColor(CellRole.Generalist), RoleColor(c.Role), c.Specialization)
            : Color.Lerp(new Color(.2f, .5f, 1), new Color(1, .3f, .22f), c.Genes.PreferredTemperature / 50);
        public void Draw(Simulation simulation, int mode, float worldSize, int selectedId, float time, float interpolation, float cameraDistance, Vector3 focus, bool detailed)
        {
            cameraFocus = focus; visibleRadius = cameraDistance * 2.5f + 5; closeView = detailed && cameraDistance < 18; membrane.SetFloat("_OceanTime", time);
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
                float heading = c.Heading;
                if (previousTick.TryGetValue(c.Id, out var previous)) {
                    Vector3 origin = Center(previous), delta = point - origin;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    point = origin + delta * interpolation;
                    point.x = Mathf.Repeat(point.x, world); point.z = Mathf.Repeat(point.z, world);
                    heading = previous.Heading + CellMotion.AngleDelta(c.Heading - previous.Heading) * interpolation;
                }
                headings[c.Id] = heading;
                positions[c.Id] = point;
            }
            expired.Clear(); foreach (int id in positions.Keys) if (!byId.ContainsKey(id)) expired.Add(id);
            foreach (int id in expired) { positions.Remove(id); divisions.Remove(id); headings.Remove(id); }
            lastCells.Clear(); foreach (var pair in byId) lastCells.Add(pair.Key, pair.Value);
            byId.TryGetValue(selectedId, out var selected);
            // Opaque organelles first, translucent membranes last; transparent depth writes are disabled.
            current = body; shape = Vector4.zero;
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); if (!Visible(c)) continue; Vector3 p = Position(c);
                var r = Rotation(c);
                float growth = Mathf.Lerp(.55f, 1, c.Growth);
                float s = c.Genes.Size * growth;
                Color color = Tint(c, mode);
                Add(p, r, new Vector3(.26f, .29f, .27f) * s, Color.Lerp(color, new Color(.35f, .2f, .55f), .65f));
                Add(p + r * new Vector3(.03f, .08f, .04f) * s, r, Vector3.one * .09f * s, new Color(.8f, .8f, 1));
                for (int j = 0; j < (closeView ? 3 + (int)(c.Genes.Uptake * 1.4f) : 2); j++) {
                    float angle = j * Mathf.PI * .5f + c.Id + Mathf.Sin(visualTime * .8f + j) * .12f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * .22f, .02f, Mathf.Sin(angle) * .2f) * s;
                    Add(p + r * offset, r * Quaternion.Euler(0, j * 45, 20), new Vector3(.12f, .08f, .065f) * s, Color.Lerp(color, Color.white, .35f));
                }
                // Chloroplast stacks, capture jaws and shell plates express continuous traits.
                if (c.Genes.Photosynthesis > .25f) {
                    for (int j = 0; j < (closeView ? 4 : 2); j++) {
                        float angle = j * 1.5708f + c.Id;
                        Add(p + r * new Vector3(Mathf.Cos(angle) * .24f, .14f, Mathf.Sin(angle) * .22f) * s,
                            r * Quaternion.Euler(0, j * 45, 0), new Vector3(.15f, .04f, .1f) * s * (.6f + c.Genes.Photosynthesis), Color.Lerp(new Color(.08f, .3f, .16f), new Color(.4f, 1, .3f), Mathf.Clamp01(c.Budget.Light * 30)));
                    }
                }
                if (c.Genes.Predation > .25f) {
                    float opening = c.AttackLastStep > 0 ? .15f + .1f * Mathf.Sin(visualTime * 18) : .1f;
                    for (int side = -1; side <= 1; side += 2)
                        Add(p + r * new Vector3(.4f, .01f, side * opening) * s,
                            r * Quaternion.Euler(0, side * 28, 0), new Vector3(.28f, .065f, .055f) * s * c.Genes.Predation,
                            new Color(.95f, .4f, .18f));
                }
                if (closeView && c.Genes.Membrane > .35f) {
                    for (int j = 0; j < 6; j++) {
                        float angle = j * Mathf.PI / 3;
                        Add(p + r * new Vector3(Mathf.Cos(angle) * .34f, .1f, Mathf.Sin(angle) * .31f) * s, r * Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0),
                            new Vector3(.09f, .12f, .22f) * s * c.Genes.Membrane, new Color(.8f, .67f, .38f));
                    }
                }
                if (closeView && c.Activity == CellActivity.Hunting && byId.TryGetValue(c.TargetId, out var prey)) {
                    Vector3 delta = Position(prey) - p;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    for (int j = 0; j < 3; j++) {
                        float phase = Mathf.Repeat(visualTime * .7f + j / 3f, 1);
                        Add(p + delta * phase + Vector3.up * .08f, r, Vector3.one * .025f * s, new Color(.95f, .48f, .22f));
                    }
                }
                if (c.AttackLastStep > 0) {
                    float bite = Mathf.Repeat(visualTime * 3 + c.Id * .17f, 1);
                    Add(p + r * new Vector3(.65f - bite * .3f, .1f, 0) * s, r, Vector3.one * .09f * s, new Color(1, .25f, .08f));
                }
                if (Mathf.Abs(c.TransportLastStep) > .00001f && byId.TryGetValue(c.BondParentId, out var donor)) {
                    Vector3 delta = Position(donor) - p;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    float phase = Mathf.Repeat(visualTime * .8f + c.Id * .31f, 1);
                    if (c.TransportLastStep > 0) phase = 1 - phase;
                    Add(p + delta * phase + Vector3.up * .09f, r, Vector3.one * .045f * s, new Color(.75f, 1, .7f));
                }
                // Motility structures are inherited; tissue development adds further specialization.
                if (closeView && c.Genes.Speed > .8f) {
                    for (int j = 0; j < 5; j++) {
                        float wave = Mathf.Sin(visualTime * (2 + c.SwimSpeed * 5) - j * .9f + c.Id) * .08f * j;
                        Add(p + r * new Vector3(-.38f - j * .11f, wave * .25f, wave) * s,
                            r * Quaternion.Euler(0, -wave * 100, 0), new Vector3(.18f, .025f, .027f) * s, color);
                    }
                }
                if (closeView && c.Genes.Uptake > 1.05f) {
                    for (int j = 0; j < 6; j++) {
                        float angle = j * Mathf.PI / 3, wave = Mathf.Sin(visualTime * 7 + j + c.Id) * .035f;
                        Add(p + r * new Vector3(Mathf.Cos(angle) * .43f, wave, Mathf.Sin(angle) * .4f) * s,
                            r * Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 15), new Vector3(.16f, .018f, .018f) * s, color);
                    }
                }
                float d = c.Specialization;
                if (closeView && d > .15f) {
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
                if (c.FoodLastStep > .00001f) {
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
                var c = simulation.GetCell(i); if (!Visible(c)) continue; var p = Position(c);
                var r = Rotation(c);
                float growth = Mathf.Lerp(.55f, 1, c.Growth);
                float s = c.Genes.Size * growth * (1 + .025f * Mathf.Sin(visualTime * 2 + c.Id));
                float stretch = 1 + (c.Genes.Speed - .6f) * .10f + c.SwimSpeed * .06f - (c.Defending ? .12f : 0) + c.Specialization * (c.Role == CellRole.Movement ? .2f : -.08f);
                float pinch = divisions.TryGetValue(c.Id, out float dividedAt) ? Mathf.Clamp01(1 - (time - dividedAt) / .8f) : 0;
                shape = new Vector4((c.Genes.Branching * .14f + c.Digestion * .07f + Mathf.Abs(c.AngularVelocity) * .01f), 2 + Mathf.Floor(c.Genes.Adhesion * 4), c.Genes.RolePattern * 6.28f, pinch);
                Color color = Tint(c, mode);
                var environment = simulation.EnvironmentAt(c.X, c.Z);
                color = Color.Lerp(color, new Color(.65f, .25f, .8f), environment.Toxicity * (1 - c.Genes.ToxinResistance) * .3f);
                color = Color.Lerp(color, environment.Temperature > c.Genes.PreferredTemperature ? new Color(1, .4f, .18f) : new Color(.3f, .6f, 1),
                    Mathf.Clamp01(Mathf.Abs(environment.Temperature - c.Genes.PreferredTemperature) / 25) * .3f);
                if (c.Energy < Biology.Capacity(c.Genes) * .2f) color *= .65f + .15f * Mathf.Sin(visualTime * 3);
                if (c.Defending) color = Color.Lerp(color, new Color(1, .85f, .4f), .4f);
                if (c.RepairLastStep > 0) color = Color.Lerp(color, new Color(.4f, 1, .8f), .2f);
                if (c.DamageLastStep > 0) color = Color.Lerp(color, new Color(1, .18f, .1f), .65f);
                color.a = .22f + .16f * c.Genes.Membrane;
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
            // One periodic image per object: camera travel never creates biological duplicates.
            p.x = cameraFocus.x + Observation.PeriodicDelta(p.x - cameraFocus.x, world);
            p.z = cameraFocus.z + Observation.PeriodicDelta(p.z - cameraFocus.z, world);
            matrices[used] = Matrix4x4.TRS(p, rotation, scale);
            shapes[used] = shape; colors[used++] = color; if (used == matrices.Length) Flush();
        }

        void Flush()
        {
            if (used == 0) return;
            properties.SetVectorArray("_Shape", shapes); properties.SetVectorArray("_InstanceColor", colors); properties.SetFloat("_WorldSize", world);
            Graphics.DrawMeshInstanced(current == membrane && closeView ? membraneMesh : mesh, 0, current, matrices, used, properties, ShadowCastingMode.Off, false);
            used = 0;
        }
    }
}
