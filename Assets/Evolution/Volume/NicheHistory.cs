using System;
using System.Collections.Generic;
using System.IO;
namespace Evolution.Volume
{
    public struct NicheHistoryPoint
    {
        public long Tick,Births,Deaths;public ChunkKey Scope;public int Living;
        public float Photo,Organic,Hot,Frugal,Generation,Nutrients,Oxygen;
    }
    public interface INicheHistoryArchive
    {
        void ReadNicheHistory(List<NicheHistoryPoint> destination);
        void WriteNicheHistory(IReadOnlyList<NicheHistoryPoint> source);
    }
    public static class NicheHistoryStore
    {
        public const int Limit=128;
        public static void Read(string path,List<NicheHistoryPoint> result)
        {
            if(!File.Exists(path))return;using(var r=new BinaryReader(File.OpenRead(path))) {
                if(r.ReadInt32()!=0x4e494331)throw new InvalidDataException("Invalid ecological history");int count=r.ReadInt32();
                if(count<0||count>Limit)throw new InvalidDataException("Invalid ecological history length");
                for(int i=0;i<count;i++)result.Add(new NicheHistoryPoint {Tick=r.ReadInt64(),Births=r.ReadInt64(),Deaths=r.ReadInt64(),
                    Scope=new ChunkKey(r.ReadInt32(),r.ReadInt32(),r.ReadInt32()),Living=r.ReadInt32(),Photo=r.ReadSingle(),Organic=r.ReadSingle(),Hot=r.ReadSingle(),Frugal=r.ReadSingle(),Generation=r.ReadSingle(),Nutrients=r.ReadSingle(),Oxygen=r.ReadSingle()});
            }
        }
        public static void Write(string path,IReadOnlyList<NicheHistoryPoint> source)
        {
            string temp=path+".tmp";using(var w=new BinaryWriter(File.Create(temp))) {
                w.Write(0x4e494331);w.Write(Math.Min(Limit,source.Count));for(int i=Math.Max(0,source.Count-Limit);i<source.Count;i++) {
                    var p=source[i];w.Write(p.Tick);w.Write(p.Births);w.Write(p.Deaths);w.Write(p.Scope.X);w.Write(p.Scope.Y);w.Write(p.Scope.Z);w.Write(p.Living);
                    foreach(float v in new[]{p.Photo,p.Organic,p.Hot,p.Frugal,p.Generation,p.Nutrients,p.Oxygen})w.Write(v);
                }
            }
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
    }
    public static class NicheReadout
    {
        public static string Explain(VolumeWorld world,int index)
        {
            if(!world.Niches)return CellEvolution.EnergyReason(world,index);
            if(index<0||index>=world.Count)return "Выбери организм: среда определяет его возможности.";
            var a=world.Get(index);if(!a.Alive)return CellEvolution.DeathReason(a);
            var c=a.Biology;var e=world.EnvironmentAt(a.Position);var b=c.Budget;
            string source=b.Light>=b.Organic&&b.Light>=b.Substrate?"свет":b.Organic>b.Substrate?"органика":"химический субстрат";
            string limit=a.MineralReserve<NicheMetabolism.NewbornMatter(c.Genes)?"Мало минералов для новой биомассы.":"Минералов для роста достаточно.";
            if(NicheMetabolism.Thermal(c.Genes,e.Temperature)<.6f)limit="Температура снижает усвоение и повышает расход.";
            else if(e.Oxygen<3)limit="Мало кислорода: дыхание менее эффективно.";
            else if(c.Genes.Photosynthesis>.6f&&e.Light<.18f)limit="Мало света: фотосистемы требуют содержания.";
            if(b.Intake<=.00001f)return "Питание сейчас не покрывает расходы. "+limit;
            return "Источник: "+source+" · баланс "+b.Net.ToString("+0.00;-0.00;0.00")+" за шаг.\n"+limit;
        }
        public static string Trend(IReadOnlyList<NicheHistoryPoint> history)
        {
            if(history.Count<2)return "Отбор виден за поколения; история накапливается каждые 10 с.";
            var last=history[history.Count-1];var first=last;
            for(int i=history.Count-2;i>=0;i--){var p=history[i];if(!p.Scope.Equals(last.Scope)||last.Tick-p.Tick>1200)break;first=p;}
            return "Наблюдаемая область: светолюбивые "+first.Photo.ToString("P0")+" → "+last.Photo.ToString("P0")+" · среднее G "+last.Generation.ToString("F1");
        }
    }
}
