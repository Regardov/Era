using System;
using System.IO;
using UnityEngine;

namespace Era
{
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        string[] args;string folder;float start;bool smoke,client,readyPressed,readySeen,jumpIssued,jumpSeen,castIssued,castsSeen,xpGranted,reported,lobbyShot,gameShot,jumpShot;
        bool homeShot,characterShot,tourStarted,groveShot,upgradeShot,levelKeepsMoving,crouchSeen,channelSeen,third,milestoneGranted,fixtureReady;float pendingAt=-1;int exit,chosenOffer=-1;
        GameApp App=>GameApp.Instance;
        [Serializable]class Report {public bool ok,connected,ready,jump,actions,music,noLevelPause,crouch,blessing,reward,auras,secondWeapons,difficulty;public int players,localId,level,kills,items,chests,gold,weapons,hostWeapons;}
        void Start()
        {
            args=Environment.GetCommandLineArgs();start=Time.realtimeSinceStartup;folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));
            int at=Array.IndexOf(args,"--capture-folder");if(at>=0&&at+1<args.Length)folder=args[at+1];Directory.CreateDirectory(folder);
            third=Has("--smoke-client2");smoke=Has("--smoke-host")||Has("--smoke-client")||third;client=Has("--smoke-client")||third;
            if(smoke){I18n.Language=client?GameLanguage.Russian:GameLanguage.English;App.Address="127.0.0.1";App.PlayerName=third?"Lumen":client?"Willow":"Rowan";App.SelectedHero=third?Hero.Lumen:client?Hero.Echo:Hero.Ember;App.SelectedRelic=Relic.StormCompass;if(client)App.Join();else App.Host();}
            else if(Has("--capture-lumen")){App.BeginSelection(SessionMode.Solo);App.SelectedHero=Hero.Lumen;I18n.Language=GameLanguage.Russian;}
            else if(Has("--capture-auras")){App.SelectedHero=Hero.Lumen;App.Solo();App.Simulation.GrantExperience(GameData.ExperienceRequired(1));App.Simulation.ChooseItem(App.LocalPlayer,0);I18n.Language=GameLanguage.Russian;}
            else if(Has("--capture-milestone")){App.SelectedHero=Hero.Lumen;App.Solo();int xp=0;for(int level=1;level<25;level++)xp+=GameData.ExperienceRequired(level);App.Simulation.GrantExperience(xp);int n=0;while(App.LocalPlayer.offerKind!=RewardKind.Weapon&&n++<100)App.Simulation.ChooseItem(App.LocalPlayer,0);I18n.Language=GameLanguage.Russian;}
            else if(Has("--capture-character")){App.BeginSelection(SessionMode.Solo);App.SelectedHero=Hero.Echo;}
            else if(Has("--capture-network"))App.Page=MenuPage.Connection;
            else if(Has("--capture-host-menu"))App.Page=MenuPage.HostLobby;
            else if(Has("--capture-join-menu"))App.Page=MenuPage.JoinLobby;
            else if(Has("--capture-inventory"))
            {App.Solo();var p=App.LocalPlayer;App.Simulation.ApplyAttribute(p,2,2);App.Simulation.ApplyAttribute(p,4,3);App.Simulation.ApplyAttribute(p,18,2);App.Simulation.ApplyItem(p,(int)ItemId.BeginnersLuck);App.Simulation.ApplyItem(p,(int)ItemId.IceCore);App.Simulation.ApplyItem(p,(int)ItemId.AngelWings);App.Inventory=true;}
            else if(Has("--capture-upgrade")){App.Solo();App.Simulation.GrantExperience(GameData.ExperienceRequired(1));}
            else if(Has("--capture-roulette"))
            {App.Solo();App.State.coins=200;var p=App.LocalPlayer;App.Simulation.ApplyAttribute(p,18,3);p.position=App.State.chests[0].position;App.Simulation.TryOpenChest(p);}
        }
        bool Has(string value)=>Array.IndexOf(args,value)>=0;
        void Shot(string file){if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,file));}
        void Update()
        {
            float age=Time.realtimeSinceStartup-start;
            if(!smoke)
            {
                float captureAt=Has("--capture-roulette")?1.45f:3;
                if(age>captureAt&&!homeShot)
                {
                    string file=Has("--capture-lumen")?"lumen-character.png":Has("--capture-auras")?"lumen-aura-choice.png":Has("--capture-milestone")?"level25-weapons.png":Has("--capture-character")?"character-select.png":Has("--capture-network")?"network-menu.png":Has("--capture-host-menu")?"host-menu.png":Has("--capture-join-menu")?"join-menu.png":Has("--capture-inventory")?"inventory-attributes.png":Has("--capture-upgrade")?"attribute-upgrade.png":Has("--capture-roulette")?"chest-floor-loot.png":"main-menu.png";
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
            if(s!=null&&s.players.Count==GameData.MaxPlayers)
            {
                if(s.players.TrueForAll(p=>p.ready))readySeen=true;
                if(!lobbyShot&&age>4){Shot(third?"lobby-client2.png":client?"lobby-client.png":"lobby-host.png");lobbyShot=true;}
                if(s.phase==RunPhase.Lobby&&!readyPressed&&age>6){App.ToggleReady();readyPressed=true;}
            }
            if(s?.phase==RunPhase.Playing&&me!=null)
            {
                if(!client&&!fixtureReady)
                {
                    // Random party spawns can be hundreds of metres from the original
                    // fixed test objects. Keep real interactions inside the smoke-test window.
                    s.chests[0].position=WorldMap.Resolve(me.position,Vector3.forward*4,1);
                    s.chests[0].position=WorldMap.OnGround(s.chests[0].position.x,s.chests[0].position.z);
                    s.shrines[0].position=WorldMap.Resolve(me.position,Vector3.back*5,2);
                    s.shrines[0].position=WorldMap.OnGround(s.shrines[0].position.x,s.shrines[0].position.z);
                    foreach(var player in s.players)player.invulnerability=999;
                    fixtureReady=true;
                }
                if(me.qCooldown>0&&me.eCooldown>0&&me.relicCooldown>0)castsSeen=true;
                if(!me.grounded&&me.position.y-WorldMap.Ground(me.position)>0.4f)jumpSeen=true;
                if(me.crouching)crouchSeen=true;if(s.Player(0)?.chargingShrine>=0)channelSeen=true;
                input.crouch=s.elapsed>6&&s.elapsed<7;input.revive=false;
                Vector3 move;
                if(!client&&s.openedChests==0)
                {var chest=s.chests[0];move=(chest.position-me.position).normalized;if(Vector3.Distance(chest.position,me.position)<3)input.interact=true;}
                else if(client&&s.loot.Exists(d=>!d.collecting))
                {var drop=s.loot.Find(d=>!d.collecting);move=(drop.position-me.position).normalized;if(Vector3.Distance(drop.position,me.position)<2.3f)input.interact=true;}
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
                if(!client&&s.elapsed>8&&!xpGranted){App.Simulation.GrantExperience(GameData.ExperienceRequired(1));xpGranted=true;}
                if(!client&&s.elapsed>18&&!milestoneGranted)
                {int xp=s.xpRequired-s.xp;for(int level=s.level+1;level<26;level++)xp+=GameData.ExperienceRequired(level);App.Simulation.GrantExperience(xp);milestoneGranted=true;}
                if(me.pendingLevels>0)
                {
                    if(pendingAt<0)pendingAt=s.elapsed;
                    if(s.elapsed-pendingAt>.5f&&!upgradeShot){Shot(client?"coop-upgrade-ru.png":"coop-upgrade-en.png");upgradeShot=true;}
                    int offerKey=me.completedLevel*32+me.upgradeStage*8+(int)me.offerKind;
                    if(s.elapsed-pendingAt>1&&offerKey!=chosenOffer)
                    {
                        levelKeepsMoving=true;
                        // Send one decision, then wait for its replicated acknowledgement.
                        // Repeating the old accessory index each frame also selects aura zero
                        // before the client can observe the next screen.
                        input.choice=me.offerKind==RewardKind.Aura?Mathf.Max(0,Array.FindIndex(me.offers,a=>me.auraLevels[a]==0)):0;
                        chosenOffer=offerKey;
                    }
                }
                if(s.elapsed>21&&!gameShot){Shot(third?"coop-lumen.png":client?"coop-client.png":"coop-host.png");gameShot=true;}
            }
            // Lumen has two decisions per level. Wait for the replicated milestone on all peers,
            // allowing for different process start times and snapshot/choice latency.
            if(age>32&&!reported&&(s!=null&&s.players.Count==GameData.MaxPlayers&&s.players.TrueForAll(p=>p.weapons[1]>=0)||age>39))
            {
                int items=0;if(me!=null)foreach(int count in me.items)items+=count;
                var report=new Report{connected=s?.players.Count==GameData.MaxPlayers,localId=App.LocalId,ready=readySeen,jump=jumpSeen,actions=castsSeen,music=SynthAudio.Instance!=null&&SynthAudio.Instance.MusicReady,
                    noLevelPause=levelKeepsMoving,crouch=crouchSeen,blessing=channelSeen&&s?.shrines[0].spent==true&&s.Player(0).blessings[s.shrines[0].buff]>0,players=s?.players.Count??0,level=s?.level??0,kills=s?.kills??0,items=items,chests=s?.openedChests??0,gold=s?.earnedCoins??0,weapons=me==null?0:Array.FindAll(me.weapons,w=>w>=0).Length,hostWeapons=s?.Player(0)==null?0:Array.FindAll(s.Player(0).weapons,w=>w>=0).Length};
                report.reward=s!=null&&s.players.Exists(p=>Array.Exists(p.items,count=>count>0));
                report.auras=s!=null&&s.players.Exists(p=>p.hero==Hero.Lumen&&Array.FindAll(p.auraLevels,n=>n>0).Length==5);
                if(!report.auras&&s!=null)File.WriteAllText(Path.Combine(folder,third?"smoke-aura-client2.json":client?"smoke-aura-client.json":"smoke-aura-host.json"),JsonUtility.ToJson(s));
                report.secondWeapons=s!=null&&s.players.TrueForAll(p=>p.weapons.Length==2&&p.weapons[1]>=0);
                report.difficulty=s!=null&&Mathf.Approximately(s.enemyPartyScale,4);
                report.ok=report.connected&&report.ready&&report.jump&&report.actions&&report.music&&report.noLevelPause&&report.crouch&&report.blessing&&report.level>=25&&report.reward&&report.auras&&report.secondWeapons&&report.difficulty&&report.chests>0&&report.gold>0&&s.phase==RunPhase.Playing;
                string json=JsonUtility.ToJson(report);File.WriteAllText(Path.Combine(folder,third?"smoke-client2.json":client?"smoke-client.json":"smoke-host.json"),json);Debug.Log("ERA_SMOKE "+json);exit=report.ok?0:2;reported=true;
            }
            if(age>(client?42:45))Application.Quit(exit);
        }
    }
}
