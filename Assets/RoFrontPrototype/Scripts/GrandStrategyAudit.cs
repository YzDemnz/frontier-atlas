using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class GrandStrategyAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();private FrontierPrototype p;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-strategy-audit");if(i<0||i+1>=args.Length)return;var a=new GameObject("Prueba estratégica ×20").AddComponent<GrandStrategyAudit>();a.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(a.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks.Add(message);Debug.Log("STRATEGY_NATIVE PASS "+message);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception failure=null;try{next=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){if(p!=null)p.AuditManualSimulation=true;File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return run.Current;}}
        private Texture2D Globe(float aspect,float turn,string name=null)
        {
            var material=new Material(Resources.Load<Shader>("MenuEarth"));material.SetTexture("_MainTex",Resources.Load<Texture2D>("Cartography/EarthRelief8K"));material.SetFloat("_Aspect",aspect);material.SetFloat("_Rotation",turn);material.SetFloat("_CloudTime",0);
            int width=960,height=Mathf.RoundToInt(width/aspect);var rt=new RenderTexture(width,height,0);var previous=RenderTexture.active;Graphics.Blit(null,rt,material);RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(material);if(name!=null)File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());return texture;
        }
        private void Claim(TerritorySimulation g,int f,int center,int radius)
        {int cx=center%g.World.Width,cy=center/g.World.Width;for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++){if(dx*dx+dy*dy>radius*radius)continue;int x=cx+dx,y=cy+dy;if(!g.World.InBounds(x,y))continue;int cell=g.World.At(x,y);if(g.World.Land[cell]&&g.Owners[cell]==TerritorySimulation.Neutral)g.Owners[cell]=f;}}
        private IEnumerator Run()
        {
            yield return null;p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.ShowMainMenu();yield return new WaitForSecondsRealtime(1);Capture("01_menu_corregido");yield return null;
            foreach(float aspect in new[]{4f/3,16f/9,21f/9})foreach(float turn in new[]{0f,.25f,.5f,.75f})
            {var image=Globe(aspect,turn,"globo_"+aspect.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"_"+(turn*360).ToString("0"));Destroy(image);float radius=Mathf.Min(.4f,aspect*.235f);Check(.73f+radius/aspect<.98f&&.52f-radius>.02f&&.52f+radius<.98f,"Globo sin recorte · aspecto "+aspect.ToString("0.00")+" giro "+turn*360);yield return null;}
            var left=Globe(16f/9,.49999f);var right=Globe(16f/9,.50001f);var a=left.GetPixels32();var b=right.GetPixels32();double difference=0;for(int i=0;i<a.Length;i++)difference+=(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b))/(255.0*3);difference/=a.Length;Destroy(left);Destroy(right);Check(difference<.01,"Textura continua al cruzar longitud 180°");
            p.PlayerName="Prueba estratégica";p.ChosenDifficulty=Difficulty.Brutal;p.BeginLocationSelection();var g=p.Simulation;int center=p.World.StartCells[Array.FindIndex(p.World.Countries,c=>c.code=="ZWE")];int start=Enumerable.Range(12,70).Select(n=>center+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation(),"Campaña brutal fundada");g.AiEnabled=false;g.ReinforcementsEnabled=false;
            for(int n=0;n<p.World.Countries.Length;n++)if(g.CapitalCells[n]>=0)Claim(g,n,g.CapitalCells[n],30);Claim(g,g.PlayerFaction,start,40);Array.Clear(g.TerritoryCounts,0,g.FactionCount);for(int cell=0;cell<g.Owners.Length;cell++)if(g.Owners[cell]>=0)g.TerritoryCounts[g.Owners[cell]]++;g.RebuildCampaignIndexes();Array.Clear(g.ProtectionUntil,0,g.FactionCount);
            for(int n=0;n<p.World.Countries.Length;n++)if(g.TerritoryCounts[n]>0)
            {
                g.Gold[n]=5000000;g.Troops[n]=15000+(n%7)*4000;int city=g.OwnedCells(n).FirstOrDefault(c=>g.ValidateConstruction(n,c,BuildingKind.City).success);if(city>0)g.Construct(n,city,BuildingKind.City);
                int factory=g.OwnedCells(n).Where(c=>g.ValidateConstruction(n,c,BuildingKind.Factory).success&&Vector2.Distance(new Vector2(c%p.World.Width,c/p.World.Width),new Vector2(city%p.World.Width,city/p.World.Width))>=15*p.World.ResolutionScale).DefaultIfEmpty(-1).First();if(factory>=0)g.Construct(n,factory,BuildingKind.Factory);
            }
            foreach(string code in new[]{"FRA","DEU","GBR","CHN","IND","USA"}){int n=Array.FindIndex(p.World.Countries,c=>c.code==code);if(n>=0)g.Troops[n]=1600000;}
            int f=g.PlayerFaction;g.Troops[f]=1600000;g.Gold[f]=10000000;
            for(int i=0;i<70;i++){g.Step(.1f);yield return null;}
            int neighbor=Enumerable.Range(0,p.World.Countries.Length).FirstOrDefault(n=>g.Neighbors(f,n));int target=g.CapitalCells[neighbor];var ui=FindAnyObjectByType<FrontierInterface>();ui.ShowWarActions(target);yield return new WaitForSecondsRealtime(.3f);Capture("02_declarar_guerra");yield return new WaitForEndOfFrame();yield return null;ui.CloseWarActions();
            Check(g.CanDeclareWar(f,neighbor)&&g.DeclareWar(f,neighbor).success,"Botón de guerra disponible contra vecino real");ui.ShowWarActions(target);yield return new WaitForSecondsRealtime(.3f);Capture("03_treguas_cinco_meses");yield return new WaitForEndOfFrame();yield return null;ui.CloseWarActions();Check(g.ProposeWarTruce(f,neighbor,.5f).success,"Oferta porcentual en mundo completo");g.AnswerCampaign(neighbor,g.Affairs.proposals.Last().id,true);
            g.CampaignSpeed=20;Check(g.CampaignSpeed==20,"Velocidad ×20 elegida");FindAnyObjectByType<FrontierCamera>().ResetView();yield return new WaitForSecondsRealtime(.3f);Capture("04_barra_velocidad");
            // Only this opt-in stress fixture protects its observer, so defeat cannot truncate the measurement.
            g.ProtectionUntil[f]=1000000;g.AiEnabled=true;g.ReinforcementsEnabled=true;g.MeasurePerformance=true;p.AuditManualSimulation=false;
            var times=new List<float>();var lateTimes=new List<float>();float began=Time.realtimeSinceStartup,initial=g.ElapsedTime,next=began+30;int peakTrains=0,peakWars=0,peakPlans=0;long peakMemory=0;float duration=Application.isEditor?12:300;
            while(Time.realtimeSinceStartup-began<duration&&g.Outcome==CampaignOutcome.Playing)
            {
                float ms=Time.unscaledDeltaTime*1000;times.Add(ms);if(Time.realtimeSinceStartup-began>180)lateTimes.Add(ms);peakTrains=Math.Max(peakTrains,g.Trains.Count);peakWars=Math.Max(peakWars,g.Affairs.wars.Count(w=>w.active));peakPlans=Math.Max(peakPlans,g.Strategy.plans.Count);peakMemory=Math.Max(peakMemory,System.GC.GetTotalMemory(false));
                if(Time.realtimeSinceStartup>=next){next+=30;File.WriteAllText(Path.Combine(folder,"progreso.json"),JsonUtility.ToJson(new Report{seconds=Time.realtimeSinceStartup-began,simulationSeconds=g.ElapsedTime-initial,peakWars=peakWars,peakPlans=peakPlans,peakTrains=peakTrains,errors=errors},true));}yield return null;
            }
            p.AuditManualSimulation=true;float elapsed=Time.realtimeSinceStartup-began;Check(g.Outcome==CampaignOutcome.Playing,"Campaña de estrés continúa sin derrota prematura");Check(errors==0,"Campaña acelerada sin excepciones");Check(peakPlans>20&&g.Affairs.proposals.Count>0,"IA brutal genera planes y negociaciones reales");Capture("05_campana_acelerada");times.Sort();lateTimes.Sort();var report=new Report{inEditor=Application.isEditor,seconds=elapsed,simulationSeconds=g.ElapsedTime-initial,effectiveSpeed=(g.ElapsedTime-initial)/elapsed,fps=times.Count*1000/times.Sum(),p95Ms=times[(int)(times.Count*.95f)],fpsAfterThreeMinutes=lateTimes.Count>0?lateTimes.Count*1000/lateTimes.Sum():0,p95AfterThreeMinutes=lateTimes.Count>0?lateTimes[(int)(lateTimes.Count*.95f)]:0,peakTrains=peakTrains,peakWars=peakWars,peakPlans=peakPlans,buildings=g.Buildings.Count,coalitions=g.Affairs.coalitions.Count,peakManagedBytes=peakMemory,errors=errors,checks=checks.ToArray(),globeSeamDifference=(float)difference};File.WriteAllText(Path.Combine(folder,"grand-strategy-native.json"),JsonUtility.ToJson(report,true));Debug.Log("STRATEGY_NATIVE_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public int errors,peakTrains,peakWars,peakPlans,buildings,coalitions;public long peakManagedBytes;public float seconds,simulationSeconds,effectiveSpeed,fps,p95Ms,fpsAfterThreeMinutes,p95AfterThreeMinutes,globeSeamDifference;public string[] checks;}
    }
}
