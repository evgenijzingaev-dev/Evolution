using System;
using Evolution.Core;

namespace Evolution.Volume
{
    public enum ColonyChallengeState { Inactive, AwaitingDivision, Settling, Won, Lost }
    public enum ColonyChallengeFailure { None, ParentDied, OffspringDied, BondBroken }

    public sealed class ColonyChallenge
    {
        const float MinimumEnergy = .35f;
        VolumeAgent parent, offspring;
        long recordedTick, lifeSequence, requestedAfterSequence;
        double requestedBirthTime = -1;
        int residenceKind;
        float entryEnergy;

        public ColonyChallengeState State { get; private set; }
        public ColonyChallengeFailure Failure { get; private set; }
        public BirthConnection Path { get; private set; }
        public bool Active => State != ColonyChallengeState.Inactive;
        public bool Finished => State == ColonyChallengeState.Won || State == ColonyChallengeState.Lost;
        public double ResidenceSeconds { get; private set; }
        public float EnergyReceived { get; private set; }
        public string Guidance { get; private set; }

        public static bool CanBegin(VolumeWorld world, int index) => world != null && world.Niches
            && index >= 0 && index < world.Count && world.Get(index).Alive && world.OrganismSize(index) == 1;

        public bool Begin(VolumeWorld world, int index)
        {
            if (!CanBegin(world, index)) return false;
            Stop();
            parent = world.Get(index);
            recordedTick = world.Tick;
            lifeSequence = world.Life.Latest;
            State = ColonyChallengeState.AwaitingDivision;
            return true;
        }

        public void Stop()
        {
            State = ColonyChallengeState.Inactive;
            Failure = ColonyChallengeFailure.None;
            Path = BirthConnection.Natural;
            parent = offspring = default;
            requestedBirthTime = -1;
            ResetResidence(null);
        }

        public bool CanChoosePath(VolumeWorld world) => State == ColonyChallengeState.AwaitingDivision
            && world != null && world.Controlled >= 0 && world.Controlled < world.Count
            && world.Get(world.Controlled).Alive && Matches(world.Get(world.Controlled), parent);

        public bool RequestDivision(VolumeWorld world)
        {
            if (world == null) return false;
            if (!CanChoosePath(world)) return world.RequestDivision();
            var choice = world.Get(world.Controlled).PlannedConnection;
            if (choice == BirthConnection.Natural || !world.RequestDivision()) return false;
            requestedAfterSequence = world.Life.Latest;
            requestedBirthTime = (world.Tick + 1) * (double)VolumeWorld.StepSeconds;
            return true;
        }

        public bool IsTrackedHandoff(string playedIdentity) => State == ColonyChallengeState.Settling
            && playedIdentity == parent.Identity;

        public int OffspringToFollow(VolumeWorld world, string playedIdentity)
        {
            if (world == null || !IsTrackedHandoff(playedIdentity)) return -1;
            int index = Find(world, offspring);
            return index >= 0 && world.Get(index).Alive ? index : -1;
        }

