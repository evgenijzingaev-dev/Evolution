using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;

namespace Evolution.Tests
{
    public sealed class PlayerReadoutTests
    {
        static VolumeAgent Agent(V3 p,float predation=0,float membrane=0)=>new VolumeAgent {
            Position=p,Direction=new V3(1,0,0),Biology=new Cell {Genes=new Genome {Size=1,Speed=1,Uptake=1,Sensitivity=1,PreferredTemperature=22,Predation=predation,Membrane=membrane},Energy=12,Integrity=1,Growth=1,Age=6}};
        [Test] public void InspectionDoesNotChangeBiologyFoodOrClock()
        {
            var s=new VolumeWorld(3);var a=s.Get(0);var f=s.GetFood(0);
            for(int i=0;i<30;i++)PlayerReadout.Read(s,0);
            Assert.AreEqual(a,s.Get(0));Assert.AreEqual(f,s.GetFood(0));Assert.AreEqual(0,s.Tick);
        }
        [Test] public void EnergyUsesRealCapacityAndIntegrityIsNotInventedHealth()
        {
            var s=new VolumeWorld(1,42,new[]{Agent(default)});var r=PlayerReadout.Read(s,0);
            Assert.AreEqual(.5f,r.Energy);Assert.AreEqual(1,r.Integrity);Assert.AreEqual("Хорошее",r.Condition);
            Assert.That(r.ThermalEfficiency,Is.EqualTo(Biology.Efficiency(s.Get(0).Biology.Genes,s.Temperature(default))));
        }
        [Test] public void FoodBeyondSensingRangeDoesNotCountAsNearbyNutrition()
        {
            var s=new VolumeWorld(1,42,new[]{Agent(new V3(1000,1000,1000))});var r=PlayerReadout.Read(s,0);
            Assert.AreEqual(-1,r.Food);Assert.AreEqual("Мало",r.Nutrition);Assert.AreEqual("Найди подходящую пищу",r.Goal);
        }
        [Test] public void FoodMatchesPotentialAndActualMaintenanceNeeds()
        {
            var template=new VolumeWorld(1);var s=new VolumeWorld(1,42,new[]{Agent(template.GetFood(1).Position)});
            var r=PlayerReadout.Read(s,0);Assert.GreaterOrEqual(r.Food,0);
            Assert.AreEqual(s.FoodPotential(r.Food,s.Get(0).Biology),r.FoodRate);
            Assert.AreEqual(Biology.Cost(s.Get(0).Biology.Genes,s.Sample(s.Get(0).Position).Temperature),r.Need);
        }
        [Test] public void GenuineDamagingPredatorOverridesFoodGoal()
        {
            var s=new VolumeWorld(2,42,new[]{Agent(default),Agent(new V3(1,0,0),1)});var r=PlayerReadout.Read(s,0);
            Assert.AreEqual(1,r.Threat);Assert.AreEqual("Отплыви от хищника",r.Goal);
        }
        [Test] public void HarmlessAndDistantOrganismsAreNotThreats()
        {
            var s=new VolumeWorld(3,42,new[]{Agent(default),Agent(new V3(1,0,0)),Agent(new V3(0,50,0),1)});
            Assert.AreEqual(-1,PlayerReadout.Read(s,0).Threat);
        }
        [Test] public void MissingSelectionOffersSelectionInsteadOfSurvivalActions()
        {
            Assert.AreEqual("Выбери другую живую клетку",PlayerReadout.Read(new VolumeWorld(1),-1).Goal);
        }
    }
}
