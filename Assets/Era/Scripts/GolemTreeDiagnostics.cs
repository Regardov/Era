using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(220)]
    public sealed class GolemTreeDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class RootSample{public Vector3 target,stopped;public bool outside,blocked;}
        [Serializable]sealed class Report{public string version=Application.version;public bool ok,model,windup,contact,recovery,damage,finite=true,planted=true;public int errors,attacks;public List<RootSample> roots=new List<RootSample>();public float raisedHand,contactHand=10,maxFootShift;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;string folder;float started,phaseAt,previousRemaining,previousHp,originalHp,firstDamage=-1;int phase,shots;EnemyState enemy;GolemMotion golem;Vector3 target,start,direction,footL,footR;readonly List<Vector3> targets=new List<Vector3>();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:Path.Combine(Application.dataPath,"../Artifacts/GolemTree");Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Ember;App.Solo();App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.lastWeapon=-1;App.State.enemies.Clear();App.State.resourceNodes.Clear();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;
            App.CameraYaw=0;App.CameraPitch=25;Application.targetFrameRate=150;QualitySettings.vSyncCount=0;
            foreach(var h in WorldTreeLayout.Collision.hulls)
            {
                var center=h.points.Aggregate(Vector2.zero,(a,b)=>a+b)/h.points.Length;
                foreach(var vertex in h.points)
                {
                    var q=Vector2.Lerp(vertex,center,.15f);var p=WorldMap.OnGround(q.x,q.y);if(q.magnitude<17||!WorldTreeLayout.ContainsTrunk(p,.4f))continue;
                    var edge=WorldTreeLayout.ResolveTrunk(p,.4f);var d=Vector3.ProjectOnPlane(edge-p,Vector3.up).normalized;var from=edge+d*1.8f;from.y=WorldMap.Ground(from);
                    if(d.sqrMagnitude>.9f&&WorldMap.ClearLocation(from,.4f)&&!targets.Any(t=>Vector3.Distance(t,p)<5)){targets.Add(p);break;}
                }
                if(targets.Count==4)break;
            }
            started=Time.realtimeSinceStartup;BeginRoot();
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name+".png"));
        void BeginRoot()
        {
            if(report.roots.Count>=targets.Count){BeginFight();return;}
            target=targets[report.roots.Count];var edge=WorldTreeLayout.ResolveTrunk(target,.4f);direction=Vector3.ProjectOnPlane(target-edge,Vector3.up).normalized;start=edge-direction*1.8f;start.y=WorldMap.Ground(start);
            App.LocalPlayer.position=start;App.LocalPlayer.velocity=Vector3.zero;phaseAt=Time.realtimeSinceStartup;
        }
        void BeginFight()
        {
            App.LocalInput.x=App.LocalInput.z=0;App.LocalPlayer.position=WorldMap.OnGround(0,-26.4f);App.LocalPlayer.velocity=Vector3.zero;
            App.LocalPlayer.maxHp=App.LocalPlayer.hp=10000;App.LocalPlayer.invulnerability=0;App.LocalPlayer.armor=0;originalHp=previousHp=App.LocalPlayer.hp;
            enemy=App.Simulation.SpawnEnemy(EnemyKind.StoneGolem);enemy.position=WorldMap.OnGround(0,-24);enemy.spawnGrace=enemy.frozen=0;enemy.hp=enemy.maxHp=100000;enemy.attackTimer=0;enemy.speed=0;enemy.facing=180;App.CameraPitch=19;phase=1;phaseAt=Time.realtimeSinceStartup;
        }
        void Update()
        {
            if(App?.LocalPlayer==null)return;App.State.spawnTimer=100000;App.LocalPlayer.pendingLevels=0;
            if(Time.realtimeSinceStartup-started>45){Finish();return;}
            if(phase==0){App.LocalInput.x=direction.x;App.LocalInput.z=direction.z;App.LocalInput.yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;}
            else{App.LocalInput.x=App.LocalInput.z=0;App.LocalInput.yaw=0;App.LocalPlayer.hitSlide=0;App.LocalPlayer.knockback=Vector3.zero;}
        }
        void LateUpdate()
        {
            if(App?.LocalPlayer==null)return;float age=Time.realtimeSinceStartup-phaseAt;
            if(phase==0)
            {
                if(age<1)return;var p=App.LocalPlayer.position;
                report.roots.Add(new RootSample{target=target,stopped=p,outside=!WorldTreeLayout.ContainsTrunk(p,.399f),blocked=Vector3.Dot(p-target,direction)<-.05f&&Vector3.Distance(p,start)<2.2f});
                Shot("Root-block-"+report.roots.Count);BeginRoot();return;
            }
            if(golem==null)golem=FindAnyObjectByType<GolemMotion>();if(golem==null)return;
            report.model=golem.sourceTriangles==2880&&golem.joints.Length==15;
            foreach(var t in golem.joints)report.finite&=ActorMotionMath.Finite(t.position);
            var left=golem.joints.First(t=>t.name=="Foot_L");var right=golem.joints.First(t=>t.name=="Foot_R");
            if(footL==Vector3.zero){footL=left.position;footR=right.position;}
            report.maxFootShift=Mathf.Max(report.maxFootShift,Mathf.Max(Vector3.Distance(left.position,footL),Vector3.Distance(right.position,footR)));
            report.planted&=report.maxFootShift<.08f;
            float hand=golem.joints.First(t=>t.name=="Hand_R").position.y-WorldMap.Ground(enemy.position);
            if(enemy.meleeWindup>0&&golem.AttackPhase>.52f&&golem.AttackPhase<.70f){report.windup=true;report.raisedHand=Mathf.Max(report.raisedHand,hand);if((shots&1)==0){Shot("Golem-windup");shots|=1;}}
            if(previousRemaining>0&&enemy.meleeWindup<=0){report.attacks++;report.contact=true;report.contactHand=Mathf.Min(report.contactHand,hand);Shot("Golem-contact");}
            if(enemy.recovery>.15f&&enemy.recovery<.4f){report.recovery=true;if((shots&2)==0){Shot("Golem-recovery");shots|=2;}}
            if(App.LocalPlayer.hp<previousHp){report.damage=true;if(firstDamage<0)firstDamage=Time.realtimeSinceStartup;}
            previousHp=App.LocalPlayer.hp;previousRemaining=enemy.meleeWindup;
            if(report.attacks>=2&&enemy.recovery<=0){Finish();}
        }
        void Finish()
        {
            report.ok=report.roots.Count>=2&&report.roots.TrueForAll(r=>r.outside&&r.blocked)&&report.model&&report.windup&&report.contact&&report.recovery&&report.damage&&report.attacks>=2&&report.raisedHand>2.5f&&report.contactHand<.65f&&report.finite&&report.planted&&report.errors==0;
            File.WriteAllText(Path.Combine(folder,"golem-tree-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_GOLEM_TREE_RUNTIME "+report.ok);enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
