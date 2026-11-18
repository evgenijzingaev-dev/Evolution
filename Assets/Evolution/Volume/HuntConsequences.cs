using System;
using System.Collections.Generic;

namespace Evolution.Volume
{
    // Makes the already-real ecology of hunted regions visible. When a dynasty member
    // bites inside a region, the observed living count of that region is remembered
    // as a baseline; a later fresh observation of the same region that lost at least
    // half of it raises a warning, and recovery back to the baseline clears it.
    // Reads the survey only — no balance change, no invented events.
    public sealed class HuntConsequences
    {
        struct Hunted { public int Baseline; public bool Warned; }
        public const int TrackedRegionLimit = 256;
        readonly Dictionary<ChunkKey, Hunted> hunted = new Dictionary<ChunkKey, Hunted>();
        public string Warning { get; private set; }
        // A baseline below two is noise: a single death is not depletion.
        public static bool Depleted(int baseline, int living) => baseline >= 2 && living <= Math.Max(1, baseline / 2);
        public static bool Recovered(int baseline, int living) => living >= baseline;
        public static string WarningText(ChunkKey key)
            => "Добычи меньше в регионе " + key.X + " / " + key.Y + " / " + key.Z + ": охота твоей линии";
        // Call once per simulation step.
        public bool Record(VolumeWorld world, OceanSurvey survey, LineageTree tree)
        {
            if (world == null || survey == null || tree == null || !tree.HasFounder) return false;
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                // The per-tick bite flag of the model itself; nothing is guessed.
                if (!a.Alive || a.Biology.AttackLastStep <= 0 || !tree.IsDynasty(a.Identity)) continue;
                var key = ChunkKey.At(a.Position);
                if (!survey.TryRegion(key, out var region) || !region.Loaded || region.Living <= 0) continue;
                // Bounded memory: a very long run forgets old hunting grounds instead
                // of growing without limit; observation simply starts over.
                if (!hunted.ContainsKey(key) && hunted.Count >= TrackedRegionLimit) hunted.Clear();
                hunted.TryGetValue(key, out var huntedRegion);
                huntedRegion.Baseline = Math.Max(huntedRegion.Baseline, region.Living);
                hunted[key] = huntedRegion;
            }
            string warning = null;
            foreach (var pair in hunted) {
                if (!survey.TryRegion(pair.Key, out var region) || !region.Loaded) continue;
                var huntedRegion = pair.Value;
                if (Depleted(huntedRegion.Baseline, region.Living)) huntedRegion.Warned = true;
                else if (Recovered(huntedRegion.Baseline, region.Living)) huntedRegion.Warned = false;
                hunted[pair.Key] = huntedRegion;
                if (huntedRegion.Warned && warning == null) warning = WarningText(pair.Key);
            }
            Warning = warning;
            return warning != null;
        }
    }
}
