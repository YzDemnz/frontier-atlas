using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class VisualRefinementAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-visual-refinement");if(i<0||i+1>=args.Length)return;var audit=new GameObject("Revisión de mar, escala y acciones").AddComponent<VisualRefinementAudit>();audit.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(audit.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("VISUAL_REFINEMENT PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private IEnumerator Start()
        {var iterator=Run();while(true){bool next=false;Exception failure=null;try{next=iterator.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return iterator.Current;}}
        private IEnumerator Run()
        {
            yield return null;var p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;
            yield return new WaitForSecondsRealtime(5);Capture("01_intro_lato");yield return null;p.ShowMainMenu();yield return new WaitForSecondsRealtime(.5f);Capture("02_menu_lato");yield return null;
            p.PlayerName="Costa Serena";p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;int nation=Array.FindIndex(p.World.Countries,c=>c.code=="PRT");if(nation<0)nation=0;int capital=p.World.StartCells[nation];int start=Enumerable.Range(10,80).Select(n=>capital+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation(),"Fundación de la partida visual");int f=g.PlayerFaction,x=start%p.World.Width,y=start/p.World.Width;
            for(int dy=-32;dy<=32;dy++)for(int dx=-42;dx<=42;dx++){int cx=x+dx,cy=y+dy;if(!p.World.InBounds(cx,cy))continue;int c=p.World.At(cx,cy);if(!p.World.Land[c])continue;int old=g.Owners[c];if(old==f)continue;if(old>=0)g.TerritoryCounts[old]--;g.Owners[c]=f;g.TerritoryCounts[f]++;}g.RebuildCampaignIndexes();g.Gold[f]=5000000;
            int city=Enumerable.Range(-18,8).Select(dx=>p.World.At(x+dx,y)).First(c=>g.Owners[c]==f);int factory=Enumerable.Range(10,15).Select(dx=>p.World.At(x+dx,y)).First(c=>g.Owners[c]==f);
            Check(g.Construct(f,city,BuildingKind.City).success&&g.Construct(f,factory,BuildingKind.Factory).success,"Ciudad y fábrica reales en la escena");for(int i=0;i<60;i++){g.Step(.1f);yield return null;}
            var camera=FindAnyObjectByType<FrontierCamera>();var ui=FindAnyObjectByType<FrontierInterface>();camera.Focus(new Vector2(x,y),32);yield return new WaitForSecondsRealtime(.4f);Capture("03_modelos_pequenos_logos_cerca");yield return null;
            Check(StructureModelVisuals.ModelFootprint==2.4f,"Escala de las estructuras reducida un 40%");camera.Focus(new Vector2(x,y),155);yield return new WaitForSecondsRealtime(.35f);Capture("04_modelos_sin_logos_lejos");yield return null;
            int coast=p.World.CoastCells.OrderBy(c=>Mathf.Abs(c%p.World.Width-x)+Mathf.Abs(c/p.World.Width-y)).First();camera.Focus(new Vector2(coast%p.World.Width-30,coast/p.World.Width),80);yield return new WaitForSecondsRealtime(.4f);Capture("05_mar_mate");yield return new WaitForSecondsRealtime(1.2f);Capture("06_mar_en_movimiento");yield return null;
            int other=Enumerable.Range(0,g.FactionCount).First(n=>n!=f&&g.TerritoryCounts[n]>0&&!g.IsTribe(n));int otherCell=g.CapitalCells[other];camera.Focus(new Vector2(otherCell%p.World.Width,otherCell/p.World.Width),75);yield return new WaitForSecondsRealtime(.2f);
            var screen=new Vector2(Screen.width*.55f,Screen.height*.5f);p.HandleMapClick(otherCell,1,screen);yield return null;Capture("07_rueda_apertura");yield return new WaitForSecondsRealtime(.3f);ui.AuditWheelPointer=ui.WheelPointForAudit(3);yield return new WaitForSecondsRealtime(.3f);Capture("08_rueda_alianza");yield return null;ui.ConfirmWheel(ui.WheelPointForAudit(3));yield return new WaitForSecondsRealtime(.3f);
            Check(ui.SimpleAllianceOpen&&!ui.WorldBoardOpen,"Alianza abre una tarjeta bilateral sin la interfaz completa");Capture("09_alianza_sencilla");yield return null;ui.CloseSimpleAlliance();
            p.HandleMapClick(factory,1,screen);ui.AuditWheelPointer=ui.WheelPointForAudit(1);yield return new WaitForSecondsRealtime(.4f);Check(ui.WheelSelection(ui.WheelPointForAudit(1))==1,"Rueda compacta conserva selección de construcción");p.CloseRadial();yield return new WaitForSecondsRealtime(.3f);Check(!ui.RadialAnimating,"Animación de cierre termina sin dejar interacción activa");
            p.Settings.ReducedMotion=true;p.HandleMapClick(factory,1,screen);yield return null;Capture("10_rueda_movimiento_reducido");yield return null;p.CloseRadial();p.Settings.ReducedMotion=false;
            for(int n=0;n<p.World.Countries.Length;n++)if(g.TerritoryCounts[n]>0&&g.CapitalCells[n]>=0&&g.BuildingAt(g.CapitalCells[n])==null){g.Gold[n]=125000;g.Construct(n,g.CapitalCells[n],BuildingKind.City);}for(int i=0;i<60;i++){g.Step(.1f);yield return null;}
            camera.ResetView();yield return new WaitForSecondsRealtime(.4f);var models=p.Visuals.GetComponent<StructureModelVisuals>();Check(models.VisibleModels>96,"Construcciones físicas visibles por todo el mundo sin depender de logos");Capture("11_construcciones_mundo");yield return null;
            float started=Time.realtimeSinceStartup;var times=new List<float>();while(Time.realtimeSinceStartup-started<30){times.Add(Time.unscaledDeltaTime*1000);yield return null;}times.Sort();Check(errors==0,"Recorrido visual sin errores");
            File.WriteAllText(Path.Combine(folder,"visual-refinement.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=errors,checks=checks.ToArray(),models=models.VisibleModels,drawCalls=models.DrawCalls,fps=times.Count*1000/times.Sum(),p95Ms=times[Mathf.FloorToInt(times.Count*.95f)],maximumMs=times.Last()},true));Debug.Log("VISUAL_REFINEMENT_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public int errors,models,drawCalls;public float fps,p95Ms,maximumMs;public string[] checks;}
    }
}
