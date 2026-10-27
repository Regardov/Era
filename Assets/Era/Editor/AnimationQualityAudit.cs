using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class AnimationQualityAudit
    {
        [Serializable] public class ClipInfo
        {
            public string name,path,guid; public bool legacy,loop,reachable;
            public float seconds,pelvisRange,spineAngle,shoulderAngle,footTravel,loopFootGap;
            public string[] events,findings;public int curves;
        }
        [Serializable] public class ModelInfo
        {
            public string prefab,system,controller,rootBone;public string[] hierarchy,layers,masks;
            public int skins,bones,blendTrees;public bool rootMotion;
            public ClipInfo[] clips;
        }
        [Serializable] public class Report {public string unity,baseCommit;public ModelInfo[] models;}
        static Transform Find(GameObject o,params string[] names)=>o.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>names.Contains(t.name));
        public static void ValidateArcherStage()
        {
            ArcherAnimationImporter.Prepare();
            int assertions=ArcherAnimationChecks.RunPrefab(Resources.Load<GameObject>("Characters/Ember"));
            Run();Debug.Log("ERA_QUALITY_STAGE_OK "+assertions+" archer assertions");
        }
        public static void Run()
        {
            var models=new List<ModelInfo>();
            string code=File.ReadAllText("Assets/Era/Scripts/SkeletalActor.cs");
            foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.EndsWith(".prefab")&&(p.StartsWith("Assets/Era/Resources/Characters/")||p.StartsWith("Assets/Era/Resources/Creatures/"))))
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var archer=source.GetComponentInChildren<ArcherAnimationDriver>(true);
                var legacy=source.GetComponentInChildren<Animation>(true);
                if(archer==null&&legacy==null)continue;
                var instance=Object.Instantiate(source);
                try
                {
                    var a=instance.GetComponentInChildren<Animator>(true);var animation=instance.GetComponentInChildren<Animation>(true);
                    var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var bones=skins.SelectMany(s=>s.bones).Where(t=>t!=null).Distinct().ToArray();
                    var m=new ModelInfo{prefab=path,system=archer!=null?"Generic Animator + post-pose IK":"Legacy Animation + SkeletalActor",skins=skins.Length,bones=bones.Length,
                        hierarchy=bones.Select(t=>t.name+" <- "+(t.parent!=null?t.parent.name:"scene")).ToArray(),rootBone=skins.FirstOrDefault()?.rootBone?.name,rootMotion=a!=null&&a.applyRootMotion};
                    if(a?.runtimeAnimatorController is AnimatorController controller)
                    {
                        m.controller=AssetDatabase.GetAssetPath(controller);
                        m.layers=controller.layers.Select(l=>l.name+" / "+l.blendingMode).ToArray();
                        m.masks=controller.layers.Where(l=>l.avatarMask!=null).Select(l=>AssetDatabase.GetAssetPath(l.avatarMask)).ToArray();
                        m.blendTrees=AssetDatabase.LoadAllAssetsAtPath(m.controller).OfType<BlendTree>().Count();
                    }
                    else {m.controller="Legacy: Animation.CrossFade + AddMixingTransform(spine)";m.layers=new[]{"0 Base","1 upper actions"};m.masks=new[]{"Runtime spine subtree"};}
                    var clips=archer!=null?instance.GetComponentInChildren<ArcherAnimationDriver>(true).Clips.Select(c=>c.clip):animation.Cast<AnimationState>().Select(s=>s.clip);
                    var transforms=instance.GetComponentsInChildren<Transform>(true);var pos=transforms.Select(t=>t.localPosition).ToArray();var rot=transforms.Select(t=>t.localRotation).ToArray();var scale=transforms.Select(t=>t.localScale).ToArray();
                    var pelvis=Find(instance,"Pelvis","hips");var spine=Find(instance,"Spine","spine");var shoulder=Find(instance,"UpperArm.L","upperarm.l");var foot=Find(instance,"Foot.L","foot.l");
                    var entries=new List<ClipInfo>();
                    foreach(var clip in clips.Where(c=>c!=null).Distinct().OrderBy(c=>c.name))
                    {
                        var hips=new List<Vector3>();var feet=new List<Vector3>();var spines=new List<Quaternion>();var shoulders=new List<Quaternion>();
                        foreach(float f in new[]{0,.25f,.5f,.75f,1})
                        {
                            for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=pos[i];transforms[i].localRotation=rot[i];transforms[i].localScale=scale[i];}
                            clip.SampleAnimation(archer!=null?a.gameObject:instance,Mathf.Max(0,clip.length*f-(f==1?.00001f:0)));
                            if(pelvis!=null)hips.Add(pelvis.localPosition);if(foot!=null)feet.Add(instance.transform.InverseTransformPoint(foot.position));
                            if(spine!=null)spines.Add(spine.localRotation);if(shoulder!=null)shoulders.Add(shoulder.localRotation);
                        }
                        Func<List<Vector3>,float> range=v=>v.Count==0?0:v.SelectMany(x=>v.Select(y=>Vector3.Distance(x,y))).Max();
                        Func<List<Quaternion>,float> angle=v=>v.Count==0?0:v.SelectMany(x=>v.Select(y=>Quaternion.Angle(x,y))).Max();
                        string assetPath=AssetDatabase.GetAssetPath(clip);var settings=AnimationUtility.GetAnimationClipSettings(clip);
                        var e=new ClipInfo{name=clip.name,path=assetPath,guid=AssetDatabase.AssetPathToGUID(assetPath),legacy=clip.legacy,loop=settings.loopTime||clip.wrapMode==WrapMode.Loop||(animation!=null&&animation[clip.name]!=null&&animation[clip.name].wrapMode==WrapMode.Loop),
                            reachable=archer!=null||code.Contains('"'+clip.name+'"'),seconds=clip.length,pelvisRange=range(hips),spineAngle=angle(spines),shoulderAngle=angle(shoulders),footTravel=range(feet),
                            loopFootGap=feet.Count>1?Vector3.Distance(feet[0],feet.Last()):0,events=AnimationUtility.GetAnimationEvents(clip).Select(ev=>ev.functionName+" @ "+ev.time.ToString("F3")).ToArray(),curves=AnimationUtility.GetCurveBindings(clip).Length};
                        var findings=new List<string>();
                        if(!e.reachable)findings.Add("Not directly selected by the current actor scripts; retain, do not invent gameplay");
                        if(e.loop&&e.loopFootGap>.025f)findings.Add("Measured end/start foot gap >25mm; inspect loop seam");
                        if(e.name.Contains("Walk")||e.name.Contains("Run"))
                        {if(e.pelvisRange<.002f)findings.Add("Pelvis position varies <2mm in clip; inspect weight transfer");if(e.spineAngle<1)findings.Add("Spine rotation varies <1 degree in clip; check procedural compensation");}
                        findings.Add("Image review required; sampled transforms alone do not prove artistic quality");e.findings=findings.ToArray();entries.Add(e);
                    }
                    m.clips=entries.ToArray();models.Add(m);
                }
                finally{Object.DestroyImmediate(instance);}
            }
            var report=new Report{unity=Application.unityVersion,baseCommit="a40e3bd",models=models.ToArray()};
            Directory.CreateDirectory("Artifacts/AnimationQuality");File.WriteAllText("Artifacts/AnimationQuality/initial-audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("ERA_QUALITY_AUDIT models="+models.Count+" clips="+models.Sum(m=>m.clips.Length));
        }
    }
}
