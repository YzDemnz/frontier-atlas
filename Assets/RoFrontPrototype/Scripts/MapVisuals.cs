using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontierAtlas
{
    public sealed class MapVisuals : MonoBehaviour
    {
        public PowerBalance Powers {get;private set;}
        public int ActivePulses {get;private set;}
        public bool UsesGeographicBorders=>false;
        private Texture2D _terrain,_data,_owners,_upload;
        internal Texture CoastMask=>_terrain;
        private Texture2D _radiation;
        private readonly HashSet<int> _radiationChunks=new HashSet<int>();
        private readonly HashSet<int> _dirtyChunks=new HashSet<int>();
        private int _chunkColumns;
        public MeshCollider SurfaceCollider {get;private set;}
        private Material _material,_pulseMaterial;
        private Material _oceanMaterial;
        private Mesh _oceanMesh;
        private Mesh _mesh,_pulseMesh;
        private HashSet<int>[] _factionChunks;
        private readonly Color32[] _uploadPixels=new Color32[1024*256];
        public int LastUploadBatches {get;private set;}
        private static WorldGrid _cachedWorld;
        private static Mesh _cachedMesh,_cachedOcean;
        private static Texture2D _cachedTerrain,_cachedData;
        private Color32[] _factionPixels;private byte[] _regionIds;private int _nextRegionId;
        private TerritorySimulation _game;
        private WorldGrid _world;
        private FrontierSettings _settings;
        private FrontierAudio _audio;
        private float _nextRefresh,_nextPromotionSound;
        private bool _dirty=true;
        private readonly float[] _pulseStart=new float[32],_pulseLife=new float[32];
        private float[] _nextFactionPulse;
        private readonly Vector3[] _pulsePosition=new Vector3[32];
        private readonly Color[] _pulseColor=new Color[32];
        private int _pulseHead;
        private Vector3[] _vertices;
        private Vector2[] _uv,_timings;
        private Color[] _colors;

        public void Initialize(TerritorySimulation game,FrontierSettings settings=null,FrontierAudio audio=null)
        {
            _game=game;_world=game.World;_settings=settings??new FrontierSettings();_audio=audio;Powers=new PowerBalance(game);
            _nextFactionPulse=new float[game.FactionCount];
            if(_cachedWorld!=_world)
            {foreach(var resource in new UnityEngine.Object[]{_cachedMesh,_cachedOcean,_cachedTerrain,_cachedData})if(resource!=null)Destroy(resource);_cachedWorld=_world;_cachedMesh=_cachedOcean=null;_cachedTerrain=_cachedData=null;}
            if(_cachedMesh==null)_cachedMesh=BuildSurface();_mesh=_cachedMesh;
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            SurfaceCollider=gameObject.AddComponent<MeshCollider>();SurfaceCollider.sharedMesh=_mesh;
            var template=Resources.Load<Material>("FrontierMap");
            var shader=Shader.Find("FrontierAtlas/StrategicEarth");
            if(shader==null)throw new InvalidOperationException("StrategicEarth shader missing.");
            _material=template!=null?new Material(template):new Material(shader);_material.shader=shader;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            BuildTerrain();
            _regionIds=new byte[game.FactionCount];_factionPixels=new Color32[game.FactionCount];_factionChunks=new HashSet<int>[game.FactionCount];_chunkColumns=Mathf.CeilToInt(_world.Width/16f);
            for(int f=0;f<game.FactionCount;f++)_factionChunks[f]=new HashSet<int>();
            for(int id=0;id<game.Owners.Length;id++)if(game.Owners[id]>=0){int chunk=id/_world.Width/16*_chunkColumns+id%_world.Width/16;_factionChunks[game.Owners[id]].Add(chunk);_dirtyChunks.Add(chunk);}
            var blank=new Color32[_world.Land.Length];_owners=Texture(_world.Width,_world.Height,"Propiedad territorial",FilterMode.Point,false);_owners.SetPixels32(blank);_owners.Apply(false,true);
            if(game.CampaignEnabled){_radiation=Texture(_world.Width,_world.Height,"Radiación territorial",FilterMode.Point,true);_radiation.SetPixels32(blank);_radiation.Apply(false,true);_material.SetTexture("_RadiationMap",_radiation);game.StrategicCellChanged+=OnStrategicCell;game.StrategicEventRaised+=OnStrategicEvent;foreach(int cell in game.Radiation.Keys)OnStrategicCell(cell);}
            _upload=Texture(1024,256,"Atlas de actualizaciones agrupadas",FilterMode.Point,false);
            _material.SetTexture("_BaseMap",_terrain);_material.SetTexture("_TerrainData",_data);_material.SetTexture("_OwnerMap",_owners);
            var atlas=Resources.Load<Texture2D>("Cartography/EarthRelief8K");
            if(atlas!=null){_material.SetTexture("_EarthAtlas",atlas);_material.SetFloat("_UseAtlas",1);}
            _material.SetFloat("_GridScale",_world.ResolutionScale);
            _material.SetFloat("_QuickMatch",game.IsQuickMatch?1:0);if(game.IsQuickMatch){_material.SetFloat("_UseAtlas",0);_material.SetFloat("_MapExposure",1);_material.SetFloat("_MapContrast",1);}
            PrepareOcean();
            PreparePulses();
            _game.TerritoryCaptured+=OnCapture;_game.BattleOccurred+=OnBattle;Powers.Promoted+=OnPromotion;
            RefreshOwners();ApplySettings();
            var fleets=gameObject.AddComponent<NavalVisuals>();fleets.Initialize(game,_settings,Powers);
            if(game.CampaignEnabled)gameObject.AddComponent<CampaignVisuals>().Initialize(game,Powers,_settings);
            game.FleetLaunched+=OnSail;game.Landed+=OnLand;
        }
        private void PrepareOcean()
        {
            _material.SetFloat("_SurfaceKind",0);_oceanMaterial=new Material(_material);_oceanMaterial.SetFloat("_SurfaceKind",1);
            if(_cachedOcean==null){_cachedOcean=Instantiate(_mesh);_cachedOcean.name="Superficie oceánica compartida";var vertices=_cachedOcean.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y=0;_cachedOcean.vertices=vertices;_cachedOcean.RecalculateBounds();}
            _oceanMesh=_cachedOcean;
            var go=new GameObject("Navigable world ocean");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=_oceanMesh;go.AddComponent<MeshRenderer>().sharedMaterial=_oceanMaterial;
        }
        private void OnSail(int faction){if(faction!=_game.PlayerFaction&&_game.IsNavalThreat(faction,_game.PlayerFaction))_audio?.IncomingInvasion();}
        private void OnLand(int faction){ }
        private void OnStrategicCell(int cell){_radiationChunks.Add((cell/_world.Width/16)*_chunkColumns+cell%_world.Width/16);_dirty=true;}
        private void OnStrategicEvent(StrategicEvent change)
        {if((change.kind==StrategicEventKind.Explosion||change.kind==StrategicEventKind.Interception)&&change.cell>=0)EmitPulse(change.cell,change.kind==StrategicEventKind.Explosion?(_game.IsIrradiated(change.cell)?new Color(.39f,.95f,.16f):new Color(1,.45f,.19f)):new Color(.25f,.9f,.95f),Mathf.Max(4,change.radius),change.kind==StrategicEventKind.Explosion?4:1);}
        private Mesh BuildSurface()
        {
            const int nx=WorldGrid.SurfaceColumns,ny=WorldGrid.SurfaceRows;var vertices=new Vector3[(nx+1)*(ny+1)];var uv=new Vector2[vertices.Length];var triangles=new int[nx*ny*6];
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {int id=y*(nx+1)+x;float px=x/(float)nx*_world.Width,pz=y/(float)ny*_world.Height;vertices[id]=new Vector3(px,_world.SurfaceHeight(px,pz),pz);uv[id]=new Vector2(x/(float)nx,y/(float)ny);}
            int t=0;for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {int a=y*(nx+1)+x,b=a+nx+1;triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;}
            var mesh=new Mesh{name="World relief",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Texture2D Texture(int width,int height,string name,FilterMode filter=FilterMode.Bilinear,bool linear=true)
            =>new Texture2D(width,height,TextureFormat.RGBA32,false,linear){name=name,filterMode=filter,wrapMode=TextureWrapMode.Clamp};
        private void BuildTerrain()
        {
            if(_cachedTerrain!=null){_terrain=_cachedTerrain;_data=_cachedData;return;}
            int count=_world.Land.Length;var distance=new int[count];var queue=new Queue<int>();
            for(int id=0;id<count;id++)
            {
                distance[id]=_world.Land[id]?0:10000;
            }
            foreach(int id in _world.CoastCells)queue.Enqueue(id);
            while(queue.Count>0)
            {
                int cell=queue.Dequeue();if(distance[cell]>=24*_world.ResolutionScale)continue;
                _world.ForEachNeighbor(cell,n=>{if(distance[n]>distance[cell]+1){distance[n]=distance[cell]+1;queue.Enqueue(n);}});
            }
            var basePixels=new Color32[count];var dataPixels=new Color32[count];
            for(int id=0;id<count;id++)
            {
                byte coastCoverage=CoastCoverage.Sample(_world,id);
                if(!_world.Land[id]){basePixels[id]=new Color32(0,0,0,coastCoverage);dataPixels[id]=new Color(0,CoastCoverage.MarineDepth(_world,distance,id),0,0);continue;}
                float moisture=_world.Moisture[id],height=_world.Elevation[id];
                Color desert=Color.Lerp(new Color(.61f,.46f,.27f),new Color(.75f,.61f,.39f),Mathf.PerlinNoise(id%_world.Width*.10f/_world.ResolutionScale,id/_world.Width*.10f/_world.ResolutionScale));
                Color forest=Color.Lerp(new Color(.13f,.24f,.17f),new Color(.21f,.31f,.20f),Mathf.PerlinNoise(id%_world.Width*.25f/_world.ResolutionScale,id/_world.Width*.25f/_world.ResolutionScale));
                Color land=Color.Lerp(desert,forest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.69f,moisture)));
                land=Color.Lerp(land,new Color(.50f,.46f,.38f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,.9f,height)));
                float snow=Mathf.Max(Mathf.InverseLerp(61,79,Mathf.Abs(_world.Latitude(id))),Mathf.InverseLerp(.73f,.98f,height));
                land=Color.Lerp(land,new Color(.79f,.85f,.84f),Mathf.Clamp01(snow));
                land.a=_world.Land[id]?1:0;
                basePixels[id]=land;basePixels[id].a=coastCoverage;
                dataPixels[id]=new Color(height,Mathf.Clamp01(distance[id]/(20f*_world.ResolutionScale)),moisture,_world.Land[id]?1:0);
            }
            _terrain=Texture(_world.Width,_world.Height,"World biome colour",FilterMode.Bilinear,false);_terrain.SetPixels32(basePixels);_terrain.Apply(false,true);
            _data=Texture(_world.Width,_world.Height,"World relief and bathymetry");_data.SetPixels32(dataPixels);_data.Apply(false,true);_cachedTerrain=_terrain;_cachedData=_data;
        }
        private void PreparePulses()
        {
            var go=new GameObject("Pooled expansion waves");go.transform.SetParent(transform,false);
            _pulseMesh=new Mesh{name="32 pooled expansion pulses"};_pulseMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh=_pulseMesh;
            var template=Resources.Load<Material>("FrontierPulse");var shader=Shader.Find("FrontierAtlas/ExpansionPulse");
            if(shader==null)throw new InvalidOperationException("ExpansionPulse shader missing.");
            _pulseMaterial=template!=null?new Material(template):new Material(shader);
            go.AddComponent<MeshRenderer>().sharedMaterial=_pulseMaterial;
            _vertices=new Vector3[128];_uv=new Vector2[128];_timings=new Vector2[128];_colors=new Color[128];
            var triangles=new int[192];
            for(int p=0;p<32;p++)
            {
                int v=p*4,t=p*6;_uv[v]=Vector2.zero;_uv[v+1]=Vector2.right;_uv[v+2]=Vector2.one;_uv[v+3]=Vector2.up;
                triangles[t]=v;triangles[t+1]=v+3;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;
                _pulseStart[p]=-100;
            }
            _pulseMesh.vertices=_vertices;_pulseMesh.uv=_uv;_pulseMesh.uv2=_timings;_pulseMesh.colors=_colors;_pulseMesh.triangles=triangles;
            _pulseMesh.bounds=new Bounds(new Vector3(_world.Width*.5f,0,_world.Height*.5f),new Vector3(_world.Width+30,20,_world.Height+30));
        }
        public void EmitPulse(int cell,Color color,float radius=3.4f,float lifetime=.9f)
        {
            if(_settings.ReducedMotion)return;
            int p=_pulseHead++%32,v=p*4;
            var center=new Vector3(cell%_world.Width+.5f,_world.SurfaceHeight(cell%_world.Width,cell/_world.Width)+.6f,cell/_world.Width+.5f);
            _pulsePosition[p]=center;_pulseColor[p]=color;_pulseStart[p]=Time.time;_pulseLife[p]=lifetime;
            _vertices[v]=center+new Vector3(-radius,0,-radius);_vertices[v+1]=center+new Vector3(radius,0,-radius);
            _vertices[v+2]=center+new Vector3(radius,0,radius);_vertices[v+3]=center+new Vector3(-radius,0,radius);
            for(int i=0;i<4;i++){_timings[v+i]=new Vector2(Time.time,lifetime);_colors[v+i]=color;}
            _pulseMesh.vertices=_vertices;_pulseMesh.uv2=_timings;_pulseMesh.colors=_colors;
        }
        private void OnCapture(int faction,int cell,int previous)
        {
            if(previous==_game.PlayerFaction&&faction!=_game.PlayerFaction)_audio?.IncomingAttack();
            _dirtyChunks.Add((cell/_world.Width/16)*_chunkColumns+(cell%_world.Width/16));
            if(faction<0){_dirty=true;return;}
            _factionChunks[faction].Add((cell/_world.Width/16)*_chunkColumns+cell%_world.Width/16);
            _dirty=true;
            if(Time.unscaledTime>=_nextFactionPulse[faction]&&(faction==_game.PlayerFaction||Powers.Tiers[faction]>0))
            {
                _nextFactionPulse[faction]=Time.unscaledTime+.18f;EmitPulse(cell,Powers.DisplayColors[faction],faction==_game.PlayerFaction?3.4f:2.4f);
            }
        }
        private void OnBattle(int attacker,int defender,float losses)
        {
            if(attacker==_game.PlayerFaction||defender==_game.PlayerFaction)
            {EmitPulse(_game.LastAdvanceCells[attacker],new Color(1,.42f,.22f),4.5f,.65f);if(defender==_game.PlayerFaction)_audio?.IncomingAttack();}
        }
        private void OnPromotion(int faction){_dirty=true;if(Time.unscaledTime>_nextPromotionSound){_nextPromotionSound=Time.unscaledTime+12;_audio?.Play("promotion",.22f);}}
        public void ApplySettings()
        {
            if(_material==null)return;_material.SetFloat("_Detail",_settings.Quality==0? .35f:1);
            _material.SetFloat("_Motion",_settings.ReducedMotion?0:1);_pulseMaterial.SetFloat("_Motion",_settings.ReducedMotion?0:1);
            if(_oceanMaterial!=null){_oceanMaterial.SetFloat("_Motion",_settings.ReducedMotion?0:1);_oceanMaterial.SetFloat("_Detail",_settings.Quality==0? .35f:1);}
            if(_settings.ReducedMotion)
            {
                for(int i=0;i<_colors.Length;i++)_colors[i]=Color.clear;
                _pulseMesh.colors=_colors;
            }
        }
        public void Invalidate()=>_dirty=true;
        private void Update()
        {
            if(_game==null)return;
            Powers.Update();ActivePulses=0;
            for(int p=0;p<32;p++)if(Time.time-_pulseStart[p]<_pulseLife[p])ActivePulses++;
            if(Time.unscaledTime<_nextRefresh||(!_dirty&&!_game.Changed))return;
            _nextRefresh=Time.unscaledTime+.12f;RefreshOwners();
        }
        private struct TileUpload{public int chunk;public bool radiation;public TileUpload(int c,bool r){chunk=c;radiation=r;}}
        private void RefreshOwners()
        {
            for(int f=0;f<_factionPixels.Length;f++)
            {
                Color colour=Powers.DisplayColors[f];if(_regionIds[f]==0&&_game.TerritoryCounts[f]>0){if(_nextRegionId>=255)throw new InvalidOperationException("Demasiadas regiones de color activas.");_regionIds[f]=(byte)++_nextRegionId;}colour.a=_regionIds[f]/255f;Color32 pixel=colour;
                if(!pixel.Equals(_factionPixels[f]))foreach(int chunk in _factionChunks[f])_dirtyChunks.Add(chunk);_factionPixels[f]=pixel;
            }
            var tiles=new List<TileUpload>(_dirtyChunks.Count+_radiationChunks.Count);
            foreach(int chunk in _dirtyChunks)tiles.Add(new TileUpload(chunk,false));if(_radiation!=null)foreach(int chunk in _radiationChunks)tiles.Add(new TileUpload(chunk,true));
            LastUploadBatches=0;
            for(int batch=0;batch<tiles.Count;batch+=1024)
            {
                int count=Mathf.Min(1024,tiles.Count-batch);
                for(int i=0;i<count;i++)
                {
                    var tile=tiles[batch+i];int x=tile.chunk%_chunkColumns*16,y=tile.chunk/_chunkColumns*16,tx=i%64*16,ty=i/64*16;
                    for(int dy=0;dy<16;dy++)for(int dx=0;dx<16;dx++)
                    {
                        if(x+dx>=_world.Width||y+dy>=_world.Height)continue;int id=(y+dy)*_world.Width+x+dx,faction=_game.Owners[id];
                        _uploadPixels[(ty+dy)*1024+tx+dx]=tile.radiation?_game.RadiationPixel(id):faction>=0?_factionPixels[faction]:new Color32(0,0,0,0);
                    }
                }
                _upload.SetPixels32(_uploadPixels);_upload.Apply(false,false);LastUploadBatches++;
                for(int i=0;i<count;i++)
                {var tile=tiles[batch+i];int x=tile.chunk%_chunkColumns*16,y=tile.chunk/_chunkColumns*16;Graphics.CopyTexture(_upload,0,0,i%64*16,i/64*16,Mathf.Min(16,_world.Width-x),Mathf.Min(16,_world.Height-y),tile.radiation?_radiation:_owners,0,0,x,y);}
            }
            _radiationChunks.Clear();_dirtyChunks.Clear();_game.ClearChanged();_dirty=false;
        }
        private void OnDestroy()
        {
            if(_game!=null){_game.TerritoryCaptured-=OnCapture;_game.BattleOccurred-=OnBattle;_game.FleetLaunched-=OnSail;_game.Landed-=OnLand;_game.StrategicCellChanged-=OnStrategicCell;_game.StrategicEventRaised-=OnStrategicEvent;}
            if(Powers!=null)Powers.Promoted-=OnPromotion;
            foreach(var texture in new[]{_owners,_upload,_radiation})if(texture!=null)Destroy(texture);
            if(_material!=null)Destroy(_material);if(_pulseMaterial!=null)Destroy(_pulseMaterial);
            if(_oceanMaterial!=null)Destroy(_oceanMaterial);
            if(_pulseMesh!=null)Destroy(_pulseMesh);
        }
    }
}
