using System;
namespace Evolution.Core
{
    // Read-only presentation helpers. No simulation random draws or state changes.
    public static class Observation
    {
        public static float PeriodicDelta(float d, float world) => d - (float)Math.Floor(d / world + .5f) * world;
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static float Zoom(float distance, float wheel, float world) => Clamp(distance * (float)Math.Exp(-wheel * .18f), Math.Min(2, world * .5f), world * 1.65f);
        public static bool SameOrganism(Cell anchor, Cell candidate) => anchor.ColonyId != 0
            ? candidate.ColonyId == anchor.ColonyId : candidate.Id == anchor.Id;
        public static bool TryFind(Simulation simulation, int id, out Cell cell)
        {
            for (int i = 0; i < simulation.Count; i++) if (simulation.GetCell(i).Id == id) { cell = simulation.GetCell(i); return true; }
            cell = default; return false;
        }
        // Ray direction must be normalized. Returns the first positive surface intersection.
        public static float RaySphere(float ox, float oy, float oz, float dx, float dy, float dz, float radius)
        {
            float b = ox * dx + oy * dy + oz * dz;
            float c = ox * ox + oy * oy + oz * oz - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0) return float.PositiveInfinity;
            float root = (float)Math.Sqrt(discriminant), near = -b - root;
            return near >= 0 ? near : -b + root >= 0 ? -b + root : float.PositiveInfinity;
        }
        public static int MutationMask(Genome a, Genome b)
        {
            int mask = 0;
            if (a.Speed != b.Speed) mask |= 1;
            if (a.Size != b.Size) mask |= 2;
            if (a.Uptake != b.Uptake) mask |= 4;
            if (a.PreferredTemperature != b.PreferredTemperature) mask |= 8;
            if (a.OrganicSpecialization != b.OrganicSpecialization) mask |= 16;
            if (a.Adhesion != b.Adhesion) mask |= 32;
            if (a.ColonySize != b.ColonySize) mask |= 64;
            if (a.Branching != b.Branching) mask |= 128;
            if (a.Differentiation != b.Differentiation) mask |= 256;
            if (a.Transport != b.Transport) mask |= 512;
            if (a.RolePattern != b.RolePattern) mask |= 1024;
            if (a.Photosynthesis != b.Photosynthesis) mask |= 1 << 11;
            if (a.Predation != b.Predation) mask |= 1 << 12;
            if (a.Membrane != b.Membrane) mask |= 1 << 13;
            if (a.Sensitivity != b.Sensitivity) mask |= 1 << 14;
            if (a.ThermalTolerance != b.ThermalTolerance) mask |= 1 << 15;
            if (a.ToxinResistance != b.ToxinResistance) mask |= 1 << 16;
            if (a.EnergyEfficiency != b.EnergyEfficiency) mask |= 1 << 17;
            return mask;
        }
    }
}
