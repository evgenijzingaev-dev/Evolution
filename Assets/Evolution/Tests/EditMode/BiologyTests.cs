using System;
using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class BiologyTests
    {
        [Test] public void ZeroMutationCopiesEveryTrait()
        {
            var rng = new RandomSource(7); var g = Genome.Founder(ref rng);
            Assert.AreEqual(g, g.Inherit(ref rng, 0, .2f));
        }
        [Test] public void MutationChangesTraitsAndRemainsBounded()
        {
            var rng = new RandomSource(9); var original = Genome.Founder(ref rng); var g = original;
            g = g.Inherit(ref rng, 1, .3f); Assert.AreNotEqual(original, g);
            for (int i = 0; i < 10000; i++) {
                g = g.Inherit(ref rng, 1, .3f);
                Assert.That(g.Speed, Is.InRange(.1f, 3)); Assert.That(g.Size, Is.InRange(.4f, 2));
                Assert.That(g.Uptake, Is.InRange(.2f, 3)); Assert.That(g.PreferredTemperature, Is.InRange(0, 50));
                Assert.That(g.OrganicSpecialization, Is.InRange(0, 1));
            }
        }
        [Test] public void MotionAndThermalMismatchHaveEnergyCosts()
        {
            var g = new Genome { Speed = 1, Size = 1, Uptake = 1, PreferredTemperature = 22 };
            Assert.That(Biology.Cost(g, 22), Is.EqualTo(.38f).Within(.00001));
            Assert.Greater(Biology.Cost(g, 45), Biology.Cost(g, 22));
            Assert.Less(Biology.Efficiency(g, 45), Biology.Efficiency(g, 22));
            var fast = g; fast.Speed = 2; Assert.Greater(Biology.Cost(fast, 22), Biology.Cost(g, 22));
        }
        [Test] public void OneTickWithoutFoodSpendsExactlyMetabolicCost()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1, InitialNutrients = 0, NutrientRegeneration = 0 };
            var world = new Simulation(cfg, 1); var before = world.GetCell(0); world.Step();
            Assert.That(world.GetCell(0).Energy, Is.EqualTo(before.Energy - Biology.Cost(before.Genes, cfg.Temperature) * cfg.StepSeconds).Within(.00001));
        }
        [Test] public void DivisionRequiresMaturityAndPaysEnergyCost()
        {
            var parent = new Cell { Genes = new Genome { Size = 1 }, Age = 4, Energy = 24 };
            Assert.IsFalse(Biology.CanDivide(parent)); parent.Age = 5; Assert.IsTrue(Biology.CanDivide(parent));
            var child = new Cell { Genes = parent.Genes }; Assert.IsTrue(Biology.TryDivide(ref parent, ref child));
            Assert.That(parent.Energy, Is.EqualTo(10.75f)); Assert.That(child.Energy, Is.EqualTo(10.75f));
            parent.Energy = 1; Assert.IsFalse(Biology.CanDivide(parent));
        }
        [Test] public void FedPopulationReproducesWithLineageAndInheritedGenome()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1, InitialNutrients = 8, NutrientRegeneration = .5f, MutationProbability = 0 };
            var world = new Simulation(cfg, 42); var founder = world.GetCell(0);
            for (int i = 0; i < 2000 && world.Births == 0; i++) world.Step();
            Assert.Greater(world.Births, 0); Assert.AreEqual(2, world.Count);
            var child = world.GetCell(1);
            Assert.AreEqual(founder.Id, child.ParentId); Assert.AreEqual(1, child.Generation);
            Assert.AreEqual(founder.Genes, child.Genes); Assert.AreEqual(0, child.Age);
            Assert.That(world.GetCell(0).Energy, Is.EqualTo(child.Energy).Within(.00001));
        }
        [Test] public void StarvationKillsAndNutrientsNeverBecomeNegative()
        {
            var cfg = new SimulationConfig { InitialPopulation = 20, InitialNutrients = 0, NutrientRegeneration = 0 };
            var world = new Simulation(cfg, 12);
            for (int i = 0; i < 6000; i++) world.Step();
            Assert.AreEqual(0, world.Count); Assert.AreEqual(20, world.Deaths);
            for (int i = 0; i < cfg.GridSize * cfg.GridSize; i++) Assert.GreaterOrEqual(world.GetNutrient(i), 0);
        }
        [Test] public void HeatShockReducesPopulationEnergy()
        {
            var cfg = new SimulationConfig { InitialPopulation = 20, InitialNutrients = 0, NutrientRegeneration = 0 };
            var mild = new Simulation(cfg, 42); var hot = new Simulation(cfg, 42); hot.SetEnvironment(80, 0);
            for (int i = 0; i < 100; i++) { mild.Step(); hot.Step(); }
            float a = 0, b = 0;
            for (int i = 0; i < mild.Count; i++) a += mild.GetCell(i).Energy;
            for (int i = 0; i < hot.Count; i++) b += hot.GetCell(i).Energy;
            Assert.Less(b, a);
        }
        [Test] public void SameSeedAndTickCommandsReproduceAllState()
        {
            var cfg = new SimulationConfig(); var a = new Simulation(cfg, 123); var b = new Simulation(cfg, 123);
            for (int tick = 0; tick < 2000; tick++) {
                if (tick == 600) { a.SetEnvironment(35, .2f); b.SetEnvironment(35, .2f); }
                a.Step(); b.Step();
            }
            Assert.Greater(a.Births, 0); Assert.Greater(a.Deaths, 0);
            Assert.AreEqual(a.Count, b.Count); Assert.AreEqual(a.Births, b.Births); Assert.AreEqual(a.Deaths, b.Deaths);
            Assert.AreEqual(a.Tick, b.Tick); Assert.AreEqual(a.RandomState, b.RandomState);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a.GetCell(i), b.GetCell(i));
            for (int i = 0; i < cfg.GridSize * cfg.GridSize; i++) {
                Assert.AreEqual(a.GetNutrient(i), b.GetNutrient(i));
                Assert.AreEqual(a.GetOrganicEnergy(i), b.GetOrganicEnergy(i));
            }
        }
        [Test] public void CapacityBoundsAndPositionsHoldDuringLongRun()
        {
            var cfg = new SimulationConfig { Capacity = 12, InitialPopulation = 12, InitialNutrients = 8, NutrientRegeneration = .5f };
            var world = new Simulation(cfg, 42);
            for (int tick = 0; tick < 5000; tick++) {
                world.Step(); Assert.LessOrEqual(world.Count, cfg.Capacity);
                for (int i = 0; i < world.Count; i++) {
                    var c = world.GetCell(i); Assert.That(c.X, Is.InRange(0, cfg.WorldSize));
                    Assert.That(c.Z, Is.InRange(0, cfg.WorldSize)); Assert.Greater(c.Energy, 0);
                }
            }
        }
        [Test] public void FeedingTransfersNutrientsToEnergyWithoutCreatingFood()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1, InitialNutrients = 1, NutrientRegeneration = 0 };
            var world = new Simulation(cfg, 42); var before = world.GetCell(0);
            float foodBefore = 0, foodAfter = 0;
            for (int i = 0; i < cfg.GridSize * cfg.GridSize; i++) foodBefore += world.GetNutrient(i);
            world.Step();
            for (int i = 0; i < cfg.GridSize * cfg.GridSize; i++) foodAfter += world.GetNutrient(i);
            float consumed = before.Genes.Uptake * cfg.StepSeconds;
            Assert.That(foodBefore - foodAfter, Is.EqualTo(consumed).Within(.0002));
            float expected = before.Energy + consumed * Biology.SubstrateYield(before.Genes, cfg.Temperature)
                - Biology.Cost(before.Genes, cfg.Temperature) * cfg.StepSeconds;
            Assert.That(world.GetCell(0).Energy, Is.EqualTo(expected).Within(.00001));
        }
        [Test] public void TemperatureFavoursMatchingInheritedOptimum()
        {
            var cold = new Genome { Speed = 1, Size = 1, Uptake = 1, PreferredTemperature = 10 };
            var warm = cold; warm.PreferredTemperature = 35;
            Assert.Less(Biology.Cost(cold, 10), Biology.Cost(warm, 10));
            Assert.Greater(Biology.Efficiency(cold, 10), Biology.Efficiency(warm, 10));
            Assert.Less(Biology.Cost(warm, 35), Biology.Cost(cold, 35));
            Assert.Greater(Biology.Efficiency(warm, 35), Biology.Efficiency(cold, 35));
        }
        [Test] public void OldAgeKillsEvenWithFood()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1, MaxAge = .1f };
            var world = new Simulation(cfg, 42); world.Step(); Assert.AreEqual(1, world.Count);
            world.Step(); Assert.AreEqual(0, world.Count); Assert.AreEqual(1, world.Deaths);
        }
        [Test] public void DifferentSeedsProduceDifferentFounders()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1 };
            Assert.AreNotEqual(new Simulation(cfg, 1).GetCell(0), new Simulation(cfg, 2).GetCell(0));
        }
        [Test] public void ConfigIsCopiedAndInvalidValuesRejected()
        {
            var cfg = new SimulationConfig(); var world = new Simulation(cfg, 1); cfg.WorldSize = 0;
            Assert.Greater(world.Config.WorldSize, 0); world.Config.WorldSize = 0; world.Step();
            Assert.Throws<ArgumentException>(() => new Simulation(cfg, 1));
            Assert.Throws<ArgumentException>(() => world.SetEnvironment(float.NaN, 0));
        }
    }
}
