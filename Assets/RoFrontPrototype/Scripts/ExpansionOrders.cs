using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private Dictionary<int,int>[] _freeBoundary;
        private List<SeaNode>[] _freeHeap;private bool[] _freeSeeded;
        private void EnsureFreeBoundary(int f){if(!_freeSeeded[f])SeedFreeBoundary(f);}
        private void InitializeFreeExpansion()
        {
            _freeBoundary=new Dictionary<int,int>[FactionCount];_freeHeap=new List<SeaNode>[FactionCount];_freeSeeded=new bool[FactionCount];_directedFronts=new DirectedFront[FactionCount];
            for(int f=0;f<FactionCount;f++){_freeBoundary[f]=new Dictionary<int,int>();_freeHeap[f]=new List<SeaNode>();}
        }
        public float AvailableTroops(int f)=>Mathf.Max(0,TacticalBalance?Troops[f]:Troops[f]-CommittedTroops[f]);
        public float PreviewTroops(int f,float fraction)=>ReferenceBalance?Mathf.Floor(AvailableTroops(f)*Mathf.Clamp01(fraction)):Mathf.Floor(AvailableTroops(f)*Mathf.Clamp01(fraction)*World.CellAreaFactor)/World.CellAreaFactor;
        public bool IsBorderTarget(int f,int cell)=>cell>=0&&cell<Owners.Length&&World.Land[cell]&&Owners[cell]!=f&&IsAdjacent(cell,f);
        private void SeedFreeBoundary(int f)
        {
            _freeBoundary[f].Clear();_freeHeap[f].Clear();_freeSeeded[f]=true;
            foreach(int edge in _frontiers[f])for(int d=0;d<4;d++)
            {int n=World.Neighbor(edge,d);if(n>=0&&Owners[n]==Neutral&&!_freeBoundary[f].ContainsKey(n))OfferFreeBoundary(f,n,0);}
        }
        private void AdvanceFreeBoundary(int f,int cell)
        {
            if(_freeBoundary==null)return;
            var boundary=_freeBoundary[f];boundary.TryGetValue(cell,out int wave);boundary.Remove(cell);
            for(int d=0;d<4;d++)
            {
                int n=World.Neighbor(cell,d);if(n<0||Owners[n]!=Neutral)continue;
                OfferFreeBoundary(f,n,wave+1);
            }
        }
        private int SelectFreeBoundary(int f)
        {long stamp=MeterStart();try{return _directedFronts[f]!=null?SelectDirectedCell(f):SelectFreeBoundaryCore(f);}finally{MeterEnd(8,stamp);}}
        private int SelectFreeBoundaryCore(int f)
        {
            var heap=_freeHeap[f];var boundary=_freeBoundary[f];
            while(heap.Count>0)
            {
                var best=heap[0];int cell=best.cell;
                if(!boundary.TryGetValue(cell,out int wave)||Owners[cell]!=Neutral||(CampaignEnabled&&IsImpassable(cell))||!IsAdjacent(cell,f))
                {Pop(heap);boundary.Remove(cell);continue;}
                float score=BoundaryScore(f,cell,wave);
                if(Mathf.Abs(best.score-score)>.0001f){Pop(heap);Push(heap,new SeaNode(cell,score,cell));continue;}
                return cell;
            }
            return -1;
        }
        private float BoundaryScore(int f,int cell,int wave)
        {
            int support=0;for(int d=0;d<4;d++){int n=World.Neighbor(cell,d);if(n>=0&&Owners[n]==f)support++;}
            if(TacticalBalance)
            {
                int home=CapitalCells[f];float x=WrappedDeltaX(cell%World.Width-home%World.Width),y=cell/World.Width-home/World.Width,scale=World.ResolutionScale;
                float patch=Mathf.PerlinNoise((cell%World.Width+f*31.7f)*.12f/scale,(cell/World.Width+f*15.3f)*.12f/scale)-.5f;
                return wave*.10f+Mathf.Sqrt(x*x+y*y)*.90f+patch*8*scale-support*.65f;
            }
            float variation=((cell*73856093L+f*19349663L)&1023)/1023f;return wave+variation*.40f-support*.18f;
        }
        private void OfferFreeBoundary(int f,int cell,int wave)
        {
            var boundary=_freeBoundary[f];if(boundary.TryGetValue(cell,out int previous))wave=System.Math.Min(wave,previous);boundary[cell]=wave;
            Push(_freeHeap[f],new SeaNode(cell,BoundaryScore(f,cell,wave),cell));OfferDirectedCell(f,cell,wave);
            if(_freeHeap[f].Count>boundary.Count*8+128)
            {var heap=_freeHeap[f];heap.Clear();foreach(var entry in boundary)if(Owners[entry.Key]==Neutral)Push(heap,new SeaNode(entry.Key,BoundaryScore(f,entry.Key,entry.Value),entry.Key));}
        }
        public OrderResult BeginExpansion(int f,float fraction=1)
        {
            if(CampaignEnabled){var result=StartCampaignExpansion(f,fraction);if(result==OrderResult.Accepted)_directedFronts[f]=null;return result;}
            if(Paused)return OrderResult.Paused;if(TerritoryCounts[f]==0)return OrderResult.Eliminated;
            float troops=PreviewTroops(f,fraction);if(troops<NeutralSectorCost)return OrderResult.NoTroops;
            SeedFreeBoundary(f);int first=SelectFreeBoundary(f);if(first<0)return OrderResult.NoFreeLand;
            bool reinforce=HasOrder(f)&&AttackFactions[f]<0&&!FleetActive[f];
            if(!reinforce)CancelOrder(f);
            Targets[f]=first;AttackFactions[f]=-1;CommittedTroops[f]+=troops;States[f]=ArmyState.Marching;
            return OrderResult.Accepted;
        }
        public OrderResult AttackBorder(int f,int cell,float fraction=1)
        {
            if(CampaignEnabled)return StartCampaignAttack(f,cell,fraction);
            if(Paused)return OrderResult.Paused;if(TerritoryCounts[f]==0)return OrderResult.Eliminated;
            if(!IsBorderTarget(f,cell))return OrderResult.NotAdjacent;
            if(Owners[cell]==Neutral)return BeginExpansion(f,fraction);
            float troops=PreviewTroops(f,fraction);if(troops<OccupationSectorCost)return OrderResult.NoTroops;
            int enemy=Owners[cell];bool reinforce=HasOrder(f)&&AttackFactions[f]==enemy&&!FleetActive[f];
            if(!reinforce)CancelOrder(f);
            _routes[f].Clear();_routes[f].Enqueue(cell);Targets[f]=cell;AttackFactions[f]=enemy;
            CommittedTroops[f]+=troops;SeedFreeBoundary(f);States[f]=ArmyState.Marching;return OrderResult.Accepted;
        }
        public OrderResult InvadeBySea(int f,int cell,float fraction=1)
        {
            if(CampaignEnabled)return StartCampaignInvasion(f,cell,fraction);
            if(Paused)return OrderResult.Paused;if(TerritoryCounts[f]==0)return OrderResult.Eliminated;
            if(cell<0||cell>=Owners.Length)return OrderResult.NoLandRoute;
            if(Owners[cell]==f)return OrderResult.OwnTerritory;
            return IssueSeaOrder(f,cell,fraction);
        }
    }
}
