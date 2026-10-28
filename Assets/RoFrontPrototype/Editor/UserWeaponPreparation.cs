using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class UserWeaponPreparation
    {
        [MenuItem("Frontier Atlas/Prepare user rockets and silos")]
        public static void Prepare()
        {
            const string output="Assets/RoFrontPrototype/Resources/Models/Weapons";Directory.CreateDirectory(output);AssetDatabase.Refresh();
            foreach(string name in new[]{"RocketLight","RocketHeavy","LaunchPlatform","LaunchSite"})
            {
                string source="Assets/UserModels/WeaponDerived/"+name;var model=AssetDatabase.LoadAssetAtPath<GameObject>(source+".fbx");if(model==null)throw new InvalidOperationException("Falta el modelo derivado "+name);
                var instance=UnityEngine.Object.Instantiate(model);var filters=instance.GetComponentsInChildren<MeshFilter>(true);var parts=new CombineInstance[filters.Length];for(int i=0;i<filters.Length;i++)parts[i]=new CombineInstance{mesh=filters[i].sharedMesh,transform=filters[i].transform.localToWorldMatrix};
                var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);var b=mesh.bounds;bool rocket=name.StartsWith("Rocket");float scale=1/(rocket?b.size.y:Mathf.Max(b.size.x,b.size.z));Vector3 anchor=rocket?b.center:new Vector3(b.center.x,b.min.y,b.center.z);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-anchor)*scale;mesh.vertices=vertices;mesh.RecalculateBounds();
                string meshPath=output+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(old==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}UnityEngine.Object.DestroyImmediate(instance);
                var importer=(TextureImporter)AssetImporter.GetAtPath(source+"Atlas.png");importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=1024;importer.SaveAndReimport();
                string matPath=output+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(Resources.Load<Shader>("StructureAtlas"));AssetDatabase.CreateAsset(mat,matPath);}mat.enableInstancing=true;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Atlas.png"));EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();Debug.Log("USER_WEAPONS_READY two rockets and two launcher bases");
        }
    }
}
