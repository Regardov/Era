using System;
using System.IO;
using UnityEngine;
namespace Era
{
    public sealed class ArtDiagnostics:MonoBehaviour
    {
        [Serializable]class Report{public bool ok,models,channel,randomBuff,crouch,extraJump,descriptions,blueCrystals;public int buff;}
        GameApp App=>GameApp.Instance;string folder;float start;int stage,startJump;Report report=new Report();
        void Start(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);start=Time.realtimeSinceStartup;App.SelectedHero=Hero.Echo;App.PlayerName="Wanderer";I18n.Language=GameLanguage.English;}
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Update()
        {
            float t=Time.realtimeSinceStartup-start;var p=App.LocalPlayer;var input=App.LocalInput;if(App.UpgradeSelection)App.Choose(0);
            if(stage==0&&t>2){Shot("menu-v04-en.png");stage++;}
            else if(stage==1&&t>3){App.SetLanguage(GameLanguage.Russian);Shot("menu-v04-ru.png");stage++;}
            else if(stage==2&&t>4){App.BeginSelection(SessionMode.Solo);Shot("mage-v04.png");stage++;}
            else if(stage==3&&t>5)
            {
                App.Solo();App.State.spawnTimer=1000;App.State.coins=1000;p=App.LocalPlayer;p.invulnerability=999;p.position=App.State.shrines[0].position+Vector3.forward*1.8f;App.CameraYaw=155;App.CameraPitch=19;input=App.LocalInput;input.revive=true;
                report.models=App.View!=null&&Resources.Load<GameObject>("Characters/Echo").GetComponent<SkeletalActor>()!=null;stage++;
            }
            else if(stage==4&&t>8){Shot("shrine-channel-v04.png");report.channel=App.State.shrines[0].charge>2&&!App.State.shrines[0].spent;stage++;}
            else if(stage==5&&t>10.5f)
            {
                var s=App.State.shrines[0];report.buff=s.buff;report.randomBuff=s.spent&&s.buff>=0&&p.blessings[s.buff]>0;input.revive=false;Shot("shrine-blessing-v04.png");if(p.extraJumps==0)App.Simulation.ApplyBlessing(p,0);startJump=p.jumpSerial;input.jump=input.jumpHeld=true;stage++;
            }
            else if(stage==6&&t>10.9f){input.jump=true;stage++;}
            else if(stage==7&&t>11.2f){report.extraJump=p.jumpSerial>=startJump+2&&p.airJumpsUsed>0;Shot("double-jump-v04.png");input.jumpHeld=false;stage++;}
            else if(stage==8&&t>13){input.crouch=true;input.x=.5f;stage++;}
            else if(stage==9&&t>14.3f){report.crouch=p.crouching&&p.velocity.magnitude<4;Shot("crouch-v04.png");stage++;}
            else if(stage==10&&t>16)
            {input.crouch=false;input.x=0;p.position=App.State.chests[0].position;App.CameraYaw=35;App.CameraPitch=23;App.Simulation.TryOpenChest(p);stage++;}
            else if(stage==11&&t>16.5f){Shot("weapon-drop-v04.png");stage++;}
            else if(stage==12&&t>18.5f)
            {
                report.descriptions=App.State.rewards.Exists(n=>n.kind==RewardKind.Weapon&&n.owner==p.id);Shot("weapon-description-v04.png");stage++;
            }
            else if(stage==13&&t>20)
            {
                p.position=WorldMap.OnGround(0,-10);
                for(int i=0;i<4;i++){var e=App.Simulation.SpawnEnemy((EnemyKind)i);e.position=p.position+new Vector3((i-1.5f)*3,0,8+i);e.position.y=WorldMap.Ground(e.position);e.hp=e.maxHp=10000;e.frozen=2;}
                for(int i=0;i<4;i++)App.Simulation.GiveWeapon(p,(WeaponKind)i);App.CameraYaw=0;App.CameraPitch=23;
                float crystalDistance=p.pickupRange+4;
                for(int i=0;i<6;i++)App.State.pickups.Add(new PickupState{id=App.State.nextId++,kind=PickupKind.Experience,position=WorldMap.OnGround(p.position.x-3+i,p.position.z+crystalDistance),value=2});
                stage++;
            }
            else if(stage==14&&t>21){report.blueCrystals=App.State.pickups.Exists(c=>c.kind==PickupKind.Experience);Shot("combat-v04-ru.png");stage++;}
            else if(stage==15&&t>23){App.SetLanguage(GameLanguage.English);Shot("combat-v04-en.png");stage++;}
            else if(stage==16&&t>24){App.Inventory=true;Shot("gear-v04.png");stage++;}
            else if(stage==17&&t>25)
            {report.ok=report.models&&report.channel&&report.randomBuff&&report.crouch&&report.extraJump&&report.descriptions&&report.blueCrystals;string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(folder,"smoke-art.json"),json);Debug.Log("ERA_ART "+json);Application.Quit(report.ok?0:2);stage++;}
            if(t>35&&stage<18){File.WriteAllText(Path.Combine(folder,"smoke-art.json"),JsonUtility.ToJson(report,true));Application.Quit(3);}
        }
    }
}
