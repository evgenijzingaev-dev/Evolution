using System;
using Evolution.Core;

namespace Evolution.Presentation
{
    public enum SwimmingStyle { Amoeboid, Flagellar, Ciliary, Armored }

    // Read-only projection of biology. No Simulation reference, random draws, or gameplay writes.
    public struct AnimationTargets
    {
        public float Drive, Turn, Frequency, Activity, Compliance, Stress, Defense, Repair, Light, Growth, Acceleration, Readiness;
        public SwimmingStyle Style;
        public bool Flagellum, Cilia;
        public static AnimationTargets From(Cell c, OceanSample water, float previousSpeed, float tickSeconds)
        {
            var g = c.Genes;
            float reserve = Clamp(c.Energy / Biology.Capacity(g));
            float thermal = Clamp(Math.Abs(water.Temperature - g.PreferredTemperature) / 25) * (1 - .65f * g.ThermalTolerance);
            float stress = Clamp(thermal + water.Toxicity * (1 - g.ToxinResistance) + (1 - c.Integrity) * .5f);
            float activity = (.12f + .88f * Clamp(reserve / .3f)) * (1 - .65f * stress);
            // These are the existing stage E phenotype thresholds, not new genes.
            bool flagellum = g.Speed > .8f, cilia = g.Uptake > 1.05f;
            return new AnimationTargets {
                Flagellum = flagellum, Cilia = cilia,
                Style = g.Membrane > .7f ? SwimmingStyle.Armored : flagellum ? SwimmingStyle.Flagellar : cilia ? SwimmingStyle.Ciliary : SwimmingStyle.Amoeboid,
                Drive = Clamp(c.SwimSpeed / Math.Max(.1f, Ecology.Mobility(g))),
                Turn = (float)Math.Tanh(c.AngularVelocity * .5f + (water.CurrentZ * (float)Math.Cos(c.Heading) - water.CurrentX * (float)Math.Sin(c.Heading)) * .2f),
                Acceleration = Math.Max(-1, Math.Min(1, (c.SwimSpeed - previousSpeed) / Math.Max(.001f, tickSeconds))),
                Activity = activity, Stress = stress,
                Frequency = (1.2f + 2.5f * Clamp(c.SwimSpeed)) / (float)Math.Sqrt(g.Size) * activity,
                Compliance = (.035f + .09f * (1 - g.Membrane)) / (float)Math.Sqrt(g.Size) * (c.Defending ? .55f : 1),
                Defense = c.Defending ? 1 : 0, Repair = Clamp(c.RepairLastStep * 100),
                Light = Clamp(c.Budget.Light * 20), Growth = c.Growth,
                Readiness = c.Age >= Multicellularity.MaturityAge(c) && c.Growth >= 1 ? Clamp((reserve - .65f) / .2f) : 0
            };
        }
        public static float Clamp(float x) => Math.Max(0, Math.Min(1, x));
    }

