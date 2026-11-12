using System;
using Evolution.Core;
using Evolution.Presentation;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class StylePresentationTests
    {
        [Test]
        public void BodyHeadingCrossesAngleSeamSmoothlyAndFreezesOnPause()
        {
            float before=3.12f,target=-3.12f;
            float after=VisualMotion.Heading(before,target,1.0/60);
            Assert.That(after-before,Is.InRange(0,.02f));
            Assert.AreEqual(before,VisualMotion.Heading(before,target,0));
            float many=before;
            for(int i=0;i<60;i++) many=VisualMotion.Heading(many,target,1.0/60);
            Assert.That(many,Is.EqualTo(VisualMotion.Heading(before,target,1)).Within(.00001));
        }
        [Test]
        public void TissueDepthStaysBoundedAndSharedBodyDoesNotTearApart()
        {
            for(int id=1;id<100;id++) for(int t=0;t<40;t++) {
                float root=VisualMotion.Depth(id,0,t*.1),child=VisualMotion.Depth(id,1,t*.1);
                Assert.That(Math.Abs(root),Is.LessThanOrEqualTo(.1951));
                Assert.That(Math.Abs(child-root),Is.LessThan(.008));
                Assert.AreEqual(root,VisualMotion.Depth(id,0,t*.1));
            }
        }
        [Test]
        public void SlowBreathingIsIndependentOfFastFlagellarPhase()
        {
            var c=new Cell { Genes=new Genome { Size=1,Speed=2,Uptake=1,PreferredTemperature=22 }, Energy=20,Integrity=1,Growth=1 };
            var state=new CellAnimation(12,AnimationTargets.From(c,new OceanSample { Temperature=22 },0,.05f));
            float first=MembranePose.From(state,3,0).Breath;
            state.Advance(.2);
            Assert.AreEqual(first,MembranePose.From(state,3,100).Breath);
            Assert.That(Math.Abs(first),Is.LessThanOrEqualTo(.0141));
        }
        [Test]
        public void AmbienceLoopsWithoutDiscontinuityOrClippingAndHasStereoWidth()
        {
            bool stereo=false;
            for(int channel=0;channel<2;channel++) {
                Assert.That(OceanSound.Ambience(0,channel),Is.EqualTo(OceanSound.Ambience(OceanSound.LoopSeconds,channel)).Within(.000001));
                double step=1.0/24000;
                double derivativeStart=(OceanSound.Ambience(step,channel)-OceanSound.Ambience(0,channel))/step;
                double derivativeEnd=(OceanSound.Ambience(OceanSound.LoopSeconds+step,channel)-OceanSound.Ambience(OceanSound.LoopSeconds,channel))/step;
                Assert.That(derivativeStart,Is.EqualTo(derivativeEnd).Within(.01));
                for(int sample=0;sample<24000;sample++) {
                    double t=sample*.0005; float wave=OceanSound.Ambience(t,channel);
                    Assert.IsFalse(float.IsNaN(wave)); Assert.That(Math.Abs(wave),Is.LessThan(.08));
                    stereo |= Math.Abs(OceanSound.Ambience(t,0)-OceanSound.Ambience(t,1))>.005;
                }
            }
            Assert.IsTrue(stereo);
        }
        [Test]
        public void InteractionSoundsStartAndEndAtSilenceWithBoundedPeaks()
        {
            Assert.AreEqual(0,OceanSound.Interaction(0,.3,190,-60));
            Assert.AreEqual(0,OceanSound.Interaction(.3,.3,190,-60));
            float peak=0;
            for(int i=0;i<7200;i++) peak=Math.Max(peak,Math.Abs(OceanSound.Interaction(i/24000.0,.3,190,-60)));
            Assert.That(peak,Is.InRange(.01f,.22f));
        }
        [Test]
        public void CautiousThrottleTradesSpeedForLowerRealSwimmingCosts()
        {
            var cfg=new SimulationConfig { InitialPopulation=0,Capacity=8,InitialNutrients=0,NutrientRegeneration=0,Ecosystem=true };
            var c=new Cell { Genes=new Genome { Size=1,Speed=1,Uptake=1,PreferredTemperature=22 },X=4,Z=4,Age=6,Energy=16 };
            var fast=new Simulation(cfg,42,new[] {c}); var slow=new Simulation(cfg,42,new[] {c});
            fast.TryControl(1); slow.TryControl(1); fast.SetControlInput(1,0); slow.SetControlInput(.38f,0);
            float fastCost=0,slowCost=0;
            for(int i=0;i<60;i++) { fast.Step(); slow.Step(); fastCost+=fast.GetCell(0).Budget.Movement; slowCost+=slow.GetCell(0).Budget.Movement; }
            Assert.Less(slow.GetCell(0).SwimSpeed,fast.GetCell(0).SwimSpeed*.5f);
            Assert.Greater(slow.GetCell(0).Energy,fast.GetCell(0).Energy);
            Assert.Less(slowCost,fastCost*.3f);
        }
    }
}
