using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Evolution.Core;
using Evolution.Presentation;
using Evolution.Volume;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class WaterVisualPreview
    {
        [Serializable] sealed class LivingEvidence
        {
            public string unity,revision,capturedUtc,scene="WaterPrototype3D";
            public bool modelDriven=true,fixture=false;
            public List<MovementEvidence> movement=new List<MovementEvidence>();
            public string deathIdentity,deathCause;
            public double deathTime;
            public long deathSequence;
            public int deathFrames;
        }
        [Serializable] sealed class MovementEvidence
        {
            public string form,identity;
            public Genome genes;
            public Vector3 start,end;
            public long firstTick,lastTick;
            public float firstEnergy,lastEnergy;
            public int frames;
        }
        // Explicitly opt-in with BOTH preview flags. Uses existing residents and ordinary commands.
        // Does not alter genomes, energy, births, death rules, food, persistence or simulation positions.
        IEnumerator CaptureLivingCells()
        {
            var evidence=new LivingEvidence {unity=Application.unityVersion,revision=Context(new Shot[0],true).revision,capturedUtc=DateTime.UtcNow.ToString("O")};
            water.HudVisible=false;water.FindFood=water.ShowCurrents=water.ShowTemperature=false;
            foreach(var form in new[] {CrystalForm.Amoeboid,CrystalForm.Ciliate,CrystalForm.Spindle}) {
                int selected=-1;float nearest=float.MaxValue;
                for(int i=0;i<water.World.Count;i++) {
                    var a=water.World.Get(i);if(!a.Alive||CrystalForms.From(a.Biology.Genes)!=form||water.World.OrganismSize(i)!=1)continue;
                    float d=(a.Position-water.World.ActiveCenter).Sqr;
                    if(d<nearest){nearest=d;selected=i;}
                }
                Require(selected>=0,"seed lacks a naturally existing "+form+" resident");
                var first=water.World.Get(selected);water.Select(selected);water.PlaySelected();
                var item=new MovementEvidence {form=form.ToString(),identity=first.Identity,genes=first.Biology.Genes,start=ToVector(first.Position),firstTick=water.World.Tick,firstEnergy=first.Biology.Energy};
                for(int frame=0;frame<40;frame++) {
                    int index=water.Ocean.Find(first.Identity);Require(index>=0&&water.World.Get(index).Alive,"movement resident was lost");
                    var a=water.World.Get(index);
                    SetView(ToVector(a.Position),frame<30?4.3f:6,Mathf.Atan2(a.Direction.X,a.Direction.Z)*Mathf.Rad2Deg+90,frame<30?18:-20,false,false,false,false);
                    yield return Capture("living-"+form.ToString().ToLowerInvariant()+"-"+frame.ToString("D3"),false);
                    index=water.Ocean.Find(first.Identity);Require(index>=0,"camera unloaded movement resident");
                    water.World.Controlled=index;water.World.Input=frame<20?new V3(1,0,0):new V3(.4f,.2f,1).Unit;water.World.Step();item.frames++;
                }
                var last=water.World.Get(water.Ocean.Find(first.Identity));
                item.end=ToVector(last.Position);item.lastTick=water.World.Tick;item.lastEnergy=last.Biology.Energy;
                Require((item.end-item.start).sqrMagnitude>.0001f,"no actual organism movement");evidence.movement.Add(item);
            }
            yield return CaptureDivision(false);
            water.HudVisible=false;water.Observe();
            long cursor=water.World.Life.Latest;LifeEvent death=default;
            for(int tick=0;tick<2400&&death.Sequence==0;tick++) {
                water.World.Step();
                var journal=water.World.Life;
                for(long seq=Math.Max(cursor+1,journal.Oldest);seq<=journal.Latest;seq++)
                    if(journal.TryRead(seq,out var e)&&e.Kind==LifeEventKind.Death&&(e.Agent.Position-water.World.ActiveCenter).Sqr<20*20){death=e;break;}
                cursor=journal.Latest;if(tick%20==0&&death.Sequence==0)yield return null;
            }
            Require(death.Sequence!=0,"no real nearby death within the observation window");
            evidence.deathIdentity=death.Agent.Identity;evidence.deathCause=death.Cause.ToString();evidence.deathTime=death.Time;evidence.deathSequence=death.Sequence;
            SetView(ToVector(death.Agent.Position),4.5f,35,18,false,false,false,false);
            for(int frame=0;frame<=80;frame++) {
                yield return Capture("living-death-"+frame.ToString("D3"),false);evidence.deathFrames++;
                water.World.Step();
            }
            File.WriteAllText(Path.Combine(destination,"living-cells.json"),JsonUtility.ToJson(evidence,true));
        }
    }
}
