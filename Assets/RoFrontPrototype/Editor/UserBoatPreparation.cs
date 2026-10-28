using System.IO;
using UnityEditor;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class UserBoatPreparation
    {
        public const string ModelPath="Assets/UserModels/TradeFishingBoat/barco_pesquero_3d.fbx";
        public const string PrefabPath="Assets/RoFrontPrototype/Resources/Models/CommercialFishingBoat.prefab";
        public static void Prepare()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);if(model==null)throw new System.InvalidOperationException("Falta el modelo de barco comercial proporcionado por el usuario.");
            Directory.CreateDirectory("Assets/RoFrontPrototype/Resources/Models");AssetDatabase.Refresh();
            const string materialPath="Assets/RoFrontPrototype/Resources/Models/CommercialFishingBoat.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UserModels/TradeFishingBoat/barco_pesquero_3d.fbm/barco_pesquero_3d_basecolor.jpg"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.18f);material.SetFloat("_Metallic",.05f);EditorUtility.SetDirty(material);
            material.enableInstancing=true;
            var root=new GameObject("Barco comercial del creador");var visual=Object.Instantiate(model,root.transform);visual.name="Modelo original sin reparar";
            foreach(var camera in visual.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera);
            foreach(var light in visual.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(light);
            var renderers=visual.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new System.InvalidOperationException("El FBX no contiene renderizadores.");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            if(bounds.size.x>bounds.size.z)visual.transform.localRotation=Quaternion.Euler(0,90,0);
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale=Vector3.one*(1.4f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)));
            bounds=renderers[0].bounds;foreach(var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            visual.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y-bounds.size.y*.18f,-bounds.center.z);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            Debug.Log("FRONTIER_USER_BOAT_READY sourceUnmodified=true prefab="+PrefabPath);
        }
    }
}
