using System;
namespace Evolution.Core
{
    [Serializable]
    public sealed class SimulationConfig
    {
        public bool OceanZones;
        public int Capacity = 4096, InitialPopulation = 160, GridSize = 32, MaxColonyCells = 16;
        public float WorldSize = 40, StepSeconds = .05f, InitialNutrients = 4,
            NutrientRegeneration = .12f, NutrientCapacity = 8, Temperature = 22,
            MutationProbability = .15f, MutationStrength = .06f, MaxAge = 180,
            InitialOrganicEnergy = 0, OrganicDecayRate = .02f;
        public void Validate()
        {
            if (MaxColonyCells < 2 || MaxColonyCells > 64 || Capacity < 1 || InitialPopulation < 0 || InitialPopulation > Capacity || GridSize < 2 || GridSize > 512 ||
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
        public int BirthMutationMask; // Actual changed traits relative to the parent at birth.
        public Genome Genes;
        public float X, Z, Heading, Energy, Age;
        // Persistent tree edge uses IDs, never dense array indices. ColonyId is derived.
        public int BondParentId, ColonyId, TissueSlot;
        public float BondAngle, Specialization;
        public CellRole Role;
    }
    // Dense storage, stable iteration and deferred births. No engine references or per-cell objects.
    public sealed partial class Simulation
    {
        readonly SimulationConfig config;
        readonly Cell[] cells;
        readonly ColonySystem colonies;
        readonly float[] nutrients, organicEnergy, pendingRemains;
        RandomSource random;
        int nextId;
        public int Count { get; private set; }
        public long Tick { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }
        public int ColonyCount => colonies.ColonyCount;
        public int ColonialCells => colonies.ColonialCells;
        public int LargestColony => colonies.LargestColony;
        public float EnergyTransferred { get; private set; }
        public float Temperature { get; private set; }
        public float NutrientRegeneration { get; private set; }
        public SimulationConfig Config => config.Copy();
        public uint RandomState => random.State;
        public Cell GetCell(int i) { if (i < 0 || i >= Count) throw new ArgumentOutOfRangeException(nameof(i)); return cells[i]; }
        public OceanSample EnvironmentAt(float x, float z) => OceanEnvironment.Sample(x, z, config.WorldSize,
            Tick * config.StepSeconds, Temperature, config.OceanZones);
        public float GetNutrient(int i) => nutrients[i];
        public float GetOrganicEnergy(int i) => organicEnergy[i];
        public Simulation(SimulationConfig settings, uint seed) : this(settings, seed, null) { }
        // Explicit founders support reproducible biological experiments. IDs/links start fresh.
        public Simulation(SimulationConfig settings, uint seed, Cell[] founders)
        {
            settings.Validate(); config = settings.Copy(); random = new RandomSource(seed);
            if (founders != null && founders.Length > config.Capacity) throw new ArgumentException("Too many founders");
            cells = new Cell[config.Capacity]; colonies = new ColonySystem(config.Capacity, config.WorldSize);
            nutrients = new float[config.GridSize * config.GridSize];
            organicEnergy = new float[nutrients.Length]; pendingRemains = new float[nutrients.Length];
            Temperature = config.Temperature; NutrientRegeneration = config.NutrientRegeneration;
            for (int i = 0; i < nutrients.Length; i++) {
                nutrients[i] = Math.Min(config.InitialNutrients * SampleTile(i).Fertility, config.NutrientCapacity);
                organicEnergy[i] = config.InitialOrganicEnergy;
            }
            for (int i = 0; i < (founders == null ? config.InitialPopulation : founders.Length); i++) {
                if (founders != null) {
                    Cell f = founders[i];
                    if (!Multicellularity.ValidFounder(f, config)) throw new ArgumentException("Invalid founder");
                    f.Id = ++nextId; f.ParentId = -1; f.BondParentId = f.ColonyId = f.TissueSlot = 0;
                    f.BirthMutationMask = 0; f.Specialization = 0; f.Role = CellRole.Generalist; f.BondAngle = 0; f.Generation = 0;
                    cells[Count++] = f; continue;
                }
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
            float dt = config.StepSeconds; bool divide = divisionRequested; divisionRequested = false;
            for (int i = 0; i < nutrients.Length; i++) {
                nutrients[i] = Math.Min(config.NutrientCapacity, nutrients[i] + NutrientRegeneration * SampleTile(i).Fertility * dt);
                // Decay dissipates energy; it does not create another energy-bearing resource.
                organicEnergy[i] *= Math.Max(0, 1 - config.OrganicDecayRate * dt);
            }
            colonies.Prepare(cells, Count, config);
            colonies.Contact(cells, Count, config, ref random);
            colonies.Prepare(cells, Count, config);
            colonies.Move(cells, Count, config, ref random, Tick * dt, ControlledId, controlX, controlZ);
            // Feed and pay upkeep first, then transport can rescue a hungry living neighbour.
            for (int i = 0; i < Count; i++) {
                Cell c = cells[i]; c.Age += dt;
                colonies.Develop(ref c, dt);
                int tile = Tile(c);
                float localTemperature = EnvironmentAt(c.X, c.Z).Temperature;
                Biology.Feed(ref c, ref nutrients[tile], ref organicEnergy[tile], localTemperature, dt);
                c.Energy -= Multicellularity.Cost(c, localTemperature, colonies.Protection(i)) * dt;
                cells[i] = c;
            }
            EnergyTransferred = colonies.Exchange(cells, Count, dt, config.MaxAge);
            int originalCount = Count, survivors = 0, births = 0;
            // Births use the free tail; stable compaction below cannot overwrite it.
            for (int i = 0; i < originalCount; i++) {
                Cell c = cells[i];
                int tile = Tile(c);
                if (c.Energy <= 0 || c.Age >= config.MaxAge) {
                    pendingRemains[tile] += Biology.CarcassEnergy(c); Deaths++; continue;
                }
                if ((c.Id != ControlledId || divide) && Biology.CanDivide(c) && originalCount + births < cells.Length) {
                    Cell child = new Cell { ParentId = c.Id, Generation = c.Generation + 1,
                        Genes = c.Genes.Inherit(ref random, config.MutationProbability, config.MutationStrength),
                        X = Wrap(c.X + .3f), Z = c.Z, Heading = random.Range(0, 6.283185f) };
                    if (Biology.TryDivide(ref c, ref child)) {
                        child.BirthMutationMask = Observation.MutationMask(c.Genes, child.Genes);
                        child.Id = ++nextId;
                        if (c.Id == ControlledId) { LastOffspringId = child.Id; LastDivisionParentId = c.Id; LastDivisionTick = Tick + 1; }
                        colonies.Retain(ref c, ref child, i, config, ref random);
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
            Count = survivors + births;
            colonies.Prepare(cells, Count, config);
            colonies.Layout(cells, Count, config.WorldSize); Tick++;
            if (ControlledId != 0 && !Observation.TryFind(this, ControlledId, out _)) ReleaseControl();
        }
        OceanSample SampleTile(int i) => EnvironmentAt((i % config.GridSize + .5f) * config.WorldSize / config.GridSize,
            (i / config.GridSize + .5f) * config.WorldSize / config.GridSize);
        int Tile(Cell c) => Math.Min(config.GridSize - 1, (int)(c.Z / config.WorldSize * config.GridSize)) * config.GridSize
            + Math.Min(config.GridSize - 1, (int)(c.X / config.WorldSize * config.GridSize));
        float Wrap(float x)
        {
            float wrapped = x - (float)Math.Floor(x / config.WorldSize) * config.WorldSize;
            return wrapped >= config.WorldSize ? 0 : Math.Max(0, wrapped);
        }
    }
}
