using System.IO;
using UnityEditor;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class UserTrainPreparation
    {
        public const string ModelPath="Assets/UserModels/TradeLocomotive/locomotora_de_tren_3d.fbx";
        public const string PrefabPath="Assets/RoFrontPrototype/Resources/Models/CommercialLocomotive.prefab";
        public static void Prepare()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);if(model==null)throw new System.InvalidOperationException("Falta la locomotora original proporcionada por el usuario.");
            const string materialPath="Assets/RoFrontPrototype/Resources/Models/CommercialLocomotive.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UserModels/TradeLocomotive/locomotora_de_tren_3d.fbm/locomotora_de_tren_3d_basecolor.jpg"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.15f);material.SetFloat("_Metallic",.06f);material.enableInstancing=true;EditorUtility.SetDirty(material);
            const string smokePath="Assets/RoFrontPrototype/Resources/Models/LocomotiveSmoke.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(smokePath)==null)AssetDatabase.CreateAsset(new Material(Shader.Find("FrontierAtlas/SoftTrainSmoke")),smokePath);
            var root=new GameObject("Locomotora comercial del creador");var visual=Object.Instantiate(model,root.transform);visual.name="Modelo original sin modificar";
            foreach(var camera in visual.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera);
            foreach(var light in visual.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(light);
            var renderers=visual.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new System.InvalidOperationException("El FBX no tiene renderizadores.");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            if(bounds.size.x>bounds.size.z)visual.transform.localRotation=Quaternion.Euler(0,90,0);
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale=Vector3.one*(1.5f/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)));
            bounds=renderers[0].bounds;foreach(var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            visual.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            var exhaust=new GameObject("Escape de humo — ajuste de contenedor").transform;exhaust.SetParent(root.transform,false);exhaust.localPosition=new Vector3(0,bounds.size.y*.93f,.36f);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            Debug.Log("FRONTIER_USER_TRAIN_READY sourceUnmodified=true normalizedHeight="+bounds.size.y+" prefab="+PrefabPath);
        }
    }
}
