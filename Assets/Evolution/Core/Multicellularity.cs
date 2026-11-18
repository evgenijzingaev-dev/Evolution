using System;
using System.Collections.Generic;
namespace Evolution.Core
{
    public enum CellRole { Generalist, Feeding, Movement, Defense, Reproduction }

    public static class Multicellularity
    {
        public const float BondCost = .15f;
        public static int SizeLimit(Genome g, int maximum) => Math.Min(maximum, 2 + (int)(g.ColonySize * 10));
        public static bool Compatible(Genome a, Genome b) => a.Adhesion >= .35f && b.Adhesion >= .35f
            && Math.Abs(a.PreferredTemperature - b.PreferredTemperature) <= 8
            && Math.Abs(a.Size - b.Size) <= .6f && Math.Abs(a.OrganicSpecialization - b.OrganicSpecialization) <= .4f;
        public static CellRole Role(Genome g, int slot) => (CellRole)(1 + (slot + Math.Min(3, (int)(g.RolePattern * 4))) % 4);
        public static float Feeding(Cell c) => 1 + c.Specialization * (c.Role == CellRole.Feeding ? 1.2f : -.35f);
        public static float Motor(Cell c) => 1 + c.Specialization * (c.Role == CellRole.Movement ? 1.6f : -.4f);
        public static float DivisionThreshold(Cell c) => .85f + c.Specialization * (c.Role == CellRole.Reproduction ? -.12f : .1f);
        public static float MaturityAge(Cell c) => 5 * (1 + c.Specialization * (c.Role == CellRole.Reproduction ? -.35f : 2));
        public static float Cost(Cell c, float temperature, float protection = 0)
        {
            float basal = Biology.Cost(c.Genes, temperature);
            // Armor buffers thermal stress, never aging or basal maintenance. Tissues cost energy even in mild conditions.
            return basal - .025f * Math.Abs(temperature - c.Genes.PreferredTemperature) * Math.Min(.55f, protection)
                + (c.ColonyId == 0 ? 0 : .055f + .035f * c.Genes.Adhesion + .08f * c.Specialization);
        }
        // Conservative local diffusion: bounded by donor reserves, receiver room, gradient and transport rate.
        // Transport dissipates 5%; a zero transport genome cannot share energy.
        public static float Transfer(ref Cell a, ref Cell b, float dt)
        {
            float ca = Biology.Capacity(a.Genes), cb = Biology.Capacity(b.Genes);
            float gradient = a.Energy / ca - b.Energy / cb;
            if (gradient < 0) return TransferOneWay(ref b, ref a, -gradient, dt);
            return TransferOneWay(ref a, ref b, gradient, dt);
        }
        static float TransferOneWay(ref Cell donor, ref Cell receiver, float gradient, float dt)
        {
            float rate = Math.Min(donor.Genes.Transport, receiver.Genes.Transport);
            float equilibrium = gradient / (1 / Biology.Capacity(donor.Genes) + .95f / Biology.Capacity(receiver.Genes));
            float sent = Math.Max(0, Math.Min(equilibrium, Math.Min(Math.Max(0, donor.Energy),
                Math.Min(Math.Max(0, Biology.Capacity(receiver.Genes) - receiver.Energy) / .95f, rate * 4 * dt))));
            donor.Energy -= sent; receiver.Energy += sent * .95f;
            return sent * .95f;
        }
        internal static bool ValidFounder(Cell c, SimulationConfig config)
        {
            var g = c.Genes;
            return In(g.Size, .4f, 2) && In(g.Speed, .1f, 3) && In(g.Uptake, .2f, 3)
                && In(g.PreferredTemperature, 0, 50) && In(g.OrganicSpecialization, 0, 1)
                && In(g.Adhesion, 0, 1) && In(g.ColonySize, 0, 1) && In(g.Branching, 0, 1)
                && In(g.Differentiation, 0, 1) && In(g.Transport, 0, 1) && In(g.RolePattern, 0, 1)
                && In(c.Energy, .00001f, Biology.Capacity(g)) && In(c.Age, 0, config.MaxAge)
                && In(c.X, 0, config.WorldSize - .00001f) && In(c.Z, 0, config.WorldSize - .00001f)
                && !float.IsNaN(c.Heading) && !float.IsInfinity(c.Heading);
        }
        static bool In(float x, float min, float max) => x >= min && x <= max;
    }

