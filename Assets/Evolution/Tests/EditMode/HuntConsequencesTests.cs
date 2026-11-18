using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
using System.Collections.Generic;

namespace Evolution.Tests
{
    public sealed class HuntConsequencesTests
    {
        static VolumeAgent Hunter() => new VolumeAgent {
            Position = new V3(10, 10, 10), Direction = new V3(1, 0, 0), Alive = true, Identity = "local-hunter",
            Biology = new Cell {
                Id = 1, LineageId = 1,
                Genes = new Genome { Speed = 1.2f, Size = 1.2f, Uptake = 1, PreferredTemperature = 22, Predation = 1, Membrane = .5f, Sensitivity = 1, EnergyEfficiency = .5f, ThermalTolerance = 1, ToxinResistance = 1 },
                Energy = 24, Age = 10, Growth = 1, Integrity = 1 } };
        static VolumeAgent Prey() => new VolumeAgent {
            Position = new V3(10.2f, 10, 10), Direction = new V3(1, 0, 0), Alive = true, Identity = "local-prey",
            Biology = new Cell {
                Id = 2, LineageId = 2,
                Genes = new Genome { Speed = .8f, Size = 1, Uptake = 1, PreferredTemperature = 22, Membrane = .2f, EnergyEfficiency = .5f, ThermalTolerance = 1, ToxinResistance = 1 },
                Energy = 8, Age = 10, Growth = 1, Integrity = 1 } };
        [Test]
        public void DepletionNeedsABaselineAndRecoveryNeedsTheFullCrowd()
        {
            Assert.IsFalse(HuntConsequences.Depleted(1, 0), "A single death is noise, not depletion.");
            Assert.IsFalse(HuntConsequences.Depleted(0, 0));
            Assert.IsTrue(HuntConsequences.Depleted(2, 1));
            Assert.IsTrue(HuntConsequences.Depleted(4, 2));
            Assert.IsFalse(HuntConsequences.Depleted(4, 3));
            Assert.IsTrue(HuntConsequences.Recovered(4, 4));
            Assert.IsFalse(HuntConsequences.Recovered(4, 3));
        }
        [Test]
        public void HuntedRegionWarnsOnFreshDepletionAndClearsOnRecovery()
        {
            var hunter = Hunter();
            var prey = Prey();
            var world = new VolumeWorld(new[] { hunter, prey }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 0, default) { Controlled = 0 };
            var tree = new LineageTree();
            tree.Record(world, 0);
            world.Attack = true;
            world.Step();
            Assert.Greater(world.Get(0).Biology.AttackLastStep, 0, "The model itself must confirm the bite.");
            var survey = new OceanSurvey();
            var loaded = new List<ChunkKey> { ChunkKey.At(hunter.Position) };
            var food = new List<FoodCloud>();
            for (int i = 0; i < world.FoodCount; i++) food.Add(world.GetFood(i));
            var hunts = new HuntConsequences();
            survey.Observe(new List<VolumeAgent> { world.Get(0), world.Get(1) }, food, loaded, world.Tick, default);
            Assert.IsFalse(hunts.Record(world, survey, tree), "Two living organisms after a bite is not depletion.");
            // A fresh observation reports the region with the prey gone.
            var corpse = world.Get(1);
            corpse.Alive = false;
            survey.Observe(new List<VolumeAgent> { world.Get(0), corpse }, food, loaded, world.Tick + 1, default);
            Assert.IsTrue(hunts.Record(world, survey, tree));
            StringAssert.Contains("охота твоей линии", hunts.Warning);
            // The neighbourhood recovers: the warning must clear, not stick forever.
            var newcomer = corpse;
            newcomer.Alive = true;
            newcomer.Identity = "local-agent-newcomer";
            survey.Observe(new List<VolumeAgent> { world.Get(0), newcomer }, food, loaded, world.Tick + 2, default);
            Assert.IsFalse(hunts.Record(world, survey, tree));
            Assert.IsNull(hunts.Warning);
        }
        [Test]
        public void UnloadedRegionProvesNothing()
        {
            var hunter = Hunter();
            var prey = Prey();
            var world = new VolumeWorld(new[] { hunter, prey }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 0, default) { Controlled = 0 };
            var tree = new LineageTree();
            tree.Record(world, 0);
            world.Attack = true;
            world.Step();
            var survey = new OceanSurvey();
            var food = new List<FoodCloud>();
            for (int i = 0; i < world.FoodCount; i++) food.Add(world.GetFood(i));
            var loaded = new List<ChunkKey> { ChunkKey.At(hunter.Position) };
            survey.Observe(new List<VolumeAgent> { world.Get(0), world.Get(1) }, food, loaded, world.Tick, default);
            var corpse = world.Get(1);
            corpse.Alive = false;
            // The region was not observed this time: last numbers prove nothing.
            survey.Observe(new List<VolumeAgent> { world.Get(0), corpse }, food, new List<ChunkKey>(), world.Tick + 1, default);
            var hunts = new HuntConsequences();
            Assert.IsFalse(hunts.Record(world, survey, tree));
            Assert.IsNull(hunts.Warning);
        }
        [Test]
        public void StrangersAndAbsentTreesAreIgnored()
        {
            var hunter = Hunter();
            var prey = Prey();
            var world = new VolumeWorld(new[] { hunter, prey }, System.Array.Empty<FoodCloud>(), System.Array.Empty<V3>(), 0, default) { Controlled = 0 };
            var survey = new OceanSurvey();
            var loaded = new List<ChunkKey> { ChunkKey.At(hunter.Position) };
            var food = new List<FoodCloud>();
            for (int i = 0; i < world.FoodCount; i++) food.Add(world.GetFood(i));
            world.Attack = true;
            world.Step();
            survey.Observe(new List<VolumeAgent> { world.Get(0), world.Get(1) }, food, loaded, world.Tick, default);
            // No founder captured: a bite by a stranger is not "your" hunting.
            Assert.IsFalse(new HuntConsequences().Record(world, survey, new LineageTree()));
            Assert.IsFalse(new HuntConsequences().Record(world, null, null));
            Assert.IsFalse(new HuntConsequences().Record(null, survey, null));
        }
    }
}
