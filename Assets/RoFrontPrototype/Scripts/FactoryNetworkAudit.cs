using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class FactoryNetworkAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();private FrontierPrototype p;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-factory-audit");if(i<0||i+1>=args.Length)return;var a=new GameObject("Prueba de conexiones persistentes").AddComponent<FactoryNetworkAudit>();a.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(a.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("FACTORY_NATIVE PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception error=null;try{next=run.MoveNext();}catch(Exception e){error=e;}if(error!=null){if(p!=null)p.AuditManualSimulation=true;File.WriteAllText(Path.Combine(folder,"failure.txt"),error.ToString());Debug.LogException(error);yield break;}if(!next)yield break;yield return run.Current;}}
        private void Claim(TerritorySimulation g,int f,int center,int radius)
        {for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++){if(dx*dx+dy*dy>radius*radius)continue;int x=center%g.World.Width+dx,y=center/g.World.Width+dy;if(!g.World.InBounds(x,y))continue;int c=g.World.At(x,y);if(g.World.Land[c]&&g.Owners[c]==TerritorySimulation.Neutral){g.Owners[c]=f;g.TerritoryCounts[f]++;}}}
        private int Spot(TerritorySimulation g,int f,BuildingKind kind,int center,float minimum=0,float maximum=99999)
        {return g.OwnedCells(f).Where(c=>{float d=Vector2.Distance(new Vector2(c%g.World.Width,c/g.World.Width),new Vector2(center%g.World.Width,center/g.World.Width));return g.LandConnected(c,center)&&d>=minimum&&d<=maximum&&g.ValidateConstruction(f,c,kind).success;}).DefaultIfEmpty(-1).First();}
        private IEnumerator Run()
        {
            yield return null;p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.PlayerName="Red industrial";p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;
            int angola=Array.FindIndex(p.World.Countries,c=>c.code=="AGO"),seed=p.World.StartCells[angola],center=p.World.CoastCells.Where(c=>g.CanFoundAt(c)).OrderBy(c=>Vector2.Distance(new Vector2(c%p.World.Width,c/p.World.Width),new Vector2(seed%p.World.Width,seed/p.World.Width))).First();p.SelectFoundation(center);Check(p.ConfirmFoundation(),"FundaciÃ³n costera vÃ¡lida");int f=g.PlayerFaction;
            double initial=g.Gold[f];for(int i=0;i<100;i++)g.Step(.1f);Check(Math.Abs(g.Gold[f]-initial-18000)<.01&&g.IncomeRate(f)==1800,"1800 oro/s desde el inicio sin edificios");float originalTroops=g.Troops[f];g.Troops[f]=0;Check(Mathf.Abs(g.ReinforcementRate(f)-12.96f)<.0001,"Reclutamiento adicional del 35% aplicado");g.Troops[f]=originalTroops;
            Claim(g,f,center,Mathf.CeilToInt(g.FactoryConnectionRadius));for(int n=0;n<p.World.Countries.Length;n++)if(g.CapitalCells[n]>=0)Claim(g,n,g.CapitalCells[n],28);g.RebuildCampaignIndexes();g.Gold[f]=100000000;
            int factoryCell=Spot(g,f,BuildingKind.Factory,center,15,38);Check(factoryCell>=0&&g.Construct(f,factoryCell,BuildingKind.Factory).success,"FÃ¡brica construida cerca de la costa");var factory=g.BuildingAt(factoryCell);var stations=new List<int>();int close=Spot(g,f,BuildingKind.City,factoryCell,7,12);Check(close>=0&&g.Construct(f,close,BuildingKind.City).success,"Ciudad cercana que antes rechazaba el mÃ­nimo de separaciÃ³n");stations.Add(g.BuildingAt(close).id);
            for(int i=0;i<11;i++){int c=Spot(g,f,BuildingKind.City,factoryCell,13,g.FactoryConnectionRadius*.78f);if(c>=0&&g.Construct(f,c,BuildingKind.City).success)stations.Add(g.BuildingAt(c).id);}
            for(int i=0;i<2;i++){int c=Spot(g,f,BuildingKind.Port,factoryCell,0,g.FactoryConnectionRadius*.9f);if(c>=0&&g.Construct(f,c,BuildingKind.Port).success)stations.Add(g.BuildingAt(c).id);}
            Check(stations.Count>=10,"Escenario con mÃ¡s de tres destinos, incluidas ciudades y puertos");
            for(int n=0;n<p.World.Countries.Length;n++)if(g.TerritoryCounts[n]>0){g.Gold[n]=10000000;int c=Spot(g,n,BuildingKind.Factory,g.CapitalCells[n]);if(c>=0)g.Construct(n,c,BuildingKind.Factory);for(int i=0;i<3;i++){int city=Spot(g,n,BuildingKind.City,g.CapitalCells[n]);if(city>=0)g.Construct(n,city,BuildingKind.City);}}
            for(int i=0;i<300;i++){g.Step(.1f);yield return null;}
            int[] ids=g.TradeRoutes.Where(r=>!r.naval&&r.fromBuilding==factory.id&&stations.Contains(r.toBuilding)).Select(r=>r.id).ToArray();Check(ids.Length==stations.Count,"Todos los destinos del alcance estÃ¡n enlazados automÃ¡ticamente");var camera=FindAnyObjectByType<FrontierCamera>();camera.Focus(new Vector2(factoryCell%p.World.Width,factoryCell/p.World.Width),85);yield return new WaitForSecondsRealtime(.5f);Capture("01_red_completa");yield return new WaitForEndOfFrame();
            var roads=FindAnyObjectByType<RoadNetworkVisuals>();Check(roads.VisibleConnections>=stations.Count,"LÃ­neas blancas visibles para todos los destinos");camera.ResetView();yield return new WaitForSecondsRealtime(.5f);Check(roads.VisibleConnections>0,"Red permanece visible al alejar a vista mundial");Capture("02_vista_mundial");yield return new WaitForEndOfFrame();camera.Focus(new Vector2(factoryCell%p.World.Width,factoryCell/p.World.Width),85);
            Check(g.Construct(f,factoryCell,BuildingKind.Factory).success,"Mejora de fÃ¡brica iniciada");for(int i=0;i<20;i++){g.Step(.1f);yield return null;}Check(ids.All(id=>g.TradeRoutes.Any(r=>r.id==id)),"Mejora conserva lÃ­neas y rutas");for(int i=0;i<50;i++){g.Step(.1f);yield return null;}
            g.CampaignSpeed=20;g.ReinforcementsEnabled=true;p.AuditManualSimulation=false;float began=Time.realtimeSinceStartup,sim=g.ElapsedTime,next=began+30,duration=Application.isEditor?8:300;var times=new List<float>();var late=new List<float>();int peak=0;
            while(Time.realtimeSinceStartup-began<duration){float elapsed=Time.realtimeSinceStartup-began;times.Add(Time.unscaledDeltaTime*1000);if(elapsed>=180)late.Add(Time.unscaledDeltaTime*1000);peak=Math.Max(peak,g.Trains.Count);if(Time.realtimeSinceStartup>=next){next+=30;Check(ids.All(id=>g.TradeRoutes.Any(r=>r.id==id)),"Identidad de conexiones conservada tras "+Mathf.FloorToInt(elapsed)+" s reales");File.WriteAllText(Path.Combine(folder,"progreso.json"),"{\"seconds\":"+Mathf.FloorToInt(elapsed)+",\"errors\":"+errors+"}");}yield return null;}
            p.AuditManualSimulation=true;Check(ids.All(id=>g.TradeRoutes.Any(r=>r.id==id)),"SesiÃ³n larga conserva todas las conexiones originales");Check(roads.VisibleConnections>=stations.Count,"LÃ­neas siguen visibles despuÃ©s de la sesiÃ³n");Check(g.TradeDeliveries.Any(d=>d.producer==factory.id),"Trenes entregan oro desde la fÃ¡brica");Check(errors==0,"Sin errores durante el recorrido");Capture("03_red_tras_sesion");times.Sort();late.Sort();float seconds=Time.realtimeSinceStartup-began;
            File.WriteAllText(Path.Combine(folder,"factory-network-native.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,seconds=seconds,effectiveSpeed=(g.ElapsedTime-sim)/seconds,fps=times.Count*1000/times.Sum(),p95Ms=times[(int)(times.Count*.95f)],fpsAfterThreeMinutes=late.Count>0?late.Count*1000/late.Sum():0,connections=ids.Length,worldRoutes=g.TradeRoutes.Count,buildings=g.Buildings.Count,peakTrains=peak,errors=errors,checks=checks.ToArray()},true));Debug.Log("FACTORY_NATIVE_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public float seconds,effectiveSpeed,fps,p95Ms,fpsAfterThreeMinutes;public int connections,worldRoutes,buildings,peakTrains,errors;public string[] checks;}
    }
}
