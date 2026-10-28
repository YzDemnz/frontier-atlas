using UnityEngine;
namespace FrontierAtlas
{
    // One cached full-screen pass, rendered only while the menu/intro draws.
    public sealed class RotatingEarthBackdrop
    {
        private Material material;private RenderTexture target;private int lastFrame=-1;
        public const float DegreesPerSecond=1.2f;
        public float RotationDegrees {get;private set;}
        public int RenderedFrames {get;private set;}
        public bool Ready=>material!=null;
        public RotatingEarthBackdrop()
        {
            var shader=Resources.Load<Shader>("MenuEarth");var earth=Resources.Load<Texture2D>("Cartography/EarthRelief8K");
            if(shader!=null&&shader.isSupported&&earth!=null){material=new Material(shader);material.SetTexture("_MainTex",earth);}
        }
        public bool Draw(float width,float height,bool reducedMotion)
        {
            if(material==null)return false;
            if(Event.current.type==EventType.Repaint&&lastFrame!=Time.frameCount)
            {
                int w=Mathf.Min(Screen.width,1920),h=Mathf.Max(1,Mathf.RoundToInt(w*height/width));
                if(target==null||target.width!=w||target.height!=h)
                {if(target!=null){target.Release();Object.Destroy(target);}target=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){name="Fondo Tierra · un pase",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};target.Create();}
                RotationDegrees=reducedMotion?0:Mathf.Repeat(Time.unscaledTime*DegreesPerSecond,360);
                material.SetFloat("_Aspect",width/height);material.SetFloat("_Rotation",RotationDegrees/360);material.SetFloat("_CloudTime",reducedMotion?0:Time.unscaledTime);
                var previous=RenderTexture.active;Graphics.Blit(null,target,material);RenderTexture.active=previous;lastFrame=Time.frameCount;RenderedFrames++;
            }
            if(target!=null)GUI.DrawTexture(new Rect(0,0,width,height),target,ScaleMode.StretchToFill);
            return true;
        }
        public void Dispose(){if(target!=null){target.Release();Object.Destroy(target);}if(material!=null)Object.Destroy(material);}
    }
}
