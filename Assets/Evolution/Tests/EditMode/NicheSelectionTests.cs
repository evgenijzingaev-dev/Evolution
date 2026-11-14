using System;
using Evolution.Volume;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class NicheSelectionTests
    {
        [TestCase(EcologicalScenario.Sunlit)] [TestCase(EcologicalScenario.Dark)]
        [TestCase(EcologicalScenario.Hydrothermal)] [TestCase(EcologicalScenario.Natural)]
        public void SixHundredSecondsSelectDifferentInheritedStrategiesWithClosedLedgers(EcologicalScenario scenario)
        {
            var w=NicheExperiment.Create(scenario);var initial=VolumeSnapshot.Read(w);
            for(int tick=0;tick<12000;tick++) {
                NicheExperiment.Step(w);if(tick%200!=0)continue;var s=VolumeSnapshot.Read(w);var d=w.Diagnostics;
                double energy=initial.Stored+d.ExternalNutrients+d.ExternalOrganic+d.Solar;
                Assert.AreEqual(energy-d.Dissipated,s.Stored,energy*.00005);
                Assert.AreEqual(d.InitialMatter+d.ExternalMatter-d.MatterLost,s.Matter,(d.InitialMatter+d.ExternalMatter)*.0002);
                Assert.AreEqual(d.InitialOxygen+d.OxygenIn+d.OxygenProduced-d.OxygenOut-d.OxygenUsed,s.Oxygen,(d.InitialOxygen+d.OxygenIn+d.OxygenProduced)*.0002);
                for(int k=0;k<w.FoodCount;k++){var f=w.GetFood(k);Assert.That(f.Nutrients,Is.GreaterThanOrEqualTo(0));Assert.That(f.Dissolved,Is.GreaterThanOrEqualTo(0));Assert.IsFalse(float.IsNaN(f.Oxygen+f.Substrate+f.Organic));}
            }
            var last=VolumeSnapshot.Read(w);Assert.Greater(last.Alive,0);Assert.Greater(last.MaxGeneration,1);Assert.Greater(w.Diagnostics.Births,10);
            int photo=0,organic=0,hot=0;for(int i=0;i<w.Count;i++){var a=w.Get(i);if(!a.Alive)continue;var g=a.Biology.Genes;if(g.Photosynthesis>.6f)photo++;if(g.OrganicSpecialization>.6f)organic++;if(g.PreferredTemperature>32)hot++;}
            if(scenario==EcologicalScenario.Sunlit)Assert.Greater(photo/(float)last.Alive,.6f);
            if(scenario==EcologicalScenario.Dark){Assert.Greater(organic/(float)last.Alive,.6f);Assert.AreEqual(0,w.Diagnostics.Solar);}
            if(scenario==EcologicalScenario.Hydrothermal)Assert.Greater(hot/(float)last.Alive,.6f);
            if(scenario==EcologicalScenario.Natural){Assert.Greater(photo,0);Assert.Greater(hot,0);}
            TestContext.WriteLine($"{scenario}: alive={last.Alive}, births={w.Diagnostics.Births}, deaths={w.Diagnostics.Deaths}, G={last.MaxGeneration}, photo={photo}, organic={organic}, hot={hot}");
        }
        [Test] public void SameMixtureFacesEveryEnvironmentAndAdultDnaDoesNotChange()
        {
            var a=NicheExperiment.Create(EcologicalScenario.Sunlit);var b=NicheExperiment.Create(EcologicalScenario.Hydrothermal);
            for(int i=0;i<a.Count;i++){Assert.AreEqual(a.Get(i).Biology.Genes,b.Get(i).Biology.Genes);Assert.AreEqual(a.Get(i).Position,b.Get(i).Position);}
            var genes=a.Get(0).Biology.Genes;for(int tick=0;tick<600;tick++)a.Step();Assert.AreEqual(genes,a.Get(0).Biology.Genes);
        }
        [Test] public void SeededNicheBiologyRepeatsExactlyIncludingBirthsDeathsAndResourceStocks()
        {
            var a=NicheExperiment.Create(EcologicalScenario.Natural,7);var b=NicheExperiment.Create(EcologicalScenario.Natural,7);
            for(int tick=0;tick<2000;tick++) {
                NicheExperiment.Step(a);NicheExperiment.Step(b);if(tick%40!=0)continue;
                Assert.AreEqual(a.Count,b.Count);for(int i=0;i<a.Count;i++)Assert.AreEqual(a.Get(i),b.Get(i));
                for(int k=0;k<a.FoodCount;k++)Assert.AreEqual(a.GetFood(k),b.GetFood(k));
                Assert.AreEqual(a.Life.Latest,b.Life.Latest);Assert.AreEqual(a.Diagnostics.Solar,b.Diagnostics.Solar);
            }
        }
        [Test] public void NoExternalEnergyInDarkWaterCannotSustainInfiniteResourceCycling()
        {
            var w=NicheExperiment.Create(EcologicalScenario.Dark);w.NutrientSupply=false;double initial=VolumeSnapshot.Read(w).Stored;
            for(int tick=0;tick<12000;tick++)NicheExperiment.Step(w);
            Assert.AreEqual(0,VolumeSnapshot.Read(w).Alive);Assert.AreEqual(0,w.Diagnostics.Solar);
            Assert.AreEqual(0,w.Diagnostics.ExternalNutrients+w.Diagnostics.ExternalOrganic+w.Diagnostics.ExternalMatter);
            Assert.Less(VolumeSnapshot.Read(w).Stored,initial);Assert.Greater(w.Diagnostics.DecayLoss,0);
        }
        [Test] public void PresentationObservationAndLodRoundTripDoNotSpendOrDuplicateEcology()
        {
            var archive=new MemoryRegionArchive();var ocean=new ChunkOcean(42,archive,EcologicalScenario.Natural);
            for(int tick=0;tick<20;tick++)ocean.World.Step();var before=ocean.Snapshot(out _);var initial=ocean.World.Diagnostics.InitialMatter;
            for(int frame=0;frame<120;frame++){ocean.Survey.Refresh(ocean.World,ocean.LoadedKeys);NicheReadout.Explain(ocean.World,0);ocean.World.EnvironmentAt(ocean.World.Get(0).Position);}
            ocean.Save();ocean.Stream(new V3(21,0,0));ocean.Stream(default);var restored=new ChunkOcean(42,archive);
            // Discovery can add explicitly credited initial stock. Repeated observation must not.
            Assert.AreEqual(ocean.World.Diagnostics.InitialMatter,restored.World.Diagnostics.InitialMatter);
            var total=restored.Snapshot(out _);var d=restored.World.Diagnostics;
            Assert.AreEqual(d.InitialEnergy+d.Solar+d.ExternalNutrients+d.ExternalOrganic-d.Dissipated,total.Stored,d.InitialEnergy*.0001);
            Assert.GreaterOrEqual(d.InitialMatter,initial);Assert.Greater(before.Alive,0);
        }
    }
}
