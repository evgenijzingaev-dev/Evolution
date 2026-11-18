using Evolution.Presentation;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class WaterOpticsTests
    {
        [Test] public void SpatialProbeOpacityIsFullNearbyAndNeverNegativeOutsideItsShell()
        {
            Assert.AreEqual(1,1-WaterOptics.SmoothRange(14,18,0));
            Assert.AreEqual(1,1-WaterOptics.SmoothRange(14,18,14));
            Assert.AreEqual(.5f,1-WaterOptics.SmoothRange(14,18,16));
            Assert.AreEqual(0,1-WaterOptics.SmoothRange(14,18,18));
            Assert.AreEqual(0,1-WaterOptics.SmoothRange(14,18,100));
        }
        [Test] public void AmbientParticlesRetainOpacityUntilTheRecyclingShell()
        {
            Assert.AreEqual(1,1-WaterOptics.SmoothRange(.75f,1,.2f));
            Assert.AreEqual(1,1-WaterOptics.SmoothRange(.75f,1,.75f));
            Assert.AreEqual(.5f,1-WaterOptics.SmoothRange(.75f,1,.875f));
            Assert.AreEqual(0,1-WaterOptics.SmoothRange(.75f,1,1.1f));
        }
        [Test] public void TracersDisappearCompletelyAtBothEndsOfTheirAdvectionCycle()
        {
            foreach(float phase in new[] {0f,1f})
                Assert.AreEqual(0,WaterOptics.SmoothRange(0,.15f,phase)*(1-WaterOptics.SmoothRange(.8f,1,phase)));
            Assert.AreEqual(1,WaterOptics.SmoothRange(0,.15f,.5f)*(1-WaterOptics.SmoothRange(.8f,1,.5f)));
        }
    }
}
