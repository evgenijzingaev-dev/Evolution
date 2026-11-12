using System;
using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class CellGameplayTests
    {
        static Genome Genes() => new Genome { Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22 };
        static Cell Founder(Genome g, float x = 4) => new Cell { Genes = g, X = x, Z = 4, Energy = 16, Age = 6 };
        static SimulationConfig Config() => new SimulationConfig { Capacity = 32, InitialPopulation = 0, WorldSize = 12,
            Ecosystem = true, InitialNutrients = 0, NutrientRegeneration = 0, MutationProbability = 0 };
        [Test] public void PlayerCanFeedBuildReserveDivideAndContinueAsInheritedChild()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.InitialOrganicEnergy = 4;
            var s = new Simulation(cfg,42,new[] {Founder(Genes())}); s.TryControl(1);
            float eaten = 0;
            for (int i = 0; i < 500 && !Biology.CanDivide(s.GetCell(0)); i++) {
                s.Step(); var c = s.GetCell(0); eaten += c.FoodLastStep;
                Assert.That(c.Energy - c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00001));
            }
            Assert.Greater(eaten,0); Assert.Greater(s.GetCell(0).Energy,16);
            Assert.IsTrue(s.RequestDivision()); s.Step();
            Assert.AreEqual(1,s.Births); Assert.IsTrue(Observation.TryFind(s,s.LastOffspringId,out var child));
            var parent = s.GetCell(0);
            Assert.AreEqual(parent.Genes,child.Genes); Assert.AreEqual(parent.LineageId,child.LineageId);
            Assert.That(parent.Energy - parent.Budget.Start,Is.EqualTo(parent.Budget.Net).Within(.00001));
            Assert.IsTrue(s.TryControl(child.Id)); s.Step();
            Assert.IsTrue(Observation.TryFind(s,child.Id,out child)); Assert.Greater(child.Growth,0); Assert.Greater(child.Budget.Growth,0);
        }
        [Test] public void BoostUsesMoreEnergyAndMovesFasterWhileDefenseNeedsAnInheritedShell()
        {
            var cfg = Config(); var g = Genes(); g.Membrane = .8f;
            var a = new Simulation(cfg,7,new[] {Founder(g)}); var b = new Simulation(cfg,7,new[] {Founder(g)});
            a.TryControl(1); b.TryControl(1); a.SetControlInput(1,0); b.SetControlInput(1,0); a.SetControlActions(false,true,false);
            for (int i = 0; i < 20; i++) { a.Step(); b.Step(); }
            Assert.Greater(a.GetCell(0).SwimSpeed,b.GetCell(0).SwimSpeed); Assert.Less(a.GetCell(0).Energy,b.GetCell(0).Energy);
            a.SetControlActions(false,true,true); a.Step(); var c = a.GetCell(0);
            Assert.IsTrue(c.Defending); Assert.IsFalse(c.Boosting); Assert.Greater(c.Budget.Defense,0);
            var plain = new Simulation(cfg,1,new[] {Founder(Genes())}); plain.TryControl(1);
            Assert.AreEqual(ActionBlock.Physiology,plain.ActionStatus(CellAction.Attack));
            Assert.AreEqual(ActionBlock.Physiology,plain.ActionStatus(CellAction.Defend));
        }
        [Test] public void PlayerBitesOnlyOnCommandAndLedgerIncludesPreyAndAttackCost()
        {
            var g = Genes(); g.Predation = 1; g.Size = 1.6f; var p = Genes(); p.Speed = .1f;
            var s = new Simulation(Config(),8,new[] {Founder(g),Founder(p,4.2f)}); s.TryControl(1);
            s.Step(); Assert.AreEqual(0,s.GetCell(0).AttackLastStep);
            s.SetControlActions(true,false,false); s.Step();
            var h = s.GetCell(0); var prey = s.GetCell(1);
            Assert.Greater(h.Budget.Prey,0); Assert.Greater(h.Budget.Attack,0); Assert.Less(prey.Integrity,1);
            Assert.That(h.Energy - h.Budget.Start,Is.EqualTo(h.Budget.Net).Within(.00001));
            Assert.That(prey.Energy - prey.Budget.Start,Is.EqualTo(prey.Budget.Net).Within(.00001));
        }
        [Test] public void ReleaseClearsHeldAbilitiesAndNoFoodCreatesNoEnergy()
        {
            var s = new Simulation(Config(),3,new[] {Founder(Genes())}); s.TryControl(1);
            s.SetControlActions(true,true,true); s.ReleaseControl(); s.TryControl(1); s.Step();
            var c = s.GetCell(0); Assert.IsFalse(c.Boosting || c.Defending || c.Attacking);
            Assert.AreEqual(0,c.Budget.Intake); Assert.Less(c.Energy,c.Budget.Start);
        }
        [Test] public void TurnAccelerationBrakingAndPeriodicCrossingAreContinuous()
        {
            var c = Founder(Genes()); c.TargetHeading = (float)Math.PI;
            CellMotion.Integrate(ref c,1,0,0,.05f);
            Assert.Less(Math.Abs(c.Heading),.1f); Assert.Greater(Math.Abs(c.AngularVelocity),0);
            Assert.That(c.VelocityX,Is.InRange(0,.999f));
            for (int i = 0; i < 40; i++) CellMotion.Integrate(ref c,1,0,0,.05f);
            Assert.Less(Math.Abs(CellMotion.AngleDelta(c.TargetHeading - c.Heading)),.3f);
            float moving = (float)Math.Sqrt(c.VelocityX*c.VelocityX+c.VelocityZ*c.VelocityZ);
            CellMotion.Integrate(ref c,0,0,0,.05f);
            Assert.That(Math.Sqrt(c.VelocityX*c.VelocityX+c.VelocityZ*c.VelocityZ),Is.InRange(.01,moving));
        }
        [Test] public void LightAndOrganicIncomeAreReportedSeparatelyAndFullCellsLeaveFood()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.InitialOrganicEnergy = 4;
            var g = Genes(); g.Photosynthesis = 1; g.OrganicSpecialization = .5f;
            var s = new Simulation(cfg,4,new[] {Founder(g)}); s.TryControl(1); s.Step(); var c = s.GetCell(0);
            Assert.Greater(c.Budget.Light,0); Assert.Greater(c.Budget.Organic,0); Assert.Greater(c.Budget.Substrate,0);
            Assert.That(c.FoodLastStep,Is.EqualTo(c.Budget.Intake).Within(.00001));
            Assert.That(c.Energy-c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00001));
        }
        [Test] public void YoungDescendantsMustFundTheirGrowthBeforeReproducing()
        {
            var c = Founder(Genes()); c.Generation = 1; c.Age = 20; c.Energy = 24; c.Growth = .5f;
            Assert.IsFalse(Biology.CanDivide(c)); c.Growth = 1; Assert.IsTrue(Biology.CanDivide(c));
        }
        [Test] public void ActiveShellDefenseReducesRealBiteDamage()
        {
            var hunter = Founder(Genes()); hunter.Id = hunter.LineageId = 1;
            hunter.Genes.Size = 1.8f; hunter.Genes.Predation = 1;
            var passive = Founder(Genes()); passive.Id = passive.LineageId = 2; passive.Integrity = 1;
            passive.Genes.Membrane = .8f; var active = passive; active.Defending = true; var secondHunter = hunter;
            Ecology.Bite(ref hunter,ref passive,0,.1f,out _);
            Ecology.Bite(ref secondHunter,ref active,0,.1f,out _);
            Assert.Less(passive.Integrity,1); Assert.Greater(active.Integrity,passive.Integrity);
        }
        [Test] public void LowReservesDisableOptionalAbilitiesWithoutCreatingEnergy()
        {
            var g = Genes(); g.Predation = g.Membrane = 1;
            var c = Founder(g); c.Energy = 1;
            var s = new Simulation(Config(),1,new[] {c}); s.TryControl(1);
            Assert.AreEqual(ActionBlock.LowEnergy,s.ActionStatus(CellAction.Attack));
            Assert.AreEqual(ActionBlock.LowEnergy,s.ActionStatus(CellAction.Boost));
            Assert.AreEqual(ActionBlock.LowEnergy,s.ActionStatus(CellAction.Defend));
            s.SetControlActions(true,true,true); s.Step(); c = s.GetCell(0);
            Assert.IsFalse(c.Boosting || c.Defending || c.Attacking); Assert.Less(c.Energy,1);
            Assert.That(c.Energy-c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00001));
        }
        [Test] public void SizeAndShellReduceAgilityAndCurrentsCanMoveAnIdleCell()
        {
            var small = Founder(Genes()); small.TargetHeading = 1.57f;
            var large = small; large.Genes.Size = 2; large.Genes.Membrane = 1;
            CellMotion.Integrate(ref small,1,0,0,.05f); CellMotion.Integrate(ref large,1,0,0,.05f);
            Assert.Greater(small.AngularVelocity,large.AngularVelocity);
            Assert.Greater(small.VelocityX,large.VelocityX);
            var idle = Founder(Genes()); CellMotion.Integrate(ref idle,0,1,0,.05f);
            Assert.Greater(idle.VelocityX,0); Assert.AreEqual(0,idle.SwimSpeed); Assert.AreEqual(0,CellMotion.Cost(idle));
        }
        [Test] public void MixedColoniesAndAutonomousPredatorsRetainAnExactEnergyLedger()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.InitialOrganicEnergy = 4; cfg.InitialPopulation = 24;
            var s = new Simulation(cfg,42);
            for (int t = 0; t < 160; t++) {
                s.Step();
                for (int i = 0; i < s.Count; i++) {
                    var c = s.GetCell(i);
                    Assert.That(c.Energy-c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00003),$"tick {s.Tick} cell {c.Id}");
                }
            }
            Assert.Greater(s.Births,0);
        }
    }
}
