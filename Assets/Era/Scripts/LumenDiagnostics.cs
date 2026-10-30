using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Era
{
    // After ArenaView/Animator, before GameHud's capture at order 100.
    [DefaultExecutionOrder(90)]
    public sealed class LumenDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Row {public string animation;public bool exists,assigned,runtimePlayed,finite=true;public float boneMotionDegrees;}
        [Serializable] public sealed class Report {public string version;public bool ok,liveLumen,avatar,noRootMotion;public int errors,skins;public Row[] animations;}
        readonly string[] names={"Idle","Walk","Run","JumpStart","Airborne","Fall","Land","AuraChannel","Cast","Hit","Death"};
        readonly Report report=new Report();
        GameApp App=>GameApp.Instance;string folder;float started,phaseStart;int phase=-1;LumenAnimationDriver actor;bool captured;
        Quaternion[] previous;Transform[] bones;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:"Artifacts/Lumen";Directory.CreateDirectory(folder);
            report.version=Application.version;report.animations=names.Select(n=>new Row{animation="AuraWeaver_"+n}).ToArray();
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Lumen;App.Solo();App.State.enemies.Clear();App.State.spawnTimer=100000;App.State.resourceNodes.Clear();
            App.LocalPlayer.invulnerability=10000;App.LocalPlayer.weapons=new[]{(int)WeaponKind.Aura,-1};App.LocalPlayer.lastWeapon=(int)WeaponKind.Aura;App.LocalPlayer.position=WorldMap.OnGround(0,0);
            // Keep the real gameplay actor/driver, but remove foliage occlusion
            // from pose evidence. The ordinary camera is tested separately.
            var environment=FindAnyObjectByType<WorldEnvironment>();if(environment!=null)environment.gameObject.SetActive(false);
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.60f,.61f,.63f);
            var key=new GameObject("Lumen diagnostic key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.transform.rotation=Quaternion.Euler(35,-35,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.position=App.LocalPlayer.position-Vector3.up*.01f;floor.transform.localScale=Vector3.one*5;floor.GetComponent<Renderer>().material.color=new Color(.72f,.73f,.71f);
            started=Time.realtimeSinceStartup;gameObject.AddComponent<LumenDiagnosticStage>().owner=this;
        }
        void Log(string m,string t,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)report.errors++;}
        public void Prepare()
        {
            if(!enabled||Time.realtimeSinceStartup-started<2)return;
            actor??=FindObjectsByType<HeroMotion>(FindObjectsSortMode.None).FirstOrDefault(h=>h.name==App.LocalPlayer.name)?.lumen;
            if(actor==null){if(Time.realtimeSinceStartup-started>10)Finish();return;}
            if(phase<0||Time.realtimeSinceStartup-phaseStart>1.35f)
            {
                if(++phase>=names.Length){Finish();return;}
                phaseStart=Time.realtimeSinceStartup;captured=false;
                bones=actor.GetComponentsInChildren<Transform>();previous=bones.Select(b=>b.localRotation).ToArray();
                var row=report.animations[phase];var clip=actor.clips.FirstOrDefault(c=>c.name==row.animation)?.clip;row.exists=clip!=null&&!clip.empty;
                row.assigned=clip!=null&&actor.animator.runtimeAnimatorController.animationClips.Contains(clip);
                if(names[phase]=="JumpStart")App.LocalPlayer.jumpSerial++;
                if(names[phase]=="Cast")App.LocalPlayer.castSerial++;
                if(names[phase]=="Hit")App.LocalPlayer.hp-=1;
            }
            var p=App.LocalPlayer;string n=names[phase];p.invulnerability=10000;p.pendingLevels=0;App.State.enemies.Clear();App.State.spawnTimer=100000;
            p.velocity=n=="Walk"?Vector3.forward*GameData.BaseMoveSpeed:n=="Run"?Vector3.forward*GameData.BaseMoveSpeed*1.3f:Vector3.zero;
            p.grounded=!(n=="JumpStart"||n=="Airborne"||n=="Fall");p.verticalSpeed=n=="JumpStart"||n=="Airborne"?2:n=="Fall"?-2:0;
            p.auraSurge=n=="AuraChannel"?1:0;p.downed=n=="Death";
            if(n=="Land"&&Time.realtimeSinceStartup-phaseStart<.1f)p.landSerial++;
        }
        void LateUpdate()
        {
            if(!enabled||actor==null||phase<0||phase>=names.Length)return;
            var row=report.animations[phase];
            var camera=App.View.Camera;camera.GetComponent<StillwaterLens>().enabled=false;camera.orthographic=true;camera.orthographicSize=1.50f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.73f,.71f);
            camera.transform.position=actor.actorRoot.position+new Vector3(3,1.8f,5);camera.transform.LookAt(actor.actorRoot.position+Vector3.up*1.20f);
            row.runtimePlayed|=actor.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.weight>.01f&&c.clip.name==row.animation);
            for(int i=0;i<bones.Length;i++)row.boneMotionDegrees=Mathf.Max(row.boneMotionDegrees,Quaternion.Angle(previous[i],bones[i].localRotation));
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();skin.BakeMesh(mesh);row.finite&=mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<8);Destroy(mesh);
            }
            if(!captured&&Time.realtimeSinceStartup-phaseStart>.35f)
            {
                captured=true;App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,"Lumen-"+names[phase]+".png"));
            }
        }
        void Finish()
        {
            report.liveLumen=actor!=null&&actor.GetComponentInParent<HeroMotion>().Hero==Hero.Lumen;
            report.avatar=actor!=null&&actor.animator.avatar!=null&&actor.animator.avatar.isValid;report.noRootMotion=actor!=null&&!actor.animator.applyRootMotion;
            report.skins=actor==null?0:actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
            report.ok=report.errors==0&&report.liveLumen&&report.avatar&&report.noRootMotion&&report.skins==26&&report.animations.All(a=>a.exists&&a.assigned&&a.runtimePlayed&&a.finite&&a.boneMotionDegrees>.1f);
            File.WriteAllText(Path.Combine(folder,"lumen-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_LUMEN_RUNTIME "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
    [DefaultExecutionOrder(-40)] public sealed class LumenDiagnosticStage:MonoBehaviour {public LumenDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}}
}
