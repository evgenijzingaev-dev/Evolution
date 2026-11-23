using System;
using System.Collections.Generic;
using Evolution.Core;

namespace Evolution.Volume
{
    public enum DevelopmentDirection { Natural, Photosynthesis, Organic, Mobility }

    // Paid change to the inherited template. The original mutation algorithm still runs on every gene.
    public static class CellEvolution
    {
        public static bool Valid(DevelopmentDirection d)=>d>=DevelopmentDirection.Natural&&d<=DevelopmentDirection.Mobility;
        public static float Cost(Cell parent,DevelopmentDirection d)=>d==DevelopmentDirection.Natural?0:2*parent.Genes.Size;
        public static Genome Template(Genome g,DevelopmentDirection d)
        {
            switch(d) {
                case DevelopmentDirection.Photosynthesis:g.Photosynthesis=Math.Min(1,g.Photosynthesis+.1f);break;
                case DevelopmentDirection.Organic:g.OrganicSpecialization=Math.Min(1,g.OrganicSpecialization+.1f);break;
                case DevelopmentDirection.Mobility:g.Speed=Math.Min(3,g.Speed+.15f);break;
            }
            return g;
        }
        public static bool Available(Cell c,DevelopmentDirection d)=>Valid(d)&&(d==DevelopmentDirection.Natural||!Template(c.Genes,d).Equals(c.Genes));
        public static string Name(DevelopmentDirection d)=>d==DevelopmentDirection.Photosynthesis?"Фотосинтез":d==DevelopmentDirection.Organic?"Органическое питание":d==DevelopmentDirection.Mobility?"Подвижность":"Естественное наследование";
        public static string Tradeoff(DevelopmentDirection d)=>d==DevelopmentDirection.Natural?"Обычное наследование со случайными мутациями; стандартная цена деления сохраняется.":d==DevelopmentDirection.Photosynthesis?
            "Больше энергии от света; дороже обмен и структура. Без света преимущество исчезает.":d==DevelopmentDirection.Organic?
            "Лучше усваивает органику; хуже субстрат, дороже обмен. Нужна органическая пища.":
            "Быстрее плавает; дороже обмен и плавание. Высокая скорость быстрее истощает запас.";
        static int ParentSeparator(VolumeAgent child) => child.Biology.ParentId == 0 || child.Identity == null
            ? -1 : child.Identity.LastIndexOf("/b", StringComparison.Ordinal);
        public static string ParentIdentity(VolumeAgent child)
        {
            int at = ParentSeparator(child);
            return at < 0 ? null : child.Identity.Substring(0, at);
        }
        public static List<int> Offspring(VolumeWorld world,string parent)
        {
            var result = new List<int>();
            CollectOffspring(world, parent, result);
            return result;
        }
        internal static void CollectOffspring(VolumeWorld world, string parent, List<int> result)
        {
            result.Clear();
            if (parent == null || world == null) return;
            Cell? anchor = null;
            for (int i = 0; i < world.Count; i++) {
                var agent = world.Get(i);
                if (agent.Identity != parent) continue;
                anchor = agent.Biology;
                break;
            }
            for (int i = world.Count - 1; i >= 0; i--) {
                var agent = world.Get(i);
                if (!agent.Alive || ParentSeparator(agent) != parent.Length
                    || string.CompareOrdinal(agent.Identity, 0, parent, 0, parent.Length) != 0) continue;
                if (anchor.HasValue && (agent.Biology.ParentId != anchor.Value.Id || agent.Biology.LineageId != anchor.Value.LineageId
                    || agent.Biology.Generation != anchor.Value.Generation + 1)) continue;
                result.Add(i);
            }
        }
        public static string DeathReason(VolumeAgent a)=>a.Biology.Consumed?"Клетка съедена хищником. Продолжи за живого потомка.":
            "Запас энергии исчерпан. "+(a.Biology.Budget.Movement>a.Biology.Budget.Upkeep?"Основной расход последнего шага — движение.":"Питание не покрыло обмен веществ.");
        public static string EnergyReason(VolumeWorld world,int index)
        {
            if(index<0||index>=world.Count)return "Выбери клетку, чтобы увидеть её обмен.";
            var a=world.Get(index);if(!a.Alive)return DeathReason(a);var c=a.Biology;var b=c.Budget;
            if(b.PredationLoss>0)return "Укусы отнимают энергию. Уходи от хищника.";
            bool inside=false;float stock=0;
            for(int k=0;k<world.FoodCount;k++){var f=world.GetFood(k);if((f.Position-a.Position).Sqr<f.Radius*f.Radius){inside=true;stock+=f.Substrate*2+f.Organic;}}
            if(!inside)return "Ты вне пищевого облака. Подплыви к отмеченной пище.";
            if(stock<.001f)return "Запас этого облака исчерпан. Ищи другое или дождись притока.";
            if(b.Movement>b.Upkeep*.5f&&b.Intake<b.Upkeep+b.Movement)return "Плавание расходует запас быстрее питания. Остановись внутри облака.";
            var sample=world.Sample(a.Position);
            if(Biology.Efficiency(c.Genes,sample.Temperature)<.7f)return "Температура снижает усвоение и повышает расход. Ищи подходящую воду.";
            if(sample.Light<.2f&&c.Genes.Photosynthesis>.5f)return "Мало света для фотосинтеза. Поднимись к свету; нужен и субстрат.";
            if(c.Growth<1)return "Потомок тратит энергию на рост. Для роста нужен запас выше 35%.";
            return b.Intake<b.Upkeep+b.Movement?"Усвоение ниже расхода. Ищи пищу, подходящую геному.":"Питание покрывает текущий расход. Копи запас для деления.";
        }
        public static readonly string[] TraitNames={"Скорость","Размер","Поглощение","Любимая температура","Усвоение органики","Сцепление","Размер колонии","Ветвление","Специализация","Транспорт","Роли клеток","Фотосинтез","Хищничество","Мембрана","Чувствительность","Термостойкость","Защита от токсинов","Экономность"};
        public static float Trait(Genome g,int i)
        {
            switch(i) {
                case 0:return g.Speed;case 1:return g.Size;case 2:return g.Uptake;case 3:return g.PreferredTemperature;case 4:return g.OrganicSpecialization;
                case 5:return g.Adhesion;case 6:return g.ColonySize;case 7:return g.Branching;case 8:return g.Differentiation;case 9:return g.Transport;
                case 10:return g.RolePattern;case 11:return g.Photosynthesis;case 12:return g.Predation;case 13:return g.Membrane;case 14:return g.Sensitivity;
                case 15:return g.ThermalTolerance;case 16:return g.ToxinResistance;default:return g.EnergyEfficiency;
            }
        }
        public static string Changes(Cell child)
        {
            if(child.ParentId==0)return "Основатель: сравнение с родителем отсутствует.";
            var result=new List<string>();for(int i=0;i<TraitNames.Length;i++) {float before=Trait(child.ParentGenes,i),after=Trait(child.Genes,i);
                if(before!=after)result.Add(TraitNames[i]+" "+(Math.Abs(after-before)<.005f?(after>before?"↑ <0.01":"↓ <0.01"):(after-before).ToString("+0.00;-0.00;0.00")));}
            return result.Count==0?"Геном унаследован без изменений.":string.Join(" · ",result);
        }
    }
    // Tracks a real played identity, never picks an unrelated replacement after death/unloading.
    public sealed class CellLineagePlay
    {
        int ownedCell,ownedLineage,ownedGeneration;
        readonly Dictionary<string,int> cycles=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly List<int> offspring = new List<int>();
        public string Identity {get;private set;}
        public VolumeAgent? Fallen {get;private set;}
        public bool TakeControl(VolumeWorld world,int index)
        {
            if(index<0||index>=world.Count||!world.Get(index).Alive)return false;
            world.Controlled=index;var a=world.Get(index);Identity=a.Identity;ownedCell=a.Biology.Id;ownedLineage=a.Biology.LineageId;ownedGeneration=a.Biology.Generation;Fallen=null;return true;
        }
        public void RecordStep(VolumeWorld world)
        {
            if(Identity==null||Fallen.HasValue)return;
            for(int i=0;i<world.Count;i++){var a=world.Get(i);if(a.Identity==Identity){if(!a.Alive)Fallen=a;return;}}
        }
        // Verified direct children of the played cell; empty while identity is unknown.
        public List<int> VerifiedChildren(VolumeWorld world)
        {
            var children = new List<int>();
            CollectVerifiedChildren(world, children);
            return children;
        }
        void CollectVerifiedChildren(VolumeWorld world, List<int> children)
        {
            CellEvolution.CollectOffspring(world, Identity, children);
            for (int i = children.Count - 1; i >= 0; i--) {
                var child = world.Get(children[i]).Biology;
                if (child.ParentId != ownedCell || child.LineageId != ownedLineage
                    || child.Generation != ownedGeneration + 1) children.RemoveAt(i);
            }
        }
        public int PeekOffspring(VolumeWorld world)
        {
            CollectVerifiedChildren(world, offspring);
            return NextOffspring(out _);
        }
        int NextOffspring(out int offset)
        {
            offset = 0;
            if (offspring.Count == 0) return -1;
            cycles.TryGetValue(Identity, out offset);
            return offspring[offset % offspring.Count];
        }
        // Without an index Y cycles through the verified offspring of the played cell:
        // returning to the same parent resumes where the last Y stopped, so a second
        // child is reachable without opening the development panel.
        public bool FollowOffspring(VolumeWorld world,int index=-1)
        {
            CollectVerifiedChildren(world, offspring);
            if (index < 0) {
                index = NextOffspring(out int offset);
                if (index < 0) return false;
                if (cycles.Count > 64) cycles.Clear();
                cycles[Identity] = (offset + 1) % offspring.Count;
            }
            else if (!offspring.Contains(index)) return false;
            return TakeControl(world,index);
        }
    }
    // First gameplay milestone for #38: observe one real lineage without changing biology.
    // Missing streamed agents are UNKNOWN, never silently dead or replaced by strangers.
    public enum LineageChallengeState { Inactive, Growing, OffspringAlive, Unobserved, Extinct, Won }

