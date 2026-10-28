using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    // Only local water disturbance. Commerce has no persistent trail.
    public sealed class CampaignVisuals : MonoBehaviour
    {
        private TerritorySimulation _game;private FrontierSettings _settings;private Mesh _mesh;private Material _material;
        private readonly List<Vector3> _vertices=new List<Vector3>();private readonly List<Color> _colours=new List<Color>();private readonly List<int> _triangles=new List<int>();
        public void Initialize(TerritorySimulation game,PowerBalance powers,FrontierSettings settings)
        {
            _game=game;_settings=settings;var node=new GameObject("Oleaje local de barcos comerciales");node.transform.SetParent(transform,false);
            _mesh=new Mesh{name="Ondas locales sin estela",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};_mesh.MarkDynamic();node.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(Resources.Load<Shader>("LocalWaterRipple"));_material.SetTexture("_LandMask",GetComponent<MapVisuals>().CoastMask);_material.SetVector("_WorldSize",new Vector4(game.World.Width,game.World.Height,0,0));node.AddComponent<MeshRenderer>().sharedMaterial=_material;
            gameObject.AddComponent<CommercialShipVisuals>().Initialize(game,settings);
            gameObject.AddComponent<RoadNetworkVisuals>().Initialize(game);
            if(!game.IsQuickMatch)gameObject.AddComponent<CommercialTrainVisuals>().Initialize(game,settings);
            if(!game.IsQuickMatch)gameObject.AddComponent<StructureModelVisuals>().Initialize(game);
            if(!game.IsQuickMatch)gameObject.AddComponent<StrategicMissileVisuals>().Initialize(game,settings);
        }
        private void LateUpdate()
        {
            if(_game==null||!_game.CampaignEnabled)return;_vertices.Clear();_colours.Clear();_triangles.Clear();var camera=Camera.main;
            if(!_settings.ReducedMotion&&camera.orthographicSize<260)foreach(var route in _game.TradeRoutes)
            {
                if(!route.naval||route.disabledUntil>_game.ElapsedTime)continue;Vector3 point=_game.TradePosition(route,out _);Vector3 screen=camera.WorldToViewportPoint(point);
                if(screen.x<0||screen.x>1||screen.y<0||screen.y>1)continue;
                for(int ring=0;ring<2;ring++)
                {
                    float phase=Mathf.Repeat(_game.ElapsedTime*.55f+route.id*.39f+ring*.5f,1),radius=(.25f+phase*.8f)*_game.World.ResolutionScale,width=.025f*_game.World.ResolutionScale;
                    Color colour=new Color(.50f,.78f,.80f,.12f*(1-phase));
                    for(int segment=0;segment<20;segment++)
                    {
                        float a=segment*Mathf.PI*2/20,b=(segment+1)*Mathf.PI*2/20;Vector3 directionA=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),directionB=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Vector3 p0=point+directionA*(radius-width),p1=point+directionA*(radius+width),p2=point+directionB*(radius-width),p3=point+directionB*(radius+width);
                        p0.y=_game.World.SeaHeight(p0.x,p0.z,Time.time)+.17f;p1.y=_game.World.SeaHeight(p1.x,p1.z,Time.time)+.17f;p2.y=_game.World.SeaHeight(p2.x,p2.z,Time.time)+.17f;p3.y=_game.World.SeaHeight(p3.x,p3.z,Time.time)+.17f;
                        Triangle(p0,p1,p2,colour);Triangle(p2,p1,p3,colour);
                    }
                }
            }
            if(!_settings.ReducedMotion&&camera.orthographicSize<200)
            {
                int ports=0;foreach(var building in _game.Buildings)
                {
                    if(building.kind!=BuildingKind.Port||!building.Operational)continue;int water=_game.AdjacentWater(building.cell);if(water<0)continue;
                    Vector3 point=new Vector3(water%_game.World.Width+.5f,0,water/_game.World.Width+.5f),view=camera.WorldToViewportPoint(point);if(view.z<0||view.x<0||view.x>1||view.y<0||view.y>1)continue;
                    float phase=Mathf.Repeat(_game.ElapsedTime*.3f+building.id*.37f,1),radius=(.35f+phase*1.3f)*_game.World.ResolutionScale;
                    Color colour=new Color(.40f,.72f,.76f,.09f*(1-phase));
                    for(int segment=0;segment<16;segment++)
                    {
                        float a=segment*Mathf.PI*2/16,b=(segment+1)*Mathf.PI*2/16;Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                        Vector3 p0=point+x*radius,p1=point+x*(radius+.035f),p2=point+y*radius,p3=point+y*(radius+.035f);
                        int cell0=_game.World.At(Mathf.Clamp((int)p0.x,0,_game.World.Width-1),Mathf.Clamp((int)p0.z,0,_game.World.Height-1));
                        int cell2=_game.World.At(Mathf.Clamp((int)p2.x,0,_game.World.Width-1),Mathf.Clamp((int)p2.z,0,_game.World.Height-1));if(_game.World.Land[cell0]||_game.World.Land[cell2])continue;
                        p0.y=_game.World.SeaHeight(p0.x,p0.z,Time.time)+.18f;p1.y=p0.y;p2.y=_game.World.SeaHeight(p2.x,p2.z,Time.time)+.18f;p3.y=p2.y;Triangle(p0,p1,p2,colour);Triangle(p2,p1,p3,colour);
                    }
                    if(++ports>=24)break;
                }
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetColors(_colours);_mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
        }
        private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color colour){int n=_vertices.Count;_vertices.Add(a);_vertices.Add(b);_vertices.Add(c);_colours.Add(colour);_colours.Add(colour);_colours.Add(colour);_triangles.Add(n);_triangles.Add(n+1);_triangles.Add(n+2);}
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
