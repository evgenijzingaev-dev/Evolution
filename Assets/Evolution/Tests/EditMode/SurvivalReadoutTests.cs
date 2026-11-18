using System;
using Evolution.Core;
using Evolution.Presentation;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class SurvivalReadoutTests
    {
        static Cell Founder(float x = 1, float predation = 0, float speed = 1) => new Cell {
            Genes = new Genome { Size=1, Speed=speed, Uptake=1, Sensitivity=1, PreferredTemperature=22, Predation=predation },
            X=x, Z=1, Age=6, Energy=12
        };
        static SimulationConfig Config(bool ecosystem = true) => new SimulationConfig {
            InitialPopulation=0, Capacity=64, WorldSize=20, GridSize=20,
            Ecosystem=ecosystem, InitialNutrients=8, NutrientRegeneration=0, MutationProbability=0
        };
        [Test]
        public void SensorsFindThreatAcrossSeamButDoNotRevealDistantOrganisms()
        {
            var s = new Simulation(Config(),42,new[] {Founder(.2f),Founder(19.8f,1),Founder(10,1)});
            var r = s.ReadSurvival(s.GetCell(0));
            Assert.AreEqual(2,r.ThreatId); Assert.That(r.ThreatDistance,Is.EqualTo(.4f).Within(.00001));
            Assert.AreEqual(SurvivalAdvice.Escape,r.Advice); Assert.AreEqual(0,r.PreyId);
            var far = new Simulation(Config(),42,new[] {Founder(.2f),Founder(10,1)});
            Assert.AreEqual(0,far.ReadSurvival(far.GetCell(0)).ThreatId);
        }
        [Test]
        public void HuntingCueUsesRealBiteEligibilityAndDoesNotMarkOwnDescendants()
        {
            var s = new Simulation(Config(),42,new[] {Founder(1,1),Founder(2)});
            Assert.AreEqual(2,s.ReadSurvival(s.GetCell(0)).PreyId);
            var parent = Founder(1,1); parent.Energy=24;
            var line = new Simulation(Config(),42,new[] {parent}); line.TryControl(1);
            Assert.IsTrue(line.RequestDivision()); line.Step();
            Assert.Greater(line.Births,0);
            Assert.AreEqual(0,line.ReadSurvival(line.GetCell(0)).PreyId);
            Assert.AreEqual(0,line.ReadSurvival(line.GetCell(0)).ThreatId);
        }
        [Test]
        public void NearbyHabitatCueRespondsToRealLocalDepletion()
        {
            var cfg=Config(false); cfg.InitialNutrients=1;
            var s = new Simulation(cfg,42,new[] {Founder()}); s.TryControl(1);
            Assert.IsFalse(s.ReadSurvival(s.GetCell(0)).BetterHabitat);
            for (int i=0;i<40;i++) s.Step();
            var c=s.GetCell(0); var r=s.ReadSurvival(c);
            Assert.IsTrue(r.BetterHabitat); Assert.That(r.FoodDistance,Is.InRange(.1f,r.Range));
            s.LocalFood(c,out float here,out _);
            c.X=r.FoodX; c.Z=r.FoodZ; s.LocalFood(c,out float there,out _);
            Assert.Greater(there,here);
        }
        [Test]
        public void MotionBreakdownReconcilesWithoutChargingEnergyTwice()
        {
            var s = new Simulation(Config(),7,new[] {Founder()}); s.TryControl(1);
            s.SetControlInput(0,1); s.SetControlActions(false,true,false);
            for (int i=0;i<20;i++) s.Step();
            var c=s.GetCell(0); var b=c.Budget;
            Assert.Greater(b.Swimming,0); Assert.Greater(b.Turning,0); Assert.Greater(b.Boost,0);
            Assert.That(b.Swimming+b.Turning+b.Boost,Is.EqualTo(b.Movement).Within(.000001));
            Assert.That(c.Energy-b.Start,Is.EqualTo(b.Net).Within(.00001));
        }
        [Test]
        public void UnsupportedDefenseDoesNotBlockBoostOrProduceFalseBracing()
        {
            var s = new Simulation(Config(),7,new[] {Founder()}); s.TryControl(1);
            s.SetControlInput(1,0); s.SetControlActions(false,true,true);
            Assert.AreEqual(ActionBlock.Physiology,s.ActionStatus(CellAction.Defend));
            Assert.AreEqual(ActionBlock.None,s.ActionStatus(CellAction.Boost));
            s.Step(); Assert.IsTrue(s.GetCell(0).Boosting); Assert.IsFalse(s.GetCell(0).Defending);
        }
        [Test]
        public void NegativeColonyFlowIsAnExpenseAndEmptyTickHasNoInventedIncome()
        {
            var s = new Simulation(Config(),7,new[] {Founder()}); var c=s.GetCell(0);
            c.Budget=new EnergyBudget { Transport=-.1f };
            var r=s.ReadSurvival(c); Assert.AreEqual(EnergySource.None,r.Source); Assert.AreEqual(EnergyDrain.Colony,r.Drain);
            c.Budget=new EnergyBudget { Light=.02f, Organic=.1f, Transport=.2f };
            Assert.AreEqual(EnergySource.Colony,s.ReadSurvival(c).Source);
        }
        [Test]
        public void ReadingSensorsRepeatedlyCannotChangeWorldOrEvolution()
        {
            var cfg=Config(); cfg.InitialPopulation=48;
            var observed=new Simulation(cfg,42); var reference=new Simulation(cfg,42);
            for (int tick=0;tick<20;tick++) {
                for (int i=0;i<observed.Count;i++) observed.ReadSurvival(observed.GetCell(i));
                observed.Step(); reference.Step();
            }
            Assert.AreEqual(reference.RandomState,observed.RandomState); Assert.AreEqual(reference.Count,observed.Count);
            for (int i=0;i<reference.Count;i++) Assert.AreEqual(reference.GetCell(i),observed.GetCell(i));
        }
        [Test]
        public void InheritedSilhouettesDifferAndRemainClosedAroundOrganelles()
        {
            var swift=Founder(speed:3).Genes; var photo=Founder(speed:.2f).Genes; photo.Photosynthesis=1;
            var a=default(MembranePose).WithGenome(swift); var b=default(MembranePose).WithGenome(photo);
            Assert.Greater(a.Axial,b.Axial); Assert.Less(a.Breadth,b.Breadth); Assert.Less(a.Flatness,b.Flatness);
            var rng=new RandomSource(42);
            for (int n=0;n<80;n++) {
                var g=Genome.EcologicalFounder(Genome.Founder(ref rng),rng.State);
                var pose=new MembranePose { Stretch=.35f, Pinch=1, Turn=1, Amplitude=.12f, Phase=n }.WithGenome(g);
                for (int j=0;j<64;j++) {
                    float angle=j*(float)Math.PI*2/63, x=.5f*(float)Math.Cos(angle), z=.5f*(float)Math.Sin(angle), y=0;
                    pose.Deform(ref x,ref y,ref z); float radius=(float)Math.Sqrt(x*x+z*z);
                    Assert.That(radius,Is.InRange(.2f,1.6f));
                    pose.Interior((float)Math.Cos(angle),0,(float)Math.Sin(angle),out float ix,out _,out float iz);
                    Assert.Less(Math.Sqrt(ix*ix+iz*iz)+.04,radius);
                }
            }
        }
        [Test]
        public void RepairStateReachesTheMembranePoseUsedByTheRenderer()
        {
            var c=Founder(); c.RepairLastStep=.01f;
            var a=new CellAnimation(1,AnimationTargets.From(c,new OceanSample { Temperature=22 },0,.05f));
            Assert.That(MembranePose.From(a,0,0).Repair,Is.EqualTo(1));
        }
        [Test]
        public void ReversingInputCannotInstantlyReverseRotationalMomentum()
        {
            var c=Founder(); c.TargetHeading=1.5f;
            for (int i=0;i<6;i++) CellMotion.Integrate(ref c,1,0,0,.05f);
            Assert.Greater(c.AngularVelocity,1);
            float before=c.AngularVelocity;
            c.TargetHeading=c.Heading-1.5f;
            CellMotion.Integrate(ref c,1,0,0,.05f);
            Assert.Greater(c.AngularVelocity,0); Assert.Less(c.AngularVelocity,before);
            for (int i=0;i<80;i++) CellMotion.Integrate(ref c,1,0,0,.05f);
            Assert.That(Math.Abs(CellMotion.AngleDelta(c.TargetHeading-c.Heading)),Is.LessThan(.1f));
        }
    }
}
