using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LineageChronicleTests
    {
        static VolumeAgent Founder(float energy = 24) => new VolumeAgent {
            Position = new V3(100, 100, 100), Direction = new V3(1, 0, 0),
            Biology = new Cell {
                Genes = new Genome { Size = 1, Speed = .9f, Uptake = 1, PreferredTemperature = 22, Photosynthesis = .58f, OrganicSpecialization = .3f },
                Energy = energy, Age = 6, Growth = 1, Integrity = 1 } };
        static VolumeWorld Ready(float energy = 24)
            => new VolumeWorld(1, 42, new[] { Founder(energy) }) { Controlled = 0, ActiveCenter = new V3(100, 100, 100) };
        [Test]
        public void ExtinctionIsDeclaredOnlyWithVerifiedCauseFromTheJournal()
        {
            var world = Ready(.001f);
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            Assert.IsFalse(chronicle.Finished);
            world.Step();
            Assert.IsTrue(chronicle.Record(world, 0), "A fully confirmed death ends the chronicle this tick.");
            Assert.AreEqual(ChronicleEnd.Extinct, chronicle.Summary.End);
            StringAssert.Contains("прекратилась", chronicle.Summary.EndText);
            StringAssert.Contains("энергии исчерпан", chronicle.Summary.EndText);
            Assert.AreEqual(1, chronicle.Summary.Generations);
            Assert.IsTrue(chronicle.Finished);
        }
        [Test]
        public void PlayerExitWithLivingDescendantsIsNotExtinction()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            world.RequestDivision();
            world.Step();
            chronicle.Record(world, 0);
            Assert.IsFalse(chronicle.Finished);
            Assert.IsTrue(chronicle.FinishByPlayer());
            Assert.AreEqual(ChronicleEnd.PlayerExit, chronicle.Summary.End);
            StringAssert.Contains("Вы вышли из роли", chronicle.Summary.EndText);
            StringAssert.Contains("жива", chronicle.Summary.EndText);
            Assert.AreEqual(1, chronicle.Summary.Divisions);
            // Two generations (founder and child) and one niche at the sunny start.
            Assert.AreEqual(21, chronicle.Summary.Score);
        }
        [Test]
        public void StreamedMissingMembersPreventExtinctionDeclaration()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            world.RequestDivision();
            world.Step();
            chronicle.Record(world, 0);
            // The founder is a confirmed corpse; the descendant is outside the area.
            var corpse = world.Get(0);
            corpse.Alive = false;
            var rebuilt = new VolumeWorld(new[] { corpse }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(rebuilt, -1);
            Assert.IsFalse(chronicle.Finished, "An unobserved descendant is not a verified extinction.");
            Assert.IsFalse(chronicle.ConsumeFinished(out _));
            Assert.IsTrue(chronicle.FinishByPlayer());
            StringAssert.Contains("неизвестен", chronicle.Summary.EndText);
        }
        [Test]
        public void DescendantsJoinTheDynastyThroughTheJournalAndCountGenerations()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            world.RequestDivision();
            world.Step();
            chronicle.Record(world, 0);
            Assert.AreEqual(1, chronicle.Divisions);
            var child = world.Get(1);
            child.Biology.Energy = Biology.Capacity(child.Biology.Genes);
            child.Biology.Age = 6;
            child.Biology.Growth = 1;
            var next = new VolumeWorld(new[] { child }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 200, default) { Controlled = 0, ActiveCenter = child.Position };
            chronicle.Record(next, 0);
            next.RequestDivision();
            next.Step();
            chronicle.Record(next, 0);
            Assert.AreEqual(2, chronicle.Divisions);
            Assert.AreEqual(2, chronicle.DeepestGeneration, "Founder 0, child 1, grandchild 2.");
            Assert.IsFalse(chronicle.Finished);
        }
        [Test]
        public void ConfirmedDeadFounderCannotAppearAliveAfterStreamingRebind()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            world.RequestDivision();
            world.Step();
            chronicle.Record(world, 0);
            Assert.AreEqual(1, chronicle.Divisions);

            var corpse = world.Get(0);
            corpse.Alive = false;
            var child = world.Get(1);
            var confirmed = new VolumeWorld(new[] { corpse, child },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(confirmed, -1);

            // A stale loaded copy reports the dead founder as alive, while the
            // genuine descendant is temporarily outside the streamed region.
            corpse.Alive = true;
            var stale = new VolumeWorld(new[] { corpse },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(stale, -1);
            Assert.IsFalse(chronicle.Finished, "Missing offspring must not be declared extinct.");
            Assert.IsTrue(chronicle.FinishByPlayer());
            StringAssert.Contains("неизвестен", chronicle.Summary.EndText);
            StringAssert.DoesNotContain("Линия жива:", chronicle.Summary.EndText,
                "Confirmed dead founder must not count as a living dynasty member.");
        }
        [Test]
        public void SwitchingControlToUnrelatedCellDoesNotAdoptItIntoDynasty()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            var outsider = Founder();
            outsider.Identity = "unrelated-outsider";
            var switched = new VolumeWorld(new[] { world.Get(0), outsider },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(switched, 1);
            // Only the founder is a dynasty member; an unrelated controlled cell
            // must not prevent extinction or appear as a surviving descendant.
            var founder = world.Get(0);
            founder.Alive = false;
            var afterDeath = new VolumeWorld(new[] { founder, outsider },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            Assert.IsTrue(chronicle.Record(afterDeath, 1));
            Assert.AreEqual(ChronicleEnd.Extinct, chronicle.Summary.End);
            Assert.AreEqual(0, chronicle.Divisions);
        }
        [Test]
        public void RepeatedHudReadsCannotAwardMultipleHuntsInOneTick()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            var founder = world.Get(0);
            founder.Biology.AttackLastStep = 1;
            // VolumeAgent is a value type: Get returns a copy. Rebuild the observed
            // snapshot with the modified agent instead of mutating a detached copy.
            var observed = new VolumeWorld(new[] { founder },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(observed, 0);
            chronicle.Record(observed, 0);
            Assert.AreEqual(1, chronicle.Hunts,
                "Reading the same simulation tick twice must not manufacture hunts.");
            var rebound = new VolumeWorld(new[] { founder },
                System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(rebound, 0);
            Assert.AreEqual(1, chronicle.Hunts,
                "Rebinding the streamed world at the same tick must not replay the hunt.");
        }
        [Test]
        public void WorldRebindDoesNotReplayOldDivisions()
        {
            var world = Ready();
            var chronicle = new LineageChronicle();
            chronicle.Record(world, 0);
            world.RequestDivision();
            world.Step();
            chronicle.Record(world, 0);
            Assert.AreEqual(1, chronicle.Divisions);
            var rebuilt = new VolumeWorld(new[] { world.Get(0) }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            chronicle.Record(rebuilt, 0);
            chronicle.Record(rebuilt, 0);
            Assert.AreEqual(1, chronicle.Divisions, "A fresh region journal must not replay old divisions.");
        }
        [Test]
        public void EraMarkerAndScoreFormulaFollowRealCounters()
        {
            Assert.AreEqual(0, LineageChronicle.Score(0, 0));
            Assert.AreEqual(32, LineageChronicle.Score(3, 2));
            Assert.AreEqual(5, LineageChronicle.Score(-1, 5), "Negative generations cannot subtract points.");
            StringAssert.Contains("хищник", LineageChronicle.CauseText(DeathAppearance.Damage));
            StringAssert.Contains("энергии", LineageChronicle.CauseText(DeathAppearance.Exhaustion));
            var chronicle = new LineageChronicle();
            chronicle.MarkEra();
            chronicle.Record(Ready(), 0);
            Assert.IsTrue(chronicle.FinishByPlayer());
            Assert.IsTrue(chronicle.Summary.Era);
        }
        [Test]
        public void EmptyDynastyAndRepeatFinishesAreRejected()
        {
            var chronicle = new LineageChronicle();
            Assert.IsFalse(chronicle.FinishByPlayer(), "Nothing was ever tracked.");
            var world = Ready();
            chronicle.Record(world, 0);
            Assert.IsTrue(chronicle.FinishByPlayer());
            Assert.IsFalse(chronicle.FinishByPlayer(), "A finished chronicle is terminal.");
            chronicle.Record(world, 0);
            Assert.IsFalse(chronicle.ConsumeFinished(out var summary) && chronicle.ConsumeFinished(out _),
                "The summary is consumed exactly once.");
            Assert.AreEqual(1, summary.Generations);
        }
    }
}
