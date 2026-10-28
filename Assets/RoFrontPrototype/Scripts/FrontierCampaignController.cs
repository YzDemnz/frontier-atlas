using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierPrototype
    {
        public string PlayerName="";
        public Difficulty ChosenDifficulty=Difficulty.Standard;
        public bool TeamMode;
        public int SelectedSpawnCell {get;private set;}=-1;
        public CampaignPanel Panel {get;private set;}
        public int ContextCell {get;private set;}=-1;
        public BuildingKind SelectedBuilding=BuildingKind.City;
        public int SelectedWarship=-1,SelectedTransport=-1;
        private bool _profileSaved;
        private float _wheelPressed;private bool _wheelHeld;
        public void BeginLocationSelection()
        {
            SendFraction=ChosenMode==MatchMode.QuickMatch?.25f:.35f;CancelQuickBuilding();_neutralDoubleClick.Clear();
            if(string.IsNullOrWhiteSpace(PlayerName)){Notify("Escribe el nombre de tu nación.");ShowMainMenu();return;}
            PlayerName=PlayerName.Trim();if(PlayerName.Length>24)PlayerName=PlayerName.Substring(0,24);
            if(!_presentationAudit){PlayerPrefs.SetString("FA.PlayerName",PlayerName);PlayerPrefs.Save();}
            SaveCampaignProfile();
            if(_world.Width!=MapWidth||_world.Height!=MapHeight){_world=new WorldGrid(MapWidth,MapHeight,4206);_navigation.Initialize(_camera,MapWidth,MapHeight);}
            CreateSimulation(0,new MatchOptions{playerName=PlayerName,difficulty=ChosenDifficulty,teams=TeamMode,customPlayer=true,advancedSystems=true,mode=ChosenMode,tribeCount=ChosenMode==MatchMode.QuickMatch?TerritorySimulation.QuickTribes:CampaignRules.Load().tribes});
            _game.Stars[_game.PlayerFaction]=PlayerPrefs.GetInt("FA.Stars",0);_profileSaved=false;CloseCampaignPanel();
            _quickDeployUntil=Time.unscaledTime+30;HasMatch=false;SelectedSpawnCell=-1;ScreenState=FrontierScreen.SpawnSelection;_game.Paused=true;_navigation.ResetView();
        }
        public bool SelectFoundation(int cell)
        {
            if(!_game.CanFoundAt(cell)){Notify("Elige tierra libre: no puedes fundar una capital en el mar ni sobre otra nación.");return false;}
            SelectedSpawnCell=cell;return true;
        }
        public bool ConfirmFoundation()
        {
            if(!_presentationAudit&&_game.IsQuickMatch&&QuickDeploymentRemaining>0){Notify("Posición elegida. Puedes cambiarla hasta que termine el despliegue.");return false;}
            if(!_game.FoundPlayer(SelectedSpawnCell,PlayerName))return false;QuickBattleStartedAt=Time.unscaledTime;
            HasMatch=true;ScreenState=FrontierScreen.Playing;_game.Paused=false;Audio.ResetThreatCooldowns();Audio.EndCinematic();FocusPlayer();
            Notify(_game.IsQuickMatch?"Clic para expandir o atacar. Clic derecho: acciones. Estructuras [1–6], buque [7], bombas [8–0].":"Expande con doble clic. Ciudad [1]: +35% de reclutamiento. La red comercial se conecta sola.");return true;
        }
        public void OpenCampaignPanel(CampaignPanel panel,int cell)
        {WeaponAim.Cancel();QuickWeaponSelection=-1;CancelQuickBuilding();Panel=panel;ContextCell=cell;CloseRadial();SelectedWarship=SelectedTransport=-1;}
        public void CloseCampaignPanel(){QuickWeaponSelection=-1;WeaponAim.Cancel();Panel=CampaignPanel.None;SelectedWarship=SelectedTransport=-1;}
        private bool SelectCampaignTarget(int cell)
        {
            if(!_game.CampaignEnabled||Panel==CampaignPanel.None)return false;
            WeaponAim.Cancel();ContextCell=cell;if(_game.IsQuickMatch&&Panel==CampaignPanel.Nuclear&&QuickWeaponSelection>=0)PreviewStrategicWeapon((WeaponKind)QuickWeaponSelection);
            if(Panel==CampaignPanel.Navy&&SelectedWarship>=0){Notify(_game.MoveWarship(SelectedWarship,cell).message);SelectedWarship=-1;}
            else if(Panel==CampaignPanel.Navy&&SelectedTransport>=0){Notify(_game.RetargetTransport(SelectedTransport,cell).message);SelectedTransport=-1;}
            return true;
        }
        private void OnStrategicEvent(StrategicEvent change)
        {
            int player=_game.PlayerFaction;
            if(change.kind==StrategicEventKind.IncomingMissile&&change.other==player){WarnIncoming("¡Ataque estratégico entrante! "+change.message);Audio.IncomingAttack();return;}
            if(change.faction==player||change.other==player)
            {
                if(change.kind==StrategicEventKind.ShipSunk&&change.other==player){WarnIncoming(change.message);Audio.IncomingAttack();}
                else if(change.kind!=StrategicEventKind.Explosion)Notify(change.message);
            }
        }
        private bool _resultPresented;private float _resultsEnteredAt;
        public float ResultsReveal=>Settings.ReducedMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-_resultsEnteredAt)/.28f));
        public bool ResultsInteractive=>Time.unscaledTime-_resultsEnteredAt>.35f;
        public void RetryCampaign()
        {
            if(_game==null||!_game.CampaignEnabled||_game.Outcome==CampaignOutcome.Playing)return;
            PlayerName=_game.Names[_game.PlayerFaction];ChosenDifficulty=_game.Options.difficulty;TeamMode=_game.Options.teams;SendFraction=.35f;ChosenMode=_game.Options.mode;
            BeginLocationSelection();
        }
        private void UpdateCampaignFlow()
        {
            if(!_game.CampaignEnabled)return;if(_game.IsQuickMatch&&!HasMatch&&ScreenState==FrontierScreen.SpawnSelection&&SelectedSpawnCell>=0&&QuickDeploymentRemaining<=0)ConfirmFoundation();
            if(HasMatch&&_game.Outcome==CampaignOutcome.Playing&&_game.TerritoryCounts[_game.PlayerFaction]==0&&_game.CapitalCells[_game.PlayerFaction]>=0)_game.EvaluateVictory();
            if(HasMatch&&!_resultPresented&&_game.Outcome!=CampaignOutcome.Playing)
            {_resultPresented=true;_resultsEnteredAt=Time.unscaledTime;ScreenState=FrontierScreen.Results;CloseRadial();CancelQuickBuilding();CloseCampaignPanel();_interface.CloseSimpleAlliance();_interface.CloseWorldBoard();SaveCampaignProfile();}
            if(!_presentationAudit&&ScreenState==FrontierScreen.Playing&&Input.GetKeyDown(KeyCode.B))OpenCampaignPanel(CampaignPanel.Construction,HoverCell>=0&&_game.Owners[HoverCell]==_game.PlayerFaction?HoverCell:_game.CapitalCells[_game.PlayerFaction]);
        }
        private void SaveCampaignProfile()
        {
            if(_presentationAudit||_game==null||!_game.CampaignEnabled||!HasMatch||_profileSaved)return;
            PlayerPrefs.SetInt("FA.Stars",_game.Stars[_game.PlayerFaction]);PlayerPrefs.Save();_profileSaved=_game.Outcome!=CampaignOutcome.Playing;
        }
        private void OnApplicationQuit()=>SaveCampaignProfile();
        private void OnApplicationFocus(bool focused)
        {if(Settings!=null&&!Application.isEditor&&!_presentationAudit)Application.targetFrameRate=focused?Settings.FrameLimit:Mathf.Min(15,Settings.FrameLimit);}
    }
}
