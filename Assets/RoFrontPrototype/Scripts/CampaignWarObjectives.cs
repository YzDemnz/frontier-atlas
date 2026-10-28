using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private sealed class StrategicFrontCache{public readonly Queue<int> cells=new Queue<int>();public float until;}
        private readonly Dictionary<long,StrategicFrontCache> _strategicFronts=new Dictionary<long,StrategicFrontCache>();
        private int StrategicContact(int f,int enemy,int fallback)
        {
            EnsureCampaignRelationIndex();_activeWarByPair.TryGetValue(RelationKey(f,enemy),out var war);if(war==null||!war.active)return fallback>=0&&Owners[fallback]==enemy&&IsAdjacent(fallback,f)&&!IsImpassable(fallback)?fallback:ContactCell(f,enemy);
            int goal=war.goalCell;if(war.objective==WarObjective.Capital||goal<0||goal>=Owners.Length)goal=CapitalCells[enemy];
            // The selected infrastructure cell is the saved war goal, not a global building scan per captured tile.
            if(f!=PlayerFaction&&Options.difficulty==Difficulty.Brutal&&Operations.Exists(o=>o.active&&o.faction==f&&o.enemy==enemy&&o.target==fallback))goal=fallback;
            if(_strategicFronts.Count>4096)_strategicFronts.Clear();long key=((long)f*FactionCount+enemy)*Owners.Length+goal;
            if(_strategicFronts.TryGetValue(key,out var cache)&&cache.until>ElapsedTime)
            {while(cache.cells.Count>0){int cell=cache.cells.Peek();if(Owners[cell]==enemy&&IsAdjacent(cell,f)&&!IsImpassable(cell))return cell;cache.cells.Dequeue();}}
            else cache=new StrategicFrontCache();cache.until=ElapsedTime+1;cache.cells.Clear();
            var candidates=new List<KeyValuePair<float,int>>(17);
            if(_contacts.TryGetValue(f*FactionCount+enemy,out var front))for(int inspected=0,count=System.Math.Min(128,front.cells.Count);inspected<count;inspected++)
            {int cell=front.cells.Dequeue();if(Owners[cell]!=enemy||!IsAdjacent(cell,f)||IsImpassable(cell)){front.known.Remove(cell);continue;}front.cells.Enqueue(cell);float score=Distance(cell,goal);int index=0;while(index<candidates.Count&&candidates[index].Key<=score)index++;if(index>=16)continue;candidates.Insert(index,new KeyValuePair<float,int>(score,cell));if(candidates.Count>16)candidates.RemoveAt(16);}
            foreach(var item in candidates)cache.cells.Enqueue(item.Value);_strategicFronts[key]=cache;return cache.cells.Count>0?cache.cells.Peek():ContactCell(f,enemy);
        }
    }
}
