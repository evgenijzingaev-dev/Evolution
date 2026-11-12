using System;
namespace Evolution.Core
{
    [Serializable]
    public sealed class SimulationConfig
    {
        public int Capacity = 4096, InitialPopulation = 160, GridSize = 32;
        public float WorldSize = 40, StepSeconds = .05f, InitialNutrients = 4,
            NutrientRegeneration = .12f, NutrientCapacity = 8, Temperature = 22,
            MutationProbability = .15f, MutationStrength = .06f, MaxAge = 180,
            InitialOrganicEnergy = 0, OrganicDecayRate = .02f;
        public void Validate()
        {
            if (Capacity < 1 || InitialPopulation < 0 || InitialPopulation > Capacity || GridSize < 2 || GridSize > 512 ||
                !Positive(WorldSize) || !Positive(StepSeconds) || !Positive(MaxAge) || !Positive(NutrientCapacity) ||
                !Nonnegative(InitialNutrients) || !Nonnegative(NutrientRegeneration) ||
                !Nonnegative(InitialOrganicEnergy) || !Nonnegative(OrganicDecayRate) ||
                !Nonnegative(MutationStrength) || !Nonnegative(MutationProbability) || MutationProbability > 1 ||
                float.IsNaN(Temperature) || float.IsInfinity(Temperature)) throw new ArgumentException("Invalid simulation configuration");
        }
        static bool Positive(float x) => x > 0 && !float.IsInfinity(x);
        static bool Nonnegative(float x) => x >= 0 && !float.IsInfinity(x);
        public SimulationConfig Copy() => (SimulationConfig)MemberwiseClone();
    }
    public struct Cell
    {
        public int Id, ParentId, Generation;
        public Genome Genes;
        public float X, Z, Heading, Energy, Age;
    }
    // Dense storage, stable iteration and deferred births. No engine references or per-cell objects.
    public sealed class Simulation
    {
        readonly SimulationConfig config;
        readonly Cell[] cells;
        readonly float[] nutrients, organicEnergy, pendingRemains;
        RandomSource random;
        int nextId;
        public int Count { get; private set; }
        public long Tick { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }
        public float Temperature { get; private set; }
        public float NutrientRegeneration { get; private set; }
        public SimulationConfig Config => config.Copy();
        public uint RandomState => random.State;
        public Cell GetCell(int i) { if (i < 0 || i >= Count) throw new ArgumentOutOfRangeException(nameof(i)); return cells[i]; }
        public float GetNutrient(int i) => nutrients[i];
        public float GetOrganicEnergy(int i) => organicEnergy[i];
        public Simulation(SimulationConfig settings, uint seed)
        {
            settings.Validate(); config = settings.Copy(); random = new RandomSource(seed);
            cells = new Cell[config.Capacity]; nutrients = new float[config.GridSize * config.GridSize];
            organicEnergy = new float[nutrients.Length]; pendingRemains = new float[nutrients.Length];
            Temperature = config.Temperature; NutrientRegeneration = config.NutrientRegeneration;
            for (int i = 0; i < nutrients.Length; i++) {
                nutrients[i] = Math.Min(config.InitialNutrients, config.NutrientCapacity);
                organicEnergy[i] = config.InitialOrganicEnergy;
            }
            for (int i = 0; i < config.InitialPopulation; i++) {
                var g = Genome.Founder(ref random);
                cells[Count++] = new Cell { Id = ++nextId, ParentId = -1, Genes = g, Energy = Biology.Capacity(g) * .6f,
                    X = random.Range(0, config.WorldSize), Z = random.Range(0, config.WorldSize), Heading = random.Range(0, 6.283185f) };
            }
        }
        // Apply environmental commands at tick boundaries for replay.
        public void SetEnvironment(float temperature, float regeneration)
        {
            if (float.IsNaN(temperature) || float.IsInfinity(temperature) || float.IsNaN(regeneration) || float.IsInfinity(regeneration) || regeneration < 0)
                throw new ArgumentException("Invalid environment");
            Temperature = temperature; NutrientRegeneration = regeneration;
        }
        public void Step()
        {
            float dt = config.StepSeconds;
            for (int i = 0; i < nutrients.Length; i++) {
                nutrients[i] = Math.Min(config.NutrientCapacity, nutrients[i] + NutrientRegeneration * dt);
                // Decay dissipates energy; it does not create another energy-bearing resource.
                organicEnergy[i] *= Math.Max(0, 1 - config.OrganicDecayRate * dt);
            }
            int originalCount = Count, survivors = 0, births = 0;
            // Births use the free tail; stable compaction below cannot overwrite it.
            for (int i = 0; i < originalCount; i++) {
                Cell c = cells[i]; c.Age += dt;
                c.Heading += random.Range(-1, 1) * dt * 2;
                c.X = Wrap(c.X + (float)Math.Cos(c.Heading) * c.Genes.Speed * dt);
                c.Z = Wrap(c.Z + (float)Math.Sin(c.Heading) * c.Genes.Speed * dt);
                int tile = Math.Min(config.GridSize - 1, (int)(c.Z / config.WorldSize * config.GridSize)) * config.GridSize
                         + Math.Min(config.GridSize - 1, (int)(c.X / config.WorldSize * config.GridSize));
                Biology.Feed(ref c, ref nutrients[tile], ref organicEnergy[tile], Temperature, dt);
                c.Energy -= Biology.Cost(c.Genes, Temperature) * dt;
                if (c.Energy <= 0 || c.Age >= config.MaxAge) {
                    pendingRemains[tile] += Biology.CarcassEnergy(c); Deaths++; continue;
                }
                if (Biology.CanDivide(c) && originalCount + births < cells.Length) {
                    Cell child = new Cell { ParentId = c.Id, Generation = c.Generation + 1,
                        Genes = c.Genes.Inherit(ref random, config.MutationProbability, config.MutationStrength),
                        X = Wrap(c.X + .3f), Z = c.Z, Heading = random.Range(0, 6.283185f) };
                    if (Biology.TryDivide(ref c, ref child)) {
                        child.Id = ++nextId;
                        cells[originalCount + births++] = child; Births++;
                    }
                }
                cells[survivors++] = c;
            }
            Array.Copy(cells, originalCount, cells, survivors, births);
            // Every cell sees only remains from previous ticks, independent of death order.
            for (int i = 0; i < organicEnergy.Length; i++) {
                organicEnergy[i] += pendingRemains[i]; pendingRemains[i] = 0;
            }
            Count = survivors + births; Tick++;
        }
        float Wrap(float x) => x - (float)Math.Floor(x / config.WorldSize) * config.WorldSize;
    }
}
