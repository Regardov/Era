using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Era
{
    // Uses the live ArenaView instance and ordinary Animator evaluation, never
    // SampleClip/Animator.Update. Scripted inputs make combinations repeatable.
    [DefaultExecutionOrder(120)]
    public sealed class ArcherMotionDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Phase
        {
            public string name;public int frames,shots;public float chest,spine,shoulder,thigh,cape,quiver;
            public float maxBank,maxCloak,maxQuiver,maxGripError,maxAimError,quiverMountDrift,phaseAdvance;
            public string[] clips;
        }
        [Serializable] public sealed class Report
        {
            public bool ok,finite=true,projectile;public int errors,requestedFps,frames,shots,arrowEvents;
            public float measuredFps,settledCloak,settledQuiver;public Phase[] phases;
        }
        [Serializable] public sealed class Surface {public string name;public Vector3[] vertices;public int[] triangles;}
        [Serializable] public sealed class Pose {public Surface[] surfaces;}
        readonly string[] names={"Idle","Walk","Accelerate","RunForward","RunAim","RunFire","Turn90","Turn180","TurnFire","StrafeLeft","StrafeRight","Backpedal","Decelerate","Settled","Roll","ReturnRun"};
        readonly float[] durations={1.4f,1.4f,1.4f,1.6f,2.3f,2.8f,1.8f,2,2.8f,1.8f,1.8f,1.8f,1.5f,1.8f,1.1f,1.6f};
        readonly List<Phase> results=new List<Phase>();readonly HashSet<string> observed=new HashSet<string>();
        readonly Report report=new Report();
        GameApp App=>GameApp.Instance;ArcherAnimationDriver actor;Transform[] bones;Quaternion[] initial;
        Vector3 initialMount,position;float initialNormalized,started,phaseStart;int phase=-1,lastSerial;bool entered,measuring;
        string folder;FrameCapture capture;Font font;Mesh baked;Texture2D strip;int stripCount;float nextCapture;
        Phase current;EnemyState target;Transform platform;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath("Artifacts/ArcherMotion");Directory.CreateDirectory(folder);
            at=Array.IndexOf(args,"--motion-fps");report.requestedFps=at>=0?int.Parse(args[at+1]):30;
            Application.targetFrameRate=report.requestedFps;QualitySettings.vSyncCount=0;
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Ember;App.Solo();App.State.multiplayer=true;
            App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;
            App.LocalPlayer.invulnerability=999;App.LocalPlayer.pendingLevels=0;App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.lastWeapon=(int)WeaponKind.Bow;
            position=WorldMap.OnGround(0,0);started=Time.realtimeSinceStartup;
            font=Font.CreateDynamicFontFromOSFont("Arial",16);capture=new FrameCapture(font);baked=new Mesh();
            gameObject.AddComponent<ArcherMotionScenarioStage>().owner=this;
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        public void PrepareFrame()
        {
            if(!enabled)return;float now=Time.realtimeSinceStartup;if(now-started<1.2f)return;
            if(actor==null)
            {
                actor=FindObjectsByType<ArcherAnimationDriver>(FindObjectsSortMode.None).FirstOrDefault(a=>a.gameObject.activeInHierarchy&&a.GetComponentInParent<HeroMotion>().name==App.LocalPlayer.name);
                if(actor==null){if(now-started>12)Finish();return;}
                bones=new[]{actor.chest,actor.spine,actor.Bone("Clavicle.L"),actor.leftThigh,actor.Bone("Cape2.C"),actor.Bone("QuiverAttach")};initial=new Quaternion[bones.Length];initialMount=bones[5].localPosition;
                foreach(var t in actor.actorRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=25;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Motion diagnostic floor";floor.layer=25;platform=floor.transform;
                platform.position=position+Vector3.down*.08f;platform.localScale=new Vector3(60,.1f,60);Destroy(floor.GetComponent<Collider>());
                floor.GetComponent<Renderer>().material.color=new Color(.3f,.32f,.33f);
            }
            if(phase<0||now-phaseStart>durations[phase])
            {
                if(current!=null)EndPhase();phase++;if(phase>=names.Length){Finish();return;}
                phaseStart=now;entered=false;measuring=false;observed.Clear();current=new Phase{name=names[phase]};stripCount=0;nextCapture=.55f;
                strip=new Texture2D(1600,900,TextureFormat.RGB24,false);
            }
            float age=now-phaseStart;var p=App.LocalPlayer;App.Paused=false;p.pendingLevels=0;p.invulnerability=999;p.grounded=true;p.crouching=false;p.downed=false;p.hp=p.maxHp;
            if(!entered)
            {
                entered=true;p.weapons=new[]{-1,-1};actor.SetAiming(false,0);App.State.enemies.Clear();target=null;
                if(current.name=="RunAim"||current.name.StartsWith("Turn")||current.name=="StrafeLeft"||current.name=="StrafeRight"||current.name=="Backpedal")actor.SetAiming(true,0);
                if(current.name=="RunFire"||current.name=="TurnFire")
                {
                    actor.SetAiming(false,0);p.weapons=new[]{(int)WeaponKind.Bow,-1};p.weaponTimers[0]=.35f;
                    target=App.Simulation.SpawnEnemy(EnemyKind.Skeleton);target.hp=target.maxHp=100000;target.frozen=1000;target.speed=0;target.spawnGrace=0;
                }
                if(current.name=="Roll")actor.PlayDodgeRoll();
                lastSerial=p.weaponCastSerial;
            }
            float speed=7.8f,yaw=0;
            if(current.name=="Idle"||current.name=="Settled"||current.name=="Roll")speed=0;
            if(current.name=="Walk")speed=actor.walkSpeed;
            if(current.name=="Accelerate")speed=7.8f*Mathf.Clamp01(age/.6f);
            if(current.name=="Decelerate")speed=7.8f*(1-Mathf.Clamp01(age/.5f));
            if(current.name=="Turn90")yaw=90*Mathf.Clamp01(age/.7f);
            if(current.name=="Turn180")yaw=age>.3f?180:0;
            if(current.name=="TurnFire")yaw=120*Mathf.Sin(age*2.2f);
            if(current.name=="StrafeLeft")yaw=-90;
            if(current.name=="StrafeRight")yaw=90;
            if(current.name=="Backpedal")yaw=180;
            var velocity=Quaternion.Euler(0,yaw,0)*Vector3.forward*speed;
            position+=velocity*Mathf.Min(Time.unscaledDeltaTime,.1f);position.y=WorldMap.Height(position.x,position.z);
            p.position=position;p.velocity=velocity;p.yaw=yaw;
            if(target!=null){target.position=position+Quaternion.Euler(0,current.name=="TurnFire"?yaw+35:0,0)*Vector3.forward*6;target.frozen=1000;target.hp=target.maxHp;target.speed=0;}
            report.projectile|=App.State.projectiles.Any(x=>x.owner==p.id&&x.weapon==WeaponKind.Bow);
            if(p.weaponCastSerial>lastSerial){current.shots+=p.weaponCastSerial-lastSerial;lastSerial=p.weaponCastSerial;}
        }
        void LateUpdate()
        {
            if(!enabled||actor==null||current==null||phase>=names.Length)return;
            float age=Time.realtimeSinceStartup-phaseStart;current.frames++;report.frames++;
            foreach(int layer in new[]{0,1})if(layer==0||actor.animator.GetLayerWeight(layer)>.1f)foreach(var clip in actor.animator.GetCurrentAnimatorClipInfo(layer).Concat(actor.animator.GetNextAnimatorClipInfo(layer)))if(clip.clip!=null&&clip.weight>.1f)observed.Add(clip.clip.name);
            if(age>.45f&&!measuring){measuring=true;for(int i=0;i<bones.Length;i++)initial[i]=bones[i].localRotation;initialNormalized=actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;}
            if(measuring)
            {
                current.chest=Mathf.Max(current.chest,Quaternion.Angle(initial[0],bones[0].localRotation));
                current.spine=Mathf.Max(current.spine,Quaternion.Angle(initial[1],bones[1].localRotation));
                current.shoulder=Mathf.Max(current.shoulder,Quaternion.Angle(initial[2],bones[2].localRotation));
                current.thigh=Mathf.Max(current.thigh,Quaternion.Angle(initial[3],bones[3].localRotation));
                current.cape=Mathf.Max(current.cape,Quaternion.Angle(initial[4],bones[4].localRotation));
                current.quiver=Mathf.Max(current.quiver,Quaternion.Angle(initial[5],bones[5].localRotation));
                current.phaseAdvance=Mathf.Max(current.phaseAdvance,actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime-initialNormalized);
                current.maxBank=Mathf.Max(current.maxBank,Mathf.Abs(actor.Motion.BodyLean));current.maxCloak=Mathf.Max(current.maxCloak,actor.Motion.CloakDeflection);
                current.maxQuiver=Mathf.Max(current.maxQuiver,actor.Motion.QuiverDeflection);current.maxGripError=Mathf.Max(current.maxGripError,actor.Motion.GripError);
                if(age>1.3f)current.maxAimError=Mathf.Max(current.maxAimError,actor.Motion.AimError);
                current.quiverMountDrift=Mathf.Max(current.quiverMountDrift,Vector3.Distance(initialMount,bones[5].localPosition));
            }
            if(current.frames%9==0)foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(baked);report.finite&=baked.vertices.All(v=>ActorMotionMath.Finite(v)&&v.magnitude<8);}
            platform.position=actor.actorRoot.position+Vector3.down*.08f;
            var cam=App.View.Camera;cam.cullingMask=1<<25;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.19f,.22f,.23f);
            cam.orthographic=false;cam.fieldOfView=38;cam.transform.position=actor.actorRoot.position+new Vector3(3.8f,2.15f,5.8f);cam.transform.LookAt(actor.actorRoot.position+Vector3.up*1.15f);cam.GetComponent<StillwaterLens>().enabled=false;
            if(age>=nextCapture&&stripCount<4)
            {
                CaptureStrip(cam);SavePose(current.name+"-"+stripCount);nextCapture+=Mathf.Max(.2f,(durations[phase]-.65f)/4);
                if(stripCount==2&&new[]{"RunAim","RunFire","TurnFire","Turn180","Backpedal","Decelerate"}.Contains(current.name))capture.Save(Path.Combine(folder,current.name+".png"),cam,()=>{});
                if(stripCount==2&&current.name=="RunFire")
                {
                    platform.gameObject.SetActive(false);cam.cullingMask=-1;cam.fieldOfView=60;
                    var rotation=Quaternion.Euler(App.CameraPitch,App.CameraYaw,0);var pivot=actor.actorRoot.position+Vector3.up*1.65f;
                    cam.transform.position=pivot+rotation*Vector3.back*5;cam.transform.LookAt(pivot+rotation*Vector3.forward*1.2f);
                    capture.Save(Path.Combine(folder,"RunFire-game-view.png"),cam,()=>{});platform.gameObject.SetActive(true);cam.cullingMask=1<<25;
                }
            }
        }
        void SavePose(string name)
        {
            // Optional offline surface-contact audit uses the real combined
            // Animator + IK + secondary pose, not the individual source clip.
            if(report.requestedFps!=30)return;
            var surfaces=new List<Surface>();
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Compensate renderer scale before TransformPoint; otherwise
                // the 2.23/1.8 visual scale is applied twice around each mesh
                // origin, manufacturing false surface intersections.
                if(!skin.enabled)continue;skin.BakeMesh(baked,true);
                surfaces.Add(new Surface{name=skin.name,vertices=baked.vertices.Select(v=>actor.actorRoot.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray(),triangles=baked.triangles});
            }
            File.WriteAllText(Path.Combine(folder,name+"-surfaces.json"),JsonUtility.ToJson(new Pose{surfaces=surfaces.ToArray()}));
        }
        void CaptureStrip(Camera cam)
        {
            var rt=RenderTexture.GetTemporary(400,900,24);var old=RenderTexture.active;var target=cam.targetTexture;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;strip.ReadPixels(new Rect(0,0,400,900),stripCount*400,0);strip.Apply();stripCount++;}
            finally{cam.targetTexture=target;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
        }
        void EndPhase()
        {
            current.clips=observed.OrderBy(x=>x).ToArray();results.Add(current);
            if(stripCount>0)File.WriteAllBytes(Path.Combine(folder,current.name+"-sequence.png"),strip.EncodeToPNG());Destroy(strip);strip=null;
            if(current.name=="Settled"){report.settledCloak=actor.Motion.CloakDeflection;report.settledQuiver=actor.Motion.QuiverDeflection;}
        }
        void Finish()
        {
            if(!enabled)return;enabled=false;report.phases=results.ToArray();report.measuredFps=report.frames/Mathf.Max(1,Time.realtimeSinceStartup-started-1.2f);
            report.shots=App.LocalPlayer.weaponCastSerial;report.arrowEvents=actor!=null?actor.ArrowEventCount:0;
            Phase Find(string name)=>results.FirstOrDefault(p=>p.name==name);
            report.ok=actor!=null&&report.errors==0&&report.finite&&report.projectile&&report.shots>=4&&report.arrowEvents==report.shots&&
                Find("RunAim")?.chest>1&&Find("RunAim")?.spine>1&&Find("RunAim")?.shoulder>.5f&&Find("RunAim")?.cape>1&&
                Find("RunFire")?.thigh>10&&Find("RunFire")?.chest>1&&Find("Turn180")?.maxBank>.4f&&Find("Turn180")?.maxQuiver>.1f&&
                Find("StrafeLeft").clips.Contains("Archer_RunLeft")&&Find("StrafeRight").clips.Contains("Archer_RunRight")&&Find("Backpedal").clips.Contains("Archer_RunBack")&&
                Find("ReturnRun")?.phaseAdvance>.5f&&report.settledCloak<.3f&&report.settledQuiver<.1f&&results.All(p=>p.maxGripError<.065f&&p.quiverMountDrift<.001f&&p.maxBank<=9&&p.maxQuiver<=3.01f);
            File.WriteAllText(Path.Combine(folder,"motion-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ARCHER_MOTION_RUNTIME "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;capture?.Dispose();if(font!=null)Destroy(font);if(strip!=null)Destroy(strip);if(baked!=null)Destroy(baked);}
    }
    [DefaultExecutionOrder(-45)] public sealed class ArcherMotionScenarioStage:MonoBehaviour{public ArcherMotionDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.PrepareFrame();}}
}
