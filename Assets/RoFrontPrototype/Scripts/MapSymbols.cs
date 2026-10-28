using UnityEngine;
namespace FrontierAtlas
{
    public static class MapSymbols
    {
        private static readonly Texture2D[] _icons=new Texture2D[9];
        public static Texture2D Get(int type)
        {
            type=Mathf.Clamp(type,0,8);if(_icons[type]!=null)return _icons[type];var p=new Color32[4096];
            System.Action<float,float,float,float> line=(a,b,c,d)=>Line(p,new Vector2(a,b),new Vector2(c,d),1.7f);
            System.Action<float,float,float,float> rect=(x,y,w,h)=>{line(x,y,x+w,y);line(x+w,y,x+w,y+h);line(x+w,y+h,x,y+h);line(x,y+h,x,y);};
            switch(type)
            {
                case 0:rect(10,30,11,24);rect(27,10,11,44);rect(44,23,11,31);line(7,55,58,55);break;
                case 1:rect(10,34,45,21);line(10,34,22,25);line(22,25,32,34);line(32,34,43,25);line(43,25,55,34);rect(44,8,8,19);line(20,44,27,44);line(38,44,45,44);break;
                case 2:Circle(p,32,12,5);line(32,17,32,54);line(18,29,46,29);line(12,39,19,49);line(19,49,32,55);line(32,55,45,49);line(45,49,52,39);line(12,39,12,46);line(52,39,52,46);break;
                case 3:line(12,12,52,12);line(52,12,49,40);line(49,40,32,56);line(32,56,15,40);line(15,40,12,12);line(32,23,32,43);line(22,33,42,33);break;
                case 4:line(32,7,22,23);line(22,23,22,46);line(22,46,32,54);line(32,54,42,46);line(42,46,42,23);line(42,23,32,7);line(22,37,13,52);line(13,52,22,48);line(42,37,51,52);line(51,52,42,48);break;
                case 5:line(14,55,50,55);line(32,55,32,42);line(16,24,43,44);line(16,24,20,40);line(20,40,43,44);Arc(p,18,44,30,-90,0);Arc(p,18,44,20,-90,0);break;
                case 6:Circle(p,32,32,25);for(int k=0;k<10;k++){float a=(-90+k*36)*Mathf.Deg2Rad,b=(-90+(k+1)*36)*Mathf.Deg2Rad,r=k%2==0?18:8,s=k%2==0?8:18;line(32+Mathf.Cos(a)*r,32+Mathf.Sin(a)*r,32+Mathf.Cos(b)*s,32+Mathf.Sin(b)*s);}break;
                case 7:rect(8,21,32,25);rect(40,29,15,17);Circle(p,19,50,5);Circle(p,47,50,5);break;
                default:line(32,8,17,51);line(17,51,32,44);line(32,44,47,51);line(47,51,32,8);break;
            }
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,name="Símbolo provisional "+type};texture.SetPixels32(p);texture.Apply(false,true);_icons[type]=texture;return texture;
        }
        private static void Circle(Color32[] p,float x,float y,float radius)=>Arc(p,x,y,radius,0,360);
        private static void Arc(Color32[] p,float x,float y,float radius,float start,float end)
        {Vector2 previous=Vector2.zero;for(int i=0;i<=36;i++){float a=Mathf.Lerp(start,end,i/36f)*Mathf.Deg2Rad;Vector2 point=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);if(i>0)Line(p,previous,point,1.7f);previous=point;}}
        private static void Line(Color32[] pixels,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=b-a;float length=d.sqrMagnitude;
            for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-width-1));y<=Mathf.Min(63,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+width+1));y++)
            for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-width-1));x<=Mathf.Min(63,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+width+1));x++)
            {Vector2 p=new Vector2(x+.5f,y+.5f);float t=length>.001f?Mathf.Clamp01(Vector2.Dot(p-a,d)/length):0;float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width-.5f,width+.5f,Vector2.Distance(p,a+d*t)));int id=(63-y)*64+x;if(alpha>pixels[id].a/255f)pixels[id]=new Color(1,1,1,alpha);}
        }
    }
}
