using System;
using Evolution.Presentation;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class CrystalFormTests
    {
        [TestCase(CrystalForm.Ciliate)] [TestCase(CrystalForm.Spindle)] [TestCase(CrystalForm.Amoeboid)]
        [TestCase(CrystalForm.Diatom)] [TestCase(CrystalForm.Radiolarian)] [TestCase(CrystalForm.Photocyte)]
        public void SurfaceIsClosedOutwardBoundedAndConsistentWithItsInterior(CrystalForm form)
        {
            for(int lat=1;lat<24;lat++)for(int lon=0;lon<=96;lon++) {
                float a=lon*(float)Math.PI*2/96,b=lat*(float)Math.PI/24;
                float nx=(float)(Math.Sin(b)*Math.Cos(a)),ny=(float)Math.Cos(b),nz=(float)(Math.Sin(b)*Math.Sin(a));
                float x=nx*.5f,y=ny*.5f,z=nz*.5f;CrystalForms.Point(form,ref x,ref y,ref z);
                float radius=(float)Math.Sqrt(x*x+y*y+z*z);Assert.That(radius,Is.InRange(.12f,CrystalForms.BoundingRadius));
                float ix=nx*.2f,iy=ny*.2f,iz=nz*.2f;CrystalForms.Point(form,ref ix,ref iy,ref iz);
                Assert.That(ix,Is.EqualTo(x*.4f).Within(.000001));Assert.That(iy,Is.EqualTo(y*.4f).Within(.000001));Assert.That(iz,Is.EqualTo(z*.4f).Within(.000001));
                // Differentiate the mapped surface, checking the same basis used by the production mesh builder.
                float tx=-(float)Math.Sin(a),tz=(float)Math.Cos(a);
                float bx=ny*tz,by=nz*tx-nx*tz,bz=-ny*tx;
                float px=nx+tx*.002f,py=ny,pz=nz+tz*.002f;Normalize(ref px,ref py,ref pz);CrystalForms.Point(form,ref px,ref py,ref pz);
                float qx=nx+bx*.002f,qy=ny+by*.002f,qz=nz+bz*.002f;Normalize(ref qx,ref qy,ref qz);CrystalForms.Point(form,ref qx,ref qy,ref qz);
                px-=x;py-=y;pz-=z;qx-=x;qy-=y;qz-=z;
                float outward=(py*qz-pz*qy)*x+(pz*qx-px*qz)*y+(px*qy-py*qx)*z;
                Assert.Greater(outward,0,"Surface normal reversed");
            }
            float lx=-.5f,ly=0,lz=.00000001f,rx=-.5f,ry=0,rz=-.00000001f;
            CrystalForms.Point(form,ref lx,ref ly,ref lz);CrystalForms.Point(form,ref rx,ref ry,ref rz);
            Assert.That(lx,Is.EqualTo(rx).Within(.000001));Assert.That(lz,Is.EqualTo(rz).Within(.000001));
        }
        static void Normalize(ref float x,ref float y,ref float z) {float scale=.5f/(float)Math.Sqrt(x*x+y*y+z*z);x*=scale;y*=scale;z*=scale;}
    }
}
