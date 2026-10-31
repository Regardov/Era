using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class WorldTreeImporter
    {
        const string Source="Assets/Era/Art/Environment/Yggdrasil";
        const string Output="Assets/Era/Resources/Environment";
        [Serializable]sealed class ColorEntry{public string name;public float[] linear;public float emission;}
        static readonly Dictionary<string,Color> Palette=new Dictionary<string,Color>{
            {"YGG_Bark",Hex("66513B")},{"YGG_BarkWarm",Hex("846647")},{"YGG_BarkCrevice",Hex("493D30")},
            {"YGG_BarkPale",Hex("9E8156")},{"YGG_RootMoss",Hex("4B6237")},
            {"YGG_LeafGreen",Hex("477C47")},{"YGG_LeafLight",Hex("79A35A")},{"YGG_LeafShade",Hex("2E583B")},
            {"YGG_LeafLime",Hex("A5B66A")},{"YGG_LeafGold",Hex("BC9B50")},{"YGG_AmberSap",Hex("D9A454")}};
        static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        static Material Material(string name)
        {
            string path=Source+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Standard")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.shader=Shader.Find("Standard");material.color=Palette.TryGetValue(name,out var color)?color:Color.gray;
            material.SetFloat("_Glossiness",.08f);material.SetFloat("_Metallic",0);material.SetFloat("_Mode",0);
            material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue=2000;material.enableInstancing=true;
            if(name.Contains("AmberSap")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",material.color*.5f);}
            EditorUtility.SetDirty(material);return material;
        }
        static Bounds BoundsOf(GameObject root)
        {var all=root.GetComponentsInChildren<Renderer>(true);var box=all[0].bounds;foreach(var r in all)box.Encapsulate(r.bounds);return box;}
        public static void Prepare()
        {
            Directory.CreateDirectory(Source+"/Materials");Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var target=new GameObject("Yggdrasil");
            try
            {
                var levels=new List<LOD>();Vector3 scale=Vector3.one;
                for(int level=0;level<3;level++)
                {
                    string path=Source+"/Yggdrasil_LOD"+level+".fbx";
                    var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                    if(importer==null)throw new Exception("Missing runtime tree FBX: "+path);
                    bool changed=!importer.isReadable||importer.animationType!=ModelImporterAnimationType.None||importer.importAnimation;
                    importer.isReadable=true;importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;
                    if(changed)importer.SaveAndReimport();
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);instance.name="Tree LOD"+level;
                    instance.transform.SetParent(target.transform,false);
                    // Normalize each simplified level too: collapse can move its extreme vertex slightly.
                    {
                        var bounds=BoundsOf(instance);
                        if(bounds.max.y<=0||bounds.size.y<bounds.size.x*.35f)throw new Exception("Tree FBX axes are not Y-up");
                        float horizontal=WorldTreeLayout.CrownDiameter/Mathf.Max(bounds.size.x,bounds.size.z);
                        scale=new Vector3(horizontal,WorldTreeLayout.Height/bounds.max.y,horizontal);
                    }
                    instance.transform.localScale=Vector3.Scale(instance.transform.localScale,scale);
                    var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
                    foreach(var renderer in renderers)
                    {
                        bool leaves=WorldTreeView.IsFoliage(renderer.name);
                        renderer.gameObject.layer=leaves?WorldTreeView.FoliageLayer:WorldTreeView.WoodLayer;
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Material(m.name)).ToArray();
                        renderer.shadowCastingMode=leaves?ShadowCastingMode.Off:ShadowCastingMode.On;
                        renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
                        renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    }
                    levels.Add(new LOD(level==0?.65f:level==1?.22f:.005f,renderers){fadeTransitionWidth=.12f});
                }
                var group=target.AddComponent<LODGroup>();group.SetLODs(levels.ToArray());group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
                var allBounds=BoundsOf(target);
                if(Mathf.Abs(allBounds.max.y-WorldTreeLayout.Height)>.25f||Mathf.Max(allBounds.size.x,allBounds.size.z)>WorldTreeLayout.CrownDiameter+1)
                    throw new Exception("Tree dimensions failed layout contract: "+allBounds);
                PrefabUtility.SaveAsPrefabAsset(target,Output+"/Yggdrasil.prefab");AssetDatabase.SaveAssets();
                Debug.Log("ERA_WORLD_TREE_IMPORTED height="+allBounds.max.y+" crown="+Mathf.Max(allBounds.size.x,allBounds.size.z));
            }
            finally{Object.DestroyImmediate(target);}
        }
    }
}
