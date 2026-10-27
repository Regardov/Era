using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Era
{
    // Measures the real ArenaView-created actor over time. Never samples clips
    // or advances the Animator manually; this catches runtime binding failures.
    [DefaultExecutionOrder(115)]
    public sealed class ArcherRuntimeDiagnostics : MonoBehaviour
    {
        [Serializable] public sealed class ClipResult { public string name; public bool exists,imported,assigned,runtimeObserved; public float seconds; }
        [Serializable] public sealed class PhaseResult
        {
            public string name; public int frames; public float thighDegrees,handMeters,pelvisMeters,nockMeters,normalizedAdvance;
            public bool animatorEnabled,avatarValid; public int shots,releaseStates; public string[] observedClips;
        }
        [Serializable] public sealed class OtherResult { public string hero; public float walkDegrees,runDegrees; }
        [Serializable] public sealed class Report
        {
            public bool ok,finite=true; public int errors,shots,arrowEvents,shotsWithoutRelease; public string version; public int sourceBones,skins;
            public PhaseResult[] phases; public ClipResult[] clips; public OtherResult[] others;
            public bool arrowVisibleDuringDraw,projectileObserved,draftDeathEnabledOnlyForTest;
        }
        readonly string[] phaseNames={"Idle","Walk","Run","AutoFire","CastFire","Hold","Hit","Dash","Cast","Interact","TeleportOut","TeleportIn","DodgeRoll","Death"};
        readonly float[] durations={2.8f,1.8f,1.6f,3.5f,1.3f,1.8f,.65f,.7f,1f,.8f,.85f,.8f,.85f,1.8f};
        readonly List<PhaseResult> results=new List<PhaseResult>(); readonly HashSet<string> observed=new HashSet<string>();
        readonly HashSet<string> phaseObserved=new HashSet<string>(); readonly Report report=new Report();
        sealed class PendingShot { public PhaseResult phase; public int count,frame; public float deadline; }
        readonly List<PendingShot> pendingShots=new List<PendingShot>();
        readonly Dictionary<Hero,OtherResult> other=new Dictionary<Hero,OtherResult>();
        readonly Dictionary<Hero,Quaternion> otherStart=new Dictionary<Hero,Quaternion>();
        GameApp App=>GameApp.Instance; ArcherAnimationDriver actor; Mesh baked; FrameCapture capture; Font font;
        string folder; int phase=-1,seenArrowEvents; float started,phaseStart; bool entered,shot,motionStarted; PhaseResult current;
        Quaternion initialThigh; Vector3 initialHand,initialPelvis,initialNock; float initialNormalized;
        EnemyState target;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Ember;App.Solo();App.State.multiplayer=true;
            App.Simulation.AddPlayer(1,"Echo",Hero.Echo);App.Simulation.AddPlayer(2,"Lumen",Hero.Lumen);
            App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;App.CameraYaw=0;
            foreach(var p in App.State.players){p.invulnerability=999;p.pendingLevels=0;p.weapons=p.hero==Hero.Ember?new[]{(int)WeaponKind.Bow,-1}:new[]{-1,-1};p.weaponTimers=new float[2];p.lastWeapon=p.hero==Hero.Ember?(int)WeaponKind.Bow:-1;}
            started=phaseStart=Time.realtimeSinceStartup;baked=new Mesh();font=Font.CreateDynamicFontFromOSFont("Arial",16);capture=new FrameCapture(font);
            gameObject.AddComponent<ArcherRuntimePoseStage>().owner=this;
            foreach(var h in new[]{Hero.Echo,Hero.Lumen})other[h]=new OtherResult{hero=h.ToString()};
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        public void PrepareFrame()
        {
            if(!enabled)return;float now=Time.realtimeSinceStartup;if(now-started<1)return;
            if(actor==null)actor=FindObjectsByType<ArcherAnimationDriver>(FindObjectsSortMode.None).FirstOrDefault(a=>a.gameObject.activeInHierarchy&&a.GetComponentInParent<HeroMotion>().name==App.LocalPlayer.name);
            if(actor==null){if(now-started>12)Finish();return;}
            if(phase<0||now-phaseStart>durations[phase])
            {
                if(current!=null){current.observedClips=phaseObserved.OrderBy(x=>x).ToArray();results.Add(current);}
                phase++;phaseStart=now;entered=false;shot=false;motionStarted=false;phaseObserved.Clear();otherStart.Clear();
                if(phase>=phaseNames.Length){Finish();return;}
                current=new PhaseResult{name=phaseNames[phase]};
            }
            var p=App.LocalPlayer;App.Paused=false;p.pendingLevels=0;p.invulnerability=999;p.grounded=true;p.crouching=false;
            p.position=WorldMap.OnGround(0,0);p.velocity=Vector3.forward*(current.name=="Walk"?actor.walkSpeed:current.name=="Run"?7.8f:0);p.yaw=0;
            foreach(var ally in App.State.players.Where(x=>x.id!=p.id)){ally.pendingLevels=0;ally.invulnerability=999;ally.grounded=true;ally.position=WorldMap.OnGround((ally.id==1?-1:1)*2.6f,0);ally.velocity=p.velocity;ally.yaw=0;}
            if(!entered)
            {
                entered=true;actor.SetAiming(false,0);p.downed=false;p.hp=p.maxHp;actor.useDraftDeathAnimation=false;
                initialThigh=actor.leftThigh.localRotation;initialHand=actor.Bone("Hand.R").localPosition;initialPelvis=actor.pelvis.localPosition;initialNock=actor.arrowNock.position;
                initialNormalized=actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                switch(current.name)
                {
                    case "AutoFire":target=App.Simulation.SpawnEnemy(EnemyKind.Skeleton);target.position=p.position+Vector3.forward*6;target.hp=target.maxHp=100000;target.frozen=1000;target.speed=0;target.spawnGrace=0;p.weaponTimers[0]=.6f;break;
                    case "CastFire":App.State.enemies.Clear();target=App.Simulation.SpawnEnemy(EnemyKind.Skeleton);target.position=p.position+Vector3.forward*6;target.hp=target.maxHp=100000;target.frozen=1000;target.speed=0;target.spawnGrace=0;p.weaponTimers[0]=.08f;p.castSerial++;break;
                    case "Hold":App.State.enemies.Clear();actor.SetAiming(true,0);break;
                    case "Hit":p.hp-=5;break;
                    case "Dash":p.dashTime=.35f;break;
                    case "Cast":p.castSerial++;break;
                    case "Interact":actor.PlayInteraction();break;
                    case "TeleportOut":actor.PlayTeleportOut();break;
                    case "TeleportIn":actor.PlayTeleportIn();break;
                    case "DodgeRoll":actor.PlayDodgeRoll();break;
                    case "Death":actor.useDraftDeathAnimation=true;report.draftDeathEnabledOnlyForTest=true;p.downed=true;break;
                }
            }
            if(current.name!="AutoFire"&&current.name!="CastFire")App.State.enemies.Clear();else if(target!=null){target.hp=target.maxHp;target.frozen=1000;target.speed=0;target.position=p.position+Vector3.forward*6;}
            report.shots=Mathf.Max(report.shots,p.weaponCastSerial);report.projectileObserved|=App.State.projectiles.Any(x=>x.owner==p.id&&x.weapon==WeaponKind.Bow);
            if(now-started>45)Finish();
        }
        void LateUpdate()
        {
            if(!enabled||actor==null||current==null||phase>=phaseNames.Length)return;
            float age=Time.realtimeSinceStartup-phaseStart;
            current.frames++;current.animatorEnabled|=actor.animator.enabled;current.avatarValid|=actor.animator.avatar!=null&&actor.animator.avatar.isValid;
            foreach(int layer in Enumerable.Range(0,actor.animator.layerCount))foreach(var clip in actor.animator.GetCurrentAnimatorClipInfo(layer).Concat(actor.animator.GetNextAnimatorClipInfo(layer)))if(clip.clip!=null&&clip.weight>.04f){observed.Add(clip.clip.name);phaseObserved.Add(clip.clip.name);}
            if(age>.25f&&!motionStarted)
            {
                motionStarted=true;initialThigh=actor.leftThigh.localRotation;initialHand=actor.actorRoot.InverseTransformPoint(actor.Bone("Hand.R").position);
                initialPelvis=actor.pelvis.localPosition;initialNock=actor.actorRoot.InverseTransformPoint(actor.arrowNock.position);initialNormalized=actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                otherStart.Clear();
            }
            if(motionStarted)
            {
                current.thighDegrees=Mathf.Max(current.thighDegrees,Quaternion.Angle(initialThigh,actor.leftThigh.localRotation));
                current.handMeters=Mathf.Max(current.handMeters,Vector3.Distance(initialHand,actor.actorRoot.InverseTransformPoint(actor.Bone("Hand.R").position)));
                current.pelvisMeters=Mathf.Max(current.pelvisMeters,Vector3.Distance(initialPelvis,actor.pelvis.localPosition));
                current.nockMeters=Mathf.Max(current.nockMeters,Vector3.Distance(initialNock,actor.actorRoot.InverseTransformPoint(actor.arrowNock.position)));
                current.normalizedAdvance=Mathf.Max(current.normalizedAdvance,actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime-initialNormalized);
            }
            report.arrowEvents=Mathf.Max(report.arrowEvents,actor.ArrowEventCount);report.arrowVisibleDuringDraw|=actor.nockedArrowRenderers.Any(x=>x.enabled)&&current.name=="AutoFire";
            // ArenaView requests presentation in LateUpdate, after Animator evaluation.
            // Require the actual weighted release clip on a subsequent evaluated frame.
            bool evaluatedRelease=actor.animator.GetCurrentAnimatorClipInfo(ArcherAnimationDriver.BowLayer).Concat(actor.animator.GetNextAnimatorClipInfo(ArcherAnimationDriver.BowLayer)).Any(c=>c.clip!=null&&c.clip.name=="Archer_BowRelease"&&c.weight>.04f)&&actor.animator.GetLayerWeight(ArcherAnimationDriver.BowLayer)>.1f;
            for(int i=pendingShots.Count-1;i>=0;i--)
            {
                var pending=pendingShots[i];
                if(Time.frameCount>pending.frame&&evaluatedRelease){pending.phase.releaseStates+=pending.count;pendingShots.RemoveAt(i);}
                else if(Time.realtimeSinceStartup>pending.deadline){report.shotsWithoutRelease+=pending.count;pendingShots.RemoveAt(i);}
            }
            if(actor.ArrowEventCount>seenArrowEvents)
            {
                int count=actor.ArrowEventCount-seenArrowEvents;current.shots+=count;
                pendingShots.Add(new PendingShot{phase=current,count=count,frame=Time.frameCount,deadline=Time.realtimeSinceStartup+.25f});
                seenArrowEvents=actor.ArrowEventCount;
            }
            foreach(var hero in FindObjectsByType<HeroMotion>(FindObjectsSortMode.None).Where(h=>h.gameObject.activeInHierarchy&&h.skeletal!=null&&other.ContainsKey(h.Hero)))
            {
                if(!otherStart.ContainsKey(hero.Hero))otherStart[hero.Hero]=hero.skeletal.leftThigh.localRotation;
                float deg=Quaternion.Angle(otherStart[hero.Hero],hero.skeletal.leftThigh.localRotation);
                if(current.name=="Walk")other[hero.Hero].walkDegrees=Mathf.Max(other[hero.Hero].walkDegrees,deg);
                if(current.name=="Run")other[hero.Hero].runDegrees=Mathf.Max(other[hero.Hero].runDegrees,deg);
            }
            if(current.frames%6==0)foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(baked);report.finite&=baked.vertices.All(v=>ActorMotionMath.Finite(v)&&v.magnitude<8);}
            var cam=App.View.Camera;cam.orthographic=true;cam.orthographicSize=1.85f;cam.transform.position=actor.actorRoot.position+new Vector3(4,2.1f,7);cam.transform.LookAt(actor.actorRoot.position+Vector3.up*1.2f);cam.GetComponent<StillwaterLens>().enabled=false;
            if(!shot&&age>durations[phase]*.58f){shot=true;capture.Save(Path.Combine(folder,"archer-"+current.name+"-v021.png"),cam,()=>{});}
        }
        void Finish()
        {
            if(!enabled)return;enabled=false;report.shotsWithoutRelease+=pendingShots.Sum(p=>p.count);report.version=Application.version;report.phases=results.ToArray();report.others=other.Values.ToArray();
            if(actor!=null){report.sourceBones=actor.Bone("Root").GetComponentsInChildren<Transform>(true).Length;report.skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length;report.clips=actor.clips.Select(c=>new ClipResult{name=c.name,exists=c.clip!=null,imported=c.clip!=null&&!c.clip.empty&&!c.clip.legacy,assigned=actor.animator.HasState(c.name.StartsWith("Archer_Bow")||c.name=="Archer_Aim"||c.name=="Archer_ShootRecovery"?1:0,Animator.StringToHash((c.name.StartsWith("Archer_Bow")||c.name=="Archer_Aim"||c.name=="Archer_ShootRecovery"?"UpperBody.":"Base Layer.")+c.name))||c.name=="Archer_Idle"||c.name.StartsWith("Archer_Walk")||c.name.StartsWith("Archer_Run"),runtimeObserved=observed.Contains(c.name),seconds=c.clip==null?0:c.clip.length}).ToArray();}
            PhaseResult Phase(string n)=>results.FirstOrDefault(x=>x.name==n);
            report.ok=actor!=null&&report.errors==0&&report.finite&&Phase("Idle")?.pelvisMeters>.0005f&&Phase("Walk")?.thighDegrees>8&&Phase("Walk")?.normalizedAdvance>.2f&&Phase("Run")?.thighDegrees>15&&Phase("Run")?.normalizedAdvance>.2f&&Phase("AutoFire")?.handMeters>.05f&&Phase("CastFire")?.shots>0&&report.shotsWithoutRelease==0&&report.shots>=2&&report.arrowEvents>=2&&report.projectileObserved&&report.arrowVisibleDuringDraw&&report.clips.All(c=>c.exists&&c.imported&&c.assigned)&&report.clips.Take(8).All(c=>c.runtimeObserved)&&report.others.All(x=>x.walkDegrees>5&&x.runDegrees>8);
            File.WriteAllText(Path.Combine(folder,"archer-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ARCHER_RUNTIME "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;if(baked!=null)Destroy(baked);capture?.Dispose();if(font!=null)Destroy(font);}
    }
    [DefaultExecutionOrder(-40)] public sealed class ArcherRuntimePoseStage:MonoBehaviour { public ArcherRuntimeDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.PrepareFrame();} }
}
