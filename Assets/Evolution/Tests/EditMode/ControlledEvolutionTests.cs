using System;
using System.IO;
using Evolution.Core;
using Evolution.Presentation;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class ControlledEvolutionTests
    {
        static VolumeWorld Ready(float energy=24)
        {
            var g=new Genome {Size=1,Speed=.9f,Uptake=1,PreferredTemperature=22,Photosynthesis=.58f,OrganicSpecialization=.3f};
            var a=new VolumeAgent {Position=new V3(100,100,100),Direction=new V3(1,0,0),Biology=new Cell {Genes=g,Energy=energy,Age=6,Growth=1,Integrity=1}};
            return new VolumeWorld(1,42,new[] {a}) {Controlled=0,ActiveCenter=a.Position};
        }
        static Genome Expected(VolumeAgent a,long tick,DevelopmentDirection direction)
        {
            uint seed=2166136261;foreach(char ch in a.Identity)seed=unchecked((seed^ch)*16777619);
            var rng=new RandomSource(seed^unchecked((uint)tick)^0x6d2b79f5u);rng.Next();
            return CellEvolution.Template(a.Biology.Genes,direction).Inherit(ref rng,.15f,.06f,true);
        }
        [TestCase(DevelopmentDirection.Natural)] [TestCase(DevelopmentDirection.Photosynthesis)]
        [TestCase(DevelopmentDirection.Organic)] [TestCase(DevelopmentDirection.Mobility)]
        public void InheritanceRetainsAllGenesAndOriginalMutationAlgorithm(DevelopmentDirection direction)
        {
            var w=Ready();var original=w.Get(0);Assert.IsTrue(w.PlanDevelopment(direction));Assert.IsTrue(w.RequestDivision());w.Step();
            Assert.AreEqual(2,w.Count);var parent=w.Get(0);var child=w.Get(1);
            Assert.AreEqual(original.Biology.Genes,parent.Biology.Genes);
            Assert.AreEqual(Expected(original,0,direction),child.Biology.Genes);Assert.AreEqual(original.Biology.Genes,child.Biology.ParentGenes);
            Assert.AreEqual(parent.Biology.Id,child.Biology.ParentId);Assert.AreEqual(parent.Biology.LineageId,child.Biology.LineageId);
            Assert.AreEqual(1,child.Biology.Generation);Assert.AreEqual(direction,child.InheritedDevelopment);
            Assert.AreEqual(Observation.MutationMask(original.Biology.Genes,child.Biology.Genes),child.Biology.BirthMutationMask);
            Assert.AreEqual(Observation.MutationMask(CellEvolution.Template(original.Biology.Genes,direction),child.Biology.Genes),child.NaturalMutationMask);
            Assert.AreEqual(DevelopmentDirection.Natural,parent.PlannedDevelopment);Assert.AreEqual(DevelopmentDirection.Natural,child.PlannedDevelopment);
        }
        [TestCase(DevelopmentDirection.Photosynthesis)] [TestCase(DevelopmentDirection.Organic)] [TestCase(DevelopmentDirection.Mobility)]
        public void PaidDevelopmentBalancesCellAndEcosystemWithoutFreeBiomass(DevelopmentDirection direction)
        {
            var w=Ready();double initial=VolumeSnapshot.Read(w).Stored;
            w.PlanDevelopment(direction);w.RequestDivision();w.Step();var p=w.Get(0).Biology;var child=w.Get(1);
            float fee=CellEvolution.Cost(p,direction);Assert.AreEqual(2,fee);Assert.AreEqual(fee,child.DevelopmentPaid);
            Assert.That(p.Budget.Net,Is.EqualTo(p.Energy-p.Budget.Start).Within(.00002));
            Assert.That(p.Budget.Division,Is.EqualTo(Biology.StructureEnergy(child.Biology.Genes)+Biology.BirthOverhead(child.Biology.Genes)+child.Biology.Energy+fee).Within(.00002));
            Assert.That(VolumeSnapshot.Read(w).Stored,Is.EqualTo(initial+w.Diagnostics.ExternalNutrients+w.Diagnostics.Solar-w.Diagnostics.Dissipated).Within(.001));
            Assert.AreEqual(0,child.Biology.Age);Assert.AreEqual(0,child.Biology.Growth);
            w.Controlled=1;Assert.IsNotNull(w.DivisionBlock(1));Assert.IsFalse(w.RequestDivision());Assert.AreEqual(1,w.Births);
        }
        [Test] public void PlansAreFreeToPreviewAndCancelButCannotBypassEnergyGate()
        {
            var w=Ready(21);var before=w.Get(0);var food=w.GetFood(0);
            Assert.IsTrue(w.PlanDevelopment(DevelopmentDirection.Mobility));Assert.Greater(w.DivisionThreshold(0),before.Biology.Energy);
            Assert.AreEqual(1,VolumeSnapshot.Read(w).EnergyBlocked);
            Assert.IsFalse(w.RequestDivision());w.Step();Assert.AreEqual(1,w.Count);Assert.AreEqual(0,w.Diagnostics.Division);
            Assert.AreEqual(before.Biology.Genes,w.Get(0).Biology.Genes);Assert.AreEqual(DevelopmentDirection.Mobility,w.Get(0).PlannedDevelopment);
            for(int i=0;i<30;i++){CellEvolution.Template(before.Biology.Genes,DevelopmentDirection.Organic);CellEvolution.Changes(before.Biology);CellEvolution.EnergyReason(w,0);}
            Assert.AreEqual(1,w.Tick);Assert.AreEqual(0,w.Diagnostics.Births);
            Assert.IsTrue(w.PlanDevelopment(DevelopmentDirection.Natural));Assert.Less(w.DivisionThreshold(0),w.Get(0).Biology.Energy);
            Assert.IsTrue(w.RequestDivision());w.Step();Assert.AreEqual(2,w.Count);Assert.AreEqual(0,w.Get(1).DevelopmentPaid);
        }
        [Test] public void UncontrolledDeadInvalidAndCappedPlansAreRejected()
        {
            var w=Ready();w.Controlled=-1;Assert.IsFalse(w.PlanDevelopment(DevelopmentDirection.Mobility));w.Controlled=0;
            Assert.IsFalse(w.PlanDevelopment((DevelopmentDirection)999));
            var c=w.Get(0).Biology;c.Genes.Photosynthesis=c.Genes.OrganicSpecialization=1;c.Genes.Speed=3;
            foreach(var d in new[] {DevelopmentDirection.Photosynthesis,DevelopmentDirection.Organic,DevelopmentDirection.Mobility})Assert.IsFalse(CellEvolution.Available(c,d));
            Assert.IsTrue(CellEvolution.Available(c,DevelopmentDirection.Natural));
            var dead=w.Get(0);dead.Alive=false;var empty=new VolumeWorld(new[] {dead},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default) {Controlled=0};
            Assert.IsFalse(empty.PlanDevelopment(DevelopmentDirection.Mobility));Assert.IsFalse(empty.RequestDivision());
        }
        [Test] public void DirectionsHaveRealPhysiologicalBenefitsAndCosts()
        {
            var c=Ready().Get(0).Biology;var g=c.Genes;
            var photo=c;photo.Genes=CellEvolution.Template(g,DevelopmentDirection.Photosynthesis);
            var sample=new OceanSample {Temperature=22,Light=1};
            Assert.Greater(Ecology.PhotoRate(photo,sample),Ecology.PhotoRate(c,sample));Assert.Greater(Biology.Cost(photo.Genes,22),Biology.Cost(g,22));Assert.Greater(Biology.StructureEnergy(photo.Genes),Biology.StructureEnergy(g));
            sample.Light=0;Assert.AreEqual(0,Ecology.PhotoRate(photo,sample));
            var organic=CellEvolution.Template(g,DevelopmentDirection.Organic);
            Assert.Greater(Biology.OrganicAssimilation(organic,22),Biology.OrganicAssimilation(g,22));Assert.Less(Biology.SubstrateYield(organic,22),Biology.SubstrateYield(g,22));
            var fast=c;fast.Genes=CellEvolution.Template(g,DevelopmentDirection.Mobility);fast.SwimSpeed=Ecology.Mobility(fast.Genes);c.SwimSpeed=Ecology.Mobility(g);
            Assert.Greater(fast.SwimSpeed,c.SwimSpeed);Assert.Greater(CellMotion.SwimmingCost(fast),CellMotion.SwimmingCost(c));Assert.Greater(Biology.Cost(fast.Genes,22),Biology.Cost(g,22));
            Assert.AreNotEqual(CrystalForms.From(g),CrystalForms.From(photo.Genes),"Threshold-crossing photo change is also visible in existing morphology");
        }
        [Test] public void GenesChangeActualFeedingAndLocomotion()
        {
            var plain=Ready().Get(0);plain.Position=new V3(10,10,10);plain.Biology.Energy=10;
            var fast=plain;fast.Biology.Genes=CellEvolution.Template(plain.Biology.Genes,DevelopmentDirection.Mobility);
            var slowWorld=new VolumeWorld(new[]{plain},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default) {Controlled=0,Input=new V3(1,0,0)};
            var fastWorld=new VolumeWorld(new[]{fast},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default) {Controlled=0,Input=new V3(1,0,0)};
            for(int i=0;i<20;i++){slowWorld.Step();fastWorld.Step();}
            Assert.Greater(fastWorld.Get(0).Position.X,slowWorld.Get(0).Position.X);Assert.Less(fastWorld.Get(0).Biology.Energy,slowWorld.Get(0).Biology.Energy);
            var a=plain.Biology;var b=a;b.Genes=CellEvolution.Template(a.Genes,DevelopmentDirection.Organic);float sa=0,sb=0,oa=10,ob=10;
            Biology.Feed(ref a,ref sa,ref oa,22,1);Biology.Feed(ref b,ref sb,ref ob,22,1);Assert.Greater(b.Energy,a.Energy);
        }
        [Test] public void OffspringSelectionRejectsUnrelatedOrganismsAndCostsNoBiology()
        {
            var w=Ready();var play=new CellLineagePlay();play.TakeControl(w,0);w.RequestDivision();w.Step();
            var p=w.Get(0);var child=w.Get(1);var state=VolumeSnapshot.Read(w);long births=w.Diagnostics.Births,tick=w.Tick;
            Assert.IsFalse(play.FollowOffspring(w,0));Assert.IsTrue(play.FollowOffspring(w,1));Assert.AreEqual(child.Identity,play.Identity);Assert.AreEqual(1,w.Controlled);
            Assert.AreEqual(state,VolumeSnapshot.Read(w));Assert.AreEqual(births,w.Diagnostics.Births);Assert.AreEqual(tick,w.Tick);Assert.AreEqual(p,w.Get(0));Assert.AreEqual(child,w.Get(1));
        }
        [Test] public void DirectChildrenExcludeGrandchildrenAndOtherFamilies()
        {
            var parent=Ready().Get(0);var child=parent;child.Identity=parent.Identity+"/b1";child.Biology.ParentId=parent.Biology.Id;child.Biology.Generation=1;child.Biology.Id=2;
            var grandchild=child;grandchild.Identity=child.Identity+"/b2";grandchild.Biology.ParentId=child.Biology.Id;grandchild.Biology.Generation=2;var unrelated=child;unrelated.Identity="other/b1";
            var w=new VolumeWorld(new[]{parent,child,grandchild,unrelated},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default);
            CollectionAssert.AreEqual(new[]{1},CellEvolution.Offspring(w,parent.Identity));
        }
        [Test] public void IdentityPrefixCannotSubstituteForBiologicalParentage()
        {
            var w=Ready();var play=new CellLineagePlay();play.TakeControl(w,0);var a=w.Get(0);var impostor=a;
            impostor.Identity=a.Identity+"/b123";impostor.Biology.ParentId=999;impostor.Biology.LineageId=999;impostor.Biology.Generation=1;
            var withoutParent=new VolumeWorld(new[]{impostor},Array.Empty<FoodCloud>(),Array.Empty<V3>(),10,default);
            Assert.IsFalse(play.FollowOffspring(withoutParent));Assert.AreEqual(-1,withoutParent.Controlled);
            var withParent=new VolumeWorld(new[]{a,impostor},Array.Empty<FoodCloud>(),Array.Empty<V3>(),10,default);
            Assert.AreEqual(0,CellEvolution.Offspring(withParent,a.Identity).Count);
        }
        [Test] public void MissingArchivedCellIsNotDeclaredDeadButRealDeathAllowsFamilyRecovery()
        {
            var w=Ready(.001f);var play=new CellLineagePlay();play.TakeControl(w,0);
            play.RecordStep(new VolumeWorld(Array.Empty<VolumeAgent>(),Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default));Assert.IsFalse(play.Fallen.HasValue);
            w.Step();play.RecordStep(w);Assert.IsTrue(play.Fallen.HasValue);StringAssert.Contains("энергии",CellEvolution.DeathReason(play.Fallen.Value));
            var ghost=w.Get(0);var child=Ready().Get(0);child.Identity=ghost.Identity+"/b1";child.Biology.ParentId=ghost.Biology.Id;child.Biology.Generation=ghost.Biology.Generation+1;
            var after=new VolumeWorld(new[]{child},Array.Empty<FoodCloud>(),Array.Empty<V3>(),100,default);
            Assert.IsTrue(play.FollowOffspring(after));Assert.IsFalse(play.Fallen.HasValue);Assert.AreEqual(child.Identity,play.Identity);
        }
        [Test] public void FeedbackExplainsRealFeedingAndPredationAndReportsInheritedDeltas()
        {
            var w=Ready();StringAssert.Contains("вне пищевого облака",CellEvolution.EnergyReason(w,0));
            var a=w.Get(0);a.Alive=false;a.Biology.Consumed=true;StringAssert.Contains("хищником",CellEvolution.DeathReason(a));
            w.PlanDevelopment(DevelopmentDirection.Photosynthesis);w.RequestDivision();w.Step();StringAssert.Contains("Фотосинтез",CellEvolution.Changes(w.Get(1).Biology));
            Assert.IsNotNull(w.DivisionBlock(1));Assert.AreEqual(w.Get(0).Biology.Genes,w.Get(1).Biology.ParentGenes);
        }
        [Test] public void DirectedGenesRemainTheBasisOfTheNextGeneration()
        {
            var w=Ready();w.PlanDevelopment(DevelopmentDirection.Photosynthesis);w.RequestDivision();w.Step();var child=w.Get(1);
            child.Biology.Energy=Biology.Capacity(child.Biology.Genes);child.Biology.Age=6;child.Biology.Growth=1;
            var next=new VolumeWorld(new[]{child},Array.Empty<FoodCloud>(),Array.Empty<V3>(),200,default) {Controlled=0,ActiveCenter=child.Position};
            next.RequestDivision();next.Step();var grandchild=next.Get(1);
            Assert.AreEqual(child.Biology.Genes,grandchild.Biology.ParentGenes);Assert.AreEqual(2,grandchild.Biology.Generation);
            Assert.AreEqual(Expected(child,200,DevelopmentDirection.Natural),grandchild.Biology.Genes);Assert.AreEqual(0,grandchild.DevelopmentPaid);
        }
        [Test] public void PlannedDevelopmentCannotBypassPopulationLimitOrChargeAFailedBirth()
        {
            var a=Ready().Get(0);var agents=new VolumeAgent[VolumeWorld.PopulationLimit];agents[0]=a;
            for(int i=1;i<agents.Length;i++){var dead=a;dead.Alive=false;dead.Identity="dead-"+i;dead.DiedAt=0;agents[i]=dead;}
            var w=new VolumeWorld(agents,Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default) {Controlled=0,ActiveCenter=a.Position};
            w.PlanDevelopment(DevelopmentDirection.Mobility);Assert.IsFalse(w.RequestDivision());w.Step();
            Assert.AreEqual(0,w.Births);Assert.AreEqual(0,w.Diagnostics.Division);Assert.AreEqual(0,w.Diagnostics.BirthOverhead);
            Assert.AreEqual(DevelopmentDirection.Mobility,w.Get(0).PlannedDevelopment);
        }
        [Test] public void ChunkDepartureAndReturnPreservePlanWithoutEnergyPayment()
        {
            var a=Ready().Get(0);a.Position=new V3(10,10,10);a.Previous=a.Position;
            var region=new RegionState {Key=default};region.Agents.Add(a);var archive=new MemoryRegionArchive();archive.Save(region);
            var ocean=new ChunkOcean(42,archive);int index=ocean.Find(a.Identity);ocean.World.Controlled=index;
            Assert.IsTrue(ocean.World.PlanDevelopment(DevelopmentDirection.Organic));var planned=ocean.World.Get(index);
            ocean.Stream(new V3(140,0,0));ocean.Stream(default);Assert.AreEqual(planned,ocean.World.Get(ocean.Find(a.Identity)));
            Assert.AreEqual(0,ocean.World.Diagnostics.Division);Assert.AreEqual(0,ocean.World.Tick);
        }
        [Test] public void RealV3OffspringKeepsItsGenesAndNaturalMutationHistory()
        {
            // Produced by BinaryRegionArchive at PR #28 head c8c4e02, with a paid natural birth.
            const string fixture="M09WRQMAAAABAAAAAAAAAAIAAAANbG9jYWwtYWdlbnQtMADmbhpBvugfQZfpH0EAACBBAAAgQQAAIEHr5H8/hJWpPEtjozwAAIA/AAAAAAAAAACqIx49wl0APSVa9zwBAAAAAAAA8P8AAAAAAADw/wAAAAAAAPD/AAAAAAAAAAAAAACgmZmpPwgAAAAAAACgmZmpP+gHIEFrBiBBLwYgQevkfz+Elak8S2OjPAABAAAAAQAAAAAAAAAAAAAAAAAAAJrAK0GamcFAAACAPwAAgD8AAAAAAAAAAAAAAAAAAAAAAAAAAJIyEz8AAAAAgD8AAIA/AACAPwAAsEEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwEEAAAAAAAAAAAAAAAAAAAAA37T3PLFpPDoAAAAAAAAAAAAAAAAAAAAAAAAAAJrAU0EAAAAAAAAAAAAAAAAAAAAAsWk8OgAAAAABAAAAAAAAABBsb2NhbC1hZ2VudC0wL2IxAOqgJUEYJCBBxyIgQegHIEFrBiBBLwYgQevkfz+Elak8S2OjPOvkfz+Elak8S2OjPEQSQT7GFQ09H+4HPQEAAAAAAADw/wAAAAAAAPD/AAAAAAAA8P8AAACgmZmpPwAAAKCZmak/AAAAAAAAAKCZmak/6AcgQWsGIEEvBiBB6+R/P4SVqTxLY6M8AZ0QDGsBAAAAAQAAAAEAAAAAAAAAmsArQQAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAPwAAgD8AAIA/AACwQQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAPwAAgD8AAIA/AACwQQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAmsArQQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAAAAAAQAAAAxsb2NhbC1mb29kLTEBAAAARcW8wHci/79f1qxAzcwMQAAADEIAAAAAAgAAAAAAAAA=";
            string path=Path.Combine(Path.GetTempPath(),"evolution-parent-v3-"+Guid.NewGuid());Directory.CreateDirectory(path);
            try {
                File.WriteAllBytes(Path.Combine(path,"0_0_0.region"),Convert.FromBase64String(fixture));
                var store=new BinaryRegionArchive(path);Assert.IsTrue(store.TryLoad(default,out var r));Assert.AreEqual(2,r.Agents.Count);
                var child=r.Agents[1];Assert.AreEqual(1,child.Biology.Generation);Assert.AreEqual(DevelopmentDirection.Natural,child.InheritedDevelopment);
                Assert.AreEqual(child.Biology.BirthMutationMask,child.NaturalMutationMask);Assert.AreEqual(0,child.DevelopmentPaid);
                Assert.AreEqual(r.Agents[0].Biology.Genes,child.Biology.ParentGenes);store.Save(r);
                Assert.IsTrue(store.TryLoad(default,out var upgraded));CollectionAssert.AreEqual(r.Agents,upgraded.Agents);
            } finally {Directory.Delete(path,true);}
        }
        [Test] public void V4SavePreservesPendingPlanPaidBirthAndMutationProvenance()
        {
            string path=Path.Combine(Path.GetTempPath(),"evolution-guidance-"+Guid.NewGuid());
            try {
                var w=Ready();w.PlanDevelopment(DevelopmentDirection.Organic);var state=new RegionState {Key=default};state.Agents.Add(w.Get(0));
                var store=new BinaryRegionArchive(path);store.Save(state);Assert.IsTrue(store.TryLoad(default,out var pending));Assert.AreEqual(state.Agents[0],pending.Agents[0]);
                w.RequestDivision();w.Step();state.Agents.Clear();state.Agents.Add(w.Get(0));state.Agents.Add(w.Get(1));store.Save(state);
                Assert.IsTrue(new BinaryRegionArchive(path).TryLoad(default,out var loaded));CollectionAssert.AreEqual(state.Agents,loaded.Agents);
                var rebuilt=new VolumeWorld(loaded.Agents.ToArray(),Array.Empty<FoodCloud>(),Array.Empty<V3>(),w.Tick,default);
                var play=new CellLineagePlay();play.TakeControl(rebuilt,0);Assert.IsTrue(play.FollowOffspring(rebuilt));Assert.AreEqual(w.Get(1).Biology.Genes,rebuilt.Get(rebuilt.Controlled).Biology.Genes);
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
