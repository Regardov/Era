using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class EchoWandBuilder
    {
        const string Asset="Assets/Era/Generated/EchoWand-v029.asset",Prefab="Assets/Era/Resources/Weapons/EchoWand.prefab";
        public static void Prepare()
        {
            if(File.Exists(Asset)&&File.Exists(Prefab))return;
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,Asset);
            var d=new WeaponAssetBuilder.Draft();
            d.Tube(new[]{Vector3.zero,Vector3.up*.18f,Vector3.up*.49f,Vector3.up*.59f},new[]{.022f,.019f,.013f,.009f},new Color(.16f,.13f,.12f),false,10);
            for(int i=0;i<3;i++)d.Tube(new[]{Vector3.up*(.035f+i*.04f),Vector3.up*(.050f+i*.04f)},new[]{.028f,.028f},new Color(.55f,.4f,.25f),false,10);
            d.Tube(new[]{Vector3.up*.48f,Vector3.up*.52f},new[]{.022f,.018f},new Color(.64f,.44f,.20f),true,10);
            d.Tube(new[]{Vector3.up*.51f,Vector3.up*.58f,Vector3.up*.65f},new[]{.001f,.034f,.001f},new Color(.22f,.64f,.68f),true,6);
            var mesh=d.Mesh("Echo / short carved storm wand");var material=new Material(Shader.Find("Era/Character Surface")){name="Echo wand / dark wood brass and storm crystal"};
            AssetDatabase.AddObjectToAsset(mesh,library);AssetDatabase.AddObjectToAsset(material,library);
            var profile=ScriptableObject.CreateInstance<WeaponGripProfile>();profile.name="Echo / one-handed wand";profile.kind=WeaponKind.LightningStaff;
            profile.primaryPoint=new Vector3(0,.055f,0);profile.fingerClosure=.85f;profile.aimEuler=new Vector3(0,0,-8);profile.twoHanded=false;
            AssetDatabase.AddObjectToAsset(profile,library);
            var root=new GameObject("EchoWand");var surface=new GameObject("Carved storm wand");surface.transform.SetParent(root.transform,false);
            surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=material;
            var points=root.AddComponent<WeaponGripPoints>();points.profile=profile;
            points.primary=new GameObject("Grip / primary").transform;points.primary.SetParent(root.transform,false);points.primary.localPosition=profile.primaryPoint;
            points.secondary=new GameObject("Grip / secondary").transform;points.secondary.SetParent(root.transform,false);
            var focus=new GameObject("Charged heart").transform;focus.SetParent(root.transform,false);focus.localPosition=Vector3.up*.58f;
            root.AddComponent<WeaponPresentation>().kind=WeaponKind.LightningStaff;
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        }
    }
}
