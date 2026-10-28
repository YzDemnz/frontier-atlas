using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    [Serializable] public sealed class GeographicRing { public Vector2[] points; }
    [Serializable] public sealed class GeographicPolygon { public GeographicRing[] rings; }
    [Serializable] public sealed class CountryDefinition
    {
        public string code, name;
        public float longitude, latitude;
        public GeographicPolygon[] polygons;
    }
    [Serializable] public sealed class AfricaGeography
    {
        public string source;
        public float west=-26,east=59,south=-36,north=39;
        public bool wrapLongitude;
        public CountryDefinition[] countries;
        public GeographicPolygon[] neutralRegions;
        public static AfricaGeography Load()
        {
            var resource = Resources.Load<TextAsset>("WorldGeography");
            if (resource == null) throw new InvalidOperationException("WorldGeography missing.");
            return JsonUtility.FromJson<AfricaGeography>(resource.text);
        }
    }

    public sealed class WorldGrid
    {
        public readonly int Width, Height, LandCount;
        public readonly bool[] Land;
        public readonly float[] Elevation, Moisture;
        public readonly int[] GeographicCountries, StartCells;
        public readonly CountryDefinition[] Countries;
        public readonly float ResolutionScale,CellAreaFactor;
        public readonly float West,East,South,North;
        public readonly bool WrapLongitude;
        public readonly int[] CoastCells;
        public readonly byte[] InlandCoastDistance;
        public WorldGrid(int width, int height, int seed) : this(width,height,seed,AfricaGeography.Load()) { }
        public WorldGrid(int width, int height, int seed, AfricaGeography data)
        {
            Width=width; Height=height; Countries=data.countries;
            West=data.west;East=data.east;South=data.south;North=data.north;WrapLongitude=data.wrapLongitude;
            ResolutionScale=Mathf.Max(1,width/((East-West)*6));CellAreaFactor=ResolutionScale*ResolutionScale;
            Land=new bool[width*height]; Elevation=new float[Land.Length]; Moisture=new float[Land.Length];
            GeographicCountries=new int[Land.Length]; Array.Fill(GeographicCountries,-2);
            foreach(var polygon in data.neutralRegions) Rasterize(polygon,-1);
            for(int c=0;c<Countries.Length;c++) foreach(var polygon in Countries[c].polygons) Rasterize(polygon,c);
            StartCells=new int[Countries.Length];
            var labels=new Vector2[Countries.Length];var nearest=new float[Countries.Length];
            for(int c=0;c<Countries.Length;c++)
            {
                labels[c]=ToGrid(Countries[c].longitude,Countries[c].latitude);nearest[c]=float.PositiveInfinity;StartCells[c]=-1;
            }
            for(int id=0;id<Land.Length;id++)
            {
                int c=GeographicCountries[id];if(c<0)continue;
                float dx=Mathf.Abs(id%Width-labels[c].x);if(WrapLongitude)dx=Mathf.Min(dx,Width-dx);
                float dy=id/Width-labels[c].y,d=dx*dx+dy*dy;
                if(d<nearest[c]){nearest[c]=d;StartCells[c]=id;}
            }
            for(int c=0;c<Countries.Length;c++)
            {
                int best=StartCells[c];Vector2 label=labels[c];
                // Tiny islands keep a minimum grid cell at their real location.
                if(best<0)
                {
                    best=At(Mathf.Clamp(Mathf.RoundToInt(label.x),0,width-1),Mathf.Clamp(Mathf.RoundToInt(label.y),0,height-1));
                    Land[best]=true; GeographicCountries[best]=c;
                }
                StartCells[c]=best;
            }
            int count=0;var coast=new List<int>();
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                int id=At(x,y); if(!Land[id]) continue; count++;
                float lon=West+(x+.5f)/width*(East-West), lat=South+(y+.5f)/height*(North-South);
                float noise=Mathf.PerlinNoise(x*.025f/ResolutionScale+seed*.001f,y*.025f/ResolutionScale+seed*.002f);
                float dry=Mathf.Max(Gauss(lon,lat,12,23,32,10)*.70f,Gauss(lon,lat,46,24,15,11)*.68f);
                dry=Mathf.Max(dry,Gauss(lon,lat,132,-25,25,14)*.65f);
                dry=Mathf.Max(dry,Gauss(lon,lat,95,42,24,12)*.55f);
                dry=Mathf.Max(dry,Gauss(lon,lat,-113,33,16,11)*.47f);
                dry=Mathf.Max(dry,Gauss(lon,lat,19,-24,10,12)*.62f);
                dry=Mathf.Max(dry,Gauss(lon,lat,-70,-24,4,16)*.61f);
                Moisture[id]=Mathf.Clamp01(.75f-dry+(noise-.5f)*.18f);
                float mountains=
                    Gauss(lon,lat,2,32,9,4)*.55f+Gauss(lon,lat,39,9,5,6)*.75f+
                    Gauss(lon,lat,35,-6,4,13)*.4f+Gauss(lon,lat,29,-29,4,4)*.55f+
                    Gauss(lon,lat,47,-20,2,7)*.45f+Gauss(lon,lat,-72,-21,5,32)*.9f+
                    Gauss(lon,lat,-111,44,7,19)*.8f+Gauss(lon,lat,87,30,20,6)*1.05f+
                    Gauss(lon,lat,11,46,8,3)*.65f+Gauss(lon,lat,46,43,7,3)*.65f+
                    Gauss(lon,lat,137,37,3,9)*.65f+Gauss(lon,lat,146,-27,4,15)*.4f+
                    Gauss(lon,lat,170,-43,3,8)*.72f+Gauss(lon,lat,-41,73,13,14)*.55f;
                float ridges=1-Mathf.Abs(Mathf.PerlinNoise(x*.085f/ResolutionScale+8,y*.085f/ResolutionScale+13)*2-1);
                Elevation[id]=Mathf.Clamp01(.1f+noise*.20f+mountains*(.35f+ridges*.55f));
                if(lat<-62)Elevation[id]=Mathf.Max(Elevation[id],.35f+noise*.3f);
                for(int direction=0;direction<4;direction++){int n=Neighbor(id,direction);if(n>=0&&!Land[n]){coast.Add(id);break;}}
            }
            LandCount=count;CoastCells=coast.ToArray();
            InlandCoastDistance=new byte[Land.Length];Array.Fill(InlandCoastDistance,(byte)255);var inland=new Queue<int>();
            foreach(int cell in CoastCells){InlandCoastDistance[cell]=0;inland.Enqueue(cell);}int band=Mathf.Min(254,Mathf.CeilToInt(24*ResolutionScale));
            while(inland.Count>0)
            {
                int cell=inland.Dequeue(),distance=InlandCoastDistance[cell]+1;if(distance>band)continue;
                for(int direction=0;direction<4;direction++){int next=Neighbor(cell,direction);if(next>=0&&Land[next]&&InlandCoastDistance[next]>distance){InlandCoastDistance[next]=(byte)distance;inland.Enqueue(next);}}
            }
        }
        private static float Gauss(float x,float y,float cx,float cy,float rx,float ry)
        {float dx=(x-cx)/rx,dy=(y-cy)/ry;return Mathf.Exp(-dx*dx-dy*dy);}
        public float Latitude(int id)=>South+(id/Width+.5f)/Height*(North-South);
        public float SurfaceHeight(float x,float z)
        {
            int ix=WrapLongitude?(int)Mathf.Repeat(x,Width):Mathf.Clamp(Mathf.RoundToInt(x),0,Width-1);
            int iy=Mathf.Clamp(Mathf.RoundToInt(z),0,Height-1),id=At(ix,iy);
            float shore=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,InlandCoastDistance[id]/(24*ResolutionScale)));
            return Land[id]?Elevation[id]*10*ResolutionScale*shore:0;
        }
        // Barycentric interpolation of the exact triangles used by MapVisuals.BuildSurface.
        public const int SurfaceColumns=720,SurfaceRows=360;
        public float RenderedSurfaceHeight(float x,float z)
        {
            float gx=Mathf.Clamp(x/Width*SurfaceColumns,0,SurfaceColumns),gz=Mathf.Clamp(z/Height*SurfaceRows,0,SurfaceRows);
            int ix=Mathf.Min(SurfaceColumns-1,Mathf.FloorToInt(gx)),iz=Mathf.Min(SurfaceRows-1,Mathf.FloorToInt(gz));float u=gx-ix,v=gz-iz;
            float x0=ix/(float)SurfaceColumns*Width,x1=(ix+1)/(float)SurfaceColumns*Width,z0=iz/(float)SurfaceRows*Height,z1=(iz+1)/(float)SurfaceRows*Height;
            float b=SurfaceHeight(x1,z0),c=SurfaceHeight(x0,z1);
            return u+v<=1?SurfaceHeight(x0,z0)*(1-u-v)+b*u+c*v:b*(1-v)+c*(1-u)+SurfaceHeight(x1,z1)*(u+v-1);
        }
        public float SeaHeight(float x,float z,float time,bool reducedMotion=false)
        {
            float t=reducedMotion?0:time;
            return (Mathf.Sin(x*.025f+t*.6f)*Mathf.Sin(z*.017f-t*.42f)*.2f+Mathf.Sin(x*.055f+z*.04f+t*.9f)*.05f)*ResolutionScale;
        }
        public int Neighbor(int id,int direction)
        {
            int x=id%Width,y=id/Width;
            switch(direction){case 0:return x>0?id-1:WrapLongitude?id+Width-1:-1;case 1:return x+1<Width?id+1:WrapLongitude?id-Width+1:-1;
                case 2:return y>0?id-Width:-1;default:return y+1<Height?id+Width:-1;}
        }
        public Vector2 ToGrid(float lon,float lat)=>new Vector2((lon-West)/(East-West)*Width,(lat-South)/(North-South)*Height);
        private void Rasterize(GeographicPolygon polygon,int country)
        {
            float low=Height,high=0;
            foreach(var ring in polygon.rings) foreach(var p in ring.points)
            {float y=ToGrid(p.x,p.y).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
            var crossings=new List<float>();
            for(int y=Mathf.Max(0,Mathf.FloorToInt(low));y<=Mathf.Min(Height-1,Mathf.CeilToInt(high));y++)
            {
                crossings.Clear(); float scan=y+.5f;
                foreach(var ring in polygon.rings) for(int i=0,j=ring.points.Length-1;i<ring.points.Length;j=i++)
                {
                    Vector2 a=ToGrid(ring.points[i].x,ring.points[i].y),b=ToGrid(ring.points[j].x,ring.points[j].y);
                    if((a.y>scan)==(b.y>scan)) continue;
                    crossings.Add(a.x+(scan-a.y)*(b.x-a.x)/(b.y-a.y));
                }
                crossings.Sort();
                for(int pair=0;pair+1<crossings.Count;pair+=2)
                    for(int x=Mathf.Max(0,Mathf.CeilToInt(crossings[pair]-.5f));x<=Mathf.Min(Width-1,Mathf.FloorToInt(crossings[pair+1]-.5f));x++)
                    {int id=At(x,y);Land[id]=true;GeographicCountries[id]=country;}
            }
        }
        public int At(int x,int y)=>y*Width+x;
        public bool InBounds(int x,int y)=>x>=0&&x<Width&&y>=0&&y<Height;
        public bool IsLand(int x,int y)=>InBounds(x,y)&&Land[At(x,y)];
        public void ForEachNeighbor(int id,Action<int> callback)
        {
            for(int direction=0;direction<4;direction++){int n=Neighbor(id,direction);if(n>=0)callback(n);}
        }
    }
}
