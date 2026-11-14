using System;
using System.IO;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class VolumeColonyTests
    {
        internal static VolumeWorld Ready(float adhesion=1,float energy=24)
        {
            var g=new Genome {Size=1,Speed=.5f,Uptake=2,PreferredTemperature=22,Photosynthesis=.8f,Adhesion=adhesion,ColonySize=1,Branching=1,Differentiation=.8f,Transport=.8f};
            return new VolumeWorld(1,42,new[]{new VolumeAgent {Position=new V3(100,100,100),Direction=new V3(1,0,0),Biology=new Cell {Genes=g,Energy=energy,Age=10,Growth=1,Integrity=1}}}){Controlled=0,ActiveCenter=new V3(100,100,100)};
        }
        internal static VolumeWorld Pair()
        {var w=Ready();Assert.IsTrue(w.PlanConnection(BirthConnection.Retain));Assert.IsTrue(w.RequestDivision());w.Step();Assert.AreEqual(2,w.Count);return w;}
        [Test] public void RetainedBirthPaysAndConservesLedger()
        {
            var w=Ready();double initial=VolumeSnapshot.Read(w).Stored;w.PlanConnection(BirthConnection.Retain);w.RequestDivision();w.Step();
            Assert.AreEqual(2,w.OrganismSize(0));Assert.AreEqual(w.Get(0).Identity,w.Get(1).BondIdentity);
            Assert.AreEqual(.3,w.Diagnostics.Contact,1e-6);Assert.AreEqual(w.Get(0).Biology.Id,w.Get(1).Biology.ParentId);
            Assert.AreEqual(initial+w.Diagnostics.ExternalNutrients+w.Diagnostics.Solar-w.Diagnostics.Dissipated,VolumeSnapshot.Read(w).Stored,1e-3);
            Assert.AreEqual(w.Get(0).Biology.Energy-w.Get(0).Biology.Budget.Start,w.Get(0).Biology.Budget.Net,1e-4);
        }
        [Test] public void RefusedPlanAndPoorParentPayNothing()
        {
            var w=Ready(0);var a=w.Get(0);Assert.IsFalse(w.PlanConnection(BirthConnection.Retain));Assert.AreEqual(a.Biology.Energy,w.Get(0).Biology.Energy);
            w=Ready(1,20.5f);Assert.IsTrue(w.PlanConnection(BirthConnection.Retain));Assert.IsFalse(w.RequestDivision());Assert.AreEqual(0,w.Diagnostics.Contact);Assert.AreEqual(1,w.Count);
        }
        [Test] public void DetachPreservesCellsAndAllowsAutonomousSurvival()
        {
            var w=Pair();double before=VolumeSnapshot.Read(w).Stored;
            Assert.IsTrue(w.Detach(1));Assert.IsFalse(w.Detach(1));Assert.AreEqual(1,w.OrganismSize(0));Assert.AreEqual(1,w.OrganismSize(1));
            Assert.AreEqual(before,VolumeSnapshot.Read(w).Stored);w.Step();Assert.IsTrue(w.Get(0).Alive&&w.Get(1).Alive);
        }
        [Test] public void ParentDeathDoesNotKillChild()
        {
            var w=Pair();var a=w.Get(0);var b=w.Get(1);a.Biology.Energy=0;
            var replay=new VolumeWorld(new[]{a,b},Array.Empty<FoodCloud>(),Array.Empty<V3>(),w.Tick,default);replay.Step();
            Assert.IsFalse(replay.Get(0).Alive);Assert.IsTrue(replay.Get(1).Alive);Assert.IsNull(replay.Get(1).BondIdentity);Assert.AreEqual(1,replay.OrganismSize(1));
        }
        [Test] public void ExchangeAccountsForLossAndRespectsBandwidth()
        {
            var w=Pair();var a=w.Get(0);var b=w.Get(1);a.Biology.Energy=20;b.Biology.Energy=2;
            var replay=new VolumeWorld(new[]{a,b},Array.Empty<FoodCloud>(),Array.Empty<V3>(),1,default){ActiveCenter=a.Position,Controlled=0};
            double initial=VolumeSnapshot.Read(replay).Stored;replay.Step();
            Assert.Greater(replay.Diagnostics.TransportReceived,0);Assert.LessOrEqual(replay.Diagnostics.TransportReceived,.8*4*.05*.95+1e-6);
            Assert.Greater(replay.Diagnostics.TransportLoss,0);Assert.AreEqual(initial-replay.Diagnostics.Dissipated,VolumeSnapshot.Read(replay).Stored,1e-3);
        }
        [Test] public void InvalidCycleAndDuplicateIdentityCannotBecomeColony()
        {
            var w=Pair();var a=w.Get(0);var b=w.Get(1);a.BondIdentity=b.Identity;
            var replay=new VolumeWorld(new[]{a,b},Array.Empty<FoodCloud>(),Array.Empty<V3>(),1,default);
            Assert.IsNull(replay.Get(0).BondIdentity);Assert.AreEqual(0,replay.BondParent(1));
            Assert.Throws<InvalidOperationException>(()=>new VolumeWorld(new[]{a,a},Array.Empty<FoodCloud>(),Array.Empty<V3>(),1,default));
        }
        [Test] public void SameSeedProducesSamePaidConnections()
        {
            var a=Pair();var b=Pair();for(int t=0;t<100;t++){a.Step();b.Step();}
            Assert.AreEqual(a.Count,b.Count);for(int i=0;i<a.Count;i++){Assert.AreEqual(a.Get(i).Biology.Energy,b.Get(i).Biology.Energy);Assert.AreEqual(a.Get(i).BondIdentity,b.Get(i).BondIdentity);Assert.AreEqual(a.Get(i).Position,b.Get(i).Position);}
        }
    }
}
