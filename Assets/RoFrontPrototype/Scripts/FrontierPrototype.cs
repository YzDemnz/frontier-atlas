using System;
using UnityEngine;

namespace FrontierAtlas
{
    public enum FrontierScreen { Intro, MainMenu, CountryPicker, Playing, PauseMenu, Settings, SpawnSelection, Results }
    [DefaultExecutionOrder(-100)]
    public sealed partial class FrontierPrototype : MonoBehaviour
    {
        public const int MapWidth=3110,MapHeight=1555;
        public TerritorySimulation Simulation=>_game;
        public WorldGrid World=>_world;
        public MapVisuals Visuals=>_visuals;
        public FrontierSettings Settings {get;private set;}
        public FrontierAudio Audio {get;private set;}
        public FrontierScreen ScreenState {get;private set;}=FrontierScreen.Intro;
        public bool SelectingCountry=>ScreenState!=FrontierScreen.Playing;
        public bool HasMatch {get;private set;}
        public bool IsPresentationAudit=>_presentationAudit;
        public bool AuditManualSimulation;
        public int ChosenCountry {get;set;}
        public int InspectedFaction {get;private set;}=-1;
        public int HoverCell {get;private set;}=-1;
        public float SendFraction=.35f;
        public int RadialCell {get;private set;}=-1;
        public Vector2 RadialPosition {get;private set;}
        public bool RadialOpen=>RadialCell>=0;
        public float PreviewTroops=>_game.PreviewTroops(_game.PlayerFaction,SendFraction);
        public void CloseRadial()=>RadialCell=-1;
        public string Notice {get;private set;}="";
        public float NoticeExpiry {get;private set;}
        public const float IntroDuration=8.4f;
        public float IntroProgress=>Mathf.Clamp01(_introStart/IntroDuration);
        public float IntroVisibility=>1-Mathf.Max(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.87f,1,IntroProgress)),_skipStarted<0?0:Mathf.SmoothStep(0,1,(Time.unscaledTime-_skipStarted)/.55f));
        public float MenuReveal=>Mathf.SmoothStep(0,1,(Time.unscaledTime-_menuEntered)/.85f);
        private Camera _camera;
        private WorldGrid _world;
        private TerritorySimulation _game;
        private MapVisuals _visuals;
        private FrontierCamera _navigation;
        private FrontierInterface _interface;
        private FrontierScreen _settingsReturn=FrontierScreen.MainMenu;
        private float _introStart,_nextPromotionNotice,_nextThreatNotice;
        private bool _presentationAudit;
        private float _skipStarted=-1,_menuEntered=-100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if(FindAnyObjectByType<FrontierPrototype>()==null)new GameObject("FRONTIER ATLAS - Earth").AddComponent<FrontierPrototype>();
        }
        private void Awake()
        {
            _presentationAudit=Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-quick-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-factory-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-strategy-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-earth-nuclear-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-defeat-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-industrial-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-network-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-visual-refinement")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-expanded-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-presentation-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-campaign-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-session-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-direction-audit")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-tactical-audit")>=0;
            Application.runInBackground=true;
            Settings=new FrontierSettings();Settings.Load();Settings.Apply(true);
            PlayerName=PlayerPrefs.GetString("FA.PlayerName","");
            _camera=Camera.main;
            if(_camera==null){var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));go.tag="MainCamera";_camera=go.GetComponent<Camera>();}
            _camera.orthographic=true;_camera.clearFlags=CameraClearFlags.SolidColor;
            _camera.backgroundColor=new Color(.012f,.035f,.055f);_camera.nearClipPlane=.1f;_camera.farClipPlane=3000;
            _navigation=_camera.GetComponent<FrontierCamera>();
            if(_navigation==null)_navigation=_camera.gameObject.AddComponent<FrontierCamera>();
            _navigation.Initialize(_camera,MapWidth,MapHeight);
            Audio=gameObject.AddComponent<FrontierAudio>();Audio.Initialize(Settings);
            _world=new WorldGrid(MapWidth,MapHeight,4206);
            ChosenCountry=Array.FindIndex(_world.Countries,c=>c.code=="ZWE");if(ChosenCountry<0)ChosenCountry=0;
            CreateSimulation(ChosenCountry);_game.Paused=true;
            _interface=gameObject.AddComponent<FrontierInterface>();_interface.Initialize(this,_camera);
            _introStart=0;
        }
        private void CreateSimulation(int country,MatchOptions options=null,TerritorySimulation restored=null)
        {
            WeaponAim.Cancel();_resultPresented=false;Audio?.ResetThreatCooldowns();_nextThreatNotice=0;_nextPromotionNotice=0;_interface?.CloseSimpleAlliance();_interface?.CloseWarActions();
            if(_visuals!=null){_visuals.gameObject.SetActive(false);Destroy(_visuals.gameObject);}
            if(restored!=null&&restored.World!=_world){_world=restored.World;_navigation.Initialize(_camera,_world.Width,_world.Height);}
            _game=restored??new TerritorySimulation(_world,4206,country,options);_navigation.Tilt=_game.IsQuickMatch?90:78;
            var map=new GameObject("Earth - terrain and live frontiers");map.transform.SetParent(transform,false);
            _visuals=map.AddComponent<MapVisuals>();_visuals.Initialize(_game,Settings,Audio);
            _game.StrategicEventRaised+=OnStrategicEvent;_game.NuclearDetonated+=OnNuclearDetonation;IncomingNuclearCount=0;
            _game.BattleOccurred+=(attacker,defender,loss)=>{if(defender==_game.PlayerFaction)WarnIncoming(_game.Names[attacker]+" te está atacando. Abre ATAQUES para localizar el frente.");};
            _game.TerritoryCaptured+=(attacker,cell,previous)=>{if(previous==_game.PlayerFaction)WarnIncoming(attacker>=0?_game.Names[attacker]+" está ocupando tu territorio.":"Un impacto ha afectado a tu territorio.");};
            _game.FleetLaunched+=attacker=>{if(_game.IsNavalThreat(attacker,_game.PlayerFaction))WarnIncoming("Invasión enemiga de "+_game.Names[attacker]+". Abre ATAQUES para localizarla.");};
            _visuals.Powers.Promoted+=f=>{if(Time.unscaledTime>_nextPromotionNotice){_nextPromotionNotice=Time.unscaledTime+10;Notify(_game.Names[f]+" se consolida como "+_visuals.Powers.Title(f).ToLowerInvariant()+".");}};
            InspectedFaction=-1;HoverCell=-1;CloseRadial();_navigation.ResetView();
        }
        public void StartMatch(int country)
        {
            ChosenCountry=Mathf.Clamp(country,0,_world.Countries.Length-1);
            CreateSimulation(ChosenCountry);HasMatch=true;ScreenState=FrontierScreen.Playing;_game.Paused=false;
            Audio.ResetThreatCooldowns();
            Audio.EndCinematic();
            FocusPlayer();Audio.Play("order",.5f);
            Notify("Doble clic en tierra libre: orientar la expansión. Clic derecho: acciones.");
        }
        public void FinishIntro()
        {if(ScreenState==FrontierScreen.Intro&&_skipStarted<0){_skipStarted=Time.unscaledTime;Audio.EndCinematic();}}
        private void CompleteIntro()
        {ScreenState=FrontierScreen.MainMenu;_menuEntered=Time.unscaledTime;Audio.EndCinematic();}
        public void ShowMainMenu(){SaveCampaignProfile();if(_game.CampaignEnabled&&_game.Outcome!=CampaignOutcome.Playing)HasMatch=false;CloseCampaignPanel();CloseRadial();ScreenState=FrontierScreen.MainMenu;_game.Paused=true;}
        public void ShowCountries()=>BeginLocationSelection();
        public void ShowSettings()
        {_settingsReturn=ScreenState;ScreenState=FrontierScreen.Settings;_game.Paused=true;}
        public void CloseSettings()
        {
            if(!_presentationAudit)Settings.Save();Settings.Apply(true);Audio.ApplyVolumes();_visuals.ApplySettings();
            ScreenState=_settingsReturn;_game.Paused=ScreenState!=FrontierScreen.Playing||_game.Outcome!=CampaignOutcome.Playing;
        }
        public void Resume()
        {
            if(!HasMatch){ShowMainMenu();return;}
            if(_game.CampaignEnabled&&_game.Outcome!=CampaignOutcome.Playing){ScreenState=FrontierScreen.Results;return;}
            ScreenState=FrontierScreen.Playing;_game.Paused=false;
        }
        public void SetFullscreen(bool fullscreen)
        {Settings.Fullscreen=fullscreen;if(!_presentationAudit)Settings.Save();Settings.Apply(true);}
        public void ToggleFullscreen()=>SetFullscreen(!Settings.Fullscreen);
        public void TogglePause(){if(ScreenState==FrontierScreen.Playing)_game.Paused=!_game.Paused;}
        public void ShowPause(){CloseRadial();ScreenState=FrontierScreen.PauseMenu;_game.Paused=true;}
        public void FocusPlayer()
        {
            int cell=_game.CapitalCells[_game.PlayerFaction];
            _navigation.Focus(new Vector2(cell%MapWidth+.5f,cell/MapWidth+.5f),48*_world.ResolutionScale);
        }
        public void ViewContinent()=>_navigation.ResetView();
        public void StopOrder(){_game.CancelOrder(_game.PlayerFaction);Notify("Retirada completada. Las tropas supervivientes vuelven a estar disponibles.");}
        public void Notify(string text){Notice=text;NoticeExpiry=Time.unscaledTime+6;}
        private void WarnIncoming(string text)
        {if(Time.unscaledTime<_nextThreatNotice)return;_nextThreatNotice=Time.unscaledTime+8;_nextPromotionNotice=Time.unscaledTime+10;Notify(text);}
        public OrderResult SendOrder(int cell)
        {
            var result=_game.IssueOrder(_game.PlayerFaction,new Vector2Int(cell%MapWidth,cell/MapWidth),SendFraction);
            return ReportOrder(result,cell,_world.Land[cell]?0:1);
        }
        private readonly NeutralDoubleClick _neutralDoubleClick=new NeutralDoubleClick();
        public OrderResult ExpandTowards(int cell)
        {var result=ReportOrder(_game.BeginDirectionalExpansion(_game.PlayerFaction,cell,SendFraction),cell);if(result==OrderResult.Accepted){_visuals.EmitPulse(cell,new Color(.2f,.94f,.87f),6,1.2f);Notify("Expansión orgánica: mayor avance hacia el lado marcado, con crecimiento por el resto de la frontera.");}return result;}
        public OrderResult Expand()=>ReportOrder(_game.BeginExpansion(_game.PlayerFaction,SendFraction),-1);
        public OrderResult SendAttack(int cell)=>_game.Owners[cell]==TerritorySimulation.Neutral?ExpandTowards(cell):ReportOrder(_game.AttackBorder(_game.PlayerFaction,cell,SendFraction),cell);
        public OrderResult SendBoat(int cell)=>ReportOrder(_game.InvadeBySea(_game.PlayerFaction,cell,SendFraction),cell,1);
        private OrderResult ReportOrder(OrderResult result,int cell,int naval=0)
        {
            Audio.Play(result==OrderResult.Accepted?"order":"denied",.65f);
            if(result==OrderResult.Accepted&&cell>=0&&(_game.FleetActive[_game.PlayerFaction]||_game.AttackFactions[_game.PlayerFaction]>=0))_visuals.EmitPulse(cell,new Color(.2f,.94f,.87f),6,1.2f);
            switch(result)
            {
                case OrderResult.Accepted:Notify(naval==1?"Transporte en marcha hacia la costa objetivo.":cell<0||_game.Owners[cell]<0?"Expansión general: ocupando zonas libres en todo el perímetro.":"Ataque terrestre en curso contra la nación vecina.");break;
                case OrderResult.NoTroops:Notify("No hay suficientes tropas en reserva. Ajusta ENVÍO o retira la operación activa.");break;
                case OrderResult.NoFreeLand:Notify("No quedan zonas libres en contacto con tu frontera.");break;
                case OrderResult.Protected:Notify("Esa nación conserva su protección inicial.");break;
                case OrderResult.Allied:Notify("No puedes atacar a un aliado. Gestiona el pacto desde Diplomacia.");break;
                case OrderResult.OperationLimit:Notify("Se alcanzó el límite de operaciones simultáneas. Retira una para abrir otro frente.");break;
                case OrderResult.NotAdjacent:Notify("El ataque necesita una frontera vecina. Para atravesar el mar, usa Invadir.");break;
                case OrderResult.NoLandRoute:Notify("No se ha encontrado una ruta hasta ese objetivo.");break;
                case OrderResult.NoCoastalAccess:Notify("Alcanza una costa para embarcar tropas y navegar.");break;
                case OrderResult.Water:Notify("El mar se cruza con Invadir desde el menú de clic derecho.");break;
                case OrderResult.OwnTerritory:Notify("Ese territorio ya te pertenece.");break;
                case OrderResult.Paused:Notify("Reanuda la partida para enviar tropas.");break;
                default:Notify("Tu país ha sido conquistado. Puedes iniciar otra partida.");break;
            }
            return result;
        }
        public void FocusCell(int cell)
        {if(cell>=0)_navigation.Focus(new Vector2(cell%MapWidth+.5f,cell/MapWidth+.5f),48*_world.ResolutionScale);}
        public void FocusOperation(int faction)
        {FocusCell(_game.FleetActive[faction]?_game.FleetCells[faction]:_game.LastAdvanceCells[faction]);}
        public void Inspect(int cell)
        {InspectedFaction=cell>=0?_game.Owners[cell]:-1;if(InspectedFaction==_game.PlayerFaction)Notify(_game.Names[InspectedFaction]+": "+Mathf.FloorToInt(_game.AvailableTroops(InspectedFaction))+" tropas en reserva.");}
        public void HandleMapClick(int cell,int button,Vector2 screenPosition)
        {
            if(ScreenState==FrontierScreen.SpawnSelection){if(button==0)SelectFoundation(cell);return;}
            if(ScreenState!=FrontierScreen.Playing||cell<0||cell>=_game.Owners.Length)return;
            if(button==1)_neutralDoubleClick.Clear();
            if(button==1&&(QuickBuildActive||QuickWarshipPlacement)){CancelQuickBuilding();return;}
            if(button==1){_interface.CloseSimpleAlliance();CloseCampaignPanel();Inspect(cell);RadialCell=cell;RadialPosition=screenPosition;return;}
            if(RadialOpen){_neutralDoubleClick.Clear();CloseRadial();return;}
            if(button==0&&QuickWarshipPlacement){PlaceQuickWarship(cell);return;}
            if(button==0&&QuickBuildActive){_neutralDoubleClick.Clear();PlaceQuickBuilding(cell,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));return;}
            if(button==0&&SelectCampaignTarget(cell)){_neutralDoubleClick.Clear();return;}
            Inspect(cell);
            if(_game.IsQuickMatch&&_game.Owners[cell]!=_game.PlayerFaction&&_world.Land[cell]){if(_game.IsBorderTarget(_game.PlayerFaction,cell))SendAttack(cell);else if(_game.HasCoastalAccess(_game.PlayerFaction)&&_game.AdjacentWater(cell)>=0)SendBoat(cell);else ReportOrder(OrderResult.NotAdjacent,cell);return;}
            if(_game.Owners[cell]==_game.PlayerFaction){_neutralDoubleClick.Clear();return;}
            if(!_world.Land[cell]){_neutralDoubleClick.Clear();ReportOrder(OrderResult.Water,cell);return;}
            if(_game.Owners[cell]==TerritorySimulation.Neutral)
            {
                if(_neutralDoubleClick.Click(screenPosition,Time.unscaledTime,20*Mathf.Clamp(Screen.height/900f,.7f,2)))ExpandTowards(cell);
                else Notify("Doble clic en tierra libre para orientar la expansión hacia ese lado.");
                return;
            }
            _neutralDoubleClick.Clear();SendAttack(cell);
        }
        private void Update()
        {
            if(!_presentationAudit&&(Input.GetKeyDown(KeyCode.F11)||((Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt))&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))))ToggleFullscreen();
            bool playing=ScreenState==FrontierScreen.Playing;bool choosing=ScreenState==FrontierScreen.SpawnSelection;
            _navigation.InputEnabled=(playing||choosing)&&!_presentationAudit&&!RadialOpen&&!_interface.BlocksPointer(Input.mousePosition);_navigation.ZoomSpeed=Settings.ZoomSpeed;
            if(ScreenState==FrontierScreen.Intro)
            {
                if(_introStart<=0)Audio.StartCinematic();
                _introStart+=Mathf.Min(Time.unscaledDeltaTime,.05f);
                if(!_presentationAudit&&(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape)))FinishIntro();
                if(IntroProgress>=1||(_skipStarted>=0&&Time.unscaledTime-_skipStarted>=.55f))CompleteIntro();
            }
            else if(!_presentationAudit&&Input.GetKeyDown(KeyCode.Escape))
            {
                if(QuickBuildActive||QuickWarshipPlacement)CancelQuickBuilding();
                else if(_interface.WorldBoardOpen)_interface.CloseWorldBoard();
                else if(_interface.WarActionsOpen)_interface.CloseWarActions();
                else if(_interface.SimpleAllianceOpen)_interface.CloseSimpleAlliance();
                else if(RadialOpen)CloseRadial();
                else if(Panel!=CampaignPanel.None)CloseCampaignPanel();
                else if(ScreenState==FrontierScreen.Settings)CloseSettings();
                else if(playing)ShowPause();
                else if(ScreenState==FrontierScreen.PauseMenu)Resume();
                else ShowMainMenu();
            }
            if(ScreenState==FrontierScreen.Intro||ScreenState==FrontierScreen.MainMenu)
                _navigation.Backdrop(Time.unscaledTime,Settings.ReducedMotion);
            if(playing&&!_presentationAudit&&!_interface.WorldBoardOpen)
            {
                UpdateQuickBuildingKeys();
                if(Input.GetKeyDown(KeyCode.P)||Input.GetKeyDown(KeyCode.Space))TogglePause();
                if(Input.GetKeyDown(KeyCode.C))FocusPlayer();
                if(Input.GetKeyDown(KeyCode.X))StopOrder();
            }
            HoverCell=-1;
            Vector2 mouse=Input.mousePosition;
            if((playing||choosing)&&!_presentationAudit&&!_interface.BlocksPointer(mouse))
            {
                Ray ray=_camera.ScreenPointToRay(mouse);
                if(_visuals.SurfaceCollider.Raycast(ray,out RaycastHit surface,10000))
                {
                    Vector3 hit=surface.point;int x=Mathf.FloorToInt(hit.x),y=Mathf.FloorToInt(hit.z);
                    if(_world.InBounds(x,y))HoverCell=_world.At(x,y);
                }
                if(Input.GetMouseButtonDown(1)&&HoverCell>=0){HandleMapClick(HoverCell,1,mouse);_wheelPressed=Time.unscaledTime;_wheelHeld=true;}
                else if(Input.GetMouseButtonDown(0)&&HoverCell>=0){if(!_interface.CaptureBoardCell(HoverCell))HandleMapClick(HoverCell,0,mouse);}
            }
            if(playing&&!_presentationAudit&&RadialOpen)
            {
                if(Input.GetMouseButtonUp(1)&&_wheelHeld){_wheelHeld=false;if(Time.unscaledTime-_wheelPressed>.18f)_interface.ConfirmWheel(mouse);}
                else if(Input.GetMouseButtonDown(0))_interface.ConfirmWheel(mouse);
            }
            if(!AuditManualSimulation)_game.Step(Time.unscaledDeltaTime);UpdateCampaignFlow();UpdateCampaignFiles();
            if(!_presentationAudit&&playing&&Input.GetKeyDown(KeyCode.Tab))_interface.OpenWorldBoard(0);
        }
    }
}
