using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class OffspringCycleTests
    {
        static VolumeAgent Founder() => new VolumeAgent {
            Position = new V3(100, 100, 100), Direction = new V3(1, 0, 0), Alive = true,
            Biology = new Cell {
                Genes = new Genome { Size = 1, Speed = .9f, Uptake = 1, PreferredTemperature = 22, Photosynthesis = .58f, OrganicSpecialization = .3f },
                Energy = 24, Age = 6, Growth = 1, Integrity = 1 } };
        static VolumeWorld Ready(float energy = 24)
            => new VolumeWorld(1, 42, new[] {
                new VolumeAgent {
                    Position = new V3(100, 100, 100), Direction = new V3(1, 0, 0),
                    Biology = new Cell {
                        Genes = new Genome { Size = 1, Speed = .9f, Uptake = 1, PreferredTemperature = 22, Photosynthesis = .58f, OrganicSpecialization = .3f },
                        Energy = energy, Age = 6, Growth = 1, Integrity = 1 } } }) { Controlled = 0, ActiveCenter = new V3(100, 100, 100) };
        // Hand-built verified genealogy, the same pattern the lineage tests use: no
        // biology is changed, the two descendants really descend from the played cell.
        static VolumeWorld FamilyOfThree()
        {
            var parent = Founder();
            parent.Identity = "local-parent";
            parent.Biology.Id = 1;
            var first = Founder();
            first.Identity = parent.Identity + "/b1";
            first.DivisionChild = true;
            first.Biology.Id = 2;
            first.Biology.ParentId = parent.Biology.Id;
            first.Biology.LineageId = parent.Biology.LineageId;
            first.Biology.Generation = parent.Biology.Generation + 1;
            var second = Founder();
            second.Identity = parent.Identity + "/b2";
            second.DivisionChild = true;
            second.Biology.Id = 3;
            second.Biology.ParentId = parent.Biology.Id;
            second.Biology.LineageId = parent.Biology.LineageId;
            second.Biology.Generation = parent.Biology.Generation + 1;
            return new VolumeWorld(new[] { parent, first, second }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 0, default) { Controlled = 0, ActiveCenter = parent.Position };
        }
        [Test]
        public void YCyclesThroughVerifiedChildrenAndReturnsToTheFirst()
        {
            var world = FamilyOfThree();
            var play = new CellLineagePlay();
            play.TakeControl(world, 0);
            var children = play.VerifiedChildren(world);
            Assert.AreEqual(2, children.Count, "Both descendants are verified direct children.");
            Assert.IsTrue(play.FollowOffspring(world));
            int firstPick = world.Controlled;
            Assert.Contains(firstPick, children);
            // Back to the founder: Y resumes after the last pick, not from the start.
            play.TakeControl(world, 0);
            Assert.IsTrue(play.FollowOffspring(world));
            int secondPick = world.Controlled;
            Assert.AreNotEqual(firstPick, secondPick, "The second Y must reach the other descendant.");
            Assert.Contains(secondPick, children);
            play.TakeControl(world, 0);
            Assert.IsTrue(play.FollowOffspring(world));
            Assert.AreEqual(firstPick, world.Controlled, "The cycle wraps around to the first pick.");
        }
        [Test]
        public void ExplicitIndexStillWorksAndStrangersAreRejected()
        {
            var world = Ready();
            var play = new CellLineagePlay();
            play.TakeControl(world, 0);
            world.RequestDivision();
            world.Step();
            int child = world.Controlled == 0 ? 1 : 0;
            Assert.IsFalse(play.FollowOffspring(world, 0), "The played parent is not its own offspring.");
            Assert.IsTrue(play.FollowOffspring(world, child));
            Assert.AreEqual(child, world.Controlled);
        }
        [Test]
        public void AChildlessCellHasNothingToCycleThrough()
        {
            var world = Ready();
            var play = new CellLineagePlay();
            play.TakeControl(world, 0);
            Assert.IsFalse(play.FollowOffspring(world));
            Assert.IsFalse(play.FollowOffspring(null));
            Assert.AreEqual(0, world.Controlled);
        }
    }
}
