using UnityEngine;

namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private bool _attacksOpen;
        private TerritorySimulation _coastGame;
        private float _nextCoastCheck;
        private bool _canEmbark;
        private bool CanEmbark
        {get{if(_coastGame!=G||Time.unscaledTime>=_nextCoastCheck){_coastGame=G;_nextCoastCheck=Time.unscaledTime+.5f;_canEmbark=G.HasCoastalAccess(G.PlayerFaction);}return _canEmbark;}}
        private Rect AttacksBounds=>new Rect(24,230,330,365);
        private Vector2 RadialCenter=>new Vector2(Mathf.Clamp(_p.RadialPosition.x/Scale,WheelRadius+20,W-WheelRadius-20),Mathf.Clamp((Screen.height-_p.RadialPosition.y)/Scale,108+WheelRadius,Dock.y-WheelRadius-16));
        private Rect RadialBounds=>new Rect(RadialCenter.x-WheelRadius,RadialCenter.y-WheelRadius,WheelRadius*2,WheelRadius*2);
        public void ShowAttacks(bool open)=>_attacksOpen=open;
        private void DrawAttackPresets(float x,float y)
        {
            float[] values={.1f,.25f,.35f,.5f,.75f,1};
            for(int i=0;i<values.Length;i++)
            {
                var rect=new Rect(x+i*42,y,38,25);bool selected=Mathf.Abs(_p.SendFraction-values[i])<.006f;
                FrontierUI.Rounded(rect,selected?new Color(.1f,.31f,.31f):new Color(.07f,.12f,.14f),4);
                var style=new GUIStyle(_ui.Caption){alignment=TextAnchor.MiddleCenter};_ui.Label(rect,(values[i]*100).ToString("0"),style,selected?FrontierUI.Accent:FrontierUI.White);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none))_p.SendFraction=values[i];
            }
        }
        private void DrawOrderPreview()
        {
            int cell=_p.HoverCell;if(cell<0||_p.ScreenState!=FrontierScreen.Playing||_p.QuickBuildActive)return;
            int owner=G.Owners[cell],player=G.PlayerFaction;bool border=G.IsBorderTarget(player,cell);
            string name=owner>=0?G.Names[owner]:_p.World.Land[cell]?"TIERRA LIBRE":"MAR";
            string detail=owner==player?TroopNumber(G.AvailableTroops(player))+" tropas en reserva":!_p.World.Land[cell]?"Clic derecho > Invadir para navegar.":owner<0?"Doble clic: más expansión hacia este lado.":border?TroopNumber(G.AvailableTroops(owner))+" tropas defensoras":"Sin contacto terrestre. Usa Invadir si hay costa.";
            float width=320;Vector2 mouse=_ui.Mouse;Rect box=new Rect(Mathf.Clamp(mouse.x+18,24,W-width-24),Mathf.Clamp(mouse.y+20,100,H-285),width,104);
            FrontierUI.Panel(box);Label(box.x+14,box.y+9,width-28,name,_ui.Heading,30);
            Label(box.x+14,box.y+44,width-28,"ENVÍO "+(_p.SendFraction*100).ToString("0")+"%  ·  "+TroopNumber(_p.PreviewTroops)+" tropas",_ui.Body,26,FrontierUI.Accent);
            Label(box.x+14,box.y+75,width-28,detail,_ui.Small,26);
        }
        private void DrawAttacks()
        {
            if(G.CampaignEnabled){DrawCampaignAttacks();return;}
            Rect r=AttacksBounds;FrontierUI.Panel(r);Label(r.x+18,r.y+14,r.width-36,"ATAQUES",_ui.Heading,32);
            Label(r.x+18,r.y+51,r.width-36,"Tu operación e incursiones enemigas",_ui.Small,25);
            int row=0,total=0;
            for(int pass=0;pass<2;pass++)for(int f=0;f<G.FactionCount;f++)
            {
                bool own=f==G.PlayerFaction;if((pass==0)!=own||!G.HasOrder(f))continue;
                bool incoming=!own&&(G.AttackFactions[f]==G.PlayerFaction||G.IsNavalThreat(f,G.PlayerFaction));
                if(!own&&!incoming)continue;total++;if(row>=3)continue;
                float y=r.y+90+row++*70;
                string title=own?(G.FleetActive[f]?"Tu invasión naval":G.AttackFactions[f]<0?"Expansión general":"Contra "+G.Names[G.AttackFactions[f]]):G.Names[f]+(G.FleetActive[f]?" invade":" ataca");
                Label(r.x+18,y,223,title,_ui.Body,27,incoming?FrontierUI.Gold:FrontierUI.White);
                Label(r.x+18,y+30,223,TroopNumber(G.CommittedTroops[f])+" tropas · "+StateTitle(G.States[f]).ToLowerInvariant(),_ui.Small,29);
                if(_ui.Button(new Rect(r.xMax-73,y+10,55,31),"VER"))_p.FocusOperation(f);
            }
            if(total==0)Label(r.x+18,r.y+104,r.width-36,"No hay operaciones activas.",_ui.Body,55);
            if(total>3)Label(r.x+18,r.y+301,r.width-36,"+"+(total-3)+" incursiones adicionales",_ui.Small,24);
            if(_ui.Button(new Rect(r.x+18,r.yMax-47,r.width-36,32),"RETIRAR · RECUPERAR TROPAS",false,G.HasOrder(G.PlayerFaction)))_p.StopOrder();
        }
    }
}
