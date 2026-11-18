using System.Text;
using Evolution.Core;
using Evolution.Localization;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner
    {
        bool showEnergyDetails, showAncestry;
        static readonly TextKey[] BlockKeys = { TextKey.ActionReady, TextKey.ActionNoControl, TextKey.ActionPhysiology,
            TextKey.ActionEnergy, TextKey.ActionImmature, TextKey.ActionFull, TextKey.ActionBracing };
        void DrawCellGameplay(Cell c)
        {
            var b = c.Budget; float dt = activeSettings.StepSeconds;
            GUILayout.Label(GameText.Format(TextKey.EnergyReserve, c.Energy, Biology.Capacity(c.Genes)), titleStyle);
            DrawMeter(c.Energy / Biology.Capacity(c.Genes));
            GUILayout.Label(GameText.Format(TextKey.EnergyFlow, b.Intake / dt, b.Spent / dt));
            GUILayout.Label(GameText.Format(TextKey.Integrity, c.Integrity)); DrawMeter(c.Integrity);
            GUILayout.Label(GameText.Format(TextKey.Hunger, 1 - c.Energy / Biology.Capacity(c.Genes), c.Growth));
            DrawMeter(c.Growth);
            var local = simulation.EnvironmentAt(c.X,c.Z);
            GUILayout.Label(GameText.Format(TextKey.Phenotype, GameText.Get(StrategyKeys[(int)Ecology.Strategy(c)]), c.LineageId));
            GUILayout.Label(GameText.Format(TextKey.LocalEcology, GameText.Get(ZoneKeys[(int)local.Zone]), local.Light, local.Toxicity), hintStyle);
            simulation.LocalFood(c,out float substrate,out float organic);
            GUILayout.Label(GameText.Format(TextKey.FoodNearby, substrate, organic), hintStyle);
            if (simulation.Count >= activeSettings.Capacity) GUILayout.Label(GameText.Get(TextKey.PopulationFull));
            else if (Biology.CanDivide(c)) GUILayout.Label(GameText.Get(TextKey.ReproductionReady));
            else if (c.Generation > 0 && c.Growth < 1) GUILayout.Label(GameText.Format(TextKey.ReproductionGrowth, c.Growth));
            else if (c.Age < Multicellularity.MaturityAge(c)) GUILayout.Label(GameText.Format(TextKey.ReproductionYoung, Multicellularity.MaturityAge(c) - c.Age));
            else GUILayout.Label(GameText.Format(TextKey.ReproductionEnergy, Mathf.Clamp01(c.Energy / (Biology.Capacity(c.Genes) * Multicellularity.DivisionThreshold(c)))));
            if (IsControlling) {
                GUILayout.Label(GameText.Format(TextKey.ActionAttack, GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Attack)])), hintStyle);
                GUILayout.Label(GameText.Format(TextKey.ActionBoost, GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Boost)])), hintStyle);
                GUILayout.Label(GameText.Format(TextKey.ActionDefend, GameText.Get(BlockKeys[(int)simulation.ActionStatus(CellAction.Defend)])), hintStyle);
            }
            if (GUILayout.Button(GameText.Get(showEnergyDetails ? TextKey.EnergyDetailsHide : TextKey.EnergyDetails))) showEnergyDetails = !showEnergyDetails;
            if (showEnergyDetails) {
                GUILayout.Label(GameText.Format(TextKey.EnergySources, b.Substrate/dt, b.Organic/dt, b.Light/dt, b.Prey/dt));
                GUILayout.Label(GameText.Format(TextKey.EnergyMaintenance, b.Upkeep/dt, b.Stress/dt));
                GUILayout.Label(GameText.Format(TextKey.EnergyMovement, b.Movement/dt));
                GUILayout.Label(GameText.Format(TextKey.EnergyRecovery, b.Defense/dt, b.Repair/dt, b.Growth/dt, b.Attack/dt));
                GUILayout.Label(GameText.Format(TextKey.EnergyTransport, b.Transport/dt));
                GUILayout.Label(GameText.Format(TextKey.EnergyDivision, b.Division, b.Contact));
                GUILayout.Label(GameText.Format(TextKey.EnergyBalance, b.Net/dt));
                GUILayout.Label(GameText.Get(TextKey.FoodTip), hintStyle);
                GUILayout.Label(GameText.Get(TextKey.GrowthHelp), hintStyle);
            }
            if (c.BirthMutationMask != 0) {
                var changes = new StringBuilder();
                for (int bit = 0; bit < MutationKeys.Length; bit++) if ((c.BirthMutationMask & (1 << bit)) != 0) {
                    if (changes.Length > 0) changes.Append(", "); changes.Append(GameText.Get(MutationKeys[bit]));
                }
                GUILayout.Label(GameText.Format(TextKey.MutationSummary, changes.ToString()), hintStyle);
            }
            if (GUILayout.Button(GameText.Get(showAncestry ? TextKey.AncestryHide : TextKey.AncestryShow))) showAncestry = !showAncestry;
            if (showAncestry) DrawAncestry(c);
            if (c.ParentId > 0) {
                GUILayout.Label(GameText.Format(TextKey.InheritedComparison, c.ParentGenes.Speed, c.Genes.Speed, c.ParentGenes.Size, c.Genes.Size), hintStyle);
                GUILayout.Label(GameText.Format(TextKey.InheritedDiet, c.ParentGenes.Photosynthesis, c.Genes.Photosynthesis, c.ParentGenes.Predation, c.Genes.Predation), hintStyle);
            }
        }
        void DrawAncestry(Cell c)
        {
            int ancestor = c.Id;
            for (int depth = 0; depth < 6 && ancestor > 0; depth++) {
                if (!simulation.History.TryFind(ancestor, out var entry)) { GUILayout.Label(GameText.Get(TextKey.AncestryEvicted),hintStyle); break; }
                GUILayout.Label(GameText.Format(TextKey.AncestryRecord, entry.Id, entry.ParentId, entry.Generation,
                    entry.DiedAtTick < 0 ? GameText.Get(TextKey.AncestryAlive) : GameText.Get(TextKey.AncestryDead)),hintStyle);
                if (entry.DiedAtTick < 0 && entry.Id != c.Id && GUILayout.Button(GameText.Format(TextKey.AncestrySelect,entry.Id))) SelectCell(entry.Id);
                ancestor = entry.ParentId;
            }
            int shown = 0;
            for (int i = 0; i < simulation.History.Count && shown < 6; i++) {
                var entry = simulation.History.Recent(i); if (entry.ParentId != c.Id) continue; shown++;
                GUILayout.Label(GameText.Format(TextKey.DescendantRecord, entry.Id, entry.Generation,
                    entry.DiedAtTick < 0 ? GameText.Get(TextKey.AncestryAlive) : GameText.Get(TextKey.AncestryDead)),hintStyle);
                if (entry.DiedAtTick < 0 && GUILayout.Button(GameText.Format(TextKey.AncestrySelect,entry.Id))) SelectCell(entry.Id);
            }
            GUILayout.Label(GameText.Format(TextKey.AncestryLimit,simulation.History.Capacity),hintStyle);
        }
        void DrawOceanMap()
        {
            GUILayout.Label(GameText.Get(TextKey.OceanMap));
            Rect map = GUILayoutUtility.GetRect(230, 130);
            Color previous = GUI.color;
            const int resolution = 16;
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++) {
                var sample = simulation.EnvironmentAt((x + .5f) / resolution * activeSettings.WorldSize,
                    (z + .5f) / resolution * activeSettings.WorldSize);
                GUI.color = Color.Lerp(new Color(.025f,.08f,.2f),new Color(.12f,.55f,.38f),sample.Light);
                GUI.color = Color.Lerp(GUI.color,new Color(.6f,.22f,.4f),sample.Toxicity);
                GUI.DrawTexture(new Rect(map.x + map.width*x/resolution,map.yMax - map.height*(z+1)/resolution,
                    map.width/resolution+1,map.height/resolution+1),Texture2D.whiteTexture);
            }
            Vector3 focus = rig.FocusPoint; GUI.color = Color.white;
            GUI.DrawTexture(new Rect(map.x + Mathf.Repeat(focus.x / activeSettings.WorldSize,1)*map.width-2,
                map.yMax - Mathf.Repeat(focus.z / activeSettings.WorldSize,1)*map.height-2,4,4),Texture2D.whiteTexture);
            GUI.color = previous;
            if (Event.current.type == EventType.MouseDown && map.Contains(Event.current.mousePosition)) {
                ObserveWorld(); rig.FocusOn(new Vector3((Event.current.mousePosition.x-map.x)/map.width*activeSettings.WorldSize,
                    .54f,(map.yMax-Event.current.mousePosition.y)/map.height*activeSettings.WorldSize),false,20);
                Event.current.Use();
            }
            GUILayout.Label(GameText.Get(TextKey.OceanMapHelp),hintStyle);
        }
    }
}
