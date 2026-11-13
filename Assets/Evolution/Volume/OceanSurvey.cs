using System;
using System.Collections.Generic;
using Evolution.Core;

namespace Evolution.Volume
{
    public enum SurveyEventKind { LineageBorn, KnownLineageLost, Division, Death, Migration, PopulationChange, Predation }
    public enum SurveyLayer { Lineage, Form, Diet, Threat, Density, Food, Temperature, Current, Light }
    public sealed class SurveyRegion
    {
        public ChunkKey Key; public long Tick; public bool Loaded;
        public int Living, Dead, Attacks; public float Substrate, Organic, Temperature, Light;
        public V3 Center, Current;public readonly int[] Forms=new int[6],Diets=new int[4]; public readonly Dictionary<int,int> Lineages=new Dictionary<int,int>();
        public SurveyRegion Clone() {return CopyTo(new SurveyRegion());}
        SurveyRegion CopyTo(SurveyRegion r) {r.Key=Key;r.Tick=Tick;r.Loaded=Loaded;r.Living=Living;r.Dead=Dead;r.Attacks=Attacks;
            r.Substrate=Substrate;r.Organic=Organic;r.Temperature=Temperature;r.Light=Light;r.Center=Center;r.Current=Current;
            Array.Copy(Forms,r.Forms,6);Array.Copy(Diets,r.Diets,4);foreach(var p in Lineages)r.Lineages[p.Key]=p.Value;return r;}
        public void Reset(long tick,V3 offset) {Tick=tick;Loaded=true;Living=Dead=Attacks=0;Substrate=Organic=0;Lineages.Clear();Array.Clear(Forms,0,6);Array.Clear(Diets,0,4);Center=Key.Origin+new V3(10,10,10);
            Temperature=WaterField.Temperature(Center+offset,tick*VolumeWorld.StepSeconds);Light=WaterField.Light(Center);Current=WaterField.Current(Center+offset,tick*VolumeWorld.StepSeconds);}
    }
    public struct SurveyAgent
    {
        public string Identity; public int Id,Lineage,Form,Diet;public V3 Position,Previous;public float Size;public bool Threat;
    }
    public struct SurveyEvent
    {
        public SurveyEventKind Kind; public long Tick;public V3 Position;public int Lineage,Count;public string Identity;
    }
    public interface ISurveyArchive
    {
        void ReadSurvey(List<SurveyRegion> destination);
        void WriteSurvey(IReadOnlyList<SurveyRegion> regions);
    }
    // Read-only observations. Does not step, generate, mutate or load the biological model.
    public sealed class OceanSurvey
    {
        public const int RegionLimit=4096, EventLimit=128, AgentHistoryLimit=16384;
        readonly Dictionary<ChunkKey,SurveyRegion> byRegion=new Dictionary<ChunkKey,SurveyRegion>();
        readonly Dictionary<string,Seen> history=new Dictionary<string,Seen>();
        readonly HashSet<int> knownLines=new HashSet<int>();
        readonly Dictionary<int,int> counts=new Dictionary<int,int>(),previousCounts=new Dictionary<int,int>();
        readonly Dictionary<ChunkKey,int> oldPopulation=new Dictionary<ChunkKey,int>();
        readonly HashSet<ChunkKey> touched=new HashSet<ChunkKey>();
        readonly List<VolumeAgent> source=new List<VolumeAgent>(512);readonly List<FoodCloud> food=new List<FoodCloud>(512);
        readonly List<string> expired=new List<string>();readonly Dictionary<ChunkKey,Movement> migrations=new Dictionary<ChunkKey,Movement>();
        public readonly List<SurveyRegion> Regions=new List<SurveyRegion>(256);
        public readonly List<SurveyAgent> Agents=new List<SurveyAgent>(512);
        public readonly List<SurveyEvent> Events=new List<SurveyEvent>(EventLimit);
        public int ActiveLiving {get;private set;} public int KnownLiving {get;private set;} public int KnownLineages=>knownLines.Count;
        public long Tick {get;private set;} public int Revision {get;private set;}
        public bool CatalogueTruncated {get;private set;}
        struct Seen {public V3 Position;public long Tick;public bool Alive;public double Bite;public int Lineage;}
        sealed class Movement {public readonly HashSet<string> Ids=new HashSet<string>();public long Since;}
        public void Restore(IEnumerable<SurveyRegion> saved) {foreach(var r in saved){if(Regions.Count>=RegionLimit){CatalogueTruncated=true;break;}
            var copy=r.Clone();copy.Loaded=false;byRegion[copy.Key]=copy;Regions.Add(copy);foreach(var pair in copy.Lineages)knownLines.Add(pair.Key);}Recount(false);}
        public void Refresh(VolumeWorld world,IEnumerable<ChunkKey> loaded)
        {
            source.Clear();food.Clear();for(int i=0;i<world.Count;i++)source.Add(world.Get(i));for(int i=0;i<world.FoodCount;i++)food.Add(world.GetFood(i));
            Observe(source,food,loaded,world.Tick,world.FlowOffset);
        }
        public void Observe(IReadOnlyList<VolumeAgent> cells,IReadOnlyList<FoodCloud> resources,IEnumerable<ChunkKey> loaded,long tick,V3 offset)
        {
            bool advanced=Revision>0&&tick>Tick;Tick=tick;touched.Clear();Agents.Clear();ActiveLiving=0;
            oldPopulation.Clear();foreach(var r in Regions){if(advanced&&r.Loaded)oldPopulation[r.Key]=r.Living;r.Loaded=false;}
            foreach(var key in loaded)Touch(key,offset);
            for(int i=0;i<cells.Count;i++) {
                var a=cells[i];var key=ChunkKey.At(a.Position);var r=Touch(key,offset);var c=a.Biology;
                if(!r.Lineages.ContainsKey(c.LineageId))r.Lineages[c.LineageId]=0;
                if(a.Alive){r.Living++;ActiveLiving++;r.Forms[Form(c.Genes)]++;r.Diets[Diet(c.Genes)]++;r.Lineages.TryGetValue(c.LineageId,out int n);r.Lineages[c.LineageId]=n+1;}
                else r.Dead++;
                bool seen=history.TryGetValue(a.Identity??"",out var before);
                if(a.Alive)Agents.Add(new SurveyAgent {Identity=a.Identity,Id=c.Id,Lineage=c.LineageId,Position=a.Position,Previous=seen?before.Position:a.Position,
                    Size=c.Genes.Size,Form=Form(c.Genes),Diet=Diet(c.Genes),Threat=IsRecent(a.BitAt,tick,8)});
                if(IsRecent(a.BitAt,tick,8))r.Attacks++;
                if(advanced&&seen) {
                    if(before.Alive&&!a.Alive)Add(SurveyEventKind.Death,a.Position,c.LineageId,1,a.Identity);
                    if(a.BitAt>before.Bite&&IsRecent(a.BitAt,tick,2))Add(SurveyEventKind.Predation,a.Position,c.LineageId,1,a.Identity);
                    if(a.Alive&&before.Alive&&tick-before.Tick<=40&&!ChunkKey.At(before.Position).Equals(key)) {
                        if(!migrations.TryGetValue(key,out var move)){move=new Movement {Since=tick};migrations[key]=move;}
                        if(tick-move.Since>200){move.Ids.Clear();move.Since=tick;}
                        move.Ids.Add(a.Identity??"");if(move.Ids.Count>=3){Add(SurveyEventKind.Migration,a.Position,c.LineageId,move.Ids.Count,null);move.Ids.Clear();move.Since=tick;}
                    }
                }
                // The current VolumeWorld has no birth/colony transitions. Do not infer them from streamed records.
                if(seen||history.Count<AgentHistoryLimit)history[a.Identity??""]=new Seen {Position=a.Position,Tick=tick,Alive=a.Alive,Bite=a.BitAt,Lineage=c.LineageId};
            }
            for(int i=0;i<resources.Count;i++){var f=resources[i];var r=Touch(ChunkKey.At(f.Position),offset);r.Substrate+=f.Substrate;r.Organic+=f.Organic;}
            foreach(var key in touched) {var r=byRegion[key];if(r.Living==0)continue;
                // Centroids are accumulated below in one linear pass, not a cells-by-regions search.
                r.Center=default;}
            for(int i=0;i<Agents.Count;i++){var a=Agents[i];byRegion[ChunkKey.At(a.Position)].Center+=a.Position;}
            foreach(var key in touched){var r=byRegion[key];if(r.Living>0)r.Center/=r.Living;}
            foreach(var key in touched)if(oldPopulation.TryGetValue(key,out int old)&&Math.Abs(byRegion[key].Living-old)>=Math.Max(4,old*.25f))
                Add(SurveyEventKind.PopulationChange,byRegion[key].Center,0,byRegion[key].Living-old,null);
            Trim();Recount(advanced);Revision++;
        }
        static bool IsRecent(double time,long tick,float seconds)=>!double.IsNaN(time)&&!double.IsInfinity(time)&&tick*VolumeWorld.StepSeconds-time>=0&&tick*VolumeWorld.StepSeconds-time<seconds;
        SurveyRegion Touch(ChunkKey key,V3 offset)
        {
            if(!byRegion.TryGetValue(key,out var r)){r=new SurveyRegion {Key=key};byRegion.Add(key,r);Regions.Add(r);}
            if(touched.Add(key))r.Reset(Tick,offset);return r;
        }
        void Recount(bool events)
        {
            previousCounts.Clear();foreach(var p in counts)previousCounts[p.Key]=p.Value;counts.Clear();knownLines.Clear();KnownLiving=0;
            foreach(var r in Regions){KnownLiving+=r.Living;foreach(var p in r.Lineages){knownLines.Add(p.Key);counts.TryGetValue(p.Key,out int n);counts[p.Key]=n+p.Value;}}
            if(events&&!CatalogueTruncated)foreach(var old in previousCounts)if(old.Value>0&&(!counts.TryGetValue(old.Key,out int n)||n==0)) {
                // Only declare loss when an observed death exists; an unloaded region is not extinction.
                for(int i=Events.Count-1;i>=0;i--)if(Events[i].Kind==SurveyEventKind.Death&&Events[i].Lineage==old.Key&&Events[i].Tick==Tick){Add(SurveyEventKind.KnownLineageLost,Events[i].Position,old.Key,old.Value,null);break;}
            }
        }
        void Trim()
        {
            while(Regions.Count>RegionLimit){int oldest=-1;long time=long.MaxValue;for(int i=0;i<Regions.Count;i++)if(!Regions[i].Loaded&&Regions[i].Tick<time){oldest=i;time=Regions[i].Tick;}
                if(oldest<0)break;byRegion.Remove(Regions[oldest].Key);Regions.RemoveAt(oldest);CatalogueTruncated=true;}
            if(history.Count>AgentHistoryLimit){expired.Clear();foreach(var p in history)if(Tick-p.Value.Tick>200)expired.Add(p.Key);foreach(var id in expired)history.Remove(id);
                if(history.Count>AgentHistoryLimit){expired.Clear();foreach(var p in history){if(p.Value.Tick!=Tick)expired.Add(p.Key);if(history.Count-expired.Count<=AgentHistoryLimit)break;}foreach(var id in expired)history.Remove(id);}}
            if(migrations.Count>RegionLimit)migrations.Clear();
        }
        void Add(SurveyEventKind kind,V3 p,int lineage,int count,string identity)
        {
            if(Events.Count>0){var last=Events[Events.Count-1];if(last.Kind==kind&&(last.Lineage==lineage||kind==SurveyEventKind.Predation)&&Tick-last.Tick<40&&ChunkKey.At(last.Position).Equals(ChunkKey.At(p))){last.Count+=count;last.Tick=Tick;Events[Events.Count-1]=last;return;}}
            if(Events.Count==EventLimit)Events.RemoveAt(0);Events.Add(new SurveyEvent {Kind=kind,Tick=Tick,Position=p,Lineage=lineage,Count=count,Identity=identity});
        }
        public bool TryRegion(ChunkKey key,out SurveyRegion region)=>byRegion.TryGetValue(key,out region);
        public bool Frame(V3 near,float radius,int lineage,out V3 center,out float extent,out int count)
        {
            center=default;extent=2;count=0;float rr=radius*radius;
            foreach(var a in Agents)if((a.Position-near).Sqr<=rr&&(lineage==0||a.Lineage==lineage)){center+=a.Position;count++;}
            if(count==0){center=near;return false;}center/=count;
            foreach(var a in Agents)if((a.Position-near).Sqr<=rr&&(lineage==0||a.Lineage==lineage))extent=Math.Max(extent,(a.Position-center).Length+a.Size);
            return true;
        }
        public static bool InSlice(float y,bool full,float height,float halfWidth)=>full||Math.Abs(y-height)<=halfWidth;
        // Same phenotype thresholds as CrystalForms; no new species or biological abilities.
        public static int Form(Genome g)=>g.Membrane>.72f?3:g.Predation>.62f?4:g.Photosynthesis>.62f?5:g.Speed>1.18f?1:g.Uptake>1.06f?0:2;
        public static int Diet(Genome g)=>g.Photosynthesis>.5f?0:g.Predation>.5f?1:g.OrganicSpecialization>.5f?2:3;
    }
}
