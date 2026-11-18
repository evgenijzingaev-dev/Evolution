using Evolution.Core;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class OceanEnvironmentTests
    {
        [Test] public void ZonesArePeriodicAndClassicEnvironmentIsUniform()
        {
            var a = OceanEnvironment.Sample(3, 7, 40, 20, 22, true);
            var b = OceanEnvironment.Sample(43, -33, 40, 20, 22, true);
            Assert.That(a.Temperature, Is.EqualTo(b.Temperature).Within(.00001));
            Assert.That(a.CurrentX, Is.EqualTo(b.CurrentX).Within(.00001));
            Assert.That(a.CurrentZ, Is.EqualTo(b.CurrentZ).Within(.00001));
            Assert.That(a.Fertility, Is.EqualTo(b.Fertility).Within(.00001));
            var classic = OceanEnvironment.Sample(3, 7, 40, 20, 22, false);
            Assert.That(classic.Temperature, Is.EqualTo(22));
            Assert.That(classic.Fertility, Is.EqualTo(1));
            Assert.That(classic.CurrentX + classic.CurrentZ, Is.Zero);
        }
        [Test] public void ZonesAffectRealResourcesAndThermalConditionsWithoutRandomDraws()
        {
            var config = new SimulationConfig { InitialPopulation = 0, OceanZones = true };
            var simulation = new Simulation(config, 42);
            uint state = simulation.RandomState;
            float min = float.MaxValue, max = 0;
            for (int i = 0; i < config.GridSize * config.GridSize; i++) {
                min = System.Math.Min(min, simulation.GetNutrient(i));
                max = System.Math.Max(max, simulation.GetNutrient(i));
            }
            Assert.That(max - min, Is.GreaterThan(1));
            Assert.That(simulation.EnvironmentAt(10, 0).Temperature, Is.EqualTo(28).Within(.001));
            simulation.Step();
            Assert.That(simulation.RandomState, Is.EqualTo(state));
        }
    }
}
