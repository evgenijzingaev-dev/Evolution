using System.Collections;
using Evolution.Runtime;
using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Evolution.Tests
{
    public sealed class VolumePrototypeTests
    {
        [Test]
        public void VolumeShadersLoadWithoutCompilerErrors()
        {
            foreach(string name in new[] {"VolumeBackdrop","VolumeBody","VolumeMembrane","VolumeSuspension"}) {
                var shader=Resources.Load<Shader>(name);Assert.IsNotNull(shader,name);Assert.IsTrue(shader.isSupported,name);
#if UNITY_EDITOR
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),name);
#endif
            }
        }
        [Test]
        public void CameraCanOrbitInEveryDirectionAboveAndBelowItsTarget()
        {
            var root=new GameObject("Volume camera test");var lens=root.AddComponent<Camera>();var rig=root.AddComponent<WaterCamera>();
            try {
                rig.Initialize(lens);rig.FocusOn(new Vector3(3,4,5),7);
                for(int i=0;i<8;i++) {rig.Orbit(45,i==0?30:i==4?-90:0);rig.ApplyImmediate();
                    Assert.That(Vector3.Distance(lens.transform.position,rig.Focus),Is.EqualTo(7).Within(.001));
                    Assert.Greater(Vector3.Dot(lens.transform.forward,(rig.Focus-lens.transform.position).normalized),.999f);
                    if(i==0) Assert.Greater(lens.transform.position.y,rig.Focus.y,"Positive pitch must orbit above the target");
                    if(i==4) Assert.Less(lens.transform.position.y,rig.Focus.y,"Negative pitch must orbit below the target");}
            } finally {Object.DestroyImmediate(root);}
        }
        [UnityTest]
        public IEnumerator WaterOnlyAndEcologicalLayersDoNotAdvancePausedBiology()
        {
            var root=new GameObject("Volume experiment test");var prototype=root.AddComponent<WaterPrototype>();
            prototype.PersistRegions=false;prototype.ParticleCount=512;prototype.SoundVolume=0;prototype.Paused=true;
            yield return null;
            try {
                var world=prototype.World;var before=world.Get(prototype.Ocean.Nearest(VolumePainter.V(prototype.Rig.Focus)));
                prototype.OrganismsVisible=false;prototype.ShowTemperature=prototype.ShowCurrents=prototype.FindFood=true;
                prototype.Rig.Orbit(45,25);prototype.Rig.ApplyImmediate();yield return null;
                Assert.AreEqual(0,prototype.World.Tick);
                var after=prototype.World.Get(prototype.Ocean.Find(before.Identity));
                Assert.AreEqual(before.Position,after.Position);Assert.AreEqual(before.Biology.Energy,after.Biology.Energy);
                Assert.Greater(prototype.Water.Count,0);Assert.IsNotNull(prototype.Lens.GetComponent<AudioListener>());
            } finally {Object.Destroy(root);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator FastCameraFlightStaysWithinStreamingVisibilityMargin()
        {
            var root=new GameObject("Fast volume camera");var lens=root.AddComponent<Camera>();var rig=root.AddComponent<WaterCamera>();rig.Initialize(lens);
            rig.FocusOn(new Vector3(500,300,-700),30);
            for(int i=0;i<8;i++){Vector3 before=lens.transform.position;rig.Tick(true,true);Assert.LessOrEqual(Vector3.Distance(before,lens.transform.position),2.501f);yield return null;}
            Object.Destroy(root);yield return null;
        }
        [Test]
        public void ReferenceDiatomIsSelectableAtItsTipWithoutSelectingEmptyWater()
        {
            var root=new GameObject("Crystal surface validation");var lens=root.AddComponent<Camera>();
            var look=new CrystalWaterLook(root.transform,lens);var painter=new VolumePainter(look);
            try {
                var world=new VolumeWorld(1,42,new[] {new VolumeAgent {Position=default,Direction=new V3(1,0,0),
                    Biology=new Cell {Genes=new Genome {Size=1,Speed=.7f,Uptake=1,Membrane=.9f,PreferredTemperature=22},Energy=16,Growth=1,Integrity=1}}});
                var before=world.Get(0);
                Assert.AreEqual(0,painter.Pick(world,new Ray(new Vector3(.9f,3,0),Vector3.down),0));
                Assert.AreEqual(-1,painter.Pick(world,new Ray(new Vector3(1.2f,3,0),Vector3.down),0));
                Assert.AreEqual(before,world.Get(0));
            } finally {painter.Dispose();look.Dispose();Object.DestroyImmediate(root);}
        }
    }
}
