using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class ExpandedCampaignAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-expanded-audit");if(i<0||i+1>=args.Length)return;var audit=new GameObject("Verificación de campaña y estructuras").AddComponent<ExpandedCampaignAudit>();audit.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(audit.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("EXPANDED_AUDIT PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private IEnumerator Start()
        {var iterator=Run();while(true){bool next=false;Exception failure=null;try{next=iterator.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return iterator.Current;}}
        private IEnumerator Run()
        {
            yield return null;var p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.PlayerName="Nueva Aurora";p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;int nation=Array.FindIndex(p.World.Countries,c=>c.code=="ZWE"),center=p.World.StartCells[nation];int start=Enumerable.Range(12,50).Select(n=>center+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation(),"Fundación en la Tierra real");int f=g.PlayerFaction,x=start%p.World.Width,y=start/p.World.Width;
            for(int dy=-30;dy<=30;dy++)for(int dx=-35;dx<=35;dx++){int c=p.World.At(x+dx,y+dy);if(!p.World.Land[c])continue;int old=g.Owners[c];if(old==f)continue;if(old>=0)g.TerritoryCounts[old]--;g.Owners[c]=f;g.TerritoryCounts[f]++;}g.RebuildCampaignIndexes();g.Gold[f]=5000000;
            int city=p.World.At(x-12,y),factory=p.World.At(x+12,y);Check(g.Construct(f,city,BuildingKind.City).success&&g.Construct(f,factory,BuildingKind.Factory).success,"Ciudad y fábrica se construyen con oro");for(int i=0;i<80;i++){g.Step(.1f);yield return null;}
            var camera=FindAnyObjectByType<FrontierCamera>();camera.Focus(new Vector2(x,y),32);yield return new WaitForSecondsRealtime(.7f);var models=p.Visuals.GetComponent<StructureModelVisuals>();Check(models!=null&&models.VisibleModels>=2&&models.DrawCalls<=6,"Modelos del usuario visibles, agrupados y con LOD");Capture("01_ciudad_fabrica_zoom");yield return null;
            camera.Focus(new Vector2(x,y),145);yield return new WaitForSecondsRealtime(.4f);Capture("02_estructuras_escala_juego");yield return null;
            int lender=Enumerable.Range(0,g.FactionCount).First(n=>n!=f&&g.TerritoryCounts[n]>0&&!g.IsTribe(n));g.RequestLoan(f,lender,50000,.01,90);var offer=g.Affairs.proposals.Last();g.AnswerCampaign(lender,offer.id,true);g.CreateCoalition(f,0,"Liga de Aurora",WarObjective.Capital,g.CapitalCells[0]);
            var ui=FindAnyObjectByType<FrontierInterface>();for(int tab=0;tab<8;tab++){ui.OpenWorldBoard(tab);yield return new WaitForSecondsRealtime(.25f);Capture("0"+(tab+3)+"_panel_"+tab);yield return null;}ui.CloseWorldBoard();
            float began=Time.realtimeSinceStartup;p.SaveFullCampaign();while(p.SavingCampaign)yield return null;float saveSeconds=Time.realtimeSinceStartup-began;string saved=CampaignFiles.List().First();p.LoadFullCampaign(saved);g=p.Simulation;models=p.Visuals.GetComponent<StructureModelVisuals>();Check(p.CampaignFileNotice.StartsWith("Partida restaurada")&&g.Buildings.Count==2&&g.DebtTotal(f)>0,"Guardado en disco y carga del mapa completo sin perder edificios o deuda");
            g.Paused=false;g.AiEnabled=true;g.ReinforcementsEnabled=true;p.AuditManualSimulation=false;g.MeasurePerformance=true;camera.Focus(new Vector2(x,y),145);
            var windows=new List<Window>();float soakStart=Time.realtimeSinceStartup,last=soakStart;var frames=new List<float>();int peakObjects=0;
            while(Time.realtimeSinceStartup-soakStart<300)
            {
                frames.Add(Time.unscaledDeltaTime*1000);if(Time.realtimeSinceStartup-last>=30){frames.Sort();float sum=frames.Sum();windows.Add(new Window{wallSeconds=Time.realtimeSinceStartup-soakStart,simulationSeconds=g.ElapsedTime,fps=frames.Count*1000/Mathf.Max(1,sum),p95Ms=frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count*.95f)-1,0,frames.Count-1)],maxMs=frames.Last(),objects=FindObjectsByType<GameObject>().Length,buildings=g.Buildings.Count,proposals=g.Affairs.proposals.Count,pendingPlans=g.PendingSeaPlans,visibleModels=models==null?0:p.Visuals.GetComponent<StructureModelVisuals>().VisibleModels});peakObjects=Math.Max(peakObjects,windows.Last().objects);frames.Clear();last=Time.realtimeSinceStartup;Debug.Log("EXPANDED_SOAK "+JsonUtility.ToJson(windows.Last()));File.WriteAllText(Path.Combine(folder,"meters_live.json"),JsonUtility.ToJson(new MeterReport{meters=g.Meters},true));}yield return null;
            }
            Check(errors==0,"Recorrido y cinco minutos reales sin errores");File.WriteAllText(Path.Combine(folder,"expanded-presentation.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=errors,checks=checks.ToArray(),saveSeconds=saveSeconds,peakObjects=peakObjects,windows=windows.ToArray(),meters=g.Meters},true));Capture("11_campana_tras_cinco_minutos");Debug.Log("EXPANDED_AUDIT_COMPLETE");
        }
        [Serializable]private sealed class Window{public float wallSeconds,simulationSeconds,fps,p95Ms,maxMs;public int objects,buildings,proposals,pendingPlans,visibleModels;}
        [Serializable]private sealed class MeterReport{public SimulationMeter[] meters;}
        [Serializable]private sealed class Report{public bool inEditor;public int errors,peakObjects;public string[] checks;public float saveSeconds;public Window[] windows;public SimulationMeter[] meters;}
    }
}
