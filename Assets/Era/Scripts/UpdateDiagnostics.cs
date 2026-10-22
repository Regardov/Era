using System;
using System.IO;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(50)]
    public sealed class UpdateDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report{public bool ok,waves,statue,elites,freeChest,resources,slide,music,humans,art,walls;public int chests,nodes,guards,wallParts;public float wallDistance;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;string folder;float start;int phase;Vector3 slideOrigin;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            start=Time.realtimeSinceStartup;I18n.Language=GameLanguage.Russian;App.SelectedHero=Hero.Lumen;App.BeginSelection(SessionMode.Solo);
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name.Replace("v010","v011")));
        void SelectEcho()=>App.SelectedHero=Hero.Echo;
        void SelectEmber()=>App.SelectedHero=Hero.Ember;
        void Update()
        {
            float age=Time.realtimeSinceStartup-start;
            if(App.State!=null)foreach(var player in App.State.players){player.invulnerability=999;player.weaponTimers[0]=999;for(int i=0;player.pendingLevels>0&&i<16;i++)App.Simulation.ChooseItem(player,0);}
            if(phase==0&&age>2){Shot("hero-lumen-v010.png");Invoke(nameof(SelectEcho),.25f);phase++;}
            else if(phase==1&&age>3.5f){Shot("hero-echo-v010.png");Invoke(nameof(SelectEmber),.25f);phase++;}
            else if(phase==2&&age>5){Shot("hero-ember-v010.png");phase++;}
            else if(phase==3&&age>6.5f){App.Page=MenuPage.Settings;Shot("settings-v010.png");phase++;}
            else if(phase==4&&age>8)
            {
                App.Solo();App.State.multiplayer=true;App.Simulation.AddPlayer(1,"Эхо",Hero.Echo);App.Simulation.AddPlayer(2,"Люмен",Hero.Lumen);
                foreach(var player in App.State.players){player.weapons=new[]{-1,-1};player.invulnerability=999;}
                App.LocalPlayer.weapons=new[]{(int)WeaponKind.Bow,-1};App.LocalPlayer.weaponTimers[0]=999;
                report.chests=App.State.chests.Count;report.nodes=App.State.resourceNodes.Count;App.State.spawnTimer=0;
                App.State.resourceNodes[0].position=WorldMap.OnGround(0,5);App.State.resourceNodes[1].position=WorldMap.OnGround(3,5);
                App.State.eliteShrines[0].position=WorldMap.OnGround(10,6);App.LocalPlayer.position=WorldMap.OnGround(-1,0);phase++;
            }
            else if(phase==5&&age>10.5f)
            {
                report.waves=App.State.waveSpawned>0&&App.State.wave==1&&CampaignData.Remaining(App.State.stageElapsed)>590&&!App.State.bossSpawned;
                report.music=SynthAudio.Instance.BattlePlaying;report.humans=true;foreach(var hero in FindObjectsByType<HeroMotion>(FindObjectsSortMode.None))if(hero.skeletal!=null)report.humans&=hero.GetComponent<HumanRigShape>()!=null;
                Shot("waves-and-gpus-v010.png");App.State.stageElapsed=59.9f;App.State.enemies.Clear();phase++;
            }
            else if(phase==6&&age>12){report.waves&=App.State.wave==2&&App.State.waveSpawned>0;Shot("minute-horde-v011.png");App.State.spawnTimer=1000;App.State.enemies.Clear();App.LocalPlayer.position=App.State.resourceNodes[0].position;App.LocalInput.interact=true;phase++;}
            else if(phase==7&&age>13){report.resources=App.State.resourceNodes[0].broken;App.LocalPlayer.position=App.State.resourceNodes[1].position;App.LocalInput.interact=true;phase++;}
            else if(phase==8&&age>14.5f){report.resources&=App.State.resourceNodes[1].broken;Shot("gpu-drops-v010.png");App.LocalPlayer.position=App.State.eliteShrines[0].position-Vector3.forward*3;App.LocalInput.interact=true;phase++;}
            else if(phase==9&&age>16)
            {
                int id=App.State.eliteShrines[0].id;report.guards=App.State.enemies.FindAll(e=>e.eliteShrine==id).Count;report.elites=report.guards==5;foreach(var e in App.State.enemies)e.frozen=999;Shot("elite-shrine-v010.png");phase++;
            }
            else if(phase==10&&age>18){foreach(var enemy in App.State.enemies.ToArray())App.Simulation.DamageEnemy(enemy,App.LocalPlayer,999999);phase++;}
            else if(phase==11&&age>19.5f)
            {
                var chest=App.State.chests.Find(c=>c.free);report.freeChest=chest!=null&&chest.cost==0&&App.State.eliteShrines[0].completed;
                if(chest!=null){App.State.Player(1).position=chest.position;int gold=App.State.coins;report.freeChest&=App.Simulation.TryOpenChest(App.State.Player(1))&&App.State.coins==gold;}phase++;
            }
            else if(phase==12&&age>21){report.art=IconLibrary.ItemAtlas!=null&&App.State.loot.Count>0;Shot("free-chest-art-v010.png");phase++;}
            else if(phase==13&&age>23){App.LocalPlayer.dodge=0;App.LocalPlayer.invulnerability=0;slideOrigin=App.LocalPlayer.position;App.Simulation.DamagePlayer(App.LocalPlayer,8,App.LocalPlayer.position+Vector3.forward);phase++;}
            else if(phase==14&&age>23.1f){Shot("short-slide-v010.png");phase++;}
            else if(phase==15&&age>24){float distance=Vector3.Distance(slideOrigin,App.LocalPlayer.position);report.slide=distance>.2f&&distance<.6f;App.LocalPlayer.position=App.State.bossStatuePosition-Vector3.forward*3;App.LocalInput.interact=true;phase++;}
            else if(phase==16&&age>26){report.statue=App.State.bossSpawned&&App.State.enemies.FindAll(e=>e.kind==EnemyKind.Warden).Count==1;Shot("stone-boss-statue-v010.png");phase++;}
            else if(phase==17&&age>27.5f){App.LocalPlayer.position=WorldMap.OnGround(0,294);App.LocalInput.z=1;phase++;}
            else if(phase==18&&age>29){App.LocalInput.z=0;report.wallDistance=new Vector2(App.LocalPlayer.position.x,App.LocalPlayer.position.z).magnitude;report.wallParts=Array.FindAll(FindAnyObjectByType<WorldEnvironment>().GetComponentsInChildren<Renderer>(),r=>r.name=="Stone perimeter / combined blocks").Length;report.walls=report.wallDistance<294&&report.wallParts==2;Shot("perimeter-wall-v010.png");phase++;}
            else if(phase==19&&age>30){App.ReturnToMenu();App.Page=MenuPage.Settings;Shot("settings-final-v010.png");phase++;}
            else if(phase==20&&age>31.5f)Finish();
            if(age>80)Finish();
        }
        void LateUpdate()
        {
            if(App.State==null||phase<5)return;Vector3 target=phase>=18?WorldMap.OnGround(0,294)+Vector3.up*2:phase<9?new Vector3(1.5f,.7f,4):phase<16?App.State.eliteShrines[0].position+Vector3.up:App.State.bossStatuePosition+Vector3.up*2;
            App.View.Camera.transform.position=target+new Vector3(11,7,-16);App.View.Camera.transform.LookAt(target);App.View.Camera.fieldOfView=48;
        }
        void Finish()
        {
            report.ok=phase>=20&&report.waves&&report.statue&&report.elites&&report.freeChest&&report.resources&&report.slide&&report.music&&report.humans&&report.art&&report.walls&&report.chests==60&&report.nodes==72;
            File.WriteAllText(Path.Combine(folder,"smoke-update.json"),JsonUtility.ToJson(report,true));Application.Quit(report.ok?0:2);enabled=false;
        }
    }
}
