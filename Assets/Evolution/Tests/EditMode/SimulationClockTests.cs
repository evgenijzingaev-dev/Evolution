using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class SimulationClockTests
    {
        // Delta times stay far from tick boundaries so assertions never depend on
        // the last double rounding bit of VolumeWorld.StepSeconds.
        [Test]
        public void FixedStepAccumulatesAndDrainsExactly()
        {
            var clock = new SimulationClock();
            Assert.AreEqual(1f, clock.Scale);
            // 0.21s at pace ×1 is 4.2 ticks: safely inside the one-frame budget of 8.
            Assert.AreEqual(4, clock.Advance(.21, false));
            Assert.AreEqual(0, clock.Advance(0, false), "A partial tick must wait for more time.");
            Assert.AreEqual(4, clock.Advance(.21, false), "Leftover fractional time still counts.");
        }
        [Test]
        public void ScaleMultipliesSimulationPaceOnly()
        {
            var clock = new SimulationClock();
            Assert.IsTrue(clock.SetScale(1));
            Assert.AreEqual(4, clock.Advance(.102, false));
            Assert.IsTrue(clock.SetScale(2));
            Assert.AreEqual(8, clock.Advance(.102, false));
            Assert.IsTrue(clock.SetScale(3));
            Assert.AreEqual(16, clock.Advance(.102, false));
        }
        [Test]
        public void BudgetCapsOneFrameBurstAndRetainsBacklog()
        {
            var clock = new SimulationClock();
            Assert.AreEqual(8, clock.Advance(1.0, false), "One frame never runs the whole backlog.");
            Assert.AreEqual(8, clock.Advance(0, false));
            Assert.AreEqual(3, clock.Advance(0, false));
            Assert.AreEqual(0, clock.Advance(0, false), "Backlog must drain to a true zero.");
        }
        [Test]
        public void PauseStopsAccumulationAndNeverDumpsStalledTime()
        {
            var clock = new SimulationClock();
            Assert.AreEqual(0, clock.Advance(1.0, true));
            Assert.AreEqual(0, clock.Advance(1.0, true));
            Assert.AreEqual(0, clock.Advance(0, false), "Paused wall time must not create a step burst on resume.");
            Assert.AreEqual(4, clock.Advance(.21, false));
        }
        [Test]
        public void TotalStepsDoNotDependOnFrameSplitting()
        {
            var split = new SimulationClock();
            int total = 0;
            for(int frame = 0; frame < 5; frame++) total += split.Advance(.103, false);
            var single = new SimulationClock();
            int burst = single.Advance(.515, false);
            for(int more = single.Advance(0, false); more > 0; more = single.Advance(0, false)) burst += more;
            Assert.AreEqual(10, total);
            Assert.AreEqual(total, burst, "After the backlog drains, total ticks match regardless of frame splitting.");
        }
        [Test]
        public void SpeedKeysAreBoundedAndClamped()
        {
            var clock = new SimulationClock();
            Assert.IsFalse(clock.Slower(), "Already at the slowest pace.");
            for(int i = 0; i < 3; i++) Assert.IsTrue(clock.Faster());
            Assert.IsFalse(clock.Faster(), "Already at the fastest pace.");
            Assert.AreEqual(8f, clock.Scale);
            Assert.IsTrue(clock.Slower());
            Assert.AreEqual(4f, clock.Scale);
            Assert.IsFalse(clock.SetScale(2), "The same scale changes nothing.");
            Assert.IsTrue(clock.SetScale(-7), "Out-of-range input clamps to the slowest pace.");
            Assert.AreEqual(1f, clock.Scale);
            Assert.IsTrue(clock.SetScale(99), "Out-of-range input clamps to the fastest pace.");
            Assert.AreEqual(8f, clock.Scale);
        }
        [Test]
        public void BlendStaysInsideOneTickForRendering()
        {
            var clock = new SimulationClock();
            Assert.AreEqual(0, clock.Blend, 1e-9);
            clock.Advance(.03, false);
            Assert.GreaterOrEqual(clock.Blend, 0);
            Assert.LessOrEqual(clock.Blend, 1);
            clock.Advance(2.0, false);
            Assert.LessOrEqual(clock.Blend, 1, "A huge frame burst must not overshoot interpolation.");
            clock.Reset();
            Assert.AreEqual(0, clock.Advance(0, false));
        }
    }
}
