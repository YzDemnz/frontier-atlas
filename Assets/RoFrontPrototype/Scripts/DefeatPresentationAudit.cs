using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class DefeatPresentationAudit:MonoBehaviour
    {
        private string folder;private int errors;private FrontierPrototype prototype;private bool originalFullscreen;private readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-frontier-defeat-audit");if(index<0||index+1>=args.Length)return;var a=new GameObject("Prueba de derrota, pantalla y alertas").AddComponent<DefeatPresentationAudit>();a.folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(a.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("DEFEAT_AUDIT PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private void RestoreDisplay(){if(prototype!=null){prototype.Settings.Fullscreen=originalFullscreen;prototype.Settings.Apply(true);}}
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception failure=null;try{next=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){RestoreDisplay();File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return run.Current;}}
        private IEnumerator Run()
        {
            yield return null;prototype=FindAnyObjectByType<FrontierPrototype>();var p=prototype;p.AuditManualSimulation=true;originalFullscreen=p.Settings.Fullscreen;p.Settings.ReducedMotion=false;p.PlayerName="Segunda oportunidad";p.ChosenDifficulty=Difficulty.Brutal;p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;
            int nation=Array.FindIndex(p.World.Countries,c=>c.code=="ZWE"),center=p.World.StartCells[nation];int start=Enumerable.Range(12,70).Select(n=>center+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation(),"Partida nueva fundada antes de la derrota");int f=g.PlayerFaction;FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(start%p.World.Width,start/p.World.Width),35);yield return new WaitForSecondsRealtime(.4f);Capture("01_partida_activa");yield return null;
            foreach(int cell in g.OwnedCells(f).ToArray())if(cell!=start){g.Owners[cell]=0;g.TerritoryCounts[f]--;g.TerritoryCounts[0]++;}
            for(int d=0;d<4;d++){int c=p.World.Neighbor(start,d);if(c<0||!p.World.Land[c]||g.Owners[c]==0)continue;int old=g.Owners[c];if(old>=0)g.TerritoryCounts[old]--;g.Owners[c]=0;g.TerritoryCounts[0]++;}g.RebuildCampaignIndexes();Array.Clear(g.ProtectionUntil,0,g.FactionCount);g.Troops[f]=0;g.Troops[0]=20000;
            Check(g.AttackBorder(0,start,.5f)==OrderResult.Accepted,"Enemigo ataca la última celda por la simulación real");g.Step(.1f);yield return new WaitForSecondsRealtime(.7f);
            Check(g.Outcome==CampaignOutcome.Defeat&&g.TerritoryCounts[f]==0&&g.Paused&&p.ScreenState==FrontierScreen.Results,"Pérdida total abre DERROTA y congela la partida");Check(p.Panel==CampaignPanel.None&&!p.QuickBuildActive&&!p.RadialOpen,"Menús de combate cerrados al perder");Capture("02_derrota");yield return null;
            p.ShowSettings();yield return null;Check(p.ScreenState==FrontierScreen.Settings,"La configuración no queda bloqueada por el resultado");typeof(FrontierInterface).GetField("_settingsTab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(FindAnyObjectByType<FrontierInterface>(),1);
            p.SetFullscreen(false);yield return new WaitForSecondsRealtime(.5f);if(!Application.isEditor)Check(!Screen.fullScreen&&Screen.fullScreenMode==FullScreenMode.Windowed,"Cambio real a ventana");Capture("03_configuracion_ventana");yield return null;
            p.SetFullscreen(true);yield return new WaitForSecondsRealtime(.7f);if(!Application.isEditor)Check(Screen.fullScreen&&Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"Pantalla completa sin bordes real");Capture("04_pantalla_completa");yield return null;p.CloseSettings();yield return null;Check(p.ScreenState==FrontierScreen.Results&&g.Paused,"Volver de configuración conserva la derrota");
            p.RetryCampaign();yield return new WaitForSecondsRealtime(.3f);var fresh=p.Simulation;
            Check(fresh!=g&&!p.HasMatch&&p.ScreenState==FrontierScreen.SpawnSelection&&fresh.Outcome==CampaignOutcome.Playing,"Reintentar crea un mundo limpio para elegir capital");Check(p.PlayerName=="Segunda oportunidad"&&p.ChosenDifficulty==Difficulty.Brutal,"Reintento conserva nombre y dificultad");Check(fresh.Buildings.Count==0&&fresh.Operations.Count==0&&fresh.Trains.Count==0&&fresh.TerritoryCounts[f]==0&&fresh.Troops[f]==10000&&fresh.Gold[f]==250000,"Reintento sin territorio, ejércitos ni economía heredados");Capture("05_reintentar_capital");yield return null;
            Check(p.SelectFoundation(start)&&p.ConfirmFoundation()&&fresh.TerritoryCounts[f]>0&&!fresh.Paused,"Segunda partida vuelve a ser jugable");Capture("06_segunda_partida");yield return null;
            // Finish a second round to exercise the actual return-to-menu path.
            foreach(int cell in fresh.OwnedCells(f).ToArray()){fresh.Owners[cell]=0;fresh.TerritoryCounts[f]--;fresh.TerritoryCounts[0]++;}fresh.RebuildCampaignIndexes();fresh.EvaluateVictory();yield return new WaitForSecondsRealtime(.4f);p.ShowMainMenu();yield return new WaitForSecondsRealtime(.4f);Check(p.ScreenState==FrontierScreen.MainMenu&&!p.HasMatch,"Menú principal permanece abierto después de perder");Capture("07_menu_sin_bucle_de_derrota");yield return null;
            var audio=p.Audio;int before=audio.PlayedEffects;audio.Expansion();audio.Play("expand");audio.Play("order");audio.Play("click");audio.Play("promotion");Check(audio.PlayedEffects==before,"Expansión, órdenes y promociones siguen silenciosas");
            for(int i=0;i<3;i++){audio.Play("incoming_attack",.72f);yield return new WaitForSecondsRealtime(.9f);audio.Play("incoming_invasion",.76f);yield return new WaitForSecondsRealtime(1.25f);}
            Check(audio.AttackVariantsUsed==3&&audio.InvasionVariantsUsed==3,"Las seis alertas importadas se reproducen con variación");audio.ResetThreatCooldowns();int attacks=audio.IncomingAttackAlerts,invasions=audio.IncomingInvasionAlerts;for(int i=0;i<20;i++){audio.IncomingAttack();audio.IncomingInvasion();}Check(audio.IncomingAttackAlerts==attacks+1&&audio.IncomingInvasionAlerts==invasions+1,"Los eventos repetidos no saturan las alertas");
            float master=p.Settings.Master;p.Settings.Master=0;audio.ApplyVolumes();Check(p.GetComponents<AudioSource>().All(s=>s.volume==0),"Volumen general silencia todas las fuentes");p.Settings.Master=master;audio.ApplyVolumes();
            RestoreDisplay();yield return new WaitForSecondsRealtime(.4f);var times=new List<float>();float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<15){times.Add(Time.unscaledDeltaTime*1000);yield return null;}times.Sort();Check(errors==0,"Recorrido de interfaz y audio sin errores");
            File.WriteAllText(Path.Combine(folder,"defeat-presentation.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=errors,checks=checks.ToArray(),attackVariants=audio.AttackVariantsUsed,invasionVariants=audio.InvasionVariantsUsed,width=Screen.width,height=Screen.height,fps=times.Count*1000/times.Sum(),p95Ms=times[(int)(times.Count*.95f)]},true));Debug.Log("DEFEAT_PRESENTATION_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public int errors,attackVariants,invasionVariants,width,height;public float fps,p95Ms;public string[] checks;}
    }
}
