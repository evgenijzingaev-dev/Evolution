using System;
using Evolution.Core;

namespace Evolution.Presentation
{
    public enum CrystalForm { Ciliate, Spindle, Amoeboid, Diatom, Radiolarian, Photocyte }
    // An art projection of existing traits, never a new species, ability or biological class.
    public static class CrystalForms
    {
        public const float BoundingRadius=1.05f;
        public static CrystalForm From(Genome g)=>g.Membrane>.72f?CrystalForm.Diatom
            :g.Predation>.62f?CrystalForm.Radiolarian:g.Photosynthesis>.62f?CrystalForm.Photocyte
            :g.Speed>1.18f?CrystalForm.Spindle:g.Uptake>1.06f?CrystalForm.Ciliate:CrystalForm.Amoeboid;
        public static void Point(CrystalForm form,ref float x,ref float y,ref float z)
        {
            float length=(float)Math.Sqrt(x*x+y*y+z*z);if(length<.000001f)return;
            float nx=x/length,ny=y/length,nz=z/length,e=nx*nx+nz*nz;
            float angle=(float)Math.Atan2(z,x);
            switch(form) {
                case CrystalForm.Ciliate:
                    x*=1.52f;y*=.86f*(1-.16f*nx);z=z*(1-.16f*nx)+length*.13f*(1-nx*nx)*e;break;
                case CrystalForm.Spindle:
                    x*=1.82f;y*=.67f*(.75f+.25f*(1-nx*nx));z*=.72f*(.75f+.25f*(1-nx*nx));break;
                case CrystalForm.Diatom:
                    x*=2;y*=.52f;z*=.8f*(.65f+.35f*(1-nx*nx));break;
                case CrystalForm.Amoeboid:
                    float lobes=1+.5f*(float)Math.Pow(Math.Max(0,Math.Cos(angle*5)),4)*e*e;
                    x*=lobes;y*=.72f;z*=lobes;break;
                case CrystalForm.Radiolarian:
                    float facets=1+.045f*(float)(Math.Cos(angle*6)*Math.Sin(ny*Math.PI));x*=facets;y*=facets;z*=facets;break;
                case CrystalForm.Photocyte:
                    float outline=1+.025f*(float)Math.Cos(angle*5)*e;x*=outline;y*=1.02f;z*=outline;break;
            }
        }
    }
}
