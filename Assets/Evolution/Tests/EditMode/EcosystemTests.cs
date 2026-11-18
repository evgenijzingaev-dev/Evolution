using System;
using Evolution.Core;
using NUnit.Framework;
namespace Evolution.Tests
{
    public sealed class EcosystemTests
    {
        static Genome Genes() => new Genome { Size = 1, Speed = .7f, Uptake = 1, PreferredTemperature = 22 };
        static Cell Founder(Genome genes, float x = 5, float z = 5) => new Cell {
            Genes = genes, Energy = Biology.Capacity(genes) * .6f, X = x, Z = z, Integrity = 1 };
        static SimulationConfig Config() => new SimulationConfig { Ecosystem = true, Capacity = 64, InitialPopulation = 0,
            GridSize = 8, WorldSize = 20, MutationProbability = 0 };
        static Cell Hunter(int id = 1)
        {
            var c = Founder(Genes()); c.Id = c.LineageId = id; c.Genes.Predation = 1; c.Genes.Size = 1.6f; return c;
        }
        [Test] public void PhotosynthesisNeedsLightAndLocalNutrientsAndPaysForItsStructures()
        {
            var g = Genes(); g.Photosynthesis = 1;
            var bright = Founder(g); var dark = bright; var empty = bright;
            float a = 2, b = 2, zero = 0;
            Assert.Greater(Ecology.Photosynthesize(ref bright, ref a, new OceanSample { Light = 1, Temperature = 22 }, 1), 0);
            Assert.AreEqual(0, Ecology.Photosynthesize(ref dark, ref b, new OceanSample { Light = 0, Temperature = 22 }, 1));
            Assert.AreEqual(0, Ecology.Photosynthesize(ref empty, ref zero, new OceanSample { Light = 1, Temperature = 22 }, 1));
            Assert.Less(a, 2); Assert.Greater(bright.Energy, dark.Energy);
            Assert.Greater(Biology.StructureEnergy(g), Biology.StructureEnergy(Genes()));
            Assert.Greater(Biology.Cost(g, 22), Biology.Cost(Genes(), 22));
        }
        [Test] public void ShellAndColonyDefenseReduceBiteDamageAndFastPreyCanEscapeMoreEasily()
        {
            var hunter = Hunter(); var plain = Founder(Genes()); plain.Id = plain.LineageId = 2;
            var shell = plain; shell.Genes.Membrane = 1;
            var colony = plain; var fast = plain; fast.Genes.Speed = 3;
            var h1 = hunter; var h2 = hunter; var h3 = hunter; var h4 = hunter;
            Ecology.Bite(ref h1, ref plain, 0, .1f, out _);
            Ecology.Bite(ref h2, ref shell, 0, .1f, out _);
            Ecology.Bite(ref h3, ref colony, .8f, .1f, out _);
            Ecology.Bite(ref h4, ref fast, 0, .1f, out _);
            Assert.Less(plain.Integrity, 1); Assert.Greater(shell.Integrity, plain.Integrity);
            Assert.Greater(colony.Integrity, plain.Integrity); Assert.Greater(fast.Integrity, plain.Integrity);
            Assert.Less(Ecology.Mobility(shell.Genes), Ecology.Mobility(plain.Genes));
            Assert.Greater(Biology.Cost(fast.Genes, 22), Biology.Cost(plain.Genes, 22));
        }
        [Test] public void TwoPredatorsCannotEatTheSameBiomassTwiceAndBitesConserveEnergy()
        {
            var h1 = Hunter(); var h2 = Hunter(3); var prey = Founder(Genes()); prey.Id = prey.LineageId = 2; prey.Integrity = .001f;
            double before = h1.Energy + Biology.StructureEnergy(h1.Genes) + prey.Energy + Biology.StructureEnergy(prey.Genes);
            Assert.IsTrue(Ecology.Bite(ref h1, ref prey, 0, .05f, out float remains));
            Assert.IsTrue(prey.Consumed); float secondBefore = h2.Energy;
            Assert.IsFalse(Ecology.Bite(ref h2, ref prey, 0, .05f, out float duplicate));
            Assert.AreEqual(0, duplicate); Assert.AreEqual(secondBefore, h2.Energy);
            Assert.Less(h1.Energy + Biology.StructureEnergy(h1.Genes) + remains, before);
            Assert.That(h1.Energy, Is.LessThanOrEqualTo(Biology.Capacity(h1.Genes)));
        }
        [Test] public void LargerArmoredPreyCannotBeAutomaticallyDefeatedAndKinAreNotHunted()
        {
            var hunter = Hunter(); hunter.Genes.Size = .4f; var prey = Founder(Genes()); prey.Genes.Size = 2;
            prey.Id = prey.LineageId = 2; prey.Genes.Membrane = 1;
            Assert.IsFalse(Ecology.CanHunt(hunter, prey));
            Assert.IsFalse(Ecology.Bite(ref hunter, ref prey, 1, 1, out _)); Assert.AreEqual(1, prey.Integrity);
            hunter.Genes.Size = 2; prey.LineageId = hunter.LineageId;
            Assert.IsFalse(Ecology.CanHunt(hunter, prey));
        }
        [Test] public void PredatorChasesAndVictimFleesAcrossPeriodicBoundary()
        {
            var cfg = Config(); cfg.WorldSize = 10; cfg.InitialNutrients = cfg.NutrientRegeneration = 0;
            var h = Hunter(); h.Genes.Speed = 1.4f; h.Genes.Sensitivity = 1;
            var prey = Founder(Genes(), .2f, 5); prey.Genes.Sensitivity = 1;
            h.X = 9; h.Z = 5;
            var s = new Simulation(cfg, 4, new[] {h, prey});
            for (int i = 0; i < 4; i++) s.Step();
            var a = s.GetCell(0); var b = s.GetCell(1);
            Assert.AreEqual(CellActivity.Hunting, a.Activity); Assert.AreEqual(b.Id, a.TargetId);
            Assert.AreEqual(CellActivity.Fleeing, b.Activity); Assert.AreEqual(a.Id, b.TargetId);
            Assert.Greater(Math.Cos(a.Heading), .7); Assert.Greater(Math.Cos(b.Heading), .7);
        }
        [Test] public void ForagingLeavesAHotToxicPatchWithoutPlayerCommands()
        {
            var cfg = Config(); cfg.OceanZones = true;
            var c = Founder(Genes(), 5, 0); c.Genes.Sensitivity = 1; c.Genes.Photosynthesis = 1;
            var s = new Simulation(cfg, 42, new[] {c});
            var initial = s.EnvironmentAt(c.X,c.Z);
            for (int i = 0; i < 120; i++) s.Step();
            var moved = s.GetCell(0); var local = s.EnvironmentAt(moved.X,moved.Z);
            Assert.Greater(Observation.PeriodicDelta(moved.Z - c.Z,cfg.WorldSize), .2f);
            Assert.Less(local.Toxicity, initial.Toxicity);
        }
        [Test] public void TemperatureAndToxinAdaptationsHaveBenefitsAndMaintenanceCosts()
        {
            var plain = Founder(Genes()); var adapted = plain;
            adapted.Genes.ThermalTolerance = adapted.Genes.ToxinResistance = 1;
            Assert.Greater(Biology.Efficiency(adapted.Genes, 40), Biology.Efficiency(plain.Genes, 40));
            Assert.Less(Ecology.Stress(adapted, new OceanSample { Toxicity = 1 }), Ecology.Stress(plain, new OceanSample { Toxicity = 1 }));
            Assert.Greater(Biology.Cost(adapted.Genes,22), Biology.Cost(plain.Genes,22));
        }
        [Test] public void AllNewGenesAreInheritedMutatedBoundedAndReported()
        {
            var rng = new RandomSource(18); var g = Genome.EcologicalFounder(Genes(), 77);
            Assert.AreEqual(g, g.Inherit(ref rng,0,1));
            var child = g.Inherit(ref rng, 1, .2f); int mask = Observation.MutationMask(g,child);
            Assert.AreEqual(0x3f800, mask & 0x3f800);
            for (int i = 0; i < 50; i++) {
                child = child.Inherit(ref rng,1,.3f);
                foreach (float value in new[] { child.Photosynthesis, child.Predation, child.Membrane, child.Sensitivity,
                    child.ThermalTolerance, child.ToxinResistance, child.EnergyEfficiency }) Assert.That(value, Is.InRange(0,1));
            }
            var cfg = Config(); var invalid = Founder(g); invalid.Genes.ToxinResistance = float.NaN;
            Assert.Throws<ArgumentException>(() => new Simulation(cfg,1,new[] {invalid}));
        }
        [Test] public void DecompositionReturnsOnlyPartOfDeadBiomassToLocalSubstrate()
        {
            var cfg = Config(); cfg.InitialNutrients = cfg.NutrientRegeneration = 0; cfg.MaxAge = .05f; cfg.OrganicDecayRate = .5f;
            var s = new Simulation(cfg,42,new[] {Founder(Genes())}); s.Step();
            double before = Stored(s); Assert.AreEqual(0,s.Count); Assert.Greater(before,0);
            s.Step(); double substrate = 0;
            for (int i = 0; i < 64; i++) substrate += s.GetNutrient(i);
            Assert.Greater(substrate,0); Assert.Less(Stored(s),before);
            Assert.That(substrate * 2, Is.EqualTo(before * cfg.OrganicDecayRate * cfg.StepSeconds * .25).Within(.00001));
        }
        [Test] public void ClosedEcosystemWithHuntingRecyclingAndBirthsDoesNotCreateEnergy()
        {
            var cfg = Config(); cfg.WorldSize = 6; cfg.GridSize = 2; cfg.InitialNutrients = 8;
            cfg.InitialOrganicEnergy = 4; cfg.NutrientRegeneration = 0; cfg.MaxAge = 12;
            var hunter = Hunter(); hunter.X = hunter.Z = 1;
            var prey = Founder(Genes(),1.4f,1); prey.Genes.Uptake = 3; prey.Age = 5; prey.Energy = Biology.Capacity(prey.Genes);
            var s = new Simulation(cfg,42,new[] {hunter,prey}); double previous = Stored(s);
            for (int i = 0; i < 300; i++) {
                s.Step(); double stored = Stored(s);
                Assert.That(stored,Is.LessThanOrEqualTo(previous + .0001), $"tick {s.Tick}"); previous = stored;
            }
            Assert.AreEqual(0,s.SunlightEnergy); Assert.Greater(s.Deaths,0); Assert.Greater(s.Births,0);
        }
        [Test] public void ZonesProvideFiveDistinctPeriodicHabitatsAndChangingCurrents()
        {
            var cfg = Config(); cfg.OceanZones = true; var s = new Simulation(cfg,1);
            int zones = 0; float minLight = 1, maxLight = 0;
            for (int x = 0; x < 20; x++) for (int z = 0; z < 20; z++) {
                var a = s.EnvironmentAt(x,z); var b = s.EnvironmentAt(x + 20,z - 20);
                zones |= 1 << (int)a.Zone; minLight = Math.Min(minLight,a.Light); maxLight = Math.Max(maxLight,a.Light);
                Assert.That(b.Temperature,Is.EqualTo(a.Temperature).Within(.00001));
                Assert.That(b.Toxicity,Is.EqualTo(a.Toxicity).Within(.00001));
            }
            Assert.AreEqual(31,zones); Assert.Greater(maxLight - minLight,.8f);
        }
        [Test] public void NaturalEventsAreTemporaryAffectEnvironmentAndReplayWithoutCommands()
        {
            var cfg = Config(); cfg.NaturalEvents = true; cfg.OceanZones = true; cfg.StepSeconds = .5f;
            var a = new Simulation(cfg,12); var b = new Simulation(cfg,12); int events = 0; bool changed = false, ended = false;
            NaturalEvent previous = NaturalEvent.None;
            for (int i = 0; i < 500; i++) {
                a.Step(); b.Step(); Assert.AreEqual(a.ActiveEvent,b.ActiveEvent);
                var local = a.EnvironmentAt(4,7); Assert.AreEqual(local,b.EnvironmentAt(4,7));
                if (a.ActiveEvent != NaturalEvent.None) {
                    events |= 1 << (int)a.ActiveEvent;
                    var baseline = OceanEnvironment.Sample(4,7,20,a.Tick * cfg.StepSeconds,22,true);
                    changed |= Math.Abs(local.Fertility - baseline.Fertility) > .8f || Math.Abs(local.Temperature - baseline.Temperature) > 2 || local.Toxicity > baseline.Toxicity + .1f;
                }
                if (previous != NaturalEvent.None && a.ActiveEvent == NaturalEvent.None) ended = true;
                previous = a.ActiveEvent;
            }
            Assert.Greater(events,0); Assert.IsTrue(changed); Assert.IsTrue(ended);
            Assert.AreEqual(a.Journal.Count,b.Journal.Count);
            for (int i = 0; i < a.Journal.Count; i++) Assert.AreEqual(a.Journal.GetRecent(i),b.Journal.GetRecent(i));
        }
        [Test] public void JournalTracksRealLineageExtinctionAndNeverRespawnsCells()
        {
            var cfg = Config(); cfg.MaxAge = .05f; cfg.StepSeconds = .05f;
            var s = new Simulation(cfg,42,new[] {Founder(Genes())});
            for (int i = 0; i < 101; i++) s.Step();
            Assert.AreEqual(0,s.Count); Assert.AreEqual(0,s.LivingLineages); bool extinct = false;
            for (int i = 0; i < s.Journal.Count; i++) extinct |= s.Journal.GetRecent(i).Kind == EvolutionRecordKind.LineageExtinct;
            Assert.IsTrue(extinct); Assert.AreEqual(0,s.Births);
        }
        [Test] public void FullEcosystemReplayIncludesCellsPredationResourcesAndJournal()
        {
            var cfg = Config(); cfg.OceanZones = cfg.NaturalEvents = true; cfg.InitialPopulation = 24;
            cfg.StepSeconds = .25f; cfg.MutationProbability = .4f;
            var a = new Simulation(cfg,42); var b = new Simulation(cfg,42);
            for (int i = 0; i < 300; i++) { a.Step(); b.Step(); }
            Assert.AreEqual(a.RandomState,b.RandomState); Assert.AreEqual(a.Count,b.Count);
            Assert.AreEqual(a.PredationDeaths,b.PredationDeaths); Assert.AreEqual(a.SunlightEnergy,b.SunlightEnergy);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a.GetCell(i),b.GetCell(i));
            for (int i = 0; i < 64; i++) {
                Assert.AreEqual(a.GetNutrient(i),b.GetNutrient(i)); Assert.AreEqual(a.GetOrganicEnergy(i),b.GetOrganicEnergy(i));
            }
            Assert.AreEqual(a.Journal.Count,b.Journal.Count);
            for (int i = 0; i < a.Journal.Count; i++) Assert.AreEqual(a.Journal.GetRecent(i),b.Journal.GetRecent(i));
        }
        [TestCase(7u)] [TestCase(42u)] [TestCase(123u)]
        public void ShortDefaultOceanHasGenerationsAndAvoidsImmediateExtinction(uint seed)
        {
            var cfg = new SimulationConfig { Ecosystem = true, OceanZones = true, NaturalEvents = true, StepSeconds = .25f, Capacity = 512 };
            var s = new Simulation(cfg,seed); int minimum = s.Count;
            for (int i = 0; i < 480; i++) {
                s.Step(); minimum = Math.Min(minimum,s.Count);
                for (int n = 0; n < s.Count; n++) {
                    var c = s.GetCell(n); Assert.That(c.Energy,Is.InRange(.000001f,Biology.Capacity(c.Genes)));
                    Assert.That(c.Integrity,Is.InRange(0,1)); Assert.That(c.X,Is.InRange(0,cfg.WorldSize)); Assert.That(c.Z,Is.InRange(0,cfg.WorldSize));
                }
            }
            TestContext.WriteLine($"seed={seed}: cells={s.Count}, min={minimum}, births={s.Births}, deaths={s.Deaths}, predation={s.PredationDeaths}, lineages={s.LivingLineages}");
            Assert.Greater(minimum,0); Assert.Greater(s.Births,0); Assert.Greater(s.Deaths,0); Assert.Greater(s.SunlightEnergy,0);
        }
        [Test] public void RealWorldPredationKillsAndReturnsUneatenRemainsExactlyOnce()
        {
            var cfg = Config(); cfg.WorldSize = 1; cfg.GridSize = 2; cfg.InitialNutrients = cfg.NutrientRegeneration = 0;
            var h = Hunter(); h.Genes.Speed = .1f; h.X = h.Z = .3f;
            var p = Founder(Genes(), .4f, .4f); p.Genes.Speed = .1f;
            var s = new Simulation(cfg,3,new[] {h,p});
            double previous = Stored(s);
            for (int i = 0; i < 240 && s.PredationDeaths == 0; i++) {
                s.Step(); Assert.That(Stored(s),Is.LessThanOrEqualTo(previous + .0001)); previous = Stored(s);
            }
            Assert.AreEqual(1,s.PredationDeaths); Assert.AreEqual(1,s.Deaths); Assert.AreEqual(1,s.Count);
            Assert.AreEqual(1,s.GetCell(0).LineageId);
            s.Step(); Assert.AreEqual(1,s.PredationDeaths); Assert.AreEqual(1,s.Deaths);
        }
        [Test] public void PredatorAndScarcityGoalsObserveRealTicksWithoutChangingBiology()
        {
            var cfg = Config(); cfg.StepSeconds = .5f; cfg.InitialNutrients = cfg.NutrientRegeneration = 0;
            var p = Founder(Genes()); p.Genes.Speed = .1f; p.Genes.Uptake = .2f; p.Energy = 20;
            var h = Hunter(); h.X = h.Z = 16; h.Genes.Speed = .1f; h.Genes.Uptake = .2f;
            var s = new Simulation(cfg,42,new[] {p,h}); var baseline = new Simulation(cfg,42,new[] {p,h});
            var journey = new EvolutionJourney(); s.TryControl(1); baseline.TryControl(1); journey.Begin(s,1);
            journey.RecordStep(s,1); Assert.AreEqual(0,journey.ScarcitySeconds);
            for (int i = 0; i < 60; i++) { s.Step(); baseline.Step(); journey.RecordStep(s,1); }
            Assert.IsTrue(journey.Completed(EvolutionGoal.Scarcity)); Assert.IsTrue(journey.Completed(EvolutionGoal.Predators));
            Assert.AreEqual(baseline.RandomState,s.RandomState);
            for (int i = 0; i < s.Count; i++) Assert.AreEqual(baseline.GetCell(i),s.GetCell(i));
            float elapsed = journey.SurvivalSeconds; s.ReleaseControl(); s.Step(); journey.RecordStep(s,0);
            Assert.AreEqual(elapsed,journey.SurvivalSeconds);
        }
        [Test] public void DevelopedShellColonyEarnsProtectionGoalAndTransportsEnergy()
        {
            var cfg = Config(); cfg.StepSeconds = .5f; cfg.Capacity = cfg.MaxColonyCells = 4;
            cfg.InitialNutrients = 8; cfg.NutrientRegeneration = 10;
            var f = Founder(Genes()); f.Energy = 24; f.Age = 5; f.Genes.Speed = .1f; f.Genes.Uptake = 3;
            f.Genes.Membrane = .8f; f.Genes.Adhesion = f.Genes.ColonySize = f.Genes.Branching = f.Genes.Differentiation = f.Genes.Transport = 1;
            var s = new Simulation(cfg,42,new[] {f});
            float transferred = 0;
            for (int i = 0; i < 60; i++) { s.Step(); transferred += s.EnergyTransferred; }
            Assert.AreEqual(4,s.LargestColony);
            var j = new EvolutionJourney(); s.TryControl(1); j.Begin(s,1);
            for (int i = 0; i < 45; i++) { s.Step(); j.RecordStep(s,1); transferred += s.EnergyTransferred; }
            Assert.IsTrue(j.Completed(EvolutionGoal.ProtectedColony)); Assert.IsTrue(j.Completed(EvolutionGoal.Colony));
            Assert.Greater(transferred,0);
        }
        [Test] public void AutonomousDescendantsDevelopASuccessfulLineageWithoutRewards()
        {
            var cfg = Config(); cfg.StepSeconds = .5f; cfg.InitialNutrients = 8; cfg.NutrientRegeneration = 10;
            var f = Founder(Genes()); f.Age = 5; f.Energy = 24; f.Genes.Speed = .1f; f.Genes.Uptake = 3;
            var s = new Simulation(cfg,42,new[] {f}); var j = new EvolutionJourney(); s.TryControl(1); j.Begin(s,1); s.RequestDivision();
            for (int i = 0; i < 90; i++) { s.Step(); j.RecordStep(s,1); }
            Assert.IsTrue(j.Completed(EvolutionGoal.SuccessfulLineage)); Assert.GreaterOrEqual(j.LivingDescendants,6);
            Assert.GreaterOrEqual(j.LineageGenerations,3);
        }
        [Test] public void PlayerCanOccupyAnotherNicheWithoutForcedTraitsOrRewards()
        {
            var cfg = Config(); cfg.OceanZones = true; cfg.StepSeconds = .5f; cfg.NutrientRegeneration = 10; cfg.InitialNutrients = 8;
            var f = Founder(Genes()); f.Genes.Speed = 1.4f; f.Genes.Uptake = 3; f.Energy = 24;
            var s = new Simulation(cfg,42,new[] {f}); var j = new EvolutionJourney(); s.TryControl(1); j.Begin(s,1);
            var start = s.EnvironmentAt(f.X,f.Z).Zone;
            s.SetControlInput(0,1);
            for (int i = 0; i < 14; i++) { s.Step(); j.RecordStep(s,1); }
            s.SetControlInput(0,0);
            for (int i = 0; i < 35; i++) { s.Step(); j.RecordStep(s,1); }
            var c = s.GetCell(0);
            Assert.AreNotEqual(start,s.EnvironmentAt(c.X,c.Z).Zone); Assert.IsTrue(j.Completed(EvolutionGoal.NewNiche));
            Assert.AreEqual(f.Genes,c.Genes);
        }
        [Test] public void EveryNaturalEventChangesTheConditionsItAdvertises()
        {
            var cfg = Config(); cfg.GridSize = 2; cfg.OceanZones = true; cfg.StepSeconds = 1;
            var quiet = new Simulation(cfg,1); for (int i = 0; i < 49; i++) quiet.Step();
            var baseline = quiet.EnvironmentAt(4,7); cfg.NaturalEvents = true; int seen = 0;
            for (uint seed = 1; seed <= 96 && seen != 254; seed++) {
                var s = new Simulation(cfg,seed * 2654435761u); for (int i = 0; i < 49; i++) s.Step();
                var local = s.EnvironmentAt(4,7); seen |= 1 << (int)s.ActiveEvent;
                switch (s.ActiveEvent) {
                    case NaturalEvent.Cooling: Assert.Less(local.Temperature,baseline.Temperature - 6); break;
                    case NaturalEvent.Warming: Assert.Greater(local.Temperature,baseline.Temperature + 6); break;
                    case NaturalEvent.NutrientShortage: Assert.Less(local.Fertility,baseline.Fertility * .2f); break;
                    case NaturalEvent.ToxicPulse: Assert.Greater(local.Toxicity,baseline.Toxicity + .6f); break;
                    case NaturalEvent.CurrentShift: Assert.Less(local.CurrentX * baseline.CurrentX,0); break;
                    case NaturalEvent.ResourcePulse: Assert.Greater(local.Fertility,baseline.Fertility * 3); break;
                    case NaturalEvent.PlanktonBloom: Assert.Greater(local.Light,baseline.Light * 1.5f); break;
                    default: Assert.Fail("The first natural event should already be active."); break;
                }
            }
            Assert.AreEqual(254,seen);
        }
        [Test] public void WarmEnvironmentSelectsMatchingInheritedLineageWithoutPlayerIntervention()
        {
            var cfg = Config(); cfg.Temperature = 35; cfg.GridSize = 2; cfg.WorldSize = 3;
            cfg.InitialNutrients = 8; cfg.NutrientRegeneration = 10; cfg.StepSeconds = .25f;
            var cold = Founder(Genes(),1,1); cold.Genes.PreferredTemperature = 10;
            var warm = cold; warm.Genes.PreferredTemperature = 35;
            var s = new Simulation(cfg,42,new[] {cold,warm});
            for (int i = 0; i < 120; i++) s.Step();
            int coldCells = 0, warmCells = 0;
            for (int i = 0; i < s.Count; i++) {
                var c = s.GetCell(i); if (c.LineageId == 1) coldCells++; else warmCells++;
                Assert.AreEqual(c.LineageId == 1 ? 10 : 35,c.Genes.PreferredTemperature);
            }
            Assert.Greater(warmCells,coldCells); Assert.Greater(s.Births,0); Assert.AreEqual(0,s.ControlledId);
        }
        static double Stored(Simulation s)
        {
            double total = 0; int grid = s.Config.GridSize;
            for (int i = 0; i < grid * grid; i++) total += s.GetNutrient(i) * 2 + s.GetOrganicEnergy(i);
            for (int i = 0; i < s.Count; i++) { var c = s.GetCell(i); total += c.Energy + Biology.StructureEnergy(c.Genes); }
            return total;
        }
    }
}
