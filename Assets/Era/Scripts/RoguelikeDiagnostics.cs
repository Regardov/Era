using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class RoguelikeDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Report{public bool ok,catalogue,models,tooltips,combat;public int items,creatures;public float fps;}
        GameApp App=>GameApp.Instance;string folder;float started;int phase,frames;bool lineup;Report report=new Report();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);started=Time.realtimeSinceStartup;App.SelectedHero=Hero.Lumen;App.PlayerName="Хранитель";App.SetLanguage(GameLanguage.Russian);
        }
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;frames++;
            if(App.UpgradeSelection)App.Choose(0);
            if(phase==0&&age>1.5f)
            {
                App.Solo();var p=App.LocalPlayer;p.weapons=new[]{-1,-1};p.invulnerability=1000;App.State.spawnTimer=10000;App.State.resourceNodes.Clear();p.position=WorldMap.OnGround(0,-8);
                int n=0;foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))if(kind>=EnemyKind.Skeleton)
                {var e=App.Simulation.SpawnEnemy(kind);e.position=WorldMap.OnGround(-9+n%6*3.6f,2+n/6*5);if(EnemyCatalog.Flying(kind))e.position.y+=1.8f;e.hp=e.maxHp=100000;e.frozen=1000;n++;}
                report.creatures=n;report.items=GameData.Items.Length;report.models=n==12;report.catalogue=report.items==130;lineup=true;phase++;
            }
            else if(phase==1&&age>3.5f){App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,"roguelike-creatures-v012.png"));phase++;}
            else if(phase==2&&age>4.5f){lineup=false;App.GetComponent<GameHud>().CaptureItemCatalogue(Path.Combine(folder,"roguelike-catalogue-v012.png"),0);phase++;}
            else if(phase==3&&age>5.5f){App.GetComponent<GameHud>().CaptureItemCatalogue(Path.Combine(folder,"roguelike-tooltip-v012.png"),3,(int)ItemId.PacifistEngine);phase++;}
            else if(phase==4&&age>6.5f)
            {
                App.Inventory=false;App.State.enemies.Clear();var p=App.LocalPlayer;p.weapons=new[]{(int)WeaponKind.LightningStaff,(int)WeaponKind.Sword};p.weaponLevels=new[]{1,1};p.hero=Hero.Echo;
                foreach(var item in new[]{ItemId.DoubleShot,ItemId.DoubleShot,ItemId.FlameBlade,ItemId.IceCore,ItemId.SteamValve,ItemId.VenomNeedle,ItemId.TimeMaster})App.Simulation.ApplyItem(p,(int)item);
                for(int n=0;n<10;n++){var e=App.Simulation.SpawnEnemy(n%2==0?EnemyKind.Ogre:EnemyKind.SkeletonArcher);e.position=WorldMap.OnGround(-6+n%5*3,0+n/5*5);e.hp=e.maxHp=5000;e.spawnGrace=0;}
                App.CameraYaw=0;App.CameraPitch=25;phase++;
            }
            else if(phase==5&&age>9.5f){report.combat=App.State.hits.Count>0||App.State.effects.Count>0;App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,"roguelike-combat-v012.png"));phase++;}
            else if(phase==6&&age>10.5f)
            {
                report.tooltips=File.Exists(Path.Combine(folder,"roguelike-tooltip-v012.png"));report.fps=frames/age;report.ok=report.catalogue&&report.models&&report.tooltips&&report.combat;
                File.WriteAllText(Path.Combine(folder,"smoke-roguelike.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ROGUELIKE "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);phase++;
            }
            if(age>25&&phase<7)Application.Quit(3);
        }
        void LateUpdate()
        {
            if(!lineup)return;var camera=App.View.Camera;camera.transform.position=WorldMap.OnGround(17,-23)+Vector3.up*12;camera.transform.LookAt(WorldMap.OnGround(0,5)+Vector3.up);camera.fieldOfView=51;
        }
    }
}