    public sealed class CellAnimation
    {
        public AnimationTargets Target;
        public float Drive, Turn, Activity, Compliance, Stress, Defense, Repair, Light, Growth, Acceleration, Readiness;
        public double Phase { get; private set; }
        public double IdentityPhase { get; }
        public float Frequency { get; private set; }
        public double FeedAt { get; private set; } = double.NegativeInfinity;
        public double AttackAt { get; private set; } = double.NegativeInfinity;
        public double DamageAt { get; private set; } = double.NegativeInfinity;
        public double DivisionAt { get; private set; } = double.NegativeInfinity;
        public float ContactAngle { get; private set; }
        public float FoodEnergy { get; private set; }
        public float DamageAngle { get; private set; }
        public float AttackAngle { get; private set; }
        public CellAnimation(int id, AnimationTargets initial)
        {
            Target = initial; IdentityPhase = Phase = (id * 2.399963229728653) % (Math.PI * 2);
            Drive = initial.Drive; Turn = initial.Turn; Activity = initial.Activity; Compliance = initial.Compliance;
            Stress = initial.Stress; Defense = initial.Defense; Repair = initial.Repair; Light = initial.Light;
            Growth = initial.Growth; Readiness = initial.Readiness; Frequency = initial.Frequency; Acceleration = initial.Acceleration;
        }
        public void Observe(Cell c, double time, float contactAngle)
        {
            // Light has no engulfed particle. Prey gain is separate from an attempted attack.
            float food = c.Budget.Substrate + c.Budget.Organic + c.Budget.Prey;
            if (food > .000001f && time - FeedAt >= 1.25) {
                FeedAt = time; FoodEnergy = food; ContactAngle = contactAngle;
            }
            if (c.AttackLastStep > 0 && time - AttackAt >= .45) { AttackAt = time; AttackAngle = contactAngle; }
            if ((c.DamageLastStep > 0 || c.Budget.Contact > 0) && time - DamageAt >= .35) { DamageAt = time; DamageAngle = contactAngle; }
        }
        public void Divide(double time) { DivisionAt = time; }
        public void Advance(double seconds)
        {
            if (seconds <= 0) return;
            float blend = (float)(1 - Math.Exp(-6 * seconds));
            // Integral of the exponential frequency transition: independent of render frame rate.
            Phase += 2 * Math.PI * (Target.Frequency * seconds + (Frequency - Target.Frequency) * blend / 6);
            Frequency = Mix(Frequency, Target.Frequency, blend);
            Drive = Mix(Drive, Target.Drive, blend); Turn = Mix(Turn, Target.Turn, blend);
            Activity = Mix(Activity, Target.Activity, blend); Compliance = Mix(Compliance, Target.Compliance, blend);
            Stress = Mix(Stress, Target.Stress, blend); Defense = Mix(Defense, Target.Defense, blend);
            Repair = Mix(Repair, Target.Repair, blend); Light = Mix(Light, Target.Light, blend);
            Growth = Mix(Growth, Target.Growth, blend); Readiness = Mix(Readiness, Target.Readiness, blend); Acceleration = Mix(Acceleration, Target.Acceleration, blend);
        }
        public void Synchronize(double rootPhase, int slot, double seconds)
        {
            double delta = rootPhase - slot * .65 - Phase;
            delta -= Math.Floor((delta + Math.PI) / (2 * Math.PI)) * (2 * Math.PI);
            Phase += delta * (1 - Math.Exp(-3 * Math.Max(0, seconds)));
        }
        static float Mix(float a, float b, float t) => a + (b - a) * t;
        public static float Pulse(double time, double start, double duration)
        {
            double t = (time - start) / duration;
            return t <= 0 || t >= 1 ? 0 : (float)Math.Pow(Math.Sin(Math.PI * t), 2);
        }
        public float Feeding(double time) => Pulse(time, FeedAt, 1.2);
        public float Damage(double time) => Pulse(time, DamageAt, .65);
        public float Attack(double time) => Pulse(time, AttackAt, .45);
        public float Division(double time) => Pulse(time, DivisionAt, 1.1);
        public static float SmoothBirth(double age)
        {
            float t = AnimationTargets.Clamp((float)(age / .8)); return t * t * (3 - 2 * t);
        }
    }

