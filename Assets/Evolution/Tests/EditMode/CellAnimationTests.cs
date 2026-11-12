using System;
using Evolution.Core;
using Evolution.Presentation;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class CellAnimationTests
    {
        static Cell Example(float speed = 1.4f, float uptake = 1.2f, float membrane = .2f) => new Cell {
            Id = 17, Genes = new Genome { Size = 1, Speed = speed, Uptake = uptake, Membrane = membrane, PreferredTemperature = 22 },
            Energy = 20, Integrity = 1, Growth = 1, SwimSpeed = 1
        };
        static OceanSample Water => new OceanSample { Temperature = 22 };
        static CellAnimation State(Cell c) => new CellAnimation(c.Id, AnimationTargets.From(c,Water,c.SwimSpeed,.05f));

        [TestCase(.8f,1.05f,.2f,false,false,SwimmingStyle.Amoeboid)]
        [TestCase(.81f,1,.2f,true,false,SwimmingStyle.Flagellar)]
        [TestCase(.5f,1.06f,.2f,false,true,SwimmingStyle.Ciliary)]
        [TestCase(1.4f,1.2f,.8f,true,true,SwimmingStyle.Armored)]
        public void PhenotypesUseExistingTraits(float speed,float uptake,float membrane,bool flagellum,bool cilia,SwimmingStyle style)
        {
            var t = AnimationTargets.From(Example(speed,uptake,membrane),Water,1,.05f);
            Assert.AreEqual(flagellum,t.Flagellum); Assert.AreEqual(cilia,t.Cilia); Assert.AreEqual(style,t.Style);
        }
        [Test]
        public void ExhaustionColdToxinsSizeAndShellReduceActivity()
        {
            var c = Example(); var normal = AnimationTargets.From(c,Water,1,.05f);
            c.Energy = .01f; var hungry = AnimationTargets.From(c,Water,1,.05f);
            Assert.Less(hungry.Activity,normal.Activity); Assert.Less(hungry.Frequency,normal.Frequency);
            c.Energy = 20; var cold = AnimationTargets.From(c,new OceanSample { Temperature = 0, Toxicity = 1 },1,.05f);
            Assert.Less(cold.Activity,normal.Activity); Assert.Greater(cold.Stress,normal.Stress);
            c.Genes.Size = 2; c.Energy = 40;
            Assert.Less(AnimationTargets.From(c,Water,1,.05f).Frequency,normal.Frequency);
            c = Example(membrane:.9f); Assert.Less(AnimationTargets.From(c,Water,1,.05f).Compliance,normal.Compliance);
        }
        [Test]
        public void StopTurnAndAccelerationBlendWithoutFrameDependence()
        {
            var c = Example(); var one = State(c); var many = State(c);
            c.SwimSpeed = 0; c.AngularVelocity = 1.2f;
            one.Target = many.Target = AnimationTargets.From(c,Water,1,.05f);
            one.Advance(1);
            for(int i=0;i<120;i++) many.Advance(1.0/120);
            Assert.That(many.Drive,Is.EqualTo(one.Drive).Within(.00001));
            Assert.That(many.Turn,Is.EqualTo(one.Turn).Within(.00001));
            Assert.That(many.Phase,Is.EqualTo(one.Phase).Within(.00002));
            Assert.Less(one.Drive,.01); Assert.Greater(one.Turn,0);
            Assert.Less(one.Acceleration,0);
        }
        [Test]
        public void PauseFreezesStateAndCyclesDoNotResetPhase()
        {
            var a = State(Example()); a.Advance(20); double phase = a.Phase;
            Assert.Greater(phase,Math.PI*2); a.Advance(0); Assert.AreEqual(phase,a.Phase);
            a.Synchronize(phase+1,0,0); Assert.AreEqual(phase,a.Phase);
            Assert.That(CellAnimation.Pulse(3,2,1.2),Is.GreaterThan(0));
            Assert.AreEqual(0,CellAnimation.Pulse(2,2,1.2)); Assert.AreEqual(0,CellAnimation.Pulse(4,2,1.2));
        }
        [Test]
        public void ColonyPhaseLocksGraduallyAndFlowBendsAnIdleBody()
        {
            var c=Example(); var a=State(c); double before=a.Phase;
            a.Synchronize(before+1,0,1.0/60);
            Assert.That(a.Phase-before,Is.InRange(.01,.1));
            for(int i=0;i<120;i++) a.Synchronize(before+1,0,1.0/60);
            Assert.That(a.Phase,Is.EqualTo(before+1).Within(.003));
            c.SwimSpeed=0; c.AngularVelocity=0;
            var flow=AnimationTargets.From(c,new OceanSample { Temperature=22,CurrentZ=1 },0,.05f);
            Assert.AreEqual(0,flow.Drive); Assert.Greater(flow.Turn,0);
        }
        [Test]
        public void IntakeIsFiniteAndRequiresActualParticleEnergy()
        {
            var c = Example(); var a = State(c);
            c.FoodLastStep = 5; c.Digestion = 1; c.Budget.Light = 5;
            a.Observe(c,1,.4f); Assert.IsTrue(double.IsNegativeInfinity(a.FeedAt));
            c.Budget.Substrate = .1f; a.Observe(c,2,.4f);
            Assert.AreEqual(2,a.FeedAt); Assert.Greater(a.Feeding(2.6),.9);
            Assert.AreEqual(0,a.Feeding(4)); a.Observe(c,2.05,.4f); Assert.AreEqual(2,a.FeedAt);
            c.Budget = default; a.Observe(c,4,.4f); Assert.AreEqual(2,a.FeedAt);
        }
        [Test]
        public void AttemptedAttackDoesNotImplyFoodOrDivision()
        {
            var c = Example(); var a = State(c); c.Attacking = true; c.Activity = CellActivity.Hunting;
            a.Observe(c,1,0); Assert.IsTrue(double.IsNegativeInfinity(a.AttackAt));
            c.AttackLastStep = 1; c.DamageLastStep = 1; a.Observe(c,2,.7f);
            Assert.Greater(a.Attack(2.2),.9); Assert.Greater(a.Damage(2.3),.9);
            Assert.IsTrue(double.IsNegativeInfinity(a.FeedAt)); Assert.AreEqual(0,a.Division(2.3));
            a.Divide(3); Assert.AreEqual(0,a.Division(3)); Assert.Greater(a.Division(3.55),.99);
            Assert.AreEqual(0,a.Division(4.2));
            Assert.AreEqual(0,CellAnimation.SmoothBirth(0)); Assert.AreEqual(1,CellAnimation.SmoothBirth(.8));
        }
        [Test]
        public void DeformedSurfaceStaysFiniteClosedAndKeepsInteriorOrganelles()
        {
            var a = State(Example(.4f,.8f));
            a.Drive = 1; a.Turn = 1; a.Acceleration = 1; a.Divide(1);
            for(int frame=0;frame<60;frame++) {
                var pose = MembranePose.From(a,1+frame/60.0,frame*.17f);
                for(int j=0;j<128;j++) {
                    float angle = j*(float)Math.PI*2/127;
                    float x = .5f*(float)Math.Cos(angle), y=0, z=.5f*(float)Math.Sin(angle);
                    pose.Deform(ref x,ref y,ref z);
                    float r = (float)Math.Sqrt(x*x+z*z);
                    Assert.That(r,Is.InRange(.24f,.90f)); Assert.IsFalse(float.IsNaN(r));
                    pose.Interior((float)Math.Cos(angle),0,(float)Math.Sin(angle),out float ix,out _,out float iz);
                    // Includes a .08 normalized organelle radius along this ray.
                    Assert.Less(Math.Sqrt(ix*ix+iz*iz)+.08,r);
                }
                float leftX=-.5f,leftY=0,leftZ=0,rightX=-.5f,rightY=0,rightZ=-.00000001f;
                pose.Deform(ref leftX,ref leftY,ref leftZ); pose.Deform(ref rightX,ref rightY,ref rightZ);
                Assert.That(leftX,Is.EqualTo(rightX).Within(.00001)); Assert.That(leftZ,Is.EqualTo(rightZ).Within(.00001));
            }
        }
        [Test]
        public void MotorsHaveAnchoredRootsTravellingWavesAndAsymmetricStrokes()
        {
            MotorCurves.Flagellum(0,0,1,1,0,out float x0,out float z0);
            MotorCurves.Flagellum(0,2,1,1,1,out float x1,out float z1);
            Assert.AreEqual(x0,x1); Assert.AreEqual(z0,z1);
            MotorCurves.Flagellum(1,0,1,1,0,out _,out float active);
            MotorCurves.Flagellum(1,0,0,.1f,0,out _,out float resting);
            Assert.Greater(Math.Abs(active),Math.Abs(resting)*10);
            MotorCurves.Cilium(0,.7f,0,1,1,0,out x0,out z0);
            MotorCurves.Cilium(0,.7f,2,1,1,1,out x1,out z1);
            Assert.AreEqual(x0,x1); Assert.AreEqual(z0,z1);
            MotorCurves.Cilium(1,.7f,0,1,1,0,out x0,out z0);
            MotorCurves.Cilium(1,.7f,2,1,1,0,out x1,out z1);
            Assert.Greater(Math.Abs(x0-x1)+Math.Abs(z0-z1),.01);
        }
        [Test]
        public void ManyVisualUpdatesDoNotChangeBiologyOrRandomSequence()
        {
            var config = new SimulationConfig { InitialPopulation=128,Capacity=512,Ecosystem=true,OceanZones=true };
            var rendered = new Simulation(config,42); var reference = new Simulation(config,42);
            for(int step=0;step<100;step++) {
                rendered.Step(); reference.Step();
                for(int i=0;i<rendered.Count;i++) {
                    var c=rendered.GetCell(i); var before=c; var a=State(c);
                    a.Observe(c,step*.05,0); a.Advance(.1); MembranePose.From(a,step*.05,(float)a.Phase);
                    Assert.AreEqual(before,c);
                }
            }
            Assert.AreEqual(reference.RandomState,rendered.RandomState); Assert.AreEqual(reference.Count,rendered.Count);
            for(int i=0;i<rendered.Count;i++) Assert.AreEqual(reference.GetCell(i),rendered.GetCell(i));
        }
    }
}
