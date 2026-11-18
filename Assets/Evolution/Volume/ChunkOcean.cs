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
        bool TryLoad(ChunkKey key,out RegionState state);
        void Save(RegionState state);
    }
    public sealed class MemoryRegionArchive : IRegionArchive,ISurveyArchive
    {
        readonly Dictionary<ChunkKey,RegionState> saved=new Dictionary<ChunkKey,RegionState>();
        public long Clock {get;set;}
        public bool TryLoad(ChunkKey key,out RegionState state) {if(saved.TryGetValue(key,out var s)){state=s.Clone();return true;}state=null;return false;}
        public void Save(RegionState state)=>saved[state.Key]=state.Clone();
        readonly List<SurveyRegion> survey=new List<SurveyRegion>();
        public void ReadSurvey(List<SurveyRegion> destination){foreach(var r in survey)destination.Add(r.Clone());}
        public void WriteSurvey(IReadOnlyList<SurveyRegion> regions){survey.Clear();foreach(var r in regions)survey.Add(r.Clone());}
    }
    // Two organisms/clouds per chunk, 5^3 visibility guard, near 20 Hz / far 1 Hz.
    // Archived regions preserve pose/genome and receive bounded analytical metabolic catch-up.
    public sealed class ChunkOcean
    {
        readonly uint seed;readonly IRegionArchive archive;
        readonly Dictionary<ChunkKey,RegionState> loaded=new Dictionary<ChunkKey,RegionState>();
        readonly Dictionary<string,ChunkKey> agentOwners=new Dictionary<string,ChunkKey>(),foodOwners=new Dictionary<string,ChunkKey>();
        readonly V3 offset;ChunkKey center;bool initialized;
        public VolumeWorld World {get;private set;}
        public OceanSurvey Survey {get;}=new OceanSurvey();
        public IEnumerable<ChunkKey> LoadedKeys=>loaded.Keys;
        public int LoadedCount=>loaded.Count;
        public ChunkOcean(uint seed,IRegionArchive archive)
        {
            this.seed=seed;this.archive=archive;
            var rng=new RandomSource(seed^0x718197u);offset=new V3(rng.Range(-100,100),rng.Range(-100,100),rng.Range(-100,100));
            if(archive is ISurveyArchive store){var records=new List<SurveyRegion>();store.ReadSurvey(records);Survey.Restore(records);}
            Stream(default);
        }
        public bool Stream(V3 camera)
        {
            var next=ChunkKey.At(camera);
            if(initialized&&next.Equals(center)) {World.ActiveCenter=camera;return false;}
            string controlled=World!=null&&World.Controlled>=0?World.Get(World.Controlled).Identity:null;
            V3 input=World?.Input??default;bool boost=World?.Boost??false,attack=World?.Attack??false;
            long tick=World?.Tick??archive.Clock;
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
                obstacles.Add(key.Origin+new V3(rng.Range(3,17),rng.Range(3,17),rng.Range(3,17)));}
            for(int i=0;i<agents.Count;i++){var a=agents[i];a.FoodCloud=0;if(a.FoodIdentity!=null)for(int k=0;k<food.Count;k++)if(food[k].Identity==a.FoodIdentity){a.FoodCloud=k;break;}agents[i]=a;}
            World=new VolumeWorld(agents.ToArray(),food.ToArray(),obstacles.ToArray(),tick,offset) {ActiveCenter=camera,Input=input,Boost=boost,Attack=attack};
            agentOwners.Clear();foodOwners.Clear();foreach(var a in agents)agentOwners[a.Identity]=ChunkKey.At(a.Position);foreach(var f in food)foodOwners[f.Identity]=ChunkKey.At(f.Position);
            if(controlled!=null)for(int i=0;i<World.Count;i++)if(World.Get(i).Identity==controlled){World.Controlled=i;break;}
            archive.Clock=tick;center=next;initialized=true;Survey.Refresh(World,loaded.Keys);
            if(archive is ISurveyArchive store)store.WriteSurvey(Survey.Regions);return true;
        }
        public int Find(string identity)
        {for(int i=0;i<World.Count;i++)if(World.Get(i).Identity==identity)return i;return -1;}
        public int Nearest(V3 point)
        {int index=-1;float distance=float.MaxValue;for(int i=0;i<World.Count;i++){var a=World.Get(i);if(!a.Alive)continue;float d=(a.Position-point).Sqr;if(d<distance){distance=d;index=i;}}return index;}
        public void Save() {Survey.Refresh(World,loaded.Keys);Synchronize();foreach(var r in loaded.Values)archive.Save(r);archive.Clock=World.Tick;
            if(archive is ISurveyArchive store)store.WriteSurvey(Survey.Regions);}
        void Synchronize()
        {
            foreach(var r in loaded.Values) {r.Agents.Clear();r.Food.Clear();r.LastTick=World.Tick;}
            var outbound=new Dictionary<ChunkKey,RegionState>();
            RegionState DestinationKey(ChunkKey key)
            {
                if(loaded.TryGetValue(key,out var r))return r;
                if(outbound.TryGetValue(key,out r))return r;
                r=archive.TryLoad(key,out var saved)?saved:Generate(key,World.Tick);CatchUp(r,World.Tick);outbound.Add(key,r);return r;
            }
            for(int i=0;i<World.Count;i++) {
                var a=World.Get(i);var key=ChunkKey.At(a.Position);
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
            return r;
        }
        void CatchUp(RegionState region,long tick)
        {
            double elapsed=(tick-region.LastTick)*(double)VolumeWorld.StepSeconds;
            if(elapsed<=0)return;
            int slices=Math.Min(16,Math.Max(1,(int)Math.Ceiling(elapsed)));float dt=(float)(elapsed/slices);
            for(int s=0;s<slices;s++)for(int i=0;i<region.Agents.Count;i++) {
                var a=region.Agents[i];if(!a.Alive)continue;var c=a.Biology;
                var sample=new OceanSample {Temperature=WaterField.Temperature(a.Position+offset,(region.LastTick*VolumeWorld.StepSeconds)+(s+.5)*dt),Light=WaterField.Light(a.Position)};
                c.Budget=new EnergyBudget {Start=c.Energy};
                for(int k=0;k<region.Food.Count;k++) {var f=region.Food[k];if((f.Position-a.Position).Sqr>=f.Radius*f.Radius)continue;
                    c.Budget.Light+=Ecology.Photosynthesize(ref c,ref f.Substrate,sample,dt);Biology.Feed(ref c,ref f.Substrate,ref f.Organic,sample.Temperature,dt);region.Food[k]=f;}
                c.FoodLastStep=c.Budget.Intake;c.AssimilatedEnergy+=c.Budget.Intake;
                c.Budget.Upkeep=Biology.Cost(c.Genes,sample.Temperature)*dt;c.Energy-=c.Budget.Upkeep;c.Age+=dt;
                if(c.Energy<=0) {a.Alive=false;a.DiedAt=tick*VolumeWorld.StepSeconds;if(region.Food.Count>0){var f=region.Food[0];f.Organic+=Biology.CarcassEnergy(c);region.Food[0]=f;}}
                a.Biology=c;a.Previous=a.Position;a.RenderTime=tick*VolumeWorld.StepSeconds;a.RenderDuration=dt;region.Agents[i]=a;
            }
            region.LastTick=tick;
        }
    }
}
