using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class LineageHistoryTests
    {
        static Cell Founder() => new Cell { Genes = new Genome {Size=1,Speed=1,Uptake=1,PreferredTemperature=22}, Energy=24,Age=5,X=2,Z=2 };
        [Test] public void ArchiveRecordsRealBirthsInheritedGenesAndDeathsWithoutAdvancingRandomness()
        {
            var cfg = new SimulationConfig { InitialPopulation=0,Capacity=4,MutationProbability=1,MutationStrength=.05f,
                MaxAge=5.1f,InitialNutrients=0,NutrientRegeneration=0 };
            var s = new Simulation(cfg,42,new[] {Founder()}); s.TryControl(1); uint random = s.RandomState;
            Assert.IsTrue(s.History.TryFind(1,out var founder)); Assert.AreEqual(-1,founder.DiedAtTick);
            Assert.AreEqual(random,s.RandomState); Assert.IsTrue(s.RequestDivision()); s.Step();
            Assert.IsTrue(s.History.TryFind(s.LastOffspringId,out var child));
            Assert.AreEqual(1,child.ParentId); Assert.AreEqual(1,child.Generation);
            Assert.IsTrue(Observation.TryFind(s,child.Id,out var live));
            Assert.AreEqual(founder.Genes,live.ParentGenes); Assert.AreEqual(live.Genes,child.Genes);
            Assert.AreEqual(Observation.MutationMask(founder.Genes,child.Genes),child.MutationMask);
            for (int i = 0; i < 4; i++) s.Step();
            Assert.IsTrue(s.History.TryFind(1,out founder)); Assert.Greater(founder.DiedAtTick,0);
            Assert.IsFalse(Observation.TryFind(s,1,out _)); Assert.AreEqual(2,s.History.Count);
        }
        [Test] public void ArchiveIsBoundedAndEvictionDoesNotRemoveLivingCells()
        {
            var cfg = new SimulationConfig {InitialPopulation=600,Capacity=600}; var s = new Simulation(cfg,42);
            Assert.AreEqual(512,s.History.Count); Assert.IsFalse(s.History.TryFind(1,out _));
            Assert.IsTrue(s.History.TryFind(600,out _)); Assert.IsTrue(Observation.TryFind(s,1,out _));
            Assert.AreEqual(600,s.Count); Assert.AreEqual(600,s.History.Recent(0).Id);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.History.Recent(512));
        }
    }
}
