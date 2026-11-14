using System;
using Evolution.Core;
using Evolution.Presentation;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class LivingWaterTests
    {
        static VolumeWorld Ready(float energy=100)
        {
            var g=new Genome {Size=1,Speed=.6f,Uptake=1,PreferredTemperature=22};
            return new VolumeWorld(1,42,new[] {new VolumeAgent {Position=new V3(200,0,0),Direction=new V3(1,0,0),
                Biology=new Cell {Genes=g,Age=8,Growth=1,Integrity=1,Energy=Math.Min(energy,Biology.Capacity(g))}}}) {Controlled=0,ActiveCenter=new V3(200,0,0)};
        }
        [Test] public void JournalPublishesOnlyCommittedPaidDivisionWithRealPair()
        {
            var w=Ready();Assert.IsTrue(w.RequestDivision());Assert.AreEqual(0,w.Life.Latest);
            w.Step();Assert.AreEqual(1,w.Births);Assert.AreEqual(1,w.Life.Latest);
            Assert.IsTrue(w.Life.TryRead(1,out var e));Assert.AreEqual(LifeEventKind.Division,e.Kind);
            Assert.AreEqual(w.Get(0).Identity,e.Agent.Identity);Assert.AreEqual(w.Get(1),e.Child);
            Assert.AreEqual(e.Agent.Biology.Id,e.Child.Biology.ParentId);
            Assert.AreEqual(w.Get(0).DivisionAt,e.Time);Assert.Greater(e.Agent.Biology.Budget.Division,0);
            for(int i=0;i<5;i++)w.Step();Assert.AreEqual(1,w.Life.Latest);
            Assert.IsFalse(w.RequestDivision());Assert.AreEqual(1,w.Life.Latest);
        }
        [Test] public void FailedRequestHasNoBirthEventOrUnpaidChild()
        {
            var w=Ready(.01f);Assert.IsFalse(w.RequestDivision());w.Step();
            Assert.AreEqual(0,w.Births);Assert.AreEqual(1,w.Count);
            for(long i=w.Life.Oldest;i<=w.Life.Latest;i++) {w.Life.TryRead(i,out var e);Assert.AreNotEqual(LifeEventKind.Division,e.Kind);}
        }
        [Test] public void StreamingKeepsTheSameJournalAndDoesNotInventEvents()
        {
            var ocean=new ChunkOcean(42,new MemoryRegionArchive());var journal=ocean.World.Life;
            ocean.Stream(new V3(21,0,0));Assert.AreSame(journal,ocean.World.Life);
            Assert.AreEqual(0,journal.Latest);ocean.Save();ocean.Stream(default);Assert.AreEqual(0,journal.Latest);
        }
        [Test] public void RestoringSavedWorldDoesNotReplayHistoricalBirths()
        {
            var store=new MemoryRegionArchive();var ocean=new ChunkOcean(42,store);
            for(int i=0;i<60;i++)ocean.World.Step();Assert.Greater(ocean.World.Life.Latest,0);
            ocean.Save();var loaded=new ChunkOcean(42,store);
            Assert.AreEqual(0,loaded.World.Life.Latest);Assert.AreEqual(ocean.World.Tick,loaded.World.Tick);
        }
        [Test] public void RingOverflowIsBoundedAndReadableWithoutDuplicatingSequence()
        {
            var journal=new LifeJournal();for(int i=0;i<1000;i++)journal.Record(new LifeEvent {Time=i});
            Assert.AreEqual(1000,journal.Latest);Assert.AreEqual(745,journal.Oldest);
            Assert.IsFalse(journal.TryRead(744,out _));Assert.IsFalse(journal.TryRead(1001,out _));
            for(long i=journal.Oldest;i<=journal.Latest;i++){Assert.IsTrue(journal.TryRead(i,out var e));Assert.AreEqual(i,e.Sequence);}
        }
        [Test] public void LivingMembraneIsBoundedFiniteAndDirectionallyExtends()
        {
            for(int frame=0;frame<240;frame++)for(int j=0;j<32;j++) {
                float a=j*(float)Math.PI/16,x=.5f*(float)Math.Cos(a),y=.12f,z=.5f*(float)Math.Sin(a);
                var pose=new WaterCellPose {Phase=frame*.07f,Drive=1,Activity=1,Amoeba=1,Turn=.8f,Feed=1,Damage=1};
                pose.Deform(ref x,ref y,ref z);Assert.IsFalse(float.IsNaN(x+y+z));Assert.Less(x*x+y*y+z*z,.7f);
            }
            var extended=new WaterCellPose {Phase=(float)Math.PI/2,Drive=1,Activity=1,Amoeba=1};
            float front=.5f,fy=0,fz=0,back=-.5f,by=0,bz=0;
            extended.Deform(ref front,ref fy,ref fz);extended.Deform(ref back,ref by,ref bz);
            Assert.Greater(front,Math.Abs(back)+.08f);
        }
        [Test] public void DivisionTransitionsNeverSpawnATinyFinishedCell()
        {
            Assert.AreEqual(0,WaterCellPose.DaughterVisibility(0));Assert.AreEqual(0,WaterCellPose.ShellSeparation(.5f));
            Assert.AreEqual(1,WaterCellPose.ShellSeparation(1));Assert.AreEqual(1,WaterCellPose.Separation(1));
            float last=0;for(int i=0;i<=100;i++){float s=WaterCellPose.Separation(i/100f);Assert.GreaterOrEqual(s,last);last=s;}
        }
    }
}
