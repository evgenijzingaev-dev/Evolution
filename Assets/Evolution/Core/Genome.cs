using System;
namespace Evolution.Core
{
    // Own PRNG: state and call order belong to the simulation, never UnityEngine.Random.
    public struct RandomSource
    {
        public uint State;
        public RandomSource(uint seed) { State = seed == 0 ? 1u : seed; }
        public float Next() { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x; return (x >> 8) / 16777216f; }
        public float Range(float min, float max) => min + (max - min) * Next();
    }
    [Serializable]
    public struct Genome
    {
        public float Speed, Size, Uptake, PreferredTemperature, OrganicSpecialization;
        // All colony traits are continuous, inherited and bounded; no evolution stage flag.
        public float Adhesion, ColonySize, Branching, Differentiation, Transport, RolePattern;
        public float Photosynthesis, Predation, Membrane, Sensitivity, ThermalTolerance, ToxinResistance, EnergyEfficiency;
        public static Genome EcologicalFounder(Genome g, uint seed)
        {
            var rng = new RandomSource(seed ^ 0x85ebca6bu);
            g.Photosynthesis = rng.Next(); g.Predation = rng.Next() * rng.Next();
            g.Membrane = rng.Next(); g.Sensitivity = rng.Next();
            g.ThermalTolerance = rng.Next(); g.ToxinResistance = rng.Next(); g.EnergyEfficiency = rng.Next();
            g.OrganicSpecialization = rng.Next();
            return g;
        }
        public static Genome Founder(ref RandomSource rng)
        {
            var g = new Genome {
                Speed = rng.Range(.6f, 1.6f), Size = rng.Range(.7f, 1.3f),
                Uptake = rng.Range(.7f, 1.3f), PreferredTemperature = rng.Range(15, 30),
                OrganicSpecialization = rng.Range(0, .15f) };
            // Derive new traits without shifting the original founder/position random sequence.
            var traits = new RandomSource(rng.State ^ 0x9e3779b9u);
            g.Adhesion = traits.Next(); g.ColonySize = traits.Next(); g.Branching = traits.Next();
            g.Differentiation = traits.Next(); g.Transport = traits.Next(); g.RolePattern = traits.Next();
            return g;
        }
        public Genome Inherit(ref RandomSource rng, float probability, float strength, bool ecological = true)
        {
            return new Genome {
                Speed = Mutate(Speed, .1f, 3f, ref rng, probability, strength),
                Size = Mutate(Size, .4f, 2f, ref rng, probability, strength),
                Uptake = Mutate(Uptake, .2f, 3f, ref rng, probability, strength),
                PreferredTemperature = Mutate(PreferredTemperature, 0, 50, ref rng, probability, strength),
                OrganicSpecialization = Mutate(OrganicSpecialization, 0, 1, ref rng, probability, strength),
                Adhesion = Mutate(Adhesion, 0, 1, ref rng, probability, strength),
                ColonySize = Mutate(ColonySize, 0, 1, ref rng, probability, strength),
                Branching = Mutate(Branching, 0, 1, ref rng, probability, strength),
                Differentiation = Mutate(Differentiation, 0, 1, ref rng, probability, strength),
                Transport = Mutate(Transport, 0, 1, ref rng, probability, strength),
                RolePattern = Mutate(RolePattern, 0, 1, ref rng, probability, strength),
                Photosynthesis = ecological ? Mutate(Photosynthesis, 0, 1, ref rng, probability, strength) : Photosynthesis,
                Predation = ecological ? Mutate(Predation, 0, 1, ref rng, probability, strength) : Predation,
                Membrane = ecological ? Mutate(Membrane, 0, 1, ref rng, probability, strength) : Membrane,
                Sensitivity = ecological ? Mutate(Sensitivity, 0, 1, ref rng, probability, strength) : Sensitivity,
                ThermalTolerance = ecological ? Mutate(ThermalTolerance, 0, 1, ref rng, probability, strength) : ThermalTolerance,
                ToxinResistance = ecological ? Mutate(ToxinResistance, 0, 1, ref rng, probability, strength) : ToxinResistance,
                EnergyEfficiency = ecological ? Mutate(EnergyEfficiency, 0, 1, ref rng, probability, strength) : EnergyEfficiency };
        }
        static float Mutate(float v, float min, float max, ref RandomSource rng, float p, float s)
        { return rng.Next() < p ? Math.Max(min, Math.Min(max, v + rng.Range(-s, s) * (max - min))) : v; }
    }
}
