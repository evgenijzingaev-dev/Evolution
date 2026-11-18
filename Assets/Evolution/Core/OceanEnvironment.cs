using System;

namespace Evolution.Core
{
    public enum OceanZone { Illuminated, Deep, NutrientRich, Hydrothermal, Hazardous }
    public struct OceanSample
    {
        public float Temperature, Fertility, CurrentX, CurrentZ, Light, Toxicity;
        public OceanZone Zone;
    }
    // Smooth periodic zones share the world's toroidal boundaries. No PRNG or engine state.
    public static class OceanEnvironment
    {
        public static OceanSample Sample(float x, float z, float world, float time, float temperature, bool enabled)
        {
            if (!enabled) return new OceanSample { Temperature = temperature, Fertility = 1, Light = 1 };
            double a = x / world * Math.PI * 2, b = z / world * Math.PI * 2, phase = time * .025;
            float light = .08f + .92f * (float)(.5 + .5 * Math.Sin(b));
            float vent = (float)Math.Pow(.5 + .5 * Math.Cos(a + b), 12);
            float toxin = (float)Math.Pow(.5 + .5 * Math.Sin(a - b + phase * .3), 10);
            float fertility = .3f + (float)(1.2 * (.5 + .5 * Math.Cos(a - b)));
            return new OceanSample {
                Light = light, Toxicity = toxin * .8f,
                Zone = toxin > .65f ? OceanZone.Hazardous : vent > .65f ? OceanZone.Hydrothermal
                    : light < .3f ? OceanZone.Deep : fertility > 1.2f ? OceanZone.NutrientRich : OceanZone.Illuminated,
                Temperature = temperature + (float)(6 * Math.Sin(a + phase) * Math.Cos(b)),
                Fertility = fertility,
                CurrentX = (float)(.18 * Math.Sin(a) * Math.Cos(b + phase)),
                CurrentZ = (float)(-.18 * Math.Cos(a) * Math.Sin(b + phase))
            };
        }
    }
}
