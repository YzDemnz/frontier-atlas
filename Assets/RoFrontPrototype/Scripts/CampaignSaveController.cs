using System;
using System.Threading.Tasks;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierPrototype
    {
        public string CampaignFileNotice="";public bool SavingCampaign=>_saveTask!=null;
        private Task<string> _saveTask;private float _lastAutoSave;
        public void SaveFullCampaign()
        {
            if(_saveTask!=null)return;if(!HasMatch||!_game.CampaignEnabled){Notify("Funda una nación antes de guardar.");return;}
            try{_saveTask=CampaignFiles.SaveAsync(_game);CampaignFileNotice="Comprimiendo guardado…";_lastAutoSave=Time.unscaledTime;}catch(Exception e){CampaignFileNotice="No se pudo guardar: "+e.Message;Notify(CampaignFileNotice);}
        }
        public void LoadFullCampaign(string path)
        {
            try
            {
                byte[] bytes=CampaignFiles.Read(path);var savedWorld=CampaignFiles.CompatibleWorld(_world,bytes);var restored=TerritorySimulation.RestoreCampaign(savedWorld,bytes);
                CreateSimulation(0,null,restored);HasMatch=true;PlayerName=_game.Names[_game.PlayerFaction];ChosenMode=_game.Options.mode;_profileSaved=false;ScreenState=FrontierScreen.Playing;_game.Paused=true;CloseCampaignPanel();CancelQuickBuilding();Audio.EndCinematic();FocusPlayer();_lastAutoSave=Time.unscaledTime;
                CampaignFileNotice="Partida restaurada. Pulsa P o ESPACIO para continuar.";Notify(CampaignFileNotice);
            }catch(Exception e){CampaignFileNotice="No se pudo cargar: "+e.Message;Notify(CampaignFileNotice);}
        }
        private void UpdateCampaignFiles()
        {
            if(_saveTask!=null&&_saveTask.IsCompleted){CampaignFileNotice=_saveTask.IsFaulted?"Error al guardar: "+_saveTask.Exception.GetBaseException().Message:"Guardado completo: "+System.IO.Path.GetFileName(_saveTask.Result);_saveTask=null;Notify(CampaignFileNotice);}
            if(_presentationAudit)return;
            if(HasMatch&&ScreenState==FrontierScreen.Playing&&Input.GetKeyDown(KeyCode.F5))SaveFullCampaign();
            if(Input.GetKeyDown(KeyCode.F9))_interface.OpenWorldBoard(7);
            if(HasMatch&&ScreenState==FrontierScreen.Playing&&!_game.Paused&&Time.unscaledTime-_lastAutoSave>=300)SaveFullCampaign();
        }
    }
}
