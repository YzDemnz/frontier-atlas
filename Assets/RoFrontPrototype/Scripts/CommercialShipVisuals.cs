using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class CommercialShipVisuals : MonoBehaviour
    {
        private TerritorySimulation _game;private FrontierSettings _settings;private GameObject _prefab;
        private readonly Dictionary<int,GameObject> _visible=new Dictionary<int,GameObject>();
        private readonly List<GameObject> _pool=new List<GameObject>();private readonly HashSet<int> _used=new HashSet<int>();
        public int ActiveModels {get;private set;}
        public GameObject ModelForRoute(int id)=>_visible.TryGetValue(id,out var model)?model:null;
        public void Initialize(TerritorySimulation game,FrontierSettings settings){_game=game;_settings=settings;_prefab=Resources.Load<GameObject>("Models/CommercialFishingBoat");}
        private void LateUpdate()
        {
            if(_game==null||!_game.CampaignEnabled||_prefab==null)return;var camera=Camera.main;_used.Clear();ActiveModels=0;
            foreach(var route in _game.TradeRoutes)
            {
                if(!route.naval||route.disabledUntil>_game.ElapsedTime)continue;
                Position(_game,route,out Vector3 position,out Vector3 direction);Vector3 viewport=camera.WorldToViewportPoint(position);
                if(viewport.z<=0||viewport.x<-.04f||viewport.x>1.04f||viewport.y<-.04f||viewport.y>1.04f||ActiveModels>=24)continue;
                if(!_visible.TryGetValue(route.id,out var boat))
                {
                    if(_pool.Count>0){boat=_pool[_pool.Count-1];_pool.RemoveAt(_pool.Count-1);}else boat=Instantiate(_prefab,transform);
                    _visible[route.id]=boat;
                }
                _used.Add(route.id);boat.SetActive(true);ActiveModels++;
                float time=_settings.ReducedMotion?0:_game.ElapsedTime,phase=route.id*1.91f;
                float bob=_settings.ReducedMotion?0:Mathf.Sin(time*1.1f+phase)*.075f;
                position.y=_game.World.SeaHeight(position.x,position.z,Time.time,_settings.ReducedMotion)+bob+.16f;
                Quaternion heading=Quaternion.LookRotation(direction.sqrMagnitude>.001f?direction:Vector3.forward);
                boat.transform.position=position;Quaternion wanted=heading*Quaternion.Euler(_settings.ReducedMotion?0:Mathf.Sin(time*.7f+phase)*1.6f,0,_settings.ReducedMotion?0:Mathf.Sin(time*.9f+phase)*2.4f);boat.transform.rotation=Quaternion.Slerp(boat.transform.rotation,wanted,1-Mathf.Exp(-4*Time.unscaledDeltaTime));
                boat.transform.localScale=Vector3.one*(_game.World.ResolutionScale*.7f);
            }
            var retired=new List<int>();foreach(var pair in _visible)if(!_used.Contains(pair.Key)){pair.Value.SetActive(false);_pool.Add(pair.Value);retired.Add(pair.Key);}
            foreach(int id in retired)_visible.Remove(id);
        }
        public static void Position(TerritorySimulation game,TradeRoute route,out Vector3 position,out Vector3 direction)
        {
            if(route.distances==null)game.PrepareTradePath(route);
            float target=(route.reverse?1-route.Progress:route.Progress)*route.length;int low=0,high=route.path.Count-1;
            while(low+1<high){int mid=(low+high)/2;if(route.distances[mid]<=target)low=mid;else high=mid;}
            int a=low,b=Mathf.Min(a+1,route.path.Count-1);float fraction=route.distances[b]>route.distances[a]?(target-route.distances[a])/(route.distances[b]-route.distances[a]):0;
            int idA=route.path[a],idB=route.path[b];Vector3 vA=new Vector3(idA%game.World.Width+.5f,0,idA/game.World.Width+.5f),vB=new Vector3(idB%game.World.Width+.5f,0,idB/game.World.Width+.5f);
            if(Mathf.Abs(vB.x-vA.x)>game.World.Width*.5f)vB.x+=vB.x<vA.x?game.World.Width:-game.World.Width;
            position=Vector3.Lerp(vA,vB,fraction);position.x=Mathf.Repeat(position.x,game.World.Width);direction=(vB-vA)*(route.reverse?-1:1);
            if(direction.sqrMagnitude<.001f&&a>0){int previous=route.path[a-1];direction=(vA-new Vector3(previous%game.World.Width+.5f,0,previous/game.World.Width+.5f))*(route.reverse?-1:1);}
        }
    }
}