    public sealed class LineageChallenge
    {
        struct Member
        {
            public int Id, LineageId, Generation;
            public bool Dead;
        }
        readonly Dictionary<string, Member> members = new Dictionary<string, Member>(StringComparer.Ordinal);
        readonly Dictionary<string, double> nicheResidence = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly Dictionary<string, int> nicheResidenceKind = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, float> nicheEntryEnergy = new Dictionary<string, float>(StringComparer.Ordinal);
        int founderNiche;
        public const double RequiredNicheSeconds = 15;
        // HUD must not round 14.6 seconds to a misleading 15/15 before victory.
        public static int DisplayNicheSeconds(double seconds)
            => double.IsNaN(seconds) || seconds <= 0 ? 0
                : (int)Math.Floor(Math.Min(RequiredNicheSeconds, seconds));
        public double BestNicheSeconds { get; private set; }
        // Current uninterrupted progress, not a historic peak from a failed attempt.
        public double CurrentNicheSeconds { get; private set; }
        // Last verified obstacle, shown as advice rather than an invented cause of death.
        public string SettlementHint { get; private set; }
        // Show a useful next action once settlement is already underway.
        public static string SettlementProgressGoal(double currentSeconds, string obstacle, int founderKind = -1)
            => obstacle ?? (currentSeconds > 0
                ? "Закрепляйся в новой нише: сохраняй энергию и не покидай её"
                : founderKind < 0 ? "Переведи потомка в другую нишу"
                : "Выбери среду для потомка: " + MigrationChoices(founderKind));
        // Offer only genuinely different target habitats. This is guidance, not a
        // teleport or resource grant: the player must still migrate and survive.
        public static string MigrationChoices(int founderKind)
        {
            if (founderKind == 1) return "темнота или термальный источник";
            if (founderKind == 2) return "светлая вода или термальный источник";
            if (founderKind == 3) return "светлая вода или темнота";
            return "светлая вода, темнота или термальный источник";
        }
        public int FounderNicheKind => founderNiche;
        public static string SettlementAdvice(bool inNewNiche, float energyFraction, bool sustainable)
        {
            if (!inNewNiche) return "Переведи потомка в другую освещённую, тёмную или термальную нишу";
            if (float.IsNaN(energyFraction) || float.IsInfinity(energyFraction) || energyFraction < 0f || energyFraction > 1f)
                return "Энергия потомка недостоверна: дождись корректного наблюдения";
            if (energyFraction < .35f) return "Потомок голодает: найди доступный источник энергии";
            if (!sustainable) return "Энергия убывает: попробуй другую пищу или нишу";
            return null;
        }
        // When several descendants are observed, the most urgent verified obstacle wins.
        public static string MoreUrgentSettlementHint(string existing, string candidate)
        {
            if (candidate == null) return existing;
            if (existing == null || candidate.Contains("голодает")) return candidate;
            return existing;
        }
        public static string NicheName(int kind)
            => kind == 1 ? "светлая вода" : kind == 2 ? "темнота" : kind == 3 ? "термальный источник" : "переходная вода";
        public string FounderNicheName => NicheName(founderNiche);
        public static int NicheKind(NicheSample sample)
        {
            if (sample.Vent >= .4f) return 3; // Hydrothermal
            if (sample.Light < .12f) return 2; // Dark
            if (sample.Light >= .55f) return 1; // Sunlit
            return 0; // Transitional water
        }
        public static double AdvanceNicheResidence(double previous, bool eligible, double stepSeconds)
        {
            // Invalid or non-forward simulation time must never grant an instant victory.
            // A corrupted prior streak is discarded rather than carried into a new tick.
            if (!eligible || double.IsNaN(previous) || double.IsInfinity(previous)
                || double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0)
                return 0;
            return Math.Max(0, previous) + stepSeconds;
        }
        // A lineage must establish itself in ONE niche; alternating between two different
        // foreign niches is migration, not uninterrupted settlement.
        public static double AdvanceNicheResidenceInKind(double previous, int previousKind, int currentKind, bool eligible, double stepSeconds)
            => AdvanceNicheResidence(previousKind == currentKind ? previous : 0, eligible, stepSeconds);
        // Transitional water is not a distinct ecological niche and cannot award settlement.
        // Settlement must be supported by local nutrition, not just inherited reserves.
        // A small loss is tolerated for movement and short resource fluctuations.
        public static bool SustainableResidence(float entryFraction, float currentFraction)
            => currentFraction >= entryFraction - .10f;
        public static bool EligibleNiche(int founderKind, int currentKind, bool isDescendant, bool alive, float energyFraction)
            => isDescendant && alive && currentKind != 0 && currentKind != founderKind
                && !float.IsNaN(energyFraction) && !float.IsInfinity(energyFraction)
                && energyFraction >= .35f && energyFraction <= 1f;
        long recordedTick;
        double startedAt;
        // Player-facing outcome uses only verified observations, never guessed death causes.
        public static string OutcomeSummary(LineageChallengeState state, int births, int living)
        {
            if (state == LineageChallengeState.Won)
                return "Успех: потомок закрепился в новой нише. Рождений: " + births + " · Живых: " + living;
            if (state == LineageChallengeState.Extinct)
                return births == 0
                    ? "Поражение: основатель погиб до первого деления. Найди пищу и накопи энергию перед размножением."
                    : "Поражение: потомки не выжили. Рождений: " + births + ". Попробуй другую нишу или источник питания.";
            if (state == LineageChallengeState.Unobserved)
                return "Исход неизвестен: линия вне наблюдения. Вернись к её области.";
            return null;
        }
        public bool Active { get; private set; }
        public string FounderIdentity { get; private set; }
        public int VerifiedBirths { get; private set; }
        public int LivingKnown { get; private set; }
        public LineageChallengeState State { get; private set; }
        public bool Begin(VolumeWorld world, int index)
        {
            if (world == null || index < 0 || index >= world.Count) return false;
            var a = world.Get(index);
            if (!a.Alive || string.IsNullOrEmpty(a.Identity)) return false;
            members.Clear();
            nicheResidence.Clear();
            nicheResidenceKind.Clear();
            nicheEntryEnergy.Clear();
            BestNicheSeconds = CurrentNicheSeconds = 0;
            SettlementHint = null;
            founderNiche = NicheKind(world.EnvironmentAt(a.Position));
            members.Add(a.Identity, new Member {
                Id = a.Biology.Id, LineageId = a.Biology.LineageId,
                Generation = a.Biology.Generation
            });
            FounderIdentity = a.Identity;
            Active = true;
            VerifiedBirths = 0;
            LivingKnown = 1;
            State = LineageChallengeState.Growing;
            recordedTick = world.Tick;
            startedAt = world.Time;
            return true;
        }
        public void Stop()
        {
            Active = false;
            members.Clear();
            nicheResidence.Clear();
            nicheResidenceKind.Clear();
            nicheEntryEnergy.Clear();
            BestNicheSeconds = CurrentNicheSeconds = 0;
            SettlementHint = null;
            FounderIdentity = null;
            VerifiedBirths = LivingKnown = 0;
            State = LineageChallengeState.Inactive;
        }
        public static bool VerifiedChild(VolumeAgent parent, VolumeAgent child)
        {
            return child.DivisionChild && !string.IsNullOrEmpty(parent.Identity)
                && CellEvolution.ParentIdentity(child) == parent.Identity
                && child.Biology.ParentId == parent.Biology.Id
                && child.Biology.LineageId == parent.Biology.LineageId
                && child.Biology.Generation == parent.Biology.Generation + 1;
        }
        // Once a member has died, later stale live snapshots cannot revive it.
        public static bool CountsAsLiving(bool snapshotAlive, bool confirmedDead)
        {
            return snapshotAlive && !confirmedDead;
        }
        // A streamed biological identity must contribute at most one observation per tick.
        public static bool FirstObservation(HashSet<string> observed, string identity)
            => observed.Add(identity);
        // DivisionChild is persistent; only births after the challenge began count.
        public static bool BornDuringChallenge(VolumeAgent child, double startedAt, double now)
        {
            return child.DivisionChild && child.DivisionAt > startedAt && child.DivisionAt <= now;
        }
        public bool CanGuideMigration(VolumeWorld world)
        {
            if (State != LineageChallengeState.OffspringAlive || CurrentNicheSeconds > 0 || SettlementHint != null
                || world == null || !world.Niches || world.Controlled < 0 || world.Controlled >= world.Count) return false;
            return IsLivingDescendant(world.Get(world.Controlled));
        }

