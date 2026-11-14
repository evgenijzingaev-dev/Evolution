using System;
using System.IO;
using Evolution.Core;

namespace Evolution.Volume
{
    // Cumulative events, never inferred from disappearing slots or camera visibility.
    public sealed class VolumeDiagnostics
    {
        public long Births,Starved,Predated,NextAgentOrder,NextFoodOrder;
        public int MaxGeneration;
        public double Intake,Light,Upkeep,Movement,Growth,Attack,Division,PredationLoss;
        public double ExternalNutrients,Solar,Recycled,RecyclingLoss,DecayLoss,AssimilationLoss;
        public double BirthOverhead,PredationDissipation,Unpaid;
        public VolumeDiagnostics Clone()=>(VolumeDiagnostics)MemberwiseClone();
        public double Dissipated=>Upkeep+Movement+Growth+Attack+BirthOverhead+RecyclingLoss+DecayLoss+AssimilationLoss+PredationDissipation-Unpaid;
        public long Deaths=>Starved+Predated;
        internal void Write(BinaryWriter w)
        {
            w.Write(1);w.Write(Births);w.Write(Starved);w.Write(Predated);w.Write(MaxGeneration);w.Write(NextAgentOrder);w.Write(NextFoodOrder);
            foreach(double v in new[] {Intake,Light,Upkeep,Movement,Growth,Attack,Division,PredationLoss,ExternalNutrients,Solar,Recycled,RecyclingLoss,DecayLoss,AssimilationLoss,BirthOverhead,PredationDissipation,Unpaid})w.Write(v);
        }
        internal static VolumeDiagnostics Read(BinaryReader r)
        {
            if(r.ReadInt32()!=1)throw new InvalidDataException("Unsupported ecology ledger; original retained.");
            return new VolumeDiagnostics {Births=r.ReadInt64(),Starved=r.ReadInt64(),Predated=r.ReadInt64(),MaxGeneration=r.ReadInt32(),NextAgentOrder=r.ReadInt64(),NextFoodOrder=r.ReadInt64(),
                Intake=r.ReadDouble(),Light=r.ReadDouble(),Upkeep=r.ReadDouble(),Movement=r.ReadDouble(),Growth=r.ReadDouble(),Attack=r.ReadDouble(),Division=r.ReadDouble(),PredationLoss=r.ReadDouble(),
                ExternalNutrients=r.ReadDouble(),Solar=r.ReadDouble(),Recycled=r.ReadDouble(),RecyclingLoss=r.ReadDouble(),DecayLoss=r.ReadDouble(),AssimilationLoss=r.ReadDouble(),BirthOverhead=r.ReadDouble(),PredationDissipation=r.ReadDouble(),Unpaid=r.ReadDouble()};
        }
        public void Metabolism(Cell c)
        {
            var b=c.Budget;Intake+=b.Substrate+b.Organic+b.Light;Light+=b.Light;
            Upkeep+=b.Upkeep;Movement+=b.Movement;Growth+=b.Growth;
        }
        public void Death(Cell c) {if(c.Consumed)Predated++;else Starved++;}
    }
    public struct VolumeSnapshot
    {
        public int Alive,MaxGeneration,EnergyBlocked,GrowthBlocked,MaturityBlocked,CapacityBlocked;
        public double Substrate,Organic,Energy,Structure;
        public double Stored=>Substrate*Biology.SubstrateEnergy+Organic+Energy+Structure;
        public void Add(VolumeSnapshot s) {Alive+=s.Alive;EnergyBlocked+=s.EnergyBlocked;GrowthBlocked+=s.GrowthBlocked;MaturityBlocked+=s.MaturityBlocked;CapacityBlocked+=s.CapacityBlocked;MaxGeneration=Math.Max(MaxGeneration,s.MaxGeneration);Substrate+=s.Substrate;Organic+=s.Organic;Energy+=s.Energy;Structure+=s.Structure;}
        public static VolumeSnapshot Read(VolumeWorld w)
        {
            var s=new VolumeSnapshot();
            for(int k=0;k<w.FoodCount;k++){var f=w.GetFood(k);s.Substrate+=f.Substrate;s.Organic+=f.Organic;}
            for(int i=0;i<w.Count;i++){
                var a=w.Get(i);if(!a.Alive)continue;var c=a.Biology;s.Alive++;s.Energy+=Math.Max(0,c.Energy);s.Structure+=Biology.StructureEnergy(c.Genes);s.MaxGeneration=Math.Max(s.MaxGeneration,c.Generation);
                if(w.Count>=VolumeWorld.PopulationLimit)s.CapacityBlocked++;
                else if(c.Generation>0&&c.Growth<1)s.GrowthBlocked++;
                else if(c.Age<Multicellularity.MaturityAge(c))s.MaturityBlocked++;
                else if(!Biology.CanDivide(c))s.EnergyBlocked++;
            }
            return s;
        }
    }
}
