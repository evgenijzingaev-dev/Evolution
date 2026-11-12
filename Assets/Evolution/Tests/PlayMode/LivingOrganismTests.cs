using Evolution.Core;
using NUnit.Framework;
using UnityEngine;

namespace Evolution.Tests
{
    // Integration with real Unity meshes, instancing and the production renderer.
    // These tests require Editor; the .NET suite intentionally cannot claim this coverage.
    public sealed class LivingOrganismTests
    {
        Evolution.Runtime.OrganismRenderer renderer;
        Mesh low, high;
        Material body, membrane;
        [SetUp]
        public void SetUp()
        {
            var shader = Resources.Load<Shader>("CellMembrane");
            Assert.IsNotNull(shader); Assert.IsTrue(shader.isSupported);
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
#endif
            low = Evolution.Runtime.MicroMeshes.Cell(); high = Evolution.Runtime.MicroMeshes.Cell(16,24);
            body = new Material(Resources.Load<Shader>("InstancedCells")) { enableInstancing=true };
            membrane = new Material(shader) { enableInstancing=true };
            renderer = new Evolution.Runtime.OrganismRenderer(low,body,membrane,512,high);
        }
        static Simulation World(float maxAge = 180) => new Simulation(new SimulationConfig {
            InitialPopulation=0,Capacity=512,WorldSize=12,InitialNutrients=8,NutrientRegeneration=.5f,
            MaxAge=maxAge,MutationProbability=0
        },42,new[] { new Cell { Genes=new Genome { Size=1,Speed=1.4f,Uptake=1.2f,PreferredTemperature=22 },X=4,Z=4,Age=6,Energy=23 } });
        void Tick(Simulation world) { renderer.Capture(world); world.Step(); renderer.ObserveTick(world); }
        [Test]
        public void TicksWithoutRenderedFramesRetainRealBirthAndFeeding()
        {
            var world=World(); Tick(world);
            Assert.Greater(world.Births,0); Assert.AreEqual(world.Count,renderer.AnimatedCount);
            Assert.IsTrue(renderer.TryAnimation(1,out var parent)); Assert.Greater(parent.FoodEnergy,0);
            Assert.AreEqual(world.StepSeconds,parent.DivisionAt);
            Assert.IsTrue(renderer.TryAnimation(2,out var child)); Assert.AreEqual(parent.DivisionAt,child.DivisionAt);
            for(int i=0;i<10;i++) Tick(world);
            Assert.AreEqual(world.Count,renderer.AnimatedCount);
        }
        [Test]
        public void DeathDropsVisualStateEvenWithoutADraw()
        {
            var world=World(6.1f); Tick(world); Tick(world); Tick(world);
            Assert.Greater(world.Deaths,0); Assert.AreEqual(world.Count,renderer.AnimatedCount);
            Assert.IsFalse(renderer.TryAnimation(1,out _));
        }
        [Test]
        public void RepeatedCloseAndOverviewDrawsCannotChangePausedWorld()
        {
            var world=World(); Tick(world); var before=world.GetCell(0); var random=world.RandomState; long tick=world.Tick;
            renderer.Draw(world,3,12,1,.05f,.5f,3,new Vector3(4,.54f,4),true);
            Assert.IsTrue(renderer.TryAnimation(1,out var a)); double phase=a.Phase;
            renderer.Draw(world,3,12,1,.05f,.5f,3,new Vector3(4,.54f,4),true);
            Assert.AreEqual(phase,a.Phase);
            renderer.Draw(world,3,12,1,.05f,.5f,40,new Vector3(4,.54f,4),false);
            Assert.AreEqual(before,world.GetCell(0)); Assert.AreEqual(random,world.RandomState); Assert.AreEqual(tick,world.Tick);
        }
        [Test]
        public void AtmosphereShadersLoadAndOceanCannotModifyPausedBiology()
        {
            foreach (string name in new[] { "MicroHabitat", "OceanCloud", "OceanLightShafts", "OceanLightVolume", "OceanParticles" }) {
                var shader=Resources.Load<Shader>(name); Assert.IsNotNull(shader,name); Assert.IsTrue(shader.isSupported,name);
#if UNITY_EDITOR
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),name);
#endif
            }
            var world=World(); var before=world.GetCell(0); uint random=world.RandomState;
            var plane=Evolution.Runtime.MicroMeshes.Habitat();
            var cameraObject=new GameObject("Ocean validation camera",typeof(Camera));
            var ocean=new Evolution.Runtime.OceanRenderer(12,32,plane,low,8);
            try {
                ocean.Draw(world,cameraObject.GetComponent<Camera>(),0,new Vector3(4,0,4));
                ocean.Draw(world,cameraObject.GetComponent<Camera>(),0,new Vector3(16,0,4));
                Assert.AreEqual(before,world.GetCell(0)); Assert.AreEqual(random,world.RandomState); Assert.AreEqual(0,world.Tick);
            } finally { ocean.Dispose(); Object.DestroyImmediate(plane); Object.DestroyImmediate(cameraObject); }
        }
        [Test]
        public void SurfacePickingHitsDeformedBodyAndRejectsNearbyEmptyWater()
        {
            var world=World(); var c=world.GetCell(0);
            renderer.Draw(world,4,12,1,0,0,3,new Vector3(4,.54f,4),true);
            Vector3 center=renderer.Position(c);
            Assert.That(renderer.HitSurface(c,new Ray(center+Vector3.up*4,Vector3.down),center),Is.InRange(3f,4f));
            Assert.IsTrue(float.IsPositiveInfinity(renderer.HitSurface(c,new Ray(center+new Vector3(0,4,1.5f),Vector3.down),center)));
            Assert.AreEqual(c,world.GetCell(0)); Assert.AreEqual(0,world.Tick);
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(low); Object.DestroyImmediate(high); Object.DestroyImmediate(body); Object.DestroyImmediate(membrane);
        }
    }
}
