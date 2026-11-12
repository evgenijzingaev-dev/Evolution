using System;
namespace Evolution.Core
{
    public sealed partial class Simulation
    {
        readonly SpatialIndex ecologyIndex;
        void SteerEcology()
        {
            ecologyIndex.Build(cells, Count, Tick);
            bool hasControl = Observation.TryFind(this, ControlledId, out var controlled);
            for (int i = 0; i < Count; i++) {
                if (cells[i].BondParentId != 0) continue;
                if (hasControl && Observation.SameOrganism(controlled, cells[i])) {
                    cells[i].Activity = CellActivity.Wandering; cells[i].TargetId = 0; continue;
                }
                if ((Tick + cells[i].Id) % 4 != 0) continue;
                Cell c = cells[i]; c.TargetId = 0; c.Activity = CellActivity.Foraging;
                float range = Ecology.SenseRadius(c.Genes), nearestThreat = range * range, preyScore = float.NegativeInfinity;
                int threat = -1, prey = -1;
                var query = ecologyIndex.Nearby(c, range);
                while (query.Next(out int j)) {
                    Cell other = cells[j];
                    float dx = Observation.PeriodicDelta(other.X - c.X, config.WorldSize);
                    float dz = Observation.PeriodicDelta(other.Z - c.Z, config.WorldSize), d2 = dx * dx + dz * dz;
                    if (d2 > range * range || other.Age >= config.MaxAge) continue;
                    if (Ecology.CanHunt(other, c) && Ecology.Attack(other) > .35f * c.Genes.Size * Ecology.Defense(c, colonies.Protection(i))
                        && d2 < nearestThreat) { threat = j; nearestThreat = d2; }
                    float score = Ecology.Attack(c) / (other.Genes.Size * (1 + Ecology.Defense(other, colonies.Protection(j))))
                        - .18f * (float)Math.Sqrt(d2);
                    if (Ecology.CanHunt(c, other) && score > preyScore) { prey = j; preyScore = score; }
                }
                if (threat >= 0) {
                    c.Activity = CellActivity.Fleeing; c.TargetId = cells[threat].Id;
                    float dx = Observation.PeriodicDelta(c.X - cells[threat].X, config.WorldSize);
                    float dz = Observation.PeriodicDelta(c.Z - cells[threat].Z, config.WorldSize);
                    c.Heading = (float)Math.Atan2(dz, dx);
                } else if (prey >= 0 && preyScore > .05f && c.Energy < Biology.Capacity(c.Genes) * .9f) {
                    c.Activity = CellActivity.Hunting; c.TargetId = cells[prey].Id;
                    c.Heading = (float)Math.Atan2(Observation.PeriodicDelta(cells[prey].Z - c.Z, config.WorldSize),
                        Observation.PeriodicDelta(cells[prey].X - c.X, config.WorldSize));
                } else {
                    // Sample a small gradient including actual depleted resources, sunlight and physiological stress.
                    float best = HabitatValue(c, c.X, c.Z); float heading = c.Heading;
                    float distance = Math.Min(range, config.WorldSize * .25f);
                    for (int n = 0; n < 8; n++) {
                        float h = c.Heading + n * .7853982f;
                        float x = Wrap(c.X + (float)Math.Cos(h) * distance), z = Wrap(c.Z + (float)Math.Sin(h) * distance);
                        float score = HabitatValue(c, x, z) - .015f * Math.Abs(n < 4 ? n : n - 8);
                        if (score > best) { best = score; heading = h; }
                    }
                    c.Heading = heading + random.Range(-.12f, .12f);
                }
                cells[i] = c;
            }
        }
        void DiffuseResources()
        {
            float rate = Math.Min(.2f, config.StepSeconds * .08f);
            for (int i = 0; i < nutrients.Length; i++) {
                int right = i / config.GridSize * config.GridSize + (i % config.GridSize + 1) % config.GridSize;
                int below = (i + config.GridSize) % nutrients.Length;
                Mix(nutrients, i, right, rate); Mix(nutrients, i, below, rate);
                Mix(organicEnergy, i, right, rate); Mix(organicEnergy, i, below, rate);
            }
        }
        static void Mix(float[] field, int a, int b, float rate)
        {
            float flux = (field[a] - field[b]) * rate; field[a] -= flux; field[b] += flux;
        }
        float HabitatValue(Cell c, float x, float z)
        {
            var s = EnvironmentAt(x, z); int tile = TileAt(x, z);
            float food = Math.Min(3, nutrients[tile]) * Biology.SubstrateYield(c.Genes, s.Temperature)
                + Math.Min(6, organicEnergy[tile]) * Biology.OrganicAssimilation(c.Genes, s.Temperature);
            return food * .2f + Ecology.PhotoRate(c, s) - Ecology.Stress(c, s) * 2
                - .03f * Math.Abs(s.Temperature - c.Genes.PreferredTemperature) * (1 - .65f * c.Genes.ThermalTolerance);
        }
        void Hunt()
        {
            ecologyIndex.Build(cells, Count, Tick);
            for (int n = 0; n < Count; n++) {
                int i = (int)((n + Tick) % Count);
                if (cells[i].Consumed || cells[i].Age >= config.MaxAge || cells[i].Genes.Predation <= .2f) continue;
                float reach = .32f * (cells[i].Genes.Size + 2); int target = -1; float nearest = reach * reach;
                var query = ecologyIndex.Nearby(cells[i], reach);
                while (query.Next(out int j)) {
                    if (!Ecology.CanHunt(cells[i], cells[j]) || cells[j].Age >= config.MaxAge) continue;
                    float dx = Observation.PeriodicDelta(cells[i].X - cells[j].X, config.WorldSize);
                    float dz = Observation.PeriodicDelta(cells[i].Z - cells[j].Z, config.WorldSize);
                    float d2 = dx * dx + dz * dz, contact = .32f * (cells[i].Genes.Size + cells[j].Genes.Size);
                    if (d2 < nearest && d2 <= contact * contact) { nearest = d2; target = j; }
                }
                if (target < 0 || cells[i].Energy >= Biology.Capacity(cells[i].Genes) * .97f) continue;
                float before = cells[i].Energy;
                bool killed = Ecology.Bite(ref cells[i], ref cells[target], colonies.Protection(target), config.StepSeconds, out float remains);
                cells[i].AttackLastStep = 1; cells[target].DamageLastStep = 1;
                float gain = Math.Max(0, cells[i].Energy - before + (.18f + .3f * cells[i].Genes.Predation) * config.StepSeconds);
                cells[i].FoodLastStep += gain; cells[i].AssimilatedEnergy += gain;
                pendingRemains[Tile(cells[target])] += remains;
                if (killed) {
                    PredationDeaths++;
                    if (PredationDeaths == 1 || PredationDeaths % 20 == 0)
                        Journal.Add(Tick + 1, EvolutionRecordKind.Predation, cells[i].LineageId, cells[target].LineageId);
                }
            }
        }
    }
}
