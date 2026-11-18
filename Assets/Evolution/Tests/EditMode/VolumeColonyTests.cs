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
        [Test] public void WholeControlMovesEveryRealCellAndChargesEveryMotor()
        {
            var w=Pair();w.ControlColony=true;w.Input=new V3(0,1,0);var a=w.Get(0).Position;var b=w.Get(1).Position;
            w.Step();Assert.AreEqual(2,w.OrganismSize(1));
            Assert.That(((w.Get(0).Position-a)-(w.Get(1).Position-b)).Length,Is.LessThan(.0001));
            Assert.Greater(w.Get(0).Biology.Budget.Movement,0);Assert.Greater(w.Get(1).Biology.Budget.Movement,0);
        }
        [TestCase(CellRole.Feeding)] [TestCase(CellRole.Movement)] [TestCase(CellRole.Defense)]
        public void SpecializationsTradeRealFunctionsForOtherFunctions(CellRole role)
        {
            var c=Ready().Get(0).Biology;c.ColonyId=1;c.Role=role;c.Specialization=.8f;
            var general=c;general.Role=CellRole.Generalist;general.Specialization=0;
            Assert.Greater(Multicellularity.Cost(c,22),Multicellularity.Cost(general,22));
            if(role==CellRole.Feeding) {
                var sample=new OceanSample {Temperature=22,Light=1};
                Assert.Greater(Ecology.PhotoRate(c,sample),Ecology.PhotoRate(general,sample));
                Assert.Less(Multicellularity.Motor(c),Multicellularity.Motor(general));
                sample.Light=0;Assert.AreEqual(0,Ecology.PhotoRate(c,sample));
            }else if(role==CellRole.Movement) {
                Assert.Greater(Multicellularity.Motor(c),Multicellularity.Motor(general));Assert.Less(Multicellularity.Feeding(c),1);
            }else {Assert.Greater(Ecology.Defense(c,0),Ecology.Defense(general,0));Assert.Less(Multicellularity.Feeding(c),1);Assert.Less(Multicellularity.Motor(c),1);}
        }
        [Test] public void ColonySurvivesFarLodAndArchiveRoundTrip()
        {
            var w=Pair();w.Controlled=-1;w.ActiveCenter=new V3(1e8f,1e8f,1e8f);
            for(int t=0;t<20;t++)w.Step();Assert.AreEqual(2,w.OrganismSize(0));
            string dir=Path.Combine(Path.GetTempPath(),"colony-"+Guid.NewGuid());
            try {
                var archive=new BinaryRegionArchive(dir);var region=new RegionState {Key=default,LastTick=w.Tick};
                region.Agents.Add(w.Get(0));region.Agents.Add(w.Get(1));archive.Save(region);Assert.IsTrue(archive.TryLoad(default,out var loaded));
                for(int i=0;i<2;i++){Assert.AreEqual(region.Agents[i].BondIdentity,loaded.Agents[i].BondIdentity);Assert.AreEqual(region.Agents[i].Biology.Specialization,loaded.Agents[i].Biology.Specialization);Assert.AreEqual(region.Agents[i].PlannedConnection,loaded.Agents[i].PlannedConnection);Assert.AreEqual(region.Agents[i].Biology.Genes,loaded.Agents[i].Biology.Genes);}
                var copy=new VolumeWorld(loaded.Agents.ToArray(),Array.Empty<FoodCloud>(),Array.Empty<V3>(),loaded.LastTick,default);Assert.AreEqual(2,copy.OrganismSize(0));
            }finally{Directory.Delete(dir,true);}
        }
        [Test] public void StreamingKeepsStraddlingColonyTogetherWithoutDuplicatedBirths()
        {
            var pair=Pair();var a=pair.Get(0);var b=pair.Get(1);a.Position=new V3(19.95f,0,0);b.Position=new V3(20.6f,0,0);
            var archive=new MemoryRegionArchive();
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)archive.Save(new RegionState {Key=new ChunkKey(x,y,z)});
            var r=new RegionState {Key=default};r.Agents.Add(a);r.Agents.Add(b);archive.Save(r);
            var ocean=new ChunkOcean(42,archive);Assert.AreEqual(2,ocean.World.OrganismSize(ocean.Find(a.Identity)));
            ocean.Save();ocean.Stream(new V3(200,0,0));ocean.Stream(default);
            int first=ocean.Find(a.Identity),second=ocean.Find(b.Identity);Assert.GreaterOrEqual(first,0);Assert.GreaterOrEqual(second,0);
            Assert.AreEqual(2,ocean.World.OrganismSize(first));Assert.AreEqual(a.Identity,ocean.World.Get(second).BondIdentity);
            int count=0;for(int i=0;i<ocean.World.Count;i++)if(ocean.World.Get(i).Identity==a.Identity||ocean.World.Get(i).Identity==b.Identity)count++;
            Assert.AreEqual(2,count);Assert.AreEqual(0,ocean.World.Diagnostics.Deaths);Assert.AreEqual(0,ocean.World.Diagnostics.Births);
        }
        [TestCase(1f,0f)] [TestCase(0f,20f)] [TestCase(0f,0f)]
        public void FeedingSpecialistMeasuredInLightOrganicAndStarvation(float light,float organic)
        {
            var g=Ready().Get(0).Biology.Genes;g.OrganicSpecialization=.8f;
            var general=new Cell {Genes=g,Energy=0};var specialist=general;specialist.Role=CellRole.Feeding;specialist.Specialization=.8f;specialist.ColonyId=1;
            float sg=light>0?20:0,ss=sg,og=organic,os=organic;var environment=new OceanSample {Light=light,Temperature=22};
            Ecology.Photosynthesize(ref general,ref sg,environment,1);Biology.Feed(ref general,ref sg,ref og,22,1);
            Ecology.Photosynthesize(ref specialist,ref ss,environment,1);Biology.Feed(ref specialist,ref ss,ref os,22,1);
            float ng=general.Energy-Multicellularity.Cost(general,22),ns=specialist.Energy-Multicellularity.Cost(specialist,22);
            TestContext.WriteLine($"light={light} organic={organic} general_net={ng:F6} feeding_net={ns:F6}");
            if(light>0||organic>0)Assert.Greater(ns,ng);else Assert.Less(ns,ng);
        }
        [Test] public void DefensiveRoleReducesRealPredationDamage()
        {
            var w=Ready();var plain=w.Get(0).Biology;plain.Id=5;plain.LineageId=5;plain.Energy=20;plain.Integrity=1;
            var armored=plain;armored.Role=CellRole.Defense;armored.Specialization=.8f;armored.ColonyId=1;
            var hunter=plain;hunter.Id=2;hunter.LineageId=2;hunter.Genes.Predation=1;hunter.Energy=1;var other=hunter;
            Ecology.Bite(ref hunter,ref plain,0,1,out _);Ecology.Bite(ref other,ref armored,0,1,out _);
            Assert.Greater(armored.Integrity,plain.Integrity);Assert.Greater(Multicellularity.Cost(armored,22),Multicellularity.Cost(plain,22));
        }
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ReadsPreviousWireSchemasAsSolitaryCells(int version)
        {
            string dir=Path.Combine(Path.GetTempPath(),"colony-old-"+Guid.NewGuid());
            try {
                var archive=new BinaryRegionArchive(dir);var region=new RegionState {Key=default};region.Agents.Add(Ready().Get(0));archive.Save(region);
                string file=Path.Combine(dir,"0_0_0.region");var bytes=File.ReadAllBytes(file);
                // v6 appends initialized + two matter floats (9 bytes). One empty bond string + seven scalar v5 fields. v4 adds four development scalars; v3 adds order.
                int remove=9+(version<5?29:0)+(version<4?16:0)+(version<3?8:0);var legacy=new byte[bytes.Length-remove];
                Array.Copy(bytes,legacy,legacy.Length-4);Array.Copy(BitConverter.GetBytes(version),0,legacy,4,4);File.WriteAllBytes(file,legacy);
                Assert.IsTrue(archive.TryLoad(default,out var loaded));var a=loaded.Agents[0];Assert.IsNull(a.BondIdentity);Assert.AreEqual(CellRole.Generalist,a.Biology.Role);
                Assert.AreEqual(region.Agents[0].Biology.Genes,a.Biology.Genes);Assert.AreEqual(region.Agents[0].Biology.Energy,a.Biology.Energy);
            } finally {Directory.Delete(dir,true);}
        }
        [Test] public void CompactionKeepsStableEdgesAfterDeadSlotsAreRemoved()
        {
            var pair=Pair();var cells=new VolumeAgent[VolumeWorld.PopulationLimit];cells[0]=pair.Get(0);cells[1]=pair.Get(1);
            for(int i=2;i<cells.Length;i++)cells[i]=new VolumeAgent {Identity="dead-"+i,Order=i+1,Alive=false,DiedAt=0};
            var w=new VolumeWorld(cells,Array.Empty<FoodCloud>(),Array.Empty<V3>(),100,default);w.CompactArchived();
            Assert.AreEqual(2,w.Count);Assert.AreEqual(2,w.OrganismSize(1));Assert.AreEqual(0,w.BondParent(1));Assert.AreEqual(pair.Get(0).Identity,w.Get(1).BondIdentity);
        }
    }
}

