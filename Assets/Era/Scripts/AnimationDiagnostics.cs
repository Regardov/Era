using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(40)]
    public sealed class AnimationDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report {public bool ok,finite=true,paused,cleanup,actors,weapons,abilities;public int errors,maxEnemies,maxProjectiles,maxEffects,peakFeedback,peakContours,frames,remainingCorpses,remainingProjectiles,remainingPickups,remainingFeedback;public float averageFrameMs,maxFrameMs;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;string folder;float started,frameSum,pausedAt;int phase;Vector3 frozenPosition;ProjectileVisual frozenProjectile;float oldIntensity;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;oldIntensity=PresentationMotion.Intensity;PresentationMotion.Intensity=.75f;
            App.Solo();App.State.multiplayer=true;App.Simulation.AddPlayer(1,"Echo",Hero.Echo);App.Simulation.AddPlayer(2,"Lumen",Hero.Lumen);
            App.State.spawnTimer=100000;App.State.resourceNodes.Clear();App.State.enemies.Clear();App.CameraYaw=0;App.CameraPitch=32;
            for(int i=0;i<3;i++){var p=App.State.players[i];p.position=WorldMap.OnGround((i-1)*3,0);p.invulnerability=999;p.weapons=new[]{i*2,i*2+1};p.weaponTimers=new float[2];p.shield=40;}
            App.State.players[2].auraLevels[0]=2;App.State.players[2].auraLevels[1]=3;
            for(int i=0;i<GameData.MaxEnemies;i++){var kind=(EnemyKind)(i%16);if(kind==EnemyKind.Warden&&i>15)kind=EnemyKind.Skeleton;var e=App.Simulation.SpawnEnemy(kind);e.position=WorldMap.OnGround((i%15-7)*1.7f,7+(i/15)*2);e.hp=e.maxHp=100000;e.spawnGrace=.25f;e.wave=0;}
            started=Time.realtimeSinceStartup;
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)report.errors++;}
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;var state=App.State;if(state==null)return;
            if(phase>=5)foreach(var p in state.players)for(int i=0;i<20&&p.pendingLevels>0;i++)App.Simulation.ChooseItem(p,0);
            if(phase<3)
            {
                foreach(var p in state.players){p.invulnerability=999;p.pendingLevels=0;p.qCooldown=p.eCooldown=p.relicCooldown=0;}
                if(phase==0&&age>1)
                {
                    var input=new Dictionary<int,InputFrame>();for(int i=0;i<3;i++)input[i]=new InputFrame{primary=true,secondary=true,relic=true};App.Simulation.Step(1f/30,input);
                    report.abilities=state.players.TrueForAll(p=>p.castSerial>0);phase=1;
                }
                if(phase==1&&age>2)
                {
                    state.projectiles.Clear();for(int i=0;i<GameData.MaxProjectiles;i++)state.projectiles.Add(new ProjectileState{id=state.nextId++,owner=i%3,position=WorldMap.OnGround((i%24-12)*.55f,4+i/24*.5f)+Vector3.up, direction=Vector3.forward,speed=.1f,life=30,weapon=WeaponKind.Bow});
                    state.effects.Clear();for(int i=0;i<GameData.MaxEffects;i++)state.effects.Add(new EffectState{id=state.nextId++,kind=i%13,position=WorldMap.OnGround((i%16-8)*1.2f,4+i/16*.7f),end=WorldMap.OnGround((i%16-8)*1.2f+1,6+i/16*.7f)+Vector3.up,radius=1.2f,life=1,weapon=(WeaponKind)(i%6)});
                    report.maxProjectiles=state.projectiles.Count;report.maxEffects=state.effects.Count;report.maxEnemies=state.enemies.Count;Shot("animation-burst-v018.png");phase=2;
                }
                if(phase==2&&age>3){Shot("animation-density-v018.png");report.actors=FindObjectsByType<CreatureMotion>(FindObjectsSortMode.None).Length>=90;report.weapons=FindObjectsByType<WeaponPresentation>(FindObjectsSortMode.None).Length>=3;phase=3;}
            }
            if(phase==3&&age>4){state.multiplayer=false;App.Paused=true;frozenProjectile=FindAnyObjectByType<ProjectileVisual>();if(frozenProjectile!=null)frozenPosition=frozenProjectile.transform.position;pausedAt=state.elapsed;phase=4;}
            else if(phase==4&&age>4.5f){report.paused=Mathf.Abs(state.elapsed-pausedAt)<.0001f&&frozenProjectile!=null&&(frozenProjectile.transform.position-frozenPosition).sqrMagnitude<.000001f;App.Paused=false;state.multiplayer=true;phase=5;}
            else if(phase==5&&age>5)
            {
                foreach(var e in state.enemies.ToArray())App.Simulation.DamageEnemy(e,App.LocalPlayer,1000000);
                state.projectiles.Clear();state.effects.Clear();state.pickups.Clear();state.loot.Clear();foreach(var p in state.players)p.weapons=new[]{-1,-1};Shot("animation-deaths-v018.png");phase=6;
            }
            else if(phase==6&&age>8){state.phase=RunPhase.Lobby;state.stageSerial++;phase=7;}
            else if(phase==7&&age>9){report.remainingCorpses=CorpseFade.ActiveCount;report.remainingProjectiles=ProjectileVisual.RetiringCount;report.remainingPickups=PickupRetirement.ActiveCount;report.remainingFeedback=PresentationFx.Instance.ActiveCount;report.cleanup=report.remainingCorpses+report.remainingProjectiles+report.remainingPickups+report.remainingFeedback==0;Finish();return;}
            var fx=PresentationFx.Instance;var contours=FindAnyObjectByType<EffectPresentation>();if(fx!=null)report.peakFeedback=Mathf.Max(report.peakFeedback,fx.ActiveCount);if(contours!=null)report.peakContours=Mathf.Max(report.peakContours,contours.ActiveCount);
            if(age>1&&age<4){float ms=Time.unscaledDeltaTime*1000;frameSum+=ms;report.frames++;report.maxFrameMs=Mathf.Max(report.maxFrameMs,ms);}
            if(age>35){Finish();return;}
        }
        void LateUpdate(){if(phase>0&&phase<7)foreach(var actor in FindObjectsByType<CreatureMotion>(FindObjectsSortMode.None)){report.finite&=ActorMotionMath.Finite(actor.transform.position)&&ActorMotionMath.Finite(actor.transform.localScale);if(actor.body!=null)report.finite&=ActorMotionMath.Finite(actor.body.localScale);}}
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Finish(){report.averageFrameMs=frameSum/Mathf.Max(1,report.frames);report.ok=report.errors==0&&report.finite&&report.paused&&report.cleanup&&report.actors&&report.weapons&&report.abilities&&report.maxProjectiles==GameData.MaxProjectiles&&report.maxEffects==GameData.MaxEffects&&report.peakFeedback<=PresentationFx.Capacity&&report.peakContours<=EffectPresentation.Capacity;File.WriteAllText(Path.Combine(folder,"smoke-animation.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ANIMATION "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);enabled=false;}
        void OnDestroy(){Application.logMessageReceived-=Log;PresentationMotion.Intensity=oldIntensity;}
    }
}
