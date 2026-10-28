using UnityEngine;

namespace FrontierAtlas
{
    public sealed class FrontierUI
    {
        public static readonly Color Accent=new Color(.20f,.74f,.70f),Ink=new Color(.035f,.065f,.083f,.96f),
            White=new Color(.89f,.92f,.92f),Muted=new Color(.67f,.74f,.76f),Gold=new Color(.86f,.71f,.47f);
        public GUIStyle Title,Hero,Heading,Body,Small,Caption,ButtonStyle,Number;
        private readonly FrontierAudio _audio;
        private readonly Texture2D _gradient,_circle;
        private string _hover="";
        private float _lastHover;
        private readonly System.Collections.Generic.Dictionary<int,float> _hoverStates=new System.Collections.Generic.Dictionary<int,float>();
        public Vector2 Mouse;
        public FrontierUI(FrontierAudio audio)
        {
            _audio=audio;
            var heading=Resources.Load<Font>("Fonts/Lato-Bold");var body=Resources.Load<Font>("Fonts/Lato-Regular");
            Body=Style(body,16,White);Body.wordWrap=true;Small=Style(body,14,Muted);Small.wordWrap=true;
            Caption=Style(heading,14,Muted);Title=Style(heading,31,White);Heading=Style(heading,23,White);Heading.wordWrap=true;
            Hero=Style(heading,82,White);Number=Style(heading,49,White);
            ButtonStyle=Style(heading,16,White);ButtonStyle.alignment=TextAnchor.MiddleCenter;
            _gradient=new Texture2D(128,1,TextureFormat.RGBA32,false);
            for(int x=0;x<128;x++)_gradient.SetPixel(x,0,new Color(.012f,.025f,.039f,Mathf.Pow(1-x/127f,.65f)*.97f));
            _gradient.Apply();
            _circle=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))/32;_circle.SetPixel(x,y,new Color(1,1,1,1-Mathf.SmoothStep(.92f,1,d)));}
            _circle.Apply();
        }
        private static GUIStyle Style(Font font,int size,Color color)=>new GUIStyle{font=font,fontSize=size,normal={textColor=color},clipping=TextClipping.Clip};
        public static void Fill(Rect rect,Color color)
        {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        public static void Line(float x,float y,float width,Color color)=>Fill(new Rect(x,y,width,1),color);
        public static void Rounded(Rect rect,Color color,float radius=7)
            =>GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
        public static void Panel(Rect rect)
        {Rounded(rect,Ink,8);Line(rect.x+8,rect.y,rect.width-16,new Color(.28f,.41f,.44f,.35f));}
        public void Gradient(Rect rect)=>GUI.DrawTexture(rect,_gradient);
        public void Dot(Vector2 point,float size,Color color)
        {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(point.x-size*.5f,point.y-size*.5f,size,size),_circle);GUI.color=old;}
        public void Label(Rect rect,string text,GUIStyle style=null,Color? color=null)
        {var s=style??Body;Color prior=s.normal.textColor;if(color.HasValue)s.normal.textColor=color.Value;GUI.Label(rect,text,s);s.normal.textColor=prior;}
        public bool Button(Rect rect,string text,bool primary=false,bool enabled=true)
        {
            if(_hoverStates.Count>768)_hoverStates.Clear();
            bool hover=enabled&&rect.Contains(Mouse);
            int key=text.GetHashCode()^(Mathf.RoundToInt(rect.x)*397)^Mathf.RoundToInt(rect.y);
            _hoverStates.TryGetValue(key,out float amount);
            if(hover&&(_hover!=text||amount<.025f)&&Time.unscaledTime-_lastHover>.14f){_audio?.Play("hover",.35f);_hover=text;_lastHover=Time.unscaledTime;}
            if(Event.current.type==EventType.Repaint){amount=Mathf.Lerp(amount,hover?1:0,1-Mathf.Exp(-14*Time.unscaledDeltaTime));_hoverStates[key]=amount;}
            Color bg=primary?Color.Lerp(Accent,new Color(.27f,.81f,.76f),amount):Color.Lerp(new Color(.075f,.13f,.16f,.96f),new Color(.12f,.21f,.24f,.99f),amount);
            if(!enabled)bg=new Color(.06f,.09f,.105f,.8f);
            Rounded(rect,bg,5);Line(rect.x+6,rect.y,rect.width-12,primary?Accent:new Color(.23f,.35f,.38f,.5f));
            Label(new Rect(rect.x+10,rect.y+2,rect.width-20,rect.height-4),text,ButtonStyle,!enabled?Muted:primary?Ink:White);
            if(enabled&&GUI.Button(rect,GUIContent.none,GUIStyle.none)){_audio?.Play("click",.6f);return true;}
            return false;
        }
        public bool Toggle(Rect rect,string label,bool value)
        {
            Label(new Rect(rect.x,rect.y+4,rect.width-76,30),label,Body);
            if(Button(new Rect(rect.xMax-68,rect.y,68,30),value?"SÍ":"NO",value))value=!value;
            return value;
        }
        public float Slider(Rect rect,string label,float value,float min=0,float max=1,string suffix="%")
        {
            Label(new Rect(rect.x,rect.y,rect.width-90,24),label,new GUIStyle(Body){fontSize=14,wordWrap=false});
            string number=suffix=="%"?(value*100).ToString("0")+suffix:value.ToString("0.0")+suffix;
            var align=new GUIStyle(Caption){alignment=TextAnchor.MiddleRight};Label(new Rect(rect.xMax-88,rect.y,88,24),number,align,Accent);
            Rect bar=new Rect(rect.x,rect.y+36,rect.width,20);
            value=GUI.HorizontalSlider(bar,value,min,max,GUIStyle.none,GUIStyle.none);
            Fill(new Rect(bar.x,bar.y+6,bar.width,3),new Color(.15f,.24f,.27f));
            Fill(new Rect(bar.x,bar.y+6,bar.width*Mathf.InverseLerp(min,max,value),3),Accent);
            Dot(new Vector2(bar.x+bar.width*Mathf.InverseLerp(min,max,value),bar.y+7),10,White);
            return value;
        }
        public void Dispose(){Object.Destroy(_gradient);Object.Destroy(_circle);}
    }
}
