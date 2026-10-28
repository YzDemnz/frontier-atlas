using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    public sealed partial class FrontierInterface : MonoBehaviour
    {
        private FrontierPrototype _p;
        private Camera _camera;
        private FrontierUI _ui;
        private CinematicPresentation _cinematic;
        private int _settingsTab;
        private bool _powersOpen;
        public void ShowPowers(bool open)=>_powersOpen=open;
        private Rect Dock=>G.IsQuickMatch?new Rect(24,H-148,W-48,132):new Rect((W-Mathf.Min(1140,W-48))*.5f,H-168,Mathf.Min(1140,W-48),144);
        private string _search="";
        private Vector2 _scroll;
        private readonly List<Rect> _labels=new List<Rect>();
        private float Scale=>Mathf.Min(Screen.height/900f,Screen.width/1200f);
        private float W=>Screen.width/Scale;
        private float H=>Screen.height/Scale;
        private TerritorySimulation G=>_p.Simulation;
        private PowerBalance Powers=>_p.Visuals.Powers;
        private static Color Fade(Color color,float alpha){color.a=alpha;return color;}
        private static string TroopNumber(float value)=>value<10?Mathf.Max(0,value).ToString("0.##"):Mathf.FloorToInt(value).ToString("N0");
        public void Initialize(FrontierPrototype prototype,Camera camera){_p=prototype;_camera=camera;}
        public void SelectSettingsTab(int tab)=>_settingsTab=Mathf.Clamp(tab,0,2);
        public bool BlocksPointer(Vector2 mouse)
        {
            if(_p.ScreenState==FrontierScreen.SpawnSelection){Vector2 location=new Vector2(mouse.x,Screen.height-mouse.y)/Scale;return location.y<108||location.y>H-126;}
            if(_p.ScreenState!=FrontierScreen.Playing)return true;
            Vector2 pos=new Vector2(mouse.x,Screen.height-mouse.y)/Scale;
            return (_p.HasWeaponPreview&&WeaponConfirmationBounds.Contains(pos))||(_p.IncomingNuclearCount>0&&NuclearWarningBounds.Contains(pos))||(WarActionsOpen&&WarActionsBounds.Contains(pos))||(SimpleAllianceOpen&&SimpleAllianceBounds.Contains(pos))||WorldBoardOpen||new Rect(24,90,500,35).Contains(pos)||pos.y<(G.IsQuickMatch?98:84)||Dock.Contains(pos)||(G.CampaignEnabled&&_p.Panel!=CampaignPanel.None&&CampaignBounds.Contains(pos))||(_p.RadialOpen&&RadialBounds.Contains(pos))||(_attacksOpen&&AttacksBounds.Contains(pos))||(_powersOpen&&new Rect(W-324,100,300,302).Contains(pos))||(_p.InspectedFaction>=0&&_p.InspectedFaction!=G.PlayerFaction&&new Rect(24,104,280,110).Contains(pos));
        }
        private void OnGUI()
        {
            if(_p==null||G==null)return;
            if(_p.IsPresentationAudit&&(Event.current.isKey||Event.current.isMouse||Event.current.type==EventType.ScrollWheel))Event.current.Use();
            if(_ui==null){_ui=new FrontierUI(_p.Audio);_cinematic=new CinematicPresentation(_ui);}
            GUI.skin.font=_ui.Body.font;GUI.skin.textField.font=_ui.Body.font;GUI.skin.textArea.font=_ui.Body.font;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Scale,Scale,1));_ui.Mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)/Scale;
            if(_p.ScreenState==FrontierScreen.Intro){DrawIntro();return;}
            if(_p.ScreenState==FrontierScreen.SpawnSelection){DrawSpawnSelection();return;}
            if(_p.ScreenState==FrontierScreen.MainMenu)
            {DrawMainMenu();DrawWorldBoard();if(_p.MenuReveal<1)FrontierUI.Fill(new Rect(0,0,W,H),new Color(.004f,.009f,.016f,1-_p.MenuReveal));return;}
            if(_p.ScreenState==FrontierScreen.Playing){DrawGame();if(_p.RadialOpen||RadialAnimating)DrawRadial();DrawSimpleAlliance();DrawWarActions();DrawWorldBoard();DrawNuclearWarning();DrawWeaponConfirmation();return;}
            bool previousEnabled=GUI.enabled;GUI.enabled=false;if(_p.HasMatch)DrawGame();else DrawMainBackdrop();GUI.enabled=previousEnabled;
            FrontierUI.Fill(new Rect(0,0,W,H),new Color(.008f,.018f,.027f,.79f));
            switch(_p.ScreenState)
            {
                case FrontierScreen.CountryPicker:DrawCountries();break;
                case FrontierScreen.Settings:DrawSettings();break;
                case FrontierScreen.PauseMenu:DrawPause();DrawWorldBoard();break;
                case FrontierScreen.Results:DrawResults();break;
            }
        }
        private void Label(float x,float y,float w,string text,GUIStyle style=null,float height=28,Color? color=null)
            =>_ui.Label(new Rect(x,y,w,height),text,style,color);
        private void Brand(float x,float y,bool compact=false)
        {
            float size=compact?21:32;
            FrontierUI.Fill(new Rect(x,y+3,3,size),FrontierUI.Accent);
            FrontierUI.Fill(new Rect(x+7,y+8,3,size-10),Fade(FrontierUI.Accent,.5f));
            Label(x+23,y-2,350,"ATLAS DE FRONTERAS",compact?_ui.Heading:_ui.Title,45);
        }
        private void DrawMainBackdrop()
        {
            _cinematic.Background(W,H,1,_p.Settings.ReducedMotion,true);
            FrontierUI.Fill(new Rect(0,0,W,90),new Color(.01f,.026f,.037f,.32f));
            Brand(50,29);
            Label(W-270,37,220,"CAMPAÑA  /  MUNDO",_ui.Caption);
        }
        private void DrawIntro()
        {
            _cinematic.Intro(_p,_ui,W,H);
        }
        private void DrawMainMenu()
        {
            DrawMainBackdrop();float x=76,y=H*.23f;
            Label(x,y,550,"CONQUISTA TERRITORIAL EN TIEMPO REAL",_ui.Caption,25,FrontierUI.Accent);
            Label(x,y+32,680,"TU NACIÓN.",_cinematic.MenuTitle,95);
            Label(x,y+108,680,"TU HISTORIA.",_cinematic.MenuTitle,100);
            if(_ui.Button(new Rect(x,y+218,168,35),"CAMPAÑA",_p.ChosenMode==MatchMode.Campaign))_p.ChosenMode=MatchMode.Campaign;
            if(_ui.Button(new Rect(x+182,y+218,168,35),"PARTIDA RÁPIDA",_p.ChosenMode==MatchMode.QuickMatch))_p.ChosenMode=MatchMode.QuickMatch;
            Label(x+3,y+261,550,_p.ChosenMode==MatchMode.QuickMatch?"Conquista directa · Solo contra IA · 80% para ganar":"Economía, diplomacia y estrategia de campaña",_ui.Small,24);
            float by=y+310;Label(x,by-30,350,"TU NOMBRE",_ui.Caption);
            _p.PlayerName=GUI.TextField(new Rect(x,by,350,43),_p.PlayerName,24,new GUIStyle(GUI.skin.textField){font=_ui.Body.font,fontSize=22,padding=new RectOffset(12,12,6,4)});
            string[] difficulty={"TRANQUILA","NORMAL","BRUTAL"};
            for(int d=0;d<3;d++)if(_ui.Button(new Rect(x+d*120,by+55,110,32),difficulty[d],(int)_p.ChosenDifficulty==d))_p.ChosenDifficulty=(Difficulty)d;
            if(_ui.Button(new Rect(x,by+100,350,48),_p.ChosenMode==MatchMode.QuickMatch?"EMPEZAR PARTIDA RÁPIDA":"EMPEZAR CAMPAÑA",true,!string.IsNullOrWhiteSpace(_p.PlayerName)))_p.BeginLocationSelection();
            if(_p.HasMatch&&_ui.Button(new Rect(x+368,by+100,210,48),"CONTINUAR"))_p.Resume();
            if(_ui.Button(new Rect(x,by+161,168,39),"CONFIGURACIÓN"))_p.ShowSettings();
            if(_ui.Button(new Rect(x+182,by+161,168,39),"SALIR"))Application.Quit();
            if(_ui.Button(new Rect(x,by+265,350,35),"CARGAR CAMPAÑA [F9]"))OpenWorldBoard(7);
            _p.TeamMode=_ui.Toggle(new Rect(x,by+217,350,32),"Partida por equipos",_p.TeamMode);
            float cardX=W-359;FrontierUI.Panel(new Rect(cardX,H-267,299,165));
            Label(cardX+22,H-249,255,"MUNDO ABIERTO",_ui.Caption);Label(cardX+22,H-219,255,"LA TIERRA",_ui.Title,41);
            Label(cardX+22,H-167,255,"Tu nombre, tu capital y tu frontera.\nEconomía, alianzas y combate.",_ui.Small,54);
            Label(76,H-44,720,"EXPANSIÓN   ·   DESARROLLO   ·   DIPLOMACIA   ·   DOMINIO MUNDIAL",_ui.Caption,25);
        }
        private void DrawCountries()
        {
            Rect r=new Rect(W*.5f-465,H*.5f-343,930,686);FrontierUI.Panel(r);
            Label(r.x+30,r.y+22,750,"ELIGE TU PAÍS",_ui.Title,43);
            Label(r.x+31,r.y+68,800,"Comienza desde un núcleo y construye tus propias fronteras.",_ui.Body);
            var field=new GUIStyle(GUI.skin.textField){font=_ui.Body.font,fontSize=17,padding=new RectOffset(12,12,6,4)};
            _search=GUI.TextField(new Rect(r.x+30,r.y+117,360,36),_search,field);
            if(string.IsNullOrEmpty(_search))Label(r.x+43,r.y+123,320,"Buscar país…",_ui.Small);
            var filtered=new List<int>();
            for(int f=0;f<_p.World.Countries.Length;f++)
                if(System.Globalization.CultureInfo.GetCultureInfo("es").CompareInfo.IndexOf(_p.World.Countries[f].name,_search,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0)filtered.Add(f);
            _scroll=GUI.BeginScrollView(new Rect(r.x+30,r.y+168,360,440),_scroll,new Rect(0,0,338,filtered.Count*39));
            for(int i=0;i<filtered.Count;i++)
            {
                int f=filtered[i];Rect row=new Rect(0,i*39,337,35);
                bool selected=f==_p.ChosenCountry;
                FrontierUI.Fill(row,selected?new Color(.09f,.28f,.28f):new Color(.05f,.10f,.13f));
                if(selected)FrontierUI.Fill(new Rect(0,row.y,3,35),FrontierUI.Accent);
                Label(14,row.y+5,310,_p.World.Countries[f].name,_ui.Body,27,selected?FrontierUI.Accent:FrontierUI.White);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none)){_p.ChosenCountry=f;_p.Audio.Play("click",.45f);}
            }
            GUI.EndScrollView();
            float cx=r.x+430;
            Label(cx,r.y+123,425,"TU PUNTO DE PARTIDA",_ui.Caption);
            Label(cx,r.y+162,425,_p.World.Countries[_p.ChosenCountry].name,_ui.Title,89);
            _ui.Dot(new Vector2(cx+15,r.y+274),22,FrontierUI.Accent);
            Label(cx+38,r.y+261,365,"TU COLOR: TURQUESA",_ui.Heading,35,FrontierUI.Accent);
            Label(cx,r.y+323,420,"400 tropas iniciales",_ui.Heading);
            Label(cx,r.y+367,420,"Doble clic: orientar la expansión libre.\nLa mancha crece más hacia el lado elegido.\nClic en enemigo vecino: atacar su frontera.",_ui.Body,105);
            Label(cx,r.y+488,410,"Los estados pequeños serán discretos. Los grandes irán ganando color y presencia.",_ui.Small,68);
            if(_ui.Button(new Rect(cx,r.y+563,440,53),_p.HasMatch?"INICIAR NUEVA PARTIDA":"COMENZAR",true))_p.StartMatch(_p.ChosenCountry);
            if(_ui.Button(new Rect(r.x+30,r.y+630,180,34),"VOLVER")){if(_p.HasMatch)_p.ShowPause();else _p.ShowMainMenu();}
        }
        private void DrawSettings()
        {
            Rect r=new Rect(W*.5f-420,H*.5f-335,840,670);FrontierUI.Panel(r);
            Label(r.x+30,r.y+20,750,"CONFIGURACIÓN",_ui.Title,43);
            string[] tabs={"AUDIO","IMAGEN","CONTROLES"};
            for(int tab=0;tab<3;tab++)if(_ui.Button(new Rect(r.x+30+tab*260,r.y+85,250,41),tabs[tab],_settingsTab==tab))_settingsTab=tab;
            float x=r.x+42,y=r.y+165,width=r.width-84;
            var s=_p.Settings;
            if(_settingsTab==0)
            {
                s.Master=_ui.Slider(new Rect(x,y,width,65),"Volumen general",s.Master);
                s.Music=_ui.Slider(new Rect(x,y+90,width,65),"Música y ambiente",s.Music);
                s.Effects=_ui.Slider(new Rect(x,y+180,width,65),"Alertas de ataque e invasión",s.Effects);
                _p.Audio.ApplyVolumes();
                if(_ui.Button(new Rect(x,y+285,245,40),"PROBAR ALERTA"))_p.Audio.Play("incoming_attack",.55f);
                Label(x,y+354,width,"La expansión y tus órdenes son silenciosas. Solo se avisan los ataques y las invasiones que recibes.",_ui.Small,55);
            }
            else if(_settingsTab==1)
            {
                Label(x,y,width,"CALIDAD DEL MAPA",_ui.Caption);
                string[] quality={"EFICIENTE","EQUILIBRADA","ALTA"};
                for(int q=0;q<3;q++)if(_ui.Button(new Rect(x+q*252,y+34,240,43),quality[q],s.Quality==q))s.Quality=q;
                bool fullscreen=_ui.Toggle(new Rect(x,y+107,width,34),"Pantalla completa sin bordes [F11]",s.Fullscreen);if(fullscreen!=s.Fullscreen)_p.SetFullscreen(fullscreen);
                s.ReducedMotion=_ui.Toggle(new Rect(x,y+164,width,34),"Movimiento reducido",s.ReducedMotion);
                s.Labels=_ui.Toggle(new Rect(x,y+221,width,34),"Nombres de los países",s.Labels);
                Label(x,y+286,width,"LÍMITE DE FOTOGRAMAS",_ui.Caption);
                int[] fps={30,60,120};
                for(int i=0;i<3;i++)if(_ui.Button(new Rect(x+i*252,y+319,240,40),fps[i]+" FPS",s.FrameLimit==fps[i]))s.FrameLimit=fps[i];
                Label(x,y+385,width,"F11 o Alt+Enter: pantalla completa. Movimiento reducido desactiva las animaciones del entorno.",_ui.Small);
            }
            else
            {
                s.ZoomSpeed=_ui.Slider(new Rect(x,y,width,68),"Velocidad del zoom",s.ZoomSpeed,.5f,2,"×");
                string[,] rows={{"EXPANDIR / ATACAR","Doble clic libre / clic enemigo vecino"},{"ACCIONES / INFO","Clic derecho · menú radial"},{"MOVER CÁMARA","WASD / flechas / botón central"},{"ENFOCAR TU PAÍS","C"},{"DETENER ORDEN","X"},{"PAUSAR / MENÚ","P o Espacio / Escape"},{"PANTALLA COMPLETA","F11 / Alt+Enter"}};
                for(int i=0;i<rows.GetLength(0);i++)
                {Label(x,y+102+i*45,255,rows[i,0],_ui.Caption);Label(x+275,y+99+i*45,width-275,rows[i,1],_ui.Body);}
            }
            if(_ui.Button(new Rect(r.x+30,r.yMax-67,780,43),"GUARDAR Y VOLVER",true))_p.CloseSettings();
        }
        private void DrawPause()
        {
            Rect r=new Rect(W*.5f-225,H*.5f-240,450,480);FrontierUI.Panel(r);
            Label(r.x+34,r.y+26,382,"PARTIDA EN PAUSA",_ui.Title,45);
            Label(r.x+35,r.y+82,380,G.Names[G.PlayerFaction],_ui.Body);
            if(_ui.Button(new Rect(r.x+34,r.y+140,382,49),"CONTINUAR",true))_p.Resume();
            if(_ui.Button(new Rect(r.x+34,r.y+204,382,45),"CONFIGURACIÓN"))_p.ShowSettings();
            if(_ui.Button(new Rect(r.x+34,r.y+263,382,45),"NUEVA PARTIDA"))_p.ShowCountries();
            if(_ui.Button(new Rect(r.x+34,r.y+322,382,45),"MENÚ PRINCIPAL"))_p.ShowMainMenu();
            if(_ui.Button(new Rect(r.x+34,r.y+396,382,40),"SALIR DEL JUEGO"))Application.Quit();
        }
        private void DrawGame()
        {
            if(G.IsQuickMatch){DrawQuickMatchGame();return;}
            if(G.CampaignEnabled){DrawProtectionRange();DrawWeaponPreview();}
            if(G.CampaignEnabled)DrawWorldSymbols();
            if(_p.Settings.Labels)DrawLabels();
            int f=G.PlayerFaction;
            FrontierUI.Panel(new Rect(16,14,W-32,66));
            bool wide=W>=1500;float px=wide?255:37,tx=wide?470:230,gx=wide?715:410,sx=wide?900:550;
            if(wide){Label(37,29,190,"ATLAS DE FRONTERAS",new GUIStyle(_ui.Heading){fontSize=15,wordWrap=false},35);FrontierUI.Fill(new Rect(233,31,1,30),new Color(.22f,.34f,.36f,.6f));}
            Label(px,24,195,G.Names[f],new GUIStyle(_ui.Heading){fontSize=G.Names[f].Length>18?17:23},32,FrontierUI.Accent);
            Label(px,55,195,(100f*G.TerritoryCounts[f]/_p.World.LandCount).ToString("0.00")+"% · líder "+(G.CampaignEnabled?G.WorldControl(Powers.Ranking[0])*100:100f*G.TerritoryCounts[Powers.Ranking[0]]/_p.World.LandCount).ToString("0.0")+"%",_ui.Small,22);
            Label(tx,24,wide?210:170,TroopNumber(G.AvailableTroops(f))+(G.CampaignEnabled?" / "+TroopNumber(G.Capacity[f]):" tropas"),new GUIStyle(_ui.Heading){fontSize=wide?21:18},32);
            if(G.CampaignEnabled)
            {
                Label(gx,24,wide?170:130,Amount(G.Gold[f])+" oro",new GUIStyle(_ui.Heading){fontSize=wide?23:18},32,FrontierUI.Gold);
                Label(gx,55,wide?170:130,"+"+Amount(G.IncomeRate(f))+" oro/s",_ui.Small,22);
                Label(sx,24,wide?170:120,Amount(G.Stars[f])+" estrellas",new GUIStyle(_ui.Body){fontSize=wide?17:14},32,FrontierUI.Gold);
                Label(sx,55,wide?170:120,G.ProtectionRemaining(f)>0?"Protección "+Mathf.CeilToInt(G.ProtectionRemaining(f))+" s":G.BetrayalUntil[f]>G.ElapsedTime?"Marca de traición":"Objetivo: 80%",new GUIStyle(_ui.Small){fontSize=wide?14:12},22);
            }
            Label(tx,55,wide?185:170,(G.ElapsedTime-G.LastCombat[f]<15?"En combate":"+"+G.ReinforcementRate(f).ToString("0.0")+" / s")+" · "+TimeSpan.FromSeconds(G.ElapsedTime).ToString(@"mm\:ss"),_ui.Small,22);
            if(_ui.Button(new Rect(W-520,28,125,36),"ATAQUES",_attacksOpen))_attacksOpen=!_attacksOpen;
            if(_ui.Button(new Rect(W-385,28,132,36),"POTENCIAS",_powersOpen))_powersOpen=!_powersOpen;
            if(_ui.Button(new Rect(W-243,28,104,36),G.Paused?"SEGUIR":"PAUSA"))_p.TogglePause();
            if(_ui.Button(new Rect(W-129,28,96,36),"MENÚ"))_p.ShowPause();

            DrawBottomDashboard();DrawQuickBuildPreview();
            if(G.CampaignEnabled&&_ui.Button(new Rect(24,90,210,35),"CAMPAÑA / "+G.CampaignDate.ToString("dd/MM/yyyy")))OpenWorldBoard(0);
            if(G.CampaignEnabled)DrawSpeedControl();
            if(_powersOpen)DrawPowers();if(_attacksOpen)DrawAttacks();if(G.CampaignEnabled)DrawCampaignPanel();DrawInspection();DrawTarget();
            if(_p.NoticeExpiry>Time.unscaledTime)
            {
                float width=Mathf.Min(760,W-80);Rect notice=new Rect((W-width)*.5f,H-224,width,44);
                FrontierUI.Panel(notice);Label(notice.x+18,notice.y+9,width-36,_p.Notice,_ui.Body,30);
            }
            if(G.Paused&&_p.ScreenState==FrontierScreen.Playing)
            {FrontierUI.Panel(new Rect(W*.5f-110,99,220,37));Label(W*.5f-91,106,190,"PARTIDA EN PAUSA",_ui.Caption,25,FrontierUI.Gold);}
        }
        private static string StateTitle(ArmyState state)
        {
            switch(state)
            {
                case ArmyState.Sailing:return "NAVEGANDO";
                case ArmyState.Anchored:return "TRANSPORTE EN ESPERA";
                case ArmyState.Marching:return "EXPANSIÓN EN CURSO";
                case ArmyState.Fighting:return "COMBATE DE FRONTERA";
                case ArmyState.NoTroops:return "ENVÍO AGOTADO";
                case ArmyState.Arrived:return "FRENTE COMPLETADO";
                case ArmyState.Blocked:return "RUTA INTERRUMPIDA";
                case ArmyState.Eliminated:return "ESTADO CONQUISTADO";
                default:return "SIN ORDEN ACTIVA";
            }
        }
        private void DrawPowers()
        {
            float x=W-324;FrontierUI.Panel(new Rect(x,100,300,302));
            Label(x+20,118,260,"BALANCE DE PODER",_ui.Heading,32);
            Label(x+20,157,260,"Los grandes estados ganan color.",_ui.Body,30);
            for(int rank=0;rank<5;rank++)
            {
                int f=Powers.Ranking[rank];float y=201+rank*36;
                _ui.Dot(new Vector2(x+24,y+9),8,Powers.DisplayColors[f]);
                Label(x+40,y-3,198,G.Names[f],_ui.Body,29,FrontierUI.White);
                var right=new GUIStyle(_ui.Caption){alignment=TextAnchor.MiddleRight};
                Label(x+237,y-2,44,(100f*G.TerritoryCounts[f]/_p.World.LandCount).ToString("0.0")+"%",right,25);
            }
        }
        private void DrawInspection()
        {
            if(_p.RadialOpen||SimpleAllianceOpen)return;
            int f=_p.InspectedFaction;if(f<0||f==G.PlayerFaction)return;
            float x=24;FrontierUI.Panel(new Rect(x,104,280,110));
            Label(x+18,117,244,G.Names[f],_ui.Heading,34,FrontierUI.White);
            Label(x+18,155,244,TroopNumber(G.AvailableTroops(f))+" tropas defensoras",_ui.Body,26);
            Label(x+18,185,244,Powers.Title(f),_ui.Caption,24,FrontierUI.Muted);
        }
        private void DrawLabels()
        {
            _labels.Clear();
            for(int pass=0;pass<2;pass++)for(int f=0;f<G.FactionCount;f++)
            {
                if((pass==0)!=(f==G.PlayerFaction)||G.TerritoryCounts[f]==0)continue;
                if(pass==1&&_camera.orthographicSize>130&&Powers.Tiers[f]==0)continue;
                int id=G.Owners[G.CapitalCells[f]]==f?G.CapitalCells[f]:G.LastAdvanceCells[f];
                Vector2 p=Project(id);
                if(p.x<24||p.x>W-24||p.y<102||p.y>H-222||(_powersOpen&&p.x>W-340&&p.y<415))continue;
                string name=(G.CampaignEnabled&&G.BetrayalUntil[f]>G.ElapsedTime?"[T] ":G.CampaignEnabled&&G.AreAllied(G.PlayerFaction,f)?"[A] ":"")+G.Names[f];float width=Mathf.Clamp(_ui.Small.CalcSize(new GUIContent(name)).x+22,60,172);
                Rect r=new Rect(p.x-width*.5f,p.y+8,width,43);bool overlap=false;
                foreach(Rect existing in _labels)if(existing.Overlaps(r)){overlap=true;break;}if(overlap)continue;
                _labels.Add(r);FrontierUI.Fill(r,new Color(.02f,.045f,.055f,.83f));
                _ui.Dot(p,f==G.PlayerFaction?7:4,Powers.DisplayColors[f]);
                var centered=new GUIStyle(_ui.Caption){alignment=TextAnchor.MiddleCenter};
                _ui.Label(new Rect(r.x,r.y,r.width,23),name,centered,f==G.PlayerFaction?FrontierUI.Accent:FrontierUI.White);
                _ui.Label(new Rect(r.x,r.y+22,r.width,20),TroopNumber(G.AvailableTroops(f)),centered,FrontierUI.White);
            }
            if(!_p.RadialOpen)DrawOrderPreview();
        }
        private Vector2 Project(int id)
        {var screen=_camera.WorldToScreenPoint(new Vector3(id%FrontierPrototype.MapWidth+.5f,_p.World.SurfaceHeight(id%FrontierPrototype.MapWidth,id/FrontierPrototype.MapWidth)+1,id/FrontierPrototype.MapWidth+.5f));return new Vector2(screen.x,Screen.height-screen.y)/Scale;}
        private void DrawTarget()
        {
            int cell=G.Targets[G.PlayerFaction];if(cell<0)return;Vector2 pos=Project(cell);
            if(pos.x<24||pos.x>W-24||pos.y<90||pos.y>H-150)return;
            Color c=G.States[G.PlayerFaction]==ArmyState.Fighting?new Color(1,.46f,.26f):FrontierUI.Accent;
            float r=11;
            FrontierUI.Fill(new Rect(pos.x-r,pos.y-r,8,2),c);FrontierUI.Fill(new Rect(pos.x-r,pos.y-r,2,8),c);
            FrontierUI.Fill(new Rect(pos.x+r-8,pos.y+r-2,8,2),c);FrontierUI.Fill(new Rect(pos.x+r-2,pos.y+r-8,2,8),c);
        }
        private void OnDestroy(){if(_hazard!=null)Destroy(_hazard);_ui?.Dispose();_cinematic?.Dispose();if(_wheelTexture!=null)Destroy(_wheelTexture);if(_wheelFocusTexture!=null)Destroy(_wheelFocusTexture);}
    }
}
