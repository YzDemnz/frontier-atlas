using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private sealed class TradePathData
        {public int obstacles=-1;public bool surface,ownersDirty=true;public Bounds bounds;public readonly HashSet<int> owners=new HashSet<int>(),chunks=new HashSet<int>();}
        private readonly Dictionary<int,TradePathData> _tradePaths=new Dictionary<int,TradePathData>();
        private readonly Dictionary<int,HashSet<int>> _tradeChunkRoutes=new Dictionary<int,HashSet<int>>();
        private readonly HashSet<int> _tradeChangedChunks=new HashSet<int>();private bool _tradeIndexReady;
        private int TradeChunk(int cell)=>cell%World.Width/32+(cell/World.Width/32)*((World.Width+31)/32);
        private void TradeOwnershipChanged(int cell)=>_tradeChangedChunks.Add(TradeChunk(cell));
        private TradePathData TradePathDataFor(TradeRoute route)
        {
            if(!_tradePaths.TryGetValue(route.id,out var data))
            {
                data=new TradePathData();_tradePaths[route.id]=data;bool first=true;
                foreach(int cell in route.path)
                {if(cell<0||cell>=Owners.Length)continue;var point=new Vector3(cell%World.Width+.5f,0,cell/World.Width+.5f);if(first){data.bounds=new Bounds(point,Vector3.zero);first=false;}else data.bounds.Encapsulate(point);int chunk=TradeChunk(cell);if(data.chunks.Add(chunk)){if(!_tradeChunkRoutes.TryGetValue(chunk,out var ids))_tradeChunkRoutes[chunk]=ids=new HashSet<int>();ids.Add(route.id);}}
                data.bounds.Expand(new Vector3(2,64*World.ResolutionScale,2));
            }
            if(data.obstacles!=Obstacles.Count)
            {data.obstacles=Obstacles.Count;data.surface=true;foreach(int cell in route.path)if(cell<0||cell>=Owners.Length||!TradeLandSurface(cell)){data.surface=false;break;}}
            return data;
        }
        private void EnsureTradePathIndex()
        {
            if(!_tradeIndexReady){_tradeIndexReady=true;foreach(var route in TradeRoutes)if(!route.naval)TradePathDataFor(route);}
            if(_tradeChangedChunks.Count==0)return;
            foreach(int chunk in _tradeChangedChunks)if(_tradeChunkRoutes.TryGetValue(chunk,out var routes))foreach(int id in routes)if(_tradePaths.TryGetValue(id,out var data))data.ownersDirty=true;
            _tradeChangedChunks.Clear();
        }
        private bool TradePathAccessible(TradeRoute route,int owner)
        {
            EnsureTradePathIndex();var data=TradePathDataFor(route);if(!data.surface)return false;
            if(data.ownersDirty){data.ownersDirty=false;data.owners.Clear();foreach(int cell in route.path)data.owners.Add(Owners[cell]);}
            foreach(int other in data.owners)if(other!=Neutral&&other!=owner&&(other<0||!CanTrade(owner,other)))return false;return true;
        }
        private void ForgetTradePath(int id)
        {if(!_tradePaths.TryGetValue(id,out var data))return;foreach(int chunk in data.chunks)if(_tradeChunkRoutes.TryGetValue(chunk,out var ids))ids.Remove(id);_tradePaths.Remove(id);}
        public Bounds TradeRouteBounds(TradeRoute route)=>TradePathDataFor(route).bounds;
        private void ResetTradePathIndex(){_tradePaths.Clear();_tradeChunkRoutes.Clear();_tradeChangedChunks.Clear();_tradeIndexReady=false;}
    }
}
