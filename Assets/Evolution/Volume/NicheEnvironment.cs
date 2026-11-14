using System;
namespace Evolution.Volume
{
    public enum EcologicalScenario { Legacy, Natural, Sunlit, Dark, Hydrothermal, Organic }
    public struct NicheSample
    {
        public float Light,Temperature,Nutrients,Oxygen,Chemicals,Dissolved,Particles,Vent,Coverage;
        public V3 Current;
    }
    // Continuous deterministic forcing, not species zones. Stocks are held by moving fluid parcels.
    public static class NicheEnvironment
    {
        static float Bell(float value,float width)=>(float)Math.Exp(-value*value/(width*width));
        public static float Vent(V3 p,V3 offset)
        {
            float x=(float)Math.Sin((p.X+offset.X-12)*Math.PI/80);
            float z=(float)Math.Sin((p.Z+offset.Z+8)*Math.PI/80);
            return Bell(x,.19f)*Bell(z,.19f)*Bell(p.Y+18,8);
        }
        public static NicheSample Sample(V3 p,double time,V3 offset,EcologicalScenario scenario)
        {
            float vent=Vent(p,offset),depth=Math.Max(0,24-p.Y);
            float shade=.84f+.16f*(float)Math.Sin((p.X+offset.X)*.07)* (float)Math.Cos((p.Z+offset.Z)*.09);
            var s=new NicheSample {Light=(float)Math.Exp(-depth*.052f)*shade,
                Temperature=12+12*(float)Math.Exp(-depth*.025f)+vent*31+2*(float)Math.Sin((p.X+offset.X)*.045),
                Nutrients=.6f+.4f*(1-(float)Math.Exp(-depth*.025f)),Oxygen=.4f+.6f*(float)Math.Exp(-depth*.035f),
                Chemicals=.025f+vent*.975f,Dissolved=(.15f+.85f*(.5f+.5f*(float)Math.Sin((p.X+offset.X)*.09f+(p.Z+offset.Z)*.04f)))*(.5f+.5f*(1-(float)Math.Exp(-depth*.025f))),Vent=vent,
                Current=WaterField.Current(p+offset,time)+new V3(0,vent*.15f,0)};
            switch(scenario) {
                case EcologicalScenario.Sunlit:s.Light=1;s.Temperature=23;s.Nutrients=1;s.Oxygen=1;s.Chemicals=.08f;s.Dissolved=.1f;s.Vent=0;break;
                case EcologicalScenario.Dark:s.Light=0;s.Temperature=14;s.Nutrients=1;s.Oxygen=.6f;s.Chemicals=.12f;s.Dissolved=.7f;s.Vent=0;break;
                case EcologicalScenario.Hydrothermal:s.Light=.01f;s.Temperature=43;s.Nutrients=1;s.Oxygen=.65f;s.Chemicals=1;s.Dissolved=.35f;s.Vent=1;break;
                case EcologicalScenario.Organic:s.Light=.08f;s.Temperature=23;s.Nutrients=.7f;s.Oxygen=.8f;s.Chemicals=.05f;s.Dissolved=1;s.Vent=0;break;
            }
            return s;
        }
        public static bool TryVent(ChunkKey key,V3 offset,out V3 vent)
        {
            var p=key.Origin;float x=(float)Math.Ceiling((p.X+offset.X-12)/80)*80+12-offset.X;
            float z=(float)Math.Ceiling((p.Z+offset.Z+8)/80)*80-8-offset.Z;
            vent=new V3(x,-18,z);return x>=p.X&&x<p.X+20&&z>=p.Z&&z<p.Z+20&&-18>=p.Y&&-18<p.Y+20;
        }
        // Compact-support C1 kernel: no concentration jump at a parcel edge.
        public static float Kernel(V3 point,FoodCloud parcel)
        {if(!(parcel.Radius>0)||float.IsInfinity(parcel.Radius))return 0;float q=(point-parcel.Position).Sqr/(parcel.Radius*parcel.Radius);return q>=1?0:(1-q)*(1-q);}
    }
    public interface INicheArchive { EcologicalScenario Scenario {get;set;} }
}
