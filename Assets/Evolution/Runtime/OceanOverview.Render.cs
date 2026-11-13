using Evolution.Volume;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    public sealed partial class OceanOverview
    {
        sealed class SurveyDraw
        {
            readonly Mesh mesh=MicroMeshes.LightSheet();readonly Material material=new Material(Resources.Load<Shader>("SurveyMarker")){enableInstancing=true};
            readonly Matrix4x4[] matrices=new Matrix4x4[256];readonly Vector4[] colours=new Vector4[256],data=new Vector4[256];
            readonly MaterialPropertyBlock block=new MaterialPropertyBlock();int used;
            public void Add(Vector3 position,Color colour,float pixels,int shape=0,Vector3 direction=default) {
                if(colour.a<.002f)return;matrices[used]=Matrix4x4.TRS(position,direction.sqrMagnitude>.00001f?Quaternion.FromToRotation(Vector3.right,direction):Quaternion.identity,Vector3.one);
                colours[used]=colour;data[used]=new Vector4(pixels,shape,0,0);if(++used==256)Flush();}
            public void Flush(){if(used==0)return;block.SetVectorArray("_InstanceColor",colours);block.SetVectorArray("_MarkerData",data);
                Graphics.DrawMeshInstanced(mesh,0,material,matrices,used,block,ShadowCastingMode.Off,false);used=0;}
            public void Dispose(){Object.Destroy(mesh);Object.Destroy(material);}
        }
        static readonly Color[] forms={new Color(.55f,.9f,1),new Color(.43f,.95f,.78f),new Color(.95f,.66f,.85f),new Color(1,.84f,.42f),new Color(.98f,.52f,.48f),new Color(.57f,.91f,.39f)};
        static readonly Color[] diets={new Color(.45f,.95f,.57f),new Color(1,.37f,.31f),new Color(1,.77f,.34f),new Color(.42f,.80f,1)};
        Color AgentColour(SurveyAgent a)
        {
            if(DepthColours)return DepthColour(a.Position.Y);
            if(SelectionKind==OverviewSelection.Lineage&&a.Lineage!=SelectedLineage)return new Color(.4f,.6f,.7f,.18f);
            if(Layer==SurveyLayer.Form)return forms[a.Form];
            if(Layer==SurveyLayer.Diet)return diets[a.Diet];
            if(Layer==SurveyLayer.Threat)return a.Threat?new Color(1,.3f,.24f):new Color(.45f,.68f,.76f,.35f);
            return LineageColour(a.Lineage);
        }
        static Color LineageColour(int id){uint hash=unchecked((uint)id*2654435761u);return Color.HSVToRGB((hash&65535u)/65536f,.55f,1);}
        static int Dominant(int[] counts){int best=0;for(int i=1;i<counts.Length;i++)if(counts[i]>counts[best])best=i;return best;}
        Color DepthColour(float height)=>Color.Lerp(new Color(.42f,.5f,1),new Color(.44f,1,.85f),Mathf.InverseLerp(water.Rig.Focus.y-40,water.Rig.Focus.y+40,height));
        Color RegionColour(SurveyRegion r)
        {
            if(DepthColours)return DepthColour(r.Center.Y);
            if(Layer==SurveyLayer.Form)return forms[Dominant(r.Forms)];
            if(Layer==SurveyLayer.Diet)return diets[Dominant(r.Diets)];
            if(Layer==SurveyLayer.Lineage){int id=0,count=-1;foreach(var p in r.Lineages)if(p.Value>count||(p.Value==count&&p.Key<id)){id=p.Key;count=p.Value;}return LineageColour(id);}
            switch(Layer){
                case SurveyLayer.Food:return Color.Lerp(new Color(.14f,.43f,.55f),new Color(1,.82f,.38f),Mathf.Clamp01((r.Substrate+r.Organic)/90));
                case SurveyLayer.Temperature:return Color.Lerp(new Color(.23f,.54f,1),new Color(1,.4f,.24f),Mathf.InverseLerp(9,35,r.Temperature));
                case SurveyLayer.Light:return Color.Lerp(new Color(.18f,.26f,.55f),new Color(1,.95f,.67f),r.Light);
                case SurveyLayer.Current:return Color.Lerp(new Color(.29f,.62f,.8f),new Color(.62f,1,1),Mathf.Clamp01(r.Current.Length/.32f));
                case SurveyLayer.Threat:return r.Attacks>0?new Color(1,.3f,.24f):r.Dead>0?new Color(.81f,.45f,.75f):new Color(.23f,.55f,.65f);
                default:return Color.Lerp(new Color(.21f,.57f,.69f),new Color(.55f,1,.77f),Mathf.Clamp01(r.Living/8f));
            }
        }
        bool DataLayer=>Layer>=SurveyLayer.Density;
        public void Render()
        {
            VisibleMarkers=VisibleClusters=0;if(!Active||!water.HudVisible||!water.OrganismsVisible)return;
            var eye=water.Lens.transform.position;
            bool fields=Analytical&&DataLayer;
            foreach(var a in Survey.Agents) {
                if(!InSlice(a.Position))continue;Vector3 p=VolumePainter.V(a.Position);var screen=water.Lens.WorldToViewportPoint(p);if(screen.z<=.2f||screen.x<-.03f||screen.x>1.03f||screen.y<-.03f||screen.y>1.03f)continue;
                float distance=Vector3.Distance(eye,p),minimum=Mathf.Lerp(WaterOpticsValue(16,30,distance),1,Blend);
                float grouped=WaterOpticsValue(Mathf.Lerp(65,100,Blend),Mathf.Lerp(110,160,Blend),distance);
                Color c=AgentColour(a);c.a*=minimum*(1-grouped)*Mathf.Lerp(1,.35f,DataLayer?Blend:0);
                bool selected=SelectionKind==OverviewSelection.Organism&&a.Identity==trackedIdentity;
                draw.Add(p,c,selected?18:Analytical?11:8,Layer==SurveyLayer.Diet?a.Diet==1?2:0:0);if(c.a>.01f)VisibleMarkers++;
                if(selected){c=Color.white;c.a=.8f;draw.Add(p,c,26,1);}
            }
            foreach(var r in nearby) {
                if(!InSlice(r))continue;Vector3 p=VolumePainter.V(Analytical&&Layer>=SurveyLayer.Temperature?r.Key.Origin+new V3(10,10,10):r.Center);var screen=water.Lens.WorldToViewportPoint(p);
                if(screen.z<=.2f||screen.x<-.05f||screen.x>1.05f||screen.y<-.05f||screen.y>1.05f)continue;
                float d=Vector3.Distance(eye,p);float alpha=Mathf.Lerp(WaterOpticsValue(Mathf.Lerp(55,90,Blend),Mathf.Lerp(95,145,Blend),d)*.85f,.55f,DataLayer?Blend:0);
                if(!r.Loaded)alpha=Mathf.Max(alpha,.4f);if(r.Living==0&&!fields)continue;
                Color c=RegionColour(r);c.a=alpha;
                if(fields)draw.Add(p,new Color(c.r,c.g,c.b,alpha*.25f*Blend),Mathf.Clamp(28+Mathf.Sqrt(r.Living)*14,28,85),4);
                draw.Add(p,c,Mathf.Clamp(13+Mathf.Sqrt(r.Living)*4,13,36),fields?Layer==SurveyLayer.Current?3:1:1,VolumePainter.V(r.Current));
                if(alpha>.01f)VisibleClusters++;
                if(r.Key.Equals(RegionKey)){draw.Add(p,new Color(.85f,.98f,1,.8f),36,1);}
            }
            draw.Flush();
        }
        static float WaterOpticsValue(float a,float b,float value)=>Evolution.Presentation.WaterOptics.SmoothRange(a,b,value);
    }
}
