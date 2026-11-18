using Evolution.Core;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class EvolutionJourneyTests
    {
        static Genome Genes() => new Genome { Size = 1, Speed = .1f, Uptake = 3, PreferredTemperature = 22 };
        static Cell Founder(float energy = 12, float age = 0, float x = 4) => new Cell { Genes = Genes(), Energy = energy, Age = age, X = x, Z = 4 };
        static SimulationConfig Config() => new SimulationConfig { InitialPopulation = 0, GridSize = 2, WorldSize = 10, Capacity = 8,
            InitialNutrients = 8, NutrientRegeneration = 10, MutationProbability = 0 };
        static void Step(Simulation s, EvolutionJourney j) { int id = s.ControlledId; s.Step(); j.RecordStep(s,id); }
        [Test] public void ProgressRequiresCommittedControlledTicksAndActualAssimilation()
        {
            var s = new Simulation(Config(),42,new[] { Founder() }); var j = new EvolutionJourney();
            s.TryControl(1); j.Begin(s,1); j.RecordStep(s,1); Assert.AreEqual(0,j.SurvivalSeconds);
            Step(s,j); float survival = j.SurvivalSeconds, food = j.FoodEnergy;
            Assert.Greater(food,0); Assert.That(survival,Is.EqualTo(s.Config.StepSeconds));
            j.RecordStep(s,1); Assert.AreEqual(survival,j.SurvivalSeconds); Assert.AreEqual(food,j.FoodEnergy);
            s.ReleaseControl(); Step(s,j); Assert.AreEqual(survival,j.SurvivalSeconds); Assert.AreEqual(food,j.FoodEnergy);
        }
        [Test] public void DivisionAndSuccessionAreSeparateGoalsAndUseRealLineage()
        {
            var s = new Simulation(Config(),42,new[] { Founder(24,5),Founder(x:8) }); var j = new EvolutionJourney();
            s.TryControl(1); j.Begin(s,1); s.RequestDivision(); Step(s,j);
            Assert.IsTrue(j.Completed(EvolutionGoal.Reproduce)); Assert.IsFalse(j.Completed(EvolutionGoal.Inherit));
            int child = s.LastOffspringId; Assert.IsTrue(j.Contains(child));
            s.TryControl(child); j.Begin(s,child); Assert.IsTrue(j.Completed(EvolutionGoal.Inherit)); Assert.AreEqual(1,j.FollowedGenerations);
            int achievements = j.AchievementCount; s.TryControl(2); j.Begin(s,2);
            Assert.AreEqual(2,j.RootId); Assert.AreEqual(0,j.CompletedCount); Assert.AreEqual(achievements,j.AchievementCount);
        }
        [Test] public void FoodAndSurvivalGoalsCanBeEarnedWithoutEnergyRewards()
        {
            var cfg = Config(); cfg.StepSeconds = 1;
            var s = new Simulation(cfg,42,new[] { Founder() }); var baseline = new Simulation(cfg,42,new[] { Founder() });
            var j = new EvolutionJourney(); s.TryControl(1); baseline.TryControl(1); j.Begin(s,1);
            for (int i = 0; i < 31; i++) { Step(s,j); baseline.Step(); }
            Assert.IsTrue(j.Completed(EvolutionGoal.Feed)); Assert.IsTrue(j.Completed(EvolutionGoal.Survive)); Assert.IsTrue(j.Completed(EvolutionGoal.Reserve));
            Assert.AreEqual(baseline.GetCell(0),s.GetCell(0)); Assert.AreEqual(baseline.RandomState,s.RandomState);
        }
        [Test] public void AdaptationRequiresSustainedReservesInChangedConditions()
        {
            var cfg = Config(); cfg.StepSeconds = 1;
            var s = new Simulation(cfg,42,new[] { Founder() }); var j = new EvolutionJourney(); s.TryControl(1); j.Begin(s,1);
            for (int i = 0; i < 16; i++) Step(s,j); Assert.AreEqual(0,j.AdaptationSeconds);
            s.SetEnvironment(28,10); for (int i = 0; i < 15; i++) Step(s,j);
            Assert.IsTrue(j.Completed(EvolutionGoal.Adapt));
        }
        [Test] public void ColonyGoalRequiresStableSpecializedOrganism()
        {
            var cfg = Config(); cfg.Capacity = cfg.MaxColonyCells = 4; cfg.StepSeconds = .5f;
            var f = Founder(24,5); f.Genes.Adhesion = f.Genes.ColonySize = f.Genes.Branching = f.Genes.Differentiation = f.Genes.Transport = 1;
            var s = new Simulation(cfg,42,new[] { f });
            for (int i = 0; i < 60; i++) s.Step();
            Assert.AreEqual(4,s.LargestColony);
            var j = new EvolutionJourney(); s.TryControl(1); j.Begin(s,1);
            for (int i = 0; i < 45; i++) Step(s,j);
            Assert.IsTrue(j.Completed(EvolutionGoal.Colony));
        }
        [Test] public void ExtinctionAndUnrelatedDeathsCannotLeavePhantomHeirs()
        {
            var cfg = Config(); cfg.MaxAge = 1; cfg.InitialNutrients = cfg.NutrientRegeneration = 0;
            var s = new Simulation(cfg,42,new[] { Founder(age:.99f),Founder() }); var j = new EvolutionJourney();
            s.TryControl(1); j.Begin(s,1); Step(s,j);
            Assert.AreEqual(0,j.LivingDescendants); Assert.IsFalse(j.Contains(2)); Assert.AreEqual(0,j.SurvivalSeconds);
        }
    }
}
