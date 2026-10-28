using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    // Opt-in native regression scene, never enabled in normal games.
    public sealed class TroopNetworkAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-network-audit");if(i<0||i+1>=args.Length)return;var a=new GameObject("Prueba de tropas, red y zoom").AddComponent<TroopNetworkAudit>();a.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(a.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("NETWORK_AUDIT PASS "+text);}
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception failure=null;try{next=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return run.Current;}}
        private static void Claim(TerritorySimulation g,int f,int center,int rx,int ry)
        {int x=center%g.World.Width,y=center/g.World.Width;for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++){if(!g.World.InBounds(x+dx,y+dy))continue;int c=g.World.At(x+dx,y+dy);if(!g.World.Land[c]||g.Owners[c]==f)continue;int old=g.Owners[c];if(old>=0)g.TerritoryCounts[old]--;g.Owners[c]=f;g.TerritoryCounts[f]++;}}
        private IEnumerator Run()
        {
            yield return null;var p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.PlayerName="Red del Atlántico";p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;
            int nation=Array.FindIndex(p.World.Countries,c=>c.code=="PRT"),center=p.World.StartCells[nation];int start=Enumerable.Range(10,80).Select(n=>center+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation()&&g.Troops[g.PlayerFaction]==10000,"Partida nueva con 10.000 tropas");int f=g.PlayerFaction;
            Claim(g,f,start,46,40);g.RebuildCampaignIndexes();g.Gold[f]=10000000;
            int city=g.OwnedCells(f).Where(c=>!g.IsImpassable(c)).OrderBy(c=>Vector2.Distance(new Vector2(c%p.World.Width,c/p.World.Width),new Vector2(start%p.World.Width-12,start/p.World.Width+8))).First();
            Check(g.Construct(f,city,BuildingKind.City).success,"Ciudad del jugador");
            int factory=g.OwnedCells(f).Where(c=>Mathf.Abs(c%p.World.Width-city%p.World.Width)>12&&g.ValidateConstruction(f,c,BuildingKind.Factory).success).OrderBy(c=>Vector2.Distance(new Vector2(c%p.World.Width,c/p.World.Width),new Vector2(city%p.World.Width+26,city/p.World.Width))).First();Check(g.Construct(f,factory,BuildingKind.Factory).success,"Fábrica del jugador");
            int port=p.World.CoastCells.First(c=>g.Owners[c]==f&&g.ValidateConstruction(f,c,BuildingKind.Port).success);Check(g.Construct(f,port,BuildingKind.Port).success,"Puerto costero del jugador");
            for(int i=0;i<180;i++){g.Step(.1f);yield return null;}
            Check(g.TradeRoutes.Any(r=>!r.naval&&(r.fromBuilding==g.BuildingAt(port).id||r.toBuilding==g.BuildingAt(port).id))&&g.Ledgers[f].landTrade>0,"Ciudad, fábrica y puerto conectados automáticamente con entregas reales");
            float reserve=g.Troops[f];g.Troops[f]=0;Check(Mathf.Abs(g.ReinforcementRate(f)/(g.Rules.tacticalBaseRegeneration*TerritorySimulation.RecruitmentBoost)-1.35f)<.001f,"Ciudad aporta el 35% a la generación base");g.Troops[f]=reserve;
            var camera=FindAnyObjectByType<FrontierCamera>();var models=p.Visuals.GetComponent<StructureModelVisuals>();Matrix4x4? baseline=null;
            foreach(float zoom in new[]{42f,88f,92f,238f,242f,42f})
            {
                camera.Focus(new Vector2(city%p.World.Width,city/p.World.Width),zoom);yield return new WaitForSecondsRealtime(.4f);
                var matrices=(Matrix4x4[][])typeof(StructureModelVisuals).GetField("_matrices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(models);var counts=(int[])typeof(StructureModelVisuals).GetField("_counts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(models);
                Check(counts[1]>0&&counts[4]>0&&counts[0]+counts[2]+counts[3]+counts[5]==0,"Mismos modelos de ciudad y fábrica a zoom "+zoom);
                var matrix=matrices[1][0];if(baseline.HasValue)Check(matrix==baseline.Value,"Zoom conserva posición, escala y pivote");else baseline=matrix;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"zoom_"+zoom+".png"));yield return null;
            }
            // Populate both model types across the world to measure the instanced render under load.
            for(int n=0;n<p.World.Countries.Length;n++)if(n!=f&&g.TerritoryCounts[n]>0&&g.CapitalCells[n]>=0)Claim(g,n,g.CapitalCells[n],12,12);
            g.RebuildCampaignIndexes();
            for(int n=0;n<p.World.Countries.Length;n++)if(g.TerritoryCounts[n]>0)
            {g.Gold[n]=1000000;foreach(var kind in new[]{BuildingKind.City,BuildingKind.Factory}){int c=g.OwnedCells(n).Where(id=>g.ValidateConstruction(n,id,kind).success).DefaultIfEmpty(-1).First();if(c>=0)g.Construct(n,c,kind);}}
            for(int i=0;i<70;i++){g.Step(.1f);yield return null;}camera.ResetView();yield return new WaitForSecondsRealtime(.5f);Check(models.VisibleModels>250,"Más de 250 edificios de ambos tipos visibles en el mundo");ScreenCapture.CaptureScreenshot(Path.Combine(folder,"mundo_red.png"));yield return null;
            g.AiEnabled=true;g.ReinforcementsEnabled=true;g.MeasurePerformance=true;p.AuditManualSimulation=false;
            var times=new List<float>();var late=new List<float>();float began=Time.realtimeSinceStartup,next=30;int peakModels=models.VisibleModels,peakPlans=0;
            while(Time.realtimeSinceStartup-began<300)
            {
                float elapsed=Time.realtimeSinceStartup-began;times.Add(Time.unscaledDeltaTime*1000);if(elapsed>=180)late.Add(Time.unscaledDeltaTime*1000);peakModels=Mathf.Max(peakModels,models.VisibleModels);peakPlans=Mathf.Max(peakPlans,g.PendingLandPlans+g.PendingSeaPlans);
                if(elapsed>=next){Debug.Log("NETWORK_SOAK seconds="+elapsed+" buildings="+g.Buildings.Count+" routes="+g.TradeRoutes.Count+" plans="+(g.PendingLandPlans+g.PendingSeaPlans));next+=30;}yield return null;
            }
            times.Sort();late.Sort();Check(errors==0,"Cinco minutos de simulación y render sin errores");
            File.WriteAllText(Path.Combine(folder,"troops-network.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=errors,checks=checks.ToArray(),seconds=300,peakModels=peakModels,drawCalls=models.DrawCalls,peakPlans=peakPlans,buildings=g.Buildings.Count,routes=g.TradeRoutes.Count,fps=times.Count*1000/times.Sum(),p95Ms=times[(int)(times.Count*.95f)],maximumMs=times.Last(),fpsAfterThreeMinutes=late.Count*1000/late.Sum(),p95AfterThreeMinutes=late[(int)(late.Count*.95f)]},true));Debug.Log("TROOPS_NETWORK_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public int errors,seconds,peakModels,drawCalls,peakPlans,buildings,routes;public float fps,p95Ms,maximumMs,fpsAfterThreeMinutes,p95AfterThreeMinutes;public string[] checks;}
    }
}
