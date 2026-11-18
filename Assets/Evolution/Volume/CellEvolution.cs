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
        public static string ParentIdentity(VolumeAgent child)
        {if(child.Biology.ParentId==0||child.Identity==null)return null;int at=child.Identity.LastIndexOf("/b",StringComparison.Ordinal);return at<0?null:child.Identity.Substring(0,at);}
        public static List<int> Offspring(VolumeWorld world,string parent)
        {
            var result=new List<int>();if(parent==null)return result;
            Cell? anchor=null;for(int i=0;i<world.Count;i++)if(world.Get(i).Identity==parent){anchor=world.Get(i).Biology;break;}
            for(int i=world.Count-1;i>=0;i--){var a=world.Get(i);if(!a.Alive||ParentIdentity(a)!=parent)continue;
                if(anchor.HasValue&&(a.Biology.ParentId!=anchor.Value.Id||a.Biology.LineageId!=anchor.Value.LineageId||a.Biology.Generation!=anchor.Value.Generation+1))continue;
                result.Add(i);
            }
            return result;
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
        public bool FollowOffspring(VolumeWorld world,int index=-1)
        {
            var children=CellEvolution.Offspring(world,Identity);
            children.RemoveAll(i=>world.Get(i).Biology.ParentId!=ownedCell||world.Get(i).Biology.LineageId!=ownedLineage||world.Get(i).Biology.Generation!=ownedGeneration+1);
            if(index<0){if(children.Count==0)return false;index=children[0];}
            if(!children.Contains(index))return false;
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
        public static string SettlementProgressGoal(double currentSeconds, string obstacle)
            => obstacle ?? (currentSeconds > 0
                ? "Закрепляйся в новой нише: сохраняй энергию и не покидай её"
                : "Переведи потомка в другую нишу");
        public static string SettlementAdvice(bool inNewNiche, float energyFraction, bool sustainable)
        {
            if (!inNewNiche) return "Переведи потомка в другую освещённую, тёмную или термальную нишу";
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
        public static int NicheKind(NicheSample sample)
        {
            if (sample.Vent >= .4f) return 3; // Hydrothermal
            if (sample.Light < .12f) return 2; // Dark
            if (sample.Light >= .55f) return 1; // Sunlit
            return 0; // Transitional water
        }
        public static double AdvanceNicheResidence(double previous, bool eligible, double stepSeconds)
            => eligible ? Math.Max(0, previous) + Math.Max(0, stepSeconds) : 0;
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
            => isDescendant && alive && currentKind != 0 && currentKind != founderKind && energyFraction >= .35f;
        long recordedTick;
        double startedAt;
        // Player-facing outcome uses only verified observations, never guessed death causes.
        public static string OutcomeSummary(LineageChallengeState state, int births, int living)
        {
            if (state == LineageChallengeState.Won)
                return "Успех: потомок закрепился в новой нише. Рождений: " + births + " · Живых: " + living;
            if (state == LineageChallengeState.Extinct)
                return "Поражение: все подтверждённые члены линии погибли. Рождений: " + births;
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
                            && energyFraction < .35f)
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
