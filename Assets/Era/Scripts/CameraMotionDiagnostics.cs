using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Era
{
    // Opt-in regression check of the ordinary simulation, actor and follow camera.
    // Unlike pose diagnostics, this does not move the camera or drive the actor
    // transform. Only gameplay input is supplied before GameApp.Update.
    [DefaultExecutionOrder(150)]
    public sealed class CameraMotionDiagnostics : MonoBehaviour
    {
        [Serializable] public sealed class Phase
        {
            public string direction;
            public int frames,stalledFrames,actorStalledFrames,rawStalledFrames,healthBarStalledFrames;
            public float targetSpeedErrorRms,cameraAngularAccelerationRms,maxTargetLag,maxFrameTime;
            public float actorSpeedErrorRms,rawSpeedErrorRms,maxActorTargetGap,measuredFps;
            public float healthBarSpeedErrorRms,maxHealthBarActorGap;
            [NonSerialized] public double speedSquared,angularSquared,actorSquared,rawSquared,healthBarSquared,seconds;
        }
        [Serializable] public sealed class Report
        {
            public string version,hero;
            public int requestedFps,errors;
            public bool ok,finite=true,ordinaryFollowCamera=true;
            public Phase[] phases;
        }
        readonly List<Phase> phases=new List<Phase>();
        readonly Report report=new Report();
        readonly StringBuilder rows=new StringBuilder("phase,time,dt,simulationX,simulationZ,targetX,targetZ,cameraX,cameraY,cameraZ,targetSpeedRatio,angularSpeed,actorX,actorZ,actorSpeedRatio,rawSpeedRatio\n");
        readonly Vector3[] directions={Vector3.forward,Vector3.back,Vector3.left,Vector3.right};
        readonly string[] names={"Forward","Backward","Left","Right"};
        GameApp App=>GameApp.Instance;
        string folder;
        float started,phaseStarted,previousAngular;
        int phase=-1;
        bool previous,allowFailure,captured;
        Vector3 previousRoot,previousActor,previousSimulation,previousHealthAnchor;
        Quaternion previousCamera;
        HeroMotion actor;
        Phase current;

        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            folder=Path.GetFullPath(Arg("--capture-folder","Artifacts/CameraMotion"));Directory.CreateDirectory(folder);
            report.requestedFps=int.Parse(Arg("--camera-fps","60"));
            App.SelectedHero=(Hero)Enum.Parse(typeof(Hero),Arg("--camera-hero","Ember"));
            report.hero=App.SelectedHero.ToString();report.version=Application.version;
            allowFailure=Array.IndexOf(args,"--camera-baseline")>=0;
            Application.targetFrameRate=report.requestedFps==0?-1:report.requestedFps;QualitySettings.vSyncCount=0;
            Application.logMessageReceived+=Log;
            App.Solo();App.CameraYaw=0;App.CameraPitch=25;
            App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;
            App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.lastWeapon=App.SelectedHero==Hero.Ember?(int)WeaponKind.Bow:-1;
            App.LocalPlayer.invulnerability=10000;App.LocalPlayer.position=WorldMap.OnGround(0,0);
            started=Time.realtimeSinceStartup;
            gameObject.AddComponent<CameraMotionInputStage>().owner=this;
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        public void Prepare()
        {
            if(!enabled)return;
            App.LocalPlayer.invulnerability=10000;App.LocalPlayer.pendingLevels=0;
            App.State.enemies.Clear();App.State.spawnTimer=100000;
            if(Time.realtimeSinceStartup-started<2)return;
            if(phase<0||Time.realtimeSinceStartup-phaseStarted>=3)
            {
                if(current!=null)EndPhase();
                if(++phase>=names.Length){Finish();return;}
                phaseStarted=Time.realtimeSinceStartup;previous=false;
                current=new Phase{direction=names[phase]};
                // Start each route inside the ordinary flat central clearing.
                var d=directions[phase];App.LocalPlayer.position=WorldMap.OnGround(-d.x*9,-d.z*9);
                App.LocalPlayer.velocity=Vector3.zero;App.LocalPlayer.yaw=0;captured=false;
            }
            App.LocalInput.x=directions[phase].x;App.LocalInput.z=directions[phase].z;App.LocalInput.yaw=0;
        }
        void LateUpdate()
        {
            if(!enabled||phase<0)return;
            if(actor==null)actor=FindObjectsByType<HeroMotion>(FindObjectsSortMode.None).FirstOrDefault(h=>h.name==App.LocalPlayer.name&&h.gameObject.activeInHierarchy);
            if(actor==null)return;
            var cam=App.View.Camera;var p=App.LocalPlayer;
            float dt=Time.unscaledDeltaTime,age=Time.realtimeSinceStartup-phaseStarted;
            var root=App.View.CameraFollowTarget;
            var visual=actor.transform.position;
            var healthAnchor=App.View.PlayerHealthAnchor(p);
            report.finite&=ActorMotionMath.Finite(root)&&ActorMotionMath.Finite(cam.transform.position);
            if(previous&&dt>0&&age>1.5f&&p.velocity.magnitude>1)
            {
                var velocity=Vector3.ProjectOnPlane(p.velocity,Vector3.up);
                float ratio=Vector3.Dot(root-previousRoot,velocity.normalized)/(velocity.magnitude*dt);
                float angular=Quaternion.Angle(previousCamera,cam.transform.rotation)/dt;
                float actorRatio=Vector3.Dot(visual-previousActor,velocity.normalized)/(velocity.magnitude*dt);
                float rawRatio=Vector3.Dot(p.position-previousSimulation,velocity.normalized)/(velocity.magnitude*dt);
                float healthRatio=Vector3.Dot(healthAnchor-previousHealthAnchor,velocity.normalized)/(velocity.magnitude*dt);
                float acceleration=(angular-previousAngular)/dt;
                current.frames++;if(Mathf.Abs(ratio)<.02f)current.stalledFrames++;
                if(Mathf.Abs(actorRatio)<.02f)current.actorStalledFrames++;
                if(Mathf.Abs(rawRatio)<.02f)current.rawStalledFrames++;
                if(Mathf.Abs(healthRatio)<.02f)current.healthBarStalledFrames++;
                current.healthBarSquared+=(healthRatio-1)*(healthRatio-1);
                current.maxHealthBarActorGap=Mathf.Max(current.maxHealthBarActorGap,Vector3.ProjectOnPlane(healthAnchor-visual,Vector3.up).magnitude);
                current.actorSquared+=(actorRatio-1)*(actorRatio-1);current.rawSquared+=(rawRatio-1)*(rawRatio-1);current.seconds+=dt;
                current.maxActorTargetGap=Mathf.Max(current.maxActorTargetGap,Vector3.Distance(visual,root));
                current.speedSquared+=(ratio-1)*(ratio-1);
                current.angularSquared+=acceleration*acceleration;
                current.maxTargetLag=Mathf.Max(current.maxTargetLag,Vector3.Distance(root,p.position));
                current.maxFrameTime=Mathf.Max(current.maxFrameTime,dt);
                rows.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:F6},{2:F6},{3:F5},{4:F5},{5:F5},{6:F5},{7:F5},{8:F5},{9:F5},{10:F5},{11:F5},{12:F5},{13:F5},{14:F5},{15:F5}\n",current.direction,Time.realtimeSinceStartup,dt,p.position.x,p.position.z,root.x,root.z,cam.transform.position.x,cam.transform.position.y,cam.transform.position.z,ratio,angular,visual.x,visual.z,actorRatio,rawRatio);
                previousAngular=angular;
            }
            else previousAngular=0;
            previous=true;previousRoot=root;previousActor=visual;previousSimulation=p.position;previousCamera=cam.transform.rotation;previousHealthAnchor=healthAnchor;
            // Capture before the measured window: synchronous GPU readback must
            // not create an artificial long frame in the camera cadence sample.
            if(!captured&&age>.85f){captured=true;App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,current.direction+".png"));}
        }
        void EndPhase()
        {
            current.targetSpeedErrorRms=(float)Math.Sqrt(current.speedSquared/Math.Max(1,current.frames));
            current.actorSpeedErrorRms=(float)Math.Sqrt(current.actorSquared/Math.Max(1,current.frames));
            current.rawSpeedErrorRms=(float)Math.Sqrt(current.rawSquared/Math.Max(1,current.frames));
            current.healthBarSpeedErrorRms=(float)Math.Sqrt(current.healthBarSquared/Math.Max(1,current.frames));
            current.measuredFps=(float)(current.frames/Math.Max(.001,current.seconds));
            current.cameraAngularAccelerationRms=(float)Math.Sqrt(current.angularSquared/Math.Max(1,current.frames));
            phases.Add(current);
        }
        void Finish()
        {
            enabled=false;App.LocalInput.x=App.LocalInput.z=0;
            report.phases=phases.ToArray();
            report.ok=report.finite&&report.errors==0&&phases.Count==4&&phases.All(p=>p.frames>=8&&p.targetSpeedErrorRms<.20f&&p.stalledFrames<=p.frames*.05f&&p.actorSpeedErrorRms<.20f&&p.actorStalledFrames<=p.frames*.05f&&p.healthBarSpeedErrorRms<.20f&&p.healthBarStalledFrames<=p.frames*.05f&&p.maxHealthBarActorGap<.001f);
            File.WriteAllText(Path.Combine(folder,"camera-motion.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(folder,"camera-motion.csv"),rows.ToString());
            Debug.Log("ERA_CAMERA_MOTION "+JsonUtility.ToJson(report));Application.Quit(report.ok||allowFailure?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
    [DefaultExecutionOrder(-120)] public sealed class CameraMotionInputStage:MonoBehaviour
    {
        public CameraMotionDiagnostics owner;
        void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}
    }
}
