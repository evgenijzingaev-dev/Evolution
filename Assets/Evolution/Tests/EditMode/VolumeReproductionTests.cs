using System;
using System.IO;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class VolumeReproductionTests
    {
        static VolumeWorld Ready(bool controlled=true)
        {
            var random=new RandomSource(17);var g=Genome.Founder(ref random);g.Size=1;g.Speed=.5f;g.Uptake=1;
            var a=new VolumeAgent {Position=new V3(2,2,2),Direction=new V3(1,0,0),
                Biology=new Cell {Genes=g,Age=6,Growth=1,Integrity=1,Energy=Biology.Capacity(g)}};
            return new VolumeWorld(1,42,new[] {a}) {Controlled=controlled?0:-1};
        }
        [Test] public void ControlledDivisionIsExplicitAndPaysForInheritedIndependentDaughter()
        {
            var w=Ready();w.Step();Assert.AreEqual(1,w.Count);
            Assert.IsTrue(w.RequestDivision());w.Step();Assert.AreEqual(2,w.Count);
            var p=w.Get(0);var d=w.Get(1);var c=p.Biology;var child=d.Biology;
            Assert.AreEqual(c.Id,child.ParentId);Assert.AreEqual(c.LineageId,child.LineageId);
            Assert.AreEqual(c.Generation+1,child.Generation);Assert.AreEqual(c.Genes,child.ParentGenes);
            Assert.AreEqual(Observation.MutationMask(c.Genes,child.Genes),child.BirthMutationMask);
            Assert.AreNotEqual(p.Identity,d.Identity);Assert.AreNotEqual(c.Id,child.Id);
            Assert.Greater(child.Energy,0);Assert.AreEqual(0,child.Age);Assert.AreEqual(0,child.Growth);
            Assert.That(c.Budget.Division,Is.EqualTo(Biology.StructureEnergy(child.Genes)+Biology.BirthOverhead(child.Genes)+child.Energy).Within(.00001));
            Assert.That(c.Energy-c.Budget.Start,Is.EqualTo(c.Budget.Net).Within(.00002));
            Assert.That(c.Energy+child.Energy,Is.EqualTo(c.Budget.Start+c.Budget.Intake-c.Budget.Upkeep-c.Budget.Movement-Biology.StructureEnergy(child.Genes)-Biology.BirthOverhead(child.Genes)).Within(.00002));
            Assert.IsFalse(w.RequestDivision());var position=d.Position;w.Step();Assert.Greater((w.Get(1).Position-position).Length,0);
        }
        [Test] public void AutonomousResidentsDivideButResourcesPreventUnboundedBirths()
        {
            var founder=Ready().Get(0);founder.Position=new V3(200,200,200);
            var w=new VolumeWorld(1,42,new[] {founder}) {ActiveCenter=founder.Position};w.Step();Assert.AreEqual(2,w.Count);
            for(int i=0;i<1200;i++)w.Step();Assert.AreEqual(2,w.Count);Assert.IsFalse(w.Get(0).Alive);Assert.IsFalse(w.Get(1).Alive);
        }
        [Test] public void PaidGrowthAndMaturityGateFurtherGenerations()
        {
            var w=Ready();w.RequestDivision();w.Step();w.Controlled=1;
            Assert.IsNotNull(w.DivisionBlock(1));Assert.IsFalse(w.RequestDivision());
            for(int i=0;i<41;i++)w.Step();
            Assert.Greater(w.Get(1).Biology.Growth,0);Assert.Less(w.Get(1).Biology.Growth,1);
        }
        [Test] public void MutationIsSmallDeterministicAndCanBeInherited()
        {
            int mutations=0;
            for(int seed=1;seed<=60;seed++) {
                var w=Ready();for(int i=0;i<seed;i++)w.Step();w.RequestDivision();w.Step();
                var p=w.Get(0).Biology;var c=w.Get(1).Biology;
                if(c.BirthMutationMask!=0)mutations++;
                Assert.Less(Math.Abs(c.Genes.Size-p.Genes.Size),.2f);
                Assert.Less(Math.Abs(c.Genes.Speed-p.Genes.Speed),.2f);
                var replay=Ready();for(int i=0;i<seed;i++)replay.Step();replay.RequestDivision();replay.Step();
                Assert.AreEqual(c.Genes,replay.Get(1).Biology.Genes);
            }
            Assert.Greater(mutations,0);
        }
        [Test] public void RealOceanResidentCanEarnResourcesAndDivideWithoutInjection()
        {
            var w=new ChunkOcean(42,new MemoryRegionArchive()).World;
            for(int i=0;i<20;i++)w.Step();
            int candidate=-1;float best=-1;
            for(int i=0;i<w.Count;i++) {
                var a=w.Get(i);if(!a.Alive||a.Biology.Generation>0)continue;
                for(int k=0;k<w.FoodCount;k++) {
                    var f=w.GetFood(k);float d=(f.Position-a.Position).Length;
                    float gain=w.FoodPotential(k,a.Biology)-Biology.Cost(a.Biology.Genes,w.Temperature(a.Position));
                    float score=gain/(1+d);if(score>best){best=score;candidate=i;}
                }
            }
            Assert.GreaterOrEqual(candidate,0);w.Controlled=candidate;
            for(int ticks=0;w.DivisionBlock(candidate)!=null&&ticks<2400;ticks++) {
                var a=w.Get(candidate);Assert.IsTrue(a.Alive);
                var read=PlayerReadout.Read(w,candidate);Assert.GreaterOrEqual(read.Food,0);
                var f=w.GetFood(read.Food);var delta=f.Position-a.Position;
                w.Input=delta.Length>f.Radius*.45f?delta.Unit:default;w.Step();
            }
            Assert.IsNull(w.DivisionBlock(candidate));Assert.Greater(w.Get(candidate).Biology.AssimilatedEnergy,0);
            Assert.IsTrue(w.RequestDivision());w.Step();Assert.IsNotNull(w.LastOffspringIdentity);
        }
        [Test] public void ReadsRealVersionOneArchiveWrittenByBaseBranch()
        {
            // Written by BinaryRegionArchive from base 445c3a26, before v2 animation fields.
            const string fixture="M09WRQEAAAAqAAAAAAAAAAEAAAAObGVnYWN5LWZvdW5kZXIAAACAPwAAAEAAAEBAAAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABwAAAAcAAAAAAAAAAAAAAAAAAAAAAIhBAADAQAAAgD8AAIA/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB6thk/vgVEP+wEnz9Gxs1BuTHMPSAMMD5OQ28/aMSpPmw4vz5MvHI/ACfnPQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";
            string directory=Path.Combine(Path.GetTempPath(),"evolution-v1-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            try {
                File.WriteAllBytes(Path.Combine(directory,"0_0_0.region"),Convert.FromBase64String(fixture));
                var archive=new BinaryRegionArchive(directory);Assert.IsTrue(archive.TryLoad(default,out var state));
                Assert.AreEqual(42,state.LastTick);Assert.AreEqual(1,state.Agents.Count);var a=state.Agents[0];
                Assert.AreEqual("legacy-founder",a.Identity);Assert.AreEqual(17,a.Biology.Energy);Assert.AreEqual(7,a.Biology.LineageId);
                Assert.AreEqual(new V3(1,2,3),a.Position);Assert.AreEqual(0,a.DivisionAt);
                archive.Save(state);Assert.IsTrue(archive.TryLoad(default,out var upgraded));Assert.AreEqual(a,upgraded.Agents[0]);
            } finally {Directory.Delete(directory,true);}
        }
        [Test] public void SurveyReportsPaidBirthOnceAndDoesNotInventStreamedBirths()
        {
            var w=Ready();var survey=new OceanSurvey();var keys=new[] {ChunkKey.At(w.Get(0).Position)};
            survey.Refresh(w,keys);w.RequestDivision();w.Step();survey.Refresh(w,keys);survey.Refresh(w,keys);
            Assert.AreEqual(1,survey.Events.FindAll(e=>e.Kind==SurveyEventKind.Division).Count);
            var restoredSurvey=new OceanSurvey();restoredSurvey.Refresh(w,keys);
            Assert.AreEqual(0,restoredSurvey.Events.FindAll(e=>e.Kind==SurveyEventKind.Division).Count);
        }
        [Test] public void BinarySaveRestoresBothGenomesEnergyAndDivisionPhase()
        {
            string directory=Path.Combine(Path.GetTempPath(),"evolution-birth-"+Guid.NewGuid());
            try {
                var w=Ready();w.RequestDivision();w.Step();var key=ChunkKey.At(w.Get(0).Position);
                var state=new RegionState {Key=key,LastTick=w.Tick};state.Agents.Add(w.Get(0));state.Agents.Add(w.Get(1));
                var archive=new BinaryRegionArchive(directory);archive.Save(state);
                Assert.IsTrue(new BinaryRegionArchive(directory).TryLoad(key,out var loaded));Assert.AreEqual(2,loaded.Agents.Count);
                for(int i=0;i<2;i++)Assert.AreEqual(state.Agents[i],loaded.Agents[i]);
            } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void ExpiredCorpsesCannotPermanentlyExhaustBirthSlots()
        {
            var archive=new MemoryRegionArchive {Clock=100};var region=new RegionState {Key=default,LastTick=100};
            var living=Ready().Get(0);region.Agents.Add(living);
            for(int i=0;i<VolumeWorld.PopulationLimit;i++) {
                var dead=living;dead.Identity="dead-"+i;dead.Alive=false;dead.DiedAt=0;region.Agents.Add(dead);
            }
            archive.Save(region);var ocean=new ChunkOcean(42,archive);
            Assert.IsNotNull(ocean.World.DivisionBlock(ocean.Find(living.Identity)));
            Assert.IsTrue(ocean.Stream(default));int index=ocean.Find(living.Identity);
            Assert.GreaterOrEqual(index,0);Assert.IsNull(ocean.World.DivisionBlock(index));
            ocean.Save();var restored=new ChunkOcean(42,archive);Assert.IsNull(restored.World.DivisionBlock(restored.Find(living.Identity)));
        }
        [Test] public void QueuedDivisionSurvivesCameraChunkTransition()
        {
            var archive=new MemoryRegionArchive();var w=Ready();var region=new RegionState {Key=default};region.Agents.Add(w.Get(0));archive.Save(region);
            var ocean=new ChunkOcean(42,archive);string identity=w.Get(0).Identity;
            ocean.World.Controlled=ocean.Find(identity);Assert.IsTrue(ocean.World.RequestDivision());
            ocean.Stream(new V3(21,0,0));Assert.AreEqual(identity,ocean.World.Get(ocean.World.Controlled).Identity);
            ocean.World.Step();Assert.IsNotNull(ocean.World.LastOffspringIdentity);
            Assert.AreEqual(ocean.World.Get(ocean.World.Controlled).Biology.Id,ocean.World.Get(ocean.Find(ocean.World.LastOffspringIdentity)).Biology.ParentId);
        }
        [Test] public void ChunkStreamingAndReloadPreserveNewbornIdentityAndLineage()
        {
            var archive=new MemoryRegionArchive();var key=new ChunkKey(0,0,0);var source=Ready();source.RequestDivision();source.Step();
            var region=new RegionState {Key=key};region.Agents.Add(source.Get(0));region.Agents.Add(source.Get(1));archive.Save(region);
            var ocean=new ChunkOcean(42,archive);string id=source.Get(1).Identity;
            int index=ocean.Find(id);Assert.GreaterOrEqual(index,0);var original=ocean.World.Get(index);
            ocean.Stream(new V3(200,0,0));ocean.Stream(default);Assert.AreEqual(original,ocean.World.Get(ocean.Find(id)));
            ocean.Save();var restored=new ChunkOcean(42,archive);Assert.AreEqual(original,restored.World.Get(restored.Find(id)));
        }
    }
}
