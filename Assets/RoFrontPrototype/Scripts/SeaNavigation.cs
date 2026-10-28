using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public int NavigationSearches {get;private set;}
        public int DirectSeaRoutes {get;private set;}
        private int OffsetCell(int cell,int dx,int dy)
        {
            int x=cell%World.Width+dx,y=cell/World.Width+dy;if(World.WrapLongitude)x=(x%World.Width+World.Width)%World.Width;
            return World.InBounds(x,y)?World.At(x,y):-1;
        }
        public float SeaDistance(int a,int b)
        {float x=Mathf.Abs(a%World.Width-b%World.Width);if(World.WrapLongitude)x=Mathf.Min(x,World.Width-x);float y=a/World.Width-b/World.Width;return Mathf.Sqrt(x*x+y*y);}
        public bool SeaLineClear(int start,int end)
        {
            if(start<0||end<0||World.Land[start]||World.Land[end])return false;
            float x=start%World.Width,dx=end%World.Width-x;if(World.WrapLongitude&&Mathf.Abs(dx)>World.Width*.5f)dx+=dx>0?-World.Width:World.Width;
            float y=start/World.Width,dy=end/World.Width-y;int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dy))*2)),previous=start;
            for(int i=1;i<=steps;i++)
            {
                float t=i/(float)steps;int px=Mathf.RoundToInt(x+dx*t),py=Mathf.RoundToInt(y+dy*t);if(World.WrapLongitude)px=(px%World.Width+World.Width)%World.Width;
                if(!World.InBounds(px,py))return false;int cell=World.At(px,py);if(World.Land[cell])return false;
                if(cell%World.Width!=previous%World.Width&&cell/World.Width!=previous/World.Width)
                {int sideA=World.At(cell%World.Width,previous/World.Width),sideB=World.At(previous%World.Width,cell/World.Width);if(World.Land[sideA]||World.Land[sideB])return false;}
                previous=cell;
            }
            return true;
        }
        private float SeaHeuristic(int a,int b)
        {int x=Mathf.Abs(a%World.Width-b%World.Width),y=Mathf.Abs(a/World.Width-b/World.Width);if(World.WrapLongitude)x=Mathf.Min(x,World.Width-x);return 10*(x+y)-6*Mathf.Min(x,y);}
        private Queue<int> FindNaturalSeaRoute(int start,int goal)
        {long stamp=MeterStart();try{return FindNaturalSeaRouteCore(start,goal);}finally{MeterEnd(7,stamp);}}
        private Queue<int> FindNaturalSeaRouteCore(int start,int goal)
        {
            NavigationSearches++;
            if(SeaLineClear(start,goal)){DirectSeaRoutes++;return start==goal?new Queue<int>():new Queue<int>(new[]{goal});}
            int stamp=++_searchId,expanded=0;var heap=new List<SeaNode>();_visited[start]=stamp;_parents[start]=-1;_search[start]=0;Push(heap,new SeaNode(start,SeaHeuristic(start,goal),SeaHeuristic(start,goal)));
            while(heap.Count>0&&expanded++<1500000)
            {
                int cell=Pop(heap).cell;
                if(cell==goal)
                {
                    var raw=new List<int>();for(int p=goal;p>=0;p=_parents[p])raw.Add(p);raw.Reverse();var smooth=new List<int>();int anchor=0;
                    while(anchor<raw.Count-1)
                    {
                        int next=raw.Count-1;while(next>anchor+1&&!SeaLineClear(raw[anchor],raw[next]))next--;
                        smooth.Add(raw[next]);anchor=next;
                    }
                    return new Queue<int>(smooth);
                }
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0)continue;int next=OffsetCell(cell,dx,dy);if(next<0||World.Land[next])continue;
                    if(dx!=0&&dy!=0){int a=OffsetCell(cell,dx,0),b=OffsetCell(cell,0,dy);if(a<0||b<0||World.Land[a]||World.Land[b])continue;}
                    int cost=_search[cell]+(dx==0||dy==0?10:14);if(_visited[next]==stamp&&cost>=_search[next])continue;
                    _visited[next]=stamp;_search[next]=cost;_parents[next]=cell;float h=SeaHeuristic(next,goal);Push(heap,new SeaNode(next,cost+h,h));
                }
            }
            return null;
        }
        public Vector3 NavalPosition(int first,int next,float progress)
        {
            float x=first%World.Width+.5f,dx=next%World.Width-first%World.Width;if(World.WrapLongitude&&Mathf.Abs(dx)>World.Width*.5f)dx+=dx>0?-World.Width:World.Width;
            return new Vector3(World.WrapLongitude?Mathf.Repeat(x+dx*progress,World.Width):x+dx*progress,0,Mathf.Lerp(first/World.Width+.5f,next/World.Width+.5f,progress));
        }
        public int OperationCell(MilitaryOperation op)
        {
            if(op.kind!=OperationKind.Naval)return op.frontCell;var p=NavalPosition(op.currentCell,op.nextCell,op.progress);
            return World.At(Mathf.Clamp(Mathf.FloorToInt(p.x),0,World.Width-1),Mathf.Clamp(Mathf.FloorToInt(p.z),0,World.Height-1));
        }
        public int WarshipCell(Warship ship)
        {var p=NavalPosition(ship.currentCell,ship.nextCell,ship.progress);return World.At(Mathf.Clamp(Mathf.FloorToInt(p.x),0,World.Width-1),Mathf.Clamp(Mathf.FloorToInt(p.z),0,World.Height-1));}
        private void AdvanceNavalMotion(ref int first,ref int next,ref float clock,ref float progress,Queue<int> route,float dt,float speed)
        {
            clock+=dt;
            while(route.Count>0)
            {
                next=route.Peek();float duration=Mathf.Max(.001f,SeaDistance(first,next)/Mathf.Max(.01f,speed));
                if(clock<duration){progress=clock/duration;return;}clock-=duration;first=route.Dequeue();
            }
            next=first;progress=0;clock=0;
        }
        public void PrepareTradePath(TradeRoute route)
        {
            route.distances=new float[route.path.Count];float length=0;
            for(int i=1;i<route.path.Count;i++){length+=SeaDistance(route.path[i-1],route.path[i]);route.distances[i]=length;}
            route.length=length;route.duration=Mathf.Max(6,length/(route.naval?Rules.tradeShipSpeed:Rules.truckSpeed));
        }
        public Vector3 TradePosition(TradeRoute route,out Vector3 direction)
        {
            return TradePathPosition(route,route.Progress,route.reverse,out direction);
        }
        public Vector3 TradePathPosition(TradeRoute route,float progress,bool reverse,out Vector3 direction)
        {
            if(route.distances==null)PrepareTradePath(route);float target=(reverse?1-progress:progress)*route.length;int low=0,high=route.path.Count-1;
            while(low+1<high){int mid=(low+high)/2;if(route.distances[mid]<=target)low=mid;else high=mid;}
            int a=low,b=Mathf.Min(a+1,route.path.Count-1);float fraction=route.distances[b]>route.distances[a]?(target-route.distances[a])/(route.distances[b]-route.distances[a]):0;
            Vector3 first=NavalPosition(route.path[a],route.path[b],0),last=NavalPosition(route.path[a],route.path[b],1);direction=(last-first)*(reverse?-1:1);
            if(direction.sqrMagnitude<.001f&&a>0)direction=(first-NavalPosition(route.path[a-1],route.path[a],0))*(reverse?-1:1);
            return NavalPosition(route.path[a],route.path[b],fraction);
        }
    }
}
