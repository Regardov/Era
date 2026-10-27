using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    // Explicit --smoke fixture: real prefab components evaluate normally. No SampleAnimation.
    // Timers/velocities below are diagnostic inputs, not changes to combat simulation.
    [DefaultExecutionOrder(125)]
    public sealed class EnsembleAnimationDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string name,system;public int frames,recordedFrames;public string[] clips;
            public float rotationRange,maxReleaseStep,freezeDrift,freezeRotation,strideClearance,arrowOffset,arrowScaleError,wingRange;
            public bool finite=true,hasStrideClearance;
        }
        [Serializable] public sealed class Report {public bool ok;public int errors;public string mode;public Result[] actors;}
        readonly List<Result> results=new List<Result>();readonly HashSet<string> clips=new HashSet<string>();
        readonly VisualFactory factory=new VisualFactory();readonly Report report=new Report();
        GameApp App=>GameApp.Instance;GameObject model;CreatureMotion creature;SkeletalActor skeleton;ProjectileVisual arrow;ItemFieldPresentation summon;
        PlayerState player;EnemyState enemy;ProjectileState projectile;ItemFieldState field;Transform joint,wing;Transform[] transforms;
        Vector3[] frozenPositions;Quaternion[] frozenRotations;Quaternion initialRotation,lastRotation,initialWing;bool freezeBound,record;
        string folder;int index=-1;float age,nextCapture,height;Texture2D frame;Transform floor;Result current;bool warmed;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:"Artifacts/AnimationQuality/Ensemble";Directory.CreateDirectory(folder);
            record=Array.IndexOf(args,"--quality-record")>=0;report.mode=Array.IndexOf(args,"--quality-baseline")>=0?"before":"after";
            int fps=Array.IndexOf(args,"--quality-fps");Application.targetFrameRate=fps>=0?int.Parse(args[fps+1]):30;
            Application.logMessageReceived+=Log;App.Solo();App.State.multiplayer=true;App.State.spawnTimer=100000;App.State.enemies.Clear();App.State.resourceNodes.Clear();App.LocalPlayer.invulnerability=9999;App.LocalPlayer.pendingLevels=0;App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.position=WorldMap.OnGround(0,-150);
            RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
            var key=new GameObject("Ensemble review key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.transform.rotation=Quaternion.Euler(30,-28,0);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;floor.gameObject.layer=25;floor.localScale=new Vector3(20,.06f,20);floor.position=Vector3.down*.04f;Destroy(floor.GetComponent<Collider>());floor.GetComponent<Renderer>().material.color=new Color(.55f,.56f,.56f);
            frame=new Texture2D(480,600,TextureFormat.RGB24,false);gameObject.AddComponent<EnsemblePoseStage>().owner=this;
            Next();
        }
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        float Duration=>index<23?3.1f:index<27?5.8f:2.2f;
        void Next()
        {
            if(model!=null)Destroy(model);model=null;creature=null;skeleton=null;arrow=null;summon=null;joint=null;wing=null;freezeBound=false;age=nextCapture=0;clips.Clear();index++;
            if(index>=31){Finish();return;}
            current=new Result();results.Add(current);
            if(index<16){enemy=new EnemyState{kind=(EnemyKind)index,hp=100,maxHp=100,attackDuration=1,attackStyle=index%4};creature=CreatureMotion.Create(factory,null,enemy.kind);model=creature.gameObject;current.name=enemy.kind.ToString();}
            else if(index<23){int stage=index-15;enemy=new EnemyState{kind=EnemyKind.Warden,bossStage=stage,hp=100,maxHp=100,attackDuration=1,attackStyle=stage%4};creature=BossArt.Create(factory,null,stage);model=creature.gameObject;current.name="Boss"+stage;}
            else if(index<27){string[] paths={"Characters/Echo","Characters/Lumen","Characters/Library/Wayfarer","Characters/Library/DuskMage"};model=Instantiate(Resources.Load<GameObject>(paths[index-23]));current.name=paths[index-23].Split('/').Last();player=new PlayerState{id=-99,hp=100,maxHp=100,grounded=true,chargingShrine=-1,lastWeapon=-1};}
            else if(index==27){projectile=new ProjectileState{id=-999,owner=App.LocalId,weapon=WeaponKind.Bow,position=Vector3.up,direction=Vector3.forward,life=10};arrow=ProjectileVisual.Create(factory,null,projectile,App.State);model=arrow.gameObject;current.name="Arrow";}
            else {var ids=new[]{ItemId.HiveMind,ItemId.HuntingPack,ItemId.GravePact};field=new ItemFieldState{id=-998,item=(int)ids[index-28],owner=App.LocalId,position=Vector3.zero,time=10};model=new GameObject("Summon test");summon=model.AddComponent<ItemFieldPresentation>();summon.Present(field,App.State);current.name=ids[index-28].ToString();}
            skeleton=model.GetComponent<SkeletalActor>();joint=skeleton!=null?skeleton.spine:creature?.body;
            if(arrow!=null)joint=model.transform.GetChild(0);
            if(summon!=null)wing=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Venom drone wing");
            current.system=skeleton!=null?"Legacy Animation + existing post pose":creature!=null?"CreatureMotion":arrow!=null?"ProjectileVisual":"ItemFieldPresentation";
            transforms=model.GetComponentsInChildren<Transform>(true);foreach(var t in transforms)t.gameObject.layer=25;
            if(joint!=null){initialRotation=lastRotation=joint.localRotation;}
            if(wing!=null)initialWing=wing.localRotation;
            var bounds=new Bounds();bool first=true;foreach(var renderer in model.GetComponentsInChildren<Renderer>()){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            // Authored skin bounds include animation envelopes; use actual vertices for camera only.
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();skin.BakeMesh(mesh);var b=mesh.bounds;bounds=new Bounds(skin.transform.TransformPoint(b.center),Vector3.Scale(b.size,skin.transform.lossyScale));Destroy(mesh);break;}
            height=Mathf.Clamp(bounds.size.y,1.6f,4.8f);if(index>=27)height=2;
        }
        public void Present()
        {
            if(!enabled||model==null)return;if(!warmed){warmed=true;return;}age+=Mathf.Min(.1f,ActorMotionMath.Delta);
            if(age>=Duration){current.clips=clips.OrderBy(x=>x).ToArray();Next();return;}
            if(creature!=null)
            {
                enemy.windup=enemy.meleeWindup=age>=.8f&&age<1.8f?1.8f-age:0;enemy.recovery=age>=1.8f&&age<2.4f?2.4f-age:0;enemy.frozen=age>=2.4f&&age<2.7f?1:0;
                creature.Present(enemy,age<.8f?EnemyCatalog.Speed(enemy.kind):0);
            }
            else if(skeleton!=null)
            {
                player.velocity=age>=.9f&&age<1.8f?Vector3.forward*2:age>=1.8f&&age<3.6f?Vector3.forward*6:Vector3.zero;player.grounded=age<4||age>=4.5f;player.verticalSpeed=age<4.25f?5:-4;player.jumpSerial=age>=4?1:0;player.landSerial=age>=4.5f?1:0;player.castSerial=age>=2.8f?1:0;player.dashTime=age>=3.6f&&age<3.8f?.15f:0;
                skeleton.Hero(player);if(age>=5.2f&&age<5.23f){skeleton.Hit(Vector3.back);skeleton.FreezePose(.045f);}
            }
            else if(arrow!=null){projectile.position=new Vector3(0,1,Mathf.Sin(age)*.5f);projectile.direction=age<1.1f?Vector3.forward:Vector3.right;arrow.Sync(projectile,1,ActorMotionMath.Delta);}
            else if(summon!=null){App.LocalPlayer.position=age<1.1f?Vector3.forward*5:Vector3.right*5;summon.Present(field,App.State);}
        }
        void LateUpdate()
        {
            if(!enabled||model==null)return;current.frames++;
            foreach(var t in transforms)current.finite&=ActorMotionMath.Finite(t.localPosition)&&ActorMotionMath.Finite(t.localScale)&&!float.IsNaN(t.localRotation.w);
            if(joint!=null){current.rotationRange=Mathf.Max(current.rotationRange,Quaternion.Angle(initialRotation,joint.localRotation));if(age>1.67f&&age<1.94f&&creature!=null)current.maxReleaseStep=Mathf.Max(current.maxReleaseStep,Quaternion.Angle(lastRotation,joint.localRotation));lastRotation=joint.localRotation;}
            if(skeleton!=null)foreach(AnimationState state in skeleton.animationPlayer)if(state.enabled&&state.weight>.01f)clips.Add(state.name);
            bool frozen=creature!=null&&age>=2.43f&&age<2.69f;
            if(frozen)
            {
                if(!freezeBound){freezeBound=true;frozenPositions=transforms.Select(t=>t.localPosition).ToArray();frozenRotations=transforms.Select(t=>t.localRotation).ToArray();}
                else for(int i=0;i<transforms.Length;i++){current.freezeDrift=Mathf.Max(current.freezeDrift,Vector3.Distance(transforms[i].localPosition,frozenPositions[i]));current.freezeRotation=Mathf.Max(current.freezeRotation,Quaternion.Angle(transforms[i].localRotation,frozenRotations[i]));}
            }
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()){int blend=skin.sharedMesh.GetBlendShapeIndex("StrideClearance");if(blend>=0){current.hasStrideClearance=true;current.strideClearance=Mathf.Max(current.strideClearance,skin.GetBlendShapeWeight(blend));}}
            if(arrow!=null){var core=model.transform.GetChild(0);current.arrowOffset=Mathf.Max(current.arrowOffset,core.localPosition.magnitude);current.arrowScaleError=Mathf.Max(current.arrowScaleError,(core.localScale-Vector3.one).magnitude);}
            if(wing!=null)current.wingRange=Mathf.Max(current.wingRange,Quaternion.Angle(initialWing,wing.localRotation));
            var camera=App.View.Camera;camera.GetComponent<StillwaterLens>().enabled=false;camera.orthographic=true;camera.orthographicSize=height*.62f;camera.cullingMask=1<<25;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.28f,.30f);camera.transform.position=new Vector3(height*1.2f,height*.64f,height*2);camera.transform.LookAt(Vector3.up*height*.48f);
            if(record&&age>=nextCapture){Capture(camera);nextCapture+=.1f;}
        }
        void Capture(Camera camera)
        {
            var path=Path.Combine(folder,current.name);Directory.CreateDirectory(path);var rt=RenderTexture.GetTemporary(480,600,24);var previous=RenderTexture.active;var target=camera.targetTexture;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;frame.ReadPixels(new Rect(0,0,480,600),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(path,(current.recordedFrames++).ToString("D4")+".png"),frame.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        void Finish()
        {
            report.actors=results.ToArray();report.ok=results.Count==31&&report.errors==0&&results.All(r=>r.finite&&r.frames>10&&r.freezeDrift<.001f&&r.freezeRotation<.1f)&&results.Where(r=>r.system.StartsWith("Legacy")).All(r=>r.clips.Length>=2);
            File.WriteAllText(Path.Combine(folder,"ensemble-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ENSEMBLE "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;if(frame!=null)Destroy(frame);}
    }
    [DefaultExecutionOrder(50)] public sealed class EnsemblePoseStage:MonoBehaviour{public EnsembleAnimationDiagnostics owner;void LateUpdate(){if(owner!=null&&owner.enabled)owner.Present();}}
}
