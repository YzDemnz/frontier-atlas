using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FrontierAtlas
{
    public sealed class StrategicMissileVisuals:MonoBehaviour
    {
        public const int MaximumMissiles=32,MaximumSmokePerMissile=48;
        private TerritorySimulation _game;private FrontierSettings _settings;private Material _smoke;
        private readonly Mesh[] _meshes=new Mesh[4];private readonly Material[] _materials=new Material[4];
        private readonly Matrix4x4[][] _silos={new Matrix4x4[1023],new Matrix4x4[1023]};private readonly int[] _siloCounts=new int[2];
        private sealed class Missile{public GameObject root;public MeshFilter mesh;public MeshRenderer renderer;public ParticleSystem smoke;public int type;}
        private readonly Dictionary<int,Missile> _visible=new Dictionary<int,Missile>();private readonly List<Missile> _pool=new List<Missile>();private readonly HashSet<int> _used=new HashSet<int>();private readonly List<int> _retire=new List<int>();
        private readonly List<ParticleSystem> _bursts=new List<ParticleSystem>();private int _burstCursor;
        public int VisibleMissiles {get;private set;}public int SmokeParticles {get;private set;}public int VisibleSilos {get;private set;}
        public void Initialize(TerritorySimulation game,FrontierSettings settings)
        {
            _game=game;_settings=settings;string[] names={"RocketLight","RocketHeavy","LaunchPlatform","LaunchSite"};for(int i=0;i<4;i++){_meshes[i]=Resources.Load<Mesh>("Models/Weapons/"+names[i]);_materials[i]=Resources.Load<Material>("Models/Weapons/"+names[i]);}
            _smoke=new Material(Resources.Load<Shader>("RocketSmoke"));game.NuclearDetonated+=OnNuclear;
        }
        private ParticleSystem MakeParticles(string name,Transform parent,bool burst=false)
        {
            var node=new GameObject(name);node.transform.SetParent(parent,false);var particles=node.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=!burst;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.scalingMode=ParticleSystemScalingMode.Shape;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=burst?24:MaximumSmokePerMissile;main.startLifetime=burst?3.2f:1.2f;main.startSpeed=burst?2.5f*_game.World.ResolutionScale:.12f*_game.World.ResolutionScale;main.startSize=new ParticleSystem.MinMaxCurve((burst?1.6f:.65f)*_game.World.ResolutionScale,(burst?3.5f:1.0f)*_game.World.ResolutionScale);main.startColor=burst?new Color(.29f,.72f,.08f,.17f):new Color(.65f,.65f,.62f,.70f);
            var emission=particles.emission;emission.rateOverTime=burst?0:40;if(burst)emission.SetBursts(new[]{new ParticleSystem.Burst(0,24)});
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=(burst?3:.025f)*_game.World.ResolutionScale;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,burst?.4f:.8f,1,2.5f));
            var colour=particles.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(burst?Color.white:new Color(1,.65f,.28f),0),new GradientColorKey(Color.white,.15f),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(.7f,.15f),new GradientAlphaKey(0,1)});colour.color=gradient;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_smoke;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;particles.useAutoRandomSeed=false;particles.randomSeed=73;return particles;
        }
        private Missile NewMissile()
        {
            var m=new Missile{root=new GameObject("Cohete y humo reutilizados")};m.root.transform.SetParent(transform,false);m.mesh=m.root.AddComponent<MeshFilter>();m.renderer=m.root.AddComponent<MeshRenderer>();m.renderer.shadowCastingMode=ShadowCastingMode.Off;m.renderer.receiveShadows=false;m.smoke=MakeParticles("Escape · humo dispersable",m.root.transform);return m;
        }
        private void DrawMissile(int id,int type,Vector3 position,Vector3 direction,float length,Camera camera)
        {
            if(VisibleMissiles>=MaximumMissiles||_meshes[type]==null)return;Vector3 view=camera.WorldToViewportPoint(position);if(view.z<0||view.x<-.05f||view.x>1.05f||view.y<-.05f||view.y>1.05f)return;
            if(!_visible.TryGetValue(id,out var m)){if(_pool.Count>0){m=_pool[_pool.Count-1];_pool.RemoveAt(_pool.Count-1);}else m=NewMissile();_visible[id]=m;m.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            m.root.SetActive(true);m.type=type;m.mesh.sharedMesh=_meshes[type];m.renderer.sharedMaterial=_materials[type];m.root.transform.SetPositionAndRotation(position,Quaternion.FromToRotation(Vector3.up,direction.sqrMagnitude>.001f?direction.normalized:Vector3.up));m.root.transform.localScale=Vector3.one*length;
            m.smoke.transform.position=position-direction.normalized*length*.45f;var emission=m.smoke.emission;emission.rateOverTime=_game.Paused||_settings.ReducedMotion?0:40;
            if(!_settings.ReducedMotion&&!m.smoke.isPlaying)m.smoke.Play();if(_settings.ReducedMotion)m.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            _used.Add(id);VisibleMissiles++;SmokeParticles+=m.smoke.particleCount;
        }
        private void LateUpdate()
        {
            if(_game==null)return;var camera=Camera.main;_used.Clear();VisibleMissiles=SmokeParticles=VisibleSilos=0;_siloCounts[0]=_siloCounts[1]=0;
            foreach(var b in _game.Buildings)if(!b.destroyed&&b.kind==BuildingKind.Silo&&VisibleSilos<1023)
            {float x=b.cell%_game.World.Width+.5f,z=b.cell/_game.World.Width+.5f;Vector3 p=new Vector3(x,_game.World.RenderedSurfaceHeight(x,z)+.12f*_game.World.ResolutionScale,z),screen=camera.WorldToViewportPoint(p);if(screen.z<0||screen.x<-.03f||screen.x>1.03f||screen.y<-.03f||screen.y>1.03f)continue;int type=b.id&1;float size=2.8f*_game.World.ResolutionScale,construction=b.Operational?1:Mathf.Lerp(.25f,1,Mathf.Clamp01(1-(b.completeAt-_game.ElapsedTime)/_game.Rules.buildSeconds));_silos[type][_siloCounts[type]++]=Matrix4x4.TRS(p,Quaternion.identity,new Vector3(size,size*construction,size));VisibleSilos++;}
            for(int i=0;i<2;i++)if(_siloCounts[i]>0&&_meshes[i+2]!=null){if(SystemInfo.supportsInstancing)Graphics.DrawMeshInstanced(_meshes[i+2],0,_materials[i+2],_silos[i],_siloCounts[i],null,ShadowCastingMode.Off,false,0,camera,LightProbeUsage.Off);else for(int j=0;j<_siloCounts[i];j++)Graphics.DrawMesh(_meshes[i+2],_silos[i][j],_materials[i+2],0,camera);}
            foreach(var p in _game.Projectiles)if(p.active){var position=_game.ProjectilePosition(p);var next=_game.ProjectilePosition(p,Mathf.Min(1,p.Progress+.002f));var direction=next-position;if(_game.World.WrapLongitude&&Mathf.Abs(direction.x)>_game.World.Width*.5f)direction.x+=direction.x>0?-_game.World.Width:_game.World.Width;DrawMissile(p.id,p.kind==WeaponKind.Hydrogen||p.kind==WeaponKind.Mega||p.kind==WeaponKind.MIRV?1:0,position,direction,2.3f*_game.World.ResolutionScale,camera);}
            foreach(var i in _game.Interceptors)if(i.active){Vector3 direction=Vector3.up;foreach(var p in _game.Projectiles)if(p.id==i.target){direction=_game.ProjectilePosition(p)-i.position;if(_game.World.WrapLongitude&&Mathf.Abs(direction.x)>_game.World.Width*.5f)direction.x+=direction.x>0?-_game.World.Width:_game.World.Width;break;}DrawMissile(-i.id,0,i.position,direction,.9f*_game.World.ResolutionScale,camera);}
            _retire.Clear();foreach(var pair in _visible)if(!_used.Contains(pair.Key)){pair.Value.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);pair.Value.root.SetActive(false);_pool.Add(pair.Value);_retire.Add(pair.Key);}foreach(int id in _retire)_visible.Remove(id);
        }
        public int NuclearBursts {get;private set;}
        private void OnNuclear(int cell,float radius)
        {
            if(_settings.ReducedMotion)return;
            ParticleSystem burst;if(_bursts.Count<4){burst=MakeParticles("Explosión nuclear · nube y destello reutilizados",transform,true);_bursts.Add(burst);}else burst=_bursts[_burstCursor++%_bursts.Count];burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            float x=cell%_game.World.Width+.5f,z=cell/_game.World.Width+.5f,scale=_game.World.ResolutionScale;
            Vector3 center=new Vector3(x,_game.World.RenderedSurfaceHeight(x,z)+1,z);burst.transform.position=center;
            var emission=burst.emission;emission.SetBursts(new ParticleSystem.Burst[0]);var main=burst.main;main.maxParticles=100;main.startLifetime=4.5f;main.startSpeed=0;main.startSize=1;
            var size=burst.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,2.4f));
            var colour=burst.colorOverLifetime;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.8f,.4f),0),new GradientColorKey(new Color(.32f,.38f,.20f),.22f),new GradientColorKey(new Color(.32f,.56f,.10f),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.5f,.25f),new GradientAlphaKey(0,1)});colour.color=gradient;
            float extent=Mathf.Clamp(radius*.6f,8*scale,55*scale);burst.Play();
            // Stem, toroidal cap and low dust ring are one bounded particle pool.
            for(int i=0;i<86;i++)
            {
                float a=i*2.399963f;Vector3 pos,velocity;float particleSize,lifetime;
                if(i<22){float h=i/22f;pos=center+Vector3.up*(h*extent*.9f);velocity=Vector3.up*(extent*.16f);particleSize=extent*.18f;lifetime=3.8f;}
                else if(i<62){float rr=extent*(.3f+.65f*Mathf.Sqrt((i-22)/40f));pos=center+new Vector3(Mathf.Cos(a)*rr,extent,Mathf.Sin(a)*rr);velocity=new Vector3(Mathf.Cos(a)*extent*.045f,extent*.11f,Mathf.Sin(a)*extent*.045f);particleSize=extent*.62f;lifetime=4.5f;}
                else {pos=center;velocity=new Vector3(Mathf.Cos(a),.02f,Mathf.Sin(a))*extent*.65f;particleSize=extent*.24f;lifetime=1.5f;}
                burst.Emit(new ParticleSystem.EmitParams{position=pos,velocity=velocity,startSize=particleSize,startLifetime=lifetime,startColor=Color.white},1);
            }
            burst.Emit(new ParticleSystem.EmitParams{position=center+Vector3.up*extent*.25f,velocity=Vector3.zero,startSize=extent*3,startLifetime=.22f,startColor=new Color(1,.86f,.54f,.85f)},1);NuclearBursts++;
        }
        private void OnDestroy(){if(_game!=null)_game.NuclearDetonated-=OnNuclear;if(_smoke!=null)Destroy(_smoke);}
    }
}
