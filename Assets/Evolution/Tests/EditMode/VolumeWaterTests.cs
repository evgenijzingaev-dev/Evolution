using System;
using System.IO;
using System.Collections.Generic;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class VolumeWaterTests
    {
        static VolumeAgent Agent(V3 point,float predation=0,float photo=0,float size=1)=>new VolumeAgent {
            Position=point,Direction=new V3(1,0,0),Biology=new Cell {Genes=new Genome {Size=size,Speed=1,Uptake=1,PreferredTemperature=22,Predation=predation,Photosynthesis=photo},Energy=16,Integrity=1,Growth=1,Age=6}};
        [Test] public void WaterHasVerticalFlowAndViscosityQuicklyDampsRelativeMotion()
        {
            var p=new V3(4,3,7);var flow=WaterField.Current(p,0);Assert.Greater(Math.Abs(flow.Y),.05);
            var velocity=flow+new V3(2,0,0);
            for(int i=0;i<20;i++)velocity=WaterField.Relax(velocity,flow,1,.05f);
            Assert.Less((velocity-flow).Length,.015);Assert.Greater(flow.Length,0);
        }
        [Test] public void WaterOnlyTracersOccupyDepthAndActuallyAdvect()
        {
            var water=new Suspension(6144,42);var before=water.Get(0);float min=0,max=0;
            for(int i=0;i<water.Count;i++){min=Math.Min(min,water.Get(i).Y);max=Math.Max(max,water.Get(i).Y);}
            Assert.Less(min,-25);Assert.Greater(max,25);
            water.Step(.05f,0,default,Array.Empty<FlowBody>(),0);
            Assert.That((water.Get(0)-(before+WaterField.Current(before,0)*.05f)).Length,Is.LessThan(.00001));
        }
        [Test] public void SwimmingDeflectsNearbySuspensionButNotDistantWater()
        {
            var a=new Suspension(64,42);var b=new Suspension(64,42);
            var body=new FlowBody {Position=a.Get(0)+new V3(.6f,0,0),Direction=new V3(1,0,0),Radius=.5f,Speed=1};
            a.Step(.05f,0,default,new[] {body},1);b.Step(.05f,0,default,Array.Empty<FlowBody>(),0);
            Assert.Greater((a.Get(0)-b.Get(0)).Length,.0001);
            Assert.AreEqual(0,WaterField.Disturbance(new V3(100,100,100),body).Length);
        }
        [Test] public void OrganismCanRiseBeyondFormerArenaWithoutWrapping()
        {
            var s=new VolumeWorld(1,42,new[] {Agent(new V3(25,25,25))}) {Controlled=0,Input=new V3(0,1,0),ActiveCenter=new V3(25,25,25)};
            for(int i=0;i<80;i++)s.Step();var p=s.Get(0).Position;
            Assert.Greater(p.Y,27);Assert.Greater(p.X,24);Assert.Greater(p.Z,24);
        }
        [Test] public void VerticalApproachFindsRealFoodAndChangesPoolAndEnergy()
        {
            var template=new VolumeWorld(1);var f=template.GetFood(1);
            var s=new VolumeWorld(1,42,new[] {Agent(f.Position+new V3(0,4,0),photo:1)}) {Controlled=0,Input=new V3(0,-1,0)};
            float intake=0;
            for(int i=0;i<100;i++){s.Step();var c=s.Get(0).Biology;intake+=c.Budget.Intake;Assert.That(c.Energy-c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00002));}
            Assert.Greater(intake,0);Assert.Less(s.GetFood(1).Substrate,f.Substrate);
        }
        [Test] public void PhotosynthesisNeedsSubstrateAndTemperatureUsesExistingPhysiology()
        {
            var template=new VolumeWorld(1);var organic=template.GetFood(0);var substrate=template.GetFood(1);
            var dark=new VolumeWorld(1,42,new[] {Agent(organic.Position,photo:1)}) {Controlled=0};dark.Step();
            // Even an initially organic-only cloud now receives the bounded ocean substrate flux.
            Assert.Greater(dark.Get(0).Biology.Budget.Light,0);Assert.Greater(dark.Get(0).Biology.Budget.Organic,0);
            var sun=new VolumeWorld(1,42,new[] {Agent(substrate.Position,photo:1)}) {Controlled=0};sun.Step();var a=sun.Get(0);
            Assert.Greater(a.Biology.Budget.Light,0);
            float temperature=WaterField.Temperature(a.Position,0);
            Assert.That(a.Biology.Budget.Upkeep,Is.EqualTo(Biology.Cost(a.Biology.Genes,temperature)*VolumeWorld.StepSeconds).Within(.00001));
        }
        [Test] public void HuntingUsesSphericalContactIncludingVerticalSeparation()
        {
            var far=new VolumeWorld(2,42,new[] {Agent(default,1,0,1.6f),Agent(new V3(.3f,5,0))}) {Controlled=0,Attack=true};far.Step();
            Assert.AreEqual(0,far.Get(0).Biology.Budget.Prey);
            var near=new VolumeWorld(2,42,new[] {Agent(default,1,0,1.6f),Agent(new V3(.3f,0,0))}) {Controlled=0,Attack=true};near.Step();
            Assert.Greater(near.Get(0).Biology.Budget.Prey,0);Assert.Less(near.Get(1).Biology.Integrity,1);
        }
        [Test] public void ObstaclesDeflectOrganismsInVolume()
        {
            var s=new VolumeWorld(1,42,new[] {Agent(new V3(-4,0,2))}) {Controlled=0};s.Step();
            Assert.That((s.Get(0).Position-s.Obstacles[0]).Length,Is.GreaterThanOrEqualTo(1.6999));
        }
        [Test] public void FieldAndFoodInspectionCannotModifyEcology()
        {
            var s=new VolumeWorld(1);var before=s.Get(0);var food=s.GetFood(0);
            for(int i=0;i<100;i++){s.Sample(new V3(i*.1f,3,-4));s.FoodPotential(0,before.Biology);}
            Assert.AreEqual(before,s.Get(0));Assert.AreEqual(food,s.GetFood(0));Assert.AreEqual(0,s.Tick);
        }
        [Test] public void StableSimulationIsUnaffectedByTheExperimentalWorld()
        {
            var cfg=new SimulationConfig {InitialPopulation=24,Capacity=128};var stable=new Simulation(cfg,42);var reference=new Simulation(cfg,42);var volume=new VolumeWorld(8);
            for(int i=0;i<60;i++){volume.Step();stable.Step();reference.Step();}
            Assert.AreEqual(reference.RandomState,stable.RandomState);Assert.AreEqual(reference.Count,stable.Count);
            for(int i=0;i<stable.Count;i++)Assert.AreEqual(reference.GetCell(i),stable.GetCell(i));
        }
        [Test] public void ChunkCoordinatesDoNotWrapAndFieldsAreContinuousAcrossBoundaries()
        {
            Assert.AreEqual(-1,ChunkKey.At(new V3(-.001f,0,0)).X);Assert.AreEqual(5001,ChunkKey.At(new V3(100020,0,0)).X);
            var a=new V3(19.999f,-20,40);var b=new V3(20.001f,-20,40);
            Assert.Less(Math.Abs(WaterField.Temperature(a,17)-WaterField.Temperature(b,17)),.01);
            Assert.Less((WaterField.Current(a,17)-WaterField.Current(b,17)).Length,.001);
        }
        [Test] public void RegionUnloadAndReturnPreserveDepletedResourcesAndGenomes()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());int selected=ocean.Nearest(default);ocean.World.Controlled=selected;
            for(int i=0;i<20;i++)ocean.World.Step();var a=ocean.World.Get(selected);
            var resources=new Dictionary<string,FoodCloud>();for(int i=0;i<ocean.World.FoodCount;i++){var f=ocean.World.GetFood(i);resources[f.Identity]=f;}
            ocean.Stream(new V3(1000,-1000,1000));ocean.Stream(default);
            int restored=ocean.Find(a.Identity);Assert.GreaterOrEqual(restored,0);var b=ocean.World.Get(restored);
            Assert.AreEqual(a.Position,b.Position);Assert.AreEqual(a.Biology.Energy,b.Biology.Energy);Assert.AreEqual(a.Biology.Genes,b.Biology.Genes);
            for(int i=0;i<ocean.World.FoodCount;i++){var f=ocean.World.GetFood(i);if(resources.TryGetValue(f.Identity,out var old))Assert.AreEqual(old.Substrate,f.Substrate);}
        }
        [Test] public void CrossingChunksPreservesControlAndContinuousCoordinatesAtBoostSpeed()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());int selected=0;float closest=100;
            for(int i=0;i<ocean.World.Count;i++){float edge=20-(ocean.World.Get(i).Position.X-(float)Math.Floor(ocean.World.Get(i).Position.X/20)*20);if(edge<closest){closest=edge;selected=i;}}
            var initial=ocean.World.Get(selected);string id=initial.Identity;var start=ChunkKey.At(initial.Position);bool crossed=false;
            for(int i=0;i<100;i++) {
                selected=ocean.Find(id);Assert.GreaterOrEqual(selected,0);var before=ocean.World.Get(selected).Position;
                ocean.World.Controlled=selected;ocean.World.Input=new V3(1,0,0);ocean.World.Boost=true;ocean.World.ActiveCenter=before;
                ocean.World.Step();var after=ocean.World.Get(selected).Position;Assert.Less((after-before).Length,.4);
                crossed|=!ChunkKey.At(after).Equals(start);ocean.Stream(after);
                Assert.AreEqual(id,ocean.World.Get(ocean.World.Controlled).Identity);
            }
            Assert.IsTrue(crossed);Assert.AreEqual(initial.Biology.Genes,ocean.World.Get(ocean.Find(id)).Biology.Genes);
        }
        [Test] public void FixedSeedGenerationAndFastCameraTravelAreRepeatableAndBounded()
        {
            var a=new ChunkOcean(7,new MemoryRegionArchive());var b=new ChunkOcean(7,new MemoryRegionArchive());
            foreach(var p in new[] {new V3(19,0,0),new V3(21,-21,40),new V3(2000,400,-1000),new V3(-400,-200,1000),default(V3)}) {
                a.Stream(p);b.Stream(p);Assert.AreEqual(125,a.LoadedCount);Assert.AreEqual(a.World.Count,b.World.Count);
                var ids=new HashSet<string>();for(int i=0;i<a.World.Count;i++){Assert.IsTrue(ids.Add(a.World.Get(i).Identity));Assert.AreEqual(a.World.Get(i),b.World.Get(i));}
            }
        }
        [Test] public void RegionDiskArchiveSurvivesReopeningWithoutResettingPopulation()
        {
            string path=Path.Combine(Path.GetTempPath(),"evolution-volume-test-"+Guid.NewGuid().ToString("N"));
            try {
                var ocean=new ChunkOcean(91,new BinaryRegionArchive(path));int i=ocean.Nearest(default);ocean.World.Controlled=i;
                for(int n=0;n<10;n++)ocean.World.Step();var a=ocean.World.Get(i);ocean.Save();
                var resumed=new ChunkOcean(91,new BinaryRegionArchive(path));var b=resumed.World.Get(resumed.Find(a.Identity));
                Assert.AreEqual(ocean.World.Tick,resumed.World.Tick);Assert.AreEqual(a.Position,b.Position);Assert.AreEqual(a.Biology.Energy,b.Biology.Energy);Assert.AreEqual(a.Biology.Genes,b.Biology.Genes);
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void ArchivedRegionsAdvanceMetabolismWithoutRecreatingTheirResidents()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());var a=ocean.World.Get(ocean.Nearest(default));
            ocean.Stream(new V3(1000,1000,1000));for(int n=0;n<40;n++)ocean.World.Step();ocean.Stream(default);
            var b=ocean.World.Get(ocean.Find(a.Identity));Assert.Greater(b.Biology.Age,a.Biology.Age);
            Assert.AreEqual(a.Biology.Genes,b.Biology.Genes);Assert.AreEqual(a.Identity,b.Identity);
        }
        [Test] public void FoodCloudsAreTransportedByTheSameThreeDimensionalCurrent()
        {
            var s=new VolumeWorld(1);var f=s.GetFood(1);var expected=f.Position+s.Current(f.Position)*VolumeWorld.StepSeconds;
            s.Step();Assert.That((expected-s.GetFood(1).Position).Length,Is.LessThan(.00001));
        }
        [Test] public void RepeatedSavesCannotDuplicateAnOrganismOutsideTheActiveWindow()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());int index=0;
            for(int i=1;i<ocean.World.Count;i++)if(ocean.World.Get(i).Position.X>ocean.World.Get(index).Position.X)index=i;
            string id=ocean.World.Get(index).Identity;ocean.World.Controlled=index;ocean.World.Input=new V3(1,0,0);ocean.World.Boost=true;
            for(int i=0;i<80;i++){ocean.World.ActiveCenter=ocean.World.Get(index).Position;ocean.World.Step();if(i%10==0)ocean.Save();}
            var saved=ocean.World.Get(index);ocean.Save();ocean.Save();ocean.Stream(saved.Position);
            int copies=0;for(int i=0;i<ocean.World.Count;i++)if(ocean.World.Get(i).Identity==id)copies++;
            Assert.AreEqual(1,copies);Assert.AreEqual(saved.Position,ocean.World.Get(ocean.Find(id)).Position);
        }
        [Test] public void RegionArchiveRetainsParentGenomeAndMutationIdentity()
        {
            string path=Path.Combine(Path.GetTempPath(),"evolution-genome-test-"+Guid.NewGuid().ToString("N"));
            try {
                var archive=new BinaryRegionArchive(path);var key=new ChunkKey(-2,7,1);var region=new RegionState {Key=key,LastTick=42};
                var a=Agent(new V3(-30,145,24));a.Identity="ancestor-check";a.Biology.ParentId=17;a.Biology.Generation=4;a.Biology.BirthMutationMask=7;
                a.Biology.ParentGenes=new Genome {Size=1.2f,Speed=.8f,Photosynthesis=.7f,Membrane=.6f,Adhesion=.4f,Transport=.2f};
                region.Agents.Add(a);archive.Save(region);Assert.IsTrue(new BinaryRegionArchive(path).TryLoad(key,out var restored));
                Assert.AreEqual(a.Biology,restored.Agents[0].Biology);Assert.AreEqual(a.Identity,restored.Agents[0].Identity);
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
