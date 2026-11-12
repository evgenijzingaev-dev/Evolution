using System;
using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class ResourceCycleTests
    {
        static Genome Genes(float size = 1, float organic = 0) => new Genome {
            Size = size, Speed = 1, Uptake = 1, PreferredTemperature = 22, OrganicSpecialization = organic };

        [Test] public void SmallerOffspringCapacityLimitsTransferWithoutDestroyingParentsReserve()
        {
            var parent = new Cell { Genes = Genes(2), Age = 5, Energy = 48 };
            var child = new Cell { Genes = Genes(.4f) };
            Assert.IsTrue(Biology.TryDivide(ref parent, ref child));
            Assert.That(child.Energy, Is.EqualTo(4.8f).Within(.00001));
            Assert.That(parent.Energy, Is.EqualTo(42.2f).Within(.00001));
            Assert.That(parent.Energy + child.Energy + Biology.StructureEnergy(child.Genes),
                Is.EqualTo(48 - Biology.BirthOverhead(child.Genes)).Within(.00001));
        }

        [Test] public void LargerOffspringMustBeFundedIncludingItsStructure()
        {
            var parent = new Cell { Genes = Genes(.4f), Age = 5, Energy = 9.6f };
            var child = new Cell { Genes = Genes(2) };
            Assert.IsTrue(Biology.TryDivide(ref parent, ref child));
            Assert.That(parent.Energy, Is.EqualTo(2.3f).Within(.00001));
            Assert.That(child.Energy, Is.EqualTo(2.3f).Within(.00001));
            Assert.That(parent.Energy + child.Energy + Biology.StructureEnergy(child.Genes),
                Is.EqualTo(8.6f).Within(.00001));
        }

        [Test] public void UnfundedOrImmatureBirthLeavesBothCellsUntouched()
        {
            var parent = new Cell { Id = 1, Genes = Genes(.4f), Age = 5, Energy = 4 };
            var child = new Cell { Id = 2, Genes = Genes(2), Energy = 1 };
            var beforeParent = parent; var beforeChild = child;
            Assert.IsFalse(Biology.TryDivide(ref parent, ref child));
            Assert.AreEqual(beforeParent, parent); Assert.AreEqual(beforeChild, child);
            parent.Energy = 24; parent.Age = 4; beforeParent = parent;
            Assert.IsFalse(Biology.TryDivide(ref parent, ref child));
            Assert.AreEqual(beforeParent, parent); Assert.AreEqual(beforeChild, child);
        }

        [Test] public void OrganicSpecializationHasBothFeedingAdvantageAndCosts()
        {
            var basal = Genes(); var specialist = Genes(organic: 1);
            Assert.That(Biology.OrganicAssimilation(basal, 22), Is.EqualTo(.2f).Within(.00001));
            Assert.That(Biology.OrganicAssimilation(specialist, 22), Is.EqualTo(.85f).Within(.00001));
            Assert.Less(Biology.SubstrateYield(specialist, 22), Biology.SubstrateYield(basal, 22));
            Assert.Greater(Biology.Cost(specialist, 22), Biology.Cost(basal, 22));
            var a = new Cell { Genes = basal, Energy = 10 }; var b = new Cell { Genes = specialist, Energy = 10 };
            float substrateA = 0, substrateB = 0, organicA = 10, organicB = 10;
            Biology.Feed(ref a, ref substrateA, ref organicA, 22, 1);
            Biology.Feed(ref b, ref substrateB, ref organicB, 22, 1);
            Assert.Greater(b.Energy, a.Energy);
            Assert.That(b.Energy - 10, Is.EqualTo((10 - organicB) * .85f).Within(.00001));
            Assert.That(organicB, Is.EqualTo(8).Within(.00001));
        }

        [Test] public void MixedFeedingSharesUptakeBudgetAndCannotConsumeNegativeReserves()
        {
            var c = new Cell { Genes = Genes(organic: .5f), Energy = 10 };
            float substrate = 10, organic = 10;
            Biology.Feed(ref c, ref substrate, ref organic, 22, 1);
            float spentSubstrate = 10 - substrate, spentOrganic = 10 - organic;
            Assert.That(spentSubstrate + spentOrganic / 2, Is.EqualTo(1).Within(.00001));
            Assert.That(c.Energy - 10, Is.EqualTo(spentSubstrate * 1.35f + spentOrganic * .525f).Within(.00001));
            substrate = .001f; organic = .001f;
            Biology.Feed(ref c, ref substrate, ref organic, 22, 1);
            Assert.AreEqual(0, substrate); Assert.AreEqual(0, organic);
            float energy = c.Energy;
            Biology.Feed(ref c, ref substrate, ref organic, 22, 1);
            Assert.AreEqual(energy, c.Energy);
        }

        [Test] public void FullCellLeavesFoodAndNearlyFullCellConsumesOnlyWhatItCanStore()
        {
            var c = new Cell { Genes = Genes(organic: 1), Energy = 24 };
            float substrate = 10, organic = 10;
            Biology.Feed(ref c, ref substrate, ref organic, 22, 1);
            Assert.AreEqual(10, substrate); Assert.AreEqual(10, organic); Assert.AreEqual(24, c.Energy);
            c.Energy = 23.9f;
            Biology.Feed(ref c, ref substrate, ref organic, 22, 1);
            Assert.That(c.Energy, Is.EqualTo(24).Within(.00001));
            Assert.That(10 - organic, Is.EqualTo(.1f / .85f).Within(.00001));
            Assert.That(substrate, Is.EqualTo(10).Within(.00001));
        }

        [Test] public void OrganicFeedingAndDecayOccurInActualWorld()
        {
            var cfg = new SimulationConfig { InitialPopulation = 1, InitialNutrients = 0,
                NutrientRegeneration = 0, InitialOrganicEnergy = 3, OrganicDecayRate = .2f };
            var world = new Simulation(cfg, 42); var before = world.GetCell(0);
            world.Step();
            double organic = 0;
            for (int i = 0; i < cfg.GridSize * cfg.GridSize; i++) organic += world.GetOrganicEnergy(i);
            double afterDecay = 3 * (1 - .2f * cfg.StepSeconds) * (cfg.GridSize * cfg.GridSize);
            double consumed = before.Genes.Uptake * cfg.StepSeconds * 2 * (.2f + .8f * before.Genes.OrganicSpecialization);
            Assert.That(afterDecay - organic, Is.EqualTo(consumed).Within(.0002));
            Assert.That(world.GetCell(0).Energy, Is.EqualTo(before.Energy + consumed *
                Biology.OrganicAssimilation(before.Genes, 22) - Biology.Cost(before.Genes, 22) * cfg.StepSeconds).Within(.00001));
        }

        [Test] public void DeathsReturnOnlyFundedEnergyAndRemainsBecomeAvailableNextTick()
        {
            // More cells than tiles ensures shared tiles. None may eat this tick's deaths.
            var cfg = new SimulationConfig { InitialPopulation = 20, GridSize = 2, WorldSize = 1,
                InitialNutrients = 0, NutrientRegeneration = 0, MaxAge = .05f, OrganicDecayRate = .1f };
            var world = new Simulation(cfg, 42); double expected = 0;
            for (int i = 0; i < world.Count; i++) {
                var c = world.GetCell(i);
                expected += .75f * (c.Energy - Biology.Cost(c.Genes, 22) * cfg.StepSeconds + 2 * c.Genes.Size);
            }
            world.Step(); Assert.AreEqual(0, world.Count); Assert.AreEqual(20, world.Deaths);
            Assert.That(Stored(world, cfg.GridSize), Is.EqualTo(expected).Within(.0001));
            world.Step();
            Assert.That(Stored(world, cfg.GridSize), Is.EqualTo(expected * (1 - .1f * cfg.StepSeconds)).Within(.0001));
            Assert.AreEqual(20, world.Deaths);
        }

        [TestCase(7u)] [TestCase(42u)] [TestCase(123u)]
        public void ClosedWorldBirthDeathAndRecyclingNeverCreateEnergy(uint seed)
        {
            var cfg = new SimulationConfig { Capacity = 128, InitialPopulation = 6, GridSize = 2, WorldSize = 1,
                InitialNutrients = 8, InitialOrganicEnergy = 12, NutrientRegeneration = 0,
                MaxAge = 20, MutationProbability = 1, MutationStrength = .3f };
            var world = new Simulation(cfg, seed); double previous = Stored(world, cfg.GridSize);
            bool consumedRemains = false;
            for (int tick = 0; tick < 10000; tick++) {
                double organicBefore = 0;
                for (int i = 0; i < 4; i++) organicBefore += world.GetOrganicEnergy(i);
                long deaths = world.Deaths;
                world.Step(); double total = Stored(world, cfg.GridSize);
                Assert.That(total, Is.LessThanOrEqualTo(previous + .0001), $"Energy created on tick {world.Tick}");
                for (int i = 0; i < 4; i++) {
                    Assert.That(world.GetNutrient(i), Is.InRange(0, cfg.NutrientCapacity));
                    Assert.GreaterOrEqual(world.GetOrganicEnergy(i), 0);
                }
                double organicAfter = 0;
                for (int i = 0; i < 4; i++) organicAfter += world.GetOrganicEnergy(i);
                if (world.Deaths > 0 && deaths == world.Deaths && organicAfter < organicBefore * (1 - cfg.OrganicDecayRate * cfg.StepSeconds) - .0001)
                    consumedRemains = true;
                for (int i = 0; i < world.Count; i++)
                    Assert.That(world.GetCell(i).Energy, Is.InRange(0, Biology.Capacity(world.GetCell(i).Genes)));
                previous = total;
            }
            Assert.Greater(world.Births, 0); Assert.Greater(world.Deaths, cfg.InitialPopulation);
            Assert.IsTrue(consumedRemains); Assert.AreEqual(0, world.Count);
        }

        [Test] public void ReplayIncludesMixedResourcesMutationsBirthsAndDeaths()
        {
            var cfg = new SimulationConfig { InitialPopulation = 6, GridSize = 2, WorldSize = 1,
                InitialNutrients = 8, InitialOrganicEnergy = 12, MaxAge = 20 };
            var a = new Simulation(cfg, 42); var b = new Simulation(cfg, 42);
            for (int tick = 0; tick < 2000; tick++) {
                if (tick == 300) { a.SetEnvironment(35, .2f); b.SetEnvironment(35, .2f); }
                a.Step(); b.Step();
            }
            Assert.Greater(a.Births, 0); Assert.Greater(a.Deaths, 0);
            Assert.AreEqual(a.Tick, b.Tick); Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.RandomState, b.RandomState); Assert.AreEqual(a.Births, b.Births); Assert.AreEqual(a.Deaths, b.Deaths);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a.GetCell(i), b.GetCell(i));
            for (int i = 0; i < 4; i++) {
                Assert.AreEqual(a.GetOrganicEnergy(i), b.GetOrganicEnergy(i));
                Assert.AreEqual(a.GetNutrient(i), b.GetNutrient(i));
            }
        }

        [Test] public void DietGeneMutatesAndZeroStrengthPreservesIt()
        {
            var rng = new RandomSource(19); var parent = Genes(organic: .5f);
            var child = parent.Inherit(ref rng, 1, .2f);
            Assert.AreNotEqual(parent.OrganicSpecialization, child.OrganicSpecialization);
            Assert.That(child.OrganicSpecialization, Is.InRange(0, 1));
            Assert.AreEqual(parent, parent.Inherit(ref rng, 1, 0));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidOrganicConfigurationIsRejected(float value)
        {
            Assert.Throws<ArgumentException>(() => new Simulation(new SimulationConfig { InitialOrganicEnergy = value }, 1));
            Assert.Throws<ArgumentException>(() => new Simulation(new SimulationConfig { OrganicDecayRate = value }, 1));
        }

        // Independent accounting: substrate's chemical energy + finite organics + reserves + paid structures.
        static double Stored(Simulation world, int gridSize)
        {
            double result = 0;
            for (int i = 0; i < gridSize * gridSize; i++) result += 2.0 * world.GetNutrient(i) + world.GetOrganicEnergy(i);
            for (int i = 0; i < world.Count; i++) {
                var c = world.GetCell(i); result += c.Energy + 2.0 * c.Genes.Size;
            }
            return result;
        }
    }
}