    // A bounded adhesion forest. Older IDs are parents: compaction cannot invalidate edges or create cycles.
    // Buffers and dictionary are reused; topology and random draws are independent of rendering.
    internal sealed class ColonySystem
    {
        readonly Dictionary<int, int> indices;
        readonly int[] parent, root, size, children, heads, next, limits;
        readonly float[] motors, defense;
        readonly int contactGrid;
        public int ColonyCount { get; private set; }
        public int ColonialCells { get; private set; }
        public int LargestColony { get; private set; }
        public ColonySystem(int capacity, float worldSize)
        {
            contactGrid = Math.Max(2, (int)Math.Min(512, worldSize / 1.4f));
            indices = new Dictionary<int, int>(capacity);
            limits = new int[capacity]; parent = new int[capacity]; root = new int[capacity]; size = new int[capacity]; children = new int[capacity];
            motors = new float[capacity]; defense = new float[capacity]; heads = new int[contactGrid * contactGrid]; next = new int[capacity];
        }
        public void Prepare(Cell[] cells, int count, SimulationConfig cfg)
        {
            indices.Clear(); Array.Clear(size, 0, count); Array.Clear(children, 0, count);
            Array.Clear(motors, 0, count); Array.Clear(defense, 0, count);
            for (int i = 0; i < count; i++) {
                indices.Add(cells[i].Id, i);
                int p;
                parent[i] = cells[i].BondParentId > 0 && cells[i].BondParentId < cells[i].Id
                    && indices.TryGetValue(cells[i].BondParentId, out p) && Multicellularity.Compatible(cells[i].Genes, cells[p].Genes) ? p : -1;
                if (parent[i] < 0) { cells[i].BondParentId = 0; cells[i].BondAngle = 0; }
                root[i] = parent[i] < 0 ? i : root[parent[i]];
                if (parent[i] >= 0) children[parent[i]]++;
                int r = root[i];
                int limit = Multicellularity.SizeLimit(cells[i].Genes, cfg.MaxColonyCells);
                limits[r] = size[r] == 0 ? limit : Math.Min(limits[r], limit);
                size[r]++; motors[r] += cells[i].Genes.Speed * Multicellularity.Motor(cells[i]);
                if (cells[i].Role == CellRole.Defense) defense[r] += cells[i].Specialization;
            }
            ColonyCount = ColonialCells = 0; LargestColony = 0;
            for (int i = 0; i < count; i++) {
                cells[i].ColonyId = size[root[i]] > 1 ? cells[root[i]].Id : 0;
                if (root[i] == i && size[i] > 1) { ColonyCount++; ColonialCells += size[i]; LargestColony = Math.Max(LargestColony, size[i]); }
            }
        }
        bool HasSocket(Cell c, int i) => children[i] < (c.Genes.Branching > .45f ? 2 : 1);
        bool Attach(ref Cell anchor, ref Cell bud, int i, SimulationConfig cfg)
        {
            int r = root[i];
            if (!HasSocket(anchor, i) || size[r] >= limits[r]
                || size[r] >= Multicellularity.SizeLimit(bud.Genes, cfg.MaxColonyCells)
                || !Multicellularity.Compatible(anchor.Genes, bud.Genes)
                || anchor.Energy <= Multicellularity.BondCost || bud.Energy <= Multicellularity.BondCost) return false;
            anchor.Energy -= Multicellularity.BondCost; bud.Energy -= Multicellularity.BondCost;
            bud.BondParentId = anchor.Id; bud.TissueSlot = anchor.TissueSlot + 1 + children[i];
            bud.BondAngle = anchor.Genes.Branching > .45f ? (children[i] == 0 ? -1 : 1) * (.5f + anchor.Genes.Branching * .6f) : 0;
            children[i]++; size[r]++; limits[r] = Math.Min(limits[r], Multicellularity.SizeLimit(bud.Genes, cfg.MaxColonyCells)); return true;
        }
        public void Retain(ref Cell anchor, ref Cell bud, int i, SimulationConfig cfg, ref RandomSource rng)
        {
            if (rng.Next() < Math.Min(anchor.Genes.Adhesion, bud.Genes.Adhesion)) Attach(ref anchor, ref bud, i, cfg);
        }
        // Nearby solitary cells may adhere; existing organisms do not merge into unbounded aggregates.
        public void Contact(Cell[] cells, int count, SimulationConfig cfg, ref RandomSource rng)
        {
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            float width = cfg.WorldSize / contactGrid;
            for (int i = count - 1; i >= 0; i--) {
                int tile = Bucket(cells[i].Z, width) * contactGrid + Bucket(cells[i].X, width);
                next[i] = heads[tile]; heads[tile] = i;
            }
            int radius = (int)Math.Ceiling(1.4f / width);
            for (int i = 0; i < count; i++) {
                if (size[root[i]] != 1 || cells[i].Genes.Adhesion < .35f || cells[i].Age + cfg.StepSeconds >= cfg.MaxAge) continue;
                int x = Bucket(cells[i].X, width), z = Bucket(cells[i].Z, width);
                int span = Math.Min(contactGrid, radius * 2 + 1);
                bool attached = false; int examined = 0;
                for (int dz = 0; dz < span && !attached && examined < 64; dz++) for (int dx = 0; dx < span && !attached && examined < 64; dx++) {
                    int tx = ((x - radius + dx) % contactGrid + contactGrid) % contactGrid;
                    int tz = ((z - radius + dz) % contactGrid + contactGrid) % contactGrid;
                    for (int j = heads[tz * contactGrid + tx]; j >= 0 && examined < 64; j = next[j]) {
                        examined++;
                        if (cells[j].Id >= cells[i].Id || !HasSocket(cells[j], j) || cells[j].Age + cfg.StepSeconds >= cfg.MaxAge) continue;
                        float a = Delta(cells[i].X - cells[j].X, cfg.WorldSize), b = Delta(cells[i].Z - cells[j].Z, cfg.WorldSize);
                        float reach = .35f * (cells[i].Genes.Size + cells[j].Genes.Size);
                        if (a * a + b * b > reach * reach || !Multicellularity.Compatible(cells[i].Genes, cells[j].Genes)) continue;
                        if (rng.Next() < Math.Min(cells[i].Genes.Adhesion, cells[j].Genes.Adhesion) * cfg.StepSeconds * 2
                            && Attach(ref cells[j], ref cells[i], j, cfg)) {
                            root[i] = root[j]; attached = true; break;
                        }
                    }
                }
            }
        }
        public float Protection(int i) => .8f * defense[root[i]] / Math.Max(1, size[root[i]]);
        public void Develop(ref Cell c, float dt)
        {
            if (c.ColonyId != 0) {
                c.Role = c.Genes.Differentiation > 0 ? Multicellularity.Role(c.Genes, c.TissueSlot) : CellRole.Generalist;
                c.Specialization = Math.Min(c.Genes.Differentiation, c.Specialization + dt * .08f * c.Genes.Differentiation);
            } else {
                c.Specialization = Math.Max(0, c.Specialization - dt * .2f);
                if (c.Specialization == 0) c.Role = CellRole.Generalist;
            }
        }
        public void Move(Cell[] cells, int count, SimulationConfig cfg, ref RandomSource rng, float time = 0, int controlledId = 0, float inputX = 0, float inputZ = 0)
        {
            int controlledRoot = indices.TryGetValue(controlledId, out int controlledIndex) ? root[controlledIndex] : -1;
            for (int i = 0; i < count; i++) if (parent[i] < 0) {
                float autonomousTurn = rng.Range(-1, 1) * cfg.StepSeconds * 2 / (1 + .15f * (size[i] - 1));
                if (i != controlledRoot) cells[i].Heading += autonomousTurn;
                float speed = motors[i] / size[i] / (1 + .09f * (size[i] - 1));
                if (i == controlledRoot) {
                    float throttle = (float)Math.Sqrt(inputX * inputX + inputZ * inputZ);
                    if (throttle > 0) cells[i].Heading = (float)Math.Atan2(inputZ, inputX);
                    speed *= Math.Min(1, throttle);
                }
                cells[i].X = Wrap(cells[i].X + (float)Math.Cos(cells[i].Heading) * speed * cfg.StepSeconds, cfg.WorldSize);
                cells[i].Z = Wrap(cells[i].Z + (float)Math.Sin(cells[i].Heading) * speed * cfg.StepSeconds, cfg.WorldSize);
                if (cfg.OceanZones) {
                    var flow = OceanEnvironment.Sample(cells[i].X, cells[i].Z, cfg.WorldSize, time, cfg.Temperature, true);
                    cells[i].X = Wrap(cells[i].X + flow.CurrentX * cfg.StepSeconds, cfg.WorldSize);
                    cells[i].Z = Wrap(cells[i].Z + flow.CurrentZ * cfg.StepSeconds, cfg.WorldSize);
                }
            }
            Layout(cells, count, cfg.WorldSize);
        }
        public void Layout(Cell[] cells, int count, float worldSize)
        {
            for (int i = 0; i < count; i++) if (parent[i] >= 0) {
                Cell p = cells[parent[i]];
                float h = p.Heading + cells[i].BondAngle, distance = .29f * (p.Genes.Size + cells[i].Genes.Size);
                cells[i].Heading = h;
                cells[i].X = Wrap(p.X + (float)Math.Cos(h) * distance, worldSize);
                cells[i].Z = Wrap(p.Z + (float)Math.Sin(h) * distance, worldSize);
            }
        }
        public float Exchange(Cell[] cells, int count, float dt, float maxAge)
        {
            float received = 0;
            // One bounded pass over real edges. Senescent cells cannot siphon energy before dying.
            for (int i = 0; i < count; i++) if (parent[i] >= 0 && cells[i].Age < maxAge && cells[parent[i]].Age < maxAge)
                received += Multicellularity.Transfer(ref cells[parent[i]], ref cells[i], dt);
            return received;
        }
        int Bucket(float position, float width) => Math.Min(contactGrid - 1, (int)(position / width));
        static float Delta(float d, float world) => d - (float)Math.Floor(d / world + .5f) * world;
        static float Wrap(float x, float world)
        {
            float wrapped = x - (float)Math.Floor(x / world) * world;
            // A tiny negative input can round to exactly world when adding the period.
            return wrapped >= world ? 0 : Math.Max(0, wrapped);
        }
    }
}
