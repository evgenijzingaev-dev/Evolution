using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Evolution.Runtime
{
    // Owned solely by the 3D prototype; the stable scene keeps its original lighting/profile.
    internal sealed class CrystalWaterLook
    {
        readonly GameObject root;readonly VolumeProfile profile;readonly Texture2D caustics,density;
        readonly DepthOfField focus;readonly AmbientMode oldAmbient;readonly Color oldSky,oldEquator,oldGround;
        public CrystalWaterLook(Transform parent,Camera camera)
        {
            oldAmbient=RenderSettings.ambientMode;oldSky=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;
            root=new GameObject("Clear blue microworld lighting");root.transform.SetParent(parent,false);
            var key=new GameObject("Soft white underwater key");key.transform.SetParent(root.transform,false);
            key.transform.rotation=Quaternion.LookRotation(-new Vector3(-.28f,.88f,.36f).normalized,Vector3.up);
            var light=key.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.87f,.95f,1);light.intensity=1.25f;light.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.23f,.4f,.6f);
            RenderSettings.ambientEquatorColor=new Color(.075f,.19f,.32f);RenderSettings.ambientGroundColor=new Color(.035f,.075f,.17f);
            camera.allowHDR=true;var data=camera.GetUniversalAdditionalCameraData();data.requiresColorTexture=true;data.requiresDepthTexture=true;data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=.95f;bloom.intensity.value=.32f;bloom.scatter.value=.7f;
            profile.Add<Tonemapping>(true).mode.value=TonemappingMode.Neutral;
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=.5f;color.contrast.value=5;color.saturation.value=-5;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.065f;vignette.smoothness.value=.7f;
            focus=profile.Add<DepthOfField>(true);focus.mode.value=DepthOfFieldMode.Bokeh;focus.aperture.value=7.5f;focus.focalLength.value=42;
            var volume=root.AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;
            const int resolution=128;var pixels=new Color[resolution*resolution];
            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++) {
                float a=x*Mathf.PI*2/resolution,b=y*Mathf.PI*2/resolution;
                float wave=Mathf.Sin(a*3+Mathf.Sin(b*2)*.7f)*Mathf.Cos(b*4+Mathf.Sin(a*2)*.8f);
                float fine=Mathf.Sin(a*7-b*5+Mathf.Sin(a+b))*.5f+.5f;
                float lightPattern=Mathf.Pow(1-Mathf.Abs(wave),5)*(.55f+.45f*fine);
                pixels[y*resolution+x]=new Color(lightPattern,lightPattern,lightPattern,1);
            }
            caustics=new Texture2D(resolution,resolution,TextureFormat.RGBA32,true,true) {name="Seamless refracted water light",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            caustics.SetPixels(pixels);caustics.Apply(true,true);
            const int size=256;var mist=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                float u=x/(float)size,v=y/(float)size;
                float broad=TileNoise(u,v,3,17.3f)*.65f+TileNoise(u,v,7,41.7f)*.25f+TileNoise(u,v,13,71.2f)*.1f;
                float lightMask=TileNoise(u,v,4,93.4f)*.7f+TileNoise(u,v,9,24.8f)*.3f;
                mist[y*size+x]=new Color(broad,lightMask,TileNoise(u,v,6,57.1f),1);
            }
            density=new Texture2D(size,size,TextureFormat.RGBA32,true,true) {name="Continuous water density",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            density.SetPixels(mist);density.Apply(true,true);
        }
        static float TileNoise(float u,float v,float frequency,float seed)
        {
            float x=u*frequency+seed,y=v*frequency+seed*.73f;
            float a=Mathf.Lerp(Mathf.PerlinNoise(x,y),Mathf.PerlinNoise(x-frequency,y),Mathf.SmoothStep(0,1,u));
            float b=Mathf.Lerp(Mathf.PerlinNoise(x,y-frequency),Mathf.PerlinNoise(x-frequency,y-frequency),Mathf.SmoothStep(0,1,u));
            return Mathf.Lerp(a,b,Mathf.SmoothStep(0,1,v));
        }
        public void Apply(Material material) {material.SetTexture("_WaterCaustics",caustics);material.SetTexture("_WaterDensity",density);material.SetVector("_WaterFade",new Vector4(20,36,0,0));}
        public void SetFocus(float distance,bool enabled) {focus.active=enabled;focus.focusDistance.value=Mathf.Clamp(distance,2.2f,45);}
        public void Dispose()
        {
            Object.Destroy(root);Object.Destroy(caustics);Object.Destroy(density);foreach(var component in profile.components)Object.Destroy(component);Object.Destroy(profile);
            RenderSettings.ambientMode=oldAmbient;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;
        }
    }
}
