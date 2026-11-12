using System;
using System.Collections.Generic;
using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class MulticellularityTests
    {
        static Genome Genes(float adhesion = 1) => new Genome {
            Size = 1, Speed = .1f, Uptake = 1, PreferredTemperature = 22,
            Adhesion = adhesion, ColonySize = 1, Branching = .9f, Differentiation = 1, Transport = 1, RolePattern = 0 };
        static Cell Founder(Genome genes, float x = 4, float energy = 14, float age = 0) =>
            new Cell { Genes = genes, X = x, Z = 4, Energy = energy, Age = age };
        static SimulationConfig Config() => new SimulationConfig { InitialPopulation = 0, Capacity = 64,
            WorldSize = 10, GridSize = 10, InitialNutrients = 0, NutrientRegeneration = 0, MaxAge = 100,
            MutationProbability = 0 };
        static Simulation Pair(float adhesion = 1, float a = 14, float b = 14, float age = 0)
        {
            var world = new Simulation(Config(), 42, new[] { Founder(Genes(adhesion), energy: a, age: age), Founder(Genes(adhesion), 4.45f, b) });
            for (int t = 0; t < 100 && world.ColonyCount == 0; t++) world.Step();
            return world;
        }
        static float Distance(float a, float b, float size) { float d = a - b; return d - (float)Math.Floor(d / size + .5f) * size; }
        static void AssertTopology(Simulation world)
        {
            var cells = new Dictionary<int, Cell>(); var sizes = new Dictionary<int, int>();
            int colonial = 0;
            for (int i = 0; i < world.Count; i++) {
                Cell c = world.GetCell(i); cells.Add(c.Id, c);
                Assert.That(c.X, Is.InRange(0, world.Config.WorldSize)); Assert.That(c.Z, Is.InRange(0, world.Config.WorldSize));
                Assert.That(c.Energy, Is.InRange(0, Biology.Capacity(c.Genes)));
                Assert.That(c.Specialization, Is.InRange(0, 1));
                if (c.ColonyId != 0) { colonial++; sizes[c.ColonyId] = sizes.TryGetValue(c.ColonyId, out int n) ? n + 1 : 1; }
                if (c.BondParentId == 0) continue;
                Assert.Less(c.BondParentId, c.Id);
                Assert.IsTrue(cells.TryGetValue(c.BondParentId, out var parent), "Dangling bond after compaction/death");
                Assert.AreEqual(parent.ColonyId, c.ColonyId);
                float x = Distance(c.X, parent.X, world.Config.WorldSize), z = Distance(c.Z, parent.Z, world.Config.WorldSize);
                Assert.That(Math.Sqrt(x * x + z * z), Is.EqualTo(.29 * (parent.Genes.Size + c.Genes.Size)).Within(.0001));
            }
            Assert.AreEqual(colonial, world.ColonialCells); Assert.AreEqual(sizes.Count, world.ColonyCount);
            foreach (var entry in sizes) {
                Assert.That(entry.Value, Is.InRange(2, world.Config.MaxColonyCells));
                Assert.LessOrEqual(entry.Value, Multicellularity.SizeLimit(cells[entry.Key].Genes, world.Config.MaxColonyCells));
            }
        }
        [Test] public void CompatibleNeighboursFormPersistentCoherentOrganism()
        {
            var world = Pair(); Assert.AreEqual(1, world.ColonyCount); Assert.AreEqual(2, world.ColonialCells);
            int anchor = world.GetCell(1).BondParentId;
            float before = world.GetCell(1).Specialization;
            for (int t = 0; t < 200; t++) { world.Step(); AssertTopology(world); Assert.AreEqual(anchor, world.GetCell(1).BondParentId); }
            Assert.Greater(world.GetCell(1).Specialization, before);
            Assert.AreNotEqual(world.GetCell(0).Role, world.GetCell(1).Role);
        }
        [Test] public void LowAdhesionRetainsSolitaryLifeAndCannotCreateColonies()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.NutrientRegeneration = .5f;
            var world = new Simulation(cfg, 42, new[] { Founder(Genes(0), energy: 24, age: 5), Founder(Genes(0), 4.45f) });
            for (int t = 0; t < 1500; t++) { world.Step(); Assert.AreEqual(0, world.ColonyCount); }
            Assert.Greater(world.Births, 0); Assert.Greater(world.Count, 0);
        }
        [Test] public void AdhesionAlsoRequiresGeneticCompatibility()
        {
            var g = Genes(); var different = g; different.PreferredTemperature = 40;
            Assert.IsFalse(Multicellularity.Compatible(g, different));
            var cfg = Config(); var world = new Simulation(cfg, 42, new[] { Founder(g), Founder(different, 4.45f) });
            for (int t = 0; t < 200; t++) world.Step();
            Assert.AreEqual(0, world.ColonyCount);
        }
        [Test] public void TransferFollowsGradientHasLossAndRespectsBothCapacities()
        {
            var a = Founder(Genes(), energy: 22); var b = Founder(Genes(), energy: 2);
            float received = Multicellularity.Transfer(ref a, ref b, 1);
            Assert.That(a.Energy, Is.EqualTo(18).Within(.00001)); Assert.That(b.Energy, Is.EqualTo(5.8).Within(.00001));
            Assert.That(received, Is.EqualTo(3.8).Within(.00001));
            for (int t = 0; t < 100; t++) Multicellularity.Transfer(ref a, ref b, 100);
            Assert.That(a.Energy, Is.EqualTo(b.Energy).Within(.0001)); Assert.Less(a.Energy + b.Energy, 24);
            var small = Genes(); small.Size = .4f; b = Founder(small, energy: 9.59f); a.Energy = 24;
            Multicellularity.Transfer(ref a, ref b, 100);
            Assert.That(b.Energy, Is.InRange(9.59f, Biology.Capacity(small)));
            float total = a.Energy + b.Energy; a.Genes.Transport = 0;
            Assert.AreEqual(0, Multicellularity.Transfer(ref a, ref b, 1)); Assert.AreEqual(total, a.Energy + b.Energy);
        }
        [Test] public void ActualBondTransfersEnergyToHungryCellOnlyWithinOrganism()
        {
            var world = Pair(a: 20, b: 2); Assert.AreEqual(1, world.ColonyCount);
            Assert.Greater(world.EnergyTransferred, 0); float low = world.GetCell(1).Energy;
            world.Step(); Assert.Greater(world.GetCell(1).Energy, low);
            var separate = Pair(0, 20, 2); Assert.AreEqual(0, separate.EnergyTransferred);
        }
        [Test] public void DifferentiationHasWorkingBenefitsAndTradeoffsForEveryRole()
        {
            var general = Founder(Genes());
            var feeder = general; feeder.Role = CellRole.Feeding; feeder.Specialization = 1; feeder.ColonyId = 1;
            var motor = feeder; motor.Role = CellRole.Movement;
            var armor = feeder; armor.Role = CellRole.Defense;
            var germ = feeder; germ.Role = CellRole.Reproduction;
            Assert.Greater(Multicellularity.Feeding(feeder), Multicellularity.Feeding(general));
            Assert.Less(Multicellularity.Feeding(motor), Multicellularity.Feeding(general));
            Assert.Greater(Multicellularity.Motor(motor), Multicellularity.Motor(general));
            Assert.Less(Multicellularity.Motor(feeder), Multicellularity.Motor(general));
            Assert.Greater(Multicellularity.Cost(armor, 22, .4f), Multicellularity.Cost(general, 22));
            Assert.Less(Multicellularity.Cost(armor, 80, .4f), Multicellularity.Cost(general, 80));
            germ.Age = 4; germ.Energy = 19; Assert.IsTrue(Biology.CanDivide(germ));
            feeder.Age = 4; feeder.Energy = 19; Assert.IsFalse(Biology.CanDivide(feeder));
            float sa = 8, sb = 8, oa = 0, ob = 0; feeder.Energy = general.Energy = 10;
            Biology.Feed(ref feeder, ref sa, ref oa, 22, 1); Biology.Feed(ref general, ref sb, ref ob, 22, 1);
            Assert.Greater(feeder.Energy, general.Energy); Assert.Less(sa, sb);
        }
        [Test] public void GenomeCopiesAndMutatesColonyStructureAndAllRoleTraits()
        {
            var rng = new RandomSource(73); var g = Genes(); g.Adhesion = g.Branching = g.Differentiation = g.Transport = g.RolePattern = g.ColonySize = .5f;
            Assert.AreEqual(g, g.Inherit(ref rng, 0, .3f)); Assert.AreEqual(g, g.Inherit(ref rng, 1, 0));
            var mutant = g.Inherit(ref rng, 1, .3f);
            Assert.AreNotEqual(g.Adhesion, mutant.Adhesion); Assert.AreNotEqual(g.ColonySize, mutant.ColonySize);
            Assert.AreNotEqual(g.Branching, mutant.Branching); Assert.AreNotEqual(g.Differentiation, mutant.Differentiation);
            Assert.AreNotEqual(g.Transport, mutant.Transport); Assert.AreNotEqual(g.RolePattern, mutant.RolePattern);
            for (int t = 0; t < 1000; t++) {
                mutant = mutant.Inherit(ref rng, 1, .3f);
                foreach (float trait in new[] { mutant.Adhesion, mutant.ColonySize, mutant.Branching, mutant.Differentiation, mutant.Transport, mutant.RolePattern })
                    Assert.That(trait, Is.InRange(0, 1));
            }
            var shift = g; shift.RolePattern = .8f;
            Assert.AreNotEqual(Multicellularity.Role(g, 0), Multicellularity.Role(shift, 0));
        }
        [Test] public void PaidOffspringInheritBlueprintButDevelopTissuesAfterBirth()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.NutrientRegeneration = .5f;
            var g = Genes(); var world = new Simulation(cfg, 42, new[] { Founder(g, energy: 24, age: 5) });
            double before = Stored(world); world.Step(); Assert.AreEqual(1, world.Births);
            var child = world.GetCell(1); Assert.AreEqual(g, child.Genes); Assert.AreEqual(1, child.ParentId);
            Assert.AreEqual(0, child.Specialization); Assert.AreEqual(0, child.Age);
            Assert.AreEqual(1, child.BondParentId); Assert.Less(Stored(world), before + cfg.NutrientRegeneration * 2 * 100 * cfg.StepSeconds);
            for (int t = 0; t < 100; t++) world.Step(); Assert.Greater(world.GetCell(1).Specialization, 0);
        }
        [Test] public void GeneticAndGlobalLimitsBoundGrowthAndFullColonyDispersesOffspring()
        {
            var cfg = Config(); cfg.Capacity = 24; cfg.MaxColonyCells = 4; cfg.InitialNutrients = 8; cfg.NutrientRegeneration = 2;
            var g = Genes(); g.ColonySize = 0; // inherited limit = two; global limit is deliberately larger
            var world = new Simulation(cfg, 42, new[] { Founder(g, energy: 24, age: 5) });
            bool dispersal = false;
            for (int t = 0; t < 2000; t++) {
                world.Step(); AssertTopology(world); Assert.LessOrEqual(world.Count, cfg.Capacity); Assert.LessOrEqual(world.LargestColony, 2);
                for (int i = 0; i < world.Count; i++) if (world.GetCell(i).Generation > 0 && world.GetCell(i).ColonyId == 0) dispersal = true;
            }
            Assert.Greater(world.Births, 10); Assert.IsTrue(dispersal);
        }
        [Test] public void LossOfAnchorDetachesSurvivorWithoutDanglingLinks()
        {
            var cfg = Config(); cfg.MaxAge = 3;
            var world = new Simulation(cfg, 42, new[] { Founder(Genes(), age: 2), Founder(Genes(), 4.45f) });
            bool formed = false;
            for (int t = 0; t < 45; t++) { world.Step(); formed |= world.ColonyCount > 0; AssertTopology(world); }
            Assert.IsTrue(formed); Assert.AreEqual(1, world.Count); Assert.AreEqual(2, world.GetCell(0).Id);
            Assert.AreEqual(0, world.GetCell(0).BondParentId); Assert.AreEqual(0, world.ColonyCount);
        }
        [TestCase(7u)] [TestCase(42u)] [TestCase(123u)]
        public void MulticellularClosedWorldConservesEnergyThroughBirthsTransportAndDeaths(uint seed)
        {
            var cfg = Config(); cfg.WorldSize = 10; cfg.MaxAge = 35; cfg.GridSize = 2; cfg.InitialNutrients = 8; cfg.InitialOrganicEnergy = 10;
            cfg.MutationProbability = 1; cfg.MutationStrength = .2f;
            var world = new Simulation(cfg, seed, new[] { Founder(Genes(), energy: 24, age: 5), Founder(Genes(), 4.45f, 20) });
            bool colony = false, transfer = false; double previous = Stored(world);
            for (int t = 0; t < 5000; t++) {
                world.Step(); double total = Stored(world);
                Assert.LessOrEqual(total, previous + .0001, $"Created energy at tick {world.Tick}"); previous = total;
                colony |= world.ColonyCount > 0; transfer |= world.EnergyTransferred > 0; AssertTopology(world);
            }
            Assert.IsTrue(colony); Assert.IsTrue(transfer); Assert.Greater(world.Births, 0); Assert.Greater(world.Deaths, 2); Assert.AreEqual(0, world.Count);
        }
        [Test] public void MulticellularReplayIncludesTopologyRolesAndEnergyTransport()
        {
            var cfg = Config(); cfg.InitialNutrients = 8; cfg.NutrientRegeneration = .5f; cfg.MaxAge = 20; cfg.MutationProbability = .5f;
            var founders = new[] { Founder(Genes(), energy: 24, age: 5), Founder(Genes(), 4.45f, 20) };
            var a = new Simulation(cfg, 42, founders); var b = new Simulation(cfg, 42, founders);
            bool colonies = false, transfers = false;
            for (int t = 0; t < 1200; t++) {
                if (t == 200) { a.SetEnvironment(40, .2f); b.SetEnvironment(40, .2f); }
                a.Step(); b.Step(); Assert.AreEqual(a.Count, b.Count); Assert.AreEqual(a.RandomState, b.RandomState);
                Assert.AreEqual(a.ColonyCount, b.ColonyCount); Assert.AreEqual(a.EnergyTransferred, b.EnergyTransferred);
                for (int i = 0; i < a.Count; i++) Assert.AreEqual(a.GetCell(i), b.GetCell(i));
                for (int i = 0; i < 100; i++) { Assert.AreEqual(a.GetNutrient(i), b.GetNutrient(i)); Assert.AreEqual(a.GetOrganicEnergy(i), b.GetOrganicEnergy(i)); }
                colonies |= a.ColonyCount > 0; transfers |= a.EnergyTransferred > 0;
            }
            Assert.IsTrue(colonies); Assert.IsTrue(transfers); Assert.Greater(a.Births, 0); Assert.Greater(a.Deaths, 0);
        }
        [Test] public void DensePopulationAndPeriodicBoundariesRemainValid()
        {
            // Regression: rounding position / bucket width could produce contactGrid at the upper edge.
            var cfg = new SimulationConfig { Capacity = 10000, InitialPopulation = 10000, GridSize = 64,
                InitialNutrients = 8, NutrientRegeneration = .5f, MaxAge = 10000 };
            var world = new Simulation(cfg, 42);
            for (int t = 0; t < 150; t++) {
                world.Step();
                if (t % 10 != 0) continue;
                for (int i = 0; i < world.Count; i++) {
                    var c = world.GetCell(i);
                    Assert.That(c.X, Is.GreaterThanOrEqualTo(0).And.LessThan(cfg.WorldSize));
                    Assert.That(c.Z, Is.GreaterThanOrEqualTo(0).And.LessThan(cfg.WorldSize));
                }
            }
            Assert.Greater(world.ColonyCount, 0);
        }
        [Test] public void BondingRequiresEnergyAndSpecializationRemainsOptional()
        {
            var cfg = Config(); cfg.StepSeconds = .5f;
            var world = new Simulation(cfg, 42, new[] { Founder(Genes(), energy: .15f), Founder(Genes(), 4.45f, .15f) });
            world.Step(); Assert.AreEqual(0, world.ColonyCount);
            var g = Genes(); g.Differentiation = g.Transport = 0;
            world = new Simulation(cfg, 42, new[] { Founder(g), Founder(g, 4.45f) });
            world.Step(); Assert.AreEqual(1, world.ColonyCount);
            var before = world.GetCell(0); world.Step(); var after = world.GetCell(0);
            float dx = Distance(after.X, before.X, cfg.WorldSize), dz = Distance(after.Z, before.Z, cfg.WorldSize);
            Assert.That(Math.Sqrt(dx * dx + dz * dz) / cfg.StepSeconds, Is.EqualTo(g.Speed / 1.09).Within(.00001));
            for (int t = 0; t < 20; t++) world.Step();
            Assert.AreEqual(0, world.EnergyTransferred);
            for (int i = 0; i < world.Count; i++) {
                Assert.AreEqual(0, world.GetCell(i).Specialization); Assert.AreEqual(CellRole.Generalist, world.GetCell(i).Role);
            }
        }
        [Test] public void InvalidColonyLimitsAndExperimentInputsAreRejected()
        {
            var cfg = Config(); cfg.MaxColonyCells = 1; Assert.Throws<ArgumentException>(() => new Simulation(cfg, 1));
            cfg.MaxColonyCells = 65; Assert.Throws<ArgumentException>(() => new Simulation(cfg, 1));
            cfg.MaxColonyCells = 16; var bad = Founder(Genes()); bad.Genes.Transport = float.NaN;
            Assert.Throws<ArgumentException>(() => new Simulation(cfg, 1, new[] { bad }));
        }
        static double Stored(Simulation world)
        {
            double total = 0;
            for (int i = 0; i < world.Config.GridSize * world.Config.GridSize; i++) total += 2.0 * world.GetNutrient(i) + world.GetOrganicEnergy(i);
            for (int i = 0; i < world.Count; i++) { var c = world.GetCell(i); total += c.Energy + Biology.StructureEnergy(c.Genes); }
            return total;
        }
    }
}
