using System.Collections;
using Evolution.Runtime;
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
                    Assert.Greater(Vector3.Dot(lens.transform.forward,(rig.Focus-lens.transform.position).normalized),.999f);}
                Assert.Greater(lens.transform.position.y,rig.Focus.y);
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
    }
}
