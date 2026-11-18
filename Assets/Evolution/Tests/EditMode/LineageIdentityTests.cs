using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LineageIdentityTests
    {
        static VolumeAgent Founder(float energy = 24) => new VolumeAgent {
            Position = new V3(100, 100, 100), Direction = new V3(1, 0, 0),
            Biology = new Cell {
                Genes = new Genome { Size = 1, Speed = .9f, Uptake = 1, PreferredTemperature = 22, Photosynthesis = .58f, OrganicSpecialization = .3f },
                Energy = energy, Age = 6, Growth = 1, Integrity = 1 } };
        static VolumeWorld Ready(float energy = 24)
            => new VolumeWorld(1, 42, new[] { Founder(energy) }) { Controlled = 0, ActiveCenter = new V3(100, 100, 100) };
        [Test]
        public void NamesAreDeterministicPerIdentityAndNeverConsumeRandomness()
        {
            Assert.AreEqual(LineageName.For("local-agent-0"), LineageName.For("local-agent-0"));
            Assert.IsNotEmpty(LineageName.For("local-agent-0"));
            Assert.IsNull(LineageName.For(null));
            Assert.IsNull(LineageName.For(""));
        }
        [Test]
        public void TreeCapturesFounderAndJournalChildrenInBirthOrder()
        {
            var world = Ready();
            var tree = new LineageTree();
            tree.Record(world, 0);
            Assert.IsTrue(tree.HasFounder);
            Assert.IsNotEmpty(tree.FounderName);
            Assert.AreEqual(1, tree.Count);
            world.RequestDivision();
            world.Step();
            tree.Record(world, 0);
            Assert.AreEqual(2, tree.Count);
            var ordered = tree.Ordered();
            Assert.AreEqual(2, ordered.Count);
            Assert.IsTrue(ordered[0].IsFounder);
            Assert.IsNull(ordered[0].ParentIdentity);
            Assert.AreEqual(ordered[0].Identity, ordered[1].ParentIdentity);
            Assert.AreEqual(ordered[0].Generation + 1, ordered[1].Generation);
            Assert.IsTrue(ordered[1].AliveNow);
            Assert.AreEqual("жив", LineageTree.StatusText(ordered[1]));
        }
        [Test]
        public void StreamedMissingMemberIsUnknownNeverDead()
        {
            var world = Ready();
            var tree = new LineageTree();
            tree.Record(world, 0);
            world.RequestDivision();
            world.Step();
            tree.Record(world, 0);
            // The descendant is streamed away: only the founder stays loaded.
            var rebuilt = new VolumeWorld(new[] { world.Get(0) }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            tree.Record(rebuilt, 0);
            var child = tree.Ordered()[1];
            Assert.IsFalse(child.AliveNow);
            Assert.IsFalse(child.Dead);
            Assert.AreEqual("не наблюдается", LineageTree.StatusText(child));
        }
        [Test]
        public void ConfirmedDeadMemberCannotBeResurrectedByReusedIdentity()
        {
            var world = Ready(.001f);
            var tree = new LineageTree();
            tree.Record(world, 0);
            world.Step();
            tree.Record(world, 0);
            Assert.IsTrue(tree.IsDead(world.Get(0).Identity));
            // A different organism reusing the identity string must stay a stranger.
            var impostor = Founder();
            impostor.Identity = world.Get(0).Identity;
            impostor.Biology.Id = 99;
            var reused = new VolumeWorld(new[] { impostor }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 10, default);
            tree.Record(reused, -1);
            Assert.IsTrue(tree.IsDead(impostor.Identity), "A stale live snapshot must not revive a confirmed death.");
            Assert.IsFalse(tree.Ordered()[0].AliveNow);
        }
        [Test]
        public void ImpostorGenealogyIsNeverAdopted()
        {
            var world = Ready();
            var tree = new LineageTree();
            tree.Record(world, 0);
            var founder = world.Get(0);
            var impostor = Founder();
            impostor.Identity = founder.Identity + "/b123";
            impostor.DivisionChild = true;
            impostor.Biology.ParentId = 999;
            var fraud = new VolumeWorld(new[] { founder, impostor }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 10, default);
            tree.Record(fraud, 0);
            Assert.AreEqual(1, tree.Count, "An identity prefix without biological parentage is not family.");
        }
        [Test]
        public void StreamedDescendantIsAdoptedByVerifiedGenealogy()
        {
            var world = Ready();
            var tree = new LineageTree();
            tree.Record(world, 0);
            var founder = world.Get(0);
            var child = Founder();
            child.Identity = founder.Identity + "/b1";
            child.DivisionChild = true;
            child.Biology.Id = 2;
            child.Biology.ParentId = founder.Biology.Id;
            child.Biology.LineageId = founder.Biology.LineageId;
            child.Biology.Generation = founder.Biology.Generation + 1;
            var streamed = new VolumeWorld(new[] { founder, child }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 20, default);
            tree.Record(streamed, 0);
            Assert.AreEqual(2, tree.Count, "A genealogy-verified streamed child belongs to the tree.");
            Assert.AreEqual(founder.Identity, tree.Ordered()[1].ParentIdentity);
        }
        [Test]
        public void WorldRebindDoesNotDuplicateMembersFromOldJournal()
        {
            var world = Ready();
            var tree = new LineageTree();
            tree.Record(world, 0);
            world.RequestDivision();
            world.Step();
            tree.Record(world, 0);
            Assert.AreEqual(2, tree.Count);
            var rebuilt = new VolumeWorld(new[] { world.Get(0) }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), world.Tick, default);
            tree.Record(rebuilt, 0);
            tree.Record(rebuilt, 0);
            Assert.AreEqual(2, tree.Count, "A fresh region journal must not replay old divisions.");
        }
    }
}
