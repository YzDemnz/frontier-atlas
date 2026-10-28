using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas
{
    // Explicit developer-only command line. No profile writes or user input during a run.
    public sealed class LongSessionAudit:MonoBehaviour
    {
        private int _lastView=-1;private string _directory;private bool _fast;private float _duration;private int _errors;private readonly List<Window> _windows=new List<Window>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-frontier-session-audit");if(index<0||index+1>=args.Length)return;
            var audit=new GameObject("Prueba de sesión prolongada").AddComponent<LongSessionAudit>();audit._directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(audit._directory);
            audit._fast=Array.IndexOf(args,"-session-fast")>=0;int seconds=Array.IndexOf(args,"-session-seconds");audit._duration=seconds>=0&&seconds+1<args.Length?float.Parse(args[seconds+1],System.Globalization.CultureInfo.InvariantCulture):360;
        }
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors++;}
        private IEnumerator Start()
        {
            var p=FindAnyObjectByType<FrontierPrototype>();while(p==null){yield return null;p=FindAnyObjectByType<FrontierPrototype>();}
            p.FinishIntro();while(p.ScreenState==FrontierScreen.Intro)yield return null;
            p.PlayerName="Prueba prolongada";p.BeginLocationSelection();var g=p.Simulation;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-session-legacy-balance")>=0){g.Rules.balanceProfile=BalanceProfile.LegacyPrototype;g.Rules.baseIncome=150;g.Rules.allowCapitalTrade=true;g.Rules.distanceBasedSeaReward=false;g.RebuildCampaignIndexes();}
            int country=Array.FindIndex(p.World.Countries,c=>c.code=="ZWE");int capital=Enumerable.Range(0,g.Owners.Length).First(c=>p.World.GeographicCountries[c]==country&&g.CanFoundAt(c));
            if(!p.SelectFoundation(capital)||!p.ConfirmFoundation()){Debug.LogError("SESSION_AUDIT Foundation failed");Application.Quit(1);yield break;}
            g.ProtectionUntil[g.PlayerFaction]=_duration+360;g.MeasurePerformance=true;p.AuditManualSimulation=_fast;p.Expand();p.ViewContinent();
            double started=Time.realtimeSinceStartupAsDouble,last=started;float nextExpand=15;var frames=new List<float>();float maximum=0;int slow=0;
            while((_fast?g.ElapsedTime:Time.realtimeSinceStartupAsDouble-started)<_duration&&g.Outcome==CampaignOutcome.Playing)
            {
                if(_fast)for(int k=0;k<10;k++)g.Step(.1f);
                if(g.ElapsedTime>=nextExpand){nextExpand=g.ElapsedTime+15;p.Expand();}
                if(!_fast){int view=(int)(g.ElapsedTime/60)%3;if(view!=_lastView){_lastView=view;if(view==0)p.ViewContinent();else {int home=g.CapitalCells[g.PlayerFaction];FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(home%g.World.Width,home/g.World.Width),view==1?96:36);}}}
                float frame=Time.unscaledDeltaTime;frames.Add(frame);maximum=Mathf.Max(maximum,frame);if(frame>.05f)slow++;
                double now=Time.realtimeSinceStartupAsDouble;
                if(now-last>=30||(_fast&&g.ElapsedTime>=_duration))
                {
                    float[] sorted=frames.OrderBy(x=>x).ToArray();var sample=new Window{wallSeconds=(float)(now-started),simulationSeconds=g.ElapsedTime,frames=frames.Count,fps=frames.Count/(float)(now-last),p95Milliseconds=sorted[(int)((sorted.Length-1)*.95f)]*1000,maxMilliseconds=maximum*1000,framesOver50ms=slow,
                        buildings=g.Buildings.Count,routes=g.TradeRoutes.Count,operations=g.Operations.Count,pendingSea=g.PendingSeaPlans,pendingLand=g.PendingLandPlans,allocatedUnityMB=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576f};
                    _windows.Add(sample);frames.Clear();last=now;maximum=0;slow=0;Save(p,g);Debug.Log("SESSION_AUDIT window="+JsonUtility.ToJson(sample));
                }
                yield return null;
            }
            Save(p,g);Application.Quit(_errors==0?0:1);
        }
        private void Save(FrontierPrototype p,TerritorySimulation g)
        {
            var report=new Report{cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,errors=_errors,fastForward=_fast,simulationSeconds=g.ElapsedTime,windows=_windows.ToArray(),meters=g.Meters};
            File.WriteAllText(Path.Combine(_directory,"long-session.json"),JsonUtility.ToJson(report,true));
        }
        [Serializable]private sealed class Window{public float wallSeconds,simulationSeconds,fps,p95Milliseconds,maxMilliseconds,allocatedUnityMB;public int frames,framesOver50ms,buildings,routes,operations,pendingSea,pendingLand;}
        [Serializable]private sealed class Report{public string cpu,gpu;public int errors;public bool fastForward;public float simulationSeconds;public Window[] windows;public SimulationMeter[] meters;}
    }
}
