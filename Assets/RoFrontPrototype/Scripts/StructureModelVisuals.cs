using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace FrontierAtlas
{
    public sealed class StructureModelVisuals:MonoBehaviour
    {
        private TerritorySimulation _game;private readonly Mesh[] _meshes=new Mesh[6];private readonly Material[] _materials=new Material[2];
        public const int MaximumVisible=1023;public const float ModelFootprint=2.4f;
        private readonly Matrix4x4[][] _matrices=new Matrix4x4[6][];private readonly int[] _counts=new int[6];
        public readonly HashSet<int> RenderedBuildings=new HashSet<int>();public int VisibleModels {get;private set;}public int DrawCalls {get;private set;}
        public void Initialize(TerritorySimulation game)
        {
            _game=game;for(int kind=0;kind<2;kind++){string name=kind==0?"City":"Factory";_materials[kind]=Resources.Load<Material>("Models/Structures/"+name);for(int lod=0;lod<3;lod++){int i=kind*3+lod;_meshes[i]=Resources.Load<Mesh>("Models/Structures/"+name+"_LOD"+lod);_matrices[i]=new Matrix4x4[MaximumVisible];}}
        }
        private void LateUpdate()
        {
            RenderedBuildings.Clear();VisibleModels=DrawCalls=0;System.Array.Clear(_counts,0,6);var camera=Camera.main;if(_game==null||camera==null)return;
            for(int pass=0;pass<2;pass++)foreach(var b in _game.Buildings)
            {
                if(b.destroyed||(b.kind!=BuildingKind.City&&b.kind!=BuildingKind.Factory)||(pass==0)!=(b.owner==_game.PlayerFaction)||VisibleModels>=MaximumVisible)continue;
                float x=b.cell%_game.World.Width+.5f,z=b.cell/_game.World.Width+.5f;var position=new Vector3(x,_game.World.RenderedSurfaceHeight(x,z)+.12f*_game.World.ResolutionScale,z);Vector3 viewport=camera.WorldToViewportPoint(position);
                if(viewport.z<0||viewport.x<-.03f||viewport.x>1.03f||viewport.y<-.03f||viewport.y>1.03f)continue;
                // A stable instanced mesh avoids silhouette, pivot and scale pops while zooming.
                int lod=1,kind=b.kind==BuildingKind.City?0:1,index=kind*3+lod;if(_meshes[index]==null||_materials[kind]==null)continue;
                float size=ModelFootprint*_game.World.ResolutionScale*(1+Mathf.Min(4,b.level-1)*.04f);float construction=b.Operational?1:Mathf.Lerp(.25f,1,Mathf.Clamp01(1-(b.completeAt-_game.ElapsedTime)/_game.Rules.buildSeconds));
                _matrices[index][_counts[index]++]=Matrix4x4.TRS(position,Quaternion.identity,new Vector3(size,size*construction,size));RenderedBuildings.Add(b.id);VisibleModels++;
            }
            for(int i=0;i<6;i++)if(_counts[i]>0)
            {
                // One mesh and one atlas per type/LOD, no per-building GameObjects or colliders.
                if(SystemInfo.supportsInstancing)Graphics.DrawMeshInstanced(_meshes[i],0,_materials[i/3],_matrices[i],_counts[i],null,ShadowCastingMode.Off,false,0,camera,LightProbeUsage.Off);
                else for(int j=0;j<_counts[i];j++)Graphics.DrawMesh(_meshes[i],_matrices[i][j],_materials[i/3],0,camera,0,null,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
                DrawCalls++;
            }
        }
    }
}
