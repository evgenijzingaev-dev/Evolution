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
    }
}
