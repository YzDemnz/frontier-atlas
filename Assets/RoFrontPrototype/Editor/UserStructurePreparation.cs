using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class UserStructurePreparation
    {
        public static void Prepare()
        {
            const string output="Assets/RoFrontPrototype/Resources/Models/Structures";Directory.CreateDirectory(output);AssetDatabase.Refresh();
            foreach(string name in new[]{"City","Factory"})
            {
                string source="Assets/UserModels/StructureDerived/"+name;
                var shader=Resources.Load<Shader>("StructureAtlas");if(shader==null)throw new InvalidOperationException("Falta el shader de edificios.");
                string matPath=output+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}mat.shader=shader;mat.enableInstancing=true;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Atlas.png"));EditorUtility.SetDirty(mat);
                var importer=AssetImporter.GetAtPath(source+"Atlas.png") as TextureImporter;if(importer!=null){importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=2048;importer.SaveAndReimport();}
                for(int lod=0;lod<3;lod++)
                {
                    string fbx=source+"_LOD"+lod+".fbx";var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);if(model==null)throw new InvalidOperationException("Falta el modelo derivado: "+fbx);
                    var instance=UnityEngine.Object.Instantiate(model);var filters=instance.GetComponentsInChildren<MeshFilter>(true);var parts=new CombineInstance[filters.Length];for(int i=0;i<filters.Length;i++)parts[i]=new CombineInstance{mesh=filters[i].sharedMesh,transform=filters[i].transform.localToWorldMatrix};
                    var mesh=new Mesh{name=name+"_LOD"+lod,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true);var bounds=mesh.bounds;float scale=1/Mathf.Max(bounds.size.x,bounds.size.z);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;mesh.vertices=vertices;mesh.RecalculateBounds();
                    string path=output+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            // Explicitly keep instanced variants for meshes submitted dynamically at runtime.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var stripping=graphics.FindProperty("m_InstancingStripping");if(stripping!=null){int keep=Array.FindIndex(stripping.enumNames,n=>n.IndexOf("KeepAll",StringComparison.OrdinalIgnoreCase)>=0);if(keep>=0)stripping.enumValueIndex=keep;graphics.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();Debug.Log("FRONTIER_STRUCTURES_READY 2 types 3 LODs 1 material per type");
        }
    }
}
