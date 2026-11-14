using System;
using System.IO;
using Evolution.Core;

namespace Evolution.Volume
{
    // Cumulative events, never inferred from disappearing slots or camera visibility.
    public sealed class VolumeDiagnostics
    {
        public bool NicheLedger;public long NicheEpochTick;
        public double InitialEnergy;
        public double NichePhoto,NicheDissolved,NicheFiltered,NicheChemical;
        public double InitialMatter,ExternalMatter,MatterLost,InitialOxygen,OxygenIn,OxygenOut,OxygenProduced,OxygenUsed,ExternalOrganic;
        public long Births,Starved,Predated,NextAgentOrder,NextFoodOrder;
        public int MaxGeneration;
        public double Intake,Light,Upkeep,Movement,Growth,Attack,Division,PredationLoss;
        public double ExternalNutrients,Solar,Recycled,RecyclingLoss,DecayLoss,AssimilationLoss;
        public double Contact,TransportLoss,TransportReceived;
        public double BirthOverhead,PredationDissipation,Unpaid;
        public VolumeDiagnostics Clone()=>(VolumeDiagnostics)MemberwiseClone();
        public double Dissipated=>Contact+TransportLoss+Upkeep+Movement+Growth+Attack+BirthOverhead+RecyclingLoss+DecayLoss+AssimilationLoss+PredationDissipation-Unpaid;
        public long Deaths=>Starved+Predated;
        internal void Write(BinaryWriter w)
        {
            w.Write(6);w.Write(Births);w.Write(Starved);w.Write(Predated);w.Write(MaxGeneration);w.Write(NextAgentOrder);w.Write(NextFoodOrder);
            foreach(double v in new[] {Intake,Light,Upkeep,Movement,Growth,Attack,Division,PredationLoss,ExternalNutrients,Solar,Recycled,RecyclingLoss,DecayLoss,AssimilationLoss,BirthOverhead,PredationDissipation,Unpaid,Contact,TransportLoss,TransportReceived})w.Write(v);
            foreach(double v in new[] {InitialMatter,ExternalMatter,MatterLost,InitialOxygen,OxygenIn,OxygenOut,OxygenProduced,OxygenUsed,ExternalOrganic})w.Write(v);
            foreach(double v in new[]{NichePhoto,NicheDissolved,NicheFiltered,NicheChemical})w.Write(v);w.Write(InitialEnergy);w.Write(NicheLedger);w.Write(NicheEpochTick);
        }
        internal static VolumeDiagnostics Read(BinaryReader r)
        {
            int version=r.ReadInt32();if(version<1||version>6)throw new InvalidDataException("Unsupported ecology ledger; original retained.");
            var d=new VolumeDiagnostics {Births=r.ReadInt64(),Starved=r.ReadInt64(),Predated=r.ReadInt64(),MaxGeneration=r.ReadInt32(),NextAgentOrder=r.ReadInt64(),NextFoodOrder=r.ReadInt64(),
                Intake=r.ReadDouble(),Light=r.ReadDouble(),Upkeep=r.ReadDouble(),Movement=r.ReadDouble(),Growth=r.ReadDouble(),Attack=r.ReadDouble(),Division=r.ReadDouble(),PredationLoss=r.ReadDouble(),
                ExternalNutrients=r.ReadDouble(),Solar=r.ReadDouble(),Recycled=r.ReadDouble(),RecyclingLoss=r.ReadDouble(),DecayLoss=r.ReadDouble(),AssimilationLoss=r.ReadDouble(),BirthOverhead=r.ReadDouble(),PredationDissipation=r.ReadDouble(),Unpaid=r.ReadDouble()};
            if(version>=2){d.Contact=r.ReadDouble();d.TransportLoss=r.ReadDouble();d.TransportReceived=r.ReadDouble();}
            if(version>=3){d.InitialMatter=r.ReadDouble();d.ExternalMatter=r.ReadDouble();d.MatterLost=r.ReadDouble();d.InitialOxygen=r.ReadDouble();d.OxygenIn=r.ReadDouble();d.OxygenOut=r.ReadDouble();d.OxygenProduced=r.ReadDouble();d.OxygenUsed=r.ReadDouble();d.ExternalOrganic=r.ReadDouble();}
            if(version>=4){d.NichePhoto=r.ReadDouble();d.NicheDissolved=r.ReadDouble();d.NicheFiltered=r.ReadDouble();d.NicheChemical=r.ReadDouble();}if(version>=5)d.InitialEnergy=r.ReadDouble();if(version>=6){d.NicheLedger=r.ReadBoolean();d.NicheEpochTick=r.ReadInt64();}return d;
        }
        internal void BeginNicheEpoch(long tick)
        {
            NicheLedger=true;NicheEpochTick=tick;
            Intake=Light=Upkeep=Movement=Growth=Attack=Division=PredationLoss=ExternalNutrients=Solar=Recycled=RecyclingLoss=DecayLoss=AssimilationLoss=BirthOverhead=PredationDissipation=Unpaid=Contact=TransportLoss=TransportReceived=0;
            InitialEnergy=InitialMatter=ExternalMatter=MatterLost=InitialOxygen=OxygenIn=OxygenOut=OxygenProduced=OxygenUsed=ExternalOrganic=NichePhoto=NicheDissolved=NicheFiltered=NicheChemical=0;
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
        public double Matter,Oxygen;
        public int Solitary,Pairs,SmallColonies,MediumColonies,LargeColonies;
        public int Alive,MaxGeneration,EnergyBlocked,GrowthBlocked,MaturityBlocked,CapacityBlocked;
        public double Substrate,Organic,Energy,Structure;
        public double Stored=>Substrate*Biology.SubstrateEnergy+Organic+Energy+Structure;
        public void Add(VolumeSnapshot s) {Matter+=s.Matter;Oxygen+=s.Oxygen;Alive+=s.Alive;Solitary+=s.Solitary;Pairs+=s.Pairs;SmallColonies+=s.SmallColonies;MediumColonies+=s.MediumColonies;LargeColonies+=s.LargeColonies;EnergyBlocked+=s.EnergyBlocked;GrowthBlocked+=s.GrowthBlocked;MaturityBlocked+=s.MaturityBlocked;CapacityBlocked+=s.CapacityBlocked;MaxGeneration=Math.Max(MaxGeneration,s.MaxGeneration);Substrate+=s.Substrate;Organic+=s.Organic;Energy+=s.Energy;Structure+=s.Structure;}
        public static VolumeSnapshot Read(VolumeWorld w)
        {
            var s=new VolumeSnapshot();
            for(int k=0;k<w.FoodCount;k++){var f=w.GetFood(k);s.Substrate+=f.Substrate;s.Organic+=f.Organic+f.Dissolved;s.Matter+=f.Nutrients+f.DetritalMatter;s.Oxygen+=f.Oxygen;}
            for(int i=0;i<w.Count;i++){
                var a=w.Get(i);if(!a.Alive)continue;var c=a.Biology;s.Alive++;
                if(w.OrganismRoot(i)==i){int size=w.OrganismSize(i);if(size==1)s.Solitary++;else if(size==2)s.Pairs++;else if(size<=4)s.SmallColonies++;else if(size<=8)s.MediumColonies++;else s.LargeColonies++;}
                s.Matter+=a.Matter+a.MineralReserve;s.Energy+=Math.Max(0,c.Energy);s.Structure+=Biology.StructureEnergy(c.Genes);s.MaxGeneration=Math.Max(s.MaxGeneration,c.Generation);
                if(w.Count>=VolumeWorld.PopulationLimit)s.CapacityBlocked++;
                else if(c.Generation>0&&c.Growth<1)s.GrowthBlocked++;
                else if(c.Age<Multicellularity.MaturityAge(c))s.MaturityBlocked++;
                else if(!Biology.CanDivide(c)||c.Energy<w.DivisionThreshold(i))s.EnergyBlocked++;
            }
            return s;
        }
    }
}

