using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class ObservationTests
    {
        [Test] public void ZoomIsMultiplicativeAndNeverCrossesDistanceLimits()
        {
            Assert.That(Observation.Zoom(20, 1, 40), Is.LessThan(20));
            Assert.That(Observation.Zoom(20, -1, 40), Is.GreaterThan(20));
            Assert.That(Observation.Zoom(20, 10000, 40), Is.EqualTo(2));
            Assert.That(Observation.Zoom(20, -10000, 40), Is.EqualTo(66));
        }
        [Test] public void OppositeZoomStepsRestoreDistanceAwayFromLimits()
        { Assert.That(Observation.Zoom(Observation.Zoom(20, 1, 40), -1, 40), Is.EqualTo(20).Within(.0001f)); }
        [Test] public void PeriodicMotionUsesShortestDisplacement()
        {
            Assert.That(Observation.PeriodicDelta(-39, 40), Is.EqualTo(1));
            Assert.That(Observation.PeriodicDelta(39, 40), Is.EqualTo(-1));
        }
        [Test] public void ColonySelectionIncludesAllMembersButNotOtherColoniesOrSingleCells()
        {
            var anchor = new Cell { Id = 1, ColonyId = 1 };
            Assert.That(Observation.SameOrganism(anchor, new Cell { Id = 2, ColonyId = 1 }), Is.True);
            Assert.That(Observation.SameOrganism(anchor, new Cell { Id = 3, ColonyId = 3 }), Is.False);
            Assert.That(Observation.SameOrganism(anchor, new Cell { Id = 4 }), Is.False);
        }
        [Test] public void SingleCellSelectionDoesNotGroupUnrelatedSolitaryCells()
        {
            var anchor = new Cell { Id = 2 };
            Assert.That(Observation.SameOrganism(anchor, anchor), Is.True);
            Assert.That(Observation.SameOrganism(anchor, new Cell { Id = 3 }), Is.False);
        }
        [Test] public void RayPickerFindsFrontSurfaceAndRejectsMissesAndObjectsBehindCamera()
        {
            Assert.That(Observation.RaySphere(0, 0, -5, 0, 0, 1, 1), Is.EqualTo(4));
            Assert.That(Observation.RaySphere(3, 0, -5, 0, 0, 1, 1), Is.EqualTo(float.PositiveInfinity));
            Assert.That(Observation.RaySphere(0, 0, 5, 0, 0, 1, 1), Is.EqualTo(float.PositiveInfinity));
            Assert.That(Observation.RaySphere(0, 0, 0, 0, 0, 1, 1), Is.EqualTo(1));
        }
        [Test] public void LookupUsesStableIdAfterDeathsCompactTheArray()
        {
            var cfg = new SimulationConfig { InitialPopulation = 0, MaxAge = 10 };
            var g = new Genome { Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22 };
            var sim = new Simulation(cfg, 42, new[] {
                new Cell { Genes = g, Energy = 10, Age = 9.99f, X = 10, Z = 10 },
                new Cell { Genes = g, Energy = 10, X = 20, Z = 20 } });
            sim.Step();
            Assert.That(Observation.TryFind(sim, 1, out _), Is.False);
            Assert.That(Observation.TryFind(sim, 2, out var cell), Is.True);
            Assert.That(cell.Id, Is.EqualTo(2));
            Assert.That(sim.GetCell(0).Id, Is.EqualTo(2));
        }
        [Test] public void ObservationDoesNotAdvanceSimulationOrConsumeRandomness()
        {
            var sim = new Simulation(new SimulationConfig(), 42);
            var random = sim.RandomState; var tick = sim.Tick;
            Assert.That(Observation.TryFind(sim, 1, out var cell), Is.True);
            Observation.SameOrganism(cell, cell); Observation.MutationMask(cell.Genes, cell.Genes);
            Assert.That(sim.RandomState, Is.EqualTo(random)); Assert.That(sim.Tick, Is.EqualTo(tick));
        }
        [Test] public void MutationMaskReportsOnlyTraitsThatActuallyChanged()
        {
            var parent = new Genome { Size = 1 }; var child = parent;
            Assert.That(Observation.MutationMask(parent, child), Is.Zero);
            child.Size = 1.1f; child.Transport = .1f;
            Assert.That(Observation.MutationMask(parent, child), Is.EqualTo(2 | 512));
        }
        [Test] public void BirthMetadataMatchesInheritedGenomesWithoutChangingRandomSequence()
        {
            var cfg = new SimulationConfig { Capacity = 4, InitialPopulation = 0, MutationProbability = 1 };
            var genes = new Genome { Size = 1, Speed = 1, Uptake = 1, PreferredTemperature = 22 };
            var sim = new Simulation(cfg, 42, new[] { new Cell { Genes = genes, Age = 6, Energy = 24, X = 20, Z = 20 } });
            sim.Step(); Assert.That(sim.Births, Is.EqualTo(1));
            var child = sim.GetCell(1);
            Assert.That(child.BirthMutationMask, Is.EqualTo(Observation.MutationMask(genes, child.Genes)));
            Assert.That(child.BirthMutationMask, Is.Not.Zero);
        }
    }
}
