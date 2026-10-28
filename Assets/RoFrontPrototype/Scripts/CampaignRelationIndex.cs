using System.Collections.Generic;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private readonly Dictionary<int,uint> _compactIndex=new Dictionary<int,uint>();private readonly HashSet<int> _warIndex=new HashSet<int>(),_embargoIndex=new HashSet<int>();private float _relationIndexUntil=-1;
        private readonly Dictionary<int,CampaignWar> _activeWarByPair=new Dictionary<int,CampaignWar>();
        private void EnsureCampaignRelationIndex()
        {
            if(ElapsedTime<_relationIndexUntil)return;_relationIndexUntil=ElapsedTime+.5f;_compactIndex.Clear();_warIndex.Clear();_embargoIndex.Clear();
            foreach(var c in Affairs.compacts)if(Active(c)){int key=RelationKey(c.source,c.target);_compactIndex.TryGetValue(key,out uint flags);_compactIndex[key]=flags|(1u<<(int)c.kind);}
            _activeWarByPair.Clear();foreach(var war in Affairs.wars)if(war.active){int key=RelationKey(war.attacker,war.defender);_warIndex.Add(key);_activeWarByPair[key]=war;}
            foreach(var e in Affairs.embargoes)if(e.active&&(e.endsDay<0||e.endsDay>CampaignDay))_embargoIndex.Add(RelationKey(e.source,e.target));
        }
    }
}