        bool IsLivingDescendant(VolumeAgent agent)
            => agent.Alive && agent.Identity != null && agent.Identity != FounderIdentity
                && members.TryGetValue(agent.Identity, out var member) && !member.Dead
                && member.Id == agent.Biology.Id && member.LineageId == agent.Biology.LineageId
                && member.Generation == agent.Biology.Generation;

        public bool CanPromptOffspring(VolumeWorld world, CellLineagePlay play)
        {
            if (State != LineageChallengeState.OffspringAlive || play?.Identity == null
                || !members.ContainsKey(play.Identity)) return false;
            int target = play.PeekOffspring(world);
            return target >= 0 && IsLivingDescendant(world.Get(target));
        }

        public int FindMigrationFood(VolumeWorld world, out int niche)
        {
            niche = 0;
            if (!CanGuideMigration(world)) return -1;
            var agent = world.Get(world.Controlled);
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < world.FoodCount; i++) {
                var food = world.GetFood(i);
                float distance = (food.Position - agent.Position).Sqr;
                if (!(food.Radius > 0) || !(distance < bestDistance)) continue;
                float intake = world.FoodPotential(i, agent.Biology);
                if (!(intake > 0)) continue;
                var environment = world.EnvironmentAt(food.Position);
                int kind = NicheKind(environment);
                if (kind == 0 || kind == founderNiche) continue;
                float upkeep = Multicellularity.Cost(agent.Biology, environment.Temperature)
                    + NicheMetabolism.Stress(agent.Biology, environment);
                if (!(intake > upkeep)) continue;
                best = i;
                bestDistance = distance;
                niche = kind;
            }
            return best;
        }

