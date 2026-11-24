using System.Collections;
using Evolution.Runtime;
using Evolution.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Evolution.Tests
{
    public sealed class PrototypeSmokeTests
    {
        EvolutionRunner runner;

        [UnitySetUp]
        public IEnumerator LoadPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CellPrototype", LoadSceneMode.Single);
            yield return null; // Allow Start to initialize the real scene component.
            runner = Object.FindFirstObjectByType<EvolutionRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.World, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneInitializesRuntimeAndAdvancesSimulation()
        {
            var world = runner.World;
            Assert.That(world.Count, Is.GreaterThan(0));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Resources.Load<Shader>("InstancedCells"), Is.Not.Null);
            var initialTick = world.Tick;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (world.Tick <= initialTick && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(world.Tick, Is.GreaterThan(initialTick), "The scene must advance through EvolutionRunner.Update.");
            Assert.That(world.Count, Is.InRange(1, world.Config.Capacity));
        }

        [UnityTest]
        public IEnumerator CellPrototypeCanOpenThe3DOceanFromTheBuild()
        {
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.Zero);
            WaterPrototype water = null;
            void UseInMemoryArchive(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "WaterPrototype3D") return;
                water = Object.FindFirstObjectByType<WaterPrototype>();
                if (!water) return;
                // The scene-loaded callback runs before Start opens the region archive.
                water.PersistRegions = false;
                water.ParticleCount = 512;
                water.SoundVolume = 0;
                water.Paused = true;
            }

            SceneManager.sceneLoaded += UseInMemoryArchive;
            try {
                var load = runner.OpenOcean3D();
                var repeatLoad = runner.OpenOcean3D();
                yield return load;
                if (repeatLoad != null && repeatLoad != load) yield return repeatLoad;
                Assert.That(load, Is.Not.Null);
                Assert.AreSame(load, repeatLoad, "Repeated clicks must reuse the pending load.");
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WaterPrototype3D"));
                Assert.That(SceneManager.GetSceneByName("CellPrototype").isLoaded, Is.False);
                Assert.That(water, Is.Not.Null);
                Assert.That(water.World, Is.Not.Null);
                Assert.That(water.World.Count, Is.GreaterThan(0));
            } finally {
                SceneManager.sceneLoaded -= UseInMemoryArchive;
            }
        }

        [UnityTest]
        public IEnumerator DisablingRunnerStopsSimulationUpdates()
        {
            runner.enabled = false;
            var world = runner.World;
            var initialTick = world.Tick;
            yield return null;
            yield return null;
            Assert.That(world.Tick, Is.EqualTo(initialTick));
        }

        [UnityTest]
        public IEnumerator SelectionAndCameraDoNotAdvancePausedBiology()
        {
            runner.IsPaused = true;
            var world = runner.World; long tick = world.Tick; uint random = world.RandomState;
            int id = world.GetCell(0).Id;
            Assert.That(runner.SelectCell(id), Is.True);
            runner.FocusSelected(true);
            yield return null;
            Assert.That(runner.SelectedId, Is.EqualTo(id));
            Assert.That(runner.CameraRig.Following, Is.True);
            Assert.That(runner.CameraRig.Distance, Is.LessThanOrEqualTo(5));
            Assert.That(world.Tick, Is.EqualTo(tick)); Assert.That(world.RandomState, Is.EqualTo(random));
            runner.CameraRig.Overview();
            Assert.That(runner.CameraRig.Following, Is.False);
            Assert.That(runner.CameraRig.Distance, Is.GreaterThan(5));
        }

        [UnityTest]
        public IEnumerator PickerSelectsRenderedCellAtItsScreenPosition()
        {
            runner.IsPaused = true;
            var cell = runner.World.GetCell(0);
            var screen = Camera.main.WorldToScreenPoint(new Vector3(cell.X, .54f, cell.Z));
            Assert.That(screen.z, Is.GreaterThan(0));
            int picked = runner.Pick(new Vector2(screen.x, screen.y));
            Assert.That(picked, Is.GreaterThan(0));
            Assert.That(runner.SelectCell(picked), Is.True);
            Assert.That(runner.SelectCell(int.MaxValue), Is.False);
            runner.ClearSelection(); Assert.That(runner.SelectedId, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PeriodicCameraAndResetPreserveThePopulation()
        {
            runner.IsPaused = true;
            runner.SelectCell(runner.World.GetCell(0).Id);
            var world = runner.World; long tick = world.Tick; uint random = world.RandomState;
            int count = world.Count; var first = world.GetCell(0);
            Vector3 before = runner.CameraRig.FocusPoint;
            runner.CameraRig.FocusOn(new Vector3(-100, -20, 10000), true);
            Assert.That(Mathf.Abs(runner.CameraRig.FocusPoint.x-before.x), Is.LessThanOrEqualTo(world.Config.WorldSize*.5f));
            Assert.That(Mathf.Abs(runner.CameraRig.FocusPoint.z-before.z), Is.LessThanOrEqualTo(world.Config.WorldSize*.5f));
            Vector3 focus = runner.CameraRig.FocusPoint;
            runner.CameraRig.FocusOn(focus + new Vector3(world.Config.WorldSize*3,0,-world.Config.WorldSize*2),true);
            Assert.That(Vector3.Distance(focus,runner.CameraRig.FocusPoint),Is.LessThan(.001f));
            yield return null;
            Assert.AreEqual(tick,world.Tick); Assert.AreEqual(random,world.RandomState);
            Assert.AreEqual(count,world.Count); Assert.AreEqual(first,world.GetCell(0));
            runner.CameraRig.Zoom(10000);
            Assert.That(runner.CameraRig.Distance, Is.GreaterThanOrEqualTo(1.8f));
            runner.ResetWorld();
            Assert.That(runner.SelectedId, Is.Zero); Assert.That(runner.CameraRig.Following, Is.False);
            Assert.That(Resources.Load<Shader>("CellMembrane"), Is.Not.Null);
            Assert.That(Resources.Load<Shader>("MicroHabitat"), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlRoundTripPreservesWorldAndPausedTicks()
        {
            runner.IsPaused = true; var world = runner.World; long tick = world.Tick;
            runner.SelectCell(world.GetCell(0).Id);
            Assert.IsTrue(runner.PlaySelected()); Assert.IsTrue(runner.IsControlling);
            Assert.IsTrue(runner.CameraRig.Following);
            yield return null;
            Assert.AreEqual(tick, world.Tick); Assert.AreSame(world, runner.World);
            runner.ObserveWorld(); Assert.IsFalse(runner.IsControlling);
            Assert.AreSame(world, runner.World); Assert.Greater(runner.SelectedId, 0);
            runner.PlaySelected(); runner.ResetWorld(); Assert.IsFalse(runner.IsControlling);
        }

        [UnityTest]
        public IEnumerator HiddenHudAndDetailSettingDoNotChangePausedBiology()
        {
            runner.IsPaused = true; var world = runner.World;
            var before = world.GetCell(0); long tick = world.Tick; uint random = world.RandomState;
            runner.HudVisible = false; runner.DetailedOrganisms = false; runner.SoundVolume = 0;
            runner.SelectCell(before.Id); runner.FocusSelected(true);
            yield return null; yield return null;
            Assert.AreEqual(tick,world.Tick); Assert.AreEqual(random,world.RandomState);
            Assert.AreEqual(before,world.GetCell(0));
            Assert.IsNotNull(Camera.main.GetComponent<AudioListener>());
        }

        [UnityTearDown]
        public IEnumerator UnloadPrototype()
        {
            // Remove the scene and generated camera between tests.
            var empty = SceneManager.CreateScene("CI teardown");
            SceneManager.SetActiveScene(empty);
            var prototype = SceneManager.GetSceneByName("CellPrototype");
            if (prototype.isLoaded) yield return SceneManager.UnloadSceneAsync(prototype);
            var ocean = SceneManager.GetSceneByName("WaterPrototype3D");
            if (ocean.isLoaded) yield return SceneManager.UnloadSceneAsync(ocean);
            runner = null;
        }
    }
}
