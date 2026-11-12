using System;
namespace Evolution.Core
{
    public static class Biology
    {
        // Abstract energy units: substrate carries 2 per unit; organics are already energy.
        public const float SubstrateEnergy = 2f;
        public static float Capacity(Genome g) => 24 * g.Size;
        public static float StructureEnergy(Genome g) => (2 + .8f * g.Membrane + .4f * g.Photosynthesis + .5f * g.Predation) * g.Size;
        public static float BirthOverhead(Genome g) => .5f * g.Size;
        public static float Cost(Genome g, float temperature) =>
            (.18f * g.Size + .12f * g.Speed * g.Speed + .08f * g.Uptake * g.Uptake) * (1 - .3f * g.EnergyEfficiency)
            + .04f * g.OrganicSpecialization * g.OrganicSpecialization
            + .025f * Math.Abs(temperature - g.PreferredTemperature) * (1 - .65f * g.ThermalTolerance)
            + .09f * g.Membrane * g.Membrane + .12f * g.Photosynthesis * g.Photosynthesis
            + .14f * g.Predation * g.Predation + .04f * g.Sensitivity * g.Sensitivity
            + .06f * g.ThermalTolerance + .06f * g.ToxinResistance + .08f * g.EnergyEfficiency;
        public static float Efficiency(Genome g, float temperature) => 1 / (1 + .04f * Math.Abs(temperature - g.PreferredTemperature) * (1 - .65f * g.ThermalTolerance));
        public static float SubstrateYield(Genome g, float temperature) =>
            SubstrateEnergy * (1 - .65f * g.OrganicSpecialization) * (1 - .5f * g.Predation) * Efficiency(g, temperature);
        public static float OrganicAssimilation(Genome g, float temperature) =>
            Math.Min(.95f, .2f + .65f * g.OrganicSpecialization + .1f * g.EnergyEfficiency) * Efficiency(g, temperature);
        public static float CarcassEnergy(Cell c) => .75f * (Math.Max(0, c.Energy) + StructureEnergy(c.Genes));

        // Both foods share one uptake budget. A full cell leaves food for its neighbours.
        public static void Feed(ref Cell c, ref float substrate, ref float organicEnergy, float temperature, float dt)
        {
            float budget = c.Genes.Uptake * Multicellularity.Feeding(c) * dt;
            float room = Math.Max(0, Capacity(c.Genes) - c.Energy);
            float assimilation = OrganicAssimilation(c.Genes, temperature);
            float organic = Math.Min(organicEnergy, Math.Min(room / assimilation,
                budget * SubstrateEnergy * (.2f + .8f * c.Genes.OrganicSpecialization)));
            organicEnergy -= organic;
            c.Budget.Organic += organic * assimilation; c.Energy += organic * assimilation;
            budget = Math.Max(0, budget - organic / SubstrateEnergy);
            room = Math.Max(0, Capacity(c.Genes) - c.Energy);
            float yield = SubstrateYield(c.Genes, temperature);
            float eaten = Math.Min(substrate, Math.Min(budget, room / yield));
            substrate -= eaten;
            c.Budget.Substrate += eaten * yield; c.Energy = Math.Min(Capacity(c.Genes), c.Energy + eaten * yield);
        }

        public static bool CanDivide(Cell c) => c.Age >= Multicellularity.MaturityAge(c) && (c.Generation == 0 || c.Growth >= 1) && c.Energy >= Capacity(c.Genes) * Multicellularity.DivisionThreshold(c);

        // Parent retains its structure and funds every part of the new cell.
        // Failed provisioning changes neither value; callers commit lineage only on success.
        public static bool TryDivide(ref Cell parent, ref Cell child)
        {
            if (!CanDivide(parent)) return false;
            float construction = StructureEnergy(child.Genes) + BirthOverhead(child.Genes);
            float reserve = Math.Min((parent.Energy - construction) * .5f, Capacity(child.Genes) * .5f);
            if (reserve <= 0) return false;
            parent.Energy -= construction + reserve;
            child.Energy = reserve;
            return true;
        }
    }
}
