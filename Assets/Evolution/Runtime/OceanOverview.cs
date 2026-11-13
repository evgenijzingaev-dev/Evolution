using System;
using System.Collections.Generic;
using Evolution.Core;
using Evolution.Volume;
using UnityEngine;
namespace Evolution.Runtime
{
    public enum OverviewSelection { Organism,Region,Lineage }
    public sealed partial class OceanOverview : MonoBehaviour
    {
        WaterPrototype water;SurveyDraw draw;
        public bool Active {get;private set;}=true;
        public bool Analytical {get;private set;}
        public bool AutoFrame=true,AutoOrbit=true,FullDepth=true,EventsVisible,DepthColours;
        public SurveyLayer Layer=SurveyLayer.Lineage;
        public int SliceIndex,SelectedLineage;public ChunkKey RegionKey;
        public OverviewSelection SelectionKind=OverviewSelection.Region;
        public OceanSurvey Survey=>water.Ocean.Survey;
        public float Blend {get;private set;}
        public int VisibleMarkers {get;private set;}public int VisibleClusters {get;private set;}
        public bool Following {get;private set;}
        string trackedIdentity;float nextRefresh,lastClick=-10;bool ready;int cachedRevision=-1;
        readonly List<SurveyRegion> navigation=new List<SurveyRegion>(256);
        readonly List<SurveyRegion> nearby=new List<SurveyRegion>(256);
        readonly WaterView[] views=new WaterView[8];readonly bool[] viewModes=new bool[8];int viewCount;
        string query="";bool noResults;
        public void Initialize(WaterPrototype prototype){water=prototype;draw=new SurveyDraw();water.Rig.SetStrategic(true);Refresh();}
        public void SetMode(bool analytical){if(!Active)Enter();Analytical=analytical;AutoOrbit=!analytical;}
        public void Enter()
        {
            if(!Active){Push();water.Observe();Active=true;water.Rig.SetStrategic(true);}
            AutoFrame=true;Following=false;Refresh();FrameActive();
        }
        public void EnterCellPlay(){Push();Active=false;Following=false;AutoFrame=false;water.Rig.SetStrategic(false);}
        public void LeaveForLegacy(){Active=false;Following=false;AutoFrame=false;AutoOrbit=false;water.Rig.SetStrategic(false);}
        public void Push(){if(viewCount==views.Length){for(int i=1;i<viewCount;i++){views[i-1]=views[i];viewModes[i-1]=viewModes[i];}viewCount--;}
            views[viewCount]=water.Rig.View;viewModes[viewCount]=Analytical;viewCount++;}
        public void Back(){water.Observe();Active=true;water.Rig.SetStrategic(true);Following=false;AutoFrame=false;
            if(viewCount>0){viewCount--;water.Rig.Restore(views[viewCount]);Analytical=viewModes[viewCount];}else Enter();}
        public void Home(){Push();water.OrganismsVisible=true;water.Observe();Active=true;water.Rig.SetStrategic(true);AutoFrame=true;Following=false;FrameActive();}
        void FrameActive()
        {
            if(Survey.Frame(VolumePainter.V(water.Rig.Focus),Analytical?36:20,0,out var center,out float radius,out int count)) {
                water.Rig.FocusOn(VolumePainter.V(center),Mathf.Clamp(radius/Mathf.Sin(water.Lens.fieldOfView*Mathf.Deg2Rad*.5f)*1.12f,12,110));
                RegionKey=ChunkKey.At(center);SelectionKind=OverviewSelection.Region;
            }
        }
        public void BeforeCamera(bool overUi)
        {
            if(!Active)return;
            Blend=Mathf.MoveTowards(Blend,Analytical?1:0,Time.unscaledDeltaTime*2.5f);
            if(Application.isFocused&&!overUi&&water.Rig.Navigating){AutoFrame=false;Following=false;}
            if(Application.isFocused&&!overUi&&Input.GetMouseButton(1))AutoOrbit=false;
            if(Time.unscaledTime>=nextRefresh||cachedRevision!=Survey.Revision){Refresh();nextRefresh=Time.unscaledTime+.5f;}
            if(Following&&SelectionKind==OverviewSelection.Organism) {
                int i=water.SelectedIndex;if(i>=0){var a=water.World.Get(i);if(a.Alive&&a.Identity==trackedIdentity)water.Rig.Follow(VolumePainter.V(a.Position));else Following=false;}
            }
            if(AutoOrbit&&!Analytical&&!water.Paused)water.Rig.Orbit(Time.unscaledDeltaTime*3,0);
        }
        public void Refresh()
        {
            Survey.Refresh(water.World,water.Ocean.LoadedKeys);cachedRevision=Survey.Revision;
            nearby.Clear();navigation.Clear();Vector3 center=water.Rig.Focus;float range=Mathf.Max(100,water.Rig.Distance*4);
            foreach(var r in Survey.Regions){if(!r.Loaded&&r.Living>0)navigation.Add(r);if((VolumePainter.V(r.Center)-center).sqrMagnitude<range*range)nearby.Add(r);}
            regionSort.Center=VolumePainter.V(center);nearby.Sort(regionSort);navigation.Sort(regionSort);if(nearby.Count>256)nearby.RemoveRange(256,nearby.Count-256);
            if(!ready){ready=true;RegionKey=ChunkKey.At(center);SliceIndex=RegionKey.Y;}
            if(AutoFrame)FrameActive();
            if(Following&&SelectionKind==OverviewSelection.Region&&Survey.TryRegion(RegionKey,out var group)&&group.Living>0)water.Rig.Follow(VolumePainter.V(group.Center));
            if(Following&&SelectionKind==OverviewSelection.Lineage&&Survey.Frame(VolumePainter.V(center),80,SelectedLineage,out var p,out float extent,out int n))water.Rig.Follow(VolumePainter.V(p));
        }
        sealed class RegionSort:IComparer<SurveyRegion>{public V3 Center;public int Compare(SurveyRegion a,SurveyRegion b)=>((a.Center-Center).Sqr).CompareTo((b.Center-Center).Sqr);}
        readonly RegionSort regionSort=new RegionSort();
        public void SetSlice(bool full,int index){FullDepth=full;SliceIndex=index;}
        bool InSlice(V3 p)=>FullDepth||ChunkKey.At(p).Y==SliceIndex;
        bool InSlice(SurveyRegion r)=>FullDepth||r.Key.Y==SliceIndex;
        public void ChooseOrganism(int index,bool zoom)
        {
            if(index<0||index>=water.World.Count)return;var a=water.World.Get(index);if(!a.Alive)return;
            water.Choose(index,false);SelectionKind=OverviewSelection.Organism;SelectedLineage=a.Biology.LineageId;trackedIdentity=a.Identity;
            RegionKey=ChunkKey.At(a.Position);Following=false;AutoFrame=false;if(zoom)ZoomSelection();
        }
        public void ChooseRegion(ChunkKey key,bool travel)
        {
            RegionKey=key;SelectionKind=OverviewSelection.Region;Following=false;AutoFrame=false;
            if(travel&&Survey.TryRegion(key,out var r)){Push();water.Observe();water.Rig.FocusOn(VolumePainter.V(r.Center),34);}
        }
        public void ZoomSelection()
        {
            Push();water.Observe();AutoFrame=false;
            if(SelectionKind==OverviewSelection.Organism&&water.SelectedIndex>=0) {
                var a=water.World.Get(water.SelectedIndex);water.Rig.FocusOn(VolumePainter.V(a.Position),Mathf.Clamp(a.Biology.Genes.Size*3,2.8f,8));trackedIdentity=a.Identity;Following=true;
            }else if(Survey.TryRegion(RegionKey,out var r))water.Rig.FocusOn(VolumePainter.V(r.Center),SelectionKind==OverviewSelection.Lineage?20:24);
        }
        public void FollowSelection()
        {
            Following=!Following;AutoFrame=false;
            if(SelectionKind==OverviewSelection.Organism&&water.SelectedIndex>=0)trackedIdentity=water.World.Get(water.SelectedIndex).Identity;
            if(SelectionKind==OverviewSelection.Region&&Survey.TryRegion(RegionKey,out var region))water.Rig.Follow(VolumePainter.V(region.Center));
            if(SelectionKind==OverviewSelection.Lineage)Refresh();
        }
        public void SelectLineage(){if(water.SelectedIndex>=0){SelectedLineage=water.World.Get(water.SelectedIndex).Biology.LineageId;SelectionKind=OverviewSelection.Lineage;Following=false;}}
        public void Search(string text)
        {
            noResults=true;query=text??"";string value=query.Trim();bool line=value.StartsWith("л:",StringComparison.OrdinalIgnoreCase);if(line)value=value.Substring(2).Trim();
            if(!int.TryParse(value,out int id))return;
            if(!line)foreach(var a in Survey.Agents)if(a.Id==id){int i=water.Ocean.Find(a.Identity);if(i>=0){ChooseOrganism(i,false);ZoomSelection();noResults=false;return;}}
            SurveyRegion best=null;foreach(var r in Survey.Regions)if(r.Lineages.TryGetValue(id,out int n)&&n>0&&(best==null||r.Tick>best.Tick))best=r;
            if(best!=null){Push();SelectedLineage=id;RegionKey=best.Key;SelectionKind=OverviewSelection.Lineage;AutoFrame=false;Following=false;water.Rig.FocusOn(VolumePainter.V(best.Center),35);noResults=false;}
        }
        public void Click(Vector2 point)
        {
            bool twice=Time.unscaledTime-lastClick<.32f&&(point-lastPointer).sqrMagnitude<100;lastClick=Time.unscaledTime;lastPointer=point;
            int picked=-1;float best=18*18;
            foreach(var a in Survey.Agents)if(InSlice(a.Position)) {var s=water.Lens.WorldToScreenPoint(VolumePainter.V(a.Position));if(s.z<=0)continue;float d=(new Vector2(s.x,s.y)-point).sqrMagnitude;
                if(d<best){best=d;picked=water.Ocean.Find(a.Identity);}}
            if(picked<0)picked=water.PickOrganism(point);
            if(picked>=0){ChooseOrganism(picked,twice);return;}
            SurveyRegion region=null;best=28*28;
            foreach(var r in nearby)if(InSlice(r)){var s=water.Lens.WorldToScreenPoint(VolumePainter.V(r.Center));if(s.z<=0)continue;float d=(new Vector2(s.x,s.y)-point).sqrMagnitude;
                if(d<best){best=d;region=r;}}
            if(region!=null)ChooseRegion(region.Key,twice);
        }
        Vector2 lastPointer;
        public void VisitEvent(int index){if(index<0||index>=Survey.Events.Count)return;Push();var e=Survey.Events[index];water.Observe();AutoFrame=Following=false;
            RegionKey=ChunkKey.At(e.Position);SelectionKind=OverviewSelection.Region;water.Rig.FocusOn(VolumePainter.V(e.Position),20);}
        void OnDestroy(){draw?.Dispose();Destroy(uiSkin);Destroy(panelTexture);Destroy(buttonTexture);}
    }
}
