using System.Text;
using Evolution.Core;
using Evolution.Localization;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed partial class EvolutionRunner
    {
        GUISkin hudSkin;
        Texture2D panelTexture;
        GUIStyle panelStyle, titleStyle, hintStyle;
        Vector2 selectionScroll, environmentScroll;
        bool showEnvironment, showGenes, confirmRestart, showGoals;
        float UiScale => Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), .65f, 1.5f);
        float UiWidth => Screen.width / UiScale;
        float UiHeight => Screen.height / UiScale;
        Rect TopPanel => new Rect(20, 16, UiWidth - 40, 82);
        Rect LeftPanel => new Rect(20, 106, 300, Mathf.Min(showEnvironment ? 760 : showGoals ? 700 : 300, UiHeight - 250));
        Rect RightPanel => new Rect(UiWidth - 370, 106, 350, selectedId == 0 ? 180 : UiHeight - 250);
        Rect BottomPanel => new Rect(20, UiHeight - 112, UiWidth - 40, 92);
        bool PointerOverUI(Vector3 pointer)
        {
            Vector2 point = new Vector2(pointer.x / UiScale, (Screen.height - pointer.y) / UiScale);
            return TopPanel.Contains(point) || LeftPanel.Contains(point) || RightPanel.Contains(point) || BottomPanel.Contains(point);
        }
        void PrepareHud()
        {
            if (hudSkin) return;
            hudSkin = Instantiate(GUI.skin);
            hudSkin.label.fontSize = 16; hudSkin.label.wordWrap = true;
            hudSkin.label.normal.textColor = new Color(.81f, .91f, .93f);
            hudSkin.label.margin = new RectOffset(2, 2, 5, 5);
            hudSkin.button.fontSize = 16; hudSkin.button.fixedHeight = 32;
            hudSkin.button.normal.textColor = new Color(.82f, 1, .94f);
            hudSkin.button.margin = new RectOffset(2, 2, 5, 5);
            hudSkin.horizontalSlider.margin = new RectOffset(4, 4, 12, 12);
            panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, new Color(.018f, .047f, .065f, .78f)); panelTexture.Apply();
            panelStyle = new GUIStyle(hudSkin.box) { padding = new RectOffset(18, 18, 14, 14) };
            panelStyle.normal.background = panelTexture;
            titleStyle = new GUIStyle(hudSkin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.55f, 1, .84f);
            hintStyle = new GUIStyle(hudSkin.label) { fontSize = 15 };
            hintStyle.normal.textColor = new Color(.53f, .72f, .78f);
        }
        void OnGUI()
        {
            if (simulation == null) return;
            PrepareHud(); var oldMatrix = GUI.matrix; var oldSkin = GUI.skin;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale); GUI.skin = hudSkin;
            GUILayout.BeginArea(TopPanel, panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(470));
            GUILayout.Label(GameText.Get(TextKey.Title), titleStyle);
            GUILayout.Label(GameText.Get(IsControlling ? TextKey.ControlledMode : TextKey.Observe), hintStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(GameText.Format(TextKey.TimeShort, simulation.Tick * activeSettings.StepSeconds), GUILayout.Width(200));
            if (GUILayout.Button(GameText.Get(paused ? TextKey.Resume : TextKey.Pause), GUILayout.Width(145))) paused = !paused;
            foreach (float value in Speeds) {
                GUI.backgroundColor = speed == value ? new Color(.4f, .95f, .75f) : Color.white;
                if (GUILayout.Button(GameText.Format(TextKey.TimeScale, value), GUILayout.Width(62))) speed = value;
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(GameText.Get(TextKey.Overview), GUILayout.Width(170))) { ObserveWorld(); rig.Overview(); }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
            GUILayout.BeginArea(LeftPanel, panelStyle);
            environmentScroll = GUILayout.BeginScrollView(environmentScroll);
            GUILayout.Label(GameText.Format(TextKey.PopulationShort, simulation.Count, simulation.ColonyCount), titleStyle);
            GUILayout.Label(GameText.Format(TextKey.Population, simulation.Count, activeSettings.Capacity, simulation.Births, simulation.Deaths));
            GUILayout.Label(GameText.Format(TextKey.Colonies, simulation.ColonyCount, simulation.ColonialCells, simulation.LargestColony));
            if (GUILayout.Button(GameText.Get(colorMode == 0 ? TextKey.TemperatureColors : colorMode == 1 ? TextKey.DietColors : TextKey.RoleColors))) colorMode = (colorMode + 1) % 3;
            GUILayout.Label(GameText.Get(colorMode == 2 ? TextKey.LegendCompact : colorMode == 1 ? TextKey.DietLegend : TextKey.TemperatureLegend), hintStyle);
            if (GUILayout.Button(GameText.Get(showEnvironment ? TextKey.CloseEnvironment : TextKey.Environment))) showEnvironment = !showEnvironment;
            if (showEnvironment) DrawEnvironment();
            if (GUILayout.Button(GameText.Get(showGoals ? TextKey.HideGoals : TextKey.ShowGoals))) showGoals = !showGoals;
            if (showGoals) DrawJourney();
            if (simulation.Count == 0) GUILayout.Label(GameText.Get(TextKey.Extinction));
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(RightPanel, panelStyle);
            selectionScroll = GUILayout.BeginScrollView(selectionScroll);
            if (Observation.TryFind(simulation, selectedId, out var selected)) DrawSelection(selected);
            else {
                GUILayout.Label(GameText.Get(TextKey.NothingSelected), titleStyle);
                GUILayout.Label(GameText.Get(TextKey.PickHelp));
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(BottomPanel, panelStyle);
            GUILayout.Label(GameText.Get(IsControlling ? TextKey.PlayHelp : TextKey.ControlHelp), hintStyle);
            if (!IsControlling) GUILayout.Label(GameText.Get(TextKey.ShortcutHelp), hintStyle);
            GUILayout.EndArea(); GUI.matrix = oldMatrix; GUI.skin = oldSkin;
        }
        static readonly TextKey[] GoalKeys = { TextKey.GoalSurvive, TextKey.GoalFeed, TextKey.GoalReserve, TextKey.GoalReproduce, TextKey.GoalInherit, TextKey.GoalAdapt, TextKey.GoalColony };
        void DrawMeter(float progress)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));
            Color previous = GUI.color; GUI.color = new Color(.07f,.18f,.21f,.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture); rect.width *= Mathf.Clamp01(progress);
            GUI.color = Color.Lerp(new Color(.85f,.45f,.2f),new Color(.3f,.9f,.75f),Mathf.Clamp01(progress));
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        void DrawJourney()
        {
            if (!Journey.Active) { GUILayout.Label(GameText.Get(TextKey.GoalsIntro), hintStyle); return; }
            GUILayout.Label(GameText.Format(TextKey.LineageTitle, Journey.RootId, Journey.CompletedCount, Journey.AchievementCount), titleStyle);
            for (int i = 0; i < GoalKeys.Length; i++) {
                var goal = (EvolutionGoal)i;
                GUILayout.Label(GameText.Format(TextKey.GoalProgress, GameText.Get(GoalKeys[i]), Journey.Progress(goal)), hintStyle);
                DrawMeter(Journey.Progress(goal));
            }
            GUILayout.Label(GameText.Get(TextKey.GoalRules), hintStyle);
            if (IsControlling && !Journey.Completed(EvolutionGoal.Adapt)) {
                if (GUILayout.Button(GameText.Get(TextKey.ThermalChallenge))) {
                    pendingTemperature = simulation.Temperature <= 44 ? simulation.Temperature + 6 : simulation.Temperature - 6;
                    environmentPending = true;
                }
            }
            if (Journey.CompletedCount == GoalKeys.Length) GUILayout.Label(GameText.Get(TextKey.StageComplete));
            if (!IsControlling) {
                int shown = 0;
                for (int i = 0; i < simulation.Count && shown < 3; i++) {
                    var c = simulation.GetCell(i);
                    if (!Journey.Contains(c.Id) || c.Generation <= Journey.FounderGeneration) continue;
                    shown++;
                    if (GUILayout.Button(GameText.Format(TextKey.ChooseHeir, c.Id, c.Generation))) { SelectCell(c.Id); PlaySelected(); break; }
                }
                if (Journey.LivingDescendants == 0) GUILayout.Label(GameText.Get(TextKey.LineageLost), hintStyle);
            }
        }
        void DrawEnvironment()
        {
            GUILayout.Label(GameText.Format(TextKey.Temperature, pendingTemperature));
            float temperature = GUILayout.HorizontalSlider(pendingTemperature, 0, 50);
            GUILayout.Label(GameText.Format(TextKey.Regeneration, pendingFood));
            float food = GUILayout.HorizontalSlider(pendingFood, 0, .5f);
            if (temperature != pendingTemperature || food != pendingFood) { pendingTemperature = temperature; pendingFood = food; environmentPending = true; }
            if (environmentPending) GUILayout.Label(GameText.Get(TextKey.PendingEnvironment), hintStyle);
            GUILayout.Label(GameText.Get(TextKey.FoodLegend), hintStyle);
            if (!confirmRestart) {
                if (GUILayout.Button(GameText.Get(TextKey.Restart))) confirmRestart = true;
            } else {
                GUILayout.Label(GameText.Get(TextKey.RestartWarning));
                if (GUILayout.Button(GameText.Get(TextKey.RestartConfirm))) { ResetWorld(); confirmRestart = false; }
                if (GUILayout.Button(GameText.Get(TextKey.RestartCancel))) confirmRestart = false;
            }
        }
        static readonly TextKey[] RoleKeys = { TextKey.GeneralistRole, TextKey.FeedingRole, TextKey.MovementRole, TextKey.DefenseRole, TextKey.ReproductionRole };
        static readonly TextKey[] MutationKeys = { TextKey.MutationSpeed, TextKey.MutationSize, TextKey.MutationUptake, TextKey.MutationTemperature, TextKey.MutationDiet, TextKey.MutationAdhesion, TextKey.MutationColonySize, TextKey.MutationBranching, TextKey.MutationDifferentiation, TextKey.MutationTransport, TextKey.MutationPattern };
        void DrawSelection(Cell c)
        {
            int count = 0, feeding = 0, moving = 0, defense = 0, reproduction = 0, generalists = 0;
            float energy = 0, capacity = 0;
            for (int i = 0; i < simulation.Count; i++) {
                var member = simulation.GetCell(i); if (!Observation.SameOrganism(c, member)) continue;
                count++; energy += member.Energy; capacity += Biology.Capacity(member.Genes);
                if (member.Specialization < .25f) { generalists++; continue; }
                switch (member.Role) {
                    case CellRole.Feeding: feeding++; break;
                    case CellRole.Movement: moving++; break;
                    case CellRole.Defense: defense++; break;
                    case CellRole.Reproduction: reproduction++; break;
                    default: generalists++; break;
                }
            }
            GUILayout.Label(GameText.Format(TextKey.OrganismTitle, c.ColonyId == 0 ? c.Id : c.ColonyId, count), titleStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GameText.Get(TextKey.Focus))) FocusSelected(false);
            if (GUILayout.Button(GameText.Get(rig.Following ? TextKey.StopFollow : TextKey.Follow))) { if (rig.Following) rig.StopFollowing(); else FocusSelected(true); }
            GUILayout.EndHorizontal();
            if (IsControlling) {
                if (GUILayout.Button(GameText.Get(TextKey.ReturnObserve))) ObserveWorld();
                bool wasEnabled = GUI.enabled; GUI.enabled = !paused && Biology.CanDivide(c) && simulation.Count < activeSettings.Capacity;
                if (GUILayout.Button(GameText.Get(TextKey.Divide))) simulation.RequestDivision();
                GUI.enabled = wasEnabled;
            } else if (GUILayout.Button(GameText.Get(TextKey.PlayOrganism))) PlaySelected();
            if (simulation.LastDivisionParentId == c.Id && Observation.TryFind(simulation, simulation.LastOffspringId, out _)) {
                if (GUILayout.Button(GameText.Get(TextKey.PlayChild))) PlayOffspring();
            }
            GUILayout.Label(GameText.Format(TextKey.ColonyEnergy, energy, capacity));
            GUILayout.Label(GameText.Format(TextKey.RolesSummary, feeding, moving, defense, reproduction));
            GUILayout.Label(GameText.Format(TextKey.Generalists, generalists));
            GUILayout.Space(10);
            GUILayout.Label(GameText.Format(TextKey.CellIdentity, c.Id, c.ParentId), hintStyle);
            GUILayout.Label(GameText.Format(TextKey.AgeGeneration, c.Age, c.Generation));
            GUILayout.Label(GameText.Format(TextKey.RoleDetail, GameText.Get(RoleKeys[(int)c.Role]), c.Specialization));
            GUILayout.Label(GameText.Format(TextKey.EnergyReserve, c.Energy, Biology.Capacity(c.Genes)));
            DrawMeter(c.Energy / Biology.Capacity(c.Genes));
            var local = simulation.EnvironmentAt(c.X,c.Z);
            GUILayout.Label(GameText.Format(TextKey.LocalEnvironment, local.Temperature, new Vector2(local.CurrentX,local.CurrentZ).magnitude));
            GUILayout.Label(GameText.Format(TextKey.Vitality, Mathf.Clamp01(c.Energy / Biology.Capacity(c.Genes))));
            GUILayout.Label(GameText.Get(TextKey.VitalityHelp), hintStyle);
            if (simulation.Count >= activeSettings.Capacity) GUILayout.Label(GameText.Get(TextKey.PopulationFull));
            else if (Biology.CanDivide(c)) GUILayout.Label(GameText.Get(TextKey.ReproductionReady));
            else if (c.Age < Multicellularity.MaturityAge(c)) GUILayout.Label(GameText.Format(TextKey.ReproductionYoung, Multicellularity.MaturityAge(c) - c.Age));
            else GUILayout.Label(GameText.Format(TextKey.ReproductionEnergy, Mathf.Clamp01(c.Energy / (Biology.Capacity(c.Genes) * Multicellularity.DivisionThreshold(c)))));
            if (GUILayout.Button(GameText.Get(showGenes ? TextKey.HideGenes : TextKey.Genes))) showGenes = !showGenes;
            if (showGenes) {
                var g = c.Genes;
                GUILayout.Label(GameText.Format(TextKey.GeneMotion, g.Speed, g.Size));
                GUILayout.Label(GameText.Format(TextKey.GeneFeeding, g.Uptake, g.OrganicSpecialization));
                GUILayout.Label(GameText.Format(TextKey.GeneTemperature, g.PreferredTemperature));
                GUILayout.Label(GameText.Format(TextKey.GeneAdhesion, g.Adhesion, g.Transport));
                GUILayout.Label(GameText.Format(TextKey.GeneStructure, Multicellularity.SizeLimit(g, activeSettings.MaxColonyCells), g.Branching));
                GUILayout.Label(GameText.Format(TextKey.GeneRoles, g.Differentiation, g.RolePattern));
                var mutations = new StringBuilder();
                for (int i = 0; i < MutationKeys.Length; i++) if ((c.BirthMutationMask & (1 << i)) != 0) {
                    if (mutations.Length > 0) mutations.Append(", "); mutations.Append(GameText.Get(MutationKeys[i]));
                }
                if (c.Generation == 0) GUILayout.Label(GameText.Get(TextKey.FounderGenome), hintStyle);
                else GUILayout.Label(GameText.Format(TextKey.MutationSummary, mutations.Length == 0 ? GameText.Get(TextKey.NoMutations) : mutations.ToString()), hintStyle);
            }
        }
    }
}
