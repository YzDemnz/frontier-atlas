using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private readonly Dictionary<int,float> _hostilityUntil=new Dictionary<int,float>();
        private struct TradePermission{public bool allowed;public float until;}
        private readonly Dictionary<int,TradePermission> _tradePermissions=new Dictionary<int,TradePermission>();
        private int RelationKey(int a,int b)=>Mathf.Min(a,b)*FactionCount+Mathf.Max(a,b);
        public bool IsHostile(int a,int b)=>a>=0&&b>=0&&a!=b&&(IsDeclaredWar(a,b)||(_hostilityUntil.TryGetValue(RelationKey(a,b),out float until)&&until>ElapsedTime));
        private void RecordHostility(int a,int b)
        {
            if(a<0||b<0||a==b)return;int key=RelationKey(a,b);bool already=IsHostile(a,b);_hostilityUntil[key]=ElapsedTime+Rules.tradeWarCooldown;
            if(!already){_tradePermissions.Remove(key);_tradeRefresh=0;}
        }
        public bool CanTrade(int a,int b)
        {
            if(a<0||b<0||a>=FactionCount||b>=FactionCount)return false;if(a==b)return true;
            if(TerritoryCounts[a]==0||TerritoryCounts[b]==0||IsTribe(a)||IsTribe(b))return false;int key=RelationKey(a,b);
            if(_tradePermissions.TryGetValue(key,out var cache)&&cache.until>ElapsedTime)return cache.allowed;
            bool allowed=TradeActivity(a,b)>0&&!Embargoed(a,b)&&!IsDeclaredWar(a,b)&&(HasCompact(a,b,CompactKind.FreeTrade)||AreAllied(a,b)||(Rules.peacefulNeighborTrade&&!IsHostile(a,b)&&BetrayalUntil[a]<=ElapsedTime&&BetrayalUntil[b]<=ElapsedTime));
            _tradePermissions[key]=new TradePermission{allowed=allowed,until=ElapsedTime+.5f};return allowed;
        }
    }
}
