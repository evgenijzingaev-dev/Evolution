using Evolution.Core;
using Evolution.Localization;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner
    {
        void RefreshSurvival(Cell c)
        {
            if (survivalId==c.Id && survivalTick>=0 && simulation.Tick>=survivalTick && simulation.Tick<survivalTick+4) return;
            survival=simulation.ReadSurvival(c); survivalId=c.Id; survivalTick=simulation.Tick;
        }
        static TextKey DietKey(Cell c) => c.Genes.Predation>.55f ? TextKey.DietHunter
            : c.Genes.Photosynthesis>.55f ? TextKey.DietSunlight
            : c.Genes.OrganicSpecialization>.6f ? TextKey.DietOrganicGuide : TextKey.DietGeneralGuide;
        void DrawReproductionReadiness(Cell c)
        {
            if (simulation.Count>=activeSettings.Capacity) GUILayout.Label(GameText.Get(TextKey.PopulationFull),hintStyle);
            else if (Biology.CanDivide(c)) GUILayout.Label(GameText.Get(TextKey.ReproductionReady),hintStyle);
            else if(c.Growth<1) GUILayout.Label(GameText.Format(TextKey.ReproductionGrowth,c.Growth),hintStyle);
            else if(c.Age<Multicellularity.MaturityAge(c)) GUILayout.Label(GameText.Format(TextKey.ReproductionYoung,Multicellularity.MaturityAge(c)-c.Age),hintStyle);
            else GUILayout.Label(GameText.Format(TextKey.DivisionDeficit,Mathf.Max(0,Biology.Capacity(c.Genes)*Multicellularity.DivisionThreshold(c)-c.Energy)),hintStyle);
        }
        void DrawActionIcons(Cell c)
        {
            Rect strip=GUILayoutUtility.GetRect(0,32,GUILayout.ExpandWidth(true));
            ActionIcon(new Rect(strip.x,strip.y,strip.width/4-3,28),TextKey.KeyAttack,CellAction.Attack,c);
            ActionIcon(new Rect(strip.x+strip.width/4,strip.y,strip.width/4-3,28),TextKey.KeyDefend,CellAction.Defend,c);
            ActionIcon(new Rect(strip.x+strip.width/2,strip.y,strip.width/4-3,28),TextKey.KeyBoost,CellAction.Boost,c);
            ActionIcon(new Rect(strip.x+strip.width*3/4,strip.y,strip.width/4-3,28),TextKey.KeyDivide,CellAction.Divide,c);
        }
        void ActionIcon(Rect rect,TextKey key,CellAction action,Cell c)
        {
            var block=simulation.ActionStatus(action); string explanation=GameText.Get(BlockKeys[(int)block]);
            bool available=block==ActionBlock.None;
            if(available && action==CellAction.Attack) {
                if(c.Energy>=Biology.Capacity(c.Genes)*.97f) { explanation=GameText.Get(TextKey.ActionSated); available=false; }
                else if(!Observation.TryFind(simulation,survival.PreyId,out var prey)) { explanation=GameText.Get(TextKey.ActionNoPrey); available=false; }
                else if(survival.PreyDistance>.32f*(c.Genes.Size+prey.Genes.Size)) { explanation=GameText.Get(TextKey.ActionApproach); available=false; }
            }
            Color previous=GUI.color;
            GUI.color=available?new Color(.65f,.91f,.76f):new Color(.82f,.66f,.5f);
            GUI.Box(rect,new GUIContent(GameText.Get(key),GameText.Format(TextKey.AbilityHint,GameText.Get(key),explanation)));
            GUI.color=previous;
        }
        void DrawWorldGuides()
        {
            if(!WorldHints || !IsControlling || !Observation.TryFind(simulation,simulation.ControlledId,out var c)) return;
            RefreshSurvival(c);
            if(survival.BetterHabitat) GuideMarker(new Vector3(survival.FoodX,.55f,survival.FoodZ),TextKey.GuideHabitat,
                new Color(.65f,.9f,.55f),survival.FoodDistance);
            if(Observation.TryFind(simulation,survival.PreyId,out var prey)) GuideMarker(organismRenderer.Position(prey),
                TextKey.GuidePrey,new Color(.95f,.72f,.37f),survival.PreyDistance);
            if(Observation.TryFind(simulation,survival.ThreatId,out var threat)) GuideMarker(organismRenderer.Position(threat),
                TextKey.GuideThreat,new Color(1,.45f,.35f),survival.ThreatDistance);
        }
        void GuideMarker(Vector3 point,TextKey label,Color color,float distance)
        {
            Vector3 screen=view.WorldToScreenPoint(rig.NearestImage(point));
            if(screen.z<=0) return;
            Vector2 p=new Vector2(screen.x/UiScale,(Screen.height-screen.y)/UiScale);
            Rect box=new Rect(p.x-87,p.y-45,174,25);
            if(box.x<10 || box.xMax>UiWidth-10 || box.y<98 || box.yMax>BottomPanel.y-5) return;
            if(box.Overlaps(LeftPanel)||box.Overlaps(RightPanel)) return;
            Color previous=GUI.color; GUI.color=color;
            // Screen-space brackets keep the real organism visible, independent of zoom.
            GUI.DrawTexture(new Rect(p.x-14,p.y-12,7,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x-14,p.y-12,2,7),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x+7,p.y+10,7,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x+12,p.y+5,2,7),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Box(box,GUIContent.none,panelStyle);
            GUI.Label(box,GameText.Format(TextKey.GuideLabel,GameText.Get(label),distance),markerStyle);
            GUI.color=previous;
        }
    }
}
