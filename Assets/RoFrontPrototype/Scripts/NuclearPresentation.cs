using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierPrototype
    {
        public int IncomingNuclearCount {get;private set;}
        public float IncomingNuclearEta {get;private set;}
        public int IncomingNuclearOwner {get;private set;}=-1;
        private float _nextNuclearScan;
        public void RefreshNuclearWarning()
        {
            IncomingNuclearCount=0;IncomingNuclearEta=float.MaxValue;IncomingNuclearOwner=-1;
            if(!HasMatch||_game==null||_game.Outcome!=CampaignOutcome.Playing)return;
            int player=_game.PlayerFaction;
            foreach(var missile in _game.Projectiles)
            {
                if(!missile.active||missile.owner==player||!_game.Rules.Weapon(missile.kind).radiation)continue;
                if(missile.targetNation!=player&&_game.Owners[missile.target]!=player&&!_game.NuclearFriendlyRisk(player,missile.target,_game.Rules.Weapon(missile.kind).radius,false))continue;
                IncomingNuclearCount++;float eta=Mathf.Max(0,missile.duration-missile.age);
                if(eta<IncomingNuclearEta){IncomingNuclearEta=eta;IncomingNuclearOwner=missile.owner;}
            }
        }
        private void OnNuclearDetonation(int cell,float radius)
        {if(HasMatch)_navigation.NuclearShake(Settings.ReducedMotion);RefreshNuclearWarning();}
        private void LateUpdate()
        {
            _navigation.ReducedMotion=Settings.ReducedMotion;
            if(Time.unscaledTime>=_nextNuclearScan){_nextNuclearScan=Time.unscaledTime+.1f;RefreshNuclearWarning();}
        }
    }
    public sealed partial class FrontierInterface
    {
        private Rect NuclearWarningBounds=>new Rect((W-500)*.5f,105,500,94);
        private Texture2D _hazard;
        private void DrawNuclearWarning()
        {
            if(_p.IncomingNuclearCount==0)return;
            float pulse=_p.Settings.ReducedMotion?1:.55f+.45f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*9));
            Rect box=NuclearWarningBounds;FrontierUI.Rounded(box,new Color(.11f,.018f,.023f,.97f),10);
            FrontierUI.Fill(new Rect(box.x,box.y+12,3,box.height-24),new Color(1,.12f,.14f,pulse));
            if(_hazard==null)
            {
                _hazard=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {float py=y/63f,px=Mathf.Abs(x-31.5f)/31.5f;bool triangle=py>.1f&&py<.94f&&px<(.94f-py)/.84f;bool mark=(Mathf.Abs(x-31.5f)<3&&y>23&&y<44)||(Mathf.Abs(x-31.5f)<3&&y>15&&y<21);_hazard.SetPixel(x,y,triangle?(mark?new Color(.1f,.01f,.015f):Color.white):Color.clear);}_hazard.Apply();
            }
            Color saved=GUI.color;GUI.color=new Color(1,.15f,.18f,pulse);GUI.DrawTexture(new Rect(box.x+18,box.y+16,60,60),_hazard);GUI.color=saved;
            _ui.Label(new Rect(box.x+92,box.y+10,390,30),"WARNING",_ui.Heading,new Color(1,.23f,.25f,pulse));
            string count=_p.IncomingNuclearCount>1?" · "+_p.IncomingNuclearCount+" OJIVAS":"";
            _ui.Label(new Rect(box.x+92,box.y+39,390,23),"ATAQUE NUCLEAR ENTRANTE"+count,_ui.Caption,new Color(1,.83f,.81f));
            string name=_p.IncomingNuclearOwner>=0?G.Names[_p.IncomingNuclearOwner]:"Enemigo";
            _ui.Label(new Rect(box.x+92,box.y+64,390,22),name+"  ·  Impacto en "+Mathf.CeilToInt(_p.IncomingNuclearEta)+" s",_ui.Small,new Color(.9f,.67f,.66f));
        }
    }
}
