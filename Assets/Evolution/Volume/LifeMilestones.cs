using System;
using System.Collections.Generic;
using Evolution.Core;
namespace Evolution.Volume
{
    public enum LifeMilestoneId { FirstHunt, WellFed, Parent, DeepDive, NewNiche, Hunter, Dynasty, Patriarch }

    // Optional personal quest chain built only from real observed events: bites, paid
    // divisions, depth, niches and dynasty members. It reads the world and never writes
    // biology, randomness or resources. Call Record once per simulation tick.
    // Milestones complete in any order; the goal line always names the first unfinished.
    public sealed class LifeMilestones
    {
        public const int Total = 8;
        public const float WellFedFraction = .9f;
        public const float DeepY = -20f;
        public const int PatriarchMembers = 3;
        readonly HashSet<string> dynasty = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> deadDynasty = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<LifeMilestoneId> finished = new HashSet<LifeMilestoneId>();
        readonly Queue<string> completed = new Queue<string>();
        VolumeWorld latest;
        long journalSequence;
        long lastTick = -1;
        int hunts, divisions;
        int founderGeneration = int.MinValue;
        int founderNiche = -1;
        bool wellFed, deep, newNiche;

        public int Hunts => hunts;
        public int Divisions => divisions;
        public int CompletedCount => finished.Count;
        public bool ChainComplete => finished.Count >= Total;
        public LifeMilestoneId CurrentId
        {
            get {
                for(int i = 0; i < Total; i++) { var id = (LifeMilestoneId)i; if(!finished.Contains(id)) return id; }
                return LifeMilestoneId.Patriarch;
            }
        }
        public string CurrentTitle => ChainComplete ? null : Title(CurrentId);
        public string CurrentProgress => ChainComplete ? null : Progress(CurrentId);
        // One toast per completed milestone, drained by the presentation layer.
        public bool ConsumeCompleted(out string title)
        {
            title = null;
            if(completed.Count == 0) return false;
            title = completed.Dequeue(); return true;
        }
        // Real physiology, never a UI button: a bite is the model's own per-tick flag.
        public static bool BitesThisTick(Cell biology) => biology.AttackLastStep > 0;
        public static bool IsWellFed(Cell biology)
            => biology.Genes.Size > 0 && biology.Energy >= WellFedFraction * Biology.Capacity(biology.Genes);
        public static bool ReachesDepth(V3 position) => position.Y <= DeepY;
        // Transitional water (kind 0) is the default soup, not a discovery.
        public static bool IsNewNiche(int founderKind, int currentKind)
            => founderKind >= 0 && currentKind >= 1 && currentKind != founderKind;
        public static bool GrandchildAlive(int founderGeneration, int aliveGeneration)
            => founderGeneration != int.MinValue && aliveGeneration >= founderGeneration + 2;
        public static bool PatriarchReached(int aliveCount) => aliveCount >= PatriarchMembers;
        public static string Title(LifeMilestoneId id)
        {
            switch(id) {
                case LifeMilestoneId.FirstHunt: return "Первый укус";
                case LifeMilestoneId.WellFed: return "Сытость";
                case LifeMilestoneId.Parent: return "Родитель";
                case LifeMilestoneId.DeepDive: return "Глубина";
                case LifeMilestoneId.NewNiche: return "Переселенец";
                case LifeMilestoneId.Hunter: return "Охотник";
                case LifeMilestoneId.Dynasty: return "Династия";
                default: return "Патриарх";
            }
        }
        public static string Hint(LifeMilestoneId id)
        {
            switch(id) {
                case LifeMilestoneId.FirstHunt: return "Укуси другой организм: Space, когда добыча рядом";
                case LifeMilestoneId.WellFed: return "Накопи 90% запаса энергии рядом с пищей";
                case LifeMilestoneId.Parent: return "Выполни настоящее деление: R при достаточной энергии";
                case LifeMilestoneId.DeepDive: return "Погрузись ниже Y="+DeepY.ToString("F0")+": глубже стартовой воды";
                case LifeMilestoneId.NewNiche: return "Освой другую нишу: свет, тьма или термальный источник";
                case LifeMilestoneId.Hunter: return "Всего укусов: 5";
                case LifeMilestoneId.Dynasty: return "Доживи до внука: поколение основателя +2 в живых";
                default: return "Три представителя твоей линии одновременно в живых";
            }
        }
        string Progress(LifeMilestoneId id)
        {
            switch(id) {
                case LifeMilestoneId.FirstHunt: return "Укусов: "+Math.Min(hunts,1)+"/1";
                case LifeMilestoneId.WellFed: return Hint(id);
                case LifeMilestoneId.Parent: return "Делений: "+Math.Min(divisions,1)+"/1";
                case LifeMilestoneId.DeepDive: return ControlledY(out float y) ? "Y "+y.ToString("F1")+" / "+DeepY.ToString("F0") : Hint(id);
                case LifeMilestoneId.NewNiche: return Hint(id);
                case LifeMilestoneId.Hunter: return "Укусов: "+Math.Min(hunts,5)+"/5";
                case LifeMilestoneId.Dynasty: return founderGeneration == int.MinValue ? Hint(id) : "Поколение "+Math.Max(0,AliveGeneration()-founderGeneration)+" из 2 в живых";
                default: return "Живых: "+Math.Min(AliveCount(),PatriarchMembers)+"/"+PatriarchMembers;
            }
        }
        bool ControlledY(out float y)
        {
            y = 0;
            if(latest == null || latest.Controlled < 0 || latest.Controlled >= latest.Count) return false;
            var a = latest.Get(latest.Controlled);
            if(!a.Alive) return false;
            y = a.Position.Y; return true;
        }
        int AliveCount()
        {
            if(latest == null) return 0;
            int count = 0;
            for(int i = 0; i < latest.Count; i++) {
                var a = latest.Get(i);
                if(a.Alive && a.Identity != null && dynasty.Contains(a.Identity) && !deadDynasty.Contains(a.Identity)) count++;
            }
            return count;
        }
        int AliveGeneration()
        {
            if(latest == null || founderGeneration == int.MinValue) return 0;
            int best = int.MinValue;
            for(int i = 0; i < latest.Count; i++) {
                var a = latest.Get(i);
                if(a.Alive && a.Identity != null && dynasty.Contains(a.Identity) && !deadDynasty.Contains(a.Identity) && a.Biology.Generation > best) best = a.Biology.Generation;
            }
            return best == int.MinValue ? founderGeneration : best;
        }
        bool Done(LifeMilestoneId id)
        {
            switch(id) {
                case LifeMilestoneId.FirstHunt: return hunts >= 1;
                case LifeMilestoneId.WellFed: return wellFed;
                case LifeMilestoneId.Parent: return divisions >= 1;
                case LifeMilestoneId.DeepDive: return deep;
                case LifeMilestoneId.NewNiche: return newNiche;
                case LifeMilestoneId.Hunter: return hunts >= 5;
                case LifeMilestoneId.Dynasty: return GrandchildAlive(founderGeneration, AliveGeneration());
                default: return PatriarchReached(AliveCount());
            }
        }
        // Returns true when at least one milestone completed during this tick.
        public bool Record(VolumeWorld world, int controlled)
        {
            if(world == null) return false;
            // The per-tick bite flag must count once even if a caller repeats the tick.
            bool newTick = world.Tick != lastTick;
            lastTick = world.Tick;
            if(!ReferenceEquals(world, latest)) { latest = world; journalSequence = 0; }
            if(controlled >= 0 && controlled < world.Count) {
                var a = world.Get(controlled);
                if(a.Alive && !string.IsNullOrEmpty(a.Identity)) {
                    dynasty.Add(a.Identity); deadDynasty.Remove(a.Identity);
                    if(founderGeneration == int.MinValue) founderGeneration = a.Biology.Generation;
                    if(founderNiche < 0) founderNiche = LineageChallenge.NicheKind(world.EnvironmentAt(a.Position));
                }
            }
            // Journal events are authoritative: divisions and deaths of dynasty members.
            for(long s = journalSequence + 1; world.Life.TryRead(s, out LifeEvent e); s++) {
                journalSequence = s;
                if(e.Kind == LifeEventKind.Division) {
                    var child = e.Child.Identity; var parent = e.Agent.Identity;
                    if(parent != null && dynasty.Contains(parent) && child != null) { dynasty.Add(child); divisions++; }
                } else if(e.Kind == LifeEventKind.Death && e.Agent.Identity != null && dynasty.Contains(e.Agent.Identity))
                    deadDynasty.Add(e.Agent.Identity);
            }
            if(controlled >= 0 && controlled < world.Count) {
                var a = world.Get(controlled);
                if(a.Alive) {
                    if(newTick && BitesThisTick(a.Biology)) hunts++;
                    if(IsWellFed(a.Biology)) wellFed = true;
                    if(ReachesDepth(a.Position)) deep = true;
                    if(IsNewNiche(founderNiche, LineageChallenge.NicheKind(world.EnvironmentAt(a.Position)))) newNiche = true;
                }
            }
            bool any = false;
            for(int i = 0; i < Total; i++) {
                var id = (LifeMilestoneId)i;
                if(!finished.Contains(id) && Done(id)) { finished.Add(id); completed.Enqueue(Title(id)); any = true; }
            }
            return any;
        }
    }
}
