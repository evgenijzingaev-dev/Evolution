using System;
namespace Evolution.Volume
{
    // Fixed-step time scaling for the volume prototype. The scale only changes how many
    // simulation ticks fit into one rendered frame; the tick sequence for a given seed
    // never depends on frame rate, pause or the selected speed.
    public struct SimulationClock
    {
        public const int BaseStepsPerFrame = 8;
        public static readonly float[] Scales = { 1f, 2f, 4f, 8f };
        // VolumeWorld.StepSeconds is a float constant; the epsilon keeps an exact
        // accumulated boundary from losing one tick to double rounding.
        const double Epsilon = 1e-9;
        double pending;
        int scaleIndex;
        // Rendering interpolation between the last two ticks, 0..1.
        public double Blend => VolumeWorld.StepSeconds <= 0 ? 0 : Math.Min(1, Math.Max(0, pending / VolumeWorld.StepSeconds));
        public int ScaleIndex => scaleIndex;
        public float Scale => Scales[scaleIndex];
        public bool Faster() { if(scaleIndex >= Scales.Length - 1) return false; scaleIndex++; return true; }
        public bool Slower() { if(scaleIndex <= 0) return false; scaleIndex--; return true; }
        public bool SetScale(int index)
        {
            int clamped = Math.Max(0, Math.Min(Scales.Length - 1, index));
            if(clamped == scaleIndex) return false;
            scaleIndex = clamped; return true;
        }
        // Accumulates real time and returns the number of full simulation steps to run
        // this frame. Backlog is retained between frames: a slow frame must never
        // silently discard simulation ticks, the budget only caps one frame's burst.
        public int Advance(double unscaledDeltaTime, bool paused)
        {
            if(paused) return 0;
            if(unscaledDeltaTime > 0 && !double.IsNaN(unscaledDeltaTime) && !double.IsInfinity(unscaledDeltaTime))
                pending += unscaledDeltaTime * Scales[scaleIndex];
            double step = VolumeWorld.StepSeconds;
            int budget = (int)Math.Ceiling(BaseStepsPerFrame * Scales[scaleIndex]);
            int steps = 0;
            while(pending + Epsilon >= step && steps < budget) { pending -= step; steps++; }
            if(pending < 0) pending = 0;
            return steps;
        }
        public void Reset() => pending = 0;
    }
}
