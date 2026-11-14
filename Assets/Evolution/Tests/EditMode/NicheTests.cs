using System;
using System.IO;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class NicheTests
    {
        [Test] public void FieldsAreContinuousAcrossNegativeAndPositiveChunkBoundaries()
        {
            Assert.AreEqual(0,NicheEnvironment.Kernel(default,new FoodCloud()));
            foreach(float edge in new[] {-40f,-20,0,20,40}) {
                var a=NicheEnvironment.Sample(new V3(edge-.0001f,-18,0),0,default,EcologicalScenario.Natural);
                var b=NicheEnvironment.Sample(new V3(edge+.0001f,-18,0),0,default,EcologicalScenario.Natural);
                Assert.Less(Math.Abs(a.Light-b.Light),.001);Assert.Less(Math.Abs(a.Temperature-b.Temperature),.001);
                Assert.Less(Math.Abs(a.Chemicals-b.Chemicals),.001);Assert.Less((a.Current-b.Current).Length,.001);
            }
            Assert.Greater(NicheEnvironment.Sample(new V3(0,24,0),0,default,EcologicalScenario.Natural).Light,NicheEnvironment.Sample(new V3(0,-80,0),0,default,EcologicalScenario.Natural).Light);
            Assert.Greater(NicheEnvironment.Sample(new V3(12,-18,-8),0,default,EcologicalScenario.Natural).Temperature,35);
        }
        [Test] public void ConservativeMixingAndDecayCannotManufactureStocks()
        {
            var a=new FoodCloud {Position=default,Radius=3,Nutrients=10,Substrate=20,Dissolved=30,Oxygen=40,Organic=15,DetritalMatter=8};
            var b=new FoodCloud {Position=new V3(1,0,0),Radius=3,Nutrients=2,Substrate=3,Dissolved=4,Oxygen=5};
            for(int i=0;i<100;i++)NicheResources.Mix(ref a,ref b,.05f);
            Assert.AreEqual(12,a.Nutrients+b.Nutrients,1e-5);Assert.AreEqual(23,a.Substrate+b.Substrate,1e-5);Assert.AreEqual(34,a.Dissolved+b.Dissolved,1e-4);
            double energy=a.Organic+a.Dissolved+a.Substrate*2,matter=a.Nutrients+a.DetritalMatter,oxygen=a.Oxygen;
            var d=new VolumeDiagnostics();NicheResources.Advance(ref a,new NicheSample {Oxygen=a.Oxygen/80},100,d,false);
            Assert.AreEqual(energy-d.DecayLoss,a.Organic+a.Dissolved+a.Substrate*2,1e-4);
            Assert.AreEqual(matter-d.MatterLost,a.Nutrients+a.DetritalMatter,1e-4);
            Assert.AreEqual(oxygen+d.OxygenIn-d.OxygenOut-d.OxygenUsed,a.Oxygen,1e-4);
        }
        [Test] public void InitializationIsOnceOnlyAndPartitionDoesNotAddEnergy()
        {
            var f=new FoodCloud {Organic=50,Substrate=20};var d=new VolumeDiagnostics();
            NicheResources.Initialize(ref f,new NicheSample {Nutrients=1,Oxygen=1},d);
            Assert.AreEqual(90,f.Organic+f.Dissolved+f.Substrate*2);double introduced=d.InitialMatter;
            f.Nutrients=0;NicheResources.Initialize(ref f,new NicheSample {Nutrients=1,Oxygen=1},d);
            Assert.AreEqual(0,f.Nutrients);Assert.AreEqual(introduced,d.InitialMatter);
        }
        [Test] public void VersionSixPreservesFiniteStocksAndProfile()
        {
            string folder=Path.Combine(Path.GetTempPath(),"niche-save-"+Guid.NewGuid());
            try {
                var archive=new BinaryRegionArchive(folder);var ocean=new ChunkOcean(42,archive,EcologicalScenario.Natural);ocean.Save();
                var a=ocean.World.Get(0);var f=ocean.World.GetFood(0);var before=ocean.Snapshot(out _);
                var reloaded=new ChunkOcean(42,new BinaryRegionArchive(folder));Assert.AreEqual(EcologicalScenario.Natural,reloaded.World.Scenario);
                Assert.AreEqual(a,reloaded.World.Get(reloaded.Find(a.Identity)));
                var after=reloaded.Snapshot(out _);Assert.AreEqual(before.Stored,after.Stored,1e-5);Assert.AreEqual(before.Matter,after.Matter,1e-5);
                Assert.AreEqual(ocean.World.Diagnostics.InitialMatter,reloaded.World.Diagnostics.InitialMatter);
            } finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        static VolumeAgent Consumer(float photo=.8f,float organic=.2f,float optimum=23,float tolerance=.3f)
        {
            var g=new Genome {Size=1,Speed=.4f,Uptake=1.2f,Photosynthesis=photo,OrganicSpecialization=organic,PreferredTemperature=optimum,ThermalTolerance=tolerance,EnergyEfficiency=.5f};
            return new VolumeAgent {NicheInitialized=true,Matter=1,MineralReserve=.3f,Biology=new Cell {Genes=g,Energy=2,Age=8,Growth=1,Integrity=1}};
        }
        [Test] public void PhotosynthesisRequiresLightAndFiniteMineralsAndChangesOxygen()
        {
            var a=Consumer();var f=new FoodCloud {Nutrients=20,Oxygen=40};var d=new VolumeDiagnostics();
            NicheMetabolism.Feed(ref a,ref f,new NicheSample {Light=1,Temperature=23},1,d);
            Assert.Greater(a.Biology.Budget.Light,0);Assert.Greater(d.OxygenProduced,0);Assert.Less(f.Nutrients,20);
            var dark=Consumer();var stock=new FoodCloud {Nutrients=20,Oxygen=40};NicheMetabolism.Feed(ref dark,ref stock,new NicheSample {Temperature=23},1,null);
            Assert.AreEqual(0,dark.Biology.Budget.Light);
            var dry=Consumer();dry.MineralReserve=0;stock=new FoodCloud {Oxygen=40};NicheMetabolism.Feed(ref dry,ref stock,new NicheSample {Light=1,Temperature=23},1,null);
            Assert.AreEqual(0,dry.Biology.Budget.Light);
        }
        [Test] public void UptakeForecastNeverMutatesGenomeAndAllFoodPathsPayForTheirGain()
        {
            var a=Consumer(.6f,.7f);var genome=a.Biology.Genes;
            var f=new FoodCloud {Nutrients=12,Oxygen=40,Substrate=10,Organic=12,Dissolved=8};
            float energy=a.Biology.Energy+f.Substrate*2+f.Organic+f.Dissolved;
            float matter=a.Matter+a.MineralReserve+f.Nutrients;var d=new VolumeDiagnostics();
            NicheMetabolism.Feed(ref a,ref f,new NicheSample {Light=1,Temperature=23},2,d);
            Assert.AreEqual(genome,a.Biology.Genes);Assert.AreEqual(matter,a.Matter+a.MineralReserve+f.Nutrients,1e-5);
            Assert.AreEqual(energy+d.Solar-d.AssimilationLoss,a.Biology.Energy+f.Substrate*2+f.Organic+f.Dissolved,1e-5);
            Assert.Greater(d.OxygenUsed,0);Assert.Less(f.Dissolved,8);Assert.Greater(a.Biology.Budget.Organic,0);
        }
        [Test] public void ThermalToleranceAndPhotosystemsHaveRealCostsAndConditionalBenefits()
        {
            var a=Consumer();var b=a;b.Biology.Genes.ThermalTolerance=.9f;
            Assert.Greater(Biology.Cost(b.Biology.Genes,23),Biology.Cost(a.Biology.Genes,23));
            Assert.Greater(NicheMetabolism.Thermal(b.Biology.Genes,43),NicheMetabolism.Thermal(a.Biology.Genes,43));
            var plain=a.Biology.Genes;plain.Photosynthesis=0;Assert.Greater(Biology.Cost(a.Biology.Genes,23),Biology.Cost(plain,23));
            Assert.That(NicheMetabolism.Thermal(a.Biology.Genes,23.001f),Is.EqualTo(NicheMetabolism.Thermal(a.Biology.Genes,22.999f)).Within(1e-6));
        }
        [Test] public void LowOxygenActuallyLimitsOrganicAndChemicalAssimilation()
        {
            var a=Consumer(0,.8f);var b=a;var food=new FoodCloud {Dissolved=20,Substrate=20,Oxygen=80};var poor=food;poor.Oxygen=0;
            var env=new NicheSample {Temperature=23};NicheMetabolism.Feed(ref a,ref food,env,1,null);NicheMetabolism.Feed(ref b,ref poor,env,1,null);
            Assert.Greater(a.Biology.Energy,b.Biology.Energy);
        }
        [Test] public void MovingBetweenConditionsDoesNotRewriteAdultDna()
        {
            var w=NicheExperiment.Create(EcologicalScenario.Natural);var genes=new Genome[w.Count];for(int i=0;i<w.Count;i++)genes[i]=w.Get(i).Biology.Genes;
            for(int tick=0;tick<120;tick++)NicheExperiment.Step(w);
            for(int i=0;i<genes.Length;i++)Assert.AreEqual(genes[i],w.Get(i).Biology.Genes);
        }
        [Test] public void PaidOffspringAndGrowthConserveLimitingMatterAndDeathDoesNotDuplicateIt()
        {
            var w=NicheExperiment.Create(EcologicalScenario.Sunlit);var d=w.Diagnostics;
            var first=VolumeSnapshot.Read(w);double initial=first.Stored;
            for(int tick=0;tick<1200;tick++) {
                NicheExperiment.Step(w);if(tick%50!=0)continue;var state=VolumeSnapshot.Read(w);
                Assert.AreEqual(d.InitialMatter+d.ExternalMatter-d.MatterLost,state.Matter,(d.InitialMatter+d.ExternalMatter)*.0002);
                double input=initial+d.Solar+d.ExternalNutrients+d.ExternalOrganic;
                Assert.AreEqual(input-d.Dissipated,state.Stored,input*.00005);
                Assert.AreEqual(d.InitialOxygen+d.OxygenIn+d.OxygenProduced-d.OxygenOut-d.OxygenUsed,state.Oxygen,(d.InitialOxygen+d.OxygenIn+d.OxygenProduced)*.0002);
            }
            Assert.Greater(d.Births,0);Assert.Greater(d.Deaths,0);
        }
        [Test] public void MineralRefusalDoesNotSpendEnergyOrPublishBirth()
        {
            var a=Consumer();a.Identity="mineral-refusal";a.Biology.Id=a.Biology.LineageId=1;a.MineralReserve=0;a.Biology.Energy=Biology.Capacity(a.Biology.Genes);
            var world=new VolumeWorld(new[]{a},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit){Controlled=0};
            Assert.IsNotNull(world.DivisionBlock(0));float energy=world.Get(0).Biology.Energy;
            Assert.IsFalse(world.RequestDivision());Assert.AreEqual(energy,world.Get(0).Biology.Energy);Assert.AreEqual(0,world.Life.Latest);
        }
        [Test] public void HydrothermalSourceIsOnePersistentFinitePhysicalParcel()
        {
            var archive=new MemoryRegionArchive();var ocean=new ChunkOcean(42,archive,EcologicalScenario.Natural);
            string identity=null;V3 position=default;int count=0;
            for(int k=0;k<ocean.World.FoodCount;k++){var f=ocean.World.GetFood(k);if(!f.Identity.EndsWith("/vent-food"))continue;identity=f.Identity;position=f.Position;count++;}
            Assert.Greater(count,0);for(int i=0;i<30;i++)ocean.World.Step();
            int matches=0;for(int k=0;k<ocean.World.FoodCount;k++){var f=ocean.World.GetFood(k);if(f.Identity==identity){matches++;Assert.AreEqual(position,f.Position);Assert.LessOrEqual(f.Substrate,CloudEcology.SubstrateCapacity);}}
            Assert.AreEqual(1,matches);ocean.Save();ocean.Stream(new V3(120,0,0));ocean.Stream(default);
            matches=0;for(int k=0;k<ocean.World.FoodCount;k++)if(ocean.World.GetFood(k).Identity==identity)matches++;
            Assert.AreEqual(1,matches);
        }
        [Test] public void HistoryAndEcologicalMapReadRealStocksWithoutChangingBiology()
        {
            var store=new MemoryRegionArchive();var ocean=new ChunkOcean(42,store,EcologicalScenario.Natural);
            for(int i=0;i<20;i++)ocean.World.Step();var before=VolumeSnapshot.Read(ocean.World);var original=ocean.World.Get(0);
            for(int i=0;i<10;i++)ocean.Survey.Refresh(ocean.World,ocean.LoadedKeys);
            Assert.AreEqual(before,VolumeSnapshot.Read(ocean.World));Assert.AreEqual(original,ocean.World.Get(0));
            int count=ocean.Survey.NicheHistory.Count;ocean.Save();var reloaded=new ChunkOcean(42,store);
            Assert.AreEqual(count,reloaded.Survey.NicheHistory.Count);Assert.IsNotEmpty(NicheReadout.Trend(reloaded.Survey.NicheHistory));
            foreach(var r in ocean.Survey.Regions)if(r.Loaded){Assert.GreaterOrEqual(r.Nutrients,0);Assert.GreaterOrEqual(r.Oxygen,0);Assert.That(r.Light,Is.InRange(0f,1f));}
        }
        [Test] public void FreshRegionsAreCreditedAsInitialStockRatherThanHiddenEnergy()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive(),EcologicalScenario.Natural);
            for(int i=0;i<30;i++)ocean.World.Step();ocean.Stream(new V3(21,0,0));
            var snapshot=ocean.Snapshot(out _);var d=ocean.World.Diagnostics;
            double energy=d.InitialEnergy+d.Solar+d.ExternalNutrients+d.ExternalOrganic;
            Assert.AreEqual(energy-d.Dissipated,snapshot.Stored,energy*.00005);
            Assert.AreEqual(d.InitialMatter+d.ExternalMatter-d.MatterLost,snapshot.Matter,(d.InitialMatter+d.ExternalMatter)*.0002);
        }
        [Test] public void CrowdingActuallyAttenuatesLightInsteadOfOnlyColouringTheMap()
        {
            var a=Consumer();a.Identity="shade-1";a.Alive=true;a.Biology.Id=a.Biology.LineageId=1;a.Position=new V3(0,1,0);
            var b=a;b.Identity="shade-2";b.Biology.Id=b.Biology.LineageId=2;b.Position=new V3(.6f,1,0);
            var f=new FoodCloud {Identity="shade-food",Position=default,Radius=3,NicheInitialized=true,Nutrients=20,Oxygen=40};
            var single=new VolumeWorld(new[]{a},new[]{f},Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit);
            var crowded=new VolumeWorld(new[]{a,b},new[]{f},Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit);
            Assert.Less(crowded.Sample(default).Light,single.Sample(default).Light);
        }
        [Test] public void OverlappingParcelsDoNotMultiplyCellPhotosystemsOrUptakeBudget()
        {
            var a=Consumer();a.Identity="overlap-cell";a.Alive=true;a.Biology.Id=a.Biology.LineageId=1;a.Direction=new V3(1,0,0);
            var first=new FoodCloud {Identity="pool-1",Radius=3,NicheInitialized=true,Nutrients=20,Oxygen=40};var second=first;second.Identity="pool-2";
            var one=new VolumeWorld(new[]{a},new[]{first},Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit){Controlled=0};
            var two=new VolumeWorld(new[]{a},new[]{first,second},Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit){Controlled=0};
            one.Step();two.Step();Assert.LessOrEqual(two.Diagnostics.Solar,one.Diagnostics.Solar*1.01);
            Assert.LessOrEqual(two.Get(0).MineralReserve-a.MineralReserve,(one.Get(0).MineralReserve-a.MineralReserve)*1.01);
        }
        [Test] public void UpgradingLegacyLedgerStartsAnExplicitEpochWithoutRewritingExistingCells()
        {
            var store=new MemoryRegionArchive();var old=new ChunkOcean(42,store);
            for(int tick=0;tick<20;tick++)old.World.Step();old.Save();
            var cell=old.World.Get(0);long births=old.World.Diagnostics.Births;Assert.Greater(old.World.Diagnostics.Upkeep,0);
            var upgraded=new ChunkOcean(42,store,EcologicalScenario.Natural);var a=upgraded.World.Get(upgraded.Find(cell.Identity));
            Assert.AreEqual(cell.Biology,a.Biology);Assert.AreEqual(cell.Position,a.Position);Assert.AreEqual(births,upgraded.World.Diagnostics.Births);
            Assert.IsTrue(upgraded.World.Diagnostics.NicheLedger);Assert.AreEqual(20,upgraded.World.Diagnostics.NicheEpochTick);
            var total=upgraded.Snapshot(out _);var d=upgraded.World.Diagnostics;
            Assert.AreEqual(d.InitialEnergy+d.Solar+d.ExternalNutrients+d.ExternalOrganic-d.Dissipated,total.Stored,d.InitialEnergy*.0001);
            Assert.AreEqual(d.InitialMatter+d.ExternalMatter-d.MatterLost,total.Matter,d.InitialMatter*.0002);
        }
        [Test] public void UnobservedLegacyTimeIsResolvedBeforeTheNewEcologyEpoch()
        {
            var source=new MemoryRegionArchive();var old=new ChunkOcean(42,source);old.Save();source.Clock=20;
            MemoryRegionArchive Clone() {
                var result=new MemoryRegionArchive {Clock=source.Clock,Diagnostics=source.Diagnostics.Clone()};
                foreach(var key in source.Keys){source.TryLoad(key,out var r);result.Save(r);}return result;
            }
            var reference=new ChunkOcean(42,Clone());var actual=new ChunkOcean(42,Clone(),EcologicalScenario.Natural);
            for(int i=0;i<reference.World.Count;i++) {
                var a=reference.World.Get(i);int index=actual.Find(a.Identity);Assert.GreaterOrEqual(index,0);
                var b=actual.World.Get(index);Assert.AreEqual(a.Biology.Genes,b.Biology.Genes);Assert.AreEqual(a.Biology.Age,b.Biology.Age);Assert.AreEqual(a.Biology.Energy,b.Biology.Energy);
            }
            Assert.AreEqual(reference.World.Tick,actual.World.Tick);
            var total=actual.Snapshot(out _);var d=actual.World.Diagnostics;
            Assert.AreEqual(d.InitialEnergy+d.Solar+d.ExternalNutrients+d.ExternalOrganic-d.Dissipated,total.Stored,d.InitialEnergy*.0001);
        }
        [TestCase(15)] [TestCase(60)] [TestCase(144)]
        public void DifferentObservationRatesCannotChangeNicheBirthsGenomesOrStocks(int fps)
        {
            var baseline=NicheExperiment.Create(EcologicalScenario.Natural,7);var observed=NicheExperiment.Create(EcologicalScenario.Natural,7);
            for(int tick=0;tick<200;tick++) {
                NicheExperiment.Step(baseline);NicheExperiment.Step(observed);
                for(int frame=0;frame<fps;frame++){observed.EnvironmentAt(observed.Get(0).Position);observed.FoodPotential(0,observed.Get(0).Biology);NicheReadout.Explain(observed,0);}
                Assert.AreEqual(baseline.Count,observed.Count);for(int i=0;i<baseline.Count;i++)Assert.AreEqual(baseline.Get(i),observed.Get(i));
                for(int k=0;k<baseline.FoodCount;k++)Assert.AreEqual(baseline.GetFood(k),observed.GetFood(k));
                Assert.AreEqual(baseline.Diagnostics.Solar,observed.Diagnostics.Solar);Assert.AreEqual(baseline.Life.Latest,observed.Life.Latest);
            }
        }
        [Test] public void EveryNpcColonyMotorPaysTheSameActualOrganismThrottle()
        {
            var pair=VolumeColonyTests.Pair();var a=pair.Get(0);var b=pair.Get(1);
            var food=new FoodCloud {Identity="colony-effort-food",Position=b.Position,Radius=2.2f,Substrate=35,Organic=35};
            var world=new VolumeWorld(new[]{a,b},new[]{food},Array.Empty<V3>(),pair.Tick,default,scenario:EcologicalScenario.Sunlit){ActiveCenter=a.Position};
            world.Step();Assert.AreEqual(2,world.OrganismSize(0));
            var first=world.Get(0).Biology;var second=world.Get(1).Biology;
            float effortA=first.SwimSpeed/(Ecology.Mobility(first.Genes)*Multicellularity.Motor(first));
            float effortB=second.SwimSpeed/(Ecology.Mobility(second.Genes)*Multicellularity.Motor(second));
            Assert.AreEqual(effortA,effortB,1e-5);Assert.Greater(first.Budget.Movement,0);Assert.Greater(second.Budget.Movement,0);
        }
        [Test] public void PredationTransfersFinitePreyMatterOnceAndOnlyCoreCreatesDetritus()
        {
            var hunter=Consumer(0,.1f);hunter.NicheInitialized=false;hunter.Identity="hunter";hunter.Alive=true;hunter.Direction=new V3(1,0,0);hunter.Biology.Id=hunter.Biology.LineageId=1;hunter.Biology.Genes.Predation=1;hunter.Biology.Genes.Size=1.2f;
            var prey=Consumer(0,.1f);prey.NicheInitialized=false;prey.Identity="prey";prey.Alive=true;prey.Direction=new V3(1,0,0);prey.Position=new V3(.1f,0,0);prey.Biology.Id=prey.Biology.LineageId=2;prey.Biology.Genes.Size=.6f;prey.Biology.Integrity=.000001f;prey.Biology.Energy=.1f;
            var f=new FoodCloud {Identity="detritus-pool",Position=default,Radius=3};
            var w=new VolumeWorld(new[]{hunter,prey},new[]{f},Array.Empty<V3>(),0,default,scenario:EcologicalScenario.Sunlit){NutrientSupply=false};
            double initial=VolumeSnapshot.Read(w).Stored;w.Step();Assert.IsFalse(w.Get(1).Alive);Assert.Greater(w.Diagnostics.Predated,0);
            Assert.AreEqual(0,w.Get(1).Matter+w.Get(1).MineralReserve);Assert.Greater(w.GetFood(0).DetritalMatter,0);
            var result=VolumeSnapshot.Read(w);Assert.AreEqual(w.Diagnostics.InitialMatter-w.Diagnostics.MatterLost,result.Matter,.0001);
            Assert.AreEqual(initial-w.Diagnostics.Dissipated,result.Stored,.0001);long events=w.Life.Latest;
            for(int frame=0;frame<10;frame++)w.Life.TryRead(events,out _);Assert.AreEqual(result,VolumeSnapshot.Read(w));
        }
    }
}
