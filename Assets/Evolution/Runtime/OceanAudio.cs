using System;
using Evolution.Core;
using UnityEngine;
namespace Evolution.Runtime
{
    // Original synthesized atmosphere. No downloaded assets, external libraries, or biology random calls.
    internal sealed class OceanAudio : IDisposable
    {
        readonly GameObject root;
        readonly AudioSource ambient, effects;
        readonly AudioClip drone, feeding, division, bite, hurt, warning, climate, click;
        long lastTick = -1;
        int lastSelected;
        NaturalEvent lastEvent;
        float nextFood, nextBite, nextHurt, nextDanger;
        public OceanAudio(Transform parent)
        {
            root = new GameObject("Ocean soundscape"); root.transform.SetParent(parent,false);
            ambient = root.AddComponent<AudioSource>(); effects = root.AddComponent<AudioSource>();
            ambient.spatialBlend = effects.spatialBlend = 0;
            ambient.playOnAwake = effects.playOnAwake = false;
            drone = Synthesize("Underwater drift",8,48,0,true);
            feeding = Synthesize("Assimilation",.18f,320,150);
            division = Synthesize("Cell division",.7f,180,110);
            bite = Synthesize("Capture",.15f,120,-60);
            hurt = Synthesize("Membrane stress",.25f,85,-25);
            warning = Synthesize("Energy warning",.6f,160,-40);
            climate = Synthesize("Ocean change",1.2f,70,45);
            click = Synthesize("Selection",.07f,380,-50);
            ambient.clip = drone; ambient.loop = true; ambient.Play();
        }
        static AudioClip Synthesize(string name, float duration, float frequency, float sweep, bool loop = false)
        {
            const int rate = 24000;
            var samples = new float[Mathf.RoundToInt(duration*rate)];
            for (int i = 0; i < samples.Length; i++) {
                float t = i/(float)rate, phase = 2*Mathf.PI*(frequency*t+sweep*t*t*.5f);
                float envelope = loop ? .55f + .15f*Mathf.Sin(2*Mathf.PI*t/duration) : Mathf.Sin(Mathf.PI*i/samples.Length)*(float)Math.Exp(-t*3/duration);
                samples[i] = (Mathf.Sin(phase)*.6f + Mathf.Sin(phase*.5f)*.25f + Mathf.Sin(phase*1.5f)*.1f) * envelope * .24f;
            }
            var clip = AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
        public void Tick(Simulation world, int selected, float volume, bool paused)
        {
            ambient.volume = Mathf.Clamp01(volume) * (paused ? .15f : .35f); effects.volume = Mathf.Clamp01(volume);
            if (selected != lastSelected) { if (selected != 0) effects.PlayOneShot(click,.25f); lastSelected = selected; }
            if (world.Tick == lastTick || paused) return;
            if (world.Tick < lastTick) lastEvent = NaturalEvent.None;
            lastTick = world.Tick;
            if (world.ActiveEvent != lastEvent) { if (world.ActiveEvent != NaturalEvent.None) effects.PlayOneShot(climate,.5f); lastEvent = world.ActiveEvent; }
            if (!Observation.TryFind(world,selected,out var c)) return;
            float now = Time.unscaledTime;
            if (c.AttackLastStep > 0 && now >= nextBite) { effects.PlayOneShot(bite,.5f); nextBite = now+.3f; }
            if (c.DamageLastStep > 0 && now >= nextHurt) { effects.PlayOneShot(hurt,.6f); nextHurt = now+.5f; }
            if (c.FoodLastStep > 0 && now >= nextFood) { effects.PlayOneShot(feeding,.22f); nextFood = now+1.1f; }
            if (world.LastDivisionParentId == selected && world.LastDivisionTick == world.Tick) effects.PlayOneShot(division,.6f);
            if (c.Energy < Biology.Capacity(c.Genes)*.15f && now >= nextDanger) { effects.PlayOneShot(warning,.4f); nextDanger = now+5; }
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(root);
            foreach (var clip in new[] {drone,feeding,division,bite,hurt,warning,climate,click}) UnityEngine.Object.Destroy(clip);
        }
    }
}
