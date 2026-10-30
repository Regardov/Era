using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Era
{
    // Real input through Simulation. Samples after the garment/rune composition.
    [DefaultExecutionOrder(98)]
    public sealed class LumenMotionDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Phase {public string name,worstPanel;public int frames;public float maxPenetration,maxCorrection,maxPalmError,maxLean,maxWalkWeight,maxRunWeight,leftHandTravel,rightHandTravel;public bool finite=true,rune;[NonSerialized]public float leftMin=1000,leftMax=-1000,rightMin=1000,rightMax=-1000;}
        [Serializable] public sealed class Report {public string version;public bool ok;public int errors;public Phase[] phases;}
        string[] names={"IdleRune","Walk","BoostRun","Strafe","Jump","Dash","HatTop","Hands"};Phase[] phases;int phase=-1,shot;float started,phaseStarted;
        GameApp App=>GameApp.Instance;LumenAnimationDriver actor;LumenGarmentClearance garment;string folder;int errors;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:"Artifacts/Lumen0261";Directory.CreateDirectory(folder);
            phases=names.Select(n=>new Phase{name=n}).ToArray();Application.logMessageReceived+=Log;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            App.SelectedHero=Hero.Lumen;App.Solo();App.LocalPlayer.weapons=new[]{(int)WeaponKind.Aura,-1};App.LocalPlayer.lastWeapon=(int)WeaponKind.Aura;
            App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;App.LocalPlayer.position=WorldMap.OnGround(0,0);
            var env=FindAnyObjectByType<WorldEnvironment>();if(env!=null)env.gameObject.SetActive(false);
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.62f,.63f);
            var key=new GameObject("Lumen motion studio key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.1f;key.transform.rotation=Quaternion.Euler(35,-25,0);
            started=Time.realtimeSinceStartup;gameObject.AddComponent<LumenMotionInputStage>().owner=this;
        }
        void Log(string m,string t,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++;}
        public void Prepare()
        {
            if(!enabled||Time.realtimeSinceStartup-started<2)return;
            actor??=FindObjectsByType<HeroMotion>(FindObjectsSortMode.None).FirstOrDefault(h=>h.name==App.LocalPlayer.name)?.lumen;
            if(actor==null){if(Time.realtimeSinceStartup-started>12)Finish();return;}
            garment??=actor.GetComponent<LumenGarmentClearance>();garment.auditCollisions=true;
            bool entered=false;
            if(phase<0||Time.realtimeSinceStartup-phaseStarted>3)
            {
                if(++phase>=names.Length){Finish();return;}phaseStarted=Time.realtimeSinceStartup;shot=0;entered=true;
                App.LocalPlayer.position=WorldMap.OnGround(0,0);App.LocalPlayer.velocity=Vector3.zero;
            }
            var p=App.LocalPlayer;var input=App.LocalInput;p.invulnerability=10000;p.pendingLevels=0;App.State.spawnTimer=100000;App.State.enemies.Clear();
            input.x=names[phase]=="Strafe"?1:0;input.z=phase>=1&&phase<=5&&phase!=3?1:0;input.yaw=0;
            p.boost=names[phase]=="BoostRun"?1:0;input.jump|=entered&&names[phase]=="Jump";input.jumpHeld=false;input.dash|=entered&&names[phase]=="Dash";
        }
        void LateUpdate()
        {
            if(actor==null||phase<0||phase>=phases.Length||!enabled)return;
            var row=phases[phase];row.frames++;if(garment.MaxPenetration>row.maxPenetration){row.maxPenetration=garment.MaxPenetration;row.worstPanel=garment.WorstPanel;}row.maxCorrection=Mathf.Max(row.maxCorrection,garment.MaxCorrection);row.finite&=garment.finite;
            var rune=FindAnyObjectByType<LumenRuneFocus>();row.rune|=rune!=null&&rune.GetComponentsInChildren<LineRenderer>().Count(l=>l.enabled)>=10;
            if(rune!=null)row.maxPalmError=Mathf.Max(row.maxPalmError,rune.PalmError);
            var up=actor.Bone("Chest").TransformDirection(Vector3.up);float lean=Mathf.Abs(Vector3.SignedAngle(actor.actorRoot.up,up,actor.actorRoot.right));
            row.maxLean=Mathf.Max(row.maxLean,lean);
            var left=actor.actorRoot.InverseTransformPoint(actor.Bone("Hand.L").position);var right=actor.actorRoot.InverseTransformPoint(actor.Bone("Hand.R").position);
            row.leftMin=Mathf.Min(row.leftMin,left.z);row.leftMax=Mathf.Max(row.leftMax,left.z);row.rightMin=Mathf.Min(row.rightMin,right.z);row.rightMax=Mathf.Max(row.rightMax,right.z);
            row.leftHandTravel=row.leftMax-row.leftMin;row.rightHandTravel=row.rightMax-row.rightMin;
            foreach(var clip in actor.animator.GetCurrentAnimatorClipInfo(0)){if(clip.clip.name=="AuraWeaver_Walk")row.maxWalkWeight=Mathf.Max(row.maxWalkWeight,clip.weight);if(clip.clip.name=="AuraWeaver_Run")row.maxRunWeight=Mathf.Max(row.maxRunWeight,clip.weight);}
            var camera=App.View.Camera;camera.GetComponent<StillwaterLens>().enabled=false;camera.orthographic=true;camera.orthographicSize=1.52f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.69f,.68f);
            Vector3 offset=names[phase]=="HatTop"?new Vector3(0,4,.2f):shot%3==1?new Vector3(4,1.6f,0):shot%3==2?new Vector3(2,1.6f,-5):new Vector3(2.7f,1.8f,5);
            camera.transform.position=actor.actorRoot.position+offset;camera.transform.LookAt(actor.actorRoot.position+Vector3.up*(names[phase]=="HatTop"?1.7f:1.18f));
            if(names[phase]=="Hands")
            {
                var center=(actor.Bone("Hand.L").position+actor.Bone("Hand.R").position)*.5f;camera.orthographicSize=.42f;
                camera.transform.position=center+(shot%3==1?new Vector3(-2,.6f,4):shot%3==2?new Vector3(3,.4f,2):new Vector3(1,.5f,4));camera.transform.LookAt(center);
            }
            if(Time.realtimeSinceStartup-phaseStarted>.55f+shot*.55f&&shot<4){App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,row.name+"-"+shot+".png"));shot++;}
        }
        void Finish()
        {
            var report=new Report{version=Application.version,errors=errors,phases=phases};
            report.ok=errors==0&&phases.All(p=>p.frames>5&&p.finite&&p.rune&&p.maxPalmError<.005f&&p.maxPenetration<.008f)&&phases[1].maxWalkWeight>.9f&&phases[1].maxLean>3&&phases[2].maxRunWeight>.9f&&phases[2].maxLean>10&&phases[1].leftHandTravel>.15f&&phases[1].rightHandTravel>.15f&&phases[2].leftHandTravel>.15f&&phases[2].rightHandTravel>.15f;
            File.WriteAllText(Path.Combine(folder,"lumen-motion.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_LUMEN_MOTION "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
    [DefaultExecutionOrder(-120)] public sealed class LumenMotionInputStage:MonoBehaviour {public LumenMotionDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}}
}
