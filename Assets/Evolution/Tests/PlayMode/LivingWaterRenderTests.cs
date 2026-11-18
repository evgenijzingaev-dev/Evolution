using Evolution.Core;
using Evolution.Runtime;
using Evolution.Volume;
using NUnit.Framework;
using UnityEngine;

namespace Evolution.Tests
{
    public sealed class LivingWaterRenderTests
    {
        [Test] public void PausedDrawingAndLodChangesDoNotChangePaidBirthOrCoreResources()
        {
            var root=new GameObject("Living render regression");var lens=root.AddComponent<Camera>();
            var look=new CrystalWaterLook(root.transform,lens);var painter=new VolumePainter(look);
            try {
                var g=new Genome {Size=1,Speed=.7f,Uptake=1,PreferredTemperature=22};
                var world=new VolumeWorld(1,42,new[] {new VolumeAgent {Position=default,Direction=new V3(1,0,0),
                    Biology=new Cell {Genes=g,Energy=Biology.Capacity(g),Age=8,Growth=1,Integrity=1}}}) {Controlled=0};
                var suspension=new Suspension(512,42);Assert.IsTrue(world.RequestDivision());world.Step();
                var parent=world.Get(0);var child=world.Get(1);long eventCount=world.Life.Latest;
                for(int frame=0;frame<8;frame++) {
                    lens.transform.position=new Vector3(0,0,frame%2==0?-4:-20);lens.transform.LookAt(Vector3.zero);
                    painter.Draw(world,suspension,lens,0,true,false,false,0);
                    Assert.AreEqual(parent,world.Get(0));Assert.AreEqual(child,world.Get(1));Assert.AreEqual(eventCount,world.Life.Latest);
                }
            } finally {painter.Dispose();look.Dispose();Object.DestroyImmediate(root);}
        }
        [Test] public void DeathDrawsRemainDecorativeWhenCoreAdvancesPastCorpseRetention()
        {
            var root=new GameObject("Death render regression");var lens=root.AddComponent<Camera>();
            var look=new CrystalWaterLook(root.transform,lens);var painter=new VolumePainter(look);
            try {
                var g=new Genome {Size=1,Speed=.7f,Uptake=1,PreferredTemperature=22};
                var world=new VolumeWorld(1,42,new[] {new VolumeAgent {Position=new V3(200,0,0),Direction=new V3(1,0,0),
                    Biology=new Cell {Genes=g,Energy=.00001f,Age=8,Growth=1,Integrity=1}}}) {Controlled=0,ActiveCenter=new V3(200,0,0)};
                lens.transform.position=new Vector3(200,0,-4);lens.transform.LookAt(new Vector3(200,0,0));
                var suspension=new Suspension(512,42);world.Step();Assert.IsFalse(world.Get(0).Alive);
                for(int step=0;step<90;step++) {
                    var dead=world.Get(0);var food=world.GetFood(0);double loss=world.Diagnostics.RecyclingLoss;
                    painter.Draw(world,suspension,lens,0,true,false,false);
                    Assert.AreEqual(dead,world.Get(0));Assert.AreEqual(food,world.GetFood(0));Assert.AreEqual(loss,world.Diagnostics.RecyclingLoss);
                    world.Step();
                }
                Assert.AreEqual(1,world.Life.Latest);Assert.AreEqual(1,world.Diagnostics.Deaths);
            } finally {painter.Dispose();look.Dispose();Object.DestroyImmediate(root);}
        }
    }
}
