using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(220)]
    public sealed class CreatureLibraryDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Case
        {
            public string kind;public bool model,locomotion,attack,damage,finite=true;public int triangles,frames;
            public float distance,meanMovementBlend,minFootY=10,maxFootY=-10,footTravel,handTravel,wingTravel;
            [NonSerialized]public float blendSum,minFootZ=100,maxFootZ=-100,minHandZ=100,maxHandZ=-100,minWing=100,maxWing=-100;
        }
        [Serializable]sealed class Report{public string version=Application.version;public bool ok;public int errors;public List<Case> cases=new List<Case>();}
        readonly EnemyKind[] kinds={EnemyKind.StoneGolem,EnemyKind.CrystalGolem,EnemyKind.Bat};readonly Report report=new Report();
        GameApp App=>GameApp.Instance;Case current;EnemyState enemy;CreatureMotion creature;GolemMotion golem;BatMotion bat;
        string folder;int index,phase,shot;float started,phaseAt,health;Vector3 lane,from;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:Path.Combine(Application.dataPath,"../Artifacts/Creatures-v030-runtime");Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Ember;App.Solo();App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.lastWeapon=-1;App.State.resourceNodes.Clear();App.State.spawnTimer=100000;
            App.CameraYaw=180;App.CameraPitch=22;QualitySettings.vSyncCount=0;Application.targetFrameRate=150;started=Time.realtimeSinceStartup;Begin();
        }
        void Log(string m,string t,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        Vector3 Lane()
        {
            foreach(var landmark in new[]{new Vector3(105,0,80),new Vector3(60,0,-110),new Vector3(-95,0,70)})for(int k=0;k<24;k++)
            {
                var start=landmark+new Vector3((k%2==0?1:-1)*(k/2)*2,0,-6);bool clear=true;
                for(int n=0;n<=56;n++){var p=WorldMap.OnGround(start.x,start.z+n*.25f);if(!WorldMap.ClearLocation(p,1.3f)){clear=false;break;}}
                if(clear)return WorldMap.OnGround(start.x,start.z);
            }
            throw new InvalidOperationException("No clear creature lane");
        }
        void Begin()
        {
            if(index>=kinds.Length){Finish();return;}
            App.State.enemies.Clear();App.State.hazards.Clear();lane=Lane();App.LocalPlayer.position=WorldMap.OnGround(lane.x,lane.z+14);App.LocalPlayer.velocity=Vector3.zero;App.LocalPlayer.invulnerability=10000;
            enemy=App.Simulation.SpawnEnemy(kinds[index]);enemy.position=lane+(EnemyCatalog.Flying(enemy.kind)?Vector3.up*1.8f:Vector3.zero);enemy.facing=0;enemy.spawnGrace=enemy.frozen=0;enemy.hp=enemy.maxHp=100000;enemy.attackTimer=1000;
            from=enemy.position;creature=null;golem=null;bat=null;current=new Case{kind=enemy.kind.ToString()};phase=0;phaseAt=Time.realtimeSinceStartup;shot=0;
        }
        void Update()
        {
            if(App?.LocalPlayer==null||!enabled)return;App.State.spawnTimer=100000;App.LocalPlayer.pendingLevels=0;App.LocalInput.x=App.LocalInput.z=0;App.LocalPlayer.hitSlide=0;App.LocalPlayer.knockback=Vector3.zero;
            if(Time.realtimeSinceStartup-started>75){Finish();return;}
        }
        void LateUpdate()
        {
            if(!enabled||App?.LocalPlayer==null)return;
            if(creature==null)creature=FindObjectsByType<CreatureMotion>(FindObjectsSortMode.None).FirstOrDefault(m=>m.enabled&&m.kind==enemy.kind&&(m.golem!=null||m.bat!=null));
            if(creature==null)return;golem=creature.golem;bat=creature.bat;var joints=golem!=null?golem.joints:bat.joints;
            current.model=golem!=null?golem.sourceTriangles>=2800:bat.sourceTriangles>=1500;current.triangles=golem!=null?golem.sourceTriangles:bat.sourceTriangles;
            foreach(var t in joints)current.finite&=ActorMotionMath.Finite(t.position);
            float age=Time.realtimeSinceStartup-phaseAt;
            if(phase==0)
            {
                if(age>1.2f)
                {
                    current.frames++;current.blendSum+=golem!=null?golem.MovementBlend:bat.FlightBlend;
                    if(golem!=null)
                    {
                        var foot=joints.First(t=>t.name=="Foot_L");float z=creature.transform.InverseTransformPoint(foot.position).z;current.minFootZ=Mathf.Min(current.minFootZ,z);current.maxFootZ=Mathf.Max(current.maxFootZ,z);
                        var hand=joints.First(t=>t.name=="Hand_R");z=creature.transform.InverseTransformPoint(hand.position).z;current.minHandZ=Mathf.Min(current.minHandZ,z);current.maxHandZ=Mathf.Max(current.maxHandZ,z);
                        foreach(var f in foot.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);float y=p.y-WorldMap.Ground(p);current.minFootY=Mathf.Min(current.minFootY,y);current.maxFootY=Mathf.Max(current.maxFootY,y);}
                    }
                    else{current.minWing=Mathf.Min(current.minWing,bat.WingAngle);current.maxWing=Mathf.Max(current.maxWing,bat.WingAngle);}
                    if(shot<2&&age>1.3f+shot*.35f){App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,enemy.kind+"-move-"+shot+".png"));shot++;}
                }
                float duration=golem!=null?3.8f:2.4f;
                if(age<duration)return;
                current.distance=Vector3.ProjectOnPlane(enemy.position-from,Vector3.up).magnitude;current.meanMovementBlend=current.blendSum/Mathf.Max(1,current.frames);current.footTravel=golem!=null?current.maxFootZ-current.minFootZ:0;current.handTravel=golem!=null?current.maxHandZ-current.minHandZ:0;current.wingTravel=bat!=null?current.maxWing-current.minWing:0;if(bat!=null)current.minFootY=current.maxFootY=0;
                current.locomotion=current.frames>30&&current.distance>3&&current.meanMovementBlend>.80f&&(golem!=null?current.footTravel>.60f&&current.handTravel>.25f&&current.minFootY>-.085f:current.wingTravel>65);
                enemy.position=lane+(EnemyCatalog.Flying(enemy.kind)?Vector3.up*1.8f:Vector3.zero);enemy.facing=0;enemy.speed=0;enemy.attackTimer=0;enemy.contactTimer=0;
                float reach=enemy.kind==EnemyKind.StoneGolem?2.4f:1.3f;App.LocalPlayer.position=WorldMap.OnGround(lane.x,lane.z+reach);App.LocalPlayer.velocity=Vector3.zero;App.LocalPlayer.hp=App.LocalPlayer.maxHp=10000;App.LocalPlayer.invulnerability=0;health=App.LocalPlayer.hp;phase=1;phaseAt=Time.realtimeSinceStartup;return;
            }
            if(enemy.meleeWindup>0)current.attack=true;if(App.LocalPlayer.hp<health)current.damage=true;
            if(age>1.4f&&shot<3){App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,enemy.kind+"-attack.png"));shot++;}
            if(age<3)return;report.cases.Add(current);index++;Begin();
        }
        void Finish()
        {
            report.ok=report.errors==0&&report.cases.Count==3&&report.cases.TrueForAll(c=>c.model&&c.locomotion&&c.attack&&c.damage&&c.finite);
            File.WriteAllText(Path.Combine(folder,"creature-library-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_CREATURE_LIBRARY_RUNTIME "+report.ok);enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
