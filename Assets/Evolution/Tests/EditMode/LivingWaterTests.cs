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
        [Test] public void RealDeathIsPublishedOnceAfterCoreRecyclingAndDoesNotCreateVfxFood()
        {
            var w=Ready(.00001f);w.Step();Assert.IsFalse(w.Get(0).Alive);
            Assert.AreEqual(1,w.Life.Latest);Assert.IsTrue(w.Life.TryRead(1,out var e));
            Assert.AreEqual(LifeEventKind.Death,e.Kind);Assert.AreEqual(DeathAppearance.Exhaustion,e.Cause);
            Assert.AreEqual(w.Get(0),e.Agent);Assert.AreEqual(0,e.Agent.Biology.Energy);
            float organic=0;for(int k=0;k<w.FoodCount;k++)organic+=w.GetFood(k).Organic;
            var diagnostics=w.Diagnostics.Clone();
            for(int frame=0;frame<240;frame++) {
                Assert.IsTrue(w.Life.TryRead(1,out var snapshot));
                WaterCellPose.Remains(frame/60f);WaterCellPose.Rupture(frame/60f);
                Assert.AreEqual(e.Agent,snapshot.Agent);
            }
            float after=0;for(int k=0;k<w.FoodCount;k++)after+=w.GetFood(k).Organic;
            Assert.AreEqual(organic,after);Assert.AreEqual(diagnostics.RecyclingLoss,w.Diagnostics.RecyclingLoss);
            for(int step=0;step<90;step++)w.Step();Assert.AreEqual(1,w.Life.Latest);
            Assert.AreEqual(1,w.Diagnostics.Deaths);
        }
        [Test] public void DamageAppearanceUsesConsumedStateAndDoesNotInventAnotherDeathRule()
        {
            var a=Ready(.00001f).Get(0);a.Biology.Consumed=true;
            var w=new VolumeWorld(1,42,new[] {a}) {Controlled=0,ActiveCenter=a.Position};w.Step();
            Assert.IsTrue(w.Life.TryRead(1,out var e));Assert.AreEqual(DeathAppearance.Damage,e.Cause);
        }
        [Test] public void DeathSequenceHasDwellRuptureAndCompleteDissolution()
        {
            Assert.AreEqual(0,WaterCellPose.Rupture(.5f));Assert.AreEqual(1,WaterCellPose.Remains(1.5f));
            Assert.Greater(WaterCellPose.Rupture(1.2f),0);Assert.Less(WaterCellPose.Remains(3),1);
            Assert.AreEqual(0,WaterCellPose.Remains(4));
        }
        [TestCase(15)] [TestCase(60)] [TestCase(144)]
        public void PresentationSamplingDoesNotChangeCoreAtAnyRenderRate(int fps)
        {
            var baseline=Ready();var observed=Ready();baseline.RequestDivision();observed.RequestDivision();
            for(int tick=0;tick<400;tick++) {
                baseline.Step();observed.Step();
                for(int frame=0;frame<fps;frame++) {
                    var c=observed.Get(0).Biology;var animation=new CellAnimation(c.Id,AnimationTargets.From(c,observed.Sample(observed.Get(0).Position),c.SwimSpeed,.05f));
                    animation.Advance(.05*(frame+1)/fps);
                    var pose=WaterCellPose.From(animation,c,observed.Time);
                    float x=.3f,y=.1f,z=.2f;pose.Deform(ref x,ref y,ref z);
                    for(long seq=observed.Life.Oldest;seq<=observed.Life.Latest;seq++)observed.Life.TryRead(seq,out _);
                }
                Assert.AreEqual(baseline.Count,observed.Count);
                for(int i=0;i<baseline.Count;i++)Assert.AreEqual(baseline.Get(i),observed.Get(i));
                Assert.AreEqual(baseline.Diagnostics.RecyclingLoss,observed.Diagnostics.RecyclingLoss);
                for(int k=0;k<baseline.FoodCount;k++)Assert.AreEqual(baseline.GetFood(k),observed.GetFood(k));
            }
        }
    }
}
