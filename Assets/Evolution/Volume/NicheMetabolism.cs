using System;
using Evolution.Core;
namespace Evolution.Volume
{
    public static class NicheMetabolism
    {
        public static float Thermal(Genome g,float temperature)
        {float width=7+15*g.ThermalTolerance,d=(temperature-g.PreferredTemperature)/width;return (float)Math.Exp(-.5f*d*d);}
        public static float OxygenFactor(float oxygen)=>oxygen/(4+oxygen);
        public static float Stress(Cell c,NicheSample s)=>.32f*(1-Thermal(c.Genes,s.Temperature))+.14f*(1-OxygenFactor(s.Oxygen))*(1-.5f*c.Genes.EnergyEfficiency);
        public static float NewbornMatter(Genome g)=>.6f*g.Size;
        // Nitrogen-like limiting matter tracer. Carbon/water are not an atomistic chemistry model.
        public static void Feed(ref VolumeAgent a,ref FoodCloud f,NicheSample s,float dt,VolumeDiagnostics d)
        {
            var c=a.Biology;var g=c.Genes;float startEnergy=c.Energy,foodBefore=f.Substrate*2+f.Organic+f.Dissolved;
            float uptake=g.Uptake*Multicellularity.Feeding(c)*dt;
            float mineral=Math.Min(f.Nutrients,Math.Min(uptake*.08f,Math.Max(0,g.Size*.75f-a.MineralReserve)));
            f.Nutrients-=mineral;a.MineralReserve+=mineral;
            float thermal=Thermal(g,s.Temperature),role=Multicellularity.Feeding(c);
            float mineralAvailability=f.Nutrients+a.MineralReserve;
            float gross=1.7f*g.Photosynthesis*s.Light*thermal*role*(1-.65f*g.Predation)*dt*mineralAvailability/(.25f+mineralAvailability);
            float gain=Math.Max(0,Math.Min(Biology.Capacity(g)-c.Energy,gross));
            // Maintenance photochemistry stores energy; only actual biomass growth consumes limiting matter.
            // A full mineral reserve must not shut down an adult chloroplast. Birth/growth still pay every unit.
            c.Energy+=gain;c.Budget.Light+=gain;NicheResources.ProduceOxygen(ref f,gain*.08f,d);
            float oxygen=.2f+.8f*OxygenFactor(f.Oxygen);
            float assimilation=Math.Min(.95f,.2f+.65f*g.OrganicSpecialization+.1f*g.EnergyEfficiency)*thermal*oxygen;
            float soluble=Math.Min(f.Dissolved,Math.Min(Math.Max(0,Biology.Capacity(g)-c.Energy)/Math.Max(.000001f,assimilation),uptake*2*(.35f+.65f*g.OrganicSpecialization)));
            f.Dissolved-=soluble;float solubleGain=soluble*assimilation;c.Energy+=solubleGain;c.Budget.Organic+=solubleGain;uptake=Math.Max(0,uptake-soluble/2);
            float filter=.45f+.55f*g.Uptake/(1+g.Uptake);
            float particles=Math.Min(f.Organic,Math.Min(Math.Max(0,Biology.Capacity(g)-c.Energy)/Math.Max(.000001f,assimilation),uptake*2*filter));
            f.Organic-=particles;float filteredGain=particles*assimilation;c.Energy+=filteredGain;c.Budget.Organic+=filteredGain;uptake=Math.Max(0,uptake-particles/2);
            // Reduced chemical carrier is distinct from inert mineral nutrients. Existing allocation genes trade off pathways.
            float yield=2*(1-.8f*g.OrganicSpecialization)*(1-.6f*g.Photosynthesis)*thermal*(.15f+.85f*OxygenFactor(f.Oxygen));
            float chemical=Math.Min(f.Substrate,Math.Min(uptake,Math.Max(0,Biology.Capacity(g)-c.Energy)/Math.Max(.000001f,yield)));
            f.Substrate-=chemical;float chemicalGain=chemical*yield;c.Energy+=chemicalGain;c.Budget.Substrate+=chemicalGain;
            NicheResources.ConsumeOxygen(ref f,(solubleGain+filteredGain+chemicalGain)*.055f,d);
            if(d!=null) {
                d.Solar+=gain;d.NichePhoto+=gain;d.NicheDissolved+=solubleGain;d.NicheFiltered+=filteredGain;d.NicheChemical+=chemicalGain;
                d.AssimilationLoss+=Math.Max(0,foodBefore-(f.Substrate*2+f.Organic+f.Dissolved)+gain-(c.Energy-startEnergy));
            }
            a.Biology=c;
        }
    }
    public sealed partial class VolumeWorld
    {
        NicheSample[] nicheCache;float[] lightTransmission;
        void CacheNiches()
        {
            if(!Niches)return;
            if(nicheCache==null||nicheCache.Length!=food.Length){nicheCache=new NicheSample[food.Length];lightTransmission=new float[food.Length];}
            for(int k=0;k<food.Length;k++) {
                var f=food[k];float shade=0;
                for(int i=0;i<agents.Count;i++) {var a=agents[i];if(!a.Alive)continue;var delta=a.Position-f.Position;
                    if(delta.Y<0||delta.Y>6)continue;float radial=(delta.X*delta.X+delta.Z*delta.Z)/16;
                    if(radial<1)shade+=a.Biology.Genes.Size*a.Biology.Genes.Size*(1-radial)*(1-delta.Y/6);
                }
                lightTransmission[k]=(float)Math.Exp(-.12f*shade);
                nicheCache[k]=NicheEnvironment.Sample(f.Position,Time,FlowOffset,Scenario);nicheCache[k].Light*=lightTransmission[k];
            }
        }
        float NichePotential(int index,VolumeAgent a)
        {
            var f=food[index];a.Biology.Energy=0;a.Biology.Budget=default;
            NicheMetabolism.Feed(ref a,ref f,nicheCache[index],1,null);return a.Biology.Energy;
        }
        void DepositMatter(ref VolumeAgent a,int index)
        {
            float matter=a.Matter+a.MineralReserve;a.Matter=a.MineralReserve=0;
            if(index>=0)food[index].DetritalMatter+=matter;else Diagnostics.MatterLost+=matter;
        }
        void TransferPreyMatter(ref VolumeAgent hunter,ref VolumeAgent prey,int remnant)
        {
            if(!prey.Biology.Consumed)return;
            float total=prey.Matter+prey.MineralReserve;
            float assimilated=Math.Min(total*.65f,Math.Max(0,1.8f*hunter.Biology.Genes.Size-hunter.MineralReserve));
            hunter.MineralReserve+=assimilated;prey.MineralReserve=0;prey.Matter=total-assimilated;DepositMatter(ref prey,remnant);
        }
    }
}
