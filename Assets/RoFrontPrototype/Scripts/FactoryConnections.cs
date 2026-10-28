using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private readonly Dictionary<long,List<Building>> _factoryTargets=new Dictionary<long,List<Building>>();
        private readonly Dictionary<int,int> _pendingTradeOrigins=new Dictionary<int,int>();
        private int _factoryTargetCount=-1,_factoryGridStep,_factoryPreviewRevision;
        private bool FactoryDestination(Building b)=>b.kind==BuildingKind.City||b.kind==BuildingKind.Port||b.kind==BuildingKind.Factory;
        private bool TradeLandAccessible(int owner,int cell)=>World.Land[cell]&&!IsImpassable(cell)&&(Owners[cell]==Neutral||Owners[cell]==owner||CanTrade(owner,Owners[cell]));
        private bool TradeLandSurface(int cell)=>World.Land[cell]&&!IsImpassable(cell);
        private Queue<int> DirectTradePath(int owner,int start,int goal)
        {
            int sx=start%World.Width,sy=start/World.Width,dx=goal%World.Width-sx,dy=goal/World.Width-sy;
            if(World.WrapLongitude&&Mathf.Abs(dx)>World.Width/2)dx+=dx>0?-World.Width:World.Width;
            int steps=Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dy)),previous=start;var path=new Queue<int>();
            for(int n=1;n<=steps;n++)
            {
                int x=sx+Mathf.RoundToInt(dx*n/(float)steps),y=sy+Mathf.RoundToInt(dy*n/(float)steps);if(World.WrapLongitude)x=(x%World.Width+World.Width)%World.Width;int cell=World.At(x,y);if(!TradeLandSurface(cell))return null;
                if(x!=previous%World.Width&&y!=previous/World.Width)
                {int corner=World.At(x,previous/World.Width);if(!TradeLandSurface(corner)){corner=World.At(previous%World.Width,y);if(!TradeLandSurface(corner))return null;}path.Enqueue(corner);}
                path.Enqueue(cell);previous=cell;
            }
            return path;
        }
        private static long FactoryGridKey(int x,int y)=>((long)x<<32)|(uint)y;
        private List<Building> FactoryTargetsNear(int cell)
        {
            int step=Mathf.Max(1,Mathf.CeilToInt(FactoryConnectionRadius));
            if(_factoryTargetCount!=Buildings.Count||_factoryGridStep!=step)
            {
                _factoryTargetCount=Buildings.Count;_factoryGridStep=step;_factoryTargets.Clear();
                foreach(var b in Buildings)if(FactoryDestination(b)){long key=FactoryGridKey(b.cell%World.Width/step,b.cell/World.Width/step);if(!_factoryTargets.TryGetValue(key,out var bin))_factoryTargets[key]=bin=new List<Building>();bin.Add(b);}
            }
            var result=new List<Building>();var seen=new HashSet<long>();int gx=cell%World.Width/step,gy=cell/World.Width/step,columns=Mathf.CeilToInt(World.Width/(float)step);
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {int x=gx+dx;if(World.WrapLongitude)x=(x%columns+columns)%columns;long key=FactoryGridKey(x,gy+dy);if(!seen.Add(key)||!_factoryTargets.TryGetValue(key,out var bin))continue;result.AddRange(bin);}return result;
        }
        private int QueueFactoryConnections(Building factory,int budget)
        {
            var candidates=FactoryTargetsNear(factory.cell);
            candidates.RemoveAll(to=>to.id==factory.id||!to.Operational||!CanTrade(factory.owner,to.owner)||!LandConnected(factory.cell,to.cell)||LandStationDistance(factory.cell,to.cell)>FactoryConnectionRadius+.01f||_tradeByPair.ContainsKey(TradeKey(factory.id,to.id))||_tradePairsPlanning.Contains(TradeKey(factory.id,to.id))||(_failedTradeUntil.TryGetValue(TradeKey(factory.id,to.id),out float due)&&due>ElapsedTime));
            candidates.Sort((a,b)=>LandStationDistance(factory.cell,a.cell).CompareTo(LandStationDistance(factory.cell,b.cell)));
            int planned=0,limit=factory.owner==PlayerFaction?16:12;foreach(var to in candidates){_pendingTradeOrigins.TryGetValue(OriginKey(factory.id,false),out int pending);if(pending>=4||planned>=budget||_tradePairsPlanning.Count>=limit)break;QueueTradeConnection(factory,to.id,false,factory.cell,to.cell);planned++;}return planned;
        }
        private void QueueTradeConnection(Building origin,int destination,bool naval,int start,int goal)
        {
            int key=OriginKey(origin.id,naval),owner=origin.owner;long pair=TradeKey(origin.id,destination,naval);
            _tradePlanning.Add(key);_tradePairsPlanning.Add(pair);_pendingTradeOrigins.TryGetValue(key,out int pending);_pendingTradeOrigins[key]=pending+1;
            Action<Queue<int>> completed=route=>
            {
                int remaining=_pendingTradeOrigins[key]-1;if(remaining<=0){_pendingTradeOrigins.Remove(key);_tradePlanning.Remove(key);}else _pendingTradeOrigins[key]=remaining;
                _tradePairsPlanning.Remove(pair);_tradeRefresh=Mathf.Min(_tradeRefresh,.25f);
                if(route!=null&&RouteStation(origin)&&origin.owner==owner){var path=new List<int>{start};path.AddRange(route);AddTradeRoute(origin,destination,naval,path);}
                if(!_tradeByPair.ContainsKey(pair)){if(_failedTradeUntil.Count>4096)_failedTradeUntil.Clear();_failedTradeUntil[pair]=ElapsedTime+3;}
            };
            if(naval)PlanSeaRoute(start,goal,completed,owner==PlayerFaction);else PlanLandRoute(owner,start,goal,completed);
        }
        private bool PhysicalTradeRouteValid(TradeRoute route)
        {
            var from=BuildingById(route.fromBuilding);var to=BuildingById(route.toBuilding);
            if(!RouteStation(from)||from.owner!=route.owner||!RouteStation(to)||route.path==null||route.path.Count<2)return false;
            if(LandStationDistance(from.cell,to.cell)>FactoryConnectionRadius+.01f)return false;
            return TradePathDataFor(route).surface;
        }
    }
}
