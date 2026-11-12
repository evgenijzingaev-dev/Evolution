using System;
namespace Evolution.Core
{
    [Serializable]
    public sealed class SimulationConfig
    {
        public bool OceanZones, Ecosystem, NaturalEvents;
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
        public int Id, ParentId, Generation, LineageId, TargetId;
        public float Integrity, AttackLastStep, DamageLastStep, TransportLastStep;
        public bool Consumed;
        public CellActivity Activity;
        public int BirthMutationMask; // Actual changed traits relative to the parent at birth.
        public Genome Genes;
        public float X, Z, Heading, Energy, Age;
        public float AssimilatedEnergy, FoodLastStep;
        public float TargetHeading, AngularVelocity, VelocityX, VelocityZ, SwimSpeed;
        public float Growth, Digestion, RepairLastStep;
        public bool Boosting, Defending, Attacking;
        public EnergyBudget Budget;
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
        public OceanSample EnvironmentAt(float x, float z) => ApplyEcology(OceanEnvironment.Sample(x, z, config.WorldSize,
            Tick * config.StepSeconds, Temperature, config.OceanZones), x, z);
        public float GetNutrient(int i) => nutrients[i];
        public float GetOrganicEnergy(int i) => organicEnergy[i];
        public Simulation(SimulationConfig settings, uint seed) : this(settings, seed, null) { }
        // Explicit founders support reproducible biological experiments. IDs/links start fresh.
        public Simulation(SimulationConfig settings, uint seed, Cell[] founders)
        {
            settings.Validate(); config = settings.Copy(); random = new RandomSource(seed);
            if (founders != null && founders.Length > config.Capacity) throw new ArgumentException("Too many founders");
            ecologyIndex = new SpatialIndex(config.Capacity, config.WorldSize);
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
                    f.Id = ++nextId; f.LineageId = f.Id; f.Integrity = 1; f.Consumed = false; f.TargetId = 0; f.AttackLastStep = f.DamageLastStep = f.TransportLastStep = 0; f.Activity = CellActivity.Wandering; f.ParentId = -1; f.BondParentId = f.ColonyId = f.TissueSlot = 0;
                    f.TargetHeading = f.Heading; f.AngularVelocity = f.VelocityX = f.VelocityZ = f.SwimSpeed = 0; f.Growth = 1; f.Digestion = 0; f.Budget = default;
                    f.BirthMutationMask = 0; f.AssimilatedEnergy = f.FoodLastStep = 0; f.Specialization = 0; f.Role = CellRole.Generalist; f.BondAngle = 0; f.Generation = 0;
                    cells[Count++] = f; continue;
                }
                var g = Genome.Founder(ref random);
                if (config.Ecosystem) g = Genome.EcologicalFounder(g, random.State);
                cells[Count++] = new Cell { Id = ++nextId, ParentId = -1, LineageId = nextId, Integrity = 1, Growth = 1, Genes = g, Energy = Biology.Capacity(g) * .6f,
                    X = random.Range(0, config.WorldSize), Z = random.Range(0, config.WorldSize), Heading = random.Range(0, 6.283185f) };
            }
            for (int i = 0; i < Count; i++) cells[i].TargetHeading = cells[i].Heading;
            if (config.Ecosystem) InitializeEcology(seed);
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
            PrepareActions();
            if (config.Ecosystem) UpdateNaturalEvents();
            for (int i = 0; i < nutrients.Length; i++) {
                nutrients[i] = Math.Min(config.NutrientCapacity, nutrients[i] + NutrientRegeneration * SampleTile(i).Fertility * dt);
                // Decomposition dissipates energy; ecology recovers only part of the loss.
                float decayed = organicEnergy[i] * Math.Min(1, config.OrganicDecayRate * dt);
                organicEnergy[i] -= decayed;
                // Mineralization retains a quarter of the decayed chemical energy, dissipating the rest.
                if (config.Ecosystem) nutrients[i] = Math.Min(config.NutrientCapacity, nutrients[i] + decayed * .125f);
            }
            if (config.Ecosystem && Tick % 4 == 0) DiffuseResources();
            colonies.Prepare(cells, Count, config);
            colonies.Contact(cells, Count, config, ref random);
            colonies.Prepare(cells, Count, config);
            if (config.Ecosystem) SteerEcology();
            colonies.Move(cells, Count, config, ref random, Tick * dt, ControlledId, controlX, controlZ, config.Ecosystem, CurrentScale);
            // Feed and pay upkeep first, then transport can rescue a hungry living neighbour.
            for (int n = 0; n < Count; n++) {
                int i = config.Ecosystem ? (int)((n + Tick) % Count) : n;
                Cell c = cells[i]; c.Age += dt; c.AttackLastStep = c.DamageLastStep = c.TransportLastStep = 0;
                colonies.Develop(ref c, dt);
                int tile = Tile(c);
                var local = EnvironmentAt(c.X, c.Z);
                float localTemperature = local.Temperature;
                float beforeFood = c.Energy;
                if (config.Ecosystem) {
                    c.Budget.Light = Ecology.Photosynthesize(ref c, ref nutrients[tile], local, dt);
                    SunlightEnergy += c.Budget.Light;
                }
                Biology.Feed(ref c, ref nutrients[tile], ref organicEnergy[tile], localTemperature, dt);
                c.FoodLastStep = Math.Max(0, c.Energy - beforeFood); c.AssimilatedEnergy += c.FoodLastStep;
                c.Budget.Upkeep = Multicellularity.Cost(c, localTemperature, colonies.Protection(i)) * dt;
                c.Budget.Movement = CellMotion.Cost(c) * dt;
                c.Budget.Defense = c.Defending ? (.3f + .5f * c.Genes.Membrane) * dt : 0;
                c.Energy -= c.Budget.Upkeep + c.Budget.Movement + c.Budget.Defense;
                if (config.Ecosystem) {
                    c.Budget.Stress = Ecology.Stress(c, local) * dt; c.Energy -= c.Budget.Stress;

                    if (c.Integrity < 1 && c.Energy > Biology.Capacity(c.Genes) * .25f) {
                        float repair = Math.Min(1 - c.Integrity, .025f * dt);
                        c.RepairLastStep = repair; c.Integrity += repair; c.Budget.Repair = repair * c.Genes.Size * 2; c.Energy -= c.Budget.Repair;
                    }
                }
                if (c.Growth < 1 && c.Energy > Biology.Capacity(c.Genes) * .35f) {
                    float growth = Math.Min(1 - c.Growth, .15f * dt);
                    c.Growth += growth; c.Budget.Growth = growth * c.Genes.Size * .8f; c.Energy -= c.Budget.Growth;
                }
                c.Digestion = Math.Min(1, Math.Max(0, c.Digestion - dt * .6f) + c.FoodLastStep * .7f);
                cells[i] = c;
            }
            if (config.Ecosystem) Hunt();
            EnergyTransferred = colonies.Exchange(cells, Count, dt, config.MaxAge);
            int originalCount = Count, survivors = 0, births = 0;
            // Births use the free tail; stable compaction below cannot overwrite it.
            for (int i = 0; i < originalCount; i++) {
                Cell c = cells[i];
                int tile = Tile(c);
                if (c.Energy <= 0 || c.Age >= config.MaxAge) {
                    if (!c.Consumed) pendingRemains[tile] += Biology.CarcassEnergy(c); Deaths++; continue;
                }
                if ((c.Id != ControlledId || divide) && Biology.CanDivide(c) && originalCount + births < cells.Length) {
                    Cell child = new Cell { ParentId = c.Id, Generation = c.Generation + 1, LineageId = c.LineageId, Integrity = 1,
                        Genes = c.Genes.Inherit(ref random, config.MutationProbability, config.MutationStrength, config.Ecosystem),
                        X = Wrap(c.X + .3f), Z = c.Z, Heading = random.Range(0, 6.283185f) };
                    if (Biology.TryDivide(ref c, ref child)) {
                        child.TargetHeading = child.Heading; child.Budget.Start = child.Energy;
                        c.Budget.Division = Biology.StructureEnergy(child.Genes) + Biology.BirthOverhead(child.Genes) + child.Energy;
                        child.BirthMutationMask = Observation.MutationMask(c.Genes, child.Genes);
                        child.Id = ++nextId;
                        if (config.Ecosystem) RecordAdaptation(c, child);
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
            if (config.Ecosystem && Tick >= nextCensusTick) Census();
            if (ControlledId != 0 && !Observation.TryFind(this, ControlledId, out _)) ReleaseControl();
        }
        OceanSample SampleTile(int i) => EnvironmentAt((i % config.GridSize + .5f) * config.WorldSize / config.GridSize,
            (i / config.GridSize + .5f) * config.WorldSize / config.GridSize);
        int Tile(Cell c) => TileAt(c.X, c.Z);
        int TileAt(float x, float z) => Math.Min(config.GridSize - 1, (int)(z / config.WorldSize * config.GridSize)) * config.GridSize
            + Math.Min(config.GridSize - 1, (int)(x / config.WorldSize * config.GridSize));
        float Wrap(float x)
        {
            float wrapped = x - (float)Math.Floor(x / config.WorldSize) * config.WorldSize;
            return wrapped >= config.WorldSize ? 0 : Math.Max(0, wrapped);
        }
    }
}
