using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class MeadowDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report {public bool ok,icons,streams,shrubs,oldTrees,grass,bossHud,aftermath,growth,reset;public int suppliedIcons,renderers;public float partyScale;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;
        string folder;float started;int phase;Vector3 target;EnemyState boss;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            started=Time.realtimeSinceStartup;I18n.Language=GameLanguage.Russian;App.SelectedHero=Hero.Ember;App.Solo();
            App.State.multiplayer=true;App.Simulation.AddPlayer(1,"Эхо",Hero.Echo);App.Simulation.AddPlayer(2,"Люмен",Hero.Lumen);
            foreach(var p in App.State.players){p.weapons=new[]{-1,-1};p.invulnerability=1000;}
            App.State.spawnTimer=1000;App.State.resourceNodes.Clear();
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name+"-v015.png"));
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;
            foreach(var p in App.State.players){p.invulnerability=1000;for(int i=0;p.pendingLevels>0&&i<32;i++)App.Simulation.ChooseItem(p,0);}
            if(phase<4&&age>1.5f+phase)
            {App.GetComponent<GameHud>().CaptureItemCatalogue(Path.Combine(folder,"new-icons-"+(phase+1)+"-v015.png"),phase);phase++;}
            else if(phase==4&&age>5.5f)
            {
                App.Inventory=false;float z=-28,x=WorldMap.RiverCenter(z,0);target=WorldMap.OnGround(x,z)+Vector3.up*.8f;
                foreach(var p in App.State.players)p.position=WorldMap.OnGround(x-6+p.id*2,z-4);
                var environment=FindAnyObjectByType<WorldEnvironment>();report.renderers=environment.GetComponentsInChildren<Renderer>().Length;
                report.streams=Array.Find(environment.GetComponentsInChildren<Transform>(),t=>t.name=="04 / Flowing streams")?.childCount==32;
                report.shrubs=WorldMap.Props.Exists(p=>p.kind==2);report.oldTrees=WorldMap.Props.Exists(p=>p.kind==0&&p.size>2.4f);
                report.grass=environment.GetComponentInChildren<LivingGrass>()?.patches.Length>70;report.icons=true;
                for(int i=0;i<ItemCatalog.Count;i++){report.icons&=IconLibrary.ItemSprite(i)!=null;if(IconLibrary.HasSuppliedItemArt(i))report.suppliedIcons++;}
                report.partyScale=App.State.enemyPartyScale;phase++;
            }
            else if(phase==5&&age>7){Shot("living-meadow");phase++;}
            else if(phase==6&&age>8)
            {
                boss=App.Simulation.SpawnEnemy(EnemyKind.Warden);boss.position=WorldMap.OnGround(target.x,target.z+8);boss.spawnGrace=1000;boss.hp=boss.maxHp*.7f;
                report.bossHud=boss.hp>0&&boss.hp<boss.maxHp;Shot("raised-boss-hud");phase++;
            }
            else if(phase==7&&age>9)
            {
                App.Simulation.DamageEnemy(boss,App.LocalPlayer,1000000);phase++;
            }
            else if(phase==8&&age>10)
            {
                report.aftermath=App.State.postBossActive&&App.State.portal.active&&App.State.enemies.Count>0&&Mathf.Abs(App.Simulation.SpawnInterval-1.1f/8)<.0001f;
                var enemy=App.State.enemies[0];enemy.position=WorldMap.OnGround(target.x,target.z+6);enemy.spawnGrace=1000;
                float hp=enemy.maxHp,damage=enemy.damage;App.State.spawnTimer=1000;
                while(App.State.postBossGrowthSteps<2)App.Simulation.Step(.05f,new System.Collections.Generic.Dictionary<int,InputFrame>());
                report.growth=Mathf.Abs(enemy.maxHp-hp*1.21f)<.01f&&Mathf.Abs(enemy.damage-damage*1.21f)<.01f;
                Shot("boss-aftermath");phase++;
            }
            else if(phase==9&&age>11)
            {
                App.LocalPlayer.position=App.State.portal.position;report.reset=App.Simulation.TryUsePortal(App.LocalPlayer)&&!App.State.postBossActive&&App.State.postBossGrowthSteps==0;
                phase++;
            }
            else if(phase==10&&age>13)
            {
                report.ok=report.icons&&report.suppliedIcons==120&&report.streams&&report.shrubs&&report.oldTrees&&report.grass&&report.bossHud&&report.aftermath&&report.growth&&report.reset&&report.renderers<800&&report.partyScale==4;
                File.WriteAllText(Path.Combine(folder,"smoke-meadow.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_MEADOW "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
            }
            if(age>35)Application.Quit(3);
        }
        void LateUpdate()
        {
            if(phase<5)return;var camera=App.View.Camera;
            camera.transform.position=target+new Vector3(10,5.5f,-12);camera.transform.LookAt(target);camera.fieldOfView=57;
        }
    }
}
