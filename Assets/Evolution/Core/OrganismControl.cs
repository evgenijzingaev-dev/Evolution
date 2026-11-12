using System;

namespace Evolution.Core
{
    public sealed partial class Simulation
    {
        public int ControlledId { get; private set; }
        public int LastOffspringId { get; private set; }
        public int LastDivisionParentId { get; private set; }
        public long LastDivisionTick { get; private set; }
        float controlX, controlZ;
        bool divisionRequested;
        public bool TryControl(int id)
        {
            if (!Observation.TryFind(this, id, out _)) return false;
            ReleaseControl(); ControlledId = id; LastOffspringId = LastDivisionParentId = 0; return true;
        }
        public void ReleaseControl() { ControlledId = 0; controlX = controlZ = 0; divisionRequested = false; attackHeld = boostHeld = defenseHeld = false; }
        public void SetControlInput(float x, float z)
        {
            if (float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z))
                throw new ArgumentException("Invalid movement command");
            double length = Math.Sqrt((double)x * x + (double)z * z);
            double divisor = Math.Max(1, length);
            controlX = (float)(x / divisor); controlZ = (float)(z / divisor);
        }
        public bool RequestDivision()
        {
            if (Count >= cells.Length || !Observation.TryFind(this, ControlledId, out var c) || !Biology.CanDivide(c)) return false;
            divisionRequested = true; return true;
        }
    }
}
