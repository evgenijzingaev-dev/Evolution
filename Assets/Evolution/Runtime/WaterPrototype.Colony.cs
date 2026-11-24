using Evolution.Volume;

namespace Evolution.Runtime
{
    public sealed partial class WaterPrototype
    {
        public ColonyChallenge ColonyAttempt { get; } = new ColonyChallenge();

        public bool StartColonyChallenge()
        {
            if (!ColonyAttempt.Begin(World, selected)) return false;
            Challenge.Stop();
            return PlaySelected();
        }

        public bool RequestPlayerDivision() => ColonyAttempt.RequestDivision(World);

        public string PlayerDivisionBlock(int index)
        {
            if (ColonyAttempt.CanChoosePath(World) && World.Controlled == index
                && World.Get(index).PlannedConnection == BirthConnection.Natural) return "Выбери способ деления";
            return World.DivisionBlock(index);
        }

        public bool FollowPlayedOffspring()
        {
            int target = ColonyAttempt.OffspringToFollow(World, EvolutionPlay.Identity);
            if (target < 0 && ColonyAttempt.IsTrackedHandoff(EvolutionPlay.Identity)) return false;
            if (!FollowOffspring(target)) return false;
            if (target >= 0 && ColonyAttempt.Path == BirthConnection.Retain) World.ControlColony = true;
            return true;
        }
    }
}
