using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class NavalVisuals : MonoBehaviour
    {
        private TerritorySimulation _game;private FrontierSettings _settings;private PowerBalance _powers;private Mesh _mesh;private Material _material;
        private readonly List<Vector3> _v=new List<Vector3>();private readonly List<Color> _c=new List<Color>();private readonly List<int> _t=new List<int>();
        private readonly Dictionary<int,List<TrailPoint>> _trails=new Dictionary<int,List<TrailPoint>>();private readonly Dictionary<int,int> _factions=new Dictionary<int,int>();private readonly HashSet<int> _active=new HashSet<int>();
        private GameObject _boatPrefab;private readonly List<GameObject> _boats=new List<GameObject>();private readonly int[] _boatIds=new int[24];
        public int ActiveModels {get;private set;}public bool UsesOriginalBoat=>_boatPrefab!=null;
        private struct TrailPoint{public Vector3 point;public float time;public TrailPoint(Vector3 p,float t){point=p;time=t;}}
        public int TrailSegments {get;private set;}
        public void Initialize(TerritorySimulation game,FrontierSettings settings,PowerBalance powers)
        {
            _game=game;_settings=settings;_powers=powers;var go=new GameObject("Transportes y estelas");go.transform.SetParent(transform,false);
            _boatPrefab=Resources.Load<GameObject>("Models/CommercialFishingBoat");
            _mesh=new Mesh{name="Estelas de transportes militares",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};_mesh.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(Resources.Load<Material>("FrontierFleet"));go.AddComponent<MeshRenderer>().sharedMaterial=_material;
        }
        private void LateUpdate()
        {
            if(_game==null)return;TrailSegments=0;ActiveModels=0;_v.Clear();_c.Clear();_t.Clear();_active.Clear();
            if(_game.CampaignEnabled)
            {foreach(var op in _game.Operations)if(op.active&&op.kind==OperationKind.Naval)Marker(op.id,op.faction,op.currentCell,op.nextCell,op.progress);}
            else for(int f=0;f<_game.FactionCount;f++)if(_game.FleetActive[f])Marker(f,f,_game.FleetCells[f],_game.FleetNext[f],_game.FleetProgress[f]);
            for(int index=ActiveModels;index<_boats.Count;index++)_boats[index].SetActive(false);
            var retired=new List<int>();float scale=_game.World.ResolutionScale;
            foreach(var entry in _trails)
            {
                int id=entry.Key;var trail=entry.Value;while(trail.Count>0&&_game.ElapsedTime-trail[0].time>24)trail.RemoveAt(0);
                if(trail.Count==0&&!_active.Contains(id)){retired.Add(id);continue;}
                for(int j=1;j<trail.Count;j++)
                {
                    Vector3 a=trail[j-1].point,b=trail[j].point;if(Vector3.Distance(a,b)>8*scale)continue;
                    TrailSegments++;
                    a.y=_game.World.SeaHeight(a.x,a.z,Time.time,_settings.ReducedMotion)+.35f;b.y=_game.World.SeaHeight(b.x,b.z,Time.time,_settings.ReducedMotion)+.35f;
                    Vector3 right=Vector3.Cross(Vector3.up,(b-a).normalized)*.075f*scale;
                    Color colour=Color.Lerp(new Color(.015f,.055f,.075f),_powers.DisplayColors[_factions[id]],.23f*(1-(_game.ElapsedTime-trail[j].time)/24));
                    Triangle(a-right,b-right,a+right,colour);Triangle(a+right,b-right,b+right,colour);
                }
            }
            foreach(int id in retired){_trails.Remove(id);_factions.Remove(id);}
            _mesh.Clear();_mesh.SetVertices(_v);_mesh.SetColors(_c);_mesh.SetTriangles(_t,0);_mesh.RecalculateBounds();
        }
        private void Marker(int id,int faction,int first,int next,float progress)
        {
            var world=_game.World;float scale=world.ResolutionScale;Vector3 p=new Vector3(first%world.Width+.5f,0,first/world.Width+.5f),q=new Vector3(next%world.Width+.5f,0,next/world.Width+.5f);
            if(Mathf.Abs(q.x-p.x)>world.Width*.5f)q.x+=q.x<p.x?world.Width:-world.Width;
            Vector3 forward=(q-p).sqrMagnitude>.001f?(q-p).normalized:Vector3.forward;p=Vector3.Lerp(p,q,progress);p.x=Mathf.Repeat(p.x,world.Width);p.y=world.SeaHeight(p.x,p.z,Time.time,_settings.ReducedMotion)+.45f;
            if(!_trails.TryGetValue(id,out var trail)){trail=new List<TrailPoint>();_trails[id]=trail;}_factions[id]=faction;_active.Add(id);
            if(trail.Count==0||Vector3.Distance(trail[trail.Count-1].point,p)>.55f*scale)trail.Add(new TrailPoint(p,_game.ElapsedTime));if(trail.Count>48)trail.RemoveAt(0);
            var camera=Camera.main;Vector3 viewport=camera.WorldToViewportPoint(p);
            if(_boatPrefab==null||ActiveModels>=24||viewport.z<=0||viewport.x<-.04f||viewport.x>1.04f||viewport.y<-.04f||viewport.y>1.04f)return;
            int slot=ActiveModels++;if(slot>=_boats.Count)_boats.Add(Instantiate(_boatPrefab,transform));var boat=_boats[slot];boat.SetActive(true);
            float time=_settings.ReducedMotion?0:_game.ElapsedTime;
            p.y=world.SeaHeight(p.x,p.z,Time.time,_settings.ReducedMotion)+.16f+(_settings.ReducedMotion?0:Mathf.Sin(time*1.1f+id)*.07f);
            Quaternion rotation=Quaternion.LookRotation(forward)*Quaternion.Euler(_settings.ReducedMotion?0:Mathf.Sin(time*.7f+id)*1.5f,0,_settings.ReducedMotion?0:Mathf.Sin(time*.9f+id)*2.3f);
            boat.transform.position=p;boat.transform.rotation=_boatIds[slot]==id?Quaternion.Slerp(boat.transform.rotation,rotation,1-Mathf.Exp(-7*Time.unscaledDeltaTime)):rotation;_boatIds[slot]=id;
            boat.transform.localScale=Vector3.one*scale*.7f;
        }
        private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color colour){int n=_v.Count;_v.Add(a);_v.Add(b);_v.Add(c);_c.Add(colour);_c.Add(colour);_c.Add(colour);_t.Add(n);_t.Add(n+1);_t.Add(n+2);}
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
