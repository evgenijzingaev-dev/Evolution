using System;
using System.Collections.Generic;
using Evolution.Core;

namespace Evolution.Volume
{
    public struct ChunkKey : IEquatable<ChunkKey>
    {
        public const float Size=20;
        public int X,Y,Z;
        public ChunkKey(int x,int y,int z) {X=x;Y=y;Z=z;}
        public static ChunkKey At(V3 p)=>new ChunkKey((int)Math.Floor(p.X/Size),(int)Math.Floor(p.Y/Size),(int)Math.Floor(p.Z/Size));
        public V3 Origin=>new V3(X*Size,Y*Size,Z*Size);
        public bool Equals(ChunkKey k)=>X==k.X&&Y==k.Y&&Z==k.Z;
        public override bool Equals(object o)=>o is ChunkKey k&&Equals(k);
        public override int GetHashCode()=>unchecked(X*73856093^Y*19349663^Z*83492791);
        public override string ToString()=>X+"_"+Y+"_"+Z;
        public uint Seed(uint world)=>unchecked((uint)GetHashCode()^world*2654435761u^0x91e10da5u);
    }
    public sealed class RegionState
    {
        public ChunkKey Key;public long LastTick;
        public readonly List<VolumeAgent> Agents=new List<VolumeAgent>();
        public readonly List<FoodCloud> Food=new List<FoodCloud>();
        public RegionState Clone() {var r=new RegionState {Key=Key,LastTick=LastTick};r.Agents.AddRange(Agents);r.Food.AddRange(Food);return r;}
    }
    public interface IRegionArchive
    {
        long Clock {get;set;}
        VolumeDiagnostics Diagnostics {get;set;}
        IEnumerable<ChunkKey> Keys {get;}
        bool TryLoad(ChunkKey key,out RegionState state);
        void Save(RegionState state);
    }
    public sealed class MemoryRegionArchive : IRegionArchive,ISurveyArchive,INicheArchive,INicheHistoryArchive
    {
        readonly Dictionary<ChunkKey,RegionState> saved=new Dictionary<ChunkKey,RegionState>();
        public EcologicalScenario Scenario {get;set;}
        public long Clock {get;set;}
        public VolumeDiagnostics Diagnostics {get;set;}=new VolumeDiagnostics();
        public IEnumerable<ChunkKey> Keys=>new List<ChunkKey>(saved.Keys);
        public bool TryLoad(ChunkKey key,out RegionState state) {if(saved.TryGetValue(key,out var s)){state=s.Clone();return true;}state=null;return false;}
        public void Save(RegionState state)=>saved[state.Key]=state.Clone();
        readonly List<NicheHistoryPoint> nicheHistory=new List<NicheHistoryPoint>();
        public void ReadNicheHistory(List<NicheHistoryPoint> destination)=>destination.AddRange(nicheHistory);
        public void WriteNicheHistory(IReadOnlyList<NicheHistoryPoint> source){nicheHistory.Clear();for(int i=0;i<source.Count;i++)nicheHistory.Add(source[i]);}
        readonly List<SurveyRegion> survey=new List<SurveyRegion>();
        public void ReadSurvey(List<SurveyRegion> destination){foreach(var r in survey)destination.Add(r.Clone());}
        public void WriteSurvey(IReadOnlyList<SurveyRegion> regions){survey.Clear();foreach(var r in regions)survey.Add(r.Clone());}
    }
    // Two organisms/clouds per chunk, 5^3 visibility guard, near 20 Hz / far 1 Hz.
    // Archived regions replay the same far-LOD biology and bounded resource flux.
    public sealed class ChunkOcean
    {
        readonly uint seed;readonly IRegionArchive archive;readonly EcologicalScenario scenario;
        readonly Dictionary<ChunkKey,RegionState> loaded=new Dictionary<ChunkKey,RegionState>();
        readonly Dictionary<string,ChunkKey> agentOwners=new Dictionary<string,ChunkKey>(),foodOwners=new Dictionary<string,ChunkKey>();
        readonly VolumeDiagnostics diagnostics;
        readonly V3 offset;ChunkKey center;bool initialized;
        public VolumeWorld World {get;private set;}
        public OceanSurvey Survey {get;}=new OceanSurvey();
        public IEnumerable<ChunkKey> LoadedKeys=>loaded.Keys;
        public int LoadedCount=>loaded.Count;
        public ChunkOcean(uint seed,IRegionArchive archive,EcologicalScenario scenario=EcologicalScenario.Legacy)
        {
            this.scenario=scenario==EcologicalScenario.Legacy&&archive is INicheArchive niche?niche.Scenario:scenario;
            if(archive is INicheArchive profile)profile.Scenario=this.scenario;
            this.seed=seed;this.archive=archive;diagnostics=archive.Diagnostics.Clone();
            var rng=new RandomSource(seed^0x718197u);offset=new V3(rng.Range(-100,100),rng.Range(-100,100),rng.Range(-100,100));
            if(archive is ISurveyArchive store){var records=new List<SurveyRegion>();store.ReadSurvey(records);Survey.Restore(records);}
            if(archive is INicheHistoryArchive ecological)ecological.ReadNicheHistory(Survey.NicheHistory);
            Stream(default);
        }
        public bool Stream(V3 camera)
        {
            var next=ChunkKey.At(camera);
            if(initialized&&next.Equals(center)&&!World.NeedsCompaction) {World.ActiveCenter=camera;return false;}
            string controlled=World!=null&&World.Controlled>=0?World.Get(World.Controlled).Identity:null;
            string divisionRequest=World?.DivisionRequest,offspring=World?.LastOffspringIdentity;
            V3 input=World?.Input??default;bool boost=World?.Boost??false,attack=World?.Attack??false;
            bool controlColony=World?.ControlColony??false;
            var life=World?.Life??new LifeJournal();
            long tick=World?.Tick??archive.Clock;
            bool supply=World?.NutrientSupply??true;
            if(initialized){Survey.Refresh(World,loaded.Keys);Synchronize();}
            var keep=new HashSet<ChunkKey>();
            for(int z=-2;z<=2;z++)for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)keep.Add(new ChunkKey(next.X+x,next.Y+y,next.Z+z));
            var expired=new List<ChunkKey>();foreach(var pair in loaded)if(!keep.Contains(pair.Key))expired.Add(pair.Key);
            foreach(var key in expired) {archive.Save(loaded[key]);loaded.Remove(key);}
            foreach(var key in keep)if(!loaded.ContainsKey(key)) {
                var region=archive.TryLoad(key,out var saved)?saved:Generate(key,tick);
                CatchUp(region,tick);loaded.Add(key,region);
            }
            // Stable ordering prevents dictionary implementation/order from altering biological competition.
            var keys=new List<ChunkKey>(loaded.Keys);keys.Sort((a,b)=>a.X!=b.X?a.X.CompareTo(b.X):a.Y!=b.Y?a.Y.CompareTo(b.Y):a.Z.CompareTo(b.Z));
            var agents=new List<VolumeAgent>();var food=new List<FoodCloud>();var obstacles=new List<V3>();
            foreach(var key in keys) {agents.AddRange(loaded[key].Agents);food.AddRange(loaded[key].Food);var rng=new RandomSource(key.Seed(seed)^0x3817u);
                obstacles.Add(key.Origin+new V3(rng.Range(3,17),rng.Range(3,17),rng.Range(3,17)));if(scenario!=EcologicalScenario.Legacy&&NicheEnvironment.TryVent(key,offset,out var vent))obstacles.Add(vent);}

