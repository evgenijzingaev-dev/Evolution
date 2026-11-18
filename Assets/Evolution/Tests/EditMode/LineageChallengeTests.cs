using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LineageChallengeTests
    {
        [Test]
        public void ChallengeOutcomeExplainsVerifiedResultWithoutInventingDeathCause()
        {
            StringAssert.Contains("закрепился", LineageChallenge.OutcomeSummary(LineageChallengeState.Won, 2, 1));
            StringAssert.Contains("Рождений: 2", LineageChallenge.OutcomeSummary(LineageChallengeState.Extinct, 2, 0));
            StringAssert.Contains("неизвестен", LineageChallenge.OutcomeSummary(LineageChallengeState.Unobserved, 1, 0));
            Assert.IsNull(LineageChallenge.OutcomeSummary(LineageChallengeState.Growing, 0, 1));
            Assert.IsNull(LineageChallenge.OutcomeSummary(LineageChallengeState.OffspringAlive, 1, 2));
        }

        [Test]
        public void ChallengeIsOptionalAndCanRestart()
        {
            var world = new VolumeWorld(1, 123);
            var challenge = new LineageChallenge();
            Assert.AreEqual(LineageChallengeState.Inactive, challenge.State);
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.AreEqual(LineageChallengeState.Growing, challenge.State);
            Assert.AreEqual(0, challenge.VerifiedBirths);
            Assert.AreEqual(1, challenge.LivingKnown);
            challenge.RecordStep(world); // Same tick cannot advance an objective.
            Assert.AreEqual(LineageChallengeState.Growing, challenge.State);
            challenge.Stop();
            Assert.AreEqual(LineageChallengeState.Inactive, challenge.State);
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.AreEqual(0, challenge.VerifiedBirths);
        }

        [Test]
        public void InvalidOrDeadFounderCannotStart()
        {
            var world = new VolumeWorld(1, 321);
            var challenge = new LineageChallenge();
            Assert.IsFalse(challenge.Begin(world, -1));
            Assert.IsFalse(challenge.Begin(world, world.Count));
            Assert.IsFalse(challenge.Active);
        }

        [Test]
        public void OnlyBirthsAfterStartCountEvenWhenDivisionChildPersists()
        {
            var child = new VolumeAgent { DivisionChild = true, DivisionAt = 2.0 };
            Assert.IsFalse(LineageChallenge.BornDuringChallenge(child, 2.0, 2.05),
                "A child born before challenge start cannot be counted as a new birth.");
            Assert.IsFalse(LineageChallenge.BornDuringChallenge(child, 1.0, 1.95),
                "Future births cannot be counted.");
            Assert.IsTrue(LineageChallenge.BornDuringChallenge(child, 1.0, 2.05));
            child.DivisionChild = false;
            Assert.IsFalse(LineageChallenge.BornDuringChallenge(child, 1.0, 2.05));
        }

        [Test]
        public void DuplicateStreamedIdentityCountsOnlyOncePerTick()
        {
            var observed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            Assert.IsTrue(LineageChallenge.FirstObservation(observed, "lineage/b1"));
            Assert.IsFalse(LineageChallenge.FirstObservation(observed, "lineage/b1"),
                "A duplicate streamed snapshot must not inflate the living count.");
            Assert.IsTrue(LineageChallenge.FirstObservation(observed, "lineage/b2"),
                "Different descendants must remain independently countable.");
        }

        [Test]
        public void ConfirmedDeadMemberCannotBeResurrectedByStaleStreamedSnapshot()
        {
            Assert.IsTrue(LineageChallenge.CountsAsLiving(true, false));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(false, false));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(false, true));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(true, true),
                "A stale live snapshot must not restore a confirmed dead identity.");
        }

        [Test]
        public void SwitchingBetweenForeignNichesResetsContinuousSettlement()
        {
            // A descendant cannot accumulate 15 seconds by alternating between dark
            // water and a hydrothermal vent, even if both differ from its origin.
            Assert.AreEqual(5, LineageChallenge.AdvanceNicheResidenceInKind(4, 2, 2, true, 1));
            Assert.AreEqual(1, LineageChallenge.AdvanceNicheResidenceInKind(14, 2, 3, true, 1));
            Assert.AreEqual(0, LineageChallenge.AdvanceNicheResidenceInKind(14, 2, 2, false, 1));
            Assert.AreEqual(1, LineageChallenge.AdvanceNicheResidenceInKind(14, -1, 2, true, 1));
        }

        [Test]
        public void HudExplainsFoundersOriginalHabitatWithoutChangingNicheRules()
        {
            Assert.AreEqual("светлая вода", LineageChallenge.NicheName(1));
            Assert.AreEqual("темнота", LineageChallenge.NicheName(2));
            Assert.AreEqual("термальный источник", LineageChallenge.NicheName(3));
            Assert.AreEqual("переходная вода", LineageChallenge.NicheName(0));
        }

        [Test]
        public void NicheVictoryRequiresLivingDescendantWithEnergyInDifferentEnvironment()
        {
            var sun = new NicheSample { Light = .8f, Temperature = 23 };
            var dark = new NicheSample { Light = .05f, Temperature = 14 };
            var vent = new NicheSample { Light = .01f, Vent = .9f, Temperature = 43 };
            Assert.AreEqual(1, LineageChallenge.NicheKind(sun));
            Assert.AreEqual(2, LineageChallenge.NicheKind(dark));
            Assert.AreEqual(3, LineageChallenge.NicheKind(vent));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 1, true, true, .8f));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 0, true, true, .8f),
                "Transitional water cannot count as a conquered niche merely because its light differs.");
            Assert.IsTrue(LineageChallenge.EligibleNiche(0, 1, true, true, .8f),
                "A founder from transitional water can settle a real sunlit niche.");
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, false, true, .8f));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, false, .8f));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, .34f));
            Assert.IsTrue(LineageChallenge.EligibleNiche(1, 2, true, true, .35f));
        }

        [Test]
        public void HudNeverDisplaysCompletedSettlementBeforeActualVictory()
        {
            Assert.AreEqual(0, LineageChallenge.DisplayNicheSeconds(0));
            Assert.AreEqual(14, LineageChallenge.DisplayNicheSeconds(14.99));
            Assert.AreEqual(15, LineageChallenge.DisplayNicheSeconds(15));
            Assert.AreEqual(15, LineageChallenge.DisplayNicheSeconds(100));
            Assert.AreEqual(0, LineageChallenge.DisplayNicheSeconds(double.NaN));
        }

        [Test]
        public void SettlementGoalChangesWhenMigrationBecomesActiveResidence()
        {
            StringAssert.Contains("Переведи", LineageChallenge.SettlementProgressGoal(0, null));
            StringAssert.Contains("Закрепляйся", LineageChallenge.SettlementProgressGoal(3, null));
            StringAssert.Contains("Энергия", LineageChallenge.SettlementProgressGoal(3, "Энергия убывает"));
        }

        [Test]
        public void SettlementAdviceExplainsRealEnergyRiskWithoutInventingRewards()
        {
            StringAssert.Contains("Переведи", LineageChallenge.SettlementAdvice(false, .8f, true));
            StringAssert.Contains("голодает", LineageChallenge.SettlementAdvice(true, .30f, true));
            StringAssert.Contains("Энергия убывает", LineageChallenge.SettlementAdvice(true, .60f, false));
            Assert.IsNull(LineageChallenge.SettlementAdvice(true, .60f, true));
        }

        [Test]
        public void StarvationTakesPriorityOverOtherDescendantsReserveDrainRegardlessOfIterationOrder()
        {
            string starvation = LineageChallenge.SettlementAdvice(true, .30f, true);
            string draining = LineageChallenge.SettlementAdvice(true, .60f, false);
            StringAssert.Contains("голодает",
                LineageChallenge.MoreUrgentSettlementHint(
                    LineageChallenge.MoreUrgentSettlementHint(null, draining), starvation));
            StringAssert.Contains("голодает",
                LineageChallenge.MoreUrgentSettlementHint(
                    LineageChallenge.MoreUrgentSettlementHint(null, starvation), draining));
            Assert.IsNull(LineageChallenge.MoreUrgentSettlementHint(null, null));
        }

        [Test]
        public void StarvingDescendantInForeignNicheGetsActionableAdviceBeforeSettlement()
        {
            // Low energy prevents settlement, but must not hide the reason from the player.
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, .30f));
            StringAssert.Contains("голодает", LineageChallenge.SettlementAdvice(true, .30f, true));
            Assert.IsTrue(LineageChallenge.EligibleNiche(1, 2, true, true, .40f));
            Assert.IsNull(LineageChallenge.SettlementAdvice(true, .40f, true));
        }

        [Test]
        public void NicheSettlementRequiresSustainableEnergyNotJustStoredReserves()
        {
            Assert.IsTrue(LineageChallenge.SustainableResidence(.75f, .70f),
                "Small energy losses from movement are acceptable.");
            Assert.IsTrue(LineageChallenge.SustainableResidence(.75f, .80f),
                "A well-adapted descendant can gain energy in its new habitat.");
            Assert.IsFalse(LineageChallenge.SustainableResidence(.75f, .60f),
                "A descendant draining inherited reserves must not win by waiting.");
            Assert.IsTrue(LineageChallenge.SustainableResidence(.50f, .41f),
                "The energy-loss budget is relative to the start of this residence.");
        }

        [Test]
        public void InvalidSimulationTimeCannotCompleteNicheChallenge()
        {
            Assert.AreEqual(0, LineageChallenge.AdvanceNicheResidence(14, true, double.PositiveInfinity));
            Assert.AreEqual(0, LineageChallenge.AdvanceNicheResidence(14, true, double.NaN));
            Assert.AreEqual(0, LineageChallenge.AdvanceNicheResidence(double.PositiveInfinity, true, .1));
            Assert.AreEqual(0, LineageChallenge.AdvanceNicheResidence(14, true, 0));
            Assert.AreEqual(14.1, LineageChallenge.AdvanceNicheResidence(14, true, .1), 1e-9);
        }

        [Test]
        public void NicheResidenceRequiresAnUninterruptedEligibleStreak()
        {
            double seconds = LineageChallenge.AdvanceNicheResidence(0, true, .1);
            Assert.AreEqual(.1, seconds, 1e-9);
            seconds = LineageChallenge.AdvanceNicheResidence(seconds, true, .1);
            Assert.AreEqual(.2, seconds, 1e-9);
            seconds = LineageChallenge.AdvanceNicheResidence(seconds, false, .1);
            Assert.AreEqual(0, seconds, 1e-9, "Leaving the niche or dropping below the energy threshold must reset progress.");
            seconds = LineageChallenge.AdvanceNicheResidence(seconds, true, .1);
            Assert.AreEqual(.1, seconds, 1e-9, "A new streak must not reuse historic best time.");
        }

        [Test]
        public void VerifiedChildRequiresRealDivisionAndExactGenealogy()
        {
            var parent = new VolumeAgent {
                Identity = "local-agent-0",
                Biology = new Cell { Id = 7, LineageId = 7, Generation = 2 }
            };
            var child = new VolumeAgent {
                Identity = "local-agent-0/b42",
                DivisionChild = true,
                Biology = new Cell { Id = 8, ParentId = 7, LineageId = 7, Generation = 3 }
            };
            Assert.IsTrue(LineageChallenge.VerifiedChild(parent, child));
            child.DivisionChild = false;
            Assert.IsFalse(LineageChallenge.VerifiedChild(parent, child));
            child.DivisionChild = true;
            child.Biology.ParentId = 99;
            Assert.IsFalse(LineageChallenge.VerifiedChild(parent, child));
            child.Biology.ParentId = 7;
            child.Biology.LineageId = 10;
            Assert.IsFalse(LineageChallenge.VerifiedChild(parent, child));
            child.Biology.LineageId = 7;
            child.Biology.Generation = 4;
            Assert.IsFalse(LineageChallenge.VerifiedChild(parent, child));
            child.Biology.Generation = 3;
            child.Identity = "other-agent/b42";
            Assert.IsFalse(LineageChallenge.VerifiedChild(parent, child));
        }
    }
}
