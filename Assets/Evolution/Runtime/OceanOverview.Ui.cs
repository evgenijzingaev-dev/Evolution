using Evolution.Localization;
using Evolution.Volume;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed partial class OceanOverview
    {
        GUISkin uiSkin;Texture2D panelTexture,buttonTexture;GUIStyle box,label,title,small;bool regionsOpen;int regionPage;
        float UiScale=>Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.7f,1.5f);
        Rect Controls=>new Rect(18,18,310,448);
        Rect Journal=>new Rect(Screen.width/UiScale-342,18,324,224);
        Rect RegionsBox=>new Rect(18,Survey.CatalogueTruncated?510:478,310,Mathf.Min(300,Screen.height/UiScale-520));
        public bool OverUi(Vector2 pixel){if(!Active||!water.HudVisible)return false;var p=new Vector2(pixel.x/UiScale,(Screen.height-pixel.y)/UiScale);
            return Controls.Contains(p)||(EventsVisible&&Journal.Contains(p))||(regionsOpen&&RegionsBox.Contains(p));}
        void Skin()
        {
            if(uiSkin)return;uiSkin=Instantiate(GUI.skin);uiSkin.font=Resources.Load<Font>("WaterUI/OpenSans-Regular");uiSkin.label.font=uiSkin.button.font=uiSkin.textField.font=uiSkin.font;
            uiSkin.label.fontStyle=uiSkin.button.fontStyle=uiSkin.textField.fontStyle=FontStyle.Normal;uiSkin.textField.fontSize=13;
            uiSkin.label.fontSize=13;uiSkin.label.wordWrap=true;uiSkin.label.normal.textColor=new Color(.83f,.94f,.96f);
            uiSkin.button.fontSize=12;uiSkin.button.padding=new RectOffset(5,5,5,5);uiSkin.button.wordWrap=false;
            panelTexture=new Texture2D(1,1);panelTexture.SetPixel(0,0,new Color(.014f,.045f,.07f,.72f));panelTexture.Apply();
            buttonTexture=new Texture2D(1,1);buttonTexture.SetPixel(0,0,new Color(.35f,.66f,.75f,.20f));buttonTexture.Apply();
            uiSkin.button.normal.background=uiSkin.button.hover.background=uiSkin.button.active.background=buttonTexture;uiSkin.button.border=new RectOffset();
            box=new GUIStyle(uiSkin.box){padding=new RectOffset(12,12,10,10)};box.normal.background=panelTexture;
            label=new GUIStyle(uiSkin.label);title=new GUIStyle(label){fontSize=16,fontStyle=FontStyle.Bold};small=new GUIStyle(label){fontSize=11};
        }
        static readonly TextKey[] layers={TextKey.SurveyLineage,TextKey.SurveyForm,TextKey.SurveyDiet,TextKey.SurveyThreat,TextKey.SurveyDensity,TextKey.SurveyFood,TextKey.SurveyTemperature,TextKey.SurveyCurrent,TextKey.SurveyLight};
        public void DrawUi()
        {
            if(!Active||!water.HudVisible)return;Skin();var old=GUI.skin;var matrix=GUI.matrix;GUI.skin=uiSkin;GUI.matrix=Matrix4x4.Scale(Vector3.one*UiScale);
            GUILayout.BeginArea(Controls,box);
            GUILayout.Label(GameText.Get(TextKey.SurveyTitle),title);
            if(GUILayout.Button(GameText.Get(Analytical?TextKey.SurveyAnalytical:TextKey.SurveyCinematic)))SetMode(!Analytical);
            GUILayout.Label(GameText.Format(TextKey.SurveyCounts,Survey.ActiveLiving,Survey.KnownLiving,Survey.KnownLineages),small);
            GUILayout.Label(GameText.Get(TextKey.SurveyNoColonies),small);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(TextKey.SurveyFilterPrev)))Layer=(SurveyLayer)(((int)Layer+8)%9);
            GUILayout.Label(GameText.Get(layers[(int)Layer]));
            if(GUILayout.Button(GameText.Get(TextKey.SurveyFilterNext)))Layer=(SurveyLayer)(((int)Layer+1)%9);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(FullDepth?TextKey.SurveyFullDepth:TextKey.SurveySlice)))FullDepth=!FullDepth;
            if(GUILayout.Button(GameText.Get(TextKey.SurveyDown)))SliceIndex--;
            if(GUILayout.Button(GameText.Get(TextKey.SurveyUp)))SliceIndex++;
            GUILayout.EndHorizontal();
            if(FullDepth)GUILayout.Label(GameText.Get(TextKey.SurveyAllDepth),small);
            else GUILayout.Label(GameText.Format(TextKey.SurveyHeight,SliceIndex*20,(SliceIndex+1)*20),small);
            if(GUILayout.Button(GameText.Get(DepthColours?TextKey.SurveyDepthOn:TextKey.SurveyDepthOff)))DepthColours=!DepthColours;
            if(Survey.TryRegion(RegionKey,out var region)){GUILayout.Label(GameText.Format(TextKey.SurveyRegion,RegionKey.X,RegionKey.Y,RegionKey.Z,region.Living,region.Temperature,region.Light),small);
                if(Layer==SurveyLayer.Food)GUILayout.Label(GameText.Format(TextKey.SurveyResourceValues,region.Substrate,region.Organic),small);
                if(Layer==SurveyLayer.Current)GUILayout.Label(GameText.Format(TextKey.VolumeVector,region.Current.X,region.Current.Y,region.Current.Z),small);}
            if(SelectionKind!=OverviewSelection.Region)GUILayout.Label(GameText.Format(TextKey.SurveySelection,water.SelectedIndex>=0?water.World.Get(water.SelectedIndex).Biology.Id:0,SelectedLineage),small);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(Following?TextKey.SurveyStopFollow:TextKey.SurveyFollow)))FollowSelection();
            if(GUILayout.Button(GameText.Get(TextKey.SurveyZoom)))ZoomSelection();
            if(GUILayout.Button(GameText.Get(TextKey.SurveyHome)))Home();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled=SelectionKind==OverviewSelection.Organism;
            if(GUILayout.Button(GameText.Get(TextKey.SurveyTrackLineage)))SelectLineage();
            if(GUILayout.Button(GameText.Get(TextKey.PlayOrganism)))water.PlaySelected();
            GUI.enabled=true;GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUI.SetNextControlName("OceanSearch");query=GUILayout.TextField(query,40);
            if(clearSearchFocus){GUI.FocusControl(null);clearSearchFocus=false;}searchFocused=GUI.GetNameOfFocusedControl()=="OceanSearch";
            if(GUILayout.Button(GameText.Get(TextKey.SurveySearch))){Search(query);ClearSearchFocus();}GUILayout.EndHorizontal();
            GUILayout.Label(GameText.Get(noResults?TextKey.SurveyNotFound:TextKey.SurveySearchHint),small);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(TextKey.SurveyRegions)))regionsOpen=!regionsOpen;
            if(GUILayout.Button(GameText.Get(TextKey.SurveyEvents)))EventsVisible=!EventsVisible;
            if(GUILayout.Button(GameText.Get(water.Paused?TextKey.WaterRun:TextKey.Pause)))water.Paused=!water.Paused;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GameText.Get(AutoFrame?TextKey.SurveyAutoOn:TextKey.SurveyAutoOff)))AutoFrame=!AutoFrame;
            if(GUILayout.Button(GameText.Get(AutoOrbit?TextKey.SurveyOrbitOn:TextKey.SurveyOrbitOff)))AutoOrbit=!AutoOrbit;
            GUILayout.EndHorizontal();GUILayout.EndArea();
            if(Survey.CatalogueTruncated)GUI.Label(new Rect(18,Controls.yMax+2,310,28),GameText.Get(TextKey.SurveyLimit),small);
            if(EventsVisible)DrawJournal();if(regionsOpen)DrawRegions();DrawLabels();
            DrawLegend();
            GUI.Label(new Rect(338,Screen.height/UiScale-32,Screen.width/UiScale-356,24),GameText.Get(TextKey.SurveyControls),small);
            GUI.skin=old;GUI.matrix=matrix;
        }
        void DrawRegions()
        {
            GUILayout.BeginArea(RegionsBox,box);GUILayout.Label(GameText.Get(TextKey.SurveyKnownRegions),title);
            // At most eight navigable records. Full catalogue stays aggregate-only and bounded.
            int pages=Mathf.Max(1,(navigation.Count+5)/6);regionPage=Mathf.Clamp(regionPage,0,pages-1);
            for(int i=regionPage*6;i<Mathf.Min(navigation.Count,(regionPage+1)*6);i++){var r=navigation[i];
                if(GUILayout.Button(GameText.Format(TextKey.SurveyRegionLink,r.Key.X,r.Key.Y,r.Key.Z,r.Living,(Survey.Tick-r.Tick)*VolumeWorld.StepSeconds)))ChooseRegion(r.Key,true);}
            if(navigation.Count==0)GUILayout.Label(GameText.Get(TextKey.SurveyNoRemote),small);
            GUILayout.BeginHorizontal();if(GUILayout.Button(GameText.Get(TextKey.SurveyFilterPrev)))regionPage=Mathf.Max(0,regionPage-1);
            GUILayout.Label(GameText.Format(TextKey.SurveyPage,regionPage+1,pages),small);
            if(GUILayout.Button(GameText.Get(TextKey.SurveyFilterNext)))regionPage=Mathf.Min(pages-1,regionPage+1);GUILayout.EndHorizontal();
            GUILayout.Label(GameText.Get(TextKey.SurveySnapshots),small);GUILayout.EndArea();
        }
        static TextKey EventText(SurveyEventKind kind)=>kind==SurveyEventKind.Death?TextKey.SurveyDeath:kind==SurveyEventKind.Predation?TextKey.SurveyPredation:
            kind==SurveyEventKind.Migration?TextKey.SurveyMigration:kind==SurveyEventKind.PopulationChange?TextKey.SurveyPopulation:kind==SurveyEventKind.KnownLineageLost?TextKey.SurveyLost:TextKey.SurveyBirth;
        void DrawJournal()
        {
            GUILayout.BeginArea(Journal,box);GUILayout.Label(GameText.Get(TextKey.SurveyEvents),title);
            if(Survey.Events.Count==0)GUILayout.Label(GameText.Get(TextKey.SurveyNoEvents),small);
            for(int i=Survey.Events.Count-1;i>=Mathf.Max(0,Survey.Events.Count-5);i--) {
                var e=Survey.Events[i];if(GUILayout.Button(GameText.Format(EventText(e.Kind),e.Count,e.Lineage)))VisitEvent(i);
            }
            GUILayout.Label(GameText.Get(TextKey.SurveyUnsupported),small);GUILayout.EndArea();
        }
        static readonly TextKey[] legend={TextKey.SurveyLegendLineage,TextKey.SurveyLegendForm,TextKey.SurveyLegendDiet,TextKey.SurveyLegendThreat,TextKey.SurveyLegendDensity,TextKey.SurveyLegendFood,TextKey.SurveyLegendHeat,TextKey.SurveyLegendFlow,TextKey.SurveyLegendLight};
        static readonly TextKey[] dietNames={TextKey.SurveyPhoto,TextKey.SurveyPredator,TextKey.SurveyOrganic,TextKey.SurveySubstrate};
        void DrawLegend()
        {
            GUI.Label(new Rect(340,18,430,40),DepthColours?GameText.Format(TextKey.SurveyDepthLegend,water.Rig.Focus.y-40,water.Rig.Focus.y+40):GameText.Get(legend[(int)Layer]),small);
            if(!DepthColours&&Layer==SurveyLayer.Diet)for(int i=0;i<4;i++){Color before=GUI.color;GUI.color=diets[i];GUI.Label(new Rect(340+(i%2)*150,52+(i/2)*18,146,18),GameText.Get(dietNames[i]),small);GUI.color=before;}
        }
        void DrawLabels()
        {
            int count=0;foreach(var r in nearby){if(!InSlice(r))continue;var p=water.Lens.WorldToScreenPoint(VolumePainter.V(RegionPoint(r)));if(p.z<=0)continue;
                if(!(Analytical&&DataLayer)&&p.z<(Analytical?100:70)&&!r.Key.Equals(RegionKey))continue;
                var rect=new Rect(p.x/UiScale+16,(Screen.height-p.y)/UiScale-10,170,40);if(rect.Overlaps(Controls)||(EventsVisible&&rect.Overlaps(Journal)))continue;
                GUI.Label(rect,GameText.Format(r.Loaded?TextKey.SurveyCluster:TextKey.SurveyOldCluster,r.Living),small);if(++count==8)break;}
        }
    }
}
