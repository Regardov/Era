using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Era.Editor
{
    // Imports the standalone art asset and binds it to the existing Lumen hero.
    // Loadouts and authoritative aura logic are not modified.
    public static class AuraWeaverImporter
    {
        const string Source="ArtSource/Characters/AuraWeaver/ReleaseV10";
        public const float VisualScale=1.10f;
        const string Folder="Assets/Era/Art/Characters/AuraWeaver";
        const string Fbx=Folder+"/AuraWeaver_Rigged.fbx";
        public const string PrefabPath=Folder+"/AuraWeaver.prefab";
        public static void PrepareLumen()
        {
            const string target="Assets/Era/Resources/Characters/Lumen.prefab",marker="Assets/Era/Generated/Lumen-V9.txt";
            string signature=File.ReadAllText(Source+"/AuraWeaver_Manifest.json")+"\nRune locomotion / V10.1";
            if(File.Exists(marker)&&File.ReadAllText(marker)==signature&&AssetDatabase.LoadAssetAtPath<GameObject>(target)?.GetComponent<HeroMotion>()?.lumen!=null)return;
            ImportAndValidate();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));root.name="Lumen";
            try
            {
                var visual=root.transform.Find("AuraWeaverVisual");var driver=visual.gameObject.AddComponent<LumenAnimationDriver>();
                driver.animator=visual.GetComponent<Animator>();driver.actorRoot=root.transform;
                driver.clips=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"/AuraWeaver_Manifest.json")).clips.Select(c=>new ArcherClipReference{name=c.name,clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Animations/"+c.name+".anim"),loop=c.loop}).ToArray();
                root.AddComponent<HeroMotion>().BindLumen(driver);
                visual.gameObject.AddComponent<LumenGarmentClearance>().actor=driver;
                foreach(string side in new[]{"L","R"})
                {
                    var palm=new GameObject("ERA palm socket").transform;palm.SetParent(driver.Bone("Hand."+side),false);palm.localPosition=new Vector3(0,.04f,0);
                }
                var authored=root.GetComponent<AuthoredCharacter>();authored.model="Lumen";authored.version=10;
                authored.details=new[]{"Aura Weaver V8 pattern 09","V9 fitted mage skeleton","30 degree rear hat tip","11 mage animations","Existing Lumen abilities preserved"};
                ActorBodyCollider.Attach(root).Present(new PlayerState());PrefabUtility.SaveAsPrefabAsset(root,target);
            }
            finally{Object.DestroyImmediate(root);}
            File.WriteAllText(marker,signature);AssetDatabase.SaveAssets();Debug.Log("ERA_LUMEN_V9_INTEGRATED: visual only; existing aura simulation retained");
        }
        [Serializable] public class ClipData {public string name,source;public float start,end;public bool loop;}
        [Serializable] public class Palette {public string name,hex;}
        [Serializable] public class Manifest {public int bones,meshes,triangles,fps;public string sourceBlendSha256;public ClipData[] clips;public Palette[] materials;}
        [Serializable] public class BoneData {public string name;}
        [Serializable] public class Schema {public BoneData[] skeleton;}
        [Serializable] public class Construction {public string source_archer_sha256;}
        [Serializable] public class ClipCheck {public string name;public int curves,missingPaths;public bool finite,controllerEvaluated;public float movementDegrees;}
        [Serializable] public class Report
        {
            public bool ok,avatarValid,basePathsCompatible,originalArcherUntouched;
            public string unity,scope="FBX/editor validation only. See the current release's separate runtime validation in Docs.";
            public int skins,bones,clips,triangles;public ClipCheck[] results;public string[] originalClipPathsChecked;
        }

        [MenuItem("ERA/Aura Weaver/Import and validate separate prefab")]
        public static void ImportAndValidate()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"/AuraWeaver_Manifest.json"));
            if(manifest.clips.Length!=11||manifest.materials==null)throw new Exception("Incomplete Aura Weaver manifest");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Animations");
            File.Copy(Source+"/AuraWeaver_Rigged.fbx",Fbx,true);File.Copy(Source+"/AuraWeaver_Manifest.json",Folder+"/AuraWeaver_Manifest.json",true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName="Root";importer.importAnimation=true;importer.resampleCurves=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.None;
            importer.importCameras=false;importer.importLights=false;importer.isReadable=true;importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(Fbx);var avatar=assets.OfType<Avatar>().FirstOrDefault();
            if(avatar==null||!avatar.isValid||avatar.isHuman)throw new Exception("Expected a valid Generic Avatar");
            var materials=new Dictionary<string,Material>();
            foreach(var p in manifest.materials)
            {
                string path=Folder+"/Materials/"+p.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("Standard")){name=p.name};AssetDatabase.CreateAsset(mat,path);}
                if(!ColorUtility.TryParseHtmlString(p.hex,out var color))throw new Exception("Invalid local palette color: "+p.name);
                mat.color=color;mat.SetFloat("_Glossiness",.05f);mat.SetFloat("_Metallic",p.name=="AW_bronze"?.12f:0);mat.doubleSidedGI=true;
                EditorUtility.SetDirty(mat);materials[p.name]=mat;
            }
            var clips=new Dictionary<string,AnimationClip>();
            foreach(var data in manifest.clips)
            {
                var take=assets.OfType<AnimationClip>().FirstOrDefault(a=>!a.name.StartsWith("__preview__")&&(a.name==data.name||a.name.EndsWith("|"+data.name,StringComparison.Ordinal)));
                if(take==null)throw new Exception("Missing FBX take: "+data.name);
                var clip=new AnimationClip{name=data.name,frameRate=30,legacy=false};
                foreach(var binding in AnimationUtility.GetCurveBindings(take))AnimationUtility.SetEditorCurve(clip,binding,CompactConstant(AnimationUtility.GetEditorCurve(take,binding)));
                clip.EnsureQuaternionContinuity();AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=data.loop;settings.loopBlend=data.loop;
                settings.keepOriginalOrientation=settings.keepOriginalPositionY=settings.keepOriginalPositionXZ=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                string path=Folder+"/Animations/"+data.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing==null)AssetDatabase.CreateAsset(clip,path);else{EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;EditorUtility.SetDirty(clip);}
                clips[data.name]=clip;
            }
            string controllerPath=Folder+"/AuraWeaver.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine=controller.layers[0].stateMachine;
            foreach(var transition in machine.anyStateTransitions)machine.RemoveAnyStateTransition(transition);
            foreach(var state in machine.states)machine.RemoveState(state.state);
            foreach(var oldTree in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>())Object.DestroyImmediate(oldTree,true);
            controller.parameters=Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("LocomotionRate",AnimatorControllerParameterType.Float);
            foreach(string n in new[]{"Jump","Cast","Hit","Death"})controller.AddParameter(n,AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AuraChannel",AnimatorControllerParameterType.Bool);controller.AddParameter("IsGrounded",AnimatorControllerParameterType.Bool);controller.AddParameter("VerticalSpeed",AnimatorControllerParameterType.Float);
            var parameters=controller.parameters;foreach(var p in parameters){if(p.name=="LocomotionRate")p.defaultFloat=1;if(p.name=="IsGrounded")p.defaultBool=true;}controller.parameters=parameters;
            var locomotion=machine.AddState("Locomotion");var tree=new BlendTree{name="Aura Weaver locomotion",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(clips["AuraWeaver_Idle"],0);tree.AddChild(clips["AuraWeaver_Walk"],GameData.BaseMoveSpeed);tree.AddChild(clips["AuraWeaver_Run"],GameData.BaseMoveSpeed*1.25f);
            locomotion.motion=tree;locomotion.speedParameterActive=true;locomotion.speedParameter="LocomotionRate";machine.defaultState=locomotion;
            var states=new Dictionary<string,AnimatorState>();
            foreach(string n in new[]{"JumpStart","Airborne","Fall","Land","AuraChannel","Cast","Hit","Death"}){var state=machine.AddState(n);state.motion=clips["AuraWeaver_"+n];states[n]=state;}
            foreach(string n in new[]{"Cast","Hit","Death","Jump"})
            {
                var t=machine.AddAnyStateTransition(states[n=="Jump"?"JumpStart":n]);t.hasExitTime=false;t.duration=.06f;t.canTransitionToSelf=false;t.AddCondition(AnimatorConditionMode.If,0,n);
            }
            foreach(string n in new[]{"Cast","Hit","Land"}){var t=states[n].AddTransition(locomotion);t.hasExitTime=true;t.exitTime=.95f;t.duration=.08f;}
            var air=states["JumpStart"].AddTransition(states["Airborne"]);air.hasExitTime=true;air.exitTime=1;air.duration=.03f;
            var fall=states["Airborne"].AddTransition(states["Fall"]);fall.hasExitTime=false;fall.duration=.06f;fall.AddCondition(AnimatorConditionMode.Less,-.1f,"VerticalSpeed");
            foreach(string n in new[]{"Airborne","Fall"}){var t=states[n].AddTransition(states["Land"]);t.hasExitTime=false;t.duration=.04f;t.AddCondition(AnimatorConditionMode.If,0,"IsGrounded");}
            var channel=locomotion.AddTransition(states["AuraChannel"]);channel.hasExitTime=false;channel.duration=.15f;channel.AddCondition(AnimatorConditionMode.If,0,"AuraChannel");
            var end=states["AuraChannel"].AddTransition(locomotion);end.hasExitTime=false;end.duration=.15f;end.AddCondition(AnimatorConditionMode.IfNot,0,"AuraChannel");
            EditorUtility.SetDirty(controller);
            var root=new GameObject("AuraWeaver");
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx),root.transform);visual.name="AuraWeaverVisual";visual.transform.localScale=Vector3.one*VisualScale;
                var animator=visual.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.sharedMaterials=skin.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var mat)?mat:throw new Exception("Missing palette role "+m.name)).ToArray();skin.updateWhenOffscreen=true;}
                var body=root.AddComponent<CapsuleCollider>();body.height=2.05f;body.radius=.28f;body.center=Vector3.up*1.025f;
                var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                var provenance=root.AddComponent<AuthoredCharacter>();provenance.model="Aura Weaver / standalone preparation";provenance.sourceHash=manifest.sourceBlendSha256;provenance.version=1;provenance.triangles=manifest.triangles;
                provenance.details=new[]{"Hat","Human face","Hood","Layered coat","Connected fingered gloves","Independent long-coat bones","No rune or aura VFX","No player-selection binding"};
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                Validate(visual,clips,manifest);
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)RenderEditorViews(clips);
            Debug.Log("ERA_AURA_WEAVER_READY separate prefab, 11 runtime clips, no gameplay replacement");
        }

        static AnimationCurve CompactConstant(AnimationCurve curve)
        {
            var keys=curve.keys;
            // FBX bakes a key for every scale/translation channel on every frame.
            // Retain genuine motion at full sampling; collapse only flat curves.
            if(keys.Length>2&&keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.0000001f&&Mathf.Abs(k.inTangent)<.00001f&&Mathf.Abs(k.outTangent)<.00001f))
                return new AnimationCurve(new Keyframe(keys[0].time,keys[0].value),new Keyframe(keys[keys.Length-1].time,keys[0].value)){preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
            return curve;
        }

        static void Validate(GameObject model,Dictionary<string,AnimationClip> clips,Manifest manifest)
        {
            var all=model.GetComponentsInChildren<Transform>(true);var positions=all.Select(t=>t.localPosition).ToArray();var rotations=all.Select(t=>t.localRotation).ToArray();var scales=all.Select(t=>t.localScale).ToArray();
            void Restore(){for(int i=0;i<all.Length;i++){all[i].localPosition=positions[i];all[i].localRotation=rotations[i];all[i].localScale=scales[i];}}
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);var list=new List<ClipCheck>();var animator=model.GetComponent<Animator>();
            foreach(var pair in clips)
            {
                var clip=pair.Value;var bindings=AnimationUtility.GetCurveBindings(clip);var result=new ClipCheck{name=pair.Key,curves=bindings.Length,missingPaths=bindings.Count(b=>b.path.Length>0&&model.transform.Find(b.path)==null),finite=true};
                foreach(float t in new[]{0f,.25f,.5f,.75f,1f})
                {
                    Restore();clip.SampleAnimation(model,clip.length*t);
                    for(int i=0;i<all.Length;i++)result.movementDegrees=Mathf.Max(result.movementDegrees,Quaternion.Angle(rotations[i],all[i].localRotation));
                    foreach(var skin in skins){var baked=new Mesh();skin.BakeMesh(baked);result.finite&=baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<8);Object.DestroyImmediate(baked);}
                }
                list.Add(result);
            }
            var oldNames=new[]{"Idle","Walk","Run","JumpStart","Airborne","Fall","Land","Cast","Hit","Death"};var originalPaths=new List<string>();
            bool originalCompatible=true;
            foreach(string name in oldNames){string path="Assets/Era/Art/Characters/Archer/Animations/Archer_"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null)throw new Exception("Missing original library clip "+path);originalCompatible&=AnimationUtility.GetCurveBindings(clip).All(b=>b.path.Length==0||model.transform.Find(b.path)!=null);originalPaths.Add(path);}
            Restore();animator.Rebind();animator.Update(0);animator.SetBool("IsGrounded",false);
            foreach(var result in list)
            {
                string name=result.name.Substring("AuraWeaver_".Length);string state=name=="Idle"||name=="Walk"||name=="Run"?"Locomotion":name;
                animator.SetFloat("Speed",name=="Run"?GameData.BaseMoveSpeed*1.3f:name=="Walk"?GameData.BaseMoveSpeed:0);animator.SetBool("AuraChannel",name=="AuraChannel");animator.Play(state,0,0);animator.Update(.1f);
                result.controllerEvaluated=animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip==clips[result.name]);
            }
            var baseNames=JsonUtility.FromJson<Schema>(File.ReadAllText("ArtSource/CharacterFactory/rig-schema.json")).skeleton.Select(b=>b.name).ToHashSet();
            var skeletonRoot=model.transform.Find("Archer_Rig");
            int boneCount=all.Count(t=>t!=skeletonRoot&&t.IsChildOf(skeletonRoot)&&!t.name.StartsWith("AW_",StringComparison.Ordinal));
            originalCompatible&=baseNames.All(n=>all.Count(t=>t.name==n)==1);
            string expected=JsonUtility.FromJson<Construction>(File.ReadAllText("ArtSource/Characters/AuraWeaver/construction.json")).source_archer_sha256;
            string currentHash;using(var sha=System.Security.Cryptography.SHA256.Create())currentHash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("ArtSource/Characters/Archer/Archer_Animated.blend"))).Replace("-","").ToLowerInvariant();
            var report=new Report{unity=Application.unityVersion,avatarValid=animator.avatar.isValid,bones=boneCount,skins=skins.Length,clips=clips.Count,triangles=manifest.triangles,basePathsCompatible=originalCompatible,originalArcherUntouched=currentHash==expected,results=list.ToArray(),originalClipPathsChecked=originalPaths.ToArray()};
            report.ok=report.avatarValid&&originalCompatible&&report.originalArcherUntouched&&report.bones==manifest.bones&&report.skins==manifest.meshes&&list.All(r=>r.curves>0&&r.missingPaths==0&&r.finite&&r.controllerEvaluated);
            File.WriteAllText(Source+"/unity-validation.json",JsonUtility.ToJson(report,true));
            if(!report.ok)throw new Exception("Aura Weaver validation failed; see "+Source+"/unity-validation.json");
            Restore();clips["AuraWeaver_Idle"].SampleAnimation(model,0);
        }

        static void RenderEditorViews(Dictionary<string,AnimationClip> clips)
        {
            var previous=SceneManager.GetActiveScene();bool replaceEmpty=string.IsNullOrEmpty(previous.path)&&previous.rootCount==0;
            if(string.IsNullOrEmpty(previous.path)&&!replaceEmpty){Debug.LogWarning("Aura Weaver assets validated; open or save the untitled scene before requesting studio renders.");return;}
            var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceEmpty?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(preview);
            Material floorMat=null;
            try
            {
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.57f);RenderSettings.fog=false;
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));var model=root.transform.Find("AuraWeaverVisual").gameObject;
                model.GetComponent<Animator>().enabled=false;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                clips["AuraWeaver_Idle"].SampleAnimation(model,0);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.layer=30;floor.transform.localScale=Vector3.one*20;floor.transform.position=Vector3.down*.005f;
                floorMat=new Material(Shader.Find("Standard"));floorMat.color=new Color(.66f,.67f,.65f);floorMat.SetFloat("_Glossiness",0);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
                var light=new GameObject("Preview key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(40,-35,0);light.cullingMask=1<<30;light.shadows=LightShadows.Soft;
                var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.66f,.67f,.65f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.orthographicSize=1.4f;
                var names=new[]{"front","side","back","perspective","channel"};var points=new[]{new Vector3(0,1.22f,6),new Vector3(6,1.22f,0),new Vector3(0,1.22f,-6),new Vector3(3,2.25f,4.6f),new Vector3(3,2.25f,4.6f)};
                for(int i=0;i<names.Length;i++)
                {
                    if(i==4)clips["AuraWeaver_AuraChannel"].SampleAnimation(model,.3f);
                    camera.orthographic=i<3;camera.fieldOfView=30;camera.transform.position=points[i];camera.transform.LookAt(Vector3.up*1.22f);
                    var rt=RenderTexture.GetTemporary(680,840,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;var image=new Texture2D(680,840,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,680,840),0,0);image.Apply();File.WriteAllBytes(Source+"/Previews/Unity-"+names[i]+".png",image.EncodeToPNG());}
                    finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
                }
            }
            finally{if(replaceEmpty)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(preview,true);}if(floorMat!=null)Object.DestroyImmediate(floorMat);}
        }
    }
}
