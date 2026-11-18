using System;
using System.IO;
using Evolution.Core;

namespace Evolution.Volume
{
    // Explicit versioned records; no BinaryFormatter, reflection or executable object deserialization.
    public sealed class BinaryRegionArchive : IRegionArchive,ISurveyArchive
    {
        const int Magic=0x45564f33,Version=3;
        readonly string directory;long clock;VolumeDiagnostics diagnostics=new VolumeDiagnostics();
        public BinaryRegionArchive(string directory)
        {
            this.directory=directory;Directory.CreateDirectory(directory);string path=Path.Combine(directory,"clock.bin");
            if(File.Exists(path))using(var r=new BinaryReader(File.OpenRead(path)))clock=r.ReadInt64();
            path=Path.Combine(directory,"ecology.bin");if(File.Exists(path))using(var r=new BinaryReader(File.OpenRead(path)))diagnostics=VolumeDiagnostics.Read(r);
        }
        public long Clock {get=>clock;set {clock=value;Atomic(Path.Combine(directory,"clock.bin"),w=>w.Write(value));}}
        public VolumeDiagnostics Diagnostics {get=>diagnostics.Clone();set {diagnostics=value.Clone();Atomic(Path.Combine(directory,"ecology.bin"),w=>diagnostics.Write(w));}}
        public System.Collections.Generic.IEnumerable<ChunkKey> Keys {
            get {foreach(string path in Directory.GetFiles(directory,"*.region")) {
                var parts=Path.GetFileNameWithoutExtension(path).Split('_');
                if(parts.Length==3&&int.TryParse(parts[0],out int x)&&int.TryParse(parts[1],out int y)&&int.TryParse(parts[2],out int z))yield return new ChunkKey(x,y,z);
            }}
        }
        public bool TryLoad(ChunkKey key,out RegionState state)
        {
            string path=Path.Combine(directory,key+".region");if(!File.Exists(path)){state=null;return false;}
            using(var r=new BinaryReader(File.OpenRead(path))) {
                int magic=r.ReadInt32(),version=r.ReadInt32();
                if(magic!=Magic||version<1||version>Version)throw new InvalidDataException("Unsupported EVOLUTION water region; original file retained.");
                state=new RegionState {Key=key,LastTick=r.ReadInt64()};int count=Count(r);
                for(int i=0;i<count;i++)state.Agents.Add(ReadAgent(r,version));count=Count(r);
                for(int i=0;i<count;i++){var f=new FoodCloud {Identity=r.ReadString(),Pattern=r.ReadInt32(),Position=ReadV(r),Radius=r.ReadSingle(),Substrate=r.ReadSingle(),Organic=r.ReadSingle()};if(version>=3)f.Order=r.ReadInt64();state.Food.Add(f);}
            }
            return true;
        }
        public void Save(RegionState state)=>Atomic(Path.Combine(directory,state.Key+".region"),w=>{
            w.Write(Magic);w.Write(Version);w.Write(state.LastTick);w.Write(state.Agents.Count);
            foreach(var a in state.Agents)WriteAgent(w,a);w.Write(state.Food.Count);
            foreach(var f in state.Food){w.Write(f.Identity??"");w.Write(f.Pattern);WriteV(w,f.Position);w.Write(f.Radius);w.Write(f.Substrate);w.Write(f.Organic);w.Write(f.Order);}
        });
        public void ReadSurvey(System.Collections.Generic.List<SurveyRegion> destination)=>SurveyPersistence.Read(Path.Combine(directory,"strategy.survey"),destination);
        public void WriteSurvey(System.Collections.Generic.IReadOnlyList<SurveyRegion> regions)=>SurveyPersistence.Write(Path.Combine(directory,"strategy.survey"),regions);
        static int Count(BinaryReader r) {int n=r.ReadInt32();if(n<0||n>10000)throw new InvalidDataException("Invalid region count");return n;}
        static void Atomic(string path,Action<BinaryWriter> write)
        {
            string temp=path+".tmp";using(var w=new BinaryWriter(File.Create(temp)))write(w);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        static void WriteV(BinaryWriter w,V3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
        static V3 ReadV(BinaryReader r)=>new V3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        static void WriteAgent(BinaryWriter w,VolumeAgent a)
        {
            w.Write(a.Identity??"");w.Write(a.FoodIdentity??"");WriteV(w,a.Position);WriteV(w,a.Previous);WriteV(w,a.Direction);WriteV(w,a.PreviousDirection);WriteV(w,a.Velocity);
            w.Write(a.Alive);w.Write(a.FedAt);w.Write(a.BitAt);w.Write(a.DiedAt);w.Write(a.RenderTime);w.Write(a.RenderDuration);w.Write(a.FoodCloud);
            w.Write(a.DivisionAt);WriteV(w,a.DivisionOrigin);WriteV(w,a.DivisionAxis);w.Write(a.DivisionChild);
            var c=a.Biology;w.Write(c.Id);w.Write(c.LineageId);w.Write(c.ParentId);w.Write(c.Generation);w.Write(c.BirthMutationMask);
            w.Write(c.Energy);w.Write(c.Age);w.Write(c.Growth);w.Write(c.Integrity);w.Write(c.AssimilatedEnergy);
            w.Write(c.FoodLastStep);w.Write(c.AttackLastStep);w.Write(c.DamageLastStep);w.Write(c.SwimSpeed);w.Write(c.AngularVelocity);w.Write(c.Boosting);w.Write(c.Consumed);
            WriteGenome(w,c.Genes);WriteGenome(w,c.ParentGenes);
            var b=c.Budget;
            foreach(float f in new[] {b.Start,b.Substrate,b.Organic,b.Light,b.Prey,b.Upkeep,b.Movement,b.Defense,b.Stress,b.Repair,b.Growth,b.Attack,b.Division,b.Contact,b.Transport,b.PredationLoss,b.Swimming,b.Turning,b.Boost})w.Write(f);
            w.Write(a.Order);
        }
        static void WriteGenome(BinaryWriter w,Genome g)
        {
            foreach(float f in new[] {g.Speed,g.Size,g.Uptake,g.PreferredTemperature,g.OrganicSpecialization,g.Adhesion,g.ColonySize,g.Branching,g.Differentiation,
                g.Transport,g.RolePattern,g.Photosynthesis,g.Predation,g.Membrane,g.Sensitivity,g.ThermalTolerance,g.ToxinResistance,g.EnergyEfficiency})w.Write(f);
        }
        static VolumeAgent ReadAgent(BinaryReader r,int version)
        {
            var a=new VolumeAgent {Identity=r.ReadString(),FoodIdentity=ReadOptional(r),Position=ReadV(r),Previous=ReadV(r),Direction=ReadV(r),PreviousDirection=ReadV(r),Velocity=ReadV(r),
                Alive=r.ReadBoolean(),FedAt=r.ReadDouble(),BitAt=r.ReadDouble(),DiedAt=r.ReadDouble(),RenderTime=r.ReadDouble(),RenderDuration=r.ReadDouble(),FoodCloud=r.ReadInt32()};
            if(version>=2){a.DivisionAt=r.ReadDouble();a.DivisionOrigin=ReadV(r);a.DivisionAxis=ReadV(r);a.DivisionChild=r.ReadBoolean();}
            a.Biology=new Cell {Id=r.ReadInt32(),LineageId=r.ReadInt32(),ParentId=r.ReadInt32(),Generation=r.ReadInt32(),BirthMutationMask=r.ReadInt32(),
                Energy=r.ReadSingle(),Age=r.ReadSingle(),Growth=r.ReadSingle(),Integrity=r.ReadSingle(),AssimilatedEnergy=r.ReadSingle(),
                FoodLastStep=r.ReadSingle(),AttackLastStep=r.ReadSingle(),DamageLastStep=r.ReadSingle(),SwimSpeed=r.ReadSingle(),AngularVelocity=r.ReadSingle(),Boosting=r.ReadBoolean(),Consumed=r.ReadBoolean()};
            a.Biology.Genes=ReadGenome(r);a.Biology.ParentGenes=ReadGenome(r);
            a.Biology.Budget=new EnergyBudget {Start=r.ReadSingle(),Substrate=r.ReadSingle(),Organic=r.ReadSingle(),Light=r.ReadSingle(),Prey=r.ReadSingle(),Upkeep=r.ReadSingle(),
                Movement=r.ReadSingle(),Defense=r.ReadSingle(),Stress=r.ReadSingle(),Repair=r.ReadSingle(),Growth=r.ReadSingle(),Attack=r.ReadSingle(),Division=r.ReadSingle(),
                Contact=r.ReadSingle(),Transport=r.ReadSingle(),PredationLoss=r.ReadSingle(),Swimming=r.ReadSingle(),Turning=r.ReadSingle(),Boost=r.ReadSingle()};
            if(version>=3)a.Order=r.ReadInt64();
            return a;
        }
        static Genome ReadGenome(BinaryReader r)=>new Genome {Speed=r.ReadSingle(),Size=r.ReadSingle(),Uptake=r.ReadSingle(),PreferredTemperature=r.ReadSingle(),OrganicSpecialization=r.ReadSingle(),
            Adhesion=r.ReadSingle(),ColonySize=r.ReadSingle(),Branching=r.ReadSingle(),Differentiation=r.ReadSingle(),Transport=r.ReadSingle(),RolePattern=r.ReadSingle(),
            Photosynthesis=r.ReadSingle(),Predation=r.ReadSingle(),Membrane=r.ReadSingle(),Sensitivity=r.ReadSingle(),ThermalTolerance=r.ReadSingle(),ToxinResistance=r.ReadSingle(),EnergyEfficiency=r.ReadSingle()};
        static string ReadOptional(BinaryReader r) {string s=r.ReadString();return s.Length==0?null:s;}
    }
}
