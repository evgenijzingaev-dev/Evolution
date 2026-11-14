using System;
using Evolution.Core;
namespace Evolution.Volume
{
    public static class NicheResources
    {
        public const float NutrientCapacity=48,OxygenCapacity=80,DissolvedCapacity=90;
        public static void Initialize(ref FoodCloud f,NicheSample forcing,VolumeDiagnostics ledger)
        {
            if(f.NicheInitialized)return;
            f.NicheInitialized=true;f.Nutrients=24*forcing.Nutrients;f.Oxygen=OxygenCapacity*forcing.Oxygen;
            // Partition existing organic energy. Migration adds no organic/chemical energy.
            f.Dissolved=f.Organic*.45f;f.Organic-=f.Dissolved;
            ledger.InitialMatter+=f.Nutrients;ledger.InitialOxygen+=f.Oxygen;
        }
        public static void Initialize(ref VolumeAgent a,VolumeDiagnostics ledger)
        {
            if(a.NicheInitialized)return;a.NicheInitialized=true;
            if(a.Alive) {a.Matter=a.Biology.Genes.Size*(a.Biology.Generation==0?1:.4f+.6f*a.Biology.Growth);a.MineralReserve=a.Biology.Genes.Size*.5f;}
            ledger.InitialMatter+=a.Matter+a.MineralReserve;
        }
        public static void Advance(ref FoodCloud f,NicheSample forcing,float dt,VolumeDiagnostics d,bool supply)
        {
            float decay=f.Organic*(1-(float)Math.Exp(-.007f*dt));f.Organic-=decay;
            // Detritus dissolves first; remaining chemical energy is dissipated, never regenerated.
            f.Dissolved+=decay*.7f;d.DecayLoss+=decay*.3f;
            float dissolved=f.Dissolved*(1-(float)Math.Exp(-.004f*dt));f.Dissolved-=dissolved;d.DecayLoss+=dissolved;
            ConsumeOxygen(ref f,(decay+dissolved)*.04f,d);
            float matter=f.DetritalMatter*(1-(float)Math.Exp(-.012f*dt));f.DetritalMatter-=matter;
            float reclaimed=Math.Min(Math.Max(0,NutrientCapacity-f.Nutrients),matter*.85f);
            f.Nutrients+=reclaimed;d.MatterLost+=matter-reclaimed;
            if(supply) {
                float chemical=Math.Min(Math.Max(0,CloudEcology.SubstrateCapacity-f.Substrate),.4f*forcing.Chemicals*dt);
                f.Substrate+=chemical;d.ExternalNutrients+=chemical*Biology.SubstrateEnergy;
                float nutrient=Math.Min(Math.Max(0,NutrientCapacity-f.Nutrients),.11f*forcing.Nutrients*dt);
                f.Nutrients+=nutrient;d.ExternalMatter+=nutrient;
                float organic=Math.Min(Math.Max(0,DissolvedCapacity-f.Dissolved),.48f*forcing.Dissolved*dt);
                f.Dissolved+=organic;d.ExternalOrganic+=organic;
            }
            // Gas exchange with unmodelled surrounding water is a measured gas flux, not energy.
            float exchange=(OxygenCapacity*forcing.Oxygen-f.Oxygen)*(1-(float)Math.Exp(-dt*.025f));
            f.Oxygen+=exchange;if(exchange>=0)d.OxygenIn+=exchange;else d.OxygenOut-=exchange;
        }
        public static void ConsumeOxygen(ref FoodCloud f,float demand,VolumeDiagnostics d)
        {float used=Math.Min(f.Oxygen,Math.Max(0,demand));f.Oxygen-=used;d.OxygenUsed+=used;}
        public static void ProduceOxygen(ref FoodCloud f,float amount,VolumeDiagnostics d)
        {d.OxygenProduced+=amount;float kept=Math.Min(Math.Max(0,OxygenCapacity-f.Oxygen),amount);f.Oxygen+=kept;d.OxygenOut+=amount-kept;}
        public static void Mix(ref FoodCloud a,ref FoodCloud b,float dt)
        {
            float q=(a.Position-b.Position).Sqr/36;if(q>=1)return;float fraction=Math.Min(.2f,.06f*dt*(1-q)*(1-q));
            Exchange(ref a.Nutrients,ref b.Nutrients,fraction);Exchange(ref a.Substrate,ref b.Substrate,fraction);
            Exchange(ref a.Dissolved,ref b.Dissolved,fraction);Exchange(ref a.Oxygen,ref b.Oxygen,fraction);
        }
        static void Exchange(ref float a,ref float b,float fraction) {float moved=(a-b)*fraction;a-=moved;b+=moved;}
    }
}
