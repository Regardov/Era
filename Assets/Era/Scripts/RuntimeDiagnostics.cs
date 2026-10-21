using System;
using System.IO;
using UnityEngine;

namespace Era
{
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        string[] args;string folder;float start;bool smoke,client,readyPressed,readySeen,jumpIssued,jumpSeen,castIssued,castsSeen,xpGranted,reported,lobbyShot,gameShot,jumpShot;
        bool homeShot,characterShot,tourStarted,groveShot,upgradeShot,levelKeepsMoving,crouchSeen,channelSeen;float pendingAt=-1;int exit;
        GameApp App=>GameApp.Instance;
        [Serializable]class Report {public bool ok,connected,ready,jump,actions,music,noLevelPause,crouch,blessing;public int players,level,kills,items,chests,gold,weapons,hostWeapons;}
        void Start()
        {
            args=Environment.GetCommandLineArgs();start=Time.realtimeSinceStartup;folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));
            int at=Array.IndexOf(args,"--capture-folder");if(at>=0&&at+1<args.Length)folder=args[at+1];Directory.CreateDirectory(folder);
            smoke=Has("--smoke-host")||Has("--smoke-client");client=Has("--smoke-client");
            if(smoke){I18n.Language=client?GameLanguage.Russian:GameLanguage.English;App.PlayerName=client?"Willow":"Rowan";App.SelectedHero=client?Hero.Echo:Hero.Ember;App.SelectedRelic=Relic.StormCompass;if(client)App.Join();else App.Host();}
            else if(Has("--capture-character")){App.BeginSelection(SessionMode.Solo);App.SelectedHero=Hero.Echo;}
            else if(Has("--capture-network"))App.Page=MenuPage.Connection;
            else if(Has("--capture-host-menu"))App.Page=MenuPage.HostLobby;
            else if(Has("--capture-join-menu"))App.Page=MenuPage.JoinLobby;
        }
        bool Has(string value)=>Array.IndexOf(args,value)>=0;
        void Shot(string file){if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,file));}
        void Update()
        {
            float age=Time.realtimeSinceStartup-start;
            if(!smoke)
            {
                if(age>3&&!homeShot)
                {
                    string file=Has("--capture-character")?"character-select.png":Has("--capture-network")?"network-menu.png":Has("--capture-host-menu")?"host-menu.png":Has("--capture-join-menu")?"join-menu.png":"main-menu.png";
                    Shot(file);homeShot=true;
                }
                if(Has("--capture-tour"))
                {
                    if(age>4&&!characterShot){App.BeginSelection(SessionMode.Solo);App.SelectedHero=Hero.Echo;characterShot=true;}
                    if(age>7&&!tourStarted){Shot("character-select.png");tourStarted=true;}
                    if(age>9&&App.State==null)App.Solo();
                    if(age>11&&!groveShot){var p=App.LocalPlayer;p.position=WorldMap.OnGround(-59,37);App.CameraYaw=205;App.CameraPitch=18;App.Simulation.SpawnEnemy(EnemyKind.Brute).position=p.position+Vector3.forward*6;groveShot=true;}
                    if(age>14&&!gameShot){Shot("lavender-grove.png");gameShot=true;}
                    if(age>17)Application.Quit();
                }
                else if(age>6)Application.Quit();
                return;
            }
            var s=App.State;var me=App.LocalPlayer;var input=App.LocalInput;
            if(s!=null&&s.players.Count==2)
            {
                if(s.players.TrueForAll(p=>p.ready))readySeen=true;
                if(!lobbyShot&&age>4){Shot(client?"lobby-client.png":"lobby-host.png");lobbyShot=true;}
                if(s.phase==RunPhase.Lobby&&!readyPressed&&age>6){App.ToggleReady();readyPressed=true;}
            }
            if(s?.phase==RunPhase.Playing&&me!=null)
            {
                if(me.qCooldown>0&&me.eCooldown>0&&me.relicCooldown>0)castsSeen=true;
                if(!me.grounded&&me.position.y-WorldMap.Ground(me.position)>0.4f)jumpSeen=true;
                if(me.crouching)crouchSeen=true;if(s.Player(0)?.chargingShrine>=0)channelSeen=true;
                input.crouch=s.elapsed>6&&s.elapsed<7;input.revive=false;
                Vector3 move;
                if(!client&&s.openedChests==0)
                {var chest=s.chests[0];move=(chest.position-me.position).normalized;if(Vector3.Distance(chest.position,me.position)<3)input.interact=true;}
                else if(!client&&s.elapsed>11&&!s.shrines[0].spent)
                {
                    me.invulnerability=999;var shrine=s.shrines[0];move=(shrine.position-me.position).normalized;
                    if(Vector3.Distance(shrine.position,me.position)<2.4f){move=Vector3.zero;input.revive=true;}
                }
                else
                {
                    move=new Vector3(Mathf.Sin(s.elapsed*.5f+(client?2:0)),0,Mathf.Cos(s.elapsed*.5f+(client?2:0)))*.7f;
                    var gold=s.pickups.Find(d=>d.kind==PickupKind.Gold&&Vector3.Distance(d.position,me.position)<10);
                    if(gold!=null)move=(gold.position-me.position).normalized;
                }
                input.x=move.x;input.z=move.z;input.yaw=30;
                if(s.elapsed>2&&!jumpIssued){input.jump=true;jumpIssued=true;}
                input.jumpHeld=s.elapsed<2.5f;
                if(jumpSeen&&!jumpShot){Shot(client?"jump-client.png":"jump-host.png");jumpShot=true;}
                if(s.elapsed>5&&!castIssued){input.primary=input.secondary=input.relic=input.dash=true;castIssued=true;}
                if(!client&&s.elapsed>8&&!xpGranted){App.Simulation.GrantExperience(15);xpGranted=true;}
                if(me.pendingLevels>0)
                {
                    if(pendingAt<0)pendingAt=s.elapsed;
                    if(s.elapsed-pendingAt>.5f&&!upgradeShot){Shot(client?"coop-upgrade-ru.png":"coop-upgrade-en.png");upgradeShot=true;}
                    if(s.elapsed-pendingAt>1){levelKeepsMoving=true;input.choice=0;}
                }
                if(s.elapsed>11&&!gameShot){Shot(client?"coop-client.png":"coop-host.png");gameShot=true;}
            }
            if(age>32&&!reported)
            {
                int items=0;if(me!=null)foreach(int count in me.items)items+=count;
                var report=new Report{connected=s?.players.Count==2,ready=readySeen,jump=jumpSeen,actions=castsSeen,music=SynthAudio.Instance!=null&&SynthAudio.Instance.MusicReady,
                    noLevelPause=levelKeepsMoving,crouch=crouchSeen,blessing=channelSeen&&s?.shrines[0].spent==true&&s.Player(0).blessings[s.shrines[0].buff]>0,players=s?.players.Count??0,level=s?.level??0,kills=s?.kills??0,items=items,chests=s?.openedChests??0,gold=s?.earnedCoins??0,weapons=me==null?0:Array.FindAll(me.weapons,w=>w>=0).Length,hostWeapons=s?.Player(0)==null?0:Array.FindAll(s.Player(0).weapons,w=>w>=0).Length};
                report.ok=report.connected&&report.ready&&report.jump&&report.actions&&report.music&&report.noLevelPause&&report.crouch&&report.blessing&&report.level>=2&&items>0&&report.chests>0&&report.gold>0&&report.hostWeapons>=2&&s.phase==RunPhase.Playing;
                string json=JsonUtility.ToJson(report);File.WriteAllText(Path.Combine(folder,client?"smoke-client.json":"smoke-host.json"),json);Debug.Log("ERA_SMOKE "+json);exit=report.ok?0:2;reported=true;
            }
            if(age>(client?34:40))Application.Quit(exit);
        }
    }
}
