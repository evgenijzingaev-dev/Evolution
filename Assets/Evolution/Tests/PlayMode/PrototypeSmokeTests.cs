using System.Collections;
using Evolution.Runtime;
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
        public IEnumerator DisablingRunnerStopsSimulationUpdates()
        {
            runner.enabled = false;
            var world = runner.World;
            var initialTick = world.Tick;
            yield return null;
            yield return null;
            Assert.That(world.Tick, Is.EqualTo(initialTick));
        }

        [UnityTearDown]
        public IEnumerator UnloadPrototype()
        {
            // Remove the scene and generated camera between tests.
            var empty = SceneManager.CreateScene("CI teardown");
            SceneManager.SetActiveScene(empty);
            var prototype = SceneManager.GetSceneByName("CellPrototype");
            if (prototype.isLoaded) yield return SceneManager.UnloadSceneAsync(prototype);
            runner = null;
        }
    }
}
