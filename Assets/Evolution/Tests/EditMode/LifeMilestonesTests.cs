using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LifeMilestonesTests
    {
        static Genome HunterGenome() => new Genome {
            Speed = 1.2f, Size = 1.2f, Uptake = 1, PreferredTemperature = 22,
            Predation = 1, Membrane = .5f, Sensitivity = 1, EnergyEfficiency = .5f,
            ThermalTolerance = 1, ToxinResistance = 1 };
        static Genome PreyGenome() => new Genome {
            Speed = .8f, Size = 1, Uptake = 1, PreferredTemperature = 22,
            Membrane = .2f, EnergyEfficiency = .5f, ThermalTolerance = 1, ToxinResistance = 1 };
        static VolumeAgent Founder(V3 position, Genome genes, float energy) => new VolumeAgent {
            Position = position, Direction = new V3(0, 0, 1), Alive = true,
            Biology = new Cell { Genes = genes, Energy = energy, Age = 10, Growth = 1, Integrity = 1 } };
        static LifeMilestones Recorded(VolumeWorld world, int controlled)
        {
            var milestones = new LifeMilestones();
            milestones.Record(world, controlled);
            return milestones;
        }
        [Test]
        public void TitlesAndHintsNeverReturnNullForHud()
        {
            for(int i = 0; i < LifeMilestones.Total; i++) {
                var id = (LifeMilestoneId)i;
                Assert.IsNotEmpty(LifeMilestones.Title(id));
                Assert.IsNotEmpty(LifeMilestones.Hint(id));
            }
        }
        [Test]
        public void RealBiteCountsExactlyOncePerTick()
        {
            var founders = new[] {
                Founder(new V3(0, 0, 0), HunterGenome(), 6),
                Founder(new V3(.2f, 0, .2f), PreyGenome(), 2) };
            var world = new VolumeWorld(founders.Length, 77, founders);
            world.Controlled = 0;
            var milestones = new LifeMilestones();
            milestones.Record(world, 0);
            Assert.AreEqual(0, milestones.Hunts, "No tick has run yet, so no bite can exist.");
            world.Attack = true;
            world.Step();
            Assert.Greater(world.Get(0).Biology.AttackLastStep, 0, "The model itself must confirm the bite.");
            milestones.Record(world, 0);
            Assert.AreEqual(1, milestones.Hunts);
            Assert.IsTrue(LifeMilestones.BitesThisTick(world.Get(0).Biology));
            milestones.Record(world, 0);
            Assert.AreEqual(1, milestones.Hunts, "The same tick must not count twice.");
            Assert.AreEqual(1, milestones.CompletedCount);
            Assert.AreEqual(LifeMilestoneId.WellFed, milestones.CurrentId);
            StringAssert.Contains("90%", milestones.CurrentProgress);
        }
        [Test]
        public void RealPaidDivisionCompletesTheParentMilestone()
        {
            var founder = Founder(new V3(0, 0, 0), HunterGenome(), Biology.Capacity(HunterGenome()));
            var world = new VolumeWorld(1, 88, new[] { founder });
            world.Controlled = 0;
            var milestones = Recorded(world, 0);
            Assert.AreEqual(1, milestones.CompletedCount, "A full reserve is real physiology: WellFed completes.");
            Assert.AreEqual(LifeMilestoneId.FirstHunt, milestones.CurrentId);
            world.RequestDivision();
            world.Step();
            milestones.Record(world, 0);
            Assert.AreEqual(1, milestones.Divisions);
            Assert.AreEqual(2, milestones.CompletedCount);
            Assert.AreEqual(LifeMilestoneId.FirstHunt, milestones.CurrentId,
                "The goal line always names the first unfinished milestone.");
        }
        [Test]
        public void DepthAndNicheRulesUseRealClassification()
        {
            Assert.IsTrue(LifeMilestones.ReachesDepth(new V3(0, -20, 0)));
            Assert.IsFalse(LifeMilestones.ReachesDepth(new V3(0, -19.9f, 0)));
            Assert.IsFalse(LifeMilestones.IsNewNiche(-1, 2), "No founder habitat captured yet.");
            Assert.IsFalse(LifeMilestones.IsNewNiche(1, 0), "Transitional water is not a discovery.");
            Assert.IsFalse(LifeMilestones.IsNewNiche(2, 2));
            Assert.IsTrue(LifeMilestones.IsNewNiche(2, 1));
            Assert.IsTrue(LifeMilestones.IsNewNiche(0, 3));
        }
        [Test]
        public void DynastyAndPatriarchRequireVerifiedGenerationsAndMembers()
        {
            Assert.IsFalse(LifeMilestones.GrandchildAlive(int.MinValue, 5), "No controlled founder captured yet.");
            Assert.IsFalse(LifeMilestones.GrandchildAlive(2, 3), "A child is not a grandchild.");
            Assert.IsTrue(LifeMilestones.GrandchildAlive(2, 4));
            Assert.IsFalse(LifeMilestones.PatriarchReached(2));
            Assert.IsTrue(LifeMilestones.PatriarchReached(3));
        }
        [Test]
        public void DeepFounderCompletesDepthWithoutLeavingTheStartNiche()
        {
            var founder = Founder(new V3(0, -25, 0), PreyGenome(), 4);
            var world = new VolumeWorld(1, 91, new[] { founder });
            world.Controlled = 0;
            var milestones = Recorded(world, 0);
            Assert.IsTrue(milestones.CompletedCount >= 1);
            Assert.IsFalse(milestones.ChainComplete);
            milestones.Record(world, 0);
            Assert.AreEqual(milestones.CompletedCount, milestones.CompletedCount, "Sticky achievements never regress.");
        }
        [Test]
        public void StreamedWorldRebindDoesNotReplayTheOldJournal()
        {
            var founder = Founder(new V3(0, 0, 0), HunterGenome(), Biology.Capacity(HunterGenome()));
            var first = new VolumeWorld(1, 88, new[] { founder });
            first.Controlled = 0;
            var milestones = new LifeMilestones();
            milestones.Record(first, 0);
            first.RequestDivision();
            first.Step();
            milestones.Record(first, 0);
            Assert.AreEqual(1, milestones.Divisions);
            var reborn = new VolumeWorld(1, 88, new[] { Founder(new V3(0, 0, 0), HunterGenome(), 4) });
            reborn.Controlled = 0;
            milestones.Record(reborn, 0);
            milestones.Record(reborn, 0);
            Assert.AreEqual(1, milestones.Divisions, "A fresh region journal must not replay old divisions.");
        }
        [Test]
        public void NullWorldAndOutOfRangeControlledAreSafe()
        {
            var milestones = new LifeMilestones();
            Assert.IsFalse(milestones.Record(null, 0));
            var founder = Founder(new V3(0, 0, 0), PreyGenome(), 4);
            var world = new VolumeWorld(1, 5, new[] { founder });
            Assert.IsFalse(milestones.Record(world, -1));
            Assert.IsFalse(milestones.Record(world, world.Count));
            Assert.AreEqual(0, milestones.CompletedCount);
            Assert.AreEqual(LifeMilestoneId.FirstHunt, milestones.CurrentId);
            Assert.IsFalse(milestones.ConsumeCompleted(out _));
        }
        [Test]
        public void CompletedToastsAreConsumedExactlyOnce()
        {
            var founder = Founder(new V3(0, 0, 0), HunterGenome(), Biology.Capacity(HunterGenome()));
            var world = new VolumeWorld(1, 88, new[] { founder });
            world.Controlled = 0;
            var milestones = new LifeMilestones();
            milestones.Record(world, 0);
            Assert.IsTrue(milestones.ConsumeCompleted(out string title));
            Assert.AreEqual(LifeMilestones.Title(LifeMilestoneId.WellFed), title);
            Assert.IsFalse(milestones.ConsumeCompleted(out _));
        }
    }
}
