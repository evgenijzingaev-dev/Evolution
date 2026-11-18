using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
using System.Collections.Generic;

namespace Evolution.Tests
{
    public sealed class DynastyGoalTests
    {
        static VolumeAgent Founder(float energy = 24, float x = 10, string identity = "local-agent-0") => new VolumeAgent {
            Position = new V3(x, 10, 10), Direction = new V3(1, 0, 0), Alive = true, Identity = identity,
            Biology = new Cell {
                Genes = new Genome { Size = 1, Speed = .9f, Uptake = 1, PreferredTemperature = 22, Photosynthesis = .58f, OrganicSpecialization = .3f },
                Energy = energy, Age = 6, Growth = 1, Integrity = 1 } };
        [Test]
        public void CoveredGenerationsNeedEveryGenerationAfterTheFounder()
        {
            var observed = new HashSet<int> { 0, 1, 2, 3, 4 };
            Assert.AreEqual(5, DynastyGoal.CoveredGenerations(observed, 0));
            Assert.AreEqual(3, DynastyGoal.CoveredGenerations(new HashSet<int> { 0, 2, 4 }, 0), "Gaps are not a continuous dynasty.");
            Assert.AreEqual(5, DynastyGoal.CoveredGenerations(new HashSet<int> { 7, 8, 9, 10, 11 }, 7));
            Assert.AreEqual(0, DynastyGoal.CoveredGenerations(new HashSet<int> { 3 }, int.MinValue), "No founder captured yet.");
            Assert.AreEqual(0, DynastyGoal.CoveredGenerations(null, 0));
        }
        [Test]
        public void CompletionGatesEveryConditionTogether()
        {
            Assert.IsFalse(DynastyGoal.IsComplete(false, 5, 3, true), "The life path must be finished first.");
            Assert.IsFalse(DynastyGoal.IsComplete(true, 4, 3, true));
            Assert.IsFalse(DynastyGoal.IsComplete(true, 5, 2, true));
            Assert.IsFalse(DynastyGoal.IsComplete(true, 5, 3, false));
            Assert.IsTrue(DynastyGoal.IsComplete(true, 5, 3, true));
        }
        [Test]
        public void CollapseNeedsAFreshZeroObservationWhileTheLineSurvivesElsewhere()
        {
            var founder = Founder();
            founder.Biology.Id = 1;
            var neighbour = Founder(24, 12, "local-neighbour");
            var survivor = Founder(24, 40, "local-agent-0/b1");
            survivor.Identity = founder.Identity + "/b1";
            survivor.DivisionChild = true;
            survivor.Biology.Id = 2;
            survivor.Biology.ParentId = founder.Biology.Id;
            survivor.Biology.LineageId = founder.Biology.LineageId;
            survivor.Biology.Generation = founder.Biology.Generation + 1;
            var world = new VolumeWorld(new[] { founder, neighbour, survivor }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 0, default) { Controlled = 0 };
            var tree = new LineageTree();
            tree.Record(world, 0);
            Assert.AreEqual(2, tree.Count, "The survivor is adopted by verified genealogy.");
            var survey = new OceanSurvey();
            var goal = new DynastyGoal();
            var loaded = new List<ChunkKey> { ChunkKey.At(founder.Position), ChunkKey.At(survivor.Position) };
            var food = new List<FoodCloud>();
            for (int i = 0; i < world.FoodCount; i++) food.Add(world.GetFood(i));
            survey.Observe(new List<VolumeAgent> { world.Get(0), world.Get(1), world.Get(2) }, food, loaded, world.Tick, default);
            goal.Record(world, 0, false, survey, tree);
            Assert.IsFalse(goal.CollapseSeen);
            Assert.IsFalse(goal.Completed);
            // Both members of the crowded region die; the survivor lives elsewhere.
            var dead1 = world.Get(0); dead1.Alive = false;
            var dead2 = world.Get(1); dead2.Alive = false;
            var after = new VolumeWorld(new[] { dead1, dead2, world.Get(2) }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick + 1, default);
            tree.Record(after, -1);
            survey.Observe(new List<VolumeAgent> { after.Get(0), after.Get(1), after.Get(2) }, food, loaded, after.Tick, default);
            goal.Record(after, -1, false, survey, tree);
            Assert.IsTrue(goal.CollapseSeen, "A fresh zero observation after a peak of two is a neighbourhood extinction.");
            StringAssert.Contains("пережито", goal.Progress);
            Assert.IsFalse(goal.Completed, "Two generations and no finished life path cannot complete the goal.");
            StringAssert.Contains("2/5", goal.Progress);
        }
        [Test]
        public void UnloadedOrUnobservedRegionsProveNothing()
        {
            var founder = Founder();
            var world = new VolumeWorld(1, 42, new[] { founder }) { Controlled = 0, ActiveCenter = founder.Position };
            var tree = new LineageTree();
            tree.Record(world, 0);
            var goal = new DynastyGoal();
            // No survey observations at all: no collapse may be invented.
            goal.Record(world, 0, true, new OceanSurvey(), tree);
            Assert.IsFalse(goal.CollapseSeen);
            Assert.IsFalse(goal.Completed);
            goal.Record(world, 0, true, null, tree);
            Assert.IsFalse(goal.Completed);
        }
        [Test]
        public void CompletionEmitsOneToastAndStops()
        {
            var goal = new DynastyGoal();
            Assert.IsFalse(goal.ConsumeCompleted(out _));
            Assert.IsFalse(goal.Record(null, 0, true, null, null));
        }
    }
}
