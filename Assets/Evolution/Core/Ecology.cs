using System;
namespace Evolution.Core
{
    // Labels describe continuous physiology; they never select an AI class or unlock abilities.
    public enum EcologicalStrategy { Generalist, Photosynthetic, Organic, Predator, Armored, Swift, Frugal, Colonial }
    public enum CellActivity { Wandering, Foraging, Hunting, Fleeing }
    public static class Ecology
    {
        public static float Mobility(Genome g) => g.Speed / (1 + .45f * g.Membrane + .3f * g.EnergyEfficiency);
        public static float SenseRadius(Genome g) => 1.5f + 4 * g.Sensitivity;
        public static EcologicalStrategy Strategy(Cell c)
        {
            var g = c.Genes;
            if (c.ColonyId != 0) return EcologicalStrategy.Colonial;
            if (g.Predation > .55f) return EcologicalStrategy.Predator;
            if (g.Membrane > .7f) return EcologicalStrategy.Armored;
            if (g.Photosynthesis > .55f) return EcologicalStrategy.Photosynthetic;
            if (g.OrganicSpecialization > .6f) return EcologicalStrategy.Organic;
            if (g.Speed > 1.35f) return EcologicalStrategy.Swift;
            if (g.EnergyEfficiency > .6f || g.Speed < .8f) return EcologicalStrategy.Frugal;
            return EcologicalStrategy.Generalist;
        }
        public static float PhotoRate(Cell c, OceanSample sample) =>
            1.7f * c.Genes.Photosynthesis * sample.Light * Biology.Efficiency(c.Genes, sample.Temperature)
            * Multicellularity.Feeding(c) * (1 - .65f * c.Genes.Predation);
        // Nutrient chemical energy is consumed too. Sunlight is an explicit external input.
        public static float Photosynthesize(ref Cell c, ref float substrate, OceanSample sample, float dt)
        {
            float gain = Math.Max(0, Math.Min(Biology.Capacity(c.Genes) - c.Energy,
                Math.Min(PhotoRate(c, sample) * dt, substrate / .12f)));
            substrate = Math.Max(0, substrate - gain * .12f); c.Energy += gain;
            return gain;
        }
        public static float Defense(Cell c, float colonyProtection) =>
            .15f + 1.4f * c.Genes.Membrane + colonyProtection * 1.4f
            + (c.Role == CellRole.Defense ? .7f * c.Specialization : 0);
        public static float Attack(Cell c) => c.Genes.Predation * c.Genes.Size * (1 + .22f * c.Genes.Speed);
        public static bool CanHunt(Cell hunter, Cell prey) => hunter.Id != prey.Id && !Observation.SameOrganism(hunter, prey)
            && hunter.LineageId != prey.LineageId && hunter.Genes.Predation > .2f
            && hunter.Energy > .5f && prey.Energy > 0 && !hunter.Consumed && !prey.Consumed
            && hunter.Genes.Size * (1 + hunter.Genes.Predation) >= prey.Genes.Size * .8f;
        // Bites have no random auto-win: size, speed, shell and developed collective defense matter.
        public static bool Bite(ref Cell hunter, ref Cell prey, float protection, float dt, out float remains)
        {
            remains = 0;
            if (!CanHunt(hunter, prey)) return false;
            hunter.Energy -= (.18f + .3f * hunter.Genes.Predation) * dt;
            float power = Attack(hunter) / (prey.Genes.Size * (1 + Defense(prey, protection)));
            float escape = 1 / (1 + Math.Max(0, Mobility(prey.Genes) - Mobility(hunter.Genes)));
            float damage = Math.Max(0, power - .28f) * escape * .55f * dt;
            prey.Integrity = Math.Max(0, prey.Integrity - damage);
            if (damage == 0) return false;
            float raw = Math.Min(Math.Max(0, prey.Energy), damage * 3);
            float assimilation = .45f + .35f * hunter.Genes.Predation;
            float eaten = Math.Min(raw, Math.Max(0, Biology.Capacity(hunter.Genes) - hunter.Energy) / assimilation);
            prey.Energy -= raw; hunter.Energy += eaten * assimilation; remains += raw - eaten;
            if (prey.Integrity > 0 && prey.Energy > 0) return false;
            raw = Biology.CarcassEnergy(prey);
            eaten = Math.Min(raw, Math.Max(0, Biology.Capacity(hunter.Genes) - hunter.Energy) / assimilation);
            hunter.Energy += eaten * assimilation; remains += raw - eaten;
            prey.Energy = 0; prey.Consumed = true;
            return true;
        }
        public static float Stress(Cell c, OceanSample s) =>
            s.Toxicity * .6f * (1 - .85f * c.Genes.ToxinResistance) * (1 - .35f * c.Genes.Membrane);
    }
}
