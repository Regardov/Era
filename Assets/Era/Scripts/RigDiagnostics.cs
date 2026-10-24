using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(110)]
    public sealed class RigDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report {public bool ok,finite=true,colliders=true,profiles=true;public int errors,poseSamples,weapons=6;public float primaryError,secondaryError;public string worstGrip="";}
        GameApp App=>GameApp.Instance;readonly Report report=new Report();string folder;float started,phaseStart;int phase=-1;bool sampled;Mesh baked;FrameCapture capture;Font font;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);Application.logMessageReceived+=Log;
            App.SelectedHero=Hero.Ember;App.Solo();App.State.multiplayer=true;App.Simulation.AddPlayer(1,"Echo",Hero.Echo);App.Simulation.AddPlayer(2,"Lumen",Hero.Lumen);App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;
            var env=FindAnyObjectByType<WorldEnvironment>();if(env!=null)env.gameObject.SetActive(false);RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.65f,.69f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Rig inspection studio";floor.transform.position=Vector3.down*.08f;floor.transform.localScale=new Vector3(12,.1f,10);floor.GetComponent<Renderer>().material.color=new Color(.67f,.69f,.67f);
            var light=new GameObject("Rig inspection key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.Euler(28,-25,0);
            foreach(var p in App.State.players){p.weapons=new[]{-1,-1};p.invulnerability=999;p.shield=0;Array.Clear(p.auraLevels,0,p.auraLevels.Length);}started=phaseStart=Time.realtimeSinceStartup;baked=new Mesh();font=Font.CreateDynamicFontFromOSFont("Arial",16);capture=new FrameCapture(font);
        }
        void Log(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        void Update()
        {
            float time=Time.realtimeSinceStartup;if(time-started<1)return;
            if(phase<0||time-phaseStart>.45f){phase++;phaseStart=time;sampled=false;if(phase>=36){Finish();return;}if(phase%6==4)foreach(var p in App.State.players)p.weaponCastSerial++;if(phase%6==5)foreach(var p in App.State.players)p.landSerial++;}
            int pose=phase%6,kind=phase/6;
            foreach(var p in App.State.players)
            {
                p.position=new Vector3((p.id-1)*1.65f,pose==3?.8f:0,0);p.velocity=Vector3.forward*(pose==1?2:pose==2?7.8f:0);p.yaw=0;p.lastWeapon=kind;p.weaponPoseTime=pose==4?.32f:0;p.grounded=pose!=3;p.verticalSpeed=pose==3?4:0;p.crouching=false;p.pendingLevels=0;p.invulnerability=999;
            }
        }
        void LateUpdate()
        {
            if(!enabled||phase<0||phase>=36)return;var camera=App.View.Camera;camera.orthographic=true;camera.orthographicSize=1.65f;camera.transform.position=new Vector3(0,1.6f,7);camera.transform.LookAt(new Vector3(0,1.35f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.76f,.78f,.76f);camera.GetComponent<StillwaterLens>().enabled=false;
            if(Time.realtimeSinceStartup-phaseStart<.22f||sampled)return;sampled=true;
            foreach(var grip in FindObjectsByType<WeaponGrip>(FindObjectsSortMode.None))
            {
                report.poseSamples++;report.primaryError=Mathf.Max(report.primaryError,grip.PrimaryError);
                if(grip.SecondaryError>report.secondaryError){report.secondaryError=grip.SecondaryError;report.worstGrip=grip.kind+" / "+grip.actor.name+" / pose "+phase%6;}
                var points=grip.GetComponent<WeaponGripPoints>();report.profiles&=points!=null&&points.profile!=null;
                var collider=grip.actor.GetComponent<CapsuleCollider>();var rb=grip.actor.GetComponent<Rigidbody>();report.colliders&=collider!=null&&collider.enabled&&!collider.isTrigger&&rb!=null&&rb.isKinematic&&grip.actor.GetComponent<CharacterController>()==null;
                var skin=grip.actor.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(baked);foreach(var v in baked.vertices)report.finite&=ActorMotionMath.Finite(v)&&v.magnitude<6;
            }
            if(phase%6==4)
            {
                capture.Save(Path.Combine(folder,"grip-"+(WeaponKind)(phase/6)+"-v019.png"),camera,()=>{});
                camera.transform.position=new Vector3(3.6f,1.9f,7);camera.transform.LookAt(new Vector3(0,1.35f,0));
                capture.Save(Path.Combine(folder,"grip-"+(WeaponKind)(phase/6)+"-side-v019.png"),camera,()=>{});
            }
        }
        void Finish(){report.ok=report.errors==0&&report.finite&&report.colliders&&report.profiles&&report.poseSamples>=108&&report.primaryError<.003f&&report.secondaryError<.03f;File.WriteAllText(Path.Combine(folder,"smoke-rig.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_RIG "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);enabled=false;}
        void OnDestroy(){Application.logMessageReceived-=Log;if(baked!=null)Destroy(baked);capture?.Dispose();if(font!=null)Destroy(font);}
    }
}
