using System;

namespace Evolution.Core
{
    public enum EnergySource { None, Substrate, Organic, Light, Prey, Colony }
    public enum EnergyDrain { None, Upkeep, Swimming, Turning, Boost, Defense, Stress, Repair, Growth, Attack, Division, Contact, Predation, Colony }
    public enum SurvivalAdvice { Explore, Escape, LeaveToxins, StopBoosting, SeekFood, SeekLight, Recover, Divide, Grow }

    // A local, read-only sensory snapshot. It never chooses a course or consumes simulation PRNG.
    public struct SurvivalReadout
    {
        public int ThreatId, PreyId;
        public float Range, ThreatDistance, PreyDistance, FoodX, FoodZ, FoodDistance;
        public bool BetterHabitat;
        public EnergySource Source;
        public EnergyDrain Drain;
        public SurvivalAdvice Advice;
    }

    public sealed partial class Simulation
    {
        public SurvivalReadout ReadSurvival(Cell c)
        {
            var r = new SurvivalReadout { Range = Math.Min(Ecology.SenseRadius(c.Genes), config.WorldSize * .25f) };
            r.ThreatDistance = r.PreyDistance = r.Range;
            float protection = 0;
            for (int i = 0; i < Count; i++) if (cells[i].Id == c.Id) { protection = colonies.Protection(i); break; }
            if (config.Ecosystem) for (int i = 0; i < Count; i++) {
                var other = cells[i];
                if (other.Age >= config.MaxAge) continue;
                float dx = Observation.PeriodicDelta(other.X - c.X, config.WorldSize);
                float dz = Observation.PeriodicDelta(other.Z - c.Z, config.WorldSize);
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > r.Range) continue;
                // Same hunting rules as biology, including developed colonial protection.
                if (Ecology.CanHunt(other, c) && d < r.ThreatDistance &&
                    Ecology.Attack(other) / (c.Genes.Size * (1 + Ecology.Defense(c, protection))) > .28f)
                { r.ThreatId = other.Id; r.ThreatDistance = d; }
                if (Ecology.CanHunt(c, other) && d < r.PreyDistance &&
                    Ecology.Attack(c) / (other.Genes.Size * (1 + Ecology.Defense(other, colonies.Protection(i)))) > .28f)
                { r.PreyId = other.Id; r.PreyDistance = d; }
            }
            float here = SurvivalHabitatValue(c, Wrap(c.X), Wrap(c.Z)), best = here;
            r.FoodX = c.X; r.FoodZ = c.Z;
            for (int ring = 1; ring <= 2; ring++) for (int n = 0; n < 8; n++) {
                float angle = n * .7853982f, distance = r.Range * ring * .5f;
                float x = Wrap(c.X + (float)Math.Cos(angle) * distance), z = Wrap(c.Z + (float)Math.Sin(angle) * distance);
                float score = SurvivalHabitatValue(c, x, z);
                if (score <= best + .01f) continue;
                best = score; r.FoodX = x; r.FoodZ = z; r.FoodDistance = distance;
            }
            r.BetterHabitat = best > here + .03f;
            var b = c.Budget; float source = 0, drain = 0;
            Pick(b.Substrate, (int)EnergySource.Substrate, ref source, out int key); r.Source = (EnergySource)key;
            if (Pick(b.Organic, (int)EnergySource.Organic, ref source, out key)) r.Source = (EnergySource)key;
            if (Pick(b.Light, (int)EnergySource.Light, ref source, out key)) r.Source = (EnergySource)key;
            if (Pick(b.Prey, (int)EnergySource.Prey, ref source, out key)) r.Source = (EnergySource)key;
            if (Pick(b.Transport, (int)EnergySource.Colony, ref source, out key)) r.Source = (EnergySource)key;
            // No array allocation in this frequently refreshed projection.
            r.Drain = EnergyDrain.None;
            SelectDrain(b.Upkeep, EnergyDrain.Upkeep, ref drain, ref r);
            SelectDrain(b.Swimming, EnergyDrain.Swimming, ref drain, ref r);
            SelectDrain(b.Turning, EnergyDrain.Turning, ref drain, ref r);
            SelectDrain(b.Boost, EnergyDrain.Boost, ref drain, ref r);
            SelectDrain(b.Defense, EnergyDrain.Defense, ref drain, ref r);
            SelectDrain(b.Stress, EnergyDrain.Stress, ref drain, ref r);
            SelectDrain(b.Repair, EnergyDrain.Repair, ref drain, ref r);
            SelectDrain(b.Growth, EnergyDrain.Growth, ref drain, ref r);
            SelectDrain(b.Attack, EnergyDrain.Attack, ref drain, ref r);
            SelectDrain(b.Division, EnergyDrain.Division, ref drain, ref r);
            SelectDrain(b.Contact, EnergyDrain.Contact, ref drain, ref r);
            SelectDrain(b.PredationLoss, EnergyDrain.Predation, ref drain, ref r);
            SelectDrain(-b.Transport, EnergyDrain.Colony, ref drain, ref r);
            float reserve = c.Energy / Biology.Capacity(c.Genes);
            var water = EnvironmentAt(c.X, c.Z);
            r.Advice = r.ThreatId != 0 ? SurvivalAdvice.Escape
                : Ecology.Stress(c, water) > .15f ? SurvivalAdvice.LeaveToxins
                : c.Boosting && b.Net < 0 ? SurvivalAdvice.StopBoosting
                : reserve < .3f && b.Net <= 0 ? SurvivalAdvice.SeekFood
                : c.Genes.Photosynthesis > .55f && water.Light < .3f ? SurvivalAdvice.SeekLight
                : c.Integrity < .65f && reserve > .3f ? SurvivalAdvice.Recover
                : Biology.CanDivide(c) && Count < cells.Length ? SurvivalAdvice.Divide
                : c.Growth < 1 ? SurvivalAdvice.Grow : SurvivalAdvice.Explore;
            return r;
        }
        float SurvivalHabitatValue(Cell c, float x, float z)
        {
            // One-second feeding potential on a COPY with room in its reserve, before competition.
            // Reuse the real shared uptake budget and substrate-limited photosynthesis.
            var water = EnvironmentAt(x,z); int tile = TileAt(x,z);
            float substrate = nutrients[tile], organic = organicEnergy[tile];
            c.Energy = 0; c.Budget = default;
            if (config.Ecosystem) Ecology.Photosynthesize(ref c,ref substrate,water,1);
            Biology.Feed(ref c,ref substrate,ref organic,water.Temperature,1);
            return c.Energy - Biology.Cost(c.Genes,water.Temperature) - (config.Ecosystem ? Ecology.Stress(c,water) : 0);
        }
        static bool Pick(float amount, int candidate, ref float largest, out int key)
        { key = 0; if (amount <= largest) return false; largest = amount; key = candidate; return true; }
        static void SelectDrain(float amount, EnergyDrain candidate, ref float largest, ref SurvivalReadout r)
        { if (amount > largest) { largest = amount; r.Drain = candidate; } }
    }
}
