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
        public static Genome Founder(ref RandomSource rng) => new Genome {
            Speed = rng.Range(.6f, 1.6f), Size = rng.Range(.7f, 1.3f),
            Uptake = rng.Range(.7f, 1.3f), PreferredTemperature = rng.Range(15, 30),
            OrganicSpecialization = rng.Range(0, .15f) };
        public Genome Inherit(ref RandomSource rng, float probability, float strength)
        {
            return new Genome {
                Speed = Mutate(Speed, .1f, 3f, ref rng, probability, strength),
                Size = Mutate(Size, .4f, 2f, ref rng, probability, strength),
                Uptake = Mutate(Uptake, .2f, 3f, ref rng, probability, strength),
                PreferredTemperature = Mutate(PreferredTemperature, 0, 50, ref rng, probability, strength),
                OrganicSpecialization = Mutate(OrganicSpecialization, 0, 1, ref rng, probability, strength) };
        }
        static float Mutate(float v, float min, float max, ref RandomSource rng, float p, float s)
        { return rng.Next() < p ? Math.Max(min, Math.Min(max, v + rng.Range(-s, s) * (max - min))) : v; }
    }
}
