using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Era
{
    // Real GameApp/Simulation inputs, actual Animator evaluation and gameplay
    // instance. No SampleClip, no injected jump trajectory or movement speed.
    [DefaultExecutionOrder(125)]
    public sealed class ArcherRefinementDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Phase
        {
            public string name;public int frames,airFrames,shots,airShots,recordedFrames,castFrames,castWithLocomotionFrames;
            public float maxSpeed,maxRate,cyclesPerSecond,maxPlantError,maxGripError,maxQuiverDrift,maxAirThighMotion,maxShotOriginGap,maxShotRootLag,maxShotDirectionError;
            public bool groundedPinInAir,runInAir;public string[] clips,states;
        }
        [Serializable] public sealed class Report
        {
            public bool ok,finite=true;public int errors,requestedFps;public string version;public Phase[] phases;
            public int jumps,landings,shots,events;public string[] failures;
        }
        string[] names={"Idle","SlowWalk","Normal","Run","Diagonal","Reverse","Turn","IdleJump","RunJump","JumpShoot","DoubleJump","Fall","BackView","RunShoot","BowDraw","Stop"};
        float[] durations={1.2f,2,2,2.6f,2,2,2,1.8f,2.1f,2.8f,2.4f,1.8f,2,2.7f,2.1f,1.5f};
        readonly List<Phase> phases=new List<Phase>();readonly HashSet<string> clips=new HashSet<string>(),states=new HashSet<string>();
        readonly List<string> failures=new List<string>();readonly Report report=new Report();
        GameApp App=>GameApp.Instance;ArcherAnimationDriver actor;Phase current;EnemyState target;
        int phase=-1,lastShot,startJump,startLand,frameIndex,lastStateHash;float start,phaseStart,nextFrame,airTime,lastNormalized;
        bool entered,jumpSent,extraSent,record,airReference,actionSent;Quaternion firstAirThigh;Vector3 mount;
        string folder;Mesh baked;Transform platform;Light key;Texture2D frame;Font font;FrameCapture capture;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:Path.GetFullPath("Artifacts/ArcherRefinement");Directory.CreateDirectory(folder);
            i=Array.IndexOf(args,"--refinement-fps");report.requestedFps=i>=0?int.Parse(args[i+1]):30;record=Array.IndexOf(args,"--refinement-record")>=0;
            Application.targetFrameRate=report.requestedFps;QualitySettings.vSyncCount=0;report.version=Application.version;
            if(Array.IndexOf(args,"--master-quality")>=0)
            {names=names.Concat(new[]{"RunCast","RunCastShoot","TurnShoot","AirAim","IdleTurn","HitMove"}).ToArray();durations=durations.Concat(new[]{2.2f,2.6f,2.6f,2.2f,2f,2.2f}).ToArray();}
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Ember;App.Solo();App.State.multiplayer=true;
            App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;
            App.LocalPlayer.position=WorldMap.OnGround(0,0);App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.lastWeapon=(int)WeaponKind.Bow;App.LocalPlayer.invulnerability=999;
            start=Time.realtimeSinceStartup;baked=new Mesh();frame=new Texture2D(640,720,TextureFormat.RGB24,false);
            font=Font.CreateDynamicFontFromOSFont("Arial",16);capture=new FrameCapture(font);
            gameObject.AddComponent<ArcherRefinementInputStage>().owner=this;
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        public void Prepare()
        {
            if(!enabled||App.LocalPlayer==null)return;
            float now=Time.realtimeSinceStartup;var p=App.LocalPlayer;var input=App.LocalInput;
            p.pendingLevels=0;p.invulnerability=999;p.hp=p.maxHp;App.State.spawnTimer=100000;App.Paused=false;
            if(now-start<1.2f){input.x=input.z=0;return;}
            if(actor==null)
            {
                actor=FindObjectsByType<ArcherAnimationDriver>(FindObjectsSortMode.None).FirstOrDefault(a=>a.gameObject.activeInHierarchy&&a.GetComponentInParent<HeroMotion>().name==p.name);
                if(actor==null){if(now-start>10)Finish();return;}
                mount=actor.Bone("QuiverAttach").localPosition;startJump=p.jumpSerial;startLand=p.landSerial;lastShot=p.weaponCastSerial;
                foreach(var t in actor.actorRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=25;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=25;platform=floor.transform;platform.localScale=new Vector3(40,.06f,40);Destroy(floor.GetComponent<Collider>());floor.GetComponent<Renderer>().material.color=new Color(.32f,.34f,.35f);
                key=new GameObject("Refinement capture key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.55f;key.color=Color.white;key.transform.rotation=Quaternion.Euler(30,-25,0);
            }
            if(phase<0||now-phaseStart>durations[phase])
            {
                if(current!=null)EndPhase();phase++;if(phase>=names.Length){Finish();return;}
                current=new Phase{name=names[phase]};phaseStart=now;entered=jumpSent=extraSent=airReference=false;clips.Clear();states.Clear();airTime=0;nextFrame=0;frameIndex=0;
            }
            float age=now-phaseStart;
            if(!entered)
            {
                entered=true;actionSent=false;App.State.enemies.Clear();target=null;p.weapons=new[]{-1,-1};actor.SetAiming(false,0);p.extraJumps=current.name=="DoubleJump"?1:0;p.qCooldown=0;
                if(current.name=="AirAim")actor.SetAiming(true,30);
                if(current.name=="Reverse"||current.name=="BowDraw")actor.SetAiming(true,0);
                if(current.name=="RunShoot"||current.name=="JumpShoot"||current.name=="RunCastShoot"||current.name=="TurnShoot")
                {
                    p.weapons=new[]{(int)WeaponKind.Bow,-1};p.weaponTimers[0]=.25f;
                    target=App.Simulation.SpawnEnemy(EnemyKind.Skeleton);target.hp=target.maxHp=100000;target.frozen=1000;target.speed=0;target.spawnGrace=0;
                }
                if(current.name=="Fall"){p.position+=Vector3.up*2.2f;p.grounded=false;p.verticalSpeed=-2;}
            }
            input.x=0;input.z=0;input.yaw=p.yaw;input.jumpHeld=false;
            switch(current.name)
            {
                case "SlowWalk":input.z=.10f;break;
                case "Normal":input.z=.5f;break;
                case "Run":case "RunJump":case "RunShoot":input.z=1;break;
                case "RunCast":case "RunCastShoot":case "HitMove":input.z=1;break;
                case "Diagonal":input.x=input.z=.7071f;break;
                case "Reverse":input.z=-1;break;
                case "Turn":input.x=Mathf.Sin(age*Mathf.PI);input.z=Mathf.Cos(age*Mathf.PI);break;
                case "TurnShoot":input.x=Mathf.Sin(age*Mathf.PI*.6f);input.z=Mathf.Cos(age*Mathf.PI*.6f);break;
                case "IdleTurn":input.yaw=age*100;break;
                case "AirAim":input.z=.5f;break;
                case "JumpShoot":case "DoubleJump":input.z=.6f;break;
                case "BackView":input.z=.4f;break;
            }
            if(current.name.Contains("Jump")||current.name=="AirAim")
            {
                if(age>.18f&&!jumpSent){input.jump=true;jumpSent=true;}
                input.jumpHeld=age<.7f;
                if(current.name=="DoubleJump"&&age>.5f&&!extraSent){input.jump=true;extraSent=true;}
            }
            if(age>.25f&&!actionSent)
            {
                if(current.name=="RunCast"||current.name=="RunCastShoot"){input.primary=true;actionSent=true;}
                if(current.name=="HitMove"){p.hp-=5;actionSent=true;}
            }
            if(target!=null){target.position=p.position+Vector3.forward*6;target.frozen=1000;target.hp=target.maxHp;target.speed=0;}
        }
        void LateUpdate()
        {
            if(!enabled||actor==null||current==null)return;var p=App.LocalPlayer;float age=Time.realtimeSinceStartup-phaseStart;
            current.frames++;float speed=Vector3.ProjectOnPlane(p.velocity,Vector3.up).magnitude;current.maxSpeed=Mathf.Max(current.maxSpeed,speed);
            float rate=actor.animator.GetFloat("LocomotionRate");current.maxRate=Mathf.Max(current.maxRate,rate);
            var stateInfo=actor.animator.GetCurrentAnimatorStateInfo(0);
            if(actor.IsLocomotion&&speed>6&&age>.5f&&stateInfo.fullPathHash==lastStateHash&&!actor.animator.IsInTransition(0))
            {
                float advance=(stateInfo.normalizedTime-lastNormalized)/Mathf.Max(.001f,Time.unscaledDeltaTime);
                if(advance>0&&advance<10)current.cyclesPerSecond=Mathf.Max(current.cyclesPerSecond,advance);
            }
            lastNormalized=stateInfo.normalizedTime;lastStateHash=stateInfo.fullPathHash;
            current.maxPlantError=Mathf.Max(current.maxPlantError,actor.Motion.FootPlantError);current.maxGripError=Mathf.Max(current.maxGripError,actor.Motion.GripError);
            current.maxQuiverDrift=Mathf.Max(current.maxQuiverDrift,Vector3.Distance(mount,actor.Bone("QuiverAttach").localPosition));
            states.Add(actor.AirState);
            bool casting=false;
            for(int layer=0;layer<actor.animator.layerCount;layer++)if(layer==0||actor.animator.GetLayerWeight(layer)>.1f)
            {
                foreach(var info in actor.animator.GetCurrentAnimatorClipInfo(layer).Concat(actor.animator.GetNextAnimatorClipInfo(layer)))if(info.clip!=null&&info.weight>.08f)clips.Add(info.clip.name);
                casting|=actor.animator.GetCurrentAnimatorClipInfo(layer).Any(c=>c.clip!=null&&c.weight>.2f&&c.clip.name=="Archer_Cast");
            }
            if(casting&&speed>2){current.castFrames++;if(actor.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip!=null&&c.weight>.2f&&(c.clip.name.StartsWith("Archer_Run")||c.clip.name.StartsWith("Archer_Walk"))))current.castWithLocomotionFrames++;}
            if(!p.grounded)
            {
                current.airFrames++;airTime+=Time.unscaledDeltaTime;current.groundedPinInAir|=actor.Motion.LeftPlanted||actor.Motion.RightPlanted;
                if(airTime>.22f)
                {
                    current.runInAir|=actor.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip!=null&&(c.clip.name.StartsWith("Archer_Run")||c.clip.name.StartsWith("Archer_Walk"))&&c.weight>.12f);
                    if(actor.AirState=="Archer_Airborne"&&!actor.animator.IsInTransition(0))
                    {
                        if(!airReference){firstAirThigh=actor.leftThigh.localRotation;airReference=true;}
                        current.maxAirThighMotion=Mathf.Max(current.maxAirThighMotion,Quaternion.Angle(firstAirThigh,actor.leftThigh.localRotation));
                    }
                }
            }
            else {airTime=0;airReference=false;}
            if(p.weaponCastSerial>lastShot)
            {
                int count=p.weaponCastSerial-lastShot;current.shots+=count;if(!p.grounded)current.airShots+=count;lastShot=p.weaponCastSerial;
                current.maxShotOriginGap=Mathf.Max(current.maxShotOriginGap,Vector3.Distance(actor.LastArrowOrigin,ArcherShotProfile.Origin(p.position,p.weaponAimYaw)));
                current.maxShotRootLag=Mathf.Max(current.maxShotRootLag,Vector3.Distance(actor.actorRoot.position,p.position));
                current.maxShotDirectionError=Mathf.Max(current.maxShotDirectionError,Vector3.Angle(actor.LastArrowDirection,Quaternion.Euler(0,p.weaponAimYaw,0)*Vector3.forward));
            }
            if(current.frames%9==0)foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(baked,true);report.finite&=baked.vertices.All(v=>ActorMotionMath.Finite(v)&&v.magnitude<8);}
            bool world=current.name=="RunShoot"||current.name=="JumpShoot";
            var cam=App.View.Camera;cam.GetComponent<StillwaterLens>().enabled=false;cam.cullingMask=world?-1:1<<25;cam.clearFlags=world?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.22f,.25f,.26f);
            platform.gameObject.SetActive(!world);platform.position=WorldMap.OnGround(p.position.x,p.position.z)+Vector3.down*.045f;key.enabled=!world;
            float ground=WorldMap.Ground(p.position);var pivot=new Vector3(p.position.x,ground+(current.name.Contains("Jump")||current.name=="Fall"?2.1f:1.2f),p.position.z);
            cam.orthographic=true;cam.orthographicSize=current.name.Contains("Jump")||current.name=="Fall"?2.7f:1.55f;
            var offset=current.name=="BackView"?new Vector3(-3,1.1f,-6):new Vector3(4,1.1f,6);cam.transform.position=pivot+offset;cam.transform.LookAt(pivot);
            if(record&&age>=nextFrame)
            {
                string path=Path.Combine(folder,current.name);Directory.CreateDirectory(path);SaveFrame(cam,Path.Combine(path,frameIndex.ToString("D4")+".png"));frameIndex++;current.recordedFrames++;nextFrame+=.05f;
                if(frameIndex==5||frameIndex==10||frameIndex==15||frameIndex==20)SaveSurfaces(current.name+"-"+frameIndex);
                if(frameIndex==Mathf.RoundToInt(durations[phase]*10))capture.Save(Path.Combine(folder,current.name+".png"),cam,()=>{});
            }
        }
        void SaveFrame(Camera cam,string path)
        {
            var rt=RenderTexture.GetTemporary(640,720,24);var previous=RenderTexture.active;var old=cam.targetTexture;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;frame.ReadPixels(new Rect(0,0,640,720),0,0);frame.Apply();File.WriteAllBytes(path,frame.EncodeToPNG());}
            finally{cam.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        void SaveSurfaces(string name)
        {
            var surfaces=new List<ArcherMotionDiagnostics.Surface>();
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!skin.enabled)continue;skin.BakeMesh(baked,true);
                surfaces.Add(new ArcherMotionDiagnostics.Surface{name=skin.name,vertices=baked.vertices.Select(v=>actor.actorRoot.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray(),triangles=baked.triangles});
            }
            string path=Path.Combine(folder,"SurfacePoses");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,name+"-surfaces.json"),JsonUtility.ToJson(new ArcherMotionDiagnostics.Pose{surfaces=surfaces.ToArray()}));
        }
        void EndPhase(){current.clips=clips.OrderBy(s=>s).ToArray();current.states=states.OrderBy(s=>s).ToArray();phases.Add(current);}
        void Finish()
        {
            if(!enabled)return;enabled=false;report.phases=phases.ToArray();report.jumps=App.LocalPlayer.jumpSerial-startJump;report.landings=App.LocalPlayer.landSerial-startLand;
            report.shots=App.LocalPlayer.weaponCastSerial;report.events=actor!=null?actor.ArrowEventCount:0;
            void Check(bool condition,string message){if(!condition)failures.Add(message);}
            Phase Find(string name)=>phases.FirstOrDefault(p=>p.name==name);
            Check(actor!=null&&report.errors==0&&report.finite,"valid instance, no console errors, finite deformation");
            Check(phases.Count==names.Length,"all scenarios completed");Check(report.jumps>=4&&report.landings>=4,"real physics jumps and landings");
            foreach(string name in new[]{"IdleJump","RunJump","JumpShoot"})
            {
                var p=Find(name);Check(p!=null&&p.airFrames>4&&!p.runInAir&&!p.groundedPinInAir,"separate air pose and released foot plants: "+name);
                Check(p!=null&&new[]{"Archer_JumpStart","Archer_Airborne","Archer_Fall","Archer_Land"}.All(n=>p.states.Contains(n)),"complete physical air sequence: "+name);
            }
            Check(Find("JumpShoot")?.airShots>0,"authoritative shot while actually airborne");Check(report.shots==report.events,"one presentation release per authoritative shot");
            Check(Find("Run")?.maxRate<1.5f&&Find("Run")?.cyclesPerSecond<2.7f,"run cadence no longer frantic at normal gameplay speed");
            Check(phases.All(p=>p.maxPlantError<.035f&&p.maxQuiverDrift<.001f),"support feet and quiver mount stay bounded");
            report.failures=failures.ToArray();report.ok=failures.Count==0;File.WriteAllText(Path.Combine(folder,"refinement-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ARCHER_REFINEMENT "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;if(baked!=null)Destroy(baked);if(frame!=null)Destroy(frame);if(font!=null)Destroy(font);capture?.Dispose();}
    }
    [DefaultExecutionOrder(-120)] public sealed class ArcherRefinementInputStage:MonoBehaviour{public ArcherRefinementDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}}
}
