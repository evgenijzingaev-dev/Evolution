using System;
using Evolution.Core;

namespace Evolution.Presentation
{
    // Pure visual projection. Mirrors WaterCellMotion.hlsl; never writes biology or RNG.
    public struct WaterCellPose
    {
        public float Phase, Drive, Activity, Amoeba, Turn, Feed, Damage, Shell;
        public static WaterCellPose From(CellAnimation animation, Cell cell, double time)
        {
            return new WaterCellPose {
                Phase=(float)(animation.Phase%(Math.PI*40)), Drive=animation.Drive,
                Activity=animation.Activity, Turn=animation.Turn,
                Amoeba=CrystalForms.From(cell.Genes)==CrystalForm.Amoeboid?1:0,
                Feed=animation.Feeding(time)+animation.Readiness*.45f, Damage=animation.Damage(time), Shell=cell.Genes.Membrane
            };
        }
        public void Deform(ref float x,ref float y,ref float z)
        {
            float radius=(float)Math.Sqrt(x*x+y*y+z*z);
            float nx=radius>.00001f?x/radius:0, nz=radius>.00001f?z/radius:0;
            float angle=(float)Math.Atan2(z,x);
            float compliance=(1-.8f*Shell)*Activity;
            float breath=.012f*(float)Math.Sin(Phase*.37f);
            float wave=.023f*(float)Math.Sin(3*angle-Phase)*compliance;
            // Extension, fluid support, then rear contraction. No implied invisible floor.
            float reach=Amoeba*Drive*compliance;
            float front=(float)Math.Pow(Math.Max(0,nx),4);
            float side=(float)Math.Pow(Math.Max(0,nz),4);
            float extension=.5f+.5f*(float)Math.Sin(Phase);
            float radial=1+breath+wave+reach*(front*.22f*extension+side*.08f*(float)Math.Sin(Phase-1.1f));
            radial+=front*Feed*.07f-Damage*.035f*(float)Math.Sin(Phase*7+angle*4);
            float original=x;
            x=x*radial*(1+.055f*Drive*compliance)+reach*.045f*(float)Math.Sin(Phase-.8f);
            y*=radial/(float)Math.Sqrt(1+.055f*Drive*compliance);
            z=z*radial+Turn*.035f*original*original;
        }
        public static float Separation(float age)=>Smooth((age-.12f)/.76f);
        public static float DaughterVisibility(float age)=>Smooth((age-.12f)/.18f);
        public static float ShellSeparation(float age)=>Smooth((age-.58f)/.28f);
        public static float Rupture(float age)=>Smooth((age-.85f)/.65f);
        public static float Remains(float age)=>1-Smooth((age-1.6f)/2.4f);
        static float Smooth(float x) {x=AnimationTargets.Clamp(x);return x*x*(3-2*x);}
    }
}
