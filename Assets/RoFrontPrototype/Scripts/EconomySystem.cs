using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public enum ConstructionState { Pending, Constructing, Operational, Upgrading, Destroyed }
    public sealed class Building
    {
        public int id,owner,cell,level=1,ammunition;public BuildingKind kind;public float health,completeAt,nextAction,nextReload;public bool destroyed,upgrading;
        public double costPaid;
        public ConstructionState State=>destroyed?ConstructionState.Destroyed:upgrading?ConstructionState.Upgrading:completeAt>0?ConstructionState.Constructing:ConstructionState.Operational;
        public bool Operational=>!destroyed&&completeAt<=0;
    }
    public sealed class TradeRoute
    {
        public int id,owner,fromBuilding,toBuilding,deliveries;public bool naval,reverse,docking;public List<int> path;public float clock,duration,disabledUntil,dockUntil;public double reward;public float[] distances;public float length;
        public float Progress=>duration>0?Mathf.Clamp01(clock/duration):0;
    }
    public sealed partial class TerritorySimulation
    {
        public readonly List<Building> Buildings=new List<Building>();
        public readonly List<TradeRoute> TradeRoutes=new List<TradeRoute>();
        private int _buildingId,_tradeId;private float _tradeRefresh;
        public int TradeNetworkVersion {get;private set;}
        private readonly Dictionary<int,Building> _buildingsById=new Dictionary<int,Building>();
        private readonly Dictionary<long,TradeRoute> _tradeByPair=new Dictionary<long,TradeRoute>();
        private readonly Dictionary<int,int> _originRouteCounts=new Dictionary<int,int>();
        // Land links are undirected; sea links have an independent namespace.
        private static long TradeKey(int from,int to,bool naval=false)=>naval?((long)(uint)from<<32)|(uint)~to:((long)(uint)Mathf.Min(from,to)<<32)|(uint)Mathf.Max(from,to);
        private static int OriginKey(int id,bool naval)=>id*2+(naval?1:0);
        private readonly HashSet<long> _tradePairsPlanning=new HashSet<long>();
        private readonly Dictionary<long,float> _failedTradeUntil=new Dictionary<long,float>();
        private int _tradeCursor;
        private readonly List<Building> _tradeOrigins=new List<Building>();private int _tradeOriginCount=-1;
        private static bool EconomicBuilding(Building b)=>b.kind==BuildingKind.Factory||b.kind==BuildingKind.City||b.kind==BuildingKind.Port;
        private readonly HashSet<int> _tradePlanning=new HashSet<int>();
        private readonly Dictionary<int,Building> _buildingCells=new Dictionary<int,Building>();
        private readonly Dictionary<int,List<Building>> _outposts=new Dictionary<int,List<Building>>();
        public Building BuildingAt(int cell)=>_buildingCells.TryGetValue(cell,out var building)&&!building.destroyed?building:null;
        public double ConstructionPrice(int f,BuildingKind kind,int cell=-1)
        {
            var definition=Rules.Building(kind);var existing=cell>=0?BuildingAt(cell):null;
            if(existing!=null&&existing.owner==f&&existing.kind==kind)return Math.Min(definition.maxCost,definition.cost*Math.Pow(2,existing.level));
            int count=0;bool shared=Rules.balanceProfile==BalanceProfile.OpenFront34Reference||Rules.sharedEconomicPriceCounter;
            foreach(var b in Buildings)if(!b.destroyed&&b.owner==f&&(b.Operational||(Rules.balanceProfile!=BalanceProfile.OpenFront34Reference&&Rules.priceCountsPending))&&(b.kind==kind||(shared&&(kind==BuildingKind.Factory||kind==BuildingKind.Port)&&(b.kind==BuildingKind.Factory||b.kind==BuildingKind.Port))))count++;
            if(!ReferenceBalance)return Math.Min(definition.maxCost,definition.cost*(1+count*.3));
            if(kind==BuildingKind.Silo)return definition.cost;
            if(kind==BuildingKind.Outpost||kind==BuildingKind.SAM)return Math.Min(definition.maxCost,definition.cost*(count+1));
            double initial=Rules.balanceProfile==BalanceProfile.OpenFront34Reference&&kind==BuildingKind.Port?125000:definition.cost;return Math.Min(definition.maxCost,initial*Math.Pow(2,Math.Min(count,30)));
        }
        public CampaignAction ValidateConstruction(int f,int cell,BuildingKind kind)
        {
            if(!CampaignEnabled)return CampaignAction.Fail("La economÃ­a no estÃ¡ activa en esta partida.");
            if(Paused||Outcome!=CampaignOutcome.Playing)return CampaignAction.Fail("Reanuda la partida para construir.");
            if(cell<0||cell>=Owners.Length||Owners[cell]!=f||IsImpassable(cell)||IsIrradiated(cell))return CampaignAction.Fail("Construye sobre tu tierra, libre de obstÃ¡culos y radiaciÃ³n.");
            var definition=Rules.Building(kind);var existing=BuildingAt(cell);
            if(existing!=null)
            {if(existing.owner!=f||existing.kind!=kind)return CampaignAction.Fail("Ya hay otro edificio aquÃ­.");if(!existing.Operational)return CampaignAction.Fail("La construcciÃ³n o mejora sigue en curso.");if(existing.level>=5)return CampaignAction.Fail("El edificio alcanzÃ³ su nivel mÃ¡ximo.");}
            else
            {
                if(definition.coastal&&AdjacentWater(cell)<0)return CampaignAction.Fail("El puerto debe colocarse junto al mar.");
                foreach(var b in Buildings)if(!b.destroyed&&Distance(b.cell,cell)<Rules.buildingSpacing)return CampaignAction.Fail("Deja espacio entre los edificios.");
            }
            if(Gold[f]+.001<ConstructionPrice(f,kind,cell))return CampaignAction.Fail("No hay suficiente oro.");
            return CampaignAction.Ok(existing==null?"ConstrucciÃ³n disponible.":"Mejora disponible.");
        }
        public CampaignAction Construct(int f,int cell,BuildingKind kind)
        {
            var result=ValidateConstruction(f,cell,kind);if(!result.success)return result;
            double paid=ConstructionPrice(f,kind,cell);Gold[f]-=paid;Ledgers[f].constructionSpent+=paid;var b=BuildingAt(cell);
            if(b!=null){b.upgrading=true;b.completeAt=ElapsedTime+Rules.buildSeconds*(1+b.level*.25f);}
            else{b=new Building{id=++_buildingId,owner=f,cell=cell,kind=kind,health=Rules.Building(kind).health,completeAt=ElapsedTime+Rules.buildSeconds};Buildings.Add(b);_buildingCells[cell]=b;_buildingsById[b.id]=b;if(kind==BuildingKind.Outpost){if(!_outposts.TryGetValue(f,out var list)){list=new List<Building>();_outposts[f]=list;}list.Add(b);}}
            b.costPaid+=paid;_factoryPreviewRevision++;return CampaignAction.Ok(b.upgrading?"Mejora en curso.":"ConstrucciÃ³n en curso.");
        }
        private void RefreshTerritorialCapacity(int f)
        {if(IsQuickMatch){Capacity[f]=QuickCapacity(f);return;}if(ReferenceBalance&&!IsTribe(f))Capacity[f]=TacticalBalance?TacticalCapacity(f):CampaignBalance.Capacity(NormalizedArea(f),_cityLevels[f],CityBonus);}
        private void RecomputeCapacity(int f)
        {
            _recruitmentUntil=0;
            float capacity=IsTribe(f)?Rules.tribeTroops*3:Rules.baseCapacity;
            foreach(var b in Buildings)if(b.owner==f&&b.Operational&&b.kind==BuildingKind.City)capacity+=b.level*CityBonus;
            Capacity[f]=capacity;
            _cityLevels[f]=Mathf.Max(0,(capacity-(IsTribe(f)?Rules.tribeTroops*3:Rules.baseCapacity))/CityBonus);RefreshTerritorialCapacity(f);
        }
        private void CaptureInfrastructure(int f,int cell)
        {
            var b=BuildingAt(cell);if(b==null||b.owner==f)return;int old=b.owner;_factoryPreviewRevision++;
            if(Rules.Building(b.kind).destroyOnCapture){DestroyBuilding(b,f);return;}
            b.owner=f;RecomputeCapacity(old);RecomputeCapacity(f);_tradeRefresh=Mathf.Min(_tradeRefresh,.5f);
            if(b.kind==BuildingKind.Outpost){if(_outposts.TryGetValue(old,out var previous))previous.Remove(b);if(!_outposts.TryGetValue(f,out var next)){next=new List<Building>();_outposts[f]=next;}next.Add(b);}
            Emit(StrategicEventKind.Information,f,old,cell,Rules.Building(b.kind).name+" capturada.");
        }
        public void DestroyBuilding(Building b,int responsible)
        {
            if(b.destroyed)return;_factoryPreviewRevision++;b.destroyed=true;_buildingCells.Remove(b.cell);if(responsible>=0)Statistics[responsible].buildingsDestroyed++;RecomputeCapacity(b.owner);_tradeRefresh=Mathf.Min(_tradeRefresh,.5f);
        }
        public bool OutpostAt(int f,int cell)
        {
            if(!CampaignEnabled||f<0)return false;
            if(!_outposts.TryGetValue(f,out var outposts))return false;foreach(var b in outposts)if(b.owner==f&&b.Operational&&Distance(b.cell,cell)<=Rules.Building(b.kind).radius*(1+(b.level-1)*.15f))return true;return false;
        }
        private double[] _incomeCache;private float _incomeCacheUntil;private int _incomeCacheVersion=-1;
        public double IncomeRate(int f)
        {
            if(!CampaignEnabled||TerritoryCounts[f]==0||IsTribe(f))return 0;
            if(_incomeCache==null||_incomeCacheUntil<=ElapsedTime||_incomeCacheVersion!=TradeNetworkVersion)
            {
                if(_incomeCache==null)_incomeCache=new double[FactionCount];System.Array.Clear(_incomeCache,0,_incomeCache.Length);
                for(int nation=0;nation<FactionCount;nation++)if(TerritoryCounts[nation]>0&&!IsTribe(nation))_incomeCache[nation]=PassiveIncome(nation);
                foreach(var route in TradeRoutes)
                {
                    if(route.disabledUntil>ElapsedTime||TacticalBalance&&!route.naval)continue;double rate=route.reward/System.Math.Max(1,route.duration+(route.naval?Rules.portDwellSeconds:1));_incomeCache[route.owner]+=rate;
                    var to=BuildingById(route.toBuilding);if(to!=null&&to.owner!=route.owner)_incomeCache[to.owner]+=rate*(route.naval?(Rules.payBothPortOwners?1:0):Rules.landDestinationRewardShare);
                }
                if(TacticalBalance)foreach(var factory in Buildings)if(factory.kind==BuildingKind.Factory&&factory.Operational)_incomeCache[factory.owner]+=FactoryEstimatedIncome(factory);
                _incomeCacheUntil=ElapsedTime+.5f;_incomeCacheVersion=TradeNetworkVersion;
            }
            return _incomeCache[f];
        }
        private List<int> TradePath(int f,int start,int goal,bool naval)
        {
            if(naval){var sea=SeaRoute(start,goal);if(sea==null)return null;var list=new List<int>{start};list.AddRange(sea);return list;}
            int stamp=++_searchId;var heap=new List<SeaNode>();_visited[start]=stamp;_parents[start]=-1;_search[start]=0;Push(heap,new SeaNode(start,Distance(start,goal)));
            int iterations=0;while(heap.Count>0&&iterations++<300000)
            {
                int id=Pop(heap).cell;if(id==goal){var reverse=new List<int>();for(int p=goal;p>=0;p=_parents[p])reverse.Add(p);reverse.Reverse();return reverse;}
                for(int d=0;d<4;d++)
                {
                    int n=World.Neighbor(id,d);if(n<0)continue;
                    if(naval?World.Land[n]:!World.Land[n]||IsImpassable(n)||(Owners[n]!=f&&(Owners[n]<0||!AreAllied(f,Owners[n]))))continue;
                    int cost=_search[id]+1;if(_visited[n]==stamp&&cost>=_search[n])continue;
                    _visited[n]=stamp;_search[n]=cost;_parents[n]=id;Push(heap,new SeaNode(n,cost+Distance(n,goal)*1.001f));
                }
            }
            return null;
        }
        private bool RouteValid(TradeRoute route)
        {
            var from=BuildingById(route.fromBuilding);var to=route.toBuilding>0?BuildingById(route.toBuilding):null;
            if(!RouteStation(from)||from.owner!=route.owner)return false;
            if(to!=null&&(!RouteStation(to)||!AreAlliedOrSame(route.owner,to.owner)))return false;
            if(to==null&&Owners[CapitalCells[route.owner]]!=route.owner)return false;
            if(TacticalBalance&&!route.naval&&to!=null&&LandStationDistance(from.cell,to.cell)>FactoryConnectionRadius+.01f)return false;
            if(!route.naval&&!TradePathAccessible(route,route.owner))return false;
            return true;
        }
        private bool AreAlliedOrSame(int a,int b)=>CanTrade(a,b);
        private void IndexTradeRoute(TradeRoute route,int delta)
        {
            int key=OriginKey(route.fromBuilding,route.naval);_originRouteCounts.TryGetValue(key,out int n);_originRouteCounts[key]=Mathf.Max(0,n+delta);
            if(!route.naval&&route.toBuilding>0){key=OriginKey(route.toBuilding,false);_originRouteCounts.TryGetValue(key,out n);_originRouteCounts[key]=Mathf.Max(0,n+delta);}
        }
        private void RefreshTrade()
        {
            if(_tradeOriginCount!=Buildings.Count){_tradeOriginCount=Buildings.Count;_tradeOrigins.Clear();_tradeOrigins.AddRange(Buildings);_tradeOrigins.Sort((a,b)=>{int first=(a.kind==BuildingKind.Factory?0:1).CompareTo(b.kind==BuildingKind.Factory?0:1);return first!=0?first:a.id.CompareTo(b.id);});}
            for(int i=TradeRoutes.Count-1;i>=0;i--)if(TacticalBalance&&!TradeRoutes[i].naval?!PhysicalTradeRouteValid(TradeRoutes[i]):!RouteValid(TradeRoutes[i]))
            {var invalid=TradeRoutes[i];_tradeByPair.Remove(TradeKey(invalid.fromBuilding,invalid.toBuilding,invalid.naval));IndexTradeRoute(invalid,-1);ForgetTradePath(invalid.id);TradeRoutes.RemoveAt(i);TradeNetworkVersion++;}
            int slots=Buildings.Count*2,planned=0;
            for(int scanned=0;scanned<slots&&planned<8&&_tradePairsPlanning.Count<16;scanned++)
            {
                int slot=_tradeCursor++%slots;var from=_tradeOrigins[slot/2];bool naval=(slot&1)!=0;
                if(!from.Operational||TerritoryCounts[from.owner]==0||!EconomicBuilding(from)||(naval&&from.kind!=BuildingKind.Port))continue;
                if(TacticalBalance&&!naval){if(from.kind==BuildingKind.Factory)planned+=QueueFactoryConnections(from,8-planned);continue;}
                int originKey=OriginKey(from.id,naval),desired=Rules.balanceProfile==BalanceProfile.LegacyPrototype?1:naval?Rules.portConnections:Rules.factoryConnections;
                _originRouteCounts.TryGetValue(originKey,out int count);if(_tradePlanning.Contains(originKey)||count>=desired||_tradePairsPlanning.Count>=(from.owner==PlayerFaction?16:12))continue;
                int destination=-1,destinationId=0;float best=naval?Rules.seaTradeRange:FactoryConnectionRadius;
                if(!naval&&Rules.allowCapitalTrade&&!_tradeByPair.ContainsKey(TradeKey(from.id,0))&&CapitalCells[from.owner]>=0&&Owners[CapitalCells[from.owner]]==from.owner&&Distance(from.cell,CapitalCells[from.owner])<best)
                {destination=CapitalCells[from.owner];best=Distance(from.cell,destination);}
                foreach(var to in Buildings)
                {
                    long pair=TradeKey(from.id,to.id,naval);
                    if(to.id==from.id||_tradeByPair.ContainsKey(pair)||_tradePairsPlanning.Contains(pair)||!to.Operational||!AreAlliedOrSame(from.owner,to.owner)||(naval?to.kind!=BuildingKind.Port:!EconomicBuilding(to)))continue;
                    if(_failedTradeUntil.TryGetValue(pair,out float retry)&&retry>ElapsedTime)continue;
                    if(naval&&((!Rules.allowDomesticSeaTrade||Rules.balanceProfile==BalanceProfile.OpenFront34Reference)&&from.owner==to.owner))continue;
                    if(naval&&!WaterConnected(AdjacentWater(from.cell),AdjacentWater(to.cell)))continue;
                    float d=naval?Distance(from.cell,to.cell):LandStationDistance(from.cell,to.cell);if(!naval&&d<FactoryConnectionMinimum)continue;bool preferCity=!naval&&from.kind==BuildingKind.Factory&&to.kind==BuildingKind.City&&(destinationId==0||BuildingById(destinationId).kind!=BuildingKind.City);bool keepCity=!naval&&from.kind==BuildingKind.Factory&&destinationId>0&&BuildingById(destinationId).kind==BuildingKind.City&&to.kind!=BuildingKind.City;if(d<(naval?Rules.seaTradeRange:FactoryConnectionRadius)&&(preferCity||d<best&&!keepCity)){best=d;destination=to.cell;destinationId=to.id;}
                }
                if(destination<0||destination==from.cell)continue;int start=naval?AdjacentWater(from.cell):from.cell,goal=naval?AdjacentWater(destination):destination;
                if(start<0||goal<0)continue;int dest=destinationId,owner=from.owner;long pairKey=TradeKey(from.id,dest,naval);var origin=from;
                QueueTradeConnection(origin,dest,naval,start,goal);planned++;
            }
            _tradeRefresh=Mathf.Min(_tradeRefresh,planned>=8?.5f:2);
            foreach(var route in TradeRoutes){if(route.distances==null)PrepareTradePath(route);var origin=BuildingById(route.fromBuilding);if(origin!=null)route.reward=ComputeTradeReward(origin,route.toBuilding,route.naval);}
        }
        private Building BuildingById(int id)=>_buildingsById.TryGetValue(id,out var b)?b:null;
        private double ComputeTradeReward(Building from,int destination,bool naval)
        {var to=BuildingById(destination);return naval&&Rules.distanceBasedSeaReward&&to!=null?CampaignBalance.MaritimeReward(Distance(from.cell,to.cell)/(Rules.normalizeTerritoryArea?World.ResolutionScale:1))*from.level*AIMultiplier(from.owner):Rules.tradeReward*from.level*(naval?2:1)*AIMultiplier(from.owner);}
        private void AddTradeRoute(Building from,int destination,bool naval,List<int> path)
        {
            long key=TradeKey(from.id,destination,naval);if(_tradeByPair.ContainsKey(key))return;

            var route=new TradeRoute{id=++_tradeId,owner=from.owner,fromBuilding=from.id,toBuilding=destination,naval=naval,path=path,reward=ComputeTradeReward(from,destination,naval)};
            PrepareTradePath(route);if(TacticalBalance&&!naval?!PhysicalTradeRouteValid(route):!RouteValid(route))return;TradeRoutes.Add(route);_tradeByPair[key]=route;IndexTradeRoute(route,1);TradeNetworkVersion++;
        }
        private void AdvanceEconomy(float dt)
        {
            EnsureTradePathIndex();
            for(int f=0;f<FactionCount;f++)if(TerritoryCounts[f]>0&&!IsTribe(f)){double income=PassiveIncome(f)*dt;CreditGold(f,income,GoldFlow.Passive);}
            foreach(var b in Buildings)
            {
                if(b.destroyed||b.completeAt<=0||b.completeAt>ElapsedTime)continue;
                _factoryPreviewRevision++;if(b.upgrading){b.level++;b.upgrading=false;}b.completeAt=0;b.health=Rules.Building(b.kind).health*b.level;RecomputeCapacity(b.owner);_tradeRefresh=0;
                if(b.kind==BuildingKind.Silo){b.ammunition=b.level+1;b.nextReload=ElapsedTime+Rules.siloCooldown;}
                Emit(StrategicEventKind.BuildingComplete,b.owner,-1,b.cell,Rules.Building(b.kind).name+" lista. Nivel "+b.level+".");
            }
            _tradeRefresh-=dt;if(_tradeRefresh<=0){_tradeRefresh=8;RefreshTrade();}
            foreach(var route in TradeRoutes)
            {
                if(route.disabledUntil>ElapsedTime||TacticalBalance&&!route.naval)continue;
                if(route.docking){if(ElapsedTime>=route.dockUntil){route.docking=false;route.clock=0;route.reverse=!route.reverse;}continue;}
                route.clock+=dt;if(route.clock<route.duration)continue;if(!RouteValid(route)){route.disabledUntil=ElapsedTime+8;_tradeRefresh=0;continue;}route.clock=route.duration;
                route.docking=true;var partner=BuildingById(route.toBuilding);float activity=partner!=null?TradeActivity(route.owner,partner.owner):1;
                route.dockUntil=ElapsedTime+(route.naval?Rules.portDwellSeconds:1)+(route.duration+(route.naval?Rules.portDwellSeconds:1))*(1/Mathf.Max(.025f,activity)-1);
                route.deliveries++;
                CreditGold(route.owner,route.reward,route.naval?GoldFlow.SeaTrade:GoldFlow.LandTrade);
                var receiver=BuildingById(route.toBuilding);if(receiver!=null&&receiver.owner!=route.owner){if(route.naval&&Rules.payBothPortOwners)CreditGold(receiver.owner,route.reward,GoldFlow.SeaTrade);else if(!route.naval)CreditGold(receiver.owner,route.reward*Rules.landDestinationRewardShare,GoldFlow.LandTrade);}
            }
            AdvanceTradePreviews();AdvanceIndustrialFleet(dt);
        }
        private int DevelopmentCell(int f,BuildingKind kind)
        {
            int choice=-1,seen=0;foreach(int cell in _owned[f])
            {
                if(BuildingAt(cell)!=null||IsIrradiated(cell))continue;if(kind==BuildingKind.Port&&AdjacentWater(cell)<0)continue;
                if(_random.Next(++seen)==0)choice=cell;if(seen>150)break;
            }
            return choice;
        }
        private void ThinkDevelopment(int f)
        {
            if(IsTribe(f))return;
            if(!IsQuickMatch&&Options.difficulty==Difficulty.Brutal&&ThinkStrategicDevelopment(f))return;if(IsQuickMatch)ThinkQuickDiplomacy(f);
            int cities=0,factories=0;bool hasPort=false;if(TacticalBalance)foreach(var b in Buildings){if(b.owner!=f||b.destroyed)continue;if(b.kind==BuildingKind.City)cities++;else if(b.kind==BuildingKind.Factory)factories++;else if(b.kind==BuildingKind.Port)hasPort=true;}
            BuildingKind kind=TacticalBalance&&(cities==0||factories>0&&cities<factories*3)?BuildingKind.City:!Buildings.Exists(b=>b.owner==f&&b.kind==BuildingKind.Factory&&!b.destroyed)?BuildingKind.Factory:
                Troops[f]>Capacity[f]*.8f?BuildingKind.City:_random.NextDouble()<.25?BuildingKind.Outpost:_random.NextDouble()<.3?BuildingKind.Port:BuildingKind.Factory;
            if(TacticalBalance&&cities>0&&factories>0&&!hasPort&&HasCoastalAccess(f)&&_random.NextDouble()<.35)kind=BuildingKind.Port;
            if(Gold[f]>3000000&&_random.NextDouble()<.2)kind=BuildingKind.SAM;
            else if(Gold[f]>1800000&&_random.NextDouble()<.2)kind=BuildingKind.Silo;
            int cell=DevelopmentCell(f,kind);if(cell>=0)Construct(f,cell,kind);
            if(Buildings.Exists(b=>b.owner==f&&b.kind==BuildingKind.Port&&b.Operational)&&Gold[f]>Rules.warshipCost*1.5&&_random.NextDouble()<.3)
            {var port=Buildings.Find(b=>b.owner==f&&b.kind==BuildingKind.Port&&b.Operational);DeployWarship(f,port.cell);}
            if(Options.difficulty!=Difficulty.Relaxed&&Gold[f]>2500000&&_random.NextDouble()<.12)
            {
                int enemy=-1;foreach(int ranked in new PowerBalance(this).Ranking)if(ranked!=f&&CanAttackNation(f,ranked)&&TerritoryCounts[ranked]>0){enemy=ranked;break;}
                if(enemy>=0&&!NuclearFriendlyRisk(f,CapitalCells[enemy],Rules.Weapon(WeaponKind.Atomic).radius))LaunchWeapon(f,CapitalCells[enemy],WeaponKind.Atomic);
            }
        }
    }
}
