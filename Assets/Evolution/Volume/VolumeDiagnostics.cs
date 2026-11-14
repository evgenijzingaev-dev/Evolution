using System;
using Evolution.Core;

namespace Evolution.Volume
{
    // Cumulative events, never inferred from disappearing slots or camera visibility.
    public sealed class VolumeDiagnostics
    {
        public long Births,Starved,Predated;
        public double Intake,Light,Upkeep,Movement,Growth,Attack,Division,PredationLoss;
        public double ExternalNutrients,Solar,Recycled,RecyclingLoss,DecayLoss,AssimilationLoss;
        public long Deaths=>Starved+Predated;
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
        public double Substrate,Organic,Energy;
        public static VolumeSnapshot Read(VolumeWorld w)
        {
            var s=new VolumeSnapshot();
            for(int k=0;k<w.FoodCount;k++){var f=w.GetFood(k);s.Substrate+=f.Substrate;s.Organic+=f.Organic;}
            for(int i=0;i<w.Count;i++){
                var a=w.Get(i);if(!a.Alive)continue;var c=a.Biology;s.Alive++;s.Energy+=c.Energy;s.MaxGeneration=Math.Max(s.MaxGeneration,c.Generation);
                if(w.Count>=VolumeWorld.PopulationLimit)s.CapacityBlocked++;
                else if(c.Generation>0&&c.Growth<1)s.GrowthBlocked++;
                else if(c.Age<Multicellularity.MaturityAge(c))s.MaturityBlocked++;
                else if(!Biology.CanDivide(c))s.EnergyBlocked++;
            }
            return s;
        }
    }
}
