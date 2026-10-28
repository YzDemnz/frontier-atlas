using UnityEngine;

namespace FrontierAtlas
{
    // Full-resolution artwork plus restrained camera and typography; no simulation changes.
    public sealed class CinematicPresentation
    {
        private readonly Texture2D _art,_shade,_vignette;
        public readonly RotatingEarthBackdrop Earth=new RotatingEarthBackdrop();
        private readonly GUIStyle _wordmark,_eyebrow,_subtitle,_menuTitle,_quiet;
        public bool ArtworkLoaded=>_art!=null;
        public GUIStyle MenuTitle=>_menuTitle;
        public CinematicPresentation(FrontierUI ui)
        {
            _art=Resources.Load<Texture2D>("Cinematic/EarthOrbitIntro");
            var font=Resources.Load<Font>("Fonts/Lato-Light");
            _wordmark=new GUIStyle(ui.Hero){font=font,fontSize=112};
            _eyebrow=new GUIStyle(ui.Caption){font=ui.Body.font,fontSize=19};
            _subtitle=new GUIStyle(ui.Body){fontSize=15};
            _quiet=new GUIStyle(ui.Small){fontSize=12};
            _menuTitle=new GUIStyle(ui.Hero){font=font,fontSize=72};
            _shade=new Texture2D(128,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int x=0;x<128;x++)_shade.SetPixel(x,0,new Color(.008f,.017f,.027f,Mathf.Lerp(.88f,0,Mathf.SmoothStep(0,1,x/127f))));
            _shade.Apply();
            _vignette=new Texture2D(128,72,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<72;y++)for(int x=0;x<128;x++)
            {
                Vector2 p=new Vector2((x+.5f)/128f,(y+.5f)/72f);
                float edge=Mathf.Clamp01((Vector2.Distance(p,new Vector2(.55f,.48f))-.28f)*1.5f);
                _vignette.SetPixel(x,y,new Color(.005f,.012f,.02f,edge*.65f));
            }
            _vignette.Apply();
        }
        private static float Ease(float t)=>t*t*(3-2*t);
        private static float Phase(float time,float start,float end)=>Ease(Mathf.InverseLerp(start,end,time));
        public void Background(float width,float height,float progress,bool reducedMotion,bool menu=false)
        {
            FrontierUI.Fill(new Rect(0,0,width,height),new Color(.005f,.012f,.02f));
            if(_art==null)return;
            float zoom=reducedMotion?1.015f:Mathf.Lerp(1.075f,1.015f,Ease(Mathf.Clamp01(progress)));
            if(menu&&!reducedMotion)zoom=1.018f+Mathf.Sin(Time.unscaledTime*.035f)*.003f;
            float x=(width-width*zoom)*.5f,y=(height-height*zoom)*.5f;
            if(!Earth.Draw(width,height,reducedMotion))GUI.DrawTexture(new Rect(x,y,width*zoom,height*zoom),_art,ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(0,0,width*.86f,height),_shade);
            GUI.DrawTexture(new Rect(0,0,width,height),_vignette);
        }
        public void Intro(FrontierPrototype p,FrontierUI ui,float width,float height)
        {
            float t=p.IntroProgress,seconds=t*FrontierPrototype.IntroDuration;
            Background(width,height,t,p.Settings.ReducedMotion);
            float worldIn=Phase(seconds,0,1.25f);
            FrontierUI.Fill(new Rect(0,0,width,height),new Color(.004f,.009f,.016f,1-worldIn));
            float reveal=Phase(seconds,1.05f,2.65f);
            float lift=p.Settings.ReducedMotion?0:14*(1-reveal);
            float x=width*.075f,y=height*.38f+lift;
            Color saved=GUI.color;GUI.color=new Color(1,1,1,reveal);
            ui.Label(new Rect(x+5,y-37,590,34),"F R O N T E R A",_eyebrow,new Color(.81f,.88f,.88f));
            ui.Label(new Rect(x,y,580,145),"ATLAS",_wordmark,new Color(.95f,.96f,.94f));
            float line=Phase(seconds,2.2f,3.8f);
            FrontierUI.Fill(new Rect(x+6,y+144,96*line,1),new Color(.77f,.65f,.45f,.72f));
            GUI.color=new Color(1,1,1,Phase(seconds,3.0f,4.4f));
            ui.Label(new Rect(x+6,y+177,520,32),"Tu país. Tu historia.",_subtitle,new Color(.79f,.85f,.86f));
            GUI.color=saved;
            ui.Label(new Rect(x+6,height-77,420,25),"UN MUNDO POR CONSTRUIR",_quiet,new Color(.63f,.74f,.77f,.8f));
            Rect skip=new Rect(width-237,height-83,177,34);
            bool hover=skip.Contains(ui.Mouse);
            if(hover)FrontierUI.Rounded(skip,new Color(.04f,.075f,.09f,.68f),5);
            var centered=new GUIStyle(_quiet){alignment=TextAnchor.MiddleCenter};
            ui.Label(skip,"OMITIR  ·  ESPACIO",centered,new Color(.74f,.82f,.84f,hover?1:.72f));
            if(GUI.Button(skip,GUIContent.none,GUIStyle.none))p.FinishIntro();
            float end=1-p.IntroVisibility;
            if(end>0)FrontierUI.Fill(new Rect(0,0,width,height),new Color(.004f,.009f,.016f,end));
        }
        public void Dispose(){Earth.Dispose();Object.Destroy(_shade);Object.Destroy(_vignette);}
    }
}
