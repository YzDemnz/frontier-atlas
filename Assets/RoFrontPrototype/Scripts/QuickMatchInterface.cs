using System;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierPrototype
    {
        public MatchMode ChosenMode=MatchMode.Campaign;
        private float _quickDeployUntil;public float QuickDeploymentRemaining=>_game!=null&&_game.IsQuickMatch?Mathf.Max(0,_quickDeployUntil-Time.unscaledTime):0;
        public float QuickBattleStartedAt;
        public int QuickWeaponSelection=-1;
        public bool QuickWarshipPlacement;
        public void SelectQuickSlot(int index)
        {
            if(index<6){SelectQuickBuilding(index);return;}
            CancelQuickBuilding();CloseRadial();CloseCampaignPanel();
            if(index==6){QuickWarshipPlacement=true;Notify("Buque: elige agua cercana a un puerto propio operativo. Esc para cancelar.");return;}
            if(index<7||index>9)return;
            OpenCampaignPanel(CampaignPanel.Nuclear,HoverCell);QuickWeaponSelection=index==7?(int)WeaponKind.Atomic:index==8?(int)WeaponKind.Hydrogen:(int)WeaponKind.MIRV;
            Notify("Elige el punto de impacto para ver el radio. Después confirma el lanzamiento.");
        }
        private void PlaceQuickWarship(int target)
        {
            if(target<0||_world.Land[target]){Notify("Elige un punto en el mar.");return;}
            Building port=null;float best=float.PositiveInfinity;
            foreach(var b in _game.Buildings)if(b.owner==_game.PlayerFaction&&b.kind==BuildingKind.Port&&b.Operational)
            {float d=_game.SeaDistance(b.cell,target);if(d<best){best=d;port=b;}}
            if(port==null){Notify("Necesitas un puerto operativo para desplegar el buque.");return;}
            var result=_game.DeployWarship(_game.PlayerFaction,port.cell);Notify(result.message);
            if(result.success){QuickWarshipPlacement=false;_game.MoveWarship(_game.Warships[_game.Warships.Count-1].id,target);}
        }
    }
    public sealed partial class FrontierInterface
    {
        private readonly System.Collections.Generic.Dictionary<int,GUIStyle> _quickNameStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
        private GUIStyle _quickTroopStyle,_quickPriceStyle,_quickBannerStyle;
        private static readonly float[] QuickSendPresets={.1f,.25f,.35f,.5f,.75f,1};
        private static string Compact(double v)=>v>=1000000?(v/1000000).ToString("0.##")+"M":v>=1000?(v/1000).ToString("0.#")+"K":v.ToString("0");
        private readonly string[] _quickSlotNames={"Ciudad","Puerto","Fábrica","Defensa","SAM","Silo","Buque","Atómica","Hidrógeno","MIRV"};
        private void DrawQuickMatchGame()
        {
            DrawProtectionRange();DrawWeaponPreview();DrawWorldSymbols();DrawQuickNationLabels();DrawQuickBuildPreview();
            int f=G.PlayerFaction,leader=Powers.Ranking[0];Rect score=new Rect(W*.5f-230,14,460,72);FrontierUI.Panel(score);
            Label(score.x+15,score.y+8,100,G.ProtectionRemaining(f)>0?"PROTEGIDO":"TIEMPO",_ui.Caption,22);Label(score.x+15,score.y+32,100,TimeSpan.FromSeconds(G.ProtectionRemaining(f)>0?G.ProtectionRemaining(f):G.ElapsedTime).ToString(@"mm\:ss"),_ui.Heading,28);
            Label(score.x+125,score.y+8,300,"CONTROL MUNDIAL                         META 80%",_ui.Caption,22);
            FrontierUI.Rounded(new Rect(score.x+125,score.y+34,305,5),new Color(.12f,.17f,.23f),2);FrontierUI.Rounded(new Rect(score.x+125,score.y+34,305*Mathf.Clamp01(G.WorldControl(f)/.8f),5),FrontierUI.Accent,2);
            Label(score.x+125,score.y+45,315,"Tú "+(G.WorldControl(f)*100).ToString("0.0")+"%  ·  "+G.Names[leader]+" "+(G.WorldControl(leader)*100).ToString("0.0")+"%",_ui.Small,23);
            if(_ui.Button(new Rect(18,18,70,32),"MENÚ"))_p.ShowPause();if(_ui.Button(new Rect(96,18,82,32),"ARSENAL"))_p.OpenCampaignPanel(CampaignPanel.Arsenal,_p.ContextCell);
            if(_ui.Button(new Rect(W-148,18,130,32),"POTENCIAS",_powersOpen))_powersOpen=!_powersOpen;
            if(_ui.Button(new Rect(W-148,59,130,32),"ATAQUES",_attacksOpen))_attacksOpen=!_attacksOpen;
            FrontierUI.Rounded(new Rect(18,55,280,27),new Color(.065f,.075f,.13f,.92f),5);Label(28,58,260,"PARTIDA RÁPIDA · SOLO CONTRA IA",_ui.Caption,22,FrontierUI.White);DrawQuickMatchDock();
            if(Time.unscaledTime-_p.QuickBattleStartedAt<2.5f)Label(W*.5f-240,H*.38f,480,"COMIENZA LA GUERRA",(_quickBannerStyle??(_quickBannerStyle=new GUIStyle(_ui.Hero){fontSize=36,alignment=TextAnchor.MiddleCenter})),70,FrontierUI.Gold);
            if(_powersOpen)DrawPowers();if(_attacksOpen)DrawAttacks();DrawCampaignPanel();DrawInspection();DrawTarget();
            if(G.Paused)Label(W*.5f-90,96,180,"PARTIDA EN PAUSA",_ui.Caption,25,FrontierUI.Gold);
            if(_p.NoticeExpiry>Time.unscaledTime){Rect toast=new Rect(24,Dock.y-65,Mathf.Min(760,W-48),48);FrontierUI.Panel(toast);Label(toast.x+14,toast.y+8,toast.width-28,_p.Notice,_ui.Small,35);}
        }
        private void DrawQuickMatchDock()
        {
            int f=G.PlayerFaction;Rect r=Dock;FrontierUI.Rounded(r,new Color(.065f,.075f,.13f,.97f),12);
            float unit=r.width/10,top=r.y+8;Label(r.x+18,top,unit*2.4f,G.Names[f],_ui.Heading,28,Powers.DisplayColors[f]);
            Label(r.x+18,top+28,unit*2.3f,(G.WorldControl(f)*100).ToString("0.00")+"% del mundo",_ui.Small,20);
            float tx=r.x+unit*2.5f;Label(tx,top,unit*2.2f,"TROPAS   +"+Compact(G.ReinforcementRate(f))+"/s",_ui.Caption,23);
            Label(tx,top+23,unit*2.2f,Compact(G.AvailableTroops(f))+" / "+Compact(G.Capacity[f]),_ui.Heading,28);
            float ax=r.x+unit*4.9f;Label(ax,top,unit*2.5f,"ATAQUE    "+Mathf.RoundToInt(_p.SendFraction*100)+"%",_ui.Caption,23);
            var values=QuickSendPresets;for(int i=0;i<values.Length;i++)if(_ui.Button(new Rect(ax+i*unit*.39f,top+23,unit*.37f,25),(values[i]*100).ToString("0")+"%",Mathf.Abs(_p.SendFraction-values[i])<.005f))_p.SendFraction=values[i];
            _p.SendFraction=GUI.HorizontalSlider(new Rect(ax,r.y+57,unit*2.35f,10),_p.SendFraction,.01f,1);
            float gx=r.x+unit*7.5f;Label(gx,top,unit*2.3f,"ORO   +"+Compact(G.IncomeRate(f))+"/s",_ui.Caption,23);Label(gx,top+23,unit*2.3f,Compact(G.Gold[f]),_ui.Heading,28,FrontierUI.Gold);
            int hover=-1;float hoverX=0;float y=r.y+68;for(int i=0;i<10;i++)
            {
                Rect slot=new Rect(r.x+10+i*(r.width-20)/10,y,(r.width-20)/10-5,54);bool active=i<6?_p.QuickBuildActive&&_p.SelectedBuilding==FrontierPrototype.QuickBuildings[i]:i==6?_p.QuickWarshipPlacement:_p.QuickWeaponSelection==(i==7?(int)WeaponKind.Atomic:i==8?(int)WeaponKind.Hydrogen:(int)WeaponKind.MIRV);
                FrontierUI.Rounded(slot,active?new Color(.14f,.28f,.31f):new Color(.09f,.10f,.17f),6);Label(slot.x+5,slot.y+1,24,i==9?"0":(i+1).ToString(),_ui.Caption,18);
                int symbol=i<6?(int)FrontierPrototype.QuickBuildings[i]:i==6?8:i==7?4:i==8?5:4;DrawSymbol(new Rect(slot.center.x-10,slot.y+3,22,22),symbol,FrontierUI.White);
                double price=i<6?G.ConstructionPrice(f,FrontierPrototype.QuickBuildings[i],_p.HoverCell):i==6?G.WarshipPrice(f):G.Rules.Weapon(i==7?WeaponKind.Atomic:i==8?WeaponKind.Hydrogen:WeaponKind.MIRV).cost;
                _ui.Label(new Rect(slot.x+2,slot.y+27,slot.width-4,23),Compact(price),(_quickPriceStyle??(_quickPriceStyle=new GUIStyle(_ui.Small){fontSize=12,alignment=TextAnchor.MiddleCenter})),G.Gold[f]>=price?FrontierUI.Gold:new Color(1,.40f,.40f));
                if(GUI.Button(slot,new GUIContent("",_quickSlotNames[i]),GUIStyle.none))_p.SelectQuickSlot(i);
                if(slot.Contains(_ui.Mouse)){hover=i;hoverX=slot.x;}
            }
            if(hover>=0)DrawQuickSlotTooltip(hover,hoverX);
        }
        private void DrawQuickSlotTooltip(int slot,float x)
        {
            Rect r=new Rect(Mathf.Clamp(x,18,W-318),Dock.y-145,300,133);FrontierUI.Panel(r);Label(r.x+14,r.y+9,272,_quickSlotNames[slot]+"  ["+(slot==9?"0":(slot+1).ToString())+"]",_ui.Heading,28);
            string[] details={"Añade 25.000 de capacidad por nivel. Se incorpora a la red de fábricas.","Comercio marítimo automático y despliegue de buques. Necesita costa.","Conecta ciudades, puertos y fábricas dentro de su círculo. Los trenes entregan oro.","Protege las fronteras: multiplica por 5 las bajas atacantes y por 3 el tiempo de conquista.","Intercepción automática de misiles compatibles. Cada posición tiene su propia recarga.","Necesario para lanzar bombas. Debe estar operativo, tener munición y alcanzar el objetivo.","Elige agua para navegar desde un puerto operativo. Combate barcos e invasiones.","Bomba atómica. Radio de 30 celdas. También destruye tus tropas, edificios y territorio.","Bomba de hidrógeno. Radio de 100 celdas. Revisa el área y confirma antes de disparar.","Múltiples ojivas dispersas por la nación objetivo. El círculo es el radio de cada ojiva."};
            Label(r.x+14,r.y+43,272,details[slot],_ui.Small,73);
        }
        private void DrawQuickNationLabels()
        {
            _labels.Clear();for(int pass=0;pass<2;pass++)for(int f=0;f<G.FactionCount;f++)
            {
                if((pass==0)!=(f==G.PlayerFaction)||G.TerritoryCounts[f]==0||G.CapitalCells[f]<0)continue;
                if(_camera.orthographicSize>250&&G.TerritoryCounts[f]<80&&f!=G.PlayerFaction)continue;
                int cell=G.Owners[G.CapitalCells[f]]==f?G.CapitalCells[f]:G.LastAdvanceCells[f];if(cell<0)continue;Vector2 p=Project(cell);
                if(p.x<20||p.x>W-20||p.y<102||p.y>Dock.y-30)continue;float size=Mathf.Clamp(13+Mathf.Sqrt(G.TerritoryCounts[f])*1.1f,13,42);if(f==G.PlayerFaction)size=Mathf.Max(19,size);
                int fontSize=Mathf.RoundToInt(size);if(!_quickNameStyles.TryGetValue(fontSize,out var style)){style=new GUIStyle(_ui.Heading){fontSize=fontSize,alignment=TextAnchor.MiddleCenter,wordWrap=false};_quickNameStyles[fontSize]=style;}string name=G.Names[f];float width=Mathf.Clamp(style.CalcSize(new GUIContent(name)).x+8,55,350);Rect box=new Rect(p.x-width*.5f,p.y,width,size+24);
                bool overlaps=false;foreach(var b in _labels)if(b.Overlaps(box)){overlaps=true;break;}if(overlaps&&f!=G.PlayerFaction)continue;_labels.Add(box);
                Color outline=Powers.DisplayColors[f];_ui.Label(new Rect(box.x-1,box.y-1,box.width,box.height),name,style,outline);_ui.Label(new Rect(box.x+1,box.y+1,box.width,box.height),name,style,outline);_ui.Label(box,name,style,new Color(.055f,.075f,.11f));
                _ui.Label(new Rect(box.x,box.y+size+9,box.width,23),Compact(G.AvailableTroops(f)),(_quickTroopStyle??(_quickTroopStyle=new GUIStyle(_ui.Caption){alignment=TextAnchor.MiddleCenter,fontSize=13})),new Color(.06f,.075f,.1f));
            }
            if(!_p.RadialOpen)DrawOrderPreview();
        }
        private void DrawQuickAlliance(float width,int other)
        {
            int f=G.PlayerFaction;var treaty=G.TreatyBetween(f,other);bool allied=G.AreAllied(f,other);Rect r=SimpleAllianceBounds;
            Label(r.x+18,r.y+85,width,allied?"Aliados. Traicionar te vuelve vulnerable durante 30 segundos.":treaty!=null&&treaty.state==TreatyState.Pending?"Propuesta de alianza pendiente.":"Pacto bilateral para proteger la frontera y comerciar.",_ui.Small,53);
            if(treaty!=null&&treaty.state==TreatyState.Pending&&treaty.target==f)
            {if(_ui.Button(new Rect(r.x+18,r.y+153,156,36),"ACEPTAR",true,!G.Paused))Action(G.AnswerAlliance(f,other,true));if(_ui.Button(new Rect(r.x+185,r.y+153,156,36),"RECHAZAR",false,!G.Paused))Action(G.AnswerAlliance(f,other,false));}
            else if(_ui.Button(new Rect(r.x+18,r.y+153,width,36),allied?"TRAICIONAR":treaty!=null&&treaty.state==TreatyState.Pending?"ESPERANDO RESPUESTA":"PROPONER ALIANZA",true,!G.Paused&&(allied||treaty==null||treaty.state!=TreatyState.Pending)))Action(allied?G.Betray(f,other):G.ProposeAlliance(f,other));
        }
    }
}
