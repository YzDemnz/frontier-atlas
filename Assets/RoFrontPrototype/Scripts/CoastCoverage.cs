using UnityEngine;
namespace FrontierAtlas
{
    public static class CoastCoverage
    {
        public static float MarineDepth(WorldGrid world,int[] distance,int cell)
        {
            if(distance[cell]>12*world.ResolutionScale)return Mathf.Clamp01(distance[cell]/(20f*world.ResolutionScale));
            float total=0;int x=cell%world.Width,y=cell/world.Width;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                int px=x+dx,py=Mathf.Clamp(y+dy,0,world.Height-1);if(world.WrapLongitude)px=(px%world.Width+world.Width)%world.Width;else px=Mathf.Clamp(px,0,world.Width-1);
                total+=distance[world.At(px,py)]*(dx==0?2:1)*(dy==0?2:1);
            }
            return Mathf.Clamp01(total/(16*20*world.ResolutionScale));
        }
        // Presentation only. Never changes land, coast access, navigation, or small islands.
        public static byte Sample(WorldGrid world,int cell)
        {
            int x=cell%world.Width,y=cell/world.Width,total=0;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                int px=x+dx,py=Mathf.Clamp(y+dy,0,world.Height-1);if(world.WrapLongitude)px=(px%world.Width+world.Width)%world.Width;else px=Mathf.Clamp(px,0,world.Width-1);
                int weight=(dx==0?2:1)*(dy==0?2:1);if(world.Land[world.At(px,py)])total+=weight;
            }
            float coverage=total/16f;coverage=world.Land[cell]?Mathf.Max(.54f,coverage):Mathf.Min(.46f,coverage);return (byte)Mathf.RoundToInt(coverage*255);
        }
    }
}