        public void RecordStep(VolumeWorld world)
        {
            if (!Active || Finished || world == null || world.Tick <= recordedTick) return;
            if (world.Tick != recordedTick + 1) ResetResidence(null);
            recordedTick = world.Tick;
            ReadLifeEvents(world.Life);
            if (requestedBirthTime >= 0 && world.Time >= requestedBirthTime) requestedBirthTime = -1;
            if (Finished) return;

            int parentIndex = Find(world, parent);
            if (State == ColonyChallengeState.AwaitingDivision) {
                if (parentIndex >= 0 && !world.Get(parentIndex).Alive) Lose(ColonyChallengeFailure.ParentDied);
                return;
            }
            int offspringIndex = Find(world, offspring);
            if (offspringIndex >= 0 && !world.Get(offspringIndex).Alive) { Lose(ColonyChallengeFailure.OffspringDied); return; }
            if (Path == BirthConnection.Retain && parentIndex >= 0 && !world.Get(parentIndex).Alive) {
                Lose(ColonyChallengeFailure.ParentDied);
                return;
            }
            if (offspringIndex < 0 || Path == BirthConnection.Retain && parentIndex < 0) {
                ResetResidence("Вернись к потомку");
                return;
            }
            if (Path == BirthConnection.Retain && world.BondParent(offspringIndex) != parentIndex) {
                Lose(ColonyChallengeFailure.BondBroken);
                return;
            }
            if (Path == BirthConnection.Separate && world.OrganismRoot(offspringIndex) != offspringIndex) {
                ResetResidence("Отдели потомка от группы");
                return;
            }

            var child = world.Get(offspringIndex);
            // A new birth and a stale far-LOD observation do not represent a feeding step.
            if (child.RenderTime != (world.Tick - 1) * (double)VolumeWorld.StepSeconds) {
                ResetResidence("Следуй за потомком к пище");
                return;
            }
            var biology = child.Biology;
            float energy = biology.Energy / Biology.Capacity(biology.Genes);
            if (!(energy >= MinimumEnergy && energy <= 1f)) {
                ResetResidence("Накопи для потомка 35% энергии");
                return;
            }
            var budget = biology.Budget;
            if (!(budget.Substrate + budget.Organic + budget.Light > 0)) {
                ResetResidence("Держись у подходящей пищи");
                return;
            }
            int kind = LineageChallenge.NicheKind(world.EnvironmentAt(child.Position));
            if (ResidenceSeconds > 0 && kind != residenceKind) ResetResidence(null);
            if (ResidenceSeconds > 0 && !LineageChallenge.SustainableResidence(entryEnergy, energy)) {
                ResetResidence("Запас убывает, поищи другую пищу");
                return;
            }
            if (ResidenceSeconds == 0) entryEnergy = energy;
            residenceKind = kind;
            ResidenceSeconds += VolumeWorld.StepSeconds;
            if (Path == BirthConnection.Retain) EnergyReceived += Math.Max(0, biology.TransportLastStep);
            Guidance = null;
            if (ResidenceSeconds >= LineageChallenge.RequiredNicheSeconds) {
                if (Path == BirthConnection.Retain && EnergyReceived <= 0)
                    ResetResidence("Для пары нужен обмен энергией");
                else State = ColonyChallengeState.Won;
            }
        }

        void ReadLifeEvents(LifeJournal life)
        {
            for (long sequence = Math.Max(lifeSequence + 1, life.Oldest); sequence <= life.Latest; sequence++) {
                if (!life.TryRead(sequence, out var item)) continue;
                if (item.Kind == LifeEventKind.Death) {
                    if (State == ColonyChallengeState.Settling && Matches(item.Agent, offspring)) {
                        Lose(ColonyChallengeFailure.OffspringDied);
                        break;
                    }
                    if ((State == ColonyChallengeState.AwaitingDivision || Path == BirthConnection.Retain)
                        && Matches(item.Agent, parent)) {
                        Lose(ColonyChallengeFailure.ParentDied);
                        break;
                    }
                }
                if (State != ColonyChallengeState.AwaitingDivision || item.Kind != LifeEventKind.Division
                    || sequence <= requestedAfterSequence || item.Time != requestedBirthTime || !Matches(item.Agent, parent)
                    || !(item.Agent.Biology.Budget.Division > 0) || !LineageChallenge.VerifiedChild(item.Agent, item.Child)) continue;
                var choice = item.Agent.PlannedConnection;
                bool linked = item.Child.BondIdentity == parent.Identity && item.Child.Biology.BondParentId == parent.Biology.Id;
                if (choice == BirthConnection.Retain && linked && item.Agent.Biology.Budget.Contact >= 2 * Multicellularity.BondCost
                    || choice == BirthConnection.Separate && item.Child.BondIdentity == null && item.Child.Biology.BondParentId == 0) {
                    Path = choice;
                    offspring = item.Child;
                    State = ColonyChallengeState.Settling;
                }
            }
            lifeSequence = life.Latest;
        }

        void ResetResidence(string guidance)
        {
            ResidenceSeconds = 0;
            EnergyReceived = 0;
            Guidance = guidance;
        }

        void Lose(ColonyChallengeFailure reason)
        {
            State = ColonyChallengeState.Lost;
            Failure = reason;
        }

        static bool Matches(VolumeAgent agent, VolumeAgent target) => agent.Identity == target.Identity
            && agent.Biology.Id == target.Biology.Id && agent.Biology.LineageId == target.Biology.LineageId
            && agent.Biology.Generation == target.Biology.Generation;

        static int Find(VolumeWorld world, VolumeAgent target)
        {
            for (int i = 0; i < world.Count; i++) if (Matches(world.Get(i), target)) return i;
            return -1;
        }
    }
}
