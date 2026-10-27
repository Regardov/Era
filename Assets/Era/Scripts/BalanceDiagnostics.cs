using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class BalanceDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Report{public bool ok,menuCatalogue,stand,crouch,shield,evade,tooltip,bossHealth;public int chests,activeItems,rareItems;public string version;}
        GameApp App=>GameApp.Instance;string folder;float started;int phase;bool portraits;Vector3 origin;Report report=new Report();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);started=Time.realtimeSinceStartup;App.SetLanguage(GameLanguage.Russian);App.PlayerName="Странник";
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;if(App.UpgradeSelection)App.Choose(0);
            if(phase==0&&age>1.5f){App.Page=MenuPage.Catalogue;Shot("balance-menu-catalogue-v014.png");phase++;}
            else if(phase==1&&age>2.5f)
            {
                report.version=Application.version;
                for(int i=0;i<GameData.Items.Length;i++)if(ItemCatalog.IsAvailable(i)){report.activeItems++;if(GameData.Items[i].rarity==ItemRarity.Rare)report.rareItems++;}
                report.menuCatalogue=App.Page==MenuPage.Catalogue&&report.activeItems==ItemCatalog.ActiveCount&&report.rareItems==10&&File.Exists(Path.Combine(folder,"balance-menu-catalogue-v014.png"));
                App.GetComponent<GameHud>().CaptureItemCatalogue(Path.Combine(folder,"balance-item-tooltip-v014.png"),1,(int)ItemId.RicochetSeed);phase++;
            }
            else if(phase==2&&age>3.5f)
            {
                App.Inventory=false;App.SelectedHero=Hero.Ember;App.Solo();origin=WorldMap.OnGround(0,-15);App.Simulation.AddPlayer(1,"Эхо",Hero.Echo);App.Simulation.AddPlayer(2,"Люмен",Hero.Lumen);
                foreach(var p in App.State.players){p.weapons=new[]{-1,-1};p.invulnerability=1000;p.yaw=0;}
                App.State.spawnTimer=10000;App.State.resourceNodes.Clear();App.State.enemies.Clear();App.LocalPlayer.shield=40;App.LocalPlayer.dodge=.36f;App.LocalPlayer.lifeSteal=1.5f;report.chests=App.State.chests.Count;portraits=true;phase++;
            }
            else if(phase==3&&age>5){report.shield=App.LocalPlayer.shield>38&&App.LocalPlayer.shield<=40;Shot("balance-models-standing-v014.png");report.stand=true;phase++;}
            else if(phase==4&&age>5.5f){App.LocalInput.crouch=true;phase++;}
            else if(phase==5&&age>6.5f){report.crouch=App.LocalPlayer.crouching;Shot("balance-models-crouching-v014.png");phase++;}
            else if(phase==6&&age>7.5f)
            {
                App.LocalInput.crouch=false;App.LocalPlayer.invulnerability=0;App.LocalPlayer.dodge=.95f;int attempts=0;
                do{App.LocalPlayer.invulnerability=0;App.Simulation.DamagePlayer(App.LocalPlayer,1);attempts++;}while(!App.State.hits.Exists(h=>h.evaded)&&attempts<100);
                report.evade=App.State.hits.Exists(h=>h.evaded);Shot("balance-shield-evade-v014.png");phase++;
            }
            else if(phase==7&&age>8.5f){App.GetComponent<GameHud>().CaptureAttribute(Path.Combine(folder,"balance-stat-tooltip-v014.png"),11);phase++;}
            else if(phase==8&&age>9.5f)
            {
                report.tooltip=File.Exists(Path.Combine(folder,"balance-stat-tooltip-v014.png"));App.Inventory=false;App.LocalPlayer.invulnerability=1000;
                var boss=App.Simulation.SpawnEnemy(EnemyKind.Warden);boss.position=origin+Vector3.forward*7;boss.spawnGrace=1000;boss.hp=boss.maxHp*.7f;
                Shot("balance-boss-health-v014.png");report.bossHealth=boss.hp>0&&boss.hp<boss.maxHp;phase++;
            }
            else if(phase==9&&age>10.5f)
            {
                report.ok=report.menuCatalogue&&report.stand&&report.crouch&&report.shield&&report.evade&&report.tooltip&&report.bossHealth&&report.chests==GameData.ChestCount;
                File.WriteAllText(Path.Combine(folder,"smoke-balance.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_BALANCE "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);phase++;
            }
            if(portraits)foreach(var p in App.State.players){p.position=origin+Vector3.right*((p.id-1)*1.8f);p.position.y=WorldMap.Ground(p.position);if(p.id!=App.LocalId)p.crouching=App.LocalInput.crouch;}
            if(age>25&&phase<10)Application.Quit(3);
        }
        void LateUpdate()
        {
            if(!portraits)return;var camera=App.View.Camera;camera.transform.position=origin+new Vector3(.3f,1.8f,5.7f);camera.transform.LookAt(origin+Vector3.up*1.15f);camera.fieldOfView=54;
        }
    }
}
