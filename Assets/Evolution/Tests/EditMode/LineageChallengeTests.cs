using System;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LineageChallengeTests
    {
        static FoodCloud MigrationCloud(string identity, V3 position, float substrate = 40) => new FoodCloud {
            Identity = identity, Position = position, Radius = 2, Substrate = substrate,
            Oxygen = 70, Nutrients = 20, NicheInitialized = true
        };

        static VolumeWorld MigrationWorld(FoodCloud[] food)
        {
            var founder = new VolumeAgent {
                Alive = true, Identity = "founder", Position = new V3(0, 24, 0), Direction = new V3(1, 0, 0),
                NicheInitialized = true, Matter = 1, MineralReserve = .75f,
                Biology = new Cell {
                    Id = 1, LineageId = 1, Energy = 24, Integrity = 1, Growth = 1, Age = 100,
                    Genes = new Genome {
                        Size = 1, Speed = .7f, Uptake = 1.6f, PreferredTemperature = 17, ThermalTolerance = .9f
                    }
                }
            };
            var stranger = founder;
            stranger.Identity = "stranger";
            stranger.Position = new V3(10, 24, 0);
            stranger.Biology.Id = stranger.Biology.LineageId = 2;
            stranger.Biology.Energy = 12;
            return new VolumeWorld(new[] { founder, stranger }, food, Array.Empty<V3>(), 0, default,
                scenario: EcologicalScenario.Natural) { Controlled = 0, NutrientSupply = false };
        }

        static void DivideAndFollow(VolumeWorld world, LineageChallenge challenge)
        {
            var play = new CellLineagePlay();
            Assert.IsTrue(play.TakeControl(world, 0));
            Assert.IsTrue(world.RequestDivision(), world.DivisionBlock(0));
            world.Step();
            challenge.RecordStep(world);
            Assert.AreEqual(1, challenge.VerifiedBirths);
            Assert.IsTrue(play.FollowOffspring(world));
        }

        [Test]
        public void MigrationTargetsNearestViableFoodInARealForeignNiche()
        {
            var world = MigrationWorld(new[] {
                MigrationCloud("founder-niche", new V3(3, 24, 0)),
                MigrationCloud("transition", new V3(0, 10, 0)),
                MigrationCloud("depleted-vent", new V3(12, -18, -8), 0),
                MigrationCloud("far-dark", new V3(40, -25, 0)),
                MigrationCloud("near-dark", new V3(0, -24, 0))
            });
            var challenge = new LineageChallenge();
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.AreEqual(1, challenge.FounderNicheKind);
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
            DivideAndFollow(world, challenge);

            var descendant = world.Get(world.Controlled);
            var cloud = world.GetFood(4);
            long tick = world.Tick;
            Assert.AreEqual(4, challenge.FindMigrationFood(world, out int kind));
            Assert.AreEqual(2, kind);
            Assert.AreEqual(descendant, world.Get(world.Controlled));
            Assert.AreEqual(cloud, world.GetFood(4));
            Assert.AreEqual(tick, world.Tick);

            world.Controlled = 1;
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
            world.Controlled = 0;
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
            world.Controlled = 2;
            challenge.Stop();
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
        }

        [Test]
        public void MigrationGivesNoTargetWhenOnlyForeignFoodIsInsufficient()
        {
            var world = MigrationWorld(new[] {
                MigrationCloud("founder-niche", new V3(3, 24, 0)),
                MigrationCloud("weak-dark", new V3(0, -24, 0), .05f)
            });
            var challenge = new LineageChallenge();
            Assert.IsTrue(challenge.Begin(world, 0));
            DivideAndFollow(world, challenge);
            Assert.Greater(world.FoodPotential(1, world.Get(world.Controlled).Biology), 0);
            Assert.IsTrue(challenge.CanGuideMigration(world));
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
        }

        [TestCase(.30f)]
        [TestCase(.60f)]
        public void MigrationDoesNotInterruptWarningsOrOngoingSettlement(float energyFraction)
        {
            var world = MigrationWorld(new[] { MigrationCloud("near-dark", new V3(0, -24, 0)) });
            var challenge = new LineageChallenge();
            Assert.IsTrue(challenge.Begin(world, 0));
            DivideAndFollow(world, challenge);
            Assert.AreEqual(0, challenge.FindMigrationFood(world, out _));

            var child = world.Get(world.Controlled);
            child.Position = world.GetFood(0).Position;
            child.Biology.Energy = Biology.Capacity(child.Biology.Genes) * energyFraction;
            var migrated = new VolumeWorld(new[] { world.Get(0), world.Get(1), child }, new[] { world.GetFood(0) },
                Array.Empty<V3>(), world.Tick + 1, default, scenario: EcologicalScenario.Natural) { Controlled = 2 };
            challenge.RecordStep(migrated);
            if (energyFraction < .35f) StringAssert.Contains("голодает", challenge.SettlementHint);
            else Assert.Greater(challenge.CurrentNicheSeconds, 0);
            Assert.AreEqual(-1, challenge.FindMigrationFood(migrated, out _));

            var play = new CellLineagePlay();
            Assert.IsTrue(play.TakeControl(migrated, 0));
            Assert.IsTrue(challenge.CanPromptOffspring(migrated, play),
                "The founder can still hand off control while the child is settling or needs food.");
        }

        [Test]
        public void PaidDivisionAndGuidedMigrationCanWinInTheRealVolumeWorld()
        {
            var genes = new Genome {
                Size = 1, Speed = 1.7f, Uptake = 2, Photosynthesis = .9f,
                OrganicSpecialization = .6f, PreferredTemperature = 22,
                ThermalTolerance = .8f, EnergyEfficiency = .9f
            };
            var founder = new VolumeAgent {
                Identity = "founder", Alive = true, Position = new V3(0, 15, 0), Direction = new V3(0, 1, 0),
                NicheInitialized = true, Matter = .4f, MineralReserve = .75f,
                Biology = new Cell {
                    Id = 1, LineageId = 1, Genes = genes, Energy = Biology.Capacity(genes),
                    Age = 100, Integrity = 1, Growth = 1
                }
            };
            var food = new FoodCloud {
                Identity = "food", Position = new V3(0, 23.5f, 0), Radius = 6, Substrate = 35,
                Organic = 55, Nutrients = 10, Dissolved = 30, Oxygen = 10, NicheInitialized = true
            };
            var world = new VolumeWorld(new[] { founder }, new[] { food }, Array.Empty<V3>(), 0, default,
                scenario: EcologicalScenario.Natural);
            var play = new CellLineagePlay();
            var challenge = new LineageChallenge();
            Assert.IsTrue(play.TakeControl(world, 0));
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.AreEqual(0, challenge.FounderNicheKind);
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
            Assert.IsTrue(world.RequestDivision(), world.DivisionBlock(0));
            world.Step();
            challenge.RecordStep(world);
            Assert.Greater(world.Get(0).Biology.Budget.Division, 0);
            Assert.AreEqual(1, challenge.VerifiedBirths);
            Assert.IsTrue(challenge.CanPromptOffspring(world, play));
            int descendantIndex = play.PeekOffspring(world);
            Assert.AreEqual(0, world.Controlled);
            Assert.IsTrue(play.FollowOffspring(world));
            Assert.AreEqual(descendantIndex, world.Controlled);
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
            int foodIndex = challenge.FindMigrationFood(world, out int kind);
            Assert.AreEqual(0, foodIndex);
            Assert.AreEqual(1, kind);

            for (int i = 0; i < 680 && challenge.State != LineageChallengeState.Won; i++) {
                var descendant = world.Get(descendantIndex);
                Assert.IsTrue(descendant.Alive);
                var toFood = world.GetFood(foodIndex).Position - descendant.Position;
                world.Input = toFood.Length > 1.2f ? toFood.Unit : default;
                world.Step();
                challenge.RecordStep(world);
                if (challenge.CurrentNicheSeconds < LineageChallenge.RequiredNicheSeconds)
                    Assert.AreNotEqual(LineageChallengeState.Won, challenge.State);
            }

            Assert.AreEqual(LineageChallengeState.Won, challenge.State);
            Assert.GreaterOrEqual(challenge.CurrentNicheSeconds, LineageChallenge.RequiredNicheSeconds);
            var survivor = world.Get(descendantIndex);
            Assert.AreEqual(kind, LineageChallenge.NicheKind(world.EnvironmentAt(survivor.Position)));
            Assert.Greater(survivor.Biology.AssimilatedEnergy, 0);
            Assert.AreEqual(-1, challenge.FindMigrationFood(world, out _));
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
        }

        [Test]
        public void HandoffPromptMatchesTheNextLivingChildOfTheCurrentAttempt()
        {
            var world = MigrationWorld(Array.Empty<FoodCloud>());
            Assert.IsTrue(world.RequestDivision());
            world.Step();
            var olderChild = world.Get(2);
            var founder = world.Get(0);
            founder.MineralReserve = .75f;
            founder.Biology.Energy = Biology.Capacity(founder.Biology.Genes);
            world = new VolumeWorld(new[] { founder, world.Get(1), olderChild }, Array.Empty<FoodCloud>(),
                Array.Empty<V3>(), world.Tick + 41, default, scenario: EcologicalScenario.Natural);

            var play = new CellLineagePlay();
            var challenge = new LineageChallenge();
            Assert.IsTrue(play.TakeControl(world, 0));
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.AreEqual(2, play.PeekOffspring(world));
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
            Assert.IsTrue(world.RequestDivision(), world.DivisionBlock(0));
            world.Step();
            challenge.RecordStep(world);
            Assert.AreEqual(1, challenge.VerifiedBirths);
            Assert.IsTrue(challenge.CanPromptOffspring(world, play));
            Assert.IsTrue(challenge.CanPromptOffspring(world, play), "Observing the hint must not advance the Y cycle.");
            Assert.IsTrue(play.FollowOffspring(world));
            int youngerIndex = world.Controlled;
            Assert.AreNotEqual(2, youngerIndex);

            Assert.IsTrue(play.TakeControl(world, 1));
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
            Assert.IsTrue(play.TakeControl(world, 0));
            Assert.AreEqual(2, play.PeekOffspring(world));
            Assert.IsFalse(challenge.CanPromptOffspring(world, play), "The next Y leads to a child born before this attempt.");
            Assert.IsTrue(play.FollowOffspring(world));
            Assert.AreEqual(2, world.Controlled);
            Assert.IsTrue(play.TakeControl(world, 0));

            var deadFounder = world.Get(0);
            deadFounder.Alive = false;
            var afterDeath = new VolumeWorld(new[] { deadFounder, olderChild, world.Get(youngerIndex) },
                Array.Empty<FoodCloud>(), Array.Empty<V3>(), world.Tick + 1, default, scenario: EcologicalScenario.Natural);
            play.RecordStep(afterDeath);
            challenge.RecordStep(afterDeath);
            Assert.IsTrue(challenge.CanPromptOffspring(afterDeath, play));
            Assert.AreEqual(-1, afterDeath.Controlled);

            var unloaded = new VolumeWorld(new[] { deadFounder }, Array.Empty<FoodCloud>(),
                Array.Empty<V3>(), afterDeath.Tick + 1, default, scenario: EcologicalScenario.Natural);
            challenge.RecordStep(unloaded);
            Assert.AreEqual(LineageChallengeState.Unobserved, challenge.State);
            Assert.IsFalse(challenge.CanPromptOffspring(unloaded, play));
            Assert.IsFalse(play.FollowOffspring(unloaded));
            Assert.AreEqual(-1, unloaded.Controlled);
        }

        [Test]
        public void RequestedPaidDivisionCreatesOnlyARealVerifiedDescendant()
        {
            var genes = new Genome {
                Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22
            };
            var founder = new VolumeAgent {
                Position = new V3(0, 0, 0),
                Direction = new V3(1, 0, 0),
                Biology = new Cell {
                    Genes = genes, Energy = 100, Integrity = 1, Growth = 1, Age = 100
                }
            };
            var world = new VolumeWorld(1, 123, new[] { founder }) { Controlled = 0 };
            var challenge = new LineageChallenge();
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.IsNull(world.DivisionBlock(0), "The prepared founder must be eligible for real division.");
            float beforeEnergy = world.Get(0).Biology.Energy;
            Assert.IsTrue(world.RequestDivision());
            world.Step();
            challenge.RecordStep(world);

            Assert.AreEqual(1, world.Births, "Only the real simulation may produce a birth.");
            Assert.AreEqual(2, world.Count);
            Assert.AreEqual(1, challenge.VerifiedBirths);
            Assert.AreEqual(2, challenge.LivingKnown);
            Assert.IsTrue(LineageChallenge.VerifiedChild(world.Get(0), world.Get(1)));
            Assert.Less(world.Get(0).Biology.Energy, beforeEnergy,
                "A real daughter must not be created without spending the parent's energy.");
            Assert.AreNotEqual(LineageChallengeState.Won, challenge.State,
                "A birth alone does not prove settlement in a new ecological niche.");
            challenge.RecordStep(world);
            Assert.AreEqual(1, challenge.VerifiedBirths,
                "Repeated HUD observations must not count the same child twice.");
        }

        [Test]
        public void DeniedDivisionCannotCountAsBirthOrChallengeProgress()
        {
            // The same real-world division request must not grant an objective
            // when the founder cannot afford the biological cost.
            var founder = new VolumeAgent {
                Position = new V3(0, 0, 0),
                Direction = new V3(1, 0, 0),
                Biology = new Cell {
                    Genes = new Genome { Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22 },
                    Energy = .01f, Integrity = 1, Growth = 1, Age = 100
                }
            };
            var world = new VolumeWorld(1, 124, new[] { founder }) { Controlled = 0 };
            var challenge = new LineageChallenge();
            var play = new CellLineagePlay();
            Assert.IsTrue(play.TakeControl(world, 0));
            Assert.IsTrue(challenge.Begin(world, 0));
            Assert.IsNotNull(world.DivisionBlock(0), "Insufficient energy must block division.");
            Assert.IsFalse(world.RequestDivision(), "A blocked division must not queue a free daughter.");
            Assert.IsFalse(challenge.CanPromptOffspring(world, play));
            Assert.IsFalse(play.FollowOffspring(world));
            Assert.AreEqual(0, world.Controlled);
            world.Step();
            challenge.RecordStep(world);
            Assert.AreEqual(0, world.Births);
            Assert.AreEqual(0, challenge.VerifiedBirths);
            Assert.AreNotEqual(LineageChallengeState.Won, challenge.State);
        }

        [Test]
        public void RealWorldTicksDoNotAdvanceChallengeWithoutVerifiedOffspring()
        {
            // Integration smoke on the actual simulation, not synthetic births.
            var world = new VolumeWorld(1, 123);
            var challenge = new LineageChallenge();
            Assert.IsTrue(challenge.Begin(world, 0));
            for (int step = 0; step < 3; step++) {
                world.Step();
                challenge.RecordStep(world);
                Assert.AreEqual(0, challenge.VerifiedBirths,
                    "No paid division means no verified birth.");
                Assert.AreNotEqual(LineageChallengeState.Won, challenge.State);
                double progress = challenge.CurrentNicheSeconds;
                challenge.RecordStep(world);
                Assert.AreEqual(progress, challenge.CurrentNicheSeconds, 1e-9,
                    "A second observation of the same tick cannot advance settlement.");
            }
        }

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
        public void CorruptEnergyCannotGrantFreeNicheVictory()
        {
            // The challenge must not reward invalid energy snapshots as survival.
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, float.NaN));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, float.PositiveInfinity));
            Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, 1.1f));
            Assert.IsTrue(LineageChallenge.EligibleNiche(1, 2, true, true, .75f));
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
        public void MigrationGoalOffersOnlyDifferentHabitatsAndPreservesUrgentWarnings()
        {
            StringAssert.Contains("темнота", LineageChallenge.SettlementProgressGoal(0, null, 1));
            StringAssert.DoesNotContain("светлая вода", LineageChallenge.MigrationChoices(1));
            StringAssert.Contains("светлая вода", LineageChallenge.MigrationChoices(2));
            StringAssert.DoesNotContain("темнота", LineageChallenge.MigrationChoices(2));
            StringAssert.DoesNotContain("термальный источник", LineageChallenge.MigrationChoices(3));
            StringAssert.Contains("термальный источник", LineageChallenge.MigrationChoices(0));
            StringAssert.Contains("Закрепляйся", LineageChallenge.SettlementProgressGoal(2, null, 1));
            Assert.AreEqual("Потомок голодает", LineageChallenge.SettlementProgressGoal(0, "Потомок голодает", 1));
        }

        [Test]
        public void ExtinctionSummaryExplainsDifferentNextDecisionsBeforeAndAfterDivision()
        {
            string before = LineageChallenge.OutcomeSummary(LineageChallengeState.Extinct, 0, 0);
            StringAssert.Contains("до первого деления", before);
            StringAssert.Contains("накопи энергию", before);
            string after = LineageChallenge.OutcomeSummary(LineageChallengeState.Extinct, 2, 0);
            StringAssert.Contains("потомки не выжили", after);
            StringAssert.Contains("Рождений: 2", after);
            StringAssert.Contains("другую нишу", after);
            Assert.IsNull(LineageChallenge.OutcomeSummary(LineageChallengeState.Growing, 0, 1));
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
        public void InvalidDescendantEnergyIsExplainedAndCannotCompleteSettlement()
        {
            foreach (float energy in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f }) {
                Assert.IsFalse(LineageChallenge.EligibleNiche(1, 2, true, true, energy));
                StringAssert.Contains("недостоверна", LineageChallenge.SettlementAdvice(true, energy, true));
            }
            StringAssert.Contains("голодает", LineageChallenge.SettlementAdvice(true, .30f, true));
            Assert.IsNull(LineageChallenge.SettlementAdvice(true, .75f, true));
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
