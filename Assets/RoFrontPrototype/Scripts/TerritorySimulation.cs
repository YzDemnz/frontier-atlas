using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    public enum ArmyState { Idle, Marching, Fighting, NoTroops, Arrived, Blocked, Eliminated, Sailing, Anchored }
    public enum OrderResult { Accepted, Paused, Water, OwnTerritory, NoTroops, NoLandRoute, Eliminated, NoCoastalAccess, NoFreeLand, NotAdjacent, Protected, Allied, OperationLimit }
    // Canonical local rules: no scene objects, renderers, input, UI, or currency.
    public sealed partial class TerritorySimulation
    {
        public const int Neutral=-1, Ocean=-2;
        public const float NeutralTroopCost=3,OccupationTroopCost=1,MarchInterval=.35f,BattleLossPerSecond=36;
        public readonly WorldGrid World;
        public readonly int FactionCount,PlayerFaction;
        public readonly int[] Owners,TerritoryCounts,CapitalCells,Targets,AttackFactions;
        public readonly int[] LastAdvanceCells;
        public event Action<int,int,int> TerritoryCaptured;
        public event Action<int,int,float> BattleOccurred;
        public readonly float[] Troops,CommittedTroops,LastCombat;
        public readonly ArmyState[] States;
        public readonly Color[] Colors;
        public readonly string[] Names;
        public readonly MatchOptions Options;
        public bool Changed {get;private set;}
        public bool Paused,AiEnabled=true,ReinforcementsEnabled=true;
        public float ElapsedTime {get;private set;}
        private readonly Queue<int>[] _routes;
        private readonly HashSet<int>[] _frontiers;
        private readonly float[] _aiTimers;
        private readonly CampaignRandom _random;
        private readonly int[] _parents,_visited,_search;
        private int _searchId;
        private float _tick;
        public float NeutralSectorCost=>NeutralTroopCost/World.CellAreaFactor;
        public float OccupationSectorCost=>OccupationTroopCost/World.CellAreaFactor;
        private float TickInterval=>MarchInterval/World.CellAreaFactor;

        public TerritorySimulation(WorldGrid world,int seed,int playerFaction=0,MatchOptions options=null)
        {
            Options=options;World=world;FactionCount=world.Countries.Length+(options!=null&&options.customPlayer?1:0)+(options!=null?Mathf.Clamp(options.tribeCount,0,options.mode==MatchMode.QuickMatch?QuickTribes:40):0);
            PlayerFaction=options!=null&&options.customPlayer?world.Countries.Length:Mathf.Clamp(playerFaction,0,FactionCount-1);
            Owners=new int[world.Land.Length];TerritoryCounts=new int[FactionCount];CapitalCells=new int[FactionCount];
            Array.Fill(CapitalCells,-1);Array.Copy(world.StartCells,CapitalCells,world.StartCells.Length);
            if(options!=null&&options.customPlayer)CapitalCells[PlayerFaction]=-1;SelectQuickNations();
            Targets=new int[FactionCount];AttackFactions=new int[FactionCount];Troops=new float[FactionCount];
            LastAdvanceCells=new int[FactionCount];
            CommittedTroops=new float[FactionCount];LastCombat=new float[FactionCount];States=new ArmyState[FactionCount];
            Colors=new Color[FactionCount];Names=new string[FactionCount];_routes=new Queue<int>[FactionCount];
            _frontiers=new HashSet<int>[FactionCount];_aiTimers=new float[FactionCount];_random=new CampaignRandom(seed);
            _parents=new int[Owners.Length];_visited=new int[Owners.Length];_search=new int[Owners.Length];
            for(int id=0;id<Owners.Length;id++)Owners[id]=world.Land[id]?Neutral:Ocean;
            for(int f=0;f<FactionCount;f++)
            {
                _routes[f]=new Queue<int>();_frontiers[f]=new HashSet<int>();Troops[f]=400;LastCombat[f]=-100;
                Targets[f]=AttackFactions[f]=-1;_aiTimers[f]=3+(float)_random.NextDouble()*22;Names[f]=f<world.Countries.Length?world.Countries[f].name:f==PlayerFaction?options.playerName:IsQuickMatch?QuickTribeName(f):"Tribu "+(f-world.Countries.Length);
                Colors[f]=f==PlayerFaction?new Color(.22f,.84f,.86f):Color.HSVToRGB((f*.61803399f+.08f)%1,.55f,.9f);
                if(IsTribe(f))
                {
                    for(int attempt=0;attempt<10000;attempt++){int candidate=_random.Next(Owners.Length);if(world.Land[candidate]&&Owners[candidate]==Neutral){CapitalCells[f]=candidate;break;}}
                }
                int capital=CapitalCells[f];
                LastAdvanceCells[f]=capital;
                if(capital<0)continue;
                float radius=world.ResolutionScale*1.15f;int extent=Mathf.CeilToInt(radius);
                for(int dy=-extent;dy<=extent;dy++)for(int dx=-extent;dx<=extent;dx++)
                {
                    int x=capital%world.Width+dx,y=capital/world.Width+dy;
                    if(dx*dx+dy*dy>radius*radius||!world.IsLand(x,y))continue;
                    int n=world.At(x,y);if((f>=world.Countries.Length||world.GeographicCountries[n]==f)&&Owners[n]==Neutral)SetInitialOwner(n,f);
                }
            }
            for(int id=0;id<Owners.Length;id++)if(Owners[id]>=0)RefreshFrontier(id);
            InitializeNavigation();
            InitializeFreeExpansion();
            InitializeCampaign();
            Changed=true;
        }
        public bool CanFoundAt(int cell)=>cell>=0&&cell<Owners.Length&&World.Land[cell]&&!IsImpassable(cell)&&(!CampaignEnabled||!IsIrradiated(cell))&&Owners[cell]==Neutral&&TerritoryCounts[PlayerFaction]==0;
        public bool FoundPlayer(int cell,string name)
        {
            if(!CanFoundAt(cell)||string.IsNullOrWhiteSpace(name))return false;
            int f=PlayerFaction;Names[f]=name.Trim().Length>24?name.Trim().Substring(0,24):name.Trim();CapitalCells[f]=LastAdvanceCells[f]=cell;
            int radius=Mathf.CeilToInt(World.ResolutionScale*1.15f);
            for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
            {
                int x=cell%World.Width+dx,y=cell/World.Width+dy;
                if(!World.IsLand(x,y)||dx*dx+dy*dy>radius*radius)continue;
                int id=World.At(x,y);if(Owners[id]!=Neutral)continue;SetInitialOwner(id,f);TerritoryCaptured?.Invoke(f,id,Neutral);
            }
            for(int dy=-radius-1;dy<=radius+1;dy++)for(int dx=-radius-1;dx<=radius+1;dx++)
            {int x=cell%World.Width+dx,y=cell/World.Width+dy;if(World.InBounds(x,y))RefreshFrontier(World.At(x,y));}
            Changed=true;CampaignFounded();return true;
        }
        private void SetInitialOwner(int id,int f){Owners[id]=f;TerritoryCounts[f]++;}
        public void ClearChanged()=>Changed=false;
        public float ReinforcementRate(int f)=>CampaignEnabled?CampaignRegeneration(f):TerritoryCounts[f]>0? .7f+Mathf.Sqrt(TerritoryCounts[f]/World.CellAreaFactor)*.12f:0;
        public bool HasOrder(int f)=>Targets[f]>=0;
        public void CancelOrder(int f)
        {if(CampaignEnabled){CancelCampaignOperations(f);return;}_routes[f].Clear();if(FleetActive!=null){FleetActive[f]=false;_seaRoutes[f].Clear();}AttackFactions[f]=Targets[f]=-1;CommittedTroops[f]=0;States[f]=ArmyState.Idle;}
        private void EndOrder(int f,ArmyState state){CancelOrder(f);States[f]=state;}

        public OrderResult IssueOrder(int f,Vector2Int target,float fraction=1)
        {
            if(CampaignEnabled)
            {
                if(!World.InBounds(target.x,target.y))return OrderResult.Water;int point=World.At(target.x,target.y);
                return World.Land[point]&&IsBorderTarget(f,point)?AttackBorder(f,point,fraction):InvadeBySea(f,point,fraction);
            }
            if(Paused)return OrderResult.Paused;if(TerritoryCounts[f]==0)return OrderResult.Eliminated;
            if(!World.InBounds(target.x,target.y))return OrderResult.Water;
            if(!World.IsLand(target.x,target.y))return IssueSeaOrder(f,World.At(target.x,target.y),fraction);
            int cell=World.At(target.x,target.y);if(Owners[cell]==f)return OrderResult.OwnTerritory;
            float committed=Mathf.Floor(Troops[f]*Mathf.Clamp01(fraction)*World.CellAreaFactor)/World.CellAreaFactor;
            if(committed<(Owners[cell]==Neutral?NeutralSectorCost:OccupationSectorCost))return OrderResult.NoTroops;
            var route=FindRoute(f,cell);if(route==null)return IssueSeaOrder(f,cell,fraction);
            if(Owners[cell]==Neutral)return BeginExpansion(f,fraction);
            FleetActive[f]=false;
            _routes[f]=route;Targets[f]=cell;AttackFactions[f]=Owners[cell];CommittedTroops[f]=committed;
            SeedFreeBoundary(f);
            States[f]=ArmyState.Marching;return OrderResult.Accepted;
        }
        private Queue<int> FindRoute(int f,int target)
        {
            int stamp=++_searchId,head=0,tail=0,reached=-1;_search[tail++]=target;_visited[target]=stamp;_parents[target]=-1;
            while(head<tail&&reached<0)
            {
                int id=_search[head++];if(Owners[id]==f){reached=id;break;}
                World.ForEachNeighbor(id,n=>{if(!World.Land[n]||_visited[n]==stamp)return;_visited[n]=stamp;_parents[n]=id;_search[tail++]=n;});
            }
            if(reached<0)return null;var route=new Queue<int>();
            for(int id=_parents[reached];id>=0;id=_parents[id])route.Enqueue(id);
            return route;
        }
        private bool IsAdjacent(int id,int f)
        {bool adjacent=false;World.ForEachNeighbor(id,n=>{if(Owners[n]==f)adjacent=true;});return adjacent;}
        private void RefreshFrontier(int id)
        {
            int owner=Owners[id];if(owner<0)return;
            _frontiers[owner].Remove(id);
            bool border=false;World.ForEachNeighbor(id,n=>{if(World.Land[n]&&Owners[n]!=owner)border=true;});
            if(border)_frontiers[owner].Add(id);
        }
        private bool Capture(int f,int id)
        {
            if(Paused||!World.Land[id]||Owners[id]==f||(!IsAdjacent(id,f)&&!CanLand(f,id)&&!CanCampaignLand(f,id)))return false;
            int previous=Owners[id];if(previous>=0&&AvailableTroops(previous)>.001f&&!_captureContested)return false;
            if(CampaignEnabled&&(IsImpassable(id)||!CanAttackNation(f,previous)))return false;
            float cost=_captureCostOverride>=0?_captureCostOverride:(previous==Neutral?NeutralSectorCost:OccupationSectorCost)*TerrainResistance(id);
            if(Troops[f]+.001f<cost||CommittedTroops[f]+.001f<cost)return false;
            Troops[f]=Mathf.Max(0,Troops[f]-cost);CommittedTroops[f]=Mathf.Max(0,CommittedTroops[f]-cost);
            if(previous>=0){TerritoryCounts[previous]--;if(TerritoryCounts[previous]==0){Troops[previous]=0;EndOrder(previous,ArmyState.Eliminated);}}
            if(previous>=0)_frontiers[previous].Remove(id);
            Owners[id]=f;TerritoryCounts[f]++;LastAdvanceCells[f]=id;
            AdvanceFreeBoundary(f,id);
            RefreshFrontier(id);World.ForEachNeighbor(id,RefreshFrontier);Changed=true;
            CampaignCaptured(f,id,previous);TerritoryCaptured?.Invoke(f,id,previous);return true;
        }
        private int NextNeutralFront(int faction)
            =>SelectFreeBoundary(faction);
        private int NextEnemyBorder(int f,int enemy)
        {
            int best=-1,goal=Targets[f];float score=float.PositiveInfinity;
            foreach(int edge in _frontiers[f])World.ForEachNeighbor(edge,n=>
            {if(Owners[n]!=enemy)return;float d=Mathf.Abs(n%World.Width-goal%World.Width)+Mathf.Abs(n/World.Width-goal/World.Width);if(d<score){score=d;best=n;}});
            return best;
        }
        private void AdvanceArmy(int f)
        {
            if(!HasOrder(f)||FleetActive[f])return;CommittedTroops[f]=Mathf.Min(CommittedTroops[f],Troops[f]);
            if(CommittedTroops[f]<OccupationSectorCost){EndOrder(f,ArmyState.NoTroops);return;}
            int cell;
            if(AttackFactions[f]<0)
            {
                cell=NextNeutralFront(f);
                if(cell<0){EndOrder(f,ArmyState.Arrived);return;}
                Targets[f]=cell;
            }
            else
            {
                while(_routes[f].Count>0&&Owners[_routes[f].Peek()]==f)_routes[f].Dequeue();
                if(_routes[f].Count==0)
                {
                    int enemy=AttackFactions[f],next=TerritoryCounts[enemy]>0?NextEnemyBorder(f,enemy):-1;
                    if(next<0){EndOrder(f,ArmyState.Arrived);return;}_routes[f].Enqueue(next);
                }
                cell=_routes[f].Peek();
                if(Owners[cell]==Neutral)
                {
                    int broadFront=NextNeutralFront(f);
                    if(broadFront>=0)cell=broadFront;
                }
            }
            if(!IsAdjacent(cell,f)){EndOrder(f,ArmyState.Blocked);return;}
            int defender=Owners[cell];
            if(defender>=0&&AvailableTroops(defender)>.001f)
            {
                float losses=Mathf.Min(BattleLossPerSecond*TickInterval,Mathf.Min(CommittedTroops[f],AvailableTroops(defender)));
                Troops[f]=Mathf.Max(0,Troops[f]-losses);CommittedTroops[f]=Mathf.Max(0,CommittedTroops[f]-losses);
                Troops[defender]=Mathf.Max(0,Troops[defender]-losses);CommittedTroops[defender]=Mathf.Min(CommittedTroops[defender],Troops[defender]);
                LastCombat[f]=LastCombat[defender]=ElapsedTime;States[f]=ArmyState.Fighting;
                LastAdvanceCells[f]=cell;BattleOccurred?.Invoke(f,defender,losses);
                if(CommittedTroops[f]<.001f)EndOrder(f,ArmyState.NoTroops);return;
            }
            if(!Capture(f,cell)){EndOrder(f,ArmyState.NoTroops);return;}
            if(AttackFactions[f]>=0&&_routes[f].Count>0&&_routes[f].Peek()==cell)_routes[f].Dequeue();States[f]=ArmyState.Marching;
        }
        private void Think(int f)
        {
            if(TerritoryCounts[f]==0||Troops[f]<35||HasOrder(f))return;
            int target=-1;float best=float.NegativeInfinity;
            foreach(int edge in _frontiers[f])World.ForEachNeighbor(edge,n=>
            {
                int owner=Owners[n];if(!World.Land[n]||owner==f)return;
                if(owner>=0&&Troops[f]<Troops[owner]*1.25f+25)return;
                float score=(owner==Neutral?100:0)+(float)_random.NextDouble()*60;
                if(score>best){best=score;target=n;}
            });
            if(target<0){ThinkNaval(f);return;}
            if(Owners[target]==Neutral)for(int step=0;step<5;step++)
            {
                int next=target;World.ForEachNeighbor(target,n=>{if(Owners[n]==Neutral&&_random.NextDouble()<.45)next=n;});target=next;
            }
            IssueOrder(f,new Vector2Int(target%World.Width,target/World.Width),.85f);
        }
        public void Step(float dt)
        {
            if(Paused||dt<=0)return;if(CampaignEnabled){AdvanceCampaign(dt);return;}ElapsedTime+=dt;
            AdvanceFleets(dt);
            for(int f=0;f<FactionCount;f++)
            {
                if(ReinforcementsEnabled&&ElapsedTime-LastCombat[f]>15)Troops[f]=Mathf.Min(2000,Troops[f]+ReinforcementRate(f)*dt);
                if(!AiEnabled||f==PlayerFaction)continue;_aiTimers[f]-=dt;
                if(_aiTimers[f]<=0){_aiTimers[f]=7+(float)_random.NextDouble()*8;Think(f);}
            }
            _tick+=dt;while(_tick>=TickInterval){_tick-=TickInterval;for(int f=0;f<FactionCount;f++)AdvanceArmy(f);}
        }
    }
}
