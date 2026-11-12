using Evolution.Core;
using Evolution.Localization;
using UnityEngine;

namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner
    {
        SurvivalReadout survival;
        long survivalTick = -1;
        int survivalId;
        bool showSelectionDetails, showHelp;
        static readonly TextKey[] SourceKeys = { TextKey.SourceNone, TextKey.SourceSubstrate, TextKey.SourceOrganic, TextKey.SourceLight, TextKey.SourcePrey, TextKey.SourceColony };
        static readonly TextKey[] DrainKeys = { TextKey.DrainNone, TextKey.DrainUpkeep, TextKey.DrainSwim, TextKey.DrainTurn, TextKey.DrainBoost,
            TextKey.DrainDefense, TextKey.DrainStress, TextKey.DrainRepair, TextKey.DrainGrowth, TextKey.DrainAttack, TextKey.DrainDivision, TextKey.DrainContact, TextKey.DrainPrey, TextKey.DrainColony };
        static readonly TextKey[] AdviceKeys = { TextKey.AdviceExplore, TextKey.AdviceEscape, TextKey.AdviceToxins, TextKey.AdviceBoost,
            TextKey.AdviceFood, TextKey.AdviceLight, TextKey.AdviceRecover, TextKey.AdviceDivide, TextKey.AdviceGrow };
        void DrawTactical(Cell c)
        {
            // A single local scan per four biological ticks, also valid on a paused selection.
            if (survivalId != c.Id || survivalTick < 0 || simulation.Tick < survivalTick || simulation.Tick >= survivalTick + 4) {
                survival = simulation.ReadSurvival(c); survivalId = c.Id; survivalTick = simulation.Tick;
            }
            float dt = simulation.StepSeconds;
            GUILayout.Label(GameText.Format(TextKey.EnergyReserve,c.Energy,Biology.Capacity(c.Genes)),titleStyle);
            DrawMeter(c.Energy/Biology.Capacity(c.Genes));
            GUILayout.Label(GameText.Format(TextKey.TacticalVitals,c.Integrity,c.Growth),hintStyle);
            GUILayout.Label(GameText.Format(TextKey.TacticalBalance,c.Budget.Net/dt));
            GUILayout.Label(GameText.Format(TextKey.TacticalSource,GameText.Get(SourceKeys[(int)survival.Source])),hintStyle);
            DrawIncomeBar(c.Budget,dt);
            GUILayout.Label(GameText.Format(TextKey.TacticalDrain,GameText.Get(DrainKeys[(int)survival.Drain])),hintStyle);
            GUILayout.Label(GameText.Get(AdviceKeys[(int)survival.Advice]));
            DrawSenseCompass(c);
            if (IsControlling) {
                Rect actions=GUILayoutUtility.GetRect(0,26,GUILayout.ExpandWidth(true));
                GUI.Label(actions,new GUIContent(GameText.Get(TextKey.ActionStrip),GameText.Format(TextKey.TacticalActions,
                    GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Attack)]),
                    GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Defend)]),
                    GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Boost)]))),hintStyle);
            }
            if (GUILayout.Button(GameText.Get(showSelectionDetails ? TextKey.InspectorClose : TextKey.InspectorOpen))) showSelectionDetails = !showSelectionDetails;
        }
        void DrawIncomeBar(EnergyBudget budget, float dt)
        {
            Rect bar = GUILayoutUtility.GetRect(0,8,GUILayout.ExpandWidth(true));
            float total = budget.Intake + Mathf.Max(0,budget.Transport), cursor = bar.x;
            Color previous = GUI.color;
            GUI.color = new Color(.07f,.18f,.21f,.85f); GUI.DrawTexture(bar,Texture2D.whiteTexture);
            if (total > 0) {
                IncomeSegment(bar,budget.Substrate,total,new Color(.3f,.78f,.63f),ref cursor);
                IncomeSegment(bar,budget.Organic,total,new Color(.75f,.54f,.27f),ref cursor);
                IncomeSegment(bar,budget.Light,total,new Color(.8f,.85f,.46f),ref cursor);
                IncomeSegment(bar,budget.Prey,total,new Color(.85f,.4f,.27f),ref cursor);
                IncomeSegment(bar,Mathf.Max(0,budget.Transport),total,new Color(.38f,.65f,.83f),ref cursor);
            }
            GUI.color = previous;
            GUI.Label(bar,new GUIContent(string.Empty,GameText.Format(TextKey.TacticalIntake,
                budget.Substrate/dt,budget.Organic/dt,budget.Light/dt,budget.Prey/dt,Mathf.Max(0,budget.Transport)/dt)),hintStyle);
        }
        static void IncomeSegment(Rect bar, float amount, float total, Color color, ref float cursor)
        {
            float width = bar.width*amount/total; GUI.color = color;
            GUI.DrawTexture(new Rect(cursor,bar.y,width,bar.height),Texture2D.whiteTexture); cursor += width;
        }
        void DrawSenseCompass(Cell c)
        {
            GUILayout.Label(GameText.Format(TextKey.SenseRange,survival.Range),hintStyle);
            Rect rect = GUILayoutUtility.GetRect(0,76,GUILayout.ExpandWidth(true));
            Vector2 origin = new Vector2(rect.x+38,rect.center.y);
            Color previous = GUI.color;
            GUI.color = new Color(.1f,.28f,.3f,.7f);
            GUI.DrawTexture(new Rect(origin.x-31,origin.y-1,62,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(origin.x-1,origin.y-31,2,62),Texture2D.whiteTexture);
            GUI.color = new Color(.7f,.95f,.87f);
            GUI.DrawTexture(new Rect(origin.x-2,origin.y-2,4,4),Texture2D.whiteTexture);
            if (survival.BetterHabitat) SenseNeedle(origin,c,survival.FoodX,survival.FoodZ,new Color(.6f,.88f,.5f));
            bool prey = Observation.TryFind(simulation,survival.PreyId,out var p);
            bool threat = Observation.TryFind(simulation,survival.ThreatId,out var t);
            if (prey) SenseNeedle(origin,c,p.X,p.Z,new Color(.94f,.69f,.35f));
            if (threat) SenseNeedle(origin,c,t.X,t.Z,new Color(1,.38f,.3f));
            GUI.color = previous;
            GUI.Label(new Rect(rect.x+86,rect.y,rect.width-86,76),GameText.Format(TextKey.SenseLegend,
                survival.BetterHabitat ? GameText.Format(TextKey.SenseDistance,survival.FoodDistance) : GameText.Get(TextKey.SenseUnknown),
                threat ? GameText.Format(TextKey.SenseDistance,survival.ThreatDistance) : GameText.Get(TextKey.SenseUnknown),
                prey ? GameText.Format(TextKey.SenseDistance,survival.PreyDistance) : GameText.Get(TextKey.SenseUnknown)),hintStyle);
        }
        void SenseNeedle(Vector2 origin, Cell c, float x, float z, Color color)
        {
            var delta = new Vector3(Observation.PeriodicDelta(x-c.X,simulation.WorldSize),0,Observation.PeriodicDelta(z-c.Z,simulation.WorldSize));
            // Match the current camera's screen axes, including rotation during follow mode.
            float angle = Mathf.Atan2(Vector3.Dot(delta,view.transform.up),Vector3.Dot(delta,view.transform.right))*Mathf.Rad2Deg;
            var old = GUI.matrix; GUI.color = color; GUIUtility.RotateAroundPivot(-angle,origin);
            GUI.DrawTexture(new Rect(origin.x+8,origin.y-1,22,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(origin.x+25,origin.y-3,5,6),Texture2D.whiteTexture);
            GUI.matrix = old;
        }
        void DrawCurrentGoal()
        {
            if (!Journey.Active) { GUILayout.Label(GameText.Get(TextKey.GoalsIntro),hintStyle); return; }
            int limit = activeSettings.Ecosystem ? GoalKeys.Length : 7;
            // Early food/reserve goals prepare the player for paid reproduction and inheritance.
            int[] order = GoalOrder;
            foreach (int i in order) if (i < limit && !Journey.Completed((EvolutionGoal)i)) {
                GUILayout.Label(GameText.Format(TextKey.CurrentGoal,GameText.Get(GoalKeys[i]),Journey.Progress((EvolutionGoal)i)),hintStyle);
                DrawMeter(Journey.Progress((EvolutionGoal)i)); return;
            }
            GUILayout.Label(GameText.Get(TextKey.StageComplete),hintStyle);
        }
        static readonly int[] GoalOrder = { 1,2,3,4,0,5,6,7,8,9,10,11 };
        void DrawSuccession()
        {
            GUILayout.Label(GameText.Get(TextKey.SuccessionTitle),titleStyle);
            GUILayout.Label(GameText.Get(TextKey.SuccessionHelp),hintStyle);
            int shown = 0;
            for (int i = 0; i < simulation.Count && shown < 4; i++) {
                var heir = simulation.GetCell(i);
                if (!Journey.Contains(heir.Id)) continue;
                shown++;
                if (GUILayout.Button(GameText.Format(TextKey.ChooseHeir,heir.Id,heir.Generation))) { SelectCell(heir.Id); PlaySelected(); break; }
            }
            if (shown == 0) GUILayout.Label(GameText.Get(TextKey.LineageLost),hintStyle);
        }
    }
}