            World=new VolumeWorld(agents.ToArray(),food.ToArray(),obstacles.ToArray(),tick,offset,diagnostics,scenario) {Life=life,NutrientSupply=supply,ActiveCenter=camera,Input=input,Boost=boost,Attack=attack,ControlColony=controlColony,DivisionRequest=divisionRequest,LastOffspringIdentity=offspring};
            agentOwners.Clear();foodOwners.Clear();for(int i=0;i<World.Count;i++)agentOwners[World.Get(i).Identity]=World.OwnerChunk(i);foreach(var f in food)foodOwners[f.Identity]=ChunkKey.At(f.Position);
            if(controlled!=null)for(int i=0;i<World.Count;i++)if(World.Get(i).Identity==controlled){World.Controlled=i;break;}
            archive.Diagnostics=diagnostics.Clone();archive.Clock=tick;center=next;initialized=true;Survey.Refresh(World,loaded.Keys);
            if(archive is ISurveyArchive store)store.WriteSurvey(Survey.Regions);if(archive is INicheHistoryArchive ecological)ecological.WriteNicheHistory(Survey.NicheHistory);return true;
        }
        public int Find(string identity)
        {for(int i=0;i<World.Count;i++)if(World.Get(i).Identity==identity)return i;return -1;}
        public int Nearest(V3 point)
        {int index=-1;float distance=float.MaxValue;for(int i=0;i<World.Count;i++){var a=World.Get(i);if(!a.Alive)continue;float d=(a.Position-point).Sqr;if(d<distance){distance=d;index=i;}}return index;}
        public void Save() {if(archive is INicheArchive niche)niche.Scenario=scenario;Survey.Refresh(World,loaded.Keys);Synchronize();foreach(var r in loaded.Values)archive.Save(r);archive.Diagnostics=diagnostics.Clone();archive.Clock=World.Tick;
            if(archive is ISurveyArchive store)store.WriteSurvey(Survey.Regions);if(archive is INicheHistoryArchive ecological)ecological.WriteNicheHistory(Survey.NicheHistory);}
        // Advances known unloaded regions lazily before a global diagnostic snapshot.
        // Unvisited regions do not exist in this accounting. Loading founders is never a birth.
        public VolumeSnapshot Snapshot(out VolumeSnapshot archived)
        {
            archived=default;var keys=new List<ChunkKey>(archive.Keys);
            var activeAgents=new HashSet<string>();var activeFood=new HashSet<string>();
            for(int i=0;i<World.Count;i++)activeAgents.Add(World.Get(i).Identity);
            for(int k=0;k<World.FoodCount;k++)activeFood.Add(World.GetFood(k).Identity);
            keys.Sort((a,b)=>a.X!=b.X?a.X.CompareTo(b.X):a.Y!=b.Y?a.Y.CompareTo(b.Y):a.Z.CompareTo(b.Z));
            foreach(var key in keys)if(!loaded.ContainsKey(key)&&archive.TryLoad(key,out var r)) {
                // Save may already have archived outbound objects still resident in World.
                // Their authoritative simulation remains active until the next streaming rebuild.
                var residentAgents=r.Agents.FindAll(a=>activeAgents.Contains(a.Identity));
                var residentFood=r.Food.FindAll(f=>activeFood.Contains(f.Identity));
                r.Agents.RemoveAll(a=>activeAgents.Contains(a.Identity));r.Food.RemoveAll(f=>activeFood.Contains(f.Identity));
                CatchUp(r,World.Tick);
                var w=new VolumeWorld(r.Agents.ToArray(),r.Food.ToArray(),Array.Empty<V3>(),r.LastTick,offset,scenario:scenario);
                archived.Add(VolumeSnapshot.Read(w));
                r.Agents.AddRange(residentAgents);r.Food.AddRange(residentFood);archive.Save(r);
            }
            archive.Diagnostics=diagnostics.Clone();var total=VolumeSnapshot.Read(World);total.Add(archived);return total;
        }
        void Synchronize()
        {
            World.RefreshColonies();
            foreach(var r in loaded.Values) {r.Agents.Clear();r.Food.Clear();r.LastTick=World.Tick;}
            var outbound=new Dictionary<ChunkKey,RegionState>();
            RegionState DestinationKey(ChunkKey key)
            {
                if(loaded.TryGetValue(key,out var r))return r;
                if(outbound.TryGetValue(key,out r))return r;
                r=archive.TryLoad(key,out var saved)?saved:new RegionState {Key=key,LastTick=World.Tick};CatchUp(r,World.Tick);outbound.Add(key,r);return r;
            }
            for(int i=0;i<World.Count;i++) {
                var a=World.Get(i);if(!a.Alive&&World.Time-a.DiedAt>VolumeWorld.DivisionSeconds)continue;var key=World.OwnerChunk(i);
                if(agentOwners.TryGetValue(a.Identity,out var old)&&!old.Equals(key)&&!loaded.ContainsKey(old))DestinationKey(old).Agents.RemoveAll(v=>v.Identity==a.Identity);
                var region=DestinationKey(key);int index=region.Agents.FindIndex(v=>v.Identity==a.Identity);
                if(index<0)region.Agents.Add(a);else region.Agents[index]=a;agentOwners[a.Identity]=key;
            }
            for(int i=0;i<World.FoodCount;i++) {
                var f=World.GetFood(i);var key=ChunkKey.At(f.Position);
                if(foodOwners.TryGetValue(f.Identity,out var old)&&!old.Equals(key)&&!loaded.ContainsKey(old))DestinationKey(old).Food.RemoveAll(v=>v.Identity==f.Identity);
                var region=DestinationKey(key);int index=region.Food.FindIndex(v=>v.Identity==f.Identity);
                if(index<0)region.Food.Add(f);else region.Food[index]=f;foodOwners[f.Identity]=key;
            }
            foreach(var r in outbound.Values)archive.Save(r);
        }
        RegionState Generate(ChunkKey key,long tick)
        {
            var rng=new RandomSource(key.Seed(seed));var r=new RegionState {Key=key,LastTick=tick};
            for(int i=0;i<2;i++) {
                V3 p=key.Origin+new V3(rng.Range(1,19),rng.Range(1,19),rng.Range(1,19));
                r.Food.Add(new FoodCloud {Identity=key+"/food/"+i,Pattern=(int)(key.Seed(seed)&0x7fffffffu)+i,
                    Position=p,Radius=2.2f,Substrate=i==0?35:0,Organic=i==1?55:0});
                var g=Genome.EcologicalFounder(Genome.Founder(ref rng),rng.State);
                int id=(int)((key.Seed(seed)^(uint)(i*7919))&0x7fffffff);if(id==0)id=1;
                r.Agents.Add(new VolumeAgent {Identity=key+"/agent/"+i,Position=p+new V3(.8f,.5f,.6f),Previous=p+new V3(.8f,.5f,.6f),Direction=new V3(1,.1f,0).Unit,
                    PreviousDirection=new V3(1,.1f,0).Unit,Alive=true,FedAt=double.NegativeInfinity,BitAt=double.NegativeInfinity,DiedAt=double.NegativeInfinity,
                    RenderDuration=VolumeWorld.StepSeconds,RenderTime=tick*VolumeWorld.StepSeconds,
                    Biology=new Cell {Id=id,LineageId=id,Genes=g,Energy=Biology.Capacity(g)*.65f,Age=6,Growth=1,Integrity=1}});
            }
            if(scenario!=EcologicalScenario.Legacy&&NicheEnvironment.TryVent(key,offset,out var vent))r.Food.Add(new FoodCloud {Identity=key+"/vent-food",Pattern=key.GetHashCode(),Position=vent+new V3(0,1.8f,0),Radius=3.5f,Substrate=25});
            return r;
        }
        void CatchUp(RegionState region,long tick)
        {
            if(tick<=region.LastTick)return;
            // Replay the same far-LOD model, including transport, paid growth/births and death.
            // Archived regions are isolated until loaded; no neighbours or new regions are generated.
            var rng=new RandomSource(region.Key.Seed(seed)^0x3817u);
            var obstacle=region.Key.Origin+new V3(rng.Range(3,17),rng.Range(3,17),rng.Range(3,17));
            var collisions=scenario!=EcologicalScenario.Legacy&&NicheEnvironment.TryVent(region.Key,offset,out var vent)?new[]{obstacle,vent}:new[]{obstacle};
            var replay=new VolumeWorld(region.Agents.ToArray(),region.Food.ToArray(),collisions,region.LastTick,offset,diagnostics,scenario) {
                NutrientSupply=World?.NutrientSupply??true,ActiveCenter=new V3(1e10f,1e10f,1e10f)};
            while(replay.Tick<tick){replay.Step();replay.CompactArchived();}
            region.Agents.Clear();region.Food.Clear();
            for(int i=0;i<replay.Count;i++){var a=replay.Get(i);if(a.Alive||replay.Time-a.DiedAt<=VolumeWorld.DivisionSeconds)region.Agents.Add(a);}
            for(int k=0;k<replay.FoodCount;k++)region.Food.Add(replay.GetFood(k));
            region.LastTick=tick;
        }
    }
}

