using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class CommercialTrain
    {public int id,owner,producer,routeId,from,to,stops;public float clock,dockUntil;public bool reverse,docking;public List<int> visited=new List<int>();}
    public sealed class TradeDelivery
    {public int train,producer,origin,destination,payer,receiver;public float time;public double ownerGold,receiverGold;}
    public readonly struct TrainVisualState
    {public readonly int id;public readonly Vector3 position,direction;public readonly bool docking;public TrainVisualState(int i,Vector3 p,Vector3 d,bool dock){id=i;position=p;direction=d;docking=dock;}}
    public sealed partial class TerritorySimulation
    {
        public readonly List<CommercialTrain> Trains=new List<CommercialTrain>();
        public readonly List<TradeDelivery> TradeDeliveries=new List<TradeDelivery>();
        private readonly Dictionary<int,float> _factoryDispatchAt=new Dictionary<int,float>();private int _trainId;
        private readonly Dictionary<int,List<TradeRoute>> _stationRoutes=new Dictionary<int,List<TradeRoute>>();
        private readonly Dictionary<int,TradeRoute> _landRoutesById=new Dictionary<int,TradeRoute>();private int _stationVersion=-1;
        private int[] _factoryLevelCache;private float _factoryLevelUntil;
        private float LandStationDistance(int a,int b)=>TacticalBalance?SeaDistance(a,b):Distance(a,b);
        public float FactoryConnectionRadius=>TacticalBalance?Rules.factoryConnectionRadius*World.ResolutionScale:Rules.landTradeRange;
        private float FactoryConnectionMinimum=>0;
        public float FactoryConnectionPathLimit=>float.PositiveInfinity;
        private static bool RouteStation(Building b)=>b!=null&&!b.destroyed&&(b.Operational||b.upgrading);
        private void RebuildStationNetwork()
        {
            if(_stationVersion==TradeNetworkVersion)return;_stationVersion=TradeNetworkVersion;_stationRoutes.Clear();_landRoutesById.Clear();
            foreach(var route in TradeRoutes)if(!route.naval)
            { _landRoutesById[route.id]=route;foreach(int station in new[]{route.fromBuilding,route.toBuilding})if(station>0){if(!_stationRoutes.TryGetValue(station,out var links))_stationRoutes[station]=links=new List<TradeRoute>();links.Add(route);} }
        }
        public int FactoryLevels(int owner)
        {
            if(_factoryLevelCache==null||_factoryLevelUntil<=ElapsedTime)
            {if(_factoryLevelCache==null)_factoryLevelCache=new int[FactionCount];Array.Clear(_factoryLevelCache,0,_factoryLevelCache.Length);foreach(var b in Buildings)if(RouteStation(b)&&b.kind==BuildingKind.Factory)_factoryLevelCache[b.owner]+=b.level;_factoryLevelUntil=ElapsedTime+.5f;}
            return _factoryLevelCache[owner];
        }
        public static float IndustrialSaturation(int active,int midpoint=560,int tail=900)
        {float a=1/(1+Mathf.Exp(Mathf.Clamp((active-midpoint)*Mathf.Log(2)/100,-40,40))),b=.25f/(1+Mathf.Exp(Mathf.Clamp((active-tail)*Mathf.Log(2)/150,-40,40)));return Mathf.Max(.0001f,Mathf.Max(a,b));}
        public float FactoryDispatchInterval(Building factory,int prospectiveLevel=0)
        {
            int level=prospectiveLevel>0?prospectiveLevel:factory.level,levels=FactoryLevels(factory.owner)+level-factory.level;float saturation=IndustrialSaturation(Trains.Count,Rules.trainSaturationMidpoint,Rules.trainSaturationTail);
            if(Rules.factoryProductionProfile==FactoryProductionProfile.Atlas)return Mathf.Max(Rules.trainMinimumDispatchSeconds,Rules.factoryDispatchSeconds*(1+Mathf.Max(0,levels-1)*.04f)/((1+(level-1)*Rules.factoryLevelProductionBonus)*Mathf.Max(.1f,saturation)));
            if(Rules.factoryProductionProfile==FactoryProductionProfile.HistoricalProbability)return (levels+10)*20*.1f/Mathf.Max(1,level);
            float boost=levels>0?1+.5f*Mathf.Clamp01((6-levels)/5f)/levels:1;
            float denominator=Mathf.Max(1,Mathf.Floor((levels+10)*15/(saturation*boost)));
            return .1f/Mathf.Max(.000001f,1-Mathf.Pow(1-1/denominator,level));
        }
        public int ConnectionsFor(int building)
        {RebuildStationNetwork();return _stationRoutes.TryGetValue(building,out var links)?links.Count:0;}
        public string FactoryStatus(Building factory)
        {
            if(!factory.Operational)return factory.upgrading?"Mejorando Â· producciÃ³n en pausa":"En construcciÃ³n";
            if(ConnectionsFor(factory.id)>0)return "Conectada";
            if(_tradePlanning.Contains(OriginKey(factory.id,false)))return "Buscando conexiÃ³n";
            foreach(var other in Buildings)if(other.id!=factory.id&&other.Operational&&EconomicBuilding(other)&&LandStationDistance(factory.cell,other.cell)<=FactoryConnectionRadius)return "Buscando ruta terrestre disponible";
            return "Aislada";
        }
        private bool TrainCanUse(TradeRoute route,int owner,bool settle=false)
        {
            if(route==null||route.disabledUntil>ElapsedTime)return false;
            return RouteValid(route)&&(owner==route.owner||TradePathAccessible(route,owner));
        }
        private TradeRoute ChooseTrainRoute(int owner,int station,int previous,List<int> visited)
        {
            if(!_stationRoutes.TryGetValue(station,out var links))return null;TradeRoute chosen=null;int seen=0;bool foundNew=false;
            foreach(var route in links)
            {int next=route.fromBuilding==station?route.toBuilding:route.fromBuilding;var target=BuildingById(next);if(target==null||!RouteStation(target)||!CanTrade(owner,target.owner)||!TrainCanUse(route,owner))continue;bool fresh=next!=previous&&(visited==null||!visited.Contains(next));if(foundNew&&!fresh)continue;if(fresh&&!foundNew){chosen=null;seen=0;foundNew=true;}if(_random.Next(++seen)==0)chosen=route;}
            return chosen;
        }
        private void PutTrainOnRoute(CommercialTrain train,TradeRoute route,int station)
        {train.routeId=route.id;train.from=station;train.to=route.fromBuilding==station?route.toBuilding:route.fromBuilding;train.reverse=route.fromBuilding!=station;train.clock=0;train.docking=false;}
        public double TrainStopReward(int owner,int destination)
        {
            var station=BuildingById(destination);if(station==null||!station.Operational||!CanTrade(owner,station.owner))return 0;
            bool team=Options.teams&&Teams[owner]==Teams[station.owner];
            return (station.owner==owner?Rules.trainOwnReward:team?Rules.trainNeutralReward:AreAllied(owner,station.owner)?Rules.trainAlliedReward:Rules.trainNeutralReward)*AIMultiplier(owner);
        }
        public double FactoryEstimatedIncome(Building factory)
        {
            RebuildStationNetwork();if(!factory.Operational||!_stationRoutes.TryGetValue(factory.id,out var links)||links.Count==0)return 0;double rewards=0;int count=0;
            foreach(var route in links)if(TrainCanUse(route,factory.owner)){rewards+=TrainStopReward(factory.owner,route.fromBuilding==factory.id?route.toBuilding:route.fromBuilding);count++;}
            return count>0?rewards/count/Math.Max(.1,FactoryDispatchInterval(factory)):0;
        }
        private void AdvanceIndustrialFleet(float dt)
        {
            if(!TacticalBalance)return;RebuildStationNetwork();
            foreach(var factory in Buildings)
            {
                if(factory.kind!=BuildingKind.Factory||!factory.Operational||Trains.Count>=Rules.maximumLogicalTrains)continue;
                if(_factoryDispatchAt.TryGetValue(factory.id,out float due)&&ElapsedTime<due)continue;
                if(Rules.factoryProductionProfile!=FactoryProductionProfile.Atlas)
                {float mean=FactoryDispatchInterval(factory);if(_random.NextDouble()>Mathf.Clamp01(dt/Mathf.Max(dt,mean)))continue;}
                var route=ChooseTrainRoute(factory.owner,factory.id,-1,null);if(route==null){_factoryDispatchAt[factory.id]=ElapsedTime+1;continue;}
                var train=new CommercialTrain{id=++_trainId,owner=factory.owner,producer=factory.id};train.visited.Add(factory.id);PutTrainOnRoute(train,route,factory.id);Trains.Add(train);
                var destination=BuildingById(route.toBuilding==factory.id?route.fromBuilding:route.toBuilding);float activity=destination==null?1:TradeActivity(factory.owner,destination.owner);
                _factoryDispatchAt[factory.id]=ElapsedTime+(Rules.factoryProductionProfile==FactoryProductionProfile.Atlas?FactoryDispatchInterval(factory):Rules.trainMinimumDispatchSeconds)/Mathf.Max(.025f,activity);
            }
            for(int i=Trains.Count-1;i>=0;i--)
            {
                var train=Trains[i];var producer=BuildingById(train.producer);var to=BuildingById(train.to);var from=BuildingById(train.from);
                if(producer==null||producer.destroyed||producer.owner!=train.owner||!_landRoutesById.TryGetValue(train.routeId,out var route)||!TrainCanUse(route,train.owner)||!RouteStation(to)||!RouteStation(from)) {Trains.RemoveAt(i);continue;}
                // Upgrades retain routes and vehicles, with production and settlement paused.
                if(!producer.Operational||!to.Operational||!from.Operational)continue;
                if(train.docking)
                {if(ElapsedTime<train.dockUntil)continue;if(train.stops>=Rules.maximumTrainStops){Trains.RemoveAt(i);continue;}var next=ChooseTrainRoute(train.owner,train.to,train.from,train.visited);if(next==null){Trains.RemoveAt(i);continue;}PutTrainOnRoute(train,next,train.to);continue;}
                train.clock+=dt;if(train.clock<route.duration)continue;if(!TrainCanUse(route,train.owner,true)){Trains.RemoveAt(i);continue;}train.clock=route.duration;double reward=TrainStopReward(train.owner,train.to);if(reward<=0){Trains.RemoveAt(i);continue;}
                double other=to.owner!=train.owner?reward*Rules.landDestinationRewardShare:0;CreditGold(train.owner,reward,GoldFlow.LandTrade);if(other>0)CreditGold(to.owner,other,GoldFlow.LandTrade);
                TradeDeliveries.Add(new TradeDelivery{train=train.id,producer=train.producer,origin=train.from,destination=train.to,payer=train.owner,receiver=to.owner,time=ElapsedTime,ownerGold=reward,receiverGold=other});if(TradeDeliveries.Count>1000)TradeDeliveries.RemoveRange(0,TradeDeliveries.Count-1000);
                route.deliveries++;train.stops++;train.visited.Add(train.to);train.docking=true;train.dockUntil=ElapsedTime+1;
            }
        }
        public IEnumerable<TrainVisualState> RenderableTrains()
        {
            if(TacticalBalance){RebuildStationNetwork();foreach(var train in Trains)if(_landRoutesById.TryGetValue(train.routeId,out var route)){var p=TradePathPosition(route,Mathf.Clamp01(train.clock/Mathf.Max(.01f,route.duration)),train.reverse,out var d);yield return new TrainVisualState(train.id,p,d,train.docking);}}
            else foreach(var route in TradeRoutes)if(!route.naval&&route.disabledUntil<=ElapsedTime){var p=TradePosition(route,out var d);yield return new TrainVisualState(route.id,p,d,route.docking);}
        }
    }
}
