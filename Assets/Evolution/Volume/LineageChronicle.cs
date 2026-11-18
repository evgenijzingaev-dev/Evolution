using System;
using System.Collections.Generic;

namespace Evolution.Volume
{
    public enum ChronicleEnd { Active, Extinct, PlayerExit }
    public struct ChronicleSummary
    {
        public string FounderName;
        public int Generations, Divisions, Hunts, Niches;
        public double Seconds;
        public ChronicleEnd End;
        public string EndText;
        public bool Era;
        public int Score;
    }
    // The epitaph of one played dynasty, composed only from verified observations.
    // The line is declared extinct only when every tracked member is confirmed dead
    // and none is merely outside the loaded area; the cause of the last death comes
    // from the model's own journal. A player exit is not a death: living descendants
    // are reported as still alive. Reads the world, never writes biology.
    public sealed class LineageChronicle
    {
        readonly HashSet<string> dynasty = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> dead = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<int> niches = new HashSet<int>();
        VolumeWorld latest;
        long journalSequence;
        int hunts, divisions, deepestGeneration, lastAlive, lastMissing;
        int founderGeneration = int.MinValue;
        string founderIdentity;
        double startedAt = double.NaN;
        string lastDeathCause;
        bool era, finished, pending;
        public int Hunts => hunts;
        public int Divisions => divisions;
        public int DeepestGeneration => deepestGeneration;
        public bool EraReached => era;
        public bool Finished => finished;
        public void MarkEra() => era = true;
        // Generations carry ten points, each mastered niche adds one.
        public static int Score(int generations, int niches) => Math.Max(0, generations) * 10 + Math.Max(0, niches);
        public static string CauseText(DeathAppearance cause)
            => cause == DeathAppearance.Damage ? "Последняя смерть: клетку съел хищник." : "Последняя смерть: запас энергии исчерпан.";
        public const string ExtinctText = "Линия прекратилась: все подтверждённые представители погибли.";
        public const string UnobservedText = "Часть линии вне наблюдения; исход её представителей неизвестен.";
        public const string MissingCauseText = "Последняя смерть осталась за пределами журнала наблюдения.";
        static double SafeTime(double time) => double.IsNaN(time) || double.IsInfinity(time) ? 0 : time;
        public bool Record(VolumeWorld world, int controlled)
        {
            if (world == null || finished) return false;
            // A rebuilt streamed world has its own journal; never replay the old one.
            if (!ReferenceEquals(world, latest)) { latest = world; journalSequence = 0; }
            if (controlled >= 0 && controlled < world.Count) {
                var a = world.Get(controlled);
                if (founderGeneration == int.MinValue && a.Alive && !string.IsNullOrEmpty(a.Identity)) {
                    dynasty.Add(a.Identity);
                    {
                        founderIdentity = a.Identity;
                        founderGeneration = a.Biology.Generation;
                        deepestGeneration = a.Biology.Generation;
                        startedAt = world.Time;
                    }
                }
            }
            if (dynasty.Count == 0) return false;
            // Journal events are authoritative: dynasty births (with the child joining
            // the dynasty) and death causes of dynasty members.
            for (long s = journalSequence + 1; world.Life.TryRead(s, out LifeEvent e); s++) {
                journalSequence = s;
                if (e.Kind == LifeEventKind.Division) {
                    var parent = e.Agent.Identity; var child = e.Child.Identity;
                    if (parent != null && dynasty.Contains(parent) && child != null) {
                        dynasty.Add(child);
                        divisions++;
                        if (e.Child.Biology.Generation > deepestGeneration) deepestGeneration = e.Child.Biology.Generation;
                    }
                } else if (e.Kind == LifeEventKind.Death && e.Agent.Identity != null && dynasty.Contains(e.Agent.Identity)) {
                    dead.Add(e.Agent.Identity);
                    lastDeathCause = CauseText(e.Cause);
                    if (e.Agent.Biology.Generation > deepestGeneration) deepestGeneration = e.Agent.Biology.Generation;
                }
            }
            // One linear pass: living members, their niches and deepest generation.
            var observed = new HashSet<string>(StringComparer.Ordinal);
            lastAlive = 0;
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                if (a.Identity == null || !dynasty.Contains(a.Identity)) continue;
                if (a.Alive && !dead.Contains(a.Identity)) {
                    // A confirmed death is terminal even if streaming returns a stale live snapshot.
                    observed.Add(a.Identity);
                    lastAlive++;
                    if (a.Biology.Generation > deepestGeneration) deepestGeneration = a.Biology.Generation;
                    int kind = LineageChallenge.NicheKind(world.EnvironmentAt(a.Position));
                    if (kind >= 1 && kind <= 3) niches.Add(kind);
                } else dead.Add(a.Identity);
            }
            // A confirmed bite of the played member is the model's own per-tick fact.
            if (controlled >= 0 && controlled < world.Count) {
                var a = world.Get(controlled);
                if (a.Alive && !dead.Contains(a.Identity) && dynasty.Contains(a.Identity) && a.Biology.AttackLastStep > 0) hunts++;
            }
            lastMissing = 0;
            foreach (var identity in dynasty) if (!dead.Contains(identity) && !observed.Contains(identity)) lastMissing++;
            // Extinction needs every tracked member confirmed dead, none just unloaded.
            if (lastAlive == 0 && lastMissing == 0) return Finish(false);
            return false;
        }
        // The player willingly ends their role; the line itself may well be alive.
        public bool FinishByPlayer()
        {
            if (finished || dynasty.Count == 0) return false;
            return Finish(true);
        }
        // Terminal: a finished chronicle never resumes tracking or finishes twice.
        bool Finish(bool byPlayer)
        {
            if (finished) return false;
            finished = true;
            int generations = founderGeneration == int.MinValue ? 0 : deepestGeneration - founderGeneration + 1;
            string extinct = ExtinctText + " " + (lastDeathCause ?? MissingCauseText);
            string endText;
            if (!byPlayer) endText = extinct;
            else if (lastAlive > 0) endText = "Вы вышли из роли. Линия жива: представителей в загруженной области — " + lastAlive + ".";
            else if (lastMissing > 0) endText = "Вы вышли из роли. " + UnobservedText;
            else endText = extinct;
            double seconds = latest == null ? 0 : Math.Max(0, SafeTime(latest.Time) - SafeTime(startedAt));
            Summary = new ChronicleSummary {
                FounderName = LineageName.For(founderIdentity), Generations = generations,
                Divisions = divisions, Hunts = hunts, Niches = niches.Count,
                Seconds = seconds, End = byPlayer ? ChronicleEnd.PlayerExit : ChronicleEnd.Extinct,
                EndText = endText, Era = era, Score = Score(generations, niches.Count)
            };
            pending = true;
            return true;
        }
        public ChronicleSummary Summary { get; private set; }
        // One consumption of the finished summary for the presentation layer.
        public bool ConsumeFinished(out ChronicleSummary summary)
        {
            summary = default;
            if (!pending) return false;
            pending = false;
            summary = Summary;
            return true;
        }
    }
}
