using System;
using System.IO;
using System.Collections.Generic;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class VolumeBalanceTests
    {
        static void Valid(VolumeWorld w)
        {
            int live=0;
            for(int i=0;i<w.Count;i++) {var a=w.Get(i);if(!a.Alive)continue;live++;
                Assert.IsFalse(float.IsNaN(a.Biology.Energy));Assert.IsFalse(float.IsInfinity(a.Position.Sqr));
                Assert.Greater(a.Biology.Energy,0);Assert.LessOrEqual(a.Biology.Energy,Biology.Capacity(a.Biology.Genes)+.0001f);
            }
            Assert.LessOrEqual(live,VolumeWorld.PopulationLimit);
            for(int i=0;i<w.FoodCount;i++) {var f=w.GetFood(i);
                Assert.That(f.Substrate,Is.InRange(0,CloudEcology.SubstrateCapacity+.0001f));
                Assert.GreaterOrEqual(f.Organic,0);Assert.IsFalse(float.IsInfinity(f.Organic));Assert.IsFalse(float.IsNaN(f.Organic));
            }
        }
        [TestCase(42u)] [TestCase(7u)] [TestCase(123u)] [TestCase(2026u)]
        public void FixedSeedsContinuePaidLifecycleFor600SecondsAndBalanceEnergy(uint seed)
        {
            var ocean=new ChunkOcean(seed,new MemoryRegionArchive());double initial=VolumeSnapshot.Read(ocean.World).Stored;
            long birthsAt300=0,deathsAt300=0;int peak=0;
            for(int tick=1;tick<=12000;tick++) {
                ocean.World.Step();ocean.Stream(default);
                if(tick%200!=0)continue;Valid(ocean.World);
                var total=ocean.Snapshot(out var archived);var d=ocean.World.Diagnostics;peak=Math.Max(peak,total.Alive);
                Assert.Greater(total.Alive,0,$"seed {seed}, tick {tick}");
                Assert.AreEqual(250+d.Births-d.Deaths,total.Alive,"Unloading is not a death, loading is not a birth");
                double throughput=initial+d.ExternalNutrients+d.Solar;
                Assert.That(total.Stored,Is.EqualTo(throughput-d.Dissipated).Within(throughput*.00005),$"energy ledger seed {seed} tick {tick}");
                if(tick==6000){birthsAt300=d.Births;deathsAt300=d.Deaths;}
            }
            var final=ocean.Snapshot(out var offline);var ledger=ocean.World.Diagnostics;
            Assert.Greater(ledger.Births,birthsAt300+10);Assert.Greater(ledger.Deaths,deathsAt300+10);
            Assert.Greater(ledger.MaxGeneration,3);Assert.Less(final.Alive,500);Assert.Less(peak,800);
            Assert.Greater(ledger.ExternalNutrients,0);Assert.Greater(ledger.Solar,0);
        }
        [Test] public void RegenerationIsRateLimitedCapacityLimitedAndIndependentOfCellCount()
        {
            var f=new FoodCloud {Substrate=0,Organic=0};var d=new VolumeDiagnostics();
            CloudEcology.Advance(ref f,.05f,d);
            Assert.That(f.Substrate,Is.EqualTo(CloudEcology.SupplyPerSecond*.05f).Within(.000001));
            Assert.That(d.ExternalNutrients,Is.EqualTo(f.Substrate*2).Within(.000001));
            for(int i=0;i<20000;i++)CloudEcology.Advance(ref f,.05f,d);
            Assert.AreEqual(CloudEcology.SubstrateCapacity,f.Substrate);
            double before=d.ExternalNutrients;CloudEcology.Advance(ref f,600,d);Assert.AreEqual(before,d.ExternalNutrients);
            var a=new FoodCloud();var b=a;var da=new VolumeDiagnostics();var db=new VolumeDiagnostics();
            CloudEcology.Advance(ref a,10,da);for(int i=0;i<200;i++)CloudEcology.Advance(ref b,.05f,db);
            Assert.That(a.Substrate,Is.EqualTo(b.Substrate).Within(.00001));
        }
        [Test] public void DetritusRecyclingLosesEnergyAndDoesNotCreateAnExternalSource()
        {
            var f=new FoodCloud {Organic=100};var d=new VolumeDiagnostics();
            for(int i=0;i<12000;i++)CloudEcology.Advance(ref f,.05f,d,false);
            Assert.Greater(d.Recycled,0);Assert.Greater(d.DecayLoss,d.Recycled);Assert.AreEqual(0,d.ExternalNutrients);
            Assert.That(2*f.Substrate+f.Organic+d.DecayLoss,Is.EqualTo(100).Within(.002));
        }
        [Test] public void EmptyClosedRegionStarvesWithoutResurrectionOrFreeBirths()
        {
            var a=new VolumeWorld(1).Get(0);a.Biology.Energy=1;a.Position=new V3(10,10,10);
            var w=new VolumeWorld(new[] {a},Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default) {ActiveCenter=a.Position};
            for(int i=0;i<12000;i++)w.Step();
            Assert.AreEqual(0,VolumeSnapshot.Read(w).Alive);Assert.AreEqual(0,w.Diagnostics.Births);Assert.AreEqual(1,w.Diagnostics.Starved);
            Assert.AreEqual(1,w.Count);
        }
        [Test] public void FullReplayIsDeterministicIncludingArchivesAndDiagnostics()
        {
            var a=new ChunkOcean(42,new MemoryRegionArchive());var b=new ChunkOcean(42,new MemoryRegionArchive());
            for(int i=0;i<12000;i++) {
                a.World.Step();b.World.Step();a.Stream(default);b.Stream(default);
                if(i%200==199){Assert.AreEqual(a.Snapshot(out var aa),b.Snapshot(out var bb));Assert.AreEqual(aa,bb);}
            }
            Assert.AreEqual(a.World.Count,b.World.Count);Assert.AreEqual(a.World.Diagnostics.Dissipated,b.World.Diagnostics.Dissipated);
            for(int i=0;i<a.World.Count;i++)Assert.AreEqual(a.World.Get(i),b.World.Get(i));
            for(int i=0;i<a.World.FoodCount;i++)Assert.AreEqual(a.World.GetFood(i),b.World.GetFood(i));
        }
        static MemoryRegionArchive EmptyArchive()
        {
            var store=new MemoryRegionArchive();
            for(int x=-2;x<=9;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)store.Save(new RegionState {Key=new ChunkKey(x,y,z)});
            return store;
        }
        [Test] public void ArchivedCatchupUsesSameFarModelAndCannotDuplicateResourcesOnReturn()
        {
            var initial=new VolumeWorld(1).Get(0);initial.Position=new V3(10,10,10);initial.Previous=initial.Position;
            var food=new FoodCloud {Identity="fixture-food",Position=initial.Position,Radius=2.2f,Substrate=5,Organic=10};
            var store=EmptyArchive();var region=new RegionState {Key=default};region.Agents.Add(initial);region.Food.Add(food);store.Save(region);
            var ocean=new ChunkOcean(42,store);ocean.Stream(new V3(140,0,0));
            // Reference is exactly the regional far model with the same obstacle and environmental offset.
            var rng=new RandomSource(new ChunkKey().Seed(42)^0x3817u);
            var obstacle=new V3(rng.Range(3,17),rng.Range(3,17),rng.Range(3,17));
            var offsetRng=new RandomSource(42^0x718197u);var offset=new V3(offsetRng.Range(-100,100),offsetRng.Range(-100,100),offsetRng.Range(-100,100));
            var reference=new VolumeWorld(new[] {initial},new[] {food},new[] {obstacle},0,offset) {ActiveCenter=new V3(1e10f,1e10f,1e10f)};
            for(int i=0;i<1200;i++){ocean.World.Step();reference.Step();}
            var snapshot=ocean.Snapshot(out var archived);Assert.AreEqual(snapshot.Alive,archived.Alive);
            Assert.That(snapshot.Stored,Is.EqualTo(VolumeSnapshot.Read(reference).Stored).Within(.00001));
            ocean.Stream(default);int retained=0;
            for(int i=0;i<reference.Count;i++) {
                var expected=reference.Get(i);if(!expected.Alive&&reference.Time-expected.DiedAt>VolumeWorld.DivisionSeconds)continue;
                Assert.AreEqual(expected,ocean.World.Get(ocean.Find(expected.Identity)));retained++;
            }
            Assert.AreEqual(retained,ocean.World.Count);
            Assert.AreEqual(reference.GetFood(0),ocean.World.GetFood(0));
            var before=ocean.Snapshot(out var unused);var external=ocean.World.Diagnostics.ExternalNutrients;
            ocean.Save();ocean.Save();ocean.Stream(new V3(140,0,0));ocean.Stream(default);
            Assert.AreEqual(before,ocean.Snapshot(out unused));Assert.AreEqual(external,ocean.World.Diagnostics.ExternalNutrients);
        }
        [Test] public void SaveSnapshotsCountOutboundObjectsOnceAcrossActiveAndArchive()
        {
            string path=Path.Combine(Path.GetTempPath(),"evolution-outbound-"+Guid.NewGuid());
            try {
                var ocean=new ChunkOcean(7,new BinaryRegionArchive(path));for(int i=0;i<600;i++)ocean.World.Step();
                var before=VolumeSnapshot.Read(ocean.World);ocean.Save();var after=ocean.Snapshot(out var archived);
                Assert.AreEqual(before.Alive,after.Alive);Assert.That(after.Stored,Is.EqualTo(before.Stored).Within(.00001));
                var external=ocean.World.Diagnostics.ExternalNutrients;var resumed=new ChunkOcean(7,new BinaryRegionArchive(path));
                var restored=resumed.Snapshot(out archived);Assert.AreEqual(after.Alive,restored.Alive);
                Assert.That(restored.Stored,Is.EqualTo(after.Stored).Within(.00001));Assert.AreEqual(external,resumed.World.Diagnostics.ExternalNutrients);
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void ArchivedCompactionFreesBirthSlotsWithoutInventingDeaths()
        {
            var a=new VolumeWorld(1).Get(0);a.Biology.Energy=Biology.Capacity(a.Biology.Genes);var agents=new VolumeAgent[VolumeWorld.PopulationLimit+1];agents[0]=a;
            for(int i=1;i<agents.Length;i++){var dead=a;dead.Identity="expired-"+i;dead.Alive=false;dead.DiedAt=-10;agents[i]=dead;}
            var w=new VolumeWorld(agents,Array.Empty<FoodCloud>(),Array.Empty<V3>(),0,default);
            w.Step();w.CompactArchived();Assert.AreEqual(1,w.Count);Assert.AreEqual(0,w.Diagnostics.Deaths);
            w.Step();Assert.AreEqual(2,w.Count);Assert.AreEqual(1,w.Diagnostics.Births);Assert.Greater(w.Diagnostics.Division,0);
        }
        [Test] public void RealV2ArchiveUpgradesWithoutRefillingFood()
        {
            // Written by the unmodified v2 writer at diagnostic commit 0cebd55.
            const string fixture="M09WRQIAAAAqAAAAAAAAAAEAAAANbG9jYWwtYWdlbnQtMABE95/AMzOzv3xT00BE95/AMzOzv3xT00COQRQ/Q6Mkv31CAD+OQRQ/Q6Mkv31CAD8AAAAAAAAAAAAAAAABAAAAAAAA8P8AAAAAAADw/wAAAAAAAPD/AAAAAAAAAAAAAACgmZmpPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAQAAAAAAAAAAAAAAAAAAAN3NiEEAAMBAAACAPwAAgD8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAN9GGj/cT4w/Nj5EP9zs3UGgm9884NucPFDldT59Vms/E3k4P1uuWT82Qpc+KFt2PuKtOz6OXk4/v6lsPwitMz+ar48+IhPgPgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAADGxvY2FsLWZvb2QtMQEAAAARxLzAAAAAwBbtrEDNzAxAAAAMQgAAAAA=";
            string path=Path.Combine(Path.GetTempPath(),"evolution-v2-"+Guid.NewGuid());Directory.CreateDirectory(path);
            try {
                File.WriteAllBytes(Path.Combine(path,"0_0_0.region"),Convert.FromBase64String(fixture));
                var archive=new BinaryRegionArchive(path);Assert.IsTrue(archive.TryLoad(default,out var state));
                Assert.AreEqual(42,state.LastTick);Assert.AreEqual(35,state.Food[0].Substrate);Assert.AreEqual(0,state.Food[0].Organic);
                Assert.AreEqual("local-agent-0",state.Agents[0].Identity);Assert.AreEqual(0,state.Agents[0].Order);
                archive.Save(state);Assert.IsTrue(archive.TryLoad(default,out var upgraded));
                Assert.AreEqual(state.Agents[0],upgraded.Agents[0]);Assert.AreEqual(state.Food[0],upgraded.Food[0]);
            } finally {Directory.Delete(path,true);}
        }
        [Test] public void BinaryArchivePreservesLedgerAndContinuationWithoutNewResourceGrants()
        {
            string path=Path.Combine(Path.GetTempPath(),"evolution-balance-"+Guid.NewGuid());
            try {
                var store=new BinaryRegionArchive(path);var key=new ChunkKey();var r=new RegionState {Key=key};
                var a=new VolumeWorld(1).Get(0);a.Position=new V3(10,10,10);a.Previous=a.Position;r.Agents.Add(a);
                r.Food.Add(new FoodCloud {Identity="save-food",Order=1,Position=a.Position,Radius=2.2f,Substrate=25,Organic=5});
                var empty=EmptyArchive();foreach(var k in empty.Keys){empty.TryLoad(k,out var blank);store.Save(blank);}store.Save(r);
                var ocean=new ChunkOcean(7,store);for(int i=0;i<600;i++)ocean.World.Step();ocean.Save();
                var resumed=new ChunkOcean(7,new BinaryRegionArchive(path));
                Assert.AreEqual(ocean.Snapshot(out var oldArchived),resumed.Snapshot(out var newArchived));
                for(int k=0;k<ocean.World.Count;k++){var living=ocean.World.Get(k);if(living.Alive)Assert.AreEqual(living,resumed.World.Get(resumed.Find(living.Identity)));}
                Assert.AreEqual(ocean.World.Diagnostics.Births,resumed.World.Diagnostics.Births);
                Assert.AreEqual(ocean.World.Diagnostics.ExternalNutrients,resumed.World.Diagnostics.ExternalNutrients);
                Assert.AreEqual(ocean.World.Diagnostics.Dissipated,resumed.World.Diagnostics.Dissipated);
                for(int i=0;i<200;i++){ocean.World.Step();resumed.World.Step();}
                Assert.AreEqual(VolumeSnapshot.Read(ocean.World),VolumeSnapshot.Read(resumed.World));
                for(int i=0;i<ocean.World.FoodCount;i++)Assert.AreEqual(ocean.World.GetFood(i),resumed.World.GetFood(i));
            } finally {if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
