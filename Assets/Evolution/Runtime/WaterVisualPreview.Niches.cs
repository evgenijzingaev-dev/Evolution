using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Evolution.Volume;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed partial class WaterVisualPreview
    {
        [Serializable] sealed class NicheCaptureEvidence
        {
            public string unity,revision,capturedUtc,scene="WaterPrototype3D";
            public bool fixture=false,modelDriven=true;
            public long firstTick,lastTick,births,deaths;
            public int firstPopulation,lastPopulation;
            public double energyResidual,matterResidual,oxygenResidual;
            public List<NicheHistoryPoint> history;
            public string[] frames;
            public Report render;
        }
        // Dormant without both preview opt-ins. No altered genomes, resources, births or positions.
        IEnumerator CaptureNicheEcology()
        {
            Require(water.World.Niches,"ecology must be active in the real game");water.Observe();water.Paused=true;
            var evidence=new NicheCaptureEvidence {unity=Application.unityVersion,revision=Context(new Shot[0],true).revision,capturedUtc=DateTime.UtcNow.ToString("O"),
                firstTick=water.World.Tick,firstPopulation=VolumeSnapshot.Read(water.World).Alive};
            var layers=new[]{SurveyLayer.Light,SurveyLayer.Temperature,SurveyLayer.Nutrients,SurveyLayer.Oxygen,SurveyLayer.Chemistry,SurveyLayer.Diet,SurveyLayer.Adaptation};
            var names=new[]{"ecology-light","ecology-temperature","ecology-nutrients","ecology-oxygen","ecology-chemical","ecology-strategies","ecology-optima"};
            water.Overview.Enter();water.Overview.SetMode(true);water.Overview.AutoFrame=false;water.Overview.AutoOrbit=false;water.Overview.EventsVisible=true;water.HudVisible=true;
            for(int i=0;i<layers.Length;i++) {
                water.Overview.Layer=layers[i];water.Hud.SetNicheLayer(i<5?i+1:7);water.Overview.Refresh();
                yield return Capture(names[i],false);
            }
            // Ordinary deterministic Core steps while observing a fixed region; no fixture food/DNA edits.
            for(int tick=0;tick<12000;tick++) {water.World.Step();if(tick%20==0){water.Overview.Refresh();yield return null;}}
            water.Overview.Refresh();water.Overview.Layer=SurveyLayer.Diet;water.Hud.SetNicheLayer(7);water.HudVisible=true;
            yield return Capture("ecology-after-600s",false);
            var total=water.Ocean.Snapshot(out _);var d=water.World.Diagnostics;
            evidence.lastTick=water.World.Tick;evidence.lastPopulation=total.Alive;evidence.births=d.Births;evidence.deaths=d.Deaths;
            evidence.energyResidual=d.InitialEnergy+d.ExternalNutrients+d.ExternalOrganic+d.Solar-d.Dissipated-total.Stored;
            evidence.matterResidual=d.InitialMatter+d.ExternalMatter-d.MatterLost-total.Matter;
            evidence.oxygenResidual=d.InitialOxygen+d.OxygenIn+d.OxygenProduced-d.OxygenOut-d.OxygenUsed-total.Oxygen;
            evidence.history=new List<NicheHistoryPoint>(water.Overview.Survey.NicheHistory);
            evidence.frames=new[]{names[0]+".png",names[1]+".png",names[2]+".png",names[3]+".png",names[4]+".png",names[5]+".png",names[6]+".png","ecology-after-600s.png"};
            evidence.render=Context(biologicalShots.ToArray(),!failed);
            File.WriteAllText(Path.Combine(destination,"ecology.json"),JsonUtility.ToJson(evidence,true));
        }
    }
}
