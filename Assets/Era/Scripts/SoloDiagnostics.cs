using System;
using System.IO;
using UnityEngine;
namespace Era
{
    // Explicit opt-in player diagnostics; no test grants or teleports in regular sessions.
    public sealed class SoloDiagnostics:MonoBehaviour
    {
        [Serializable]class Report{public bool ok,paused,resumed,bilingual,animatedLoot,fourWeapons;public int weaponCount;}
        GameApp App=>GameApp.Instance;
        string folder;float start,pausedTime,pauseStarted;Vector3 pausedPosition;int stage,opened;float openedAt;Report report=new Report();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));int at=Array.IndexOf(args,"--capture-folder");if(at>=0&&at+1<args.Length)folder=args[at+1];Directory.CreateDirectory(folder);
            start=Time.realtimeSinceStartup;I18n.Language=GameLanguage.English;App.PlayerName="Rowan";App.SelectedHero=Hero.Ember;
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Update()
        {
            float age=Time.realtimeSinceStartup-start;
            if(stage==0&&age>2){Shot("menu-en.png");stage++;}
            else if(stage==1&&age>3){App.SetLanguage(GameLanguage.Russian);Shot("menu-ru.png");stage++;}
            else if(stage==2&&age>4){App.BeginSelection(SessionMode.Solo);Shot("character-ru.png");stage++;}
            else if(stage==3&&age>5)
            {
                App.Solo();App.Simulation.State.spawnTimer=1000;App.Simulation.State.coins=1000;
                App.Simulation.GrantExperience(12);pausedTime=App.State.elapsed;pausedPosition=App.LocalPlayer.position;pauseStarted=Time.realtimeSinceStartup;stage++;
            }
            else if(stage==4)
            {
                App.LocalInput.x=1;
                if(Time.realtimeSinceStartup-pauseStarted>1){Shot("solo-upgrade-ru.png");stage++;}
            }
            else if(stage==5&&Time.realtimeSinceStartup-pauseStarted>2)
            {report.paused=App.WorldPaused&&App.Simulation.UpgradePaused&&App.State.elapsed==pausedTime&&App.LocalPlayer.position==pausedPosition;App.SetLanguage(GameLanguage.English);report.bilingual=GameData.Weapons[0].name=="Crescent Axe";Shot("solo-upgrade-en.png");stage++;}
            else if(stage==6&&Time.realtimeSinceStartup-pauseStarted>3){App.Choose(0);stage++;}
            else if(stage==7&&App.State.elapsed>pausedTime+.3f)
            {report.resumed=!App.WorldPaused&&!App.Simulation.UpgradePaused;App.LocalInput.x=0;OpenNext();stage++;}
            else if(stage==8)
            {
                if(App.UpgradeSelection)App.Choose(0);
                var drop=App.State.loot.Count>0?App.State.loot[0]:null;
                if(drop!=null&&drop.age>.35f&&drop.age<.6f){report.animatedLoot|=drop.position.y>drop.origin.y+1;Shot("chest-loot.png");}
                if(App.State.elapsed-openedAt>1.9f)
                {
                    opened++;if(opened<3)OpenNext();else
                    {
                        var p=App.LocalPlayer;p.position=WorldMap.OnGround(-59,37);App.CameraYaw=205;App.CameraPitch=18;
                        report.weaponCount=Array.FindAll(p.weapons,w=>w>=0).Length;report.fourWeapons=report.weaponCount==4;
                        App.Simulation.SpawnEnemy(EnemyKind.Brute).position=p.position+Vector3.forward*5;App.Simulation.SpawnEnemy(EnemyKind.Stalker).position=p.position+new Vector3(3,0,6);stage++;openedAt=age;
                    }
                }
            }
            else if(stage==9&&age-openedAt>1)
            {App.SetLanguage(GameLanguage.Russian);Shot("gameplay-v03-ru.png");stage++;openedAt=age;}
            else if(stage==10&&age-openedAt>1)
            {App.SetLanguage(GameLanguage.English);App.Inventory=true;Shot("inventory-v03-en.png");stage++;openedAt=age;}
            else if(stage==11&&age-openedAt>1)
            {
                report.ok=report.paused&&report.resumed&&report.bilingual&&report.animatedLoot&&report.fourWeapons;
                File.WriteAllText(Path.Combine(folder,"smoke-solo.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_SOLO "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);stage++;
            }
            if(age>35&&stage<12){File.WriteAllText(Path.Combine(folder,"smoke-solo.json"),JsonUtility.ToJson(report,true));Application.Quit(3);}
        }
        void OpenNext()
        {
            var chest=App.State.chests[opened];App.LocalPlayer.position=chest.position;App.CameraYaw=30;App.CameraPitch=25;App.Simulation.TryOpenChest(App.LocalPlayer);openedAt=App.State.elapsed;
        }
    }
}
