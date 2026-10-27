// Copied only into an isolated ignored test project. Never install into ERA Assets.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EraFactory
{
    public static class UnityFactoryProbe
    {
        [Serializable] public class ClipResult
        {
            public string name; public int curves,missingPaths;public bool finite;
            public float maxBonePositionDelta,maxBoneRotationDelta;
        }
        [Serializable] public class Report
        {
            public bool ok,validGenericAvatar,controllerAssigned;
            public string unity,scope="Isolated Editor sample/import check. No ERA prefab or runtime gameplay changes.";
            public int skins,bones,fbxTakes,controllerLayers;public ClipResult[] clips;
        }
        public static void Run()
        {
            const string path="Assets/FactorySmoke.fbx";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName="Root";importer.importAnimation=true;importer.resampleCurves=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects=false;importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.None;importer.importCameras=false;importer.importLights=false;
            importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar=assets.OfType<Avatar>().FirstOrDefault();
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator=model.GetComponent<Animator>();
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/OriginalClips/Archer.controller");
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bones=skins.SelectMany(s=>s.bones).Distinct().ToArray();
                var all=model.GetComponentsInChildren<Transform>();
                var positions=all.Select(t=>t.localPosition).ToArray();var rotations=all.Select(t=>t.localRotation).ToArray();var scales=all.Select(t=>t.localScale).ToArray();
                var list=new List<ClipResult>();
                foreach(string clipPath in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/OriginalClips"}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    var curves=AnimationUtility.GetCurveBindings(clip);
                    var result=new ClipResult{name=clip.name,curves=curves.Length,missingPaths=curves.Count(c=>c.path.Length>0&&model.transform.Find(c.path)==null),finite=true};
                    foreach(float t in new[]{0f,.25f,.5f,.75f,1f})
                    {
                        for(int i=0;i<all.Length;i++){all[i].localPosition=positions[i];all[i].localRotation=rotations[i];all[i].localScale=scales[i];}
                        clip.SampleAnimation(model,clip.length*t);
                        for(int i=0;i<all.Length;i++){result.maxBonePositionDelta=Mathf.Max(result.maxBonePositionDelta,Vector3.Distance(positions[i],all[i].localPosition));result.maxBoneRotationDelta=Mathf.Max(result.maxBoneRotationDelta,Quaternion.Angle(rotations[i],all[i].localRotation));}
                        foreach(var skin in skins)
                        {
                            var baked=new Mesh();skin.BakeMesh(baked);
                            result.finite&=baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<8);
                            UnityEngine.Object.DestroyImmediate(baked);
                        }
                    }
                    list.Add(result);
                }
                var report=new Report{unity=Application.unityVersion,validGenericAvatar=avatar!=null&&avatar.isValid&&!avatar.isHuman,
                    skins=skins.Length,bones=bones.Length,fbxTakes=assets.OfType<AnimationClip>().Count(c=>!c.name.StartsWith("__preview__")),
                    controllerAssigned=controller!=null,controllerLayers=controller?.layers.Length??0,clips=list.OrderBy(c=>c.name).ToArray()};
                report.ok=report.validGenericAvatar&&report.controllerAssigned&&report.controllerLayers==3&&report.skins==17&&report.bones==54&&report.fbxTakes==26&&report.clips.Length==26&&report.clips.All(c=>c.finite&&c.curves>0&&c.missingPaths==0);
                File.WriteAllText(Path.GetFullPath("../unity-validation.json"),JsonUtility.ToJson(report,true));
                if(!report.ok)throw new Exception("Factory Unity probe failed; see ../unity-validation.json");
                Debug.Log("ERA_FACTORY_UNITY_OK 26 original clips, 54 bones, 17 skins, 3 controller layers");
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
        }
    }
}
