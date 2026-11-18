using System;
using Evolution.Core;

namespace Evolution.Volume
{
    public static class CloudEcology
    {
        // Ocean upwelling is an external chemical source, per cloud, never per cell.
        public const float SubstrateCapacity=35,SupplyPerSecond=.4f,DecayPerSecond=.007f,RecyclingYield=.35f;
        public static void Advance(ref FoodCloud f,float dt,VolumeDiagnostics ledger,bool supply=true)
        {
            float decomposed=f.Organic*(1-(float)Math.Exp(-DecayPerSecond*dt));
            f.Organic-=decomposed;
            float recycled=Math.Min(Math.Max(0,SubstrateCapacity-f.Substrate)*Biology.SubstrateEnergy,decomposed*RecyclingYield);
            f.Substrate+=recycled/Biology.SubstrateEnergy;ledger.Recycled+=recycled;ledger.DecayLoss+=decomposed-recycled;
            float added=supply?Math.Min(Math.Max(0,SubstrateCapacity-f.Substrate),SupplyPerSecond*dt):0;
            f.Substrate+=added;ledger.ExternalNutrients+=added*Biology.SubstrateEnergy;
        }
        public static void Feed(ref Cell c,ref FoodCloud f,OceanSample sample,float dt,VolumeDiagnostics ledger)
        {
            float before=f.Substrate*Biology.SubstrateEnergy+f.Organic,energy=c.Energy;
            float light=Ecology.Photosynthesize(ref c,ref f.Substrate,sample,dt);c.Budget.Light+=light;
            // 0.12 substrate = 0.24 chemical units per unit of gross photo gain.
            ledger.Solar+=light*(1-.12f*Biology.SubstrateEnergy);
            Biology.Feed(ref c,ref f.Substrate,ref f.Organic,sample.Temperature,dt);
            ledger.AssimilationLoss+=Math.Max(0,before-(f.Substrate*Biology.SubstrateEnergy+f.Organic)+light*(1-.12f*Biology.SubstrateEnergy)-(c.Energy-energy));
        }
    }
}