    // Mirrored in LivingMembrane.hlsl. Positive, bounded radial displacement preserves topology.
    // Interior points use the same map as the surface, so cytoplasm follows its membrane.
    public struct MembranePose
    {
        public float Amplitude, Phase, Stretch, Pinch, Drive, Turn, Acceleration, Amoeba;
        public float Feed, Damage, Defense, Stress, Repair, ContactX, ContactZ, InjuryX, InjuryZ;
        public float Axial, Flatness, Breadth, Lobes, Taper;
        public float Photo, Predator, Shell, Breath, Pattern;
        public MembranePose WithGenome(Genome g)
        {
            float speed = AnimationTargets.Clamp((g.Speed - .5f) / 1.5f);
            Axial = .55f * speed + .24f * g.Predation;
            Flatness = .3f * g.Photosynthesis - .1f * g.Membrane;
            Breadth = -.22f * speed + .2f * g.Photosynthesis + .12f * g.Membrane;
            Lobes = .065f * g.Photosynthesis + .04f * g.OrganicSpecialization;
            Taper = .23f * g.Predation;
            Photo = g.Photosynthesis; Predator = g.Predation; Shell = g.Membrane;
            return this;
        }
        public static MembranePose From(CellAnimation a, double time, float phase)
        {
            return new MembranePose { Amplitude = a.Compliance * (.3f + .7f * a.Activity), Phase = phase,
                Stretch = .08f * a.Drive + .055f * a.Acceleration + .06f * a.Readiness + .24f * a.Division(time), Pinch = a.Division(time),
                Drive = a.Drive, Turn = a.Turn, Acceleration = a.Acceleration,
                Amoeba = a.Target.Style == SwimmingStyle.Amoeboid ? a.Drive : 0,
                Feed = a.Feeding(time), Damage = a.Damage(time), Defense = a.Defense, Stress = a.Stress, Repair = a.Repair,
                Breath = .014f * a.Activity * (float)Math.Sin(time * 1.8 + a.IdentityPhase),
                Pattern = (float)(a.IdentityPhase / (2 * Math.PI)),
                ContactX = (float)Math.Cos(a.ContactAngle), ContactZ = (float)Math.Sin(a.ContactAngle),
                InjuryX = (float)Math.Cos(a.DamageAngle), InjuryZ = (float)Math.Sin(a.DamageAngle) };
        }
        public void Deform(ref float x, ref float y, ref float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            float nx = length > .00001f ? x / length : 1, nz = length > .00001f ? z / length : 0;
            float equator = length > .00001f ? (x * x + z * z) / (length * length) : 0;
            float angle = (float)Math.Atan2(z, x);
            float local = (float)Math.Pow(Math.Max(0, nx * ContactX + nz * ContactZ), 6) * equator;
            float radial = 1 + Breath + equator * Amplitude * ((float)Math.Sin(angle * 3 - Phase) + .45f * (float)Math.Sin(angle * 5 + Phase * .7f));
            radial += Amoeba * .085f * (float)Math.Pow(Math.Max(0, nx), 6) * (.5f + .5f * (float)Math.Sin(Phase));
            float injury = (float)Math.Pow(Math.Max(0, nx * InjuryX + nz * InjuryZ), 6) * equator;
            radial += local * .065f * Feed - injury * .055f * Damage;
            radial = Math.Max(.76f, Math.Min(1.24f, radial));
            float originalX = x;
            x *= radial * (1 + Stretch); y *= radial; z *= radial / (float)Math.Sqrt(1 + Stretch);
            float neck = 1 - .32f * Pinch * (float)Math.Exp(-originalX * originalX * 65);
            y *= neck; z *= neck;
            z += Turn * .09f * originalX * originalX + Acceleration * .025f * originalX;
            // Stable inherited silhouette, shared by the membrane, organelles and motor roots.
            float outline = 1 + equator * (Lobes * (float)Math.Cos(3 * angle)
                + Photo * .09f * (float)Math.Cos(2 * angle) + Shell * .055f * (float)Math.Cos(6 * angle));
            outline = Math.Max(.88f,Math.Min(1.2f,outline));
            x *= (1 + Axial) * outline;
            y *= (1 - Flatness) * outline;
            z *= (1 + Breadth) * outline * (1 - Taper * nx);
            z += Predator * .035f * equator * Math.Max(0, 1 - 4 * originalX * originalX) * (1-.7f*Pinch);
        }
        // Organelle centers stay in the central quarter-radius, before the shared deformation.
        public void Interior(float x, float y, float z, out float px, out float py, out float pz)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            float shrink = length > .24f ? .24f / length : 1;
            px = x * shrink; py = y * shrink; pz = z * shrink; Deform(ref px, ref py, ref pz);
        }
    }

    public static class MotorCurves
    {
        public static void Flagellum(float t, float phase, float drive, float activity, float turn, out float x, out float z)
        {
            float amplitude = (.012f + .095f * drive) * activity * (1 + turn * .3f);
            x = -.5f - t * .85f;
            z = (float)Math.Sin(phase - t * 8) * amplitude * t + turn * t * t * .13f;
        }
        public static void Cilium(float t, float angle, float phase, float drive, float activity, float turn, out float x, out float z)
        {
            // Fast power stroke, slow folded recovery; travelling metachronal wave around the cell.
            float wave = phase - angle * 2;
            float stroke = (float)Math.Sin(wave + .45f * Math.Sin(wave));
            float reach = .16f * activity * (.25f + .75f * drive) * (1 + turn * (float)Math.Sin(angle) * .35f);
            float bend = stroke * t * t * reach;
            float radial = .5f + t * (.05f + reach * (.65f + .35f * (float)Math.Cos(wave)));
            x = (float)Math.Cos(angle) * radial - (float)Math.Sin(angle) * bend;
            z = (float)Math.Sin(angle) * radial + (float)Math.Cos(angle) * bend;
        }
    }
}
