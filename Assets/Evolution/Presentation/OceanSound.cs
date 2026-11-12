using System;

namespace Evolution.Presentation
{
    // Quiet, deterministic DSP. All ambience frequencies complete integer cycles in 12 seconds.
    public static class OceanSound
    {
        public const float LoopSeconds = 12;
        public static float Ambience(double time, int channel)
        {
            double phase = 2 * Math.PI * time, side = channel == 0 ? 0 : .65;
            double swell = .6 + .16 * Math.Sin(phase / LoopSeconds + side);
            double body = .045 * Math.Sin(phase * 44 + side) + .025 * Math.Sin(phase * 61 - side);
            double water = .012 * Math.Sin(phase * 131 + Math.Sin(phase * 2 / LoopSeconds) * 2 + side)
                + .008 * Math.Sin(phase * 229 + Math.Sin(phase / LoopSeconds) * 3 - side);
            return (float)((body + water) * swell);
        }
        public static float Interaction(double time, double duration, double frequency, double sweep)
        {
            if (time <= 0 || time >= duration) return 0;
            double t = time / duration;
            double envelope = Math.Pow(Math.Sin(Math.PI * t), 2) * Math.Exp(-2.5 * t);
            double phase = 2 * Math.PI * (frequency * time + sweep * time * time * .5);
            return (float)((Math.Sin(phase) + .2 * Math.Sin(phase * 2)) * envelope * .18);
        }
    }
}
