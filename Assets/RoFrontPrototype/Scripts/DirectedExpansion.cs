using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private sealed class DirectedFront
        {
            public Vector2 origin,direction;public int target,primaryCaptured,otherCaptured;
            public double primaryWeight,otherWeight,credit;
            public readonly List<SeaNode> primary=new List<SeaNode>(),other=new List<SeaNode>();
            public bool PreferPrimary=>credit+primaryWeight>=primaryWeight+otherWeight;
        }
        private DirectedFront[] _directedFronts;
        public OrderResult BeginDirectionalExpansion(int faction,int target,float fraction=1)
        {
            if(target<0||target>=Owners.Length||!World.Land[target])return OrderResult.Water;
            if(Owners[target]==faction)return OrderResult.OwnTerritory;
            if(Owners[target]!=Neutral)return OrderResult.NotAdjacent;
            var result=BeginExpansion(faction,fraction);if(result!=OrderResult.Accepted)return result;
            ConfigureDirectedFront(faction,target);return result;
        }
        private float WrappedDeltaX(float difference)
        {if(World.WrapLongitude&&Mathf.Abs(difference)>World.Width*.5f)difference+=difference>0?-World.Width:World.Width;return difference;}
        private void ConfigureDirectedFront(int faction,int target)
        {
            float reference=CapitalCells[faction]>=0?CapitalCells[faction]%World.Width:target%World.Width;Vector2 sum=Vector2.zero;int count=0;
            // One perimeter pass per player command; normal captures stay incremental.
            foreach(int cell in _frontiers[faction]){sum+=new Vector2(reference+WrappedDeltaX(cell%World.Width-reference),cell/World.Width);count++;}
            Vector2 origin=count>0?sum/count:new Vector2(reference,target/World.Width);
            Vector2 vector=new Vector2(WrappedDeltaX(target%World.Width-origin.x),target/World.Width-origin.y);
            if(vector.sqrMagnitude<.01f)return;
            double main=Math.Max(0,Rules.directionalExpansionWeight),other=Math.Max(0,Rules.otherExpansionWeight);if(main+other<=0)return;
            var state=new DirectedFront{origin=origin,direction=vector.normalized,target=target,primaryWeight=main,otherWeight=other,credit=other};_directedFronts[faction]=state;
            RebuildDirectedHeaps(faction,state);
        }
        private bool InPrimarySector(DirectedFront state,int cell)
        {
            Vector2 delta=new Vector2(WrappedDeltaX(cell%World.Width-state.origin.x),cell/World.Width-state.origin.y);
            float edge=TacticalBalance?(Mathf.PerlinNoise((state.origin.x+delta.x)*.055f/World.ResolutionScale,(cell/World.Width+factionSeed(state))*.055f/World.ResolutionScale)-.5f)*.26f:0;
            return delta.sqrMagnitude>.001f&&Vector2.Dot(delta.normalized,state.direction)>=Rules.directionalSectorCosine+edge;
        }
        private static float factionSeed(DirectedFront state)=>state.target%97;
        private float DirectedScore(int faction,DirectedFront state,int cell,int wave)
        {
            Vector2 delta=new Vector2(WrappedDeltaX(cell%World.Width-state.origin.x),cell/World.Width-state.origin.y);
            float scale=World.ResolutionScale,alignment=delta.sqrMagnitude>.001f?Vector2.Dot(delta.normalized,state.direction):0;
            // Coherent patches and radial distance soften the square grid's Manhattan waves.
            // The support term still fills notches; adjacency remains mandatory at capture time.
            float patch=Mathf.PerlinNoise((state.origin.x+delta.x+faction*31.7f)*.12f/scale,(cell/World.Width+faction*15.3f)*.12f/scale)-.5f;
            if(TacticalBalance)
            {
                int support=0;for(int d=0;d<4;d++){int neighbour=World.Neighbor(cell,d);if(neighbour>=0&&Owners[neighbour]==faction)support++;}
                return wave*.10f+delta.magnitude*.90f+patch*8*scale+(1-alignment)*1.8f*scale-support*.65f;
            }
            float shoulder=alignment>=Rules.directionalSectorCosine?(1-alignment)*4*scale:0;
            return BoundaryScore(faction,cell,wave)*.45f+delta.magnitude*.55f+patch*6*scale+shoulder;
        }
        private void RebuildDirectedHeaps(int faction,DirectedFront state)
        {
            state.primary.Clear();state.other.Clear();
            foreach(var entry in _freeBoundary[faction])if(Owners[entry.Key]==Neutral)Push(InPrimarySector(state,entry.Key)?state.primary:state.other,new SeaNode(entry.Key,DirectedScore(faction,state,entry.Key,entry.Value),entry.Key));
        }
        private void OfferDirectedCell(int faction,int cell,int wave)
        {
            var state=_directedFronts[faction];if(state==null)return;
            Push(InPrimarySector(state,cell)?state.primary:state.other,new SeaNode(cell,DirectedScore(faction,state,cell,wave),cell));
            if(state.primary.Count+state.other.Count>_freeBoundary[faction].Count*8+128)RebuildDirectedHeaps(faction,state);
        }
        private int PeekDirectedHeap(int faction,List<SeaNode> heap)
        {
            var boundary=_freeBoundary[faction];
            while(heap.Count>0)
            {
                var candidate=heap[0];int cell=candidate.cell;
                if(!boundary.TryGetValue(cell,out int wave)||Owners[cell]!=Neutral||IsImpassable(cell)||!IsAdjacent(cell,faction)){Pop(heap);boundary.Remove(cell);continue;}
                float score=DirectedScore(faction,_directedFronts[faction],cell,wave);
                if(Mathf.Abs(candidate.score-score)>.0001f){Pop(heap);Push(heap,new SeaNode(cell,score,cell));continue;}
                return cell;
            }
            return -1;
        }
        private int SelectDirectedCell(int faction)
        {
            var state=_directedFronts[faction];var preferred=state.PreferPrimary?state.primary:state.other;var fallback=state.PreferPrimary?state.other:state.primary;
            int cell=PeekDirectedHeap(faction,preferred);return cell>=0?cell:PeekDirectedHeap(faction,fallback);
        }
        private void RecordDirectedCapture(int faction,int cell)
        {
            var state=_directedFronts[faction];if(state==null)return;
            if(InPrimarySector(state,cell))state.primaryCaptured++;else state.otherCaptured++;
            state.credit=(state.credit+state.primaryWeight)%(state.primaryWeight+state.otherWeight);
        }
        public bool ExpansionDirection(int faction,out Vector2 origin,out Vector2 direction,out int primary,out int other)
        {
            var state=_directedFronts[faction];origin=state?.origin??Vector2.zero;direction=state?.direction??Vector2.zero;primary=state?.primaryCaptured??0;other=state?.otherCaptured??0;return state!=null;
        }
    }
    // Input timing is separate from canonical simulation time and troop accounting.
    public sealed class NeutralDoubleClick
    {
        private double _lastTime=double.NegativeInfinity;private Vector2 _lastPosition;
        public bool Click(Vector2 position,double now,float radius=20,float seconds=.5f)
        {
            bool doubleClick=now>=_lastTime&&now-_lastTime<=seconds&&(position-_lastPosition).sqrMagnitude<=radius*radius;
            _lastTime=doubleClick?double.NegativeInfinity:now;_lastPosition=position;return doubleClick;
        }
        public void Clear()=>_lastTime=double.NegativeInfinity;
    }
}
