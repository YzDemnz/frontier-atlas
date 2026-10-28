using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public bool[] FleetActive {get;private set;}
        public int[] FleetCells {get;private set;}
        public int[] FleetNext {get;private set;}
        public float[] FleetProgress {get;private set;}
        public event Action<int> FleetLaunched, Landed;
        private Queue<int>[] _seaRoutes;
        private int[] _landingCells;
        private float[] _sailClock;
        public bool HasCoastalAccess(int faction)
        {if(FleetActive[faction])return true;if(CampaignEnabled){foreach(int cell in _owned[faction])if(AdjacentWater(cell)>=0)return true;return false;}foreach(int cell in World.CoastCells)if(Owners[cell]==faction)return true;return false;}
        public bool IsNavalThreat(int attacker,int defender)
        {
            if(CampaignEnabled){foreach(var operation in Operations)if(operation.active&&operation.faction==attacker&&operation.kind==OperationKind.Naval&&((operation.target>=0&&Owners[operation.target]==defender)||(operation.landing>=0&&Owners[operation.landing]==defender)))return true;return false;}
            if(attacker==defender||!FleetActive[attacker])return false;
            int target=Targets[attacker],beach=_landingCells[attacker];
            return (target>=0&&Owners[target]==defender)||(beach>=0&&Owners[beach]==defender);
        }
        private void InitializeNavigation()
        {
            InitializeWaterComponents();
            FleetActive=new bool[FactionCount];FleetCells=new int[FactionCount];FleetNext=new int[FactionCount];
            FleetProgress=new float[FactionCount];_seaRoutes=new Queue<int>[FactionCount];
            _landingCells=new int[FactionCount];_sailClock=new float[FactionCount];
            for(int f=0;f<FactionCount;f++){_seaRoutes[f]=new Queue<int>();FleetCells[f]=FleetNext[f]=_landingCells[f]=-1;}
        }
        private float Distance(int a,int b)
        {
            int dx=Math.Abs(a%World.Width-b%World.Width);if(World.WrapLongitude)dx=Math.Min(dx,World.Width-dx);
            return dx+Math.Abs(a/World.Width-b/World.Width);
        }
        private bool CanLand(int f,int cell)
        {
            if(FleetActive==null||!FleetActive[f]||_landingCells[f]!=cell)return false;
            for(int d=0;d<4;d++)if(World.Neighbor(cell,d)==FleetCells[f])return true;
            return false;
        }
        private OrderResult IssueSeaOrder(int f,int target,float fraction)
        {
            float committed=FleetActive[f]?Mathf.Min(Troops[f],CommittedTroops[f]):PreviewTroops(f,fraction);
            if(committed<NeutralSectorCost)return OrderResult.NoTroops;
            int landing=-1,destination=target;
            if(World.Land[target])
            {
                float best=float.PositiveInfinity;int region=World.GeographicCountries[target];
                foreach(int coast in World.CoastCells)
                {
                    if(Owners[coast]==f)continue;
                    float score=Distance(coast,target)+(region>=0&&World.GeographicCountries[coast]!=region?World.Width:0);
                    if(score<best){best=score;landing=coast;}
                }
                if(landing<0)return OrderResult.NoLandRoute;
                destination=-1;
                for(int d=0;d<4;d++){int n=World.Neighbor(landing,d);if(n>=0&&!World.Land[n]){destination=n;break;}}
                if(destination<0)return OrderResult.NoLandRoute;
            }
            int origin=FleetActive[f]?FleetCells[f]:-1;
            if(origin<0)
            {
                float best=float.PositiveInfinity;
                foreach(int coast in World.CoastCells)
                {
                    if(Owners[coast]!=f)continue;
                    for(int d=0;d<4;d++)
                    {
                        int n=World.Neighbor(coast,d);if(n<0||World.Land[n])continue;
                        float score=Distance(n,destination);if(score<best){best=score;origin=n;}
                    }
                }
            }
            if(origin<0)return OrderResult.NoCoastalAccess;
            var route=SeaRoute(origin,destination);if(route==null)return OrderResult.NoLandRoute;
            _routes[f].Clear();_seaRoutes[f]=route;_landingCells[f]=landing;Targets[f]=target;
            AttackFactions[f]=World.Land[target]?Owners[target]:-1;CommittedTroops[f]=committed;
            FleetCells[f]=origin;FleetNext[f]=route.Count>0?route.Peek():origin;
            FleetProgress[f]=_sailClock[f]=0;FleetActive[f]=true;States[f]=ArmyState.Sailing;
            FleetLaunched?.Invoke(f);return OrderResult.Accepted;
        }
        // A* shares reusable grid arrays with land searches; no allocations per visited cell.
        private Queue<int> SeaRoute(int start,int goal)=>FindNaturalSeaRoute(start,goal);
        private struct SeaNode {public int cell;public float score,tie;public SeaNode(int c,float s,float t=0){cell=c;score=s;tie=t;}}
        private static bool Before(SeaNode a,SeaNode b)=>a.score<b.score||(a.score==b.score&&a.tie<=b.tie);
        private static void Push(List<SeaNode> h,SeaNode value)
        {int i=h.Count;h.Add(value);while(i>0){int p=(i-1)/2;if(Before(h[p],value))break;h[i]=h[p];i=p;}h[i]=value;}
        private static SeaNode Pop(List<SeaNode> h)
        {
            SeaNode first=h[0],last=h[h.Count-1];h.RemoveAt(h.Count-1);if(h.Count==0)return first;
            int i=0;while(i*2+1<h.Count){int c=i*2+1;if(c+1<h.Count&&Before(h[c+1],h[c]))c++;if(Before(last,h[c]))break;h[i]=h[c];i=c;}h[i]=last;return first;
        }
        private void AdvanceFleets(float dt)
        {
            for(int f=0;f<FactionCount;f++)
            {
                if(!FleetActive[f])continue;
                CommittedTroops[f]=Mathf.Min(CommittedTroops[f],Troops[f]);
                if(CommittedTroops[f]<OccupationSectorCost){EndOrder(f,ArmyState.NoTroops);continue;}
                AdvanceNavalMotion(ref FleetCells[f],ref FleetNext[f],ref _sailClock[f],ref FleetProgress[f],_seaRoutes[f],dt,.8f*World.ResolutionScale);
                if(_seaRoutes[f].Count>0)continue;
                int beach=_landingCells[f];
                if(beach<0){States[f]=ArmyState.Anchored;_sailClock[f]=0;continue;}
                int defender=Owners[beach];
                if(defender>=0&&defender!=f&&AvailableTroops(defender)>.001f)
                {
                    float loss=Mathf.Min(BattleLossPerSecond*dt,Mathf.Min(CommittedTroops[f],AvailableTroops(defender)));
                    Troops[f]-=loss;CommittedTroops[f]-=loss;Troops[defender]-=loss;
                    LastCombat[f]=LastCombat[defender]=ElapsedTime;LastAdvanceCells[f]=beach;States[f]=ArmyState.Fighting;
                    BattleOccurred?.Invoke(f,defender,loss);continue;
                }
                if(Owners[beach]!=f&&!Capture(f,beach)){EndOrder(f,ArmyState.NoTroops);continue;}
                int goal=Targets[f];float fraction=CommittedTroops[f]/Mathf.Max(.001f,Troops[f]);
                FleetActive[f]=false;CommittedTroops[f]=0;Landed?.Invoke(f);
                if(Owners[goal]==f)EndOrder(f,ArmyState.Arrived);
                else if(IssueOrder(f,new Vector2Int(goal%World.Width,goal/World.Width),fraction)!=OrderResult.Accepted)EndOrder(f,ArmyState.Arrived);
            }
        }
        private void ThinkNaval(int f)
        {
            if(_random.NextDouble()>.25)return;
            int best=-1;float distance=float.PositiveInfinity;
            foreach(int coast in World.CoastCells)
            {
                if(Owners[coast]==f)continue;
                int owner=Owners[coast];if(owner>=0&&Troops[f]<Troops[owner]*1.25f+25)continue;
                float d=Distance(CapitalCells[f],coast);if(d<distance){distance=d;best=coast;}
            }
            if(best>=0)IssueSeaOrder(f,best,.85f);
        }
    }
}
