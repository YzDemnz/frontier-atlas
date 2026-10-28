using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class RoadNetworkVisuals:MonoBehaviour
    {
        private TerritorySimulation _game;private Mesh _mesh;private Material _material;private Vector3 _lastCamera;private float _lastZoom;private int _version=-1,_lastHeight;private float _nextBuild;
        private readonly List<Vector3> _vertices=new List<Vector3>();private readonly List<int> _indices=new List<int>();private readonly List<Color> _colours=new List<Color>();
        private readonly Dictionary<int,Vector2[]> _shapes=new Dictionary<int,Vector2[]>();private readonly HashSet<int> _live=new HashSet<int>();private readonly List<int> _retired=new List<int>();
        public int VisibleConnections {get;private set;}
        public void Initialize(TerritorySimulation game)
        {
            _game=game;var node=new GameObject("Conexiones automáticas de fábricas");node.transform.SetParent(transform,false);_mesh=new Mesh{name="Red industrial completa",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};node.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(Resources.Load<Material>("FrontierFleet"));var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        private Vector2 Cell(int c)=>new Vector2(c%_game.World.Width+.5f,c/_game.World.Width+.5f);
        private Vector3 Surface(Vector2 p,float lift)=>new Vector3(p.x,_game.World.RenderedSurfaceHeight(p.x,p.y)+lift,p.y);
        private Vector2[] Shape(TradeRoute route)
        {
            if(_shapes.TryGetValue(route.id,out var cached))return cached;var path=route.path;var keep=new bool[path.Count];keep[0]=keep[path.Count-1]=true;var spans=new Stack<Vector2Int>();int begin=0;
            for(int i=1;i<path.Count;i++)if(Mathf.Abs(path[i]%_game.World.Width-path[i-1]%_game.World.Width)>_game.World.Width*.5f){spans.Push(new Vector2Int(begin,i-1));keep[i-1]=keep[i]=true;begin=i;}spans.Push(new Vector2Int(begin,path.Count-1));
            float tolerance=Mathf.Max(1,_game.World.ResolutionScale*.65f);tolerance*=tolerance;
            while(spans.Count>0)
            {
                var span=spans.Pop();if(span.y-span.x<2)continue;Vector2 a=Cell(path[span.x]),b=Cell(path[span.y]),delta=b-a;float length=delta.sqrMagnitude,best=tolerance;int index=-1;
                for(int i=span.x+1;i<span.y;i++){Vector2 p=Cell(path[i]);float t=length>.001f?Mathf.Clamp01(Vector2.Dot(p-a,delta)/length):0;float d=(p-(a+delta*t)).sqrMagnitude;if(d>best){best=d;index=i;}}
                if(index<0)continue;keep[index]=true;spans.Push(new Vector2Int(span.x,index));spans.Push(new Vector2Int(index,span.y));
            }
            var points=new List<Vector2>();for(int i=0;i<path.Count;i++)if(keep[i])points.Add(Cell(path[i]));cached=points.ToArray();_shapes[route.id]=cached;return cached;
        }
        private bool InView(Camera camera,Bounds bounds)
        {
            float minX=2,maxX=-1,minY=2,maxY=-1;var lo=bounds.min;var hi=bounds.max;
            for(int i=0;i<8;i++){var p=camera.WorldToViewportPoint(new Vector3((i&1)==0?lo.x:hi.x,(i&2)==0?lo.y:hi.y,(i&4)==0?lo.z:hi.z));minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}return maxX>=-.1f&&minX<=1.1f&&maxY>=-.1f&&minY<=1.1f;
        }
        private void LateUpdate()
        {
            if(_game==null)return;var camera=Camera.main;if(camera==null)return;bool moved=Vector3.Distance(_lastCamera,camera.transform.position)>camera.orthographicSize*.1f||Mathf.Abs(_lastZoom-camera.orthographicSize)>Mathf.Max(2,_lastZoom*.08f)||_lastHeight!=Screen.height;
            if((_version==_game.TradeNetworkVersion&&!moved)||Time.unscaledTime<_nextBuild)return;_lastCamera=camera.transform.position;_lastZoom=camera.orthographicSize;_lastHeight=Screen.height;_nextBuild=Time.unscaledTime+.35f;_version=_game.TradeNetworkVersion;
            _vertices.Clear();_indices.Clear();_colours.Clear();_live.Clear();VisibleConnections=0;float step=Mathf.Clamp(camera.orthographicSize/100,2,12),width=Mathf.Max(.07f*_game.World.ResolutionScale,camera.orthographicSize/Mathf.Max(1,Screen.height)*.9f),lift=.3f+step*.1f;
            foreach(var route in _game.TradeRoutes)
            {
                if(route.naval||route.path==null||route.path.Count<2)continue;_live.Add(route.id);if(!InView(camera,_game.TradeRouteBounds(route)))continue;VisibleConnections++;var points=Shape(route);
                for(int k=1;k<points.Length;k++)
                {Vector2 a=points[k-1],b=points[k];if(Mathf.Abs(a.x-b.x)>_game.World.Width*.5f)continue;int segments=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/step));Vector3 first=Surface(a,lift);
                    for(int j=1;j<=segments;j++){var last=Surface(Vector2.Lerp(a,b,j/(float)segments),lift);Vector3 right=Vector3.Cross(Vector3.up,(last-first).normalized)*width;int n=_vertices.Count;_vertices.Add(first-right);_vertices.Add(first+right);_vertices.Add(last-right);_vertices.Add(last+right);for(int c=0;c<4;c++)_colours.Add(new Color(.9f,.94f,.95f,.85f));_indices.Add(n);_indices.Add(n+1);_indices.Add(n+2);_indices.Add(n+2);_indices.Add(n+1);_indices.Add(n+3);first=last;}
                }
            }
            _retired.Clear();foreach(int id in _shapes.Keys)if(!_live.Contains(id))_retired.Add(id);foreach(int id in _retired)_shapes.Remove(id);
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetColors(_colours);_mesh.SetTriangles(_indices,0);_mesh.RecalculateBounds();
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
