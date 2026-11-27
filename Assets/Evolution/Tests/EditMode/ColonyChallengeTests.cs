using System;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class ColonyChallengeTests
    {
        static VolumeWorld Ready(float energy = 24, bool withFood = true, float transport = .85f)
        {
            var founder = new VolumeAgent {
                Identity = "colony-founder", Position = new V3(0, 23.5f, 0), Direction = new V3(1, 0, 0), Alive = true,
                NicheInitialized = true, Matter = 1, MineralReserve = .75f,
                Biology = new Cell {
                    Id = 1, LineageId = 1, Age = 100, Integrity = 1, Growth = 1, Energy = energy,
                    Genes = new Genome {
                        Size = 1, Speed = 1, Uptake = 2, PreferredTemperature = 22, Adhesion = .8f,
                        Transport = transport, Photosynthesis = .9f, OrganicSpecialization = .6f, ThermalTolerance = .8f
                    }
                }
            };
            var food = new FoodCloud {
                Identity = "food", Position = founder.Position, Radius = 5, NicheInitialized = true,
                Nutrients = 30, Oxygen = 45, Substrate = 60, Organic = 70, Dissolved = 55
            };
            return new VolumeWorld(new[] { founder }, withFood ? new[] { food } : Array.Empty<FoodCloud>(),
                Array.Empty<V3>(), 0, default, scenario: EcologicalScenario.Natural) { ActiveCenter = founder.Position };
        }

        static int DivideAndFollow(VolumeWorld world, ColonyChallenge attempt, BirthConnection path)
        {
            var player = new CellLineagePlay();
            Assert.IsTrue(player.TakeControl(world, 0));
            Assert.IsTrue(attempt.Begin(world, 0));
            Assert.IsTrue(world.PlanConnection(path));
            Assert.IsTrue(attempt.RequestDivision(world), world.DivisionBlock(0));
            world.Step();
            attempt.RecordStep(world);
            Assert.AreEqual(path, attempt.Path);
            Assert.AreEqual(ColonyChallengeState.Settling, attempt.State);
            Assert.AreEqual(0, attempt.ResidenceSeconds);
            int child = attempt.OffspringToFollow(world, player.Identity);
            Assert.GreaterOrEqual(child, 0);
            Assert.IsTrue(LineageChallenge.VerifiedChild(world.Get(0), world.Get(child)));
            Assert.IsTrue(player.FollowOffspring(world, child));
            world.ControlColony = path == BirthConnection.Retain;
            return child;
        }

        static void FeedForStep(VolumeWorld world, ColonyChallenge attempt, int child)
        {
            var toFood = world.GetFood(0).Position - world.Get(child).Position;
            world.Input = toFood.Length > .45f ? toFood.Unit * .35f : default;
            world.Step();
            attempt.RecordStep(world);
        }

        [TestCase(BirthConnection.Separate)]
        [TestCase(BirthConnection.Retain)]
        public void BothPaidBirthPathsCanSustainTheSameFifteenSeconds(BirthConnection path)
        {
            var world = Ready();
            var attempt = new ColonyChallenge();
            int child = DivideAndFollow(world, attempt, path);
            float contact = path == BirthConnection.Retain ? 2 * Multicellularity.BondCost : 0;
            Assert.AreEqual(contact, world.Get(0).Biology.Budget.Contact, 1e-6);
            Assert.Greater(world.Get(0).Biology.Budget.Division, 0);
            Assert.Less(world.Get(0).MineralReserve, .75f);
            Assert.AreEqual(path == BirthConnection.Retain ? 0 : -1, world.BondParent(child));

            for (int tick = 0; tick < 460 && !attempt.Finished; tick++) {
                FeedForStep(world, attempt, child);
                double elapsed = attempt.ResidenceSeconds;
                attempt.RecordStep(world);
                Assert.AreEqual(elapsed, attempt.ResidenceSeconds, "The same simulation tick cannot advance the clock twice.");
                if (elapsed < LineageChallenge.RequiredNicheSeconds)
                    Assert.AreNotEqual(ColonyChallengeState.Won, attempt.State);
            }

            Assert.AreEqual(ColonyChallengeState.Won, attempt.State);
            Assert.AreEqual(LineageChallenge.RequiredNicheSeconds, attempt.ResidenceSeconds, .0001);
            Assert.IsTrue(world.Get(child).Alive);
            if (path == BirthConnection.Retain) Assert.Greater(attempt.EnergyReceived, 0);
            else Assert.AreEqual(0, attempt.EnergyReceived);

            Assert.AreEqual(path == BirthConnection.Separate, attempt.Begin(world, child));
            attempt.Stop();
            Assert.IsFalse(attempt.Active);
            Assert.AreEqual(BirthConnection.Natural, attempt.Path);
            Assert.AreEqual(0, attempt.ResidenceSeconds);
        }

        [Test]
        public void SeparateOffspringKeepsSettlingWhenItStartsItsOwnPair()
        {
            var ocean = new ChunkOcean(42, new MemoryRegionArchive(), EcologicalScenario.Natural);
            var world = ocean.World;
            int founder = ocean.Find("-1_1_-2/agent/1");
            var attempt = new ColonyChallenge();
            var player = new CellLineagePlay();
            Assert.IsTrue(attempt.Begin(world, founder));
            Assert.IsTrue(player.TakeControl(world, founder));
            Assert.IsTrue(world.PlanConnection(BirthConnection.Separate));
            string offspringIdentity = null;

            for (int tick = 0; tick < 360; tick++) {
                var position = world.Get(world.Controlled).Position;
                ocean.Stream(position + new V3(0, 1, -4));
                world = ocean.World;
                int food = 0;
                while (food < world.FoodCount && world.GetFood(food).Identity != "-1_1_-2/food/1") food++;
                Assert.Less(food, world.FoodCount);
                var delta = world.GetFood(food).Position - position;
                world.Input = delta.Length > .45f ? delta.Unit * Math.Max(.20f, Math.Min(.60f, .15f + .22f * delta.Length)) : default;
                if (world.Tick == 86) Assert.IsTrue(attempt.RequestDivision(world), world.DivisionBlock(world.Controlled));

                world.Step();
                player.RecordStep(world);
                double previousResidence = attempt.ResidenceSeconds;
                attempt.RecordStep(world);
                if (offspringIdentity == null && attempt.State == ColonyChallengeState.Settling) {
                    int offspring = attempt.OffspringToFollow(world, player.Identity);
                    Assert.GreaterOrEqual(offspring, 0);
                    Assert.AreEqual(BirthConnection.Separate, attempt.Path);
                    Assert.Greater(world.Get(world.Controlled).Biology.Budget.Division, 0);
                    Assert.AreEqual(1, world.OrganismSize(offspring));
                    offspringIdentity = world.Get(offspring).Identity;
                }

                if (offspringIdentity == null) continue;
                int index = ocean.Find(offspringIdentity);
                Assert.GreaterOrEqual(index, 0);
                if (world.OrganismSize(index) == 1) continue;
                Assert.AreEqual(index, world.OrganismRoot(index));
                Assert.Greater(previousResidence, 10);
                Assert.AreEqual(previousResidence + VolumeWorld.StepSeconds, attempt.ResidenceSeconds, .0001);
                Assert.AreEqual(ColonyChallengeState.Settling, attempt.State);
                Assert.IsTrue(player.FollowOffspring(world, attempt.OffspringToFollow(world, player.Identity)));
                Assert.AreEqual(offspringIdentity, player.Identity);
                return;
            }
            Assert.Fail("Expected the paid separate offspring to start a pair while settling in Natural seed 42.");
        }

        [Test]
        public void RetainedPairMustExchangeEnergyDuringItsCreditedWindow()
        {
            var world = Ready(transport: 0);
            var attempt = new ColonyChallenge();
            int child = DivideAndFollow(world, attempt, BirthConnection.Retain);
            double longest = 0;
            for (int tick = 0; tick < 380 && attempt.Guidance != "Для пары нужен обмен энергией"; tick++) {
                FeedForStep(world, attempt, child);
                longest = Math.Max(longest, attempt.ResidenceSeconds);
            }
            Assert.Greater(longest, 14.9);
            Assert.AreEqual(ColonyChallengeState.Settling, attempt.State);
            Assert.AreEqual(0, attempt.ResidenceSeconds);
            Assert.AreEqual(0, attempt.EnergyReceived);
            Assert.AreEqual(0, world.BondParent(child));
            Assert.IsTrue(world.Get(child).Alive);
        }

        [Test]
        public void ConfirmedBreakEndsTheRetainedAttemptWithItsOwnCause()
        {
            var world = Ready();
            var attempt = new ColonyChallenge();
            int child = DivideAndFollow(world, attempt, BirthConnection.Retain);
            FeedForStep(world, attempt, child);
            Assert.Greater(attempt.ResidenceSeconds, 0);
            Assert.IsTrue(world.Detach(child));
            FeedForStep(world, attempt, child);
            Assert.AreEqual(ColonyChallengeState.Lost, attempt.State);
            Assert.AreEqual(ColonyChallengeFailure.BondBroken, attempt.Failure);
            Assert.IsTrue(world.Get(child).Alive);
            Assert.AreEqual(-1, world.BondParent(child));
        }

        [Test]
        public void FailedPlayerRequestCannotClaimALaterNpcBirth()
        {
            var world = Ready(24 * .85f, withFood: false);
            world.Controlled = 0;
            var attempt = new ColonyChallenge();
            Assert.IsTrue(attempt.Begin(world, 0));
            Assert.IsFalse(attempt.RequestDivision(world), "The player has not chosen which birth to invest in.");
            Assert.IsTrue(world.PlanConnection(BirthConnection.Separate));
            Assert.IsTrue(attempt.RequestDivision(world), world.DivisionBlock(0));
            world.Step();
            attempt.RecordStep(world);
            Assert.AreEqual(0, world.Births, "Maintenance spends enough energy to deny this queued birth.");
            Assert.AreEqual(BirthConnection.Natural, attempt.Path);

            var reloadedParent = world.Get(0);
            reloadedParent.Biology.Energy = 24;
            var observed = new VolumeWorld(new[] { reloadedParent }, Array.Empty<FoodCloud>(), Array.Empty<V3>(),
                world.Tick, default, scenario: EcologicalScenario.Natural) { Life = world.Life };
            observed.Step();
            attempt.RecordStep(observed);
            Assert.AreEqual(1, observed.Births);
            Assert.AreEqual(ColonyChallengeState.AwaitingDivision, attempt.State);
            Assert.AreEqual(BirthConnection.Natural, attempt.Path);
        }
    }
}
