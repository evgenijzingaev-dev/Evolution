using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Evolution.Core;
using Evolution.Volume;
using Evolution.Presentation;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class OceanSurveyTests
    {
        static VolumeAgent Agent(int id,float x,bool alive=true,int lineage=7)=>new VolumeAgent {Identity="cell-"+id,Position=new V3(x,2,3),Alive=alive,BitAt=double.NegativeInfinity,
            Biology=new Cell {Id=id,LineageId=lineage,Genes=new Genome {Size=1,Speed=1}}};
        static readonly ChunkKey[] Keys={new ChunkKey(0,0,0),new ChunkKey(1,0,0)};
        [Test] public void ObservationDoesNotAlterSimulationOrCreateFood()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());var world=ocean.World;world.Step();var cell=world.Get(0);var resource=world.GetFood(0);
            ocean.Survey.Refresh(world,ocean.LoadedKeys);
            Assert.AreEqual(world.Count,ocean.Survey.Agents.Count);Assert.AreEqual(world.Tick,ocean.Survey.Tick);
            Assert.AreEqual(cell.Position.X,world.Get(0).Position.X);Assert.AreEqual(cell.Biology.Energy,world.Get(0).Biology.Energy);
            Assert.AreEqual(resource.Substrate,world.GetFood(0).Substrate);Assert.AreEqual(resource.Organic,world.GetFood(0).Organic);
            Assert.That(ocean.Survey.Regions.Sum(r=>r.Substrate),Is.EqualTo(Enumerable.Range(0,world.FoodCount).Sum(i=>world.GetFood(i).Substrate)).Within(.001));
            Assert.IsEmpty(ocean.Survey.Events);
        }
        [Test] public void UnloadingIsNotExtinctionAndDoesNotEraseKnownPopulation()
        {
            var s=new OceanSurvey();s.Observe(new[]{Agent(1,4)},Array.Empty<FoodCloud>(),Keys,1,default);
            s.Observe(Array.Empty<VolumeAgent>(),Array.Empty<FoodCloud>(),new[]{new ChunkKey(10,0,0)},2,default);
            Assert.AreEqual(0,s.ActiveLiving);Assert.AreEqual(1,s.KnownLiving);Assert.IsEmpty(s.Events);
            Assert.IsTrue(s.TryRegion(Keys[0],out var r));Assert.IsFalse(r.Loaded);Assert.AreEqual(1,r.Living);
        }
        [Test] public void DeathAndKnownLineageLossRequireAnObservedDeath()
        {
            var s=new OceanSurvey();s.Observe(new[]{Agent(1,4)},Array.Empty<FoodCloud>(),Keys,1,default);
            s.Observe(new[]{Agent(1,4,false)},Array.Empty<FoodCloud>(),Keys,2,default);
            Assert.AreEqual(0,s.KnownLiving);Assert.AreEqual(1,s.KnownLineages);
            Assert.IsTrue(s.Events.Exists(e=>e.Kind==SurveyEventKind.Death));Assert.IsTrue(s.Events.Exists(e=>e.Kind==SurveyEventKind.KnownLineageLost));
        }
        [Test] public void MigrationRequiresThreeDistinctCellsCrossingAnActualBoundary()
        {
            var s=new OceanSurvey();var a=new[]{Agent(1,19),Agent(2,19),Agent(3,19)};
            s.Observe(a,Array.Empty<FoodCloud>(),Keys,1,default);
            for(int i=0;i<a.Length;i++)a[i].Position.X=21;
            s.Observe(a,Array.Empty<FoodCloud>(),Keys,2,default);
            Assert.AreEqual(1,s.Events.Count(e=>e.Kind==SurveyEventKind.Migration));Assert.AreEqual(3,s.Events.First(e=>e.Kind==SurveyEventKind.Migration).Count);
            var solo=new OceanSurvey();for(int i=0;i<10;i++)solo.Observe(new[]{Agent(5,i%2==0?19:21)},Array.Empty<FoodCloud>(),Keys,i+1,default);
            Assert.IsFalse(solo.Events.Exists(e=>e.Kind==SurveyEventKind.Migration));
        }
        [Test] public void StreamedFounderOrArchivedGenerationDoesNotInventBirthsOrColonies()
        {
            var a=Agent(1,4);a.Biology.Generation=5;a.Biology.ParentId=123;
            var s=new OceanSurvey();s.Observe(new[]{a},Array.Empty<FoodCloud>(),Keys,20,default);
            Assert.IsEmpty(s.Events);
        }
        [Test] public void PopulationChangeUsesSameRegionAcrossObservations()
        {
            var s=new OceanSurvey();var a=Enumerable.Range(1,8).Select(i=>Agent(i,4)).ToArray();s.Observe(a,Array.Empty<FoodCloud>(),Keys,1,default);
            for(int i=3;i<8;i++)a[i].Alive=false;s.Observe(a,Array.Empty<FoodCloud>(),Keys,2,default);
            Assert.IsTrue(s.Events.Exists(e=>e.Kind==SurveyEventKind.PopulationChange&&e.Count==-5));
        }
        [Test] public void CompactSurveyPersistsWithoutLoadingArchivedOrganisms()
        {
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".survey");
            try{var s=new OceanSurvey();s.Observe(new[]{Agent(1,4)},new[]{new FoodCloud{Position=new V3(1,2,3),Substrate=12,Organic=5}},Keys,20,default);
                SurveyPersistence.Write(path,s.Regions);var saved=new List<SurveyRegion>();SurveyPersistence.Read(path,saved);var restored=new OceanSurvey();restored.Restore(saved);
                Assert.AreEqual(1,restored.KnownLiving);Assert.IsEmpty(restored.Agents);Assert.IsTrue(restored.Regions.TrueForAll(r=>!r.Loaded));
                Assert.AreEqual(12,restored.Regions.Sum(r=>r.Substrate));Assert.AreEqual(5,restored.Regions.Sum(r=>r.Organic));}
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void ThousandsOfCellsRemainExactAndFramingUsesRequestedLineage()
        {
            var a=new VolumeAgent[4000];for(int i=0;i<a.Length;i++)a[i]=Agent(i,i%35,true,i%10+1);
            var s=new OceanSurvey();s.Observe(a,Array.Empty<FoodCloud>(),Keys,1,default);s.Observe(a,Array.Empty<FoodCloud>(),Keys,11,default);
            Assert.AreEqual(4000,s.ActiveLiving);Assert.AreEqual(4000,s.KnownLiving);Assert.AreEqual(4000,s.Agents.Count);
            Assert.IsTrue(s.Frame(new V3(17,2,3),40,7,out var center,out float radius,out int n));Assert.AreEqual(400,n);Assert.Greater(radius,1);Assert.Less(radius,40);
        }
        [Test] public void CatalogueEvictionIsBoundedAndNeverReportedAsExtinction()
        {
            var regions=new List<SurveyRegion>();for(int i=0;i<OceanSurvey.RegionLimit;i++){var r=new SurveyRegion{Key=new ChunkKey(i+2,0,0),Living=1};r.Lineages[i]=1;regions.Add(r);}
            var s=new OceanSurvey();s.Restore(regions);s.Observe(new[]{Agent(1,4)},Array.Empty<FoodCloud>(),Keys,2,default);
            Assert.LessOrEqual(s.Regions.Count,OceanSurvey.RegionLimit);Assert.IsTrue(s.CatalogueTruncated);Assert.IsFalse(s.Events.Exists(e=>e.Kind==SurveyEventKind.KnownLineageLost));
        }
        [Test] public void FullAgentHistoryRetiresStaleEntriesAndContinuesObservingNewCells()
        {
            var s=new OceanSurvey();var old=new VolumeAgent[OceanSurvey.AgentHistoryLimit];for(int i=0;i<old.Length;i++)old[i]=Agent(i,4);
            s.Observe(old,Array.Empty<FoodCloud>(),Keys,1,default);
            s.Observe(new[]{Agent(99999,4)},Array.Empty<FoodCloud>(),Keys,500,default);
            s.Observe(new[]{Agent(99999,4)},Array.Empty<FoodCloud>(),Keys,510,default);
            s.Observe(new[]{Agent(99999,4,false)},Array.Empty<FoodCloud>(),Keys,520,default);
            Assert.IsTrue(s.Events.Exists(e=>e.Kind==SurveyEventKind.Death&&e.Identity=="cell-99999"));
        }
        [Test] public void RepeatedObservationAtSameTickDoesNotEraseInterpolationHistory()
        {
            var s=new OceanSurvey();s.Observe(new[]{Agent(1,1)},Array.Empty<FoodCloud>(),Keys,1,default);
            s.Observe(new[]{Agent(1,3)},Array.Empty<FoodCloud>(),Keys,11,default);
            s.Observe(new[]{Agent(1,3)},Array.Empty<FoodCloud>(),Keys,11,default);
            Assert.AreEqual(1,s.Agents[0].Previous.X);Assert.AreEqual(3,s.Agents[0].Position.X);
            Assert.AreEqual(1,s.KnownPopulation(7));
        }
        [Test] public void FramingASliceDoesNotIncludeOtherHorizontalLayers()
        {
            var low=Agent(1,3);var high=Agent(2,9);high.Position.Y=25;
            var s=new OceanSurvey();s.Observe(new[]{low,high},Array.Empty<FoodCloud>(),new[]{Keys[0],new ChunkKey(0,1,0)},1,default);
            Assert.IsTrue(s.Frame(new V3(5,10,3),50,0,out var p,out float r,out int n,0));
            Assert.AreEqual(1,n);Assert.AreEqual(2,p.Y);
            Assert.IsTrue(s.Frame(new V3(5,30,3),50,0,out p,out r,out n,1));Assert.AreEqual(1,n);Assert.AreEqual(25,p.Y);
        }
        [Test] public void MorphotypeProjectionMatchesExistingPresentation()
        {
            var rng=new RandomSource(42);for(int i=0;i<100;i++){var g=Genome.Founder(ref rng);Assert.AreEqual((int)CrystalForms.From(g),OceanSurvey.Form(g));}
            Assert.AreEqual(2,ChunkKey.At(new V3(0,40,0)).Y);
            Assert.AreEqual(-1,ChunkKey.At(new V3(0,-.1f,0)).Y);
        }
    }
}
