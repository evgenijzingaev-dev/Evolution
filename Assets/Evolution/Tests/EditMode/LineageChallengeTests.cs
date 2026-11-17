using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LineageChallengeTests
    {
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
        public void ConfirmedDeadMemberCannotBeResurrectedByStaleStreamedSnapshot()
        {
            Assert.IsTrue(LineageChallenge.CountsAsLiving(true, false));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(false, false));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(false, true));
            Assert.IsFalse(LineageChallenge.CountsAsLiving(true, true),
                "A stale live snapshot must not restore a confirmed dead identity.");
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
