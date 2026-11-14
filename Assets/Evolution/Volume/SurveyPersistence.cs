using System;
using System.Collections.Generic;
using System.IO;
namespace Evolution.Volume
{
    // Optional compact sidecar. Existing .region format/version remains untouched.
    public static class SurveyPersistence
    {
        const int Magic=0x45565331,CurrentMagic=0x45565332;
        public static void Read(string path,List<SurveyRegion> result)
        {
            if(!File.Exists(path))return;
            using(var r=new BinaryReader(File.OpenRead(path))) {
                int magic=r.ReadInt32();if(magic!=Magic&&magic!=CurrentMagic)throw new InvalidDataException("Unsupported ocean survey; original retained.");
                int n=Bound(r.ReadInt32(),OceanSurvey.RegionLimit);
                for(int i=0;i<n;i++) {
                    var region=new SurveyRegion {Key=new ChunkKey(r.ReadInt32(),r.ReadInt32(),r.ReadInt32()),Tick=r.ReadInt64(),Living=r.ReadInt32(),Dead=r.ReadInt32(),Attacks=r.ReadInt32(),
                        Substrate=r.ReadSingle(),Organic=r.ReadSingle(),Temperature=r.ReadSingle(),Light=r.ReadSingle(),Center=Vector(r),Current=Vector(r)};
                    for(int j=0;j<6;j++)region.Forms[j]=r.ReadInt32();for(int j=0;j<4;j++)region.Diets[j]=r.ReadInt32();
                    int lines=Bound(r.ReadInt32(),OceanSurvey.AgentHistoryLimit);
                    for(int j=0;j<lines;j++)region.Lineages.Add(r.ReadInt32(),r.ReadInt32());if(magic==CurrentMagic){region.Nutrients=r.ReadSingle();region.Oxygen=r.ReadSingle();region.MeanTolerance=r.ReadSingle();region.MeanGeneration=r.ReadSingle();region.MeanOptimum=r.ReadSingle();}result.Add(region);
                }
            }
        }
        public static void Write(string path,IReadOnlyList<SurveyRegion> regions)
        {
            string temp=path+".tmp";using(var w=new BinaryWriter(File.Create(temp))){w.Write(CurrentMagic);w.Write(Math.Min(OceanSurvey.RegionLimit,regions.Count));
                for(int i=0;i<Math.Min(OceanSurvey.RegionLimit,regions.Count);i++) {var r=regions[i];w.Write(r.Key.X);w.Write(r.Key.Y);w.Write(r.Key.Z);w.Write(r.Tick);w.Write(r.Living);w.Write(r.Dead);w.Write(r.Attacks);
                    w.Write(r.Substrate);w.Write(r.Organic);w.Write(r.Temperature);w.Write(r.Light);Vector(w,r.Center);Vector(w,r.Current);foreach(int n in r.Forms)w.Write(n);foreach(int n in r.Diets)w.Write(n);w.Write(r.Lineages.Count);foreach(var line in r.Lineages){w.Write(line.Key);w.Write(line.Value);}w.Write(r.Nutrients);w.Write(r.Oxygen);w.Write(r.MeanTolerance);w.Write(r.MeanGeneration);w.Write(r.MeanOptimum);}
            }
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        static int Bound(int count,int max){if(count<0||count>max)throw new InvalidDataException("Invalid survey count");return count;}
        static void Vector(BinaryWriter w,V3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        static V3 Vector(BinaryReader r)=>new V3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    }
}

