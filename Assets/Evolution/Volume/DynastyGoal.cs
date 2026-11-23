using System;
using System.Collections.Generic;

namespace Evolution.Volume
{
    // The long-horizon goal "Династия": keep the played dynasty alive across five
    // generations, master three real niches, and survive a neighbourhood extinction —
    // only after the eight-milestone "Путь жизни" chain is complete. Every condition
    // is composed from verified observations; completion is remembered in the
    // chronicle, nothing is paid, nothing is spawned.
    public sealed class DynastyGoal
    {
        public const int GenerationsRequired = 5, NichesRequired = 3, RegionPeak = 2, RegionWatchLimit = 1024;
        readonly HashSet<int> generations = new HashSet<int>();
        readonly HashSet<int> niches = new HashSet<int>();
        readonly Dictionary<ChunkKey, RegionWatch> regions = new Dictionary<ChunkKey, RegionWatch>();
        struct RegionWatch { public int Peak; public bool Collapsed; }
        VolumeWorld latest;
        public int GenerationsCovered { get; private set; }
        public int NichesSeen => niches.Count;
        public bool CollapseSeen { get; private set; }
        public bool Completed { get; private set; }
        readonly Queue<string> completed = new Queue<string>();
        // How many of the five generations after the founder were ever observed alive.
        public static int CoveredGenerations(HashSet<int> observed, int founderGeneration)
        {
            if (observed == null || founderGeneration == int.MinValue) return 0;
            int covered = 0;
            for (int generation = founderGeneration; generation < founderGeneration + GenerationsRequired; generation++)
                if (observed.Contains(generation)) covered++;
            return covered;
        }
        public static bool IsComplete(bool chainComplete, int generations, int niches, bool collapseSeen)
            => chainComplete && generations >= GenerationsRequired && niches >= NichesRequired && collapseSeen;
        public string Progress
            => GenerationsCovered + "/" + GenerationsRequired + " поколений · " + niches.Count + "/" + NichesRequired
                + " ниш · вымирание: " + (CollapseSeen ? "пережито" : "нет");
        // One toast on completion, drained by the presentation layer.
        public bool ConsumeCompleted(out string text)
        {
            text = null;
            if (completed.Count == 0) return false;
            text = completed.Dequeue();
            return true;
        }
        // Call once per simulation step.
        public bool Record(VolumeWorld world, int controlled, bool chainComplete, OceanSurvey survey, LineageTree tree)
        {
            if (world == null || Completed || tree == null || !tree.HasFounder) return false;
            if (!ReferenceEquals(world, latest)) latest = world;
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                if (!a.Alive || !tree.IsDynasty(a.Identity) || tree.IsDead(a.Identity)) continue;
                generations.Add(a.Biology.Generation);
                int kind = LineageChallenge.NicheKind(world.EnvironmentAt(a.Position));
                if (kind >= 1 && kind <= 3) niches.Add(kind);
                // Regions visited by living members become watched; the check below
                // reads them from the survey even after our members leave or die there.
                if (survey == null) continue;
                var key = ChunkKey.At(a.Position);
                if (!regions.ContainsKey(key) && regions.Count < RegionWatchLimit) regions[key] = default;
            }
            // A neighbourhood dies for good only on a fresh observation: an unloaded
            // region keeps its last numbers and proves nothing.
            // Mono (Unity) invalidates a Dictionary enumerator even when an existing
            // key is overwritten in place, so walk a snapshot of the keys and write
            // each entry after reading it.
            var watched = new ChunkKey[regions.Count];
            regions.Keys.CopyTo(watched, 0);
            foreach (var key in watched) {
                if (survey == null || !survey.TryRegion(key, out var region) || !region.Loaded) continue;
                var watch = regions[key];
                if (region.Living > watch.Peak) watch.Peak = region.Living;
                if (watch.Peak >= RegionPeak && region.Living == 0) {
                    watch.Collapsed = true;
                    CollapseSeen = true;
                }
                regions[key] = watch;
            }
            GenerationsCovered = CoveredGenerations(generations, tree.FounderGeneration);
            if (IsComplete(chainComplete, GenerationsCovered, niches.Count, CollapseSeen)) {
                Completed = true;
                completed.Enqueue("Пять поколений подряд · три ниши · пережитое вымирание соседей");
                return true;
            }
            return false;
        }
    }
}
