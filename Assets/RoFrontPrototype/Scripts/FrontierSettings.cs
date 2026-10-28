using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrontierAtlas
{
    public sealed class FrontierSettings
    {
        public float Master=.8f,Music=.42f,Effects=.72f,ZoomSpeed=1;
        public bool Fullscreen=true,ReducedMotion,Labels=true;
        private int _windowWidth=1600,_windowHeight=900;
        public int Quality=2,FrameLimit=60;
        public void Load()
        {
            Master=PlayerPrefs.GetFloat("FA.Master",.8f);Music=PlayerPrefs.GetFloat("FA.Music",.42f);
            Effects=PlayerPrefs.GetFloat("FA.Effects",.72f);ZoomSpeed=PlayerPrefs.GetFloat("FA.Zoom",1);
            Fullscreen=PlayerPrefs.GetInt("FA.Fullscreen",1)==1;ReducedMotion=PlayerPrefs.GetInt("FA.Motion",0)==1;
            Labels=PlayerPrefs.GetInt("FA.Labels",1)==1;Quality=Mathf.Clamp(PlayerPrefs.GetInt("FA.Quality",2),0,2);
            FrameLimit=PlayerPrefs.GetInt("FA.Fps",60);
        }
        public void Save()
        {
            PlayerPrefs.SetFloat("FA.Master",Master);PlayerPrefs.SetFloat("FA.Music",Music);PlayerPrefs.SetFloat("FA.Effects",Effects);
            PlayerPrefs.SetFloat("FA.Zoom",ZoomSpeed);PlayerPrefs.SetInt("FA.Fullscreen",Fullscreen?1:0);
            PlayerPrefs.SetInt("FA.Motion",ReducedMotion?1:0);PlayerPrefs.SetInt("FA.Labels",Labels?1:0);
            PlayerPrefs.SetInt("FA.Quality",Quality);PlayerPrefs.SetInt("FA.Fps",FrameLimit);PlayerPrefs.Save();
        }
        public void Apply(bool display=false)
        {
            Application.targetFrameRate=FrameLimit;QualitySettings.vSyncCount=0;
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                pipeline.renderScale=Quality==0? .85f:1;
            if(display&&!Application.isEditor)
            {
                if(Fullscreen)
                {if(!Screen.fullScreen){_windowWidth=Screen.width;_windowHeight=Screen.height;}var desktop=Screen.currentResolution;Screen.SetResolution(desktop.width,desktop.height,FullScreenMode.FullScreenWindow);}
                else Screen.SetResolution(Mathf.Min(_windowWidth,Screen.currentResolution.width),Mathf.Min(_windowHeight,Screen.currentResolution.height),FullScreenMode.Windowed);
            }
        }
    }
}