        public void RecordStep(VolumeWorld world)
        {
            if (!Active || world == null || world.Tick <= recordedTick
                || State == LineageChallengeState.Extinct || State == LineageChallengeState.Won) return;
            recordedTick = world.Tick;
            // Multi-generation births can be observed in one tick after streaming.
            bool added;
            do {
                added = false;
                for (int i = 0; i < world.Count; i++) {
                    var child = world.Get(i);
                    if (string.IsNullOrEmpty(child.Identity) || members.ContainsKey(child.Identity)
                        || !BornDuringChallenge(child, startedAt, world.Time)) continue;
                    string parentIdentity = CellEvolution.ParentIdentity(child);
                    if (parentIdentity == null || !members.TryGetValue(parentIdentity, out var parent)) continue;
                    if (child.Biology.ParentId != parent.Id
                        || child.Biology.LineageId != parent.LineageId
                        || child.Biology.Generation != parent.Generation + 1) continue;
                    members.Add(child.Identity, new Member {
                        Id = child.Biology.Id, LineageId = child.Biology.LineageId,
                        Generation = child.Biology.Generation
                    });
                    VerifiedBirths++;
                    added = true;
                }
            } while (added);
            int live = 0, liveDescendants = 0, missing = 0;
            double dt = VolumeWorld.StepSeconds;
            var eligible = new HashSet<string>(StringComparer.Ordinal);
            var eligibleEnergy = new Dictionary<string, float>(StringComparer.Ordinal);
            var eligibleKind = new Dictionary<string, int>(StringComparer.Ordinal);
            var observed = new HashSet<string>(StringComparer.Ordinal);
            SettlementHint = null;
            // Resolve confirmed deaths first. Duplicate streamed snapshots of the same
            // biological identity must not inflate living counts or override a death.
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                if (a.Alive || a.Identity == null || !members.TryGetValue(a.Identity, out var member)) continue;
                if (member.Id != a.Biology.Id || member.LineageId != a.Biology.LineageId
                    || member.Generation != a.Biology.Generation) continue;
                member.Dead = true;
                members[a.Identity] = member;
            }
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                if (a.Identity == null || !members.TryGetValue(a.Identity, out var member)) continue;
                // Reused names with incompatible biological identity are not our lineage.
                if (member.Id != a.Biology.Id || member.LineageId != a.Biology.LineageId
                    || member.Generation != a.Biology.Generation) continue;
                // A biological identity can appear more than once during streaming.
                // Count its observation only once, with confirmed death taking precedence.
                if (!FirstObservation(observed, a.Identity)) continue;
                // A confirmed death is terminal for this biological identity. A stale
                // streamed snapshot must never resurrect a dead member or fake success.
                if (!a.Alive) {
                    member.Dead = true;
                    members[a.Identity] = member;
                } else if (CountsAsLiving(a.Alive, member.Dead)) {
                    live++;
                    if (a.Identity != FounderIdentity) liveDescendants++;
                    if (world.Niches) {
                        float energyFraction = a.Biology.Energy / Math.Max(.001f, Biology.Capacity(a.Biology.Genes));
                        int kind = NicheKind(world.EnvironmentAt(a.Position));
                        // A real descendant in a foreign niche may be starving before
                        // becoming eligible for settlement. Explain the blocked goal.
                        if (a.Identity != FounderIdentity && kind != 0 && kind != founderNiche
                            && (float.IsNaN(energyFraction) || float.IsInfinity(energyFraction)
                                || energyFraction < .35f || energyFraction > 1f))
                            SettlementHint = MoreUrgentSettlementHint(SettlementHint, SettlementAdvice(true, energyFraction, true));
                        if (EligibleNiche(founderNiche, kind,
                            a.Identity != FounderIdentity, true, energyFraction)) {
                            eligible.Add(a.Identity);
                            eligibleEnergy[a.Identity] = energyFraction;
                            eligibleKind[a.Identity] = kind;
                        }
                    }
                }
            }
            foreach (var pair in members)
                if (!pair.Value.Dead && !observed.Contains(pair.Key)) missing++;
            LivingKnown = live;
            // Only continuous, actually observed residence counts; unloading or low energy resets progress.
            foreach (var key in new List<string>(nicheResidence.Keys))
                if (!eligible.Contains(key)) { nicheResidence.Remove(key); nicheResidenceKind.Remove(key); nicheEntryEnergy.Remove(key); }
            CurrentNicheSeconds = 0;
            foreach (var key in eligible) {
                // Use the same identity-validated observation for both energy and niche.
                // A stale/reused identity elsewhere in the loaded world must not
                // contribute a different niche to this descendant's residence.
                int kind = eligibleKind[key];
                float energyFraction = eligibleEnergy[key];
                bool sameKind = nicheResidenceKind.TryGetValue(key, out var oldKind) && oldKind == kind;
                float entryFraction = sameKind && nicheEntryEnergy.TryGetValue(key, out var entry)
                    ? entry : energyFraction;
                if (!SustainableResidence(entryFraction, energyFraction)) {
                    nicheResidence.Remove(key);
                    nicheResidenceKind.Remove(key);
                    nicheEntryEnergy.Remove(key);
                    SettlementHint = MoreUrgentSettlementHint(SettlementHint, SettlementAdvice(true, energyFraction, false));
                    continue;
                }
                nicheEntryEnergy[key] = entryFraction;
                double elapsed = AdvanceNicheResidenceInKind(
                    nicheResidence.TryGetValue(key, out var previous) ? previous : 0,
                    nicheResidenceKind.TryGetValue(key, out var previousKind) ? previousKind : -1,
                    kind, true, dt);
                nicheResidence[key] = elapsed;
                nicheResidenceKind[key] = kind;
                CurrentNicheSeconds = Math.Max(CurrentNicheSeconds, elapsed);
            }
            BestNicheSeconds = Math.Max(BestNicheSeconds, CurrentNicheSeconds);
            if (VerifiedBirths > 0 && CurrentNicheSeconds >= RequiredNicheSeconds) {
                State = LineageChallengeState.Won;
                return;
            }
            State = live > 0 ? liveDescendants > 0 ? LineageChallengeState.OffspringAlive : LineageChallengeState.Growing
                : missing > 0 ? LineageChallengeState.Unobserved : LineageChallengeState.Extinct;
        }
    }

}
