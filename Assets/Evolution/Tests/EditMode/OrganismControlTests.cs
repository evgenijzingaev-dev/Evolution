using System;
using Evolution.Core;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class OrganismControlTests
    {
        static Genome Genes(float adhesion = 0) => new Genome { Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22, Adhesion = adhesion, ColonySize = 1 };
        static Cell Founder(float energy = 14, float age = 0, float x = 4, float adhesion = 0) => new Cell { Genes = Genes(adhesion), Energy = energy, Age = age, X = x, Z = 4 };
        static SimulationConfig Config() => new SimulationConfig { InitialPopulation = 0, InitialNutrients = 0, NutrientRegeneration = 0, MutationProbability = 0, Capacity = 16, WorldSize = 10 };
        [Test] public void EnterAndExitDoNotChangeCellsOrRandomnessAndAutonomyResumes()
        {
            var a = new Simulation(Config(),42,new[] { Founder() }); var b = new Simulation(Config(),42,new[] { Founder() });
            var before = a.GetCell(0); uint random = a.RandomState;
            Assert.IsTrue(a.TryControl(before.Id)); a.SetControlInput(1,0); a.ReleaseControl();
            Assert.AreEqual(before,a.GetCell(0)); Assert.AreEqual(random,a.RandomState); Assert.AreEqual(0,a.Tick);
            a.Step(); b.Step(); Assert.AreEqual(a.GetCell(0),b.GetCell(0)); Assert.AreEqual(a.RandomState,b.RandomState);
        }
        [Test] public void InputIsBoundedAndInactiveWorldKeepsMoving()
        {
            var a = new Simulation(Config(),42,new[] { Founder(x:9.99f), Founder(x:2) });
            a.TryControl(1); a.SetControlInput(100,0); var other = a.GetCell(1); a.Step();
            Assert.That(a.GetCell(0).X,Is.EqualTo(.04f).Within(.0001));
            Assert.That(a.GetCell(0).Z,Is.EqualTo(4)); Assert.AreNotEqual(other.X,a.GetCell(1).X);
            a.SetControlInput(0,0); var stationary = a.GetCell(0); a.Step();
            Assert.AreEqual(stationary.X,a.GetCell(0).X); Assert.AreEqual(stationary.Z,a.GetCell(0).Z);
            Assert.Less(a.GetCell(0).Energy,stationary.Energy);
            Assert.Throws<ArgumentException>(() => a.SetControlInput(float.NaN,0));
        }
        [Test] public void ControlledCellsPayNormalCostsAndCanStarve()
        {
            var cfg = Config(); var a = new Simulation(cfg,42,new[] { Founder(.01f) });
            a.TryControl(1); a.Step(); Assert.AreEqual(0,a.Count); Assert.AreEqual(0,a.ControlledId); Assert.AreEqual(1,a.Deaths);
            var b = new Simulation(cfg,42,new[] { Founder() }); var c = new Simulation(cfg,42,new[] { Founder() });
            b.TryControl(1); b.SetControlInput(0,0); b.Step(); c.Step(); Assert.AreEqual(c.GetCell(0).Energy,b.GetCell(0).Energy);
        }
        [Test] public void ManualDivisionUsesNormalProvisioningAndChildCanBeControlled()
        {
            var a = new Simulation(Config(),42,new[] { Founder(24,5) }); a.TryControl(1); a.Step();
            Assert.AreEqual(0,a.Births); Assert.IsTrue(a.RequestDivision()); float before = a.GetCell(0).Energy;
            a.Step(); Assert.AreEqual(1,a.Births); Assert.AreEqual(1,a.LastDivisionParentId);
            Assert.IsTrue(Observation.TryFind(a,a.LastOffspringId,out var child)); Assert.AreEqual(1,child.ParentId);
            Assert.AreEqual(Genes(),child.Genes); Assert.Less(a.GetCell(0).Energy + child.Energy,before);
            Assert.IsTrue(a.TryControl(child.Id)); Assert.AreEqual(child.Id,a.ControlledId);
            Assert.IsFalse(a.RequestDivision());
        }
        [Test] public void InvalidControlAndFullPopulationCannotCreateBirths()
        {
            var cfg = Config(); cfg.Capacity = 1;
            var a = new Simulation(cfg,42,new[] { Founder(24,5) }); a.TryControl(1);
            Assert.IsFalse(a.TryControl(999)); Assert.AreEqual(1,a.ControlledId);
            Assert.IsFalse(a.RequestDivision()); a.Step(); Assert.AreEqual(0,a.Births);
        }
        [Test] public void SelectingColonyMemberSteersItsRootAndPreservesTheBond()
        {
            var a = new Simulation(Config(),42,new[] { Founder(24,5,adhesion:1) });
            a.Step(); Assert.AreEqual(1,a.ColonyCount); var child = a.GetCell(1); a.TryControl(child.Id); a.SetControlInput(0,1);
            var root = a.GetCell(0); a.Step(); Assert.That(a.GetCell(0).X,Is.EqualTo(root.X).Within(.00001));
            Assert.Greater(a.GetCell(0).Z,root.Z); Assert.AreEqual(root.Id,a.GetCell(1).BondParentId);
        }
        [Test] public void ReleaseClearsQueuedDivisionAndControlDoesNotPreventAgingDeath()
        {
            var a = new Simulation(Config(),42,new[] { Founder(24,5) }); a.TryControl(1); a.RequestDivision(); a.ReleaseControl();
            a.TryControl(1); a.Step(); Assert.AreEqual(0,a.Births);
            var cfg = Config(); cfg.MaxAge = 1;
            a = new Simulation(cfg,42,new[] { Founder(age:.99f) }); a.TryControl(1); a.Step();
            Assert.AreEqual(0,a.Count); Assert.AreEqual(0,a.ControlledId);
        }
    }
}
