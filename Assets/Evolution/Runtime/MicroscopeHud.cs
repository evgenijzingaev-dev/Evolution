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
        GUIStyle panelStyle, headerStyle, titleStyle, hintStyle, markerStyle;
        Vector2 selectionScroll, environmentScroll;
        bool showEnvironment, showGenes, confirmRestart, showGoals, showJournal;
        float UiScale => Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), .65f, 1.5f);
        float UiWidth => Screen.width / UiScale;
        float UiHeight => Screen.height / UiScale;
        Rect TopPanel => new Rect(20, 16, UiWidth - 40, 72);
        Rect LeftPanel => new Rect(20, 98, 290, Mathf.Min(showEnvironment || showGoals || showJournal ? 740 : 194, UiHeight - 210));
        Rect RightPanel => new Rect(UiWidth - 360, 98, 340, Mathf.Min(selectedId == 0 && !Journey.Active ? 180 : showSelectionDetails ? 740 : 570, UiHeight - (showHelp ? 254 : 210)));
        Rect BottomPanel => new Rect(20, UiHeight - (showHelp ? 140 : 92), UiWidth - 40, showHelp ? 124 : 76);
        bool PointerOverUI(Vector3 pointer)
        {
            Vector2 point = new Vector2(pointer.x / UiScale, (Screen.height - pointer.y) / UiScale);
            return HudVisible && (TopPanel.Contains(point) || LeftPanel.Contains(point) || RightPanel.Contains(point) || BottomPanel.Contains(point));
        }
        void PrepareHud()
        {
            if (hudSkin) return;
            hudSkin = Instantiate(GUI.skin);
            hudSkin.label.fontSize = 16; hudSkin.label.wordWrap = true;
            hudSkin.label.normal.textColor = new Color(.81f, .91f, .93f);
            hudSkin.label.margin = new RectOffset(2, 2, 3, 3);
            hudSkin.button.fontSize = 16; hudSkin.button.wordWrap = true;
            hudSkin.button.fixedHeight = 0;
            hudSkin.button.padding = new RectOffset(8, 8, 7, 7);
            hudSkin.button.normal.textColor = new Color(.82f, 1, .94f);
            hudSkin.button.margin = new RectOffset(2, 2, 3, 3);
            hudSkin.box.fontSize=15; hudSkin.box.alignment=TextAnchor.MiddleCenter;
            hudSkin.horizontalSlider.margin = new RectOffset(4, 4, 12, 12);
            panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, new Color(.018f, .047f, .065f, .78f)); panelTexture.Apply();
            panelStyle = new GUIStyle(hudSkin.box) { padding = new RectOffset(16, 16, 12, 12) };
            panelStyle.normal.background = panelTexture;
            headerStyle=new GUIStyle(panelStyle) { padding=new RectOffset(16,16,6,6) };
            titleStyle = new GUIStyle(hudSkin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.55f, 1, .84f);
            hintStyle = new GUIStyle(hudSkin.label) { fontSize = 15,margin=new RectOffset(2,2,2,2) };
            hintStyle.normal.textColor = new Color(.53f, .72f, .78f);
            markerStyle=new GUIStyle(hintStyle) { alignment=TextAnchor.MiddleCenter,wordWrap=false,fontSize=14 };
            markerStyle.normal.textColor=new Color(.9f,.97f,.93f);
        }
        void BeginPanelScroll(ref Vector2 scroll, Rect panel)
        {
            // IMGUI otherwise sizes scroll content from the longest control and clips wrapped labels.
            scroll.x = 0;
            scroll = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, hudSkin.verticalScrollbar);
            float scrollbarWidth = hudSkin.verticalScrollbar.fixedWidth + hudSkin.verticalScrollbar.margin.horizontal;
            float contentWidth = Mathf.Max(1, panel.width - panelStyle.padding.horizontal - scrollbarWidth - 4);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
        }
        static void EndPanelScroll()
        {
            GUILayout.EndVertical(); GUILayout.EndScrollView();
        }
        void OnGUI()
        {
            if (simulation == null || !HudVisible) return;
            PrepareHud(); var oldMatrix = GUI.matrix; var oldSkin = GUI.skin;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale); GUI.skin = hudSkin;
            DrawWorldGuides();
            GUILayout.BeginArea(TopPanel, headerStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(200,UiWidth-1180)));
            GUILayout.Label(GameText.Get(TextKey.Title), titleStyle);
            GUILayout.Label(GameText.Get(IsControlling ? TextKey.ControlledMode : TextKey.Observe), hintStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(GameText.Format(TextKey.TimeShort, simulation.Tick * activeSettings.StepSeconds), GUILayout.Width(140));
            if (GUILayout.Button(GameText.Get(paused ? TextKey.Resume : TextKey.Pause), GUILayout.Width(125))) paused = !paused;
            foreach (float value in Speeds) {
                GUI.backgroundColor = speed == value ? new Color(.4f, .95f, .75f) : Color.white;
                if (GUILayout.Button(GameText.Format(TextKey.TimeScale, value), GUILayout.Width(52))) speed = value;
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(GameText.Get(TextKey.Overview), GUILayout.Width(140))) { ObserveWorld(); rig.Overview(); }
            if (CanOpenOcean3D && GUILayout.Button(GameText.Get(TextKey.OpenOcean3D), GUILayout.Width(110))) OpenOcean3D();
            GUILayout.EndHorizontal(); GUILayout.EndArea();
            GUILayout.BeginArea(LeftPanel, panelStyle);
            BeginPanelScroll(ref environmentScroll, LeftPanel);
            GUILayout.Label(GameText.Format(TextKey.PopulationShort, simulation.Count, simulation.ColonyCount), titleStyle);
            if (GUILayout.Button(GameText.Get(showEnvironment ? TextKey.CloseEnvironment : TextKey.Environment))) showEnvironment = !showEnvironment;
            if (showEnvironment) {
                GUILayout.Label(GameText.Format(TextKey.Population, simulation.Count, activeSettings.Capacity, simulation.Births, simulation.Deaths));
                GUILayout.Label(GameText.Format(TextKey.Colonies, simulation.ColonyCount, simulation.ColonialCells, simulation.LargestColony));
                if (simulation.ActiveEvent == NaturalEvent.None) GUILayout.Label(GameText.Get(TextKey.ClimateQuiet), hintStyle);
                else GUILayout.Label(GameText.Format(TextKey.ClimateStatus, GameText.Get(EventKeys[(int)simulation.ActiveEvent]), simulation.EventSecondsRemaining));
                if (GUILayout.Button(GameText.Get(colorMode == 4 ? TextKey.NaturalColors : colorMode == 0 ? TextKey.TemperatureColors : colorMode == 1 ? TextKey.DietColors : colorMode == 2 ? TextKey.RoleColors : TextKey.StrategyColors))) colorMode = (colorMode + 1) % 5;
                GUILayout.Label(GameText.Get(colorMode == 4 ? TextKey.NaturalLegend : colorMode == 3 ? TextKey.StrategyLegend : colorMode == 2 ? TextKey.LegendCompact : colorMode == 1 ? TextKey.DietLegend : TextKey.TemperatureLegend), hintStyle);
                DrawEnvironment();
                GUILayout.Label(GameText.Format(TextKey.EcosystemSummary, simulation.LivingLineages, simulation.PredatorCount, simulation.PredationDeaths));
                DrawOceanMap();
                if (GUILayout.Button(GameText.Get(DetailedOrganisms ? TextKey.DetailOn : TextKey.DetailOff))) DetailedOrganisms = !DetailedOrganisms;
                if (GUILayout.Button(GameText.Get(WorldHints ? TextKey.WorldHintsOn : TextKey.WorldHintsOff))) WorldHints=!WorldHints;
                GUILayout.Label(GameText.Get(TextKey.SoundSetting)); SoundVolume = GUILayout.HorizontalSlider(SoundVolume, 0, 1);
            }
            if (GUILayout.Button(GameText.Get(showGoals ? TextKey.HideGoals : TextKey.ShowGoals))) showGoals = !showGoals;
            if (showGoals) DrawJourney();
            if (GUILayout.Button(GameText.Get(showJournal ? TextKey.HideJournal : TextKey.ShowJournal))) showJournal = !showJournal;
            if (showJournal) DrawJournal();
            if (simulation.Count == 0) GUILayout.Label(GameText.Get(TextKey.Extinction));
            EndPanelScroll(); GUILayout.EndArea();
            GUILayout.BeginArea(RightPanel, panelStyle);
            BeginPanelScroll(ref selectionScroll, RightPanel);
            if (Observation.TryFind(simulation, selectedId, out var selected)) DrawSelection(selected);
            else if (Journey.Active) DrawSuccession();
            else {
                GUILayout.Label(GameText.Get(TextKey.NothingSelected), titleStyle);
                GUILayout.Label(GameText.Get(TextKey.PickHelp));
            }
            EndPanelScroll(); GUILayout.EndArea();
            GUILayout.BeginArea(BottomPanel, panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(); DrawCurrentGoal(); GUILayout.EndVertical();
            if (GUILayout.Button(GameText.Get(showHelp ? TextKey.HelpClose : TextKey.HelpOpen),GUILayout.Width(170))) showHelp = !showHelp;
            GUILayout.EndHorizontal();
            if (showHelp) GUILayout.Label(GameText.Get(IsControlling ? TextKey.GameHelp : TextKey.ObserverHelp), hintStyle);
            GUILayout.EndArea();
            string tooltipText = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltipText)) {
                var tooltip = new Rect(Mathf.Clamp(Event.current.mousePosition.x-160,20,UiWidth-340),
                    Mathf.Clamp(Event.current.mousePosition.y-150,20,UiHeight-160),320,140);
                GUI.Box(tooltip,GUIContent.none,panelStyle);
                GUI.Label(new Rect(tooltip.x+12,tooltip.y+10,tooltip.width-24,tooltip.height-20),tooltipText,hintStyle);
            }
            GUI.matrix = oldMatrix; GUI.skin = oldSkin;
        }
        static readonly TextKey[] GoalKeys = { TextKey.GoalSurvive, TextKey.GoalFeed, TextKey.GoalReserve, TextKey.GoalReproduce, TextKey.GoalInherit, TextKey.GoalAdapt, TextKey.GoalColony, TextKey.GoalPredators, TextKey.GoalLineage, TextKey.GoalNiche, TextKey.GoalScarcity, TextKey.GoalProtected };
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
            int goalCount = activeSettings.Ecosystem ? GoalKeys.Length : 7;
            GUILayout.Label(GameText.Format(TextKey.LineageTitle, Journey.RootId, Journey.CompletedCount, Journey.AchievementCount, goalCount), titleStyle);
            for (int i = 0; i < goalCount; i++) {
                var goal = (EvolutionGoal)i;
                GUILayout.Label(GameText.Format(TextKey.GoalProgress, GameText.Get(GoalKeys[i]), Journey.Progress(goal)), hintStyle);
                DrawMeter(Journey.Progress(goal));
            }
            GUILayout.Label(GameText.Get(TextKey.GoalRules), hintStyle);
            if (activeSettings.Ecosystem) GUILayout.Label(GameText.Get(TextKey.EcosystemGoalRules), hintStyle);
            if (IsControlling && !Journey.Completed(EvolutionGoal.Adapt)) {
                if (GUILayout.Button(GameText.Get(TextKey.ThermalChallenge))) {
                    pendingTemperature = simulation.Temperature <= 44 ? simulation.Temperature + 6 : simulation.Temperature - 6;
                    environmentPending = true;
                }
            }
            if (Journey.CompletedCount >= goalCount) GUILayout.Label(GameText.Get(TextKey.StageComplete));
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
        static readonly TextKey[] MutationKeys = { TextKey.MutationSpeed, TextKey.MutationSize, TextKey.MutationUptake, TextKey.MutationTemperature, TextKey.MutationDiet, TextKey.MutationAdhesion, TextKey.MutationColonySize, TextKey.MutationBranching, TextKey.MutationDifferentiation, TextKey.MutationTransport, TextKey.MutationPattern, TextKey.MutationPhoto, TextKey.MutationPredation, TextKey.MutationMembrane, TextKey.MutationSense, TextKey.MutationThermalTolerance, TextKey.MutationToxinResistance, TextKey.MutationEfficiency };
        static readonly TextKey[] EventKeys = { TextKey.EventNone, TextKey.EventCooling, TextKey.EventWarming, TextKey.EventShortage, TextKey.EventToxins, TextKey.EventCurrents, TextKey.EventResources, TextKey.EventBloom };
        static readonly TextKey[] StrategyKeys = { TextKey.StrategyGeneralist, TextKey.StrategyPhoto, TextKey.StrategyOrganic, TextKey.StrategyPredator, TextKey.StrategyArmor, TextKey.StrategySwift, TextKey.StrategyFrugal, TextKey.StrategyColony };
        static readonly TextKey[] ZoneKeys = { TextKey.ZoneLight, TextKey.ZoneDeep, TextKey.ZoneFood, TextKey.ZoneVent, TextKey.ZoneDanger };
        static readonly TextKey[] ActivityKeys = { TextKey.ActivityWander, TextKey.ActivityFeed, TextKey.ActivityHunt, TextKey.ActivityFlee };
        void DrawJournal()
        {
            if (simulation.Journal.Count == 0) GUILayout.Label(GameText.Get(TextKey.JournalEmpty), hintStyle);
            for (int i = 0; i < Mathf.Min(12, simulation.Journal.Count); i++) {
                var e = simulation.Journal.GetRecent(i); float seconds = e.Tick * activeSettings.StepSeconds;
                switch (e.Kind) {
                    case EvolutionRecordKind.Climate: GUILayout.Label(GameText.Format(TextKey.JournalClimate, seconds, GameText.Get(EventKeys[e.Subject])), hintStyle); break;
                    case EvolutionRecordKind.ClimateEnded: GUILayout.Label(GameText.Format(TextKey.JournalClimateEnded, seconds, GameText.Get(EventKeys[e.Subject])), hintStyle); break;
                    case EvolutionRecordKind.StrategyAppeared: GUILayout.Label(GameText.Format(TextKey.JournalStrategy, seconds, GameText.Get(StrategyKeys[e.Subject])), hintStyle); break;
                    case EvolutionRecordKind.Adaptation:
                        for (int bit = 0; bit < MutationKeys.Length; bit++) if ((e.Value & (1 << bit)) != 0)
                            GUILayout.Label(GameText.Format(TextKey.JournalAdaptation, seconds, e.Subject, GameText.Get(MutationKeys[bit])), hintStyle);
                        break;
                    case EvolutionRecordKind.LineageGrowth: GUILayout.Label(GameText.Format(TextKey.JournalGrowth, seconds, e.Subject, e.Value), hintStyle); break;
                    case EvolutionRecordKind.LineageExtinct: GUILayout.Label(GameText.Format(TextKey.JournalExtinct, seconds, e.Subject), hintStyle); break;
                    case EvolutionRecordKind.PopulationBoom: GUILayout.Label(GameText.Format(TextKey.JournalBoom, seconds, e.Value), hintStyle); break;
                    case EvolutionRecordKind.PopulationDecline: GUILayout.Label(GameText.Format(TextKey.JournalDecline, seconds, e.Value), hintStyle); break;
                    case EvolutionRecordKind.Predation: GUILayout.Label(GameText.Format(TextKey.JournalPredation, seconds, e.Subject, e.Value), hintStyle); break;
                }
            }
        }
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
            if (!IsControlling || showSelectionDetails) {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(GameText.Get(TextKey.Focus))) FocusSelected(false);
                if (GUILayout.Button(GameText.Get(rig.Following ? TextKey.StopFollow : TextKey.Follow))) { if (rig.Following) rig.StopFollowing(); else FocusSelected(true); }
                GUILayout.EndHorizontal();
            }
            if (IsControlling) {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(GameText.Get(TextKey.ObserveCompact))) ObserveWorld();
                bool wasEnabled = GUI.enabled; GUI.enabled = !paused && Biology.CanDivide(c) && simulation.Count < activeSettings.Capacity;
                if (GUILayout.Button(GameText.Get(TextKey.Divide))) simulation.RequestDivision();
                GUI.enabled = wasEnabled;
                GUILayout.EndHorizontal();
            } else if (GUILayout.Button(GameText.Get(TextKey.PlayOrganism))) PlaySelected();
            if (simulation.LastDivisionParentId == c.Id && Observation.TryFind(simulation, simulation.LastOffspringId, out _)) {
                if (GUILayout.Button(GameText.Get(TextKey.PlayChild))) PlayOffspring();
            }
            DrawTactical(c);
            if (!showSelectionDetails) return;
            DrawCellGameplay(c);
            if (GUILayout.Button(GameText.Get(showGenes ? TextKey.HideGenes : TextKey.Genes))) showGenes = !showGenes;
            if (!showGenes) return;
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
            GUILayout.Label(GameText.Format(TextKey.LocalEcology, GameText.Get(ZoneKeys[(int)local.Zone]), local.Light, local.Toxicity));
            GUILayout.Label(GameText.Format(TextKey.Phenotype, GameText.Get(StrategyKeys[(int)Ecology.Strategy(c)]), c.LineageId));
            GUILayout.Label(GameText.Format(TextKey.Activity, GameText.Get(ActivityKeys[(int)c.Activity])));
            GUILayout.Label(GameText.Format(TextKey.Integrity, c.Integrity));
            GUILayout.Label(GameText.Format(TextKey.Vitality, Mathf.Clamp01(c.Energy / Biology.Capacity(c.Genes))));
            GUILayout.Label(GameText.Get(TextKey.VitalityHelp), hintStyle);
            if (simulation.Count >= activeSettings.Capacity) GUILayout.Label(GameText.Get(TextKey.PopulationFull));
            else if (Biology.CanDivide(c)) GUILayout.Label(GameText.Get(TextKey.ReproductionReady));
            else if (c.Generation > 0 && c.Growth < 1) GUILayout.Label(GameText.Format(TextKey.ReproductionGrowth, c.Growth));
            else if (c.Age < Multicellularity.MaturityAge(c)) GUILayout.Label(GameText.Format(TextKey.ReproductionYoung, Multicellularity.MaturityAge(c) - c.Age));
            else GUILayout.Label(GameText.Format(TextKey.ReproductionEnergy, Mathf.Clamp01(c.Energy / (Biology.Capacity(c.Genes) * Multicellularity.DivisionThreshold(c)))));
            if (showGenes) {
                var g = c.Genes;
                GUILayout.Label(GameText.Format(TextKey.GeneMotion, g.Speed, g.Size));
                GUILayout.Label(GameText.Format(TextKey.GeneFeeding, g.Uptake, g.OrganicSpecialization));
                GUILayout.Label(GameText.Format(TextKey.GeneTemperature, g.PreferredTemperature));
                GUILayout.Label(GameText.Format(TextKey.GenePhotoPredation, g.Photosynthesis, g.Predation));
                GUILayout.Label(GameText.Format(TextKey.GeneArmorSense, g.Membrane, g.Sensitivity));
                GUILayout.Label(GameText.Format(TextKey.GeneResistance, g.ThermalTolerance, g.ToxinResistance));
                GUILayout.Label(GameText.Format(TextKey.GeneEfficiency, g.EnergyEfficiency));
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
