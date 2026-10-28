using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class CampaignPerformanceValidation
    {
        public static void Batch()
        {
            try
            {
                var world=new WorldGrid(4320,2160,4206);var g=new TerritorySimulation(world,4206,0,new MatchOptions{customPlayer=true,advancedSystems=true,playerName="Rendimiento",tribeCount=16}){MeasurePerformance=true};int f=g.PlayerFaction,nation=Array.FindIndex(world.Countries,c=>c.code=="ZWE"),center=world.StartCells[nation];int start=Enumerable.Range(12,50).Select(n=>center+n).First(c=>g.CanFoundAt(c));g.FoundPlayer(start,"Rendimiento");int x=start%world.Width,y=start/world.Width;
                for(int dy=-30;dy<=30;dy++)for(int dx=-35;dx<=35;dx++){int c=world.At(x+dx,y+dy);if(!world.Land[c])continue;int old=g.Owners[c];if(old==f)continue;if(old>=0)g.TerritoryCounts[old]--;g.Owners[c]=f;g.TerritoryCounts[f]++;}g.RebuildCampaignIndexes();g.Gold[f]=5000000;g.Construct(f,world.At(x-12,y),BuildingKind.City);g.Construct(f,world.At(x+12,y),BuildingKind.Factory);
                var watch=Stopwatch.StartNew();double window=0,max=0;for(int i=0;i<3600;i++){double begin=watch.Elapsed.TotalMilliseconds;g.Step(.1f);double ms=watch.Elapsed.TotalMilliseconds-begin;max=Math.Max(max,ms);if(i%300==299){UnityEngine.Debug.Log("CAMPAIGN_BENCH sim="+g.ElapsedTime+" wallWindowMs="+(watch.Elapsed.TotalMilliseconds-window)+" maxStepMs="+max+" buildings="+g.Buildings.Count+" pacts="+g.Affairs.compacts.Count+" requests="+g.Affairs.support.Count+" nav="+g.PendingSeaPlans);foreach(var m in g.Meters)UnityEngine.Debug.Log("BENCH_METER "+m.name+" total="+m.totalMilliseconds+" max="+m.maxMilliseconds);window=watch.Elapsed.TotalMilliseconds;max=0;}}
                string output=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("../outputs");File.WriteAllText(Path.Combine(output,"expanded-campaign-cpu.json"),JsonUtility.ToJson(new Report{simulationSeconds=g.ElapsedTime,wallSeconds=watch.Elapsed.TotalSeconds,meters=g.Meters,buildings=g.Buildings.Count,requests=g.Affairs.support.Count},true));UnityEngine.Debug.Log("CAMPAIGN_BENCH_COMPLETE");EditorApplication.Exit(0);
            }catch(Exception e){UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable]private sealed class Report{public float simulationSeconds;public double wallSeconds;public int buildings,requests;public SimulationMeter[] meters;}
    }
}
