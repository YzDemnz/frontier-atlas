using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    // Opt-in developer fixture; no player profile writes or resource changes in ordinary games.
    public sealed class DirectionalPresentationAudit:MonoBehaviour
    {
        private string _directory;private int _errors;private readonly List<string> _passed=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-frontier-direction-audit");if(flag<0||flag+1>=args.Length)return;
            var audit=new GameObject("Prueba de expansión dirigida").AddComponent<DirectionalPresentationAudit>();audit._directory=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(audit._directory);
        }
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)_errors++;}
        private void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);_passed.Add(name);Debug.Log("DIRECTION_AUDIT PASS "+name);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
        private IEnumerator Start()
        {
            var run=Run();while(true){bool more=false;Exception failure=null;try{more=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(_directory,"failure.txt"),failure.ToString());Debug.LogException(failure);Application.Quit(1);yield break;}if(!more)yield break;yield return run.Current;}
        }
        private IEnumerator Advance(TerritorySimulation game,int cells)
        {
            int initial=game.TerritoryCounts[game.PlayerFaction];for(int tick=0;tick<10000&&game.TerritoryCounts[game.PlayerFaction]-initial<cells;tick++){game.Step(.1f);if(tick%8==0)yield return null;}
            Check(game.TerritoryCounts[game.PlayerFaction]-initial>=cells,"El frente avanza sobre el mapa real");
        }
        private IEnumerator Run()
        {
            var p=FindAnyObjectByType<FrontierPrototype>();while(p==null){yield return null;p=FindAnyObjectByType<FrontierPrototype>();}
            p.FinishIntro();while(p.ScreenState==FrontierScreen.Intro)yield return null;
            p.PlayerName="Horizonte";p.BeginLocationSelection();p.AuditManualSimulation=true;var g=p.Simulation;int f=g.PlayerFaction;
            g.AiEnabled=false;g.ReinforcementsEnabled=false;int region=Array.FindIndex(p.World.Countries,c=>c.code=="ZMB");
            int desired=p.World.At(Mathf.RoundToInt((26-p.World.West)/(p.World.East-p.World.West)*p.World.Width),Mathf.RoundToInt((-14-p.World.South)/(p.World.North-p.World.South)*p.World.Height));
            int capital=Enumerable.Range(0,g.Owners.Length).Where(c=>p.World.GeographicCountries[c]==region&&g.CanFoundAt(c)).OrderBy(c=>Mathf.Abs(c%p.World.Width-desired%p.World.Width)+Mathf.Abs(c/p.World.Width-desired/p.World.Width)).First();
            Check(p.SelectFoundation(capital)&&p.ConfirmFoundation(),"Fundación en tierra real");
            // A larger starting blot makes the directional silhouette readable without waiting for a whole campaign.
            var callback=(Action<int,int,int>)typeof(TerritorySimulation).GetField("TerritoryCaptured",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
            for(int dy=-18;dy<=18;dy++)for(int dx=-18;dx<=18;dx++)if(dx*dx+dy*dy<=324){int cell=g.World.At(capital%g.World.Width+dx,capital/g.World.Width+dy);if(g.Owners[cell]!=-1)continue;g.Owners[cell]=f;g.TerritoryCounts[f]++;callback?.Invoke(f,cell,-1);}
            g.RebuildCampaignIndexes();g.Troops[f]=100000;g.MeasurePerformance=true;p.SendFraction=.5f;
            FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(capital%g.World.Width,capital/g.World.Width),75);
            yield return new WaitForSecondsRealtime(.5f);Capture("01_mancha_inicial");yield return null;
            int target=g.World.At(capital%g.World.Width+100,capital/g.World.Width),sound=p.Audio.PlayedEffects;float available=g.AvailableTroops(f);
            p.HandleMapClick(target,0,new Vector2(1050,420));Check(g.Operations.Count==0&&g.AvailableTroops(f)==available,"Un clic no envía soldados");
            yield return new WaitForSecondsRealtime(.12f);p.HandleMapClick(target,0,new Vector2(1052,421));Check(g.Operations.Count==1&&g.CommittedTroops[f]==50000,"Doble clic real compromete la mitad de la reserva");
            var advancing=Advance(g,1600);while(advancing.MoveNext())yield return advancing.Current;
            g.ExpansionDirection(f,out _,out var heading,out int primary,out int other);float eastShare=primary/(float)(primary+other);
            Check(heading.x>.99f&&Mathf.Abs(eastShare-75f/110)<.01f&&other>450,"Predominio oriental con crecimiento en el resto");Check(p.Audio.PlayedEffects==sound,"La expansión continúa sin sonidos molestos");
            p.Notify("Doble clic al ESTE · avance amplio hacia ese lado y crecimiento por el resto.");yield return new WaitForSecondsRealtime(.4f);Capture("02_prioridad_este");yield return null;
            target=g.World.At(capital%g.World.Width-100,capital/g.World.Width);p.HandleMapClick(target,0,new Vector2(430,420));yield return new WaitForSecondsRealtime(.12f);p.HandleMapClick(target,0,new Vector2(431,420));
            advancing=Advance(g,1200);while(advancing.MoveNext())yield return advancing.Current;
            Check(g.ExpansionDirection(f,out _,out heading,out primary,out other)&&heading.x<-.99f&&primary>other,"Otro doble clic cambia la prioridad al oeste sin duplicar frente");
            Check(g.Operations.Count(o=>o.active&&o.kind==OperationKind.Expansion)==1,"Una ofensiva reforzada y reorientada");p.Notify("Doble clic al OESTE · el frente cambia su prioridad y sigue ocupando huecos.");yield return new WaitForSecondsRealtime(.4f);Capture("03_prioridad_oeste");yield return null;
            p.AuditManualSimulation=false;var frames=new List<float>();double start=Time.realtimeSinceStartupAsDouble;while(Time.realtimeSinceStartupAsDouble-start<30){frames.Add(Time.unscaledDeltaTime);yield return null;}
            float duration=(float)(Time.realtimeSinceStartupAsDouble-start);frames.Sort();Check(_errors==0,"Sin errores durante la prueba de ejecución");
            var report=new Report{inEditor=Application.isEditor,passed=_passed.ToArray(),errors=_errors,eastPrimaryShare=eastShare,realTimeSeconds=duration,fps=frames.Count/duration,p95Milliseconds=frames[(int)((frames.Count-1)*.95f)]*1000,maximumMilliseconds=frames.Last()*1000,simulationSeconds=g.ElapsedTime,objects=FindObjectsByType<GameObject>().Length};
            File.WriteAllText(Path.Combine(_directory,"direction-audit.json"),JsonUtility.ToJson(report,true));Capture("04_frontera_final");yield return new WaitForSecondsRealtime(.4f);Application.Quit(0);
        }
        [Serializable]private sealed class Report{public bool inEditor;public string[] passed;public int errors,objects;public float eastPrimaryShare,realTimeSeconds,fps,p95Milliseconds,maximumMilliseconds,simulationSeconds;}
    }
}
