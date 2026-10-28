using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private static WorldGrid _waterWorld;private static int[] _waterComponents;private int[] _waterComponentMap;
        private readonly Dictionary<ulong,int[]> _seaCache=new Dictionary<ulong,int[]>();
        private readonly Queue<ulong> _seaCacheOrder=new Queue<ulong>();
        private readonly List<SeaPlan> _waitingSeaPlans=new List<SeaPlan>();private SeaPlan _seaPlan;
        private readonly Dictionary<ulong,SeaPlan> _pendingSeaPlans=new Dictionary<ulong,SeaPlan>();
        private int[] _planVisited,_planCosts,_planParents;private int _planStamp;
        public int PendingSeaPlans=>_pendingSeaPlans.Count;
        public int PendingLandPlans {get;private set;}
        public int SeaCacheHits {get;private set;}
        public int DisconnectedSeaRequests {get;private set;}
        public int NavigationNodesLastTick {get;private set;}
        private sealed class SeaPlan
        {
            public bool naval=true;public int faction;public int start,goal,stamp,expanded;public bool priority,reconstructing,smoothing;public int cursor,anchor,candidate;
            public readonly List<SeaNode> heap=new List<SeaNode>();public readonly List<int> raw=new List<int>(),smooth=new List<int>();
            public readonly List<Action<Queue<int>>> completed=new List<Action<Queue<int>>>();
        }
        private static ulong SeaKey(int start,int goal)=>((ulong)(uint)start<<32)|(uint)goal;
        private void InitializeWaterComponents()
        {
            if(Options!=null&&Options.advancedSystems){_planVisited=new int[Owners.Length];_planCosts=new int[Owners.Length];_planParents=new int[Owners.Length];}
            if(_waterWorld==World&&_waterComponents!=null){_waterComponentMap=_waterComponents;return;}_waterWorld=World;_waterComponents=new int[Owners.Length];_waterComponentMap=_waterComponents;int component=0;
            for(int cell=0;cell<Owners.Length;cell++)
            {
                if(World.Land[cell]||_waterComponents[cell]!=0)continue;component++;int head=0,tail=0;_search[tail++]=cell;_waterComponents[cell]=component;
                while(head<tail){int current=_search[head++];for(int d=0;d<4;d++){int n=World.Neighbor(current,d);if(n<0||World.Land[n]||_waterComponents[n]!=0)continue;_waterComponents[n]=component;_search[tail++]=n;}}
            }
            component=0;for(int cell=0;cell<Owners.Length;cell++)
            {if(!World.Land[cell]||_waterComponents[cell]!=0)continue;component--;int head=0,tail=0;_search[tail++]=cell;_waterComponents[cell]=component;
                while(head<tail){int current=_search[head++];for(int d=0;d<4;d++){int n=World.Neighbor(current,d);if(n<0||!World.Land[n]||_waterComponents[n]!=0)continue;_waterComponents[n]=component;_search[tail++]=n;}}}
        }
        public bool LandConnected(int a,int b)=>a>=0&&b>=0&&a<Owners.Length&&b<Owners.Length&&World.Land[a]&&World.Land[b]&&_waterComponentMap[a]==_waterComponentMap[b];
        public bool WaterConnected(int a,int b)=>a>=0&&b>=0&&a<Owners.Length&&b<Owners.Length&&!World.Land[a]&&!World.Land[b]&&_waterComponentMap[a]==_waterComponentMap[b];
        private bool CachedSeaRoute(int start,int goal,out Queue<int> route)
        {
            if(_seaCache.TryGetValue(SeaKey(start,goal),out var stored)){SeaCacheHits++;route=stored==null?null:new Queue<int>(stored);return true;}route=null;return false;
        }
        private void CacheSeaRoute(int start,int goal,int[] path)
        {
            ulong key=SeaKey(start,goal);if(_seaCache.ContainsKey(key))return;
            while(_seaCache.Count>=512)_seaCache.Remove(_seaCacheOrder.Dequeue());_seaCache[key]=path;_seaCacheOrder.Enqueue(key);
        }
        private void PlanSeaRoute(int start,int goal,Action<Queue<int>> completed,bool priority=false)
        {
            if(CachedSeaRoute(start,goal,out var cached)){completed(cached);return;}
            if(!WaterConnected(start,goal)){DisconnectedSeaRequests++;CacheSeaRoute(start,goal,null);completed(null);return;}
            if(SeaLineClear(start,goal)){DirectSeaRoutes++;int[] path=start==goal?Array.Empty<int>():new[]{goal};CacheSeaRoute(start,goal,path);completed(new Queue<int>(path));return;}
            ulong key=SeaKey(start,goal);if(_pendingSeaPlans.TryGetValue(key,out var existing)){existing.completed.Add(completed);existing.priority|=priority;return;}
            var plan=new SeaPlan{start=start,goal=goal,priority=priority};plan.completed.Add(completed);_pendingSeaPlans[key]=plan;_waitingSeaPlans.Add(plan);NavigationSearches++;
        }
        private void PlanLandRoute(int faction,int start,int goal,Action<Queue<int>> completed)
        {if(!LandConnected(start,goal)){completed(null);return;}var direct=DirectTradePath(faction,start,goal);if(direct!=null){completed(direct);return;}var plan=new SeaPlan{start=start,goal=goal,naval=false,faction=faction,priority=faction==PlayerFaction};plan.completed.Add(completed);_waitingSeaPlans.Add(plan);PendingLandPlans++;}
        private float RouteHeuristic(SeaPlan plan,int cell)=>plan.naval?SeaHeuristic(cell,plan.goal):Distance(cell,plan.goal)*10;
        private void FinishSeaPlan(int[] path)
        {
            var plan=_seaPlan;if(plan.naval)CacheSeaRoute(plan.start,plan.goal,path);
            if(path!=null&&plan.naval)
            {
                var reverse=new int[path.Length];for(int i=0;i<reverse.Length;i++)reverse[i]=i==reverse.Length-1?plan.start:path[path.Length-2-i];CacheSeaRoute(plan.goal,plan.start,reverse);
            }
            if(plan.naval)_pendingSeaPlans.Remove(SeaKey(plan.start,plan.goal));else PendingLandPlans--;_seaPlan=null;
            foreach(var callback in plan.completed)callback(path==null?null:new Queue<int>(path));
        }
        private void AdvanceSeaPlanning()
        {
            long stamp=MeterStart();try{AdvanceSeaPlanningCore();}finally{MeterEnd(7,stamp);}
        }
        private void AdvanceSeaPlanningCore()
        {
            if(_seaPlan!=null&&!_seaPlan.priority&&_waitingSeaPlans.Exists(p=>p.priority))
            {
                // Search arrays are shared: restart background work rather than resume stale costs/parents.
                var background=_seaPlan;background.heap.Clear();background.raw.Clear();background.smooth.Clear();background.reconstructing=background.smoothing=false;background.expanded=background.cursor=background.anchor=background.candidate=0;
                _waitingSeaPlans.Add(background);_seaPlan=null;
            }
            NavigationNodesLastTick=0;bool foreground=_seaPlan!=null&&_seaPlan.priority||_waitingSeaPlans.Exists(p=>p.priority);int budget=Mathf.Max(64,Mathf.RoundToInt(Rules.navigationNodesPerTick/(foreground?1:Mathf.Max(1,CampaignSpeed)))),lineBudget=12;
            while(budget>0&&lineBudget>0)
            {
                if(_seaPlan==null)
                {
                    if(_waitingSeaPlans.Count==0)return;int index=_waitingSeaPlans.FindIndex(p=>p.priority);if(index<0)index=0;
                    _seaPlan=_waitingSeaPlans[index];_waitingSeaPlans.RemoveAt(index);
                    if(_planVisited==null){_planVisited=new int[Owners.Length];_planCosts=new int[Owners.Length];_planParents=new int[Owners.Length];}
                    var begin=_seaPlan;begin.stamp=++_planStamp;_planVisited[begin.start]=begin.stamp;_planCosts[begin.start]=0;_planParents[begin.start]=-1;
                    float h=RouteHeuristic(begin,begin.start);Push(begin.heap,new SeaNode(begin.start,h,h));
                }
                var plan=_seaPlan;
                if(plan.reconstructing)
                {
                    while(plan.cursor>=0&&budget-->0){plan.raw.Add(plan.cursor);plan.cursor=_planParents[plan.cursor];}
                    if(plan.cursor>=0)return;plan.raw.Reverse();if(!plan.naval){plan.raw.RemoveAt(0);FinishSeaPlan(plan.raw.ToArray());continue;}plan.reconstructing=false;plan.smoothing=true;plan.candidate=plan.raw.Count-1;
                }
                if(plan.smoothing)
                {
                    if(plan.anchor>=plan.raw.Count-1){FinishSeaPlan(plan.smooth.ToArray());continue;}
                    while(lineBudget-->0)
                    {
                        int next=plan.candidate;
                        if(next<=plan.anchor+1||SeaLineClear(plan.raw[plan.anchor],plan.raw[next]))
                        {plan.smooth.Add(plan.raw[next]);plan.anchor=next;plan.candidate=plan.raw.Count-1;break;}
                        plan.candidate--;
                    }
                    continue;
                }
                if(plan.heap.Count==0||plan.expanded>=1500000){FinishSeaPlan(null);continue;}
                var node=Pop(plan.heap);int cell=node.cell;budget--;NavigationNodesLastTick++;plan.expanded++;
                if(node.score>_planCosts[cell]+RouteHeuristic(plan,cell)+.01f)continue;
                if(cell==plan.goal){plan.reconstructing=true;plan.cursor=cell;continue;}
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if((dx==0&&dy==0)||(!plan.naval&&dx!=0&&dy!=0))continue;int next=OffsetCell(cell,dx,dy);if(next<0)continue;
                    if(plan.naval?World.Land[next]:!TradeLandSurface(next))continue;
                    if(dx!=0&&dy!=0){int a=OffsetCell(cell,dx,0),b=OffsetCell(cell,0,dy);if(a<0||b<0||World.Land[a]||World.Land[b])continue;}
                    int cost=_planCosts[cell]+(dx==0||dy==0?10:14);if(_planVisited[next]==plan.stamp&&cost>=_planCosts[next])continue;
                    _planVisited[next]=plan.stamp;_planCosts[next]=cost;_planParents[next]=cell;float h=RouteHeuristic(plan,next);Push(plan.heap,new SeaNode(next,cost+h,h));
                }
            }
        }
    }
}
