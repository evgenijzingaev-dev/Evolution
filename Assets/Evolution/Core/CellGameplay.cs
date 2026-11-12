using System;
namespace Evolution.Core
{
    // Quantities committed during one biological tick, in the existing energy unit.
    public struct EnergyBudget
    {
        public float Start, Substrate, Organic, Light, Prey, Upkeep, Movement, Defense, Stress, Repair, Growth, Attack, Division, Contact, Transport, PredationLoss;
        public float Intake => Substrate + Organic + Light + Prey;
        public float Spent => Upkeep + Movement + Defense + Stress + Repair + Growth + Attack + Division + Contact + PredationLoss;
        public float Net => Intake - Spent + Transport;
    }
    public enum CellAction { Attack, Boost, Defend, Divide }
    public enum ActionBlock { None, NoControl, Physiology, LowEnergy, Immature, PopulationFull, Defending }

    public static class CellMotion
    {
        public static float AngleDelta(float angle) => angle - (float)Math.Floor((angle + Math.PI) / (2 * Math.PI)) * (float)(2 * Math.PI);
        // Steering changes torque, torque changes heading, thrust changes velocity. Water damps both.
        public static void Integrate(ref Cell c, float speed, float currentX, float currentZ, float dt, int members = 1)
        {
            float agility = (2 + c.Genes.Speed + c.Genes.Uptake * .3f) / (c.Genes.Size * (1 + .12f * (members - 1)) * (1 + c.Genes.Membrane * .5f));
            float wanted = Math.Max(-agility, Math.Min(agility, AngleDelta(c.TargetHeading - c.Heading) * 4));
            c.AngularVelocity += (wanted - c.AngularVelocity) * (1 - (float)Math.Exp(-7 * dt));
            c.Heading = AngleDelta(c.Heading + c.AngularVelocity * dt);
            float blend = 1 - (float)Math.Exp(-4 * dt / (c.Genes.Size * (1 + .08f * (members - 1))));
            c.SwimSpeed += (speed - c.SwimSpeed) * blend;
            float vx = (float)Math.Cos(c.Heading) * speed + currentX;
            float vz = (float)Math.Sin(c.Heading) * speed + currentZ;
            c.VelocityX += (vx - c.VelocityX) * blend;
            c.VelocityZ += (vz - c.VelocityZ) * blend;
            c.X += c.VelocityX * dt; c.Z += c.VelocityZ * dt;
        }
        public static float Cost(Cell c) => (.07f * c.SwimSpeed * c.SwimSpeed * c.Genes.Size
            + .025f * Math.Abs(c.AngularVelocity) * c.Genes.Size) * (1 - .3f * c.Genes.EnergyEfficiency) * (c.Boosting ? 1.8f : 1);
    }
    public sealed partial class Simulation
    {
        bool attackHeld, boostHeld, defenseHeld;
        Cell playerAnchor;
        bool hasPlayer;
        bool IsPlayerOrganism(Cell c) => hasPlayer && Observation.SameOrganism(playerAnchor, c);
        public void SetControlActions(bool attack, bool boost, bool defend)
        { attackHeld = attack; boostHeld = boost; defenseHeld = defend; }
        public ActionBlock ActionStatus(CellAction action)
        {
            if (!Observation.TryFind(this, ControlledId, out var c)) return ActionBlock.NoControl;
            if (action == CellAction.Divide) {
                if (Count >= cells.Length) return ActionBlock.PopulationFull;
                if (c.Age < Multicellularity.MaturityAge(c)) return ActionBlock.Immature;
                return Biology.CanDivide(c) ? ActionBlock.None : ActionBlock.LowEnergy;
            }
            if ((action == CellAction.Attack && (!config.Ecosystem || c.Genes.Predation <= .2f)) ||
                (action == CellAction.Defend && (!config.Ecosystem || c.Genes.Membrane < .35f))) return ActionBlock.Physiology;
            if (action != CellAction.Defend && defenseHeld) return ActionBlock.Defending;
            return c.Energy > Biology.Capacity(c.Genes) * .12f ? ActionBlock.None : ActionBlock.LowEnergy;
        }
        void PrepareActions()
        {
            hasPlayer = Observation.TryFind(this, ControlledId, out playerAnchor);
            bool boost = ActionStatus(CellAction.Boost) == ActionBlock.None && boostHeld;
            bool defend = ActionStatus(CellAction.Defend) == ActionBlock.None && defenseHeld;
            bool attack = ActionStatus(CellAction.Attack) == ActionBlock.None && attackHeld;
            for (int i = 0; i < Count; i++) {
                cells[i].Budget = new EnergyBudget { Start = cells[i].Energy };
                cells[i].RepairLastStep = 0;
                bool player = IsPlayerOrganism(cells[i]);
                cells[i].Boosting = player && boost;
                cells[i].Defending = player && defend && cells[i].Genes.Membrane >= .35f;
                cells[i].Attacking = player && attack && cells[i].Genes.Predation > .2f;
            }
        }
        public void LocalFood(Cell c, out float substrate, out float organic)
        { int tile = TileAt(Wrap(c.X), Wrap(c.Z)); substrate = nutrients[tile]; organic = organicEnergy[tile]; }
    }
}
