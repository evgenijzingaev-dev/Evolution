using System;

namespace Evolution.Volume
{
    public struct V3
    {
        public float X,Y,Z;
        public V3(float x,float y,float z) { X=x;Y=y;Z=z; }
        public float Sqr => X*X+Y*Y+Z*Z;
        public float Length => (float)Math.Sqrt(Sqr);
        public V3 Unit => Length>.000001f ? this/Length : new V3(1,0,0);
        public static V3 operator +(V3 a,V3 b)=>new V3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static V3 operator -(V3 a,V3 b)=>new V3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static V3 operator -(V3 a)=>a*-1;
        public static V3 operator *(V3 a,float b)=>new V3(a.X*b,a.Y*b,a.Z*b);
        public static V3 operator /(V3 a,float b)=>a*(1/b);
        public static float Dot(V3 a,V3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public static V3 Cross(V3 a,V3 b)=>new V3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        public static V3 Lerp(V3 a,V3 b,float t)=>a+(b-a)*t;
        public static V3 Turn(V3 a,V3 b,float maxAngle)
        {
            a=a.Unit;b=b.Unit; float angle=(float)Math.Acos(Math.Max(-1,Math.Min(1,Dot(a,b))));
            if(angle<=maxAngle) return b;
            var axis=Cross(a,b);
            if(axis.Sqr<.000001f) axis=Cross(a,Math.Abs(a.Y)<.9f?new V3(0,1,0):new V3(1,0,0));
            axis=axis.Unit;
            return (a*(float)Math.Cos(maxAngle)+Cross(axis,a)*(float)Math.Sin(maxAngle)).Unit;
        }
    }
    public struct FlowBody { public V3 Position,Direction; public float Radius,Speed; }
    public static class WaterField
    {
        // Each velocity component is independent of its own axis: zero analytic divergence.
        public static V3 Current(V3 p,double time)=>new V3(
            (float)(.16*Math.Sin(p.Y*.18+time*.08)+.07*Math.Cos(p.Z*.13)),
            (float)(.12*Math.Sin(p.Z*.17-time*.06)+.05*Math.Cos(p.X*.11)),
            (float)(.14*Math.Sin(p.X*.14+time*.07)+.05*Math.Cos(p.Y*.16)));
        public static float Temperature(V3 p,double time)=>(float)(22+7*Math.Sin(p.X*.15+time*.02)+6*Math.Sin(p.Y*.18+p.Z*.1));
        public static float Light(V3 p)=>Math.Max(.06f,Math.Min(1,.5f+p.Y*.025f));
        public static V3 Relax(V3 velocity,V3 desired,float size,float dt)=>V3.Lerp(velocity,desired,1-(float)Math.Exp(-dt/(.13+.07*size)));
        // Bounded force-free dipole approximation, not turbulence or a Navier-Stokes solver.
        public static V3 Disturbance(V3 point,FlowBody body)
        {
            var r=point-body.Position; float d2=r.Sqr,radius=body.Radius;
            if(d2<.0001f || d2>36*radius*radius) return default;
            var n=r/(float)Math.Sqrt(d2); float alignment=V3.Dot(n,body.Direction);
            return n*((3*alignment*alignment-1)*.18f*body.Speed*radius*radius/(d2+radius*radius));
        }
    }
    public sealed class Suspension
    {
        readonly V3[] points,previous;
        public int Count=>points.Length;
        public V3 Get(int i,float t=1)=>V3.Lerp(previous[i],points[i],t);
        public Suspension(int count,uint seed)
        {
            if(count<1||count>8192) throw new ArgumentOutOfRangeException(nameof(count));
            points=new V3[count];previous=new V3[count];var rng=new Evolution.Core.RandomSource(seed);
            for(int i=0;i<count;i++) { float half=i<count/3?12:32; points[i]=new V3(rng.Range(-half,half),rng.Range(-half,half),rng.Range(-half,half)); previous[i]=points[i]; }
        }
        public void Step(float dt,double time,V3 camera,FlowBody[] bodies,int count,V3 environmentalOffset=default)
        {
            for(int i=0;i<points.Length;i++) {
                var p=points[i]; float half=i<points.Length/3?12:32;
                // Recycle only beyond a faded outer shell, preserving world-space parallax nearby.
                var d=p-camera; bool recycled=false;
                if(Math.Abs(d.X)>half) { p.X=camera.X+Wrap(d.X,half);recycled=true; }
                if(Math.Abs(d.Y)>half) { p.Y=camera.Y+Wrap(d.Y,half);recycled=true; }
                if(Math.Abs(d.Z)>half) { p.Z=camera.Z+Wrap(d.Z,half);recycled=true; }
                previous[i]=p;
                var velocity=WaterField.Current(p+environmentalOffset,time);
                for(int b=0;b<count;b++) velocity+=WaterField.Disturbance(p,bodies[b]);
                p+=velocity*dt;
                for(int b=0;b<count;b++) {
                    var delta=p-bodies[b].Position;float radius=bodies[b].Radius*.8f;
                    if(delta.Sqr<radius*radius) p=bodies[b].Position+delta.Unit*radius;
                }
                points[i]=p;if(recycled) previous[i]=p;
            }
        }
        static float Wrap(float d,float half)=>d-(float)Math.Floor((d+half)/(2*half))*2*half;
    }
}
