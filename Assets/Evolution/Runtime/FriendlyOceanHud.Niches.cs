using Evolution.Volume;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed partial class FriendlyOceanHud
    {
        int miniLayer;
        static readonly string[] miniNames={"Население","Свет","Температура","Минералы","Кислород","Химическая пища","Органика","Стратегии"};
        string MiniLayerName=>miniNames[miniLayer];
        static Color MiniDietColour(int diet)=>diet==0?new Color(.4f,1,.5f):diet==1?new Color(1,.4f,.35f):diet==2?new Color(1,.75f,.3f):new Color(.45f,.75f,1);
        void DrawNicheMapTiles(Rect map,V3 center)
        {
            if(miniLayer==0||miniLayer==7)return;
            int slice=water.Overview.FullDepth?ChunkKey.At(center).Y:water.Overview.SliceIndex;
            foreach(var r in water.Overview.Survey.Regions) {
                if(!r.Loaded||r.Key.Y!=slice)continue;
                var a=MapPoint(r.Key.Origin+new V3(0,0,20),map,center);var b=MapPoint(r.Key.Origin+new V3(20,0,0),map,center);
                var rect=Rect.MinMaxRect(Mathf.Max(map.x,a.x),Mathf.Max(map.y,a.y),Mathf.Min(map.xMax,b.x),Mathf.Min(map.yMax,b.y));
                if(rect.width<=0||rect.height<=0)continue;
                float value=miniLayer==1?r.Light:miniLayer==2?Mathf.InverseLerp(9,50,r.Temperature):miniLayer==3?r.Nutrients/96:miniLayer==4?r.Oxygen/160:miniLayer==5?r.Substrate/70:r.Organic/100;
                Color high=miniLayer==1?new Color(1,.94f,.55f):miniLayer==2?new Color(1,.4f,.2f):miniLayer==3?new Color(.3f,1,.7f):miniLayer==4?new Color(.6f,.9f,1):new Color(1,.7f,.25f);
                Color colour=Color.Lerp(new Color(.08f,.13f,.24f),high,Mathf.Clamp01(value));colour.a=.55f;Fill(rect,colour);
            }
        }
    }
}
