using System;

namespace Evolution.Core
{
    public struct OceanSample
    {
        public float Temperature, Fertility, CurrentX, CurrentZ;
    }
    // Smooth periodic zones share the world's toroidal boundaries. No PRNG or engine state.
    public static class OceanEnvironment
    {
        public static OceanSample Sample(float x, float z, float world, float time, float temperature, bool enabled)
        {
            if (!enabled) return new OceanSample { Temperature = temperature, Fertility = 1 };
            double a = x / world * Math.PI * 2, b = z / world * Math.PI * 2, phase = time * .025;
            return new OceanSample {
                Temperature = temperature + (float)(6 * Math.Sin(a + phase) * Math.Cos(b)),
                Fertility = .3f + (float)(1.2 * (.5 + .5 * Math.Cos(a - b))),
                CurrentX = (float)(.18 * Math.Sin(a) * Math.Cos(b + phase)),
                CurrentZ = (float)(-.18 * Math.Cos(a) * Math.Sin(b + phase))
            };
        }
    }
}
