using System;
using System.Collections.Generic;

namespace Evolution.Core
{
    public enum EvolutionGoal { Survive, Feed, Reserve, Reproduce, Inherit, Adapt, Colony }

    // Optional goals observe committed ticks. They never award energy, genes, or forced bonds.
    public sealed class EvolutionJourney
    {
        readonly bool[] completed = new bool[7], achievements = new bool[7];
        HashSet<int> lineage = new HashSet<int>(), nextLineage = new HashSet<int>();
        readonly Dictionary<int, float> lastFood = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextFood = new Dictionary<int, float>();
        public int RootId { get; private set; }
        public int FounderGeneration { get; private set; }
        public float SurvivalSeconds { get; private set; }
        public float FoodEnergy { get; private set; }
        public float PeakReserve { get; private set; }
        public float AdaptationSeconds { get; private set; }
        public float ColonySeconds { get; private set; }
        public float StartTemperature { get; private set; }
        public int FollowedGenerations { get; private set; }
        public int Reproductions { get; private set; }
        public int LivingDescendants => lineage.Count;
        public long RecordedTick { get; private set; } = -1;
        public bool Active => RootId != 0;
        public bool Contains(int id) => lineage.Contains(id);
        public bool Completed(EvolutionGoal goal) => completed[(int)goal];
        public int CompletedCount { get { int n = 0; foreach (bool value in completed) if (value) n++; return n; } }
        public int AchievementCount { get { int n = 0; foreach (bool value in achievements) if (value) n++; return n; } }
        public bool Begin(Simulation simulation, int id)
        {
            if (!Observation.TryFind(simulation, id, out var cell)) return false;
            if (lineage.Contains(id)) {
                FollowedGenerations = Math.Max(FollowedGenerations, cell.Generation - FounderGeneration);
                if (FollowedGenerations > 0) Finish(EvolutionGoal.Inherit);
                return true;
            }
            RootId = id; FounderGeneration = cell.Generation;
            SurvivalSeconds = FoodEnergy = PeakReserve = AdaptationSeconds = ColonySeconds = 0;
            FollowedGenerations = Reproductions = 0;
            Array.Clear(completed, 0, completed.Length);
            lineage.Clear(); lineage.Add(id); nextLineage.Clear(); lastFood.Clear(); nextFood.Clear();
            lastFood[id] = cell.AssimilatedEnergy;
            StartTemperature = simulation.EnvironmentAt(cell.X, cell.Z).Temperature;
            RecordedTick = simulation.Tick; return true;
        }
        public void RecordStep(Simulation simulation, int controlledBeforeStep)
        {
            if (!Active || simulation.Tick <= RecordedTick) return;
            // One call per actual tick; pause and rendering cannot advance progress.
            RecordedTick = simulation.Tick; nextLineage.Clear(); nextFood.Clear();
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i);
                if (!lineage.Contains(c.Id) && !lineage.Contains(c.ParentId) && !nextLineage.Contains(c.ParentId)) continue;
                nextLineage.Add(c.Id); nextFood[c.Id] = c.AssimilatedEnergy;
            }
            bool activeCell = controlledBeforeStep != 0 && nextLineage.Contains(controlledBeforeStep)
                && Observation.TryFind(simulation, controlledBeforeStep, out _);
            if (activeCell) {
                Observation.TryFind(simulation, controlledBeforeStep, out var c);
                float dt = simulation.Config.StepSeconds;
                SurvivalSeconds += dt;
                float previousFood = lastFood.TryGetValue(c.Id, out float value) ? value : c.AssimilatedEnergy;
                FoodEnergy += Math.Max(0, c.AssimilatedEnergy - previousFood);
                PeakReserve = Math.Max(PeakReserve, c.Energy / Biology.Capacity(c.Genes));
                if (Math.Abs(simulation.EnvironmentAt(c.X,c.Z).Temperature - StartTemperature) >= 4
                    && c.Energy >= Biology.Capacity(c.Genes) * .35f) AdaptationSeconds += dt;
                else AdaptationSeconds = 0;
                int members = 0, roles = 0; float reserve = 0;
                for (int i = 0; i < simulation.Count; i++) {
                    var m = simulation.GetCell(i); if (!Observation.SameOrganism(c,m)) continue;
                    members++; reserve += m.Energy / Biology.Capacity(m.Genes);
                    if (m.Specialization >= .25f && m.Role != CellRole.Generalist) roles |= 1 << (int)m.Role;
                }
                bool multipleRoles = (roles & (roles - 1)) != 0;
                ColonySeconds = members >= 4 && multipleRoles && reserve / members >= .4f ? ColonySeconds + dt : 0;
            } else { AdaptationSeconds = ColonySeconds = 0; }
            if (simulation.LastDivisionTick == simulation.Tick && lineage.Contains(simulation.LastDivisionParentId)) Reproductions++;
            var swap = lineage; lineage = nextLineage; nextLineage = swap;
            lastFood.Clear(); foreach (var item in nextFood) lastFood.Add(item.Key,item.Value);
            if (SurvivalSeconds >= 30) Finish(EvolutionGoal.Survive);
            if (FoodEnergy >= 5) Finish(EvolutionGoal.Feed);
            if (PeakReserve >= .85f) Finish(EvolutionGoal.Reserve);
            if (Reproductions > 0) Finish(EvolutionGoal.Reproduce);
            if (AdaptationSeconds >= 15) Finish(EvolutionGoal.Adapt);
            if (ColonySeconds >= 20) Finish(EvolutionGoal.Colony);
        }
        void Finish(EvolutionGoal goal) { completed[(int)goal] = achievements[(int)goal] = true; }
        public float Progress(EvolutionGoal goal)
        {
            if (Completed(goal)) return 1;
            switch (goal) {
                case EvolutionGoal.Survive: return Math.Min(1, SurvivalSeconds / 30);
                case EvolutionGoal.Feed: return Math.Min(1, FoodEnergy / 5);
                case EvolutionGoal.Reserve: return Math.Min(1, PeakReserve / .85f);
                case EvolutionGoal.Reproduce: return Reproductions > 0 ? 1 : 0;
                case EvolutionGoal.Inherit: return FollowedGenerations > 0 ? 1 : 0;
                case EvolutionGoal.Adapt: return Math.Min(1, AdaptationSeconds / 15);
                default: return Math.Min(1, ColonySeconds / 20);
            }
        }
    }
}
