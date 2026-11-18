using System;
using Evolution.Core;
using Evolution.Presentation;
using UnityEngine;
namespace Evolution.Runtime
{
    // Original synthesized atmosphere. No downloaded assets, external libraries, or biology random calls.
    internal sealed class OceanAudio : IDisposable
    {
        readonly GameObject root;
        readonly AudioSource ambient, effects;
        readonly AudioClip drone, feeding, division, bite, hurt, warning, climate, click, brace, boost;
        long lastTick = -1;
        int lastSelected;
        NaturalEvent lastEvent;
        float nextFood, nextBite, nextHurt, nextDanger, nextBrace, nextBoost;
        bool wasDefending, wasBoosting;
        public OceanAudio(Transform parent)
        {
            root = new GameObject("Ocean soundscape"); root.transform.SetParent(parent,false);
            ambient = root.AddComponent<AudioSource>(); effects = root.AddComponent<AudioSource>();
            ambient.spatialBlend = effects.spatialBlend = 0;
            ambient.playOnAwake = effects.playOnAwake = false;
            drone = Synthesize("Underwater drift",OceanSound.LoopSeconds,0,0,true);
            feeding = Synthesize("Assimilation",.18f,320,150);
            division = Synthesize("Cell division",.7f,180,110);
            bite = Synthesize("Capture",.15f,120,-60);
            hurt = Synthesize("Membrane stress",.25f,85,-25);
            warning = Synthesize("Energy warning",.6f,160,-40);
            climate = Synthesize("Ocean change",1.2f,70,45);
            click = Synthesize("Selection",.07f,380,-50);
            brace=Synthesize("Membrane brace",.35f,190,-60);
            boost=Synthesize("Flagellar drive",.3f,110,70);
            ambient.clip = drone; ambient.loop = true; ambient.volume=0; ambient.Play();
        }
        static AudioClip Synthesize(string name, float duration, float frequency, float sweep, bool loop = false)
        {
            const int rate = 24000;
            int frames=Mathf.RoundToInt(duration*rate),channels=loop?2:1;
            var samples = new float[frames*channels];
            for (int i = 0; i < frames; i++) for(int c=0;c<channels;c++)
                samples[i*channels+c]=loop?OceanSound.Ambience(i/(double)rate,c):OceanSound.Interaction(i/(double)rate,duration,frequency,sweep);
            var clip = AudioClip.Create(name,frames,channels,rate,false); clip.SetData(samples,0); return clip;
        }
        public void Tick(Simulation world, int selected, float volume, bool paused)
        {
            ambient.volume = volume<=0?0:Mathf.MoveTowards(ambient.volume,Mathf.Clamp01(volume) * (paused ? .12f : .65f),Time.unscaledDeltaTime*.7f);
            effects.volume = Mathf.Clamp01(volume);
            if (selected != lastSelected) { if (selected != 0) effects.PlayOneShot(click,.18f); lastSelected = selected; wasDefending=wasBoosting=false; }
            if (world.Tick == lastTick || paused) return;
            if (world.Tick < lastTick) { lastEvent = NaturalEvent.None; nextFood=nextBite=nextHurt=nextDanger=nextBrace=nextBoost=0; wasDefending=wasBoosting=false; }
            lastTick = world.Tick;
            if (world.ActiveEvent != lastEvent) { if (world.ActiveEvent != NaturalEvent.None) effects.PlayOneShot(climate,.5f); lastEvent = world.ActiveEvent; }
            if (!Observation.TryFind(world,selected,out var c)) return;
            float now = Time.unscaledTime;
            if (c.AttackLastStep > 0 && now >= nextBite) { effects.PlayOneShot(bite,.32f); nextBite = now+.45f; }
            if (c.DamageLastStep > 0 && now >= nextHurt) { effects.PlayOneShot(hurt,.3f); nextHurt = now+.7f; }
            if (c.FoodLastStep > 0 && now >= nextFood) { effects.PlayOneShot(feeding,.18f); nextFood = now+1.4f; }
            if (c.Defending&&!wasDefending&&now>=nextBrace) { effects.PlayOneShot(brace,.25f); nextBrace=now+1.2f; }
            if (c.Boosting&&!wasBoosting&&now>=nextBoost) { effects.PlayOneShot(boost,.16f); nextBoost=now+1.2f; }
            wasDefending=c.Defending; wasBoosting=c.Boosting;
            if (world.LastDivisionParentId == selected && world.LastDivisionTick == world.Tick) effects.PlayOneShot(division,.45f);
            if (c.Energy < Biology.Capacity(c.Genes)*.15f && now >= nextDanger) { effects.PlayOneShot(warning,.25f); nextDanger = now+7; }
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(root);
            foreach (var clip in new[] {drone,feeding,division,bite,hurt,warning,climate,click,brace,boost}) UnityEngine.Object.Destroy(clip);
        }
    }
}
