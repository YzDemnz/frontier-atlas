using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class CommercialTrainVisuals:MonoBehaviour
    {
        private TerritorySimulation _game;private FrontierSettings _settings;private GameObject _prefab;private Material _smokeMaterial;
        private readonly Dictionary<int,Train> _visible=new Dictionary<int,Train>();private readonly List<Train> _pool=new List<Train>();private readonly List<int> _retired=new List<int>();private readonly HashSet<int> _used=new HashSet<int>();
        private sealed class Train{public GameObject model;public ParticleSystem smoke;public Vector3 velocity;public bool positioned;}
        public int ActiveModels {get;private set;}
        public int SmokeParticles {get;private set;}
        public GameObject ModelForRoute(int id)=>_visible.TryGetValue(id,out var train)?train.model:null;
        public void Initialize(TerritorySimulation game,FrontierSettings settings)
        {
            _game=game;_settings=settings;_prefab=Resources.Load<GameObject>("Models/CommercialLocomotive");
            var template=Resources.Load<Material>("Models/LocomotiveSmoke");if(template!=null)_smokeMaterial=new Material(template);
        }
        private Train Create()
        {
            var train=new Train{model=Instantiate(_prefab,transform)};var anchor=train.model.transform.Find("Escape de humo — ajuste de contenedor");
            if(anchor!=null&&_smokeMaterial!=null)
            {
                train.smoke=anchor.gameObject.AddComponent<ParticleSystem>();train.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=train.smoke.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=8;main.startLifetime=1.4f;main.startSpeed=.22f;main.startSize=.10f*_game.World.ResolutionScale;main.startColor=new Color(.025f,.025f,.028f,.065f);main.scalingMode=ParticleSystemScalingMode.Shape;
                var emission=train.smoke.emission;emission.rateOverTime=1.3f;var shape=train.smoke.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.035f;
                var velocity=train.smoke.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.055f;velocity.y=.42f;velocity.z=.025f;
                var size=train.smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(1,1.8f)));
                var colour=train.smoke.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.18f),new GradientAlphaKey(0,1)});colour.color=gradient;
                train.smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=_smokeMaterial;train.smoke.useAutoRandomSeed=false;train.smoke.randomSeed=73;
            }
            return train;
        }
        private void LateUpdate()
        {
            if(_game==null||_prefab==null)return;var camera=Camera.main;_used.Clear();ActiveModels=SmokeParticles=0;
            if(camera.orthographicSize<220)foreach(var route in _game.RenderableTrains())
            {
                Vector3 position=route.position,direction=route.direction;position.y=_game.World.RenderedSurfaceHeight(position.x,position.z)+.18f;
                Vector3 viewport=camera.WorldToViewportPoint(position);if(viewport.z<=0||viewport.x<-.03f||viewport.x>1.03f||viewport.y<-.03f||viewport.y>1.03f||ActiveModels>=16)continue;
                if(!_visible.TryGetValue(route.id,out var train)){if(_pool.Count>0){train=_pool[_pool.Count-1];_pool.RemoveAt(_pool.Count-1);}else train=Create();_visible[route.id]=train;train.positioned=false;}
                train.model.SetActive(true);_used.Add(route.id);ActiveModels++;train.model.transform.localScale=Vector3.one*(_game.World.ResolutionScale*.70f);
                if(!train.positioned){train.model.transform.position=position;train.velocity=Vector3.zero;train.model.transform.rotation=Quaternion.LookRotation(direction.sqrMagnitude>.001f?direction:Vector3.forward);train.positioned=true;}
                train.model.transform.position=Vector3.SmoothDamp(train.model.transform.position,position,ref train.velocity,.075f,float.PositiveInfinity,Time.unscaledDeltaTime);
                if(direction.sqrMagnitude>.001f)train.model.transform.rotation=Quaternion.Slerp(train.model.transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-12*Time.unscaledDeltaTime));
                if(train.smoke!=null)
                {
                    bool visible=camera.orthographicSize<70&&!_settings.ReducedMotion;
                    if(visible&&!train.smoke.isPlaying)train.smoke.Play();else if(!visible&&train.smoke.isPlaying)train.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    if(visible){var emission=train.smoke.emission;emission.rateOverTime=route.docking?.4f:1.3f;}SmokeParticles+=train.smoke.particleCount;
                }
            }
            _retired.Clear();foreach(var pair in _visible)if(!_used.Contains(pair.Key)){if(pair.Value.smoke!=null)pair.Value.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);pair.Value.model.SetActive(false);_pool.Add(pair.Value);_retired.Add(pair.Key);}
            foreach(int id in _retired)_visible.Remove(id);
        }
        private void OnDestroy(){if(_smokeMaterial!=null)Destroy(_smokeMaterial);}
    }
}
