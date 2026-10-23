using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Era.Editor
{
    public static class SimulationChecks
    {
        static int assertions;
        static void Check(bool condition, string label)
        { assertions++; if (!condition) throw new Exception("ERA check failed: " + label); }
        static Simulation New(bool coop = false)
        {
            var sim = new Simulation(23,coop); sim.AddPlayer(0, "Искра", Hero.Ember);
            if (coop) {sim.AddPlayer(1, "Echo", Hero.Echo);sim.SetReady(0,true);sim.SetReady(1,true);}
            sim.Start(); sim.State.spawnTimer = 1000;sim.State.resourceNodes.Clear(); return sim;
        }
        static void Step(Simulation sim, float seconds, Dictionary<int, InputFrame> inputs = null)
        {
            inputs ??= new Dictionary<int, InputFrame>();
            for (int i = 0; i < Mathf.CeilToInt(seconds * 30); i++) sim.Step(1f / 30, inputs);
        }
        [MenuItem("ERA/Run simulation and network checks")]
        public static void Run()
        {
            assertions = 0;
            Readiness();Movement();Jumping();Crouching();Shrines();Combat();Weapons();Progression();AurasAndMilestone();Campaign();LevelPause();Economy();ChestPricesAndAuraProgress();Encounters();Cooperation();Endings();Serialization();Presentation();Localization();Network();WorldMap.SetStage(1);
            assertions+=RoguelikeChecks.Run();assertions+=BalanceChecks.Run();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/editor-checks.json", "{\"ok\":true,\"assertions\":" + assertions + "}");
            Debug.Log("ERA_CHECKS_OK " + assertions + " assertions");
        }
        static void Movement()
        {
            var sim = New(); var p = sim.State.Player(0);
            Vector3 start = p.position;
            Step(sim, 1, new Dictionary<int, InputFrame> { [0] = new InputFrame { x = 999, z = 999 } });
            float travelled = Vector3.Distance(start, p.position);
            Check(travelled < 7.9f && travelled > 7.6f, "normalized input speed");
            p.position=WorldMap.OnGround(GameData.ArenaRadius-1,0);
            Step(sim, 1, new Dictionary<int, InputFrame> { [0] = new InputFrame { x = 1 } });
            Check(new Vector2(p.position.x, p.position.z).magnitude <= GameData.ArenaRadius + 0.01f, "arena bounds");
            Step(sim, 1, new Dictionary<int, InputFrame> { [0] = new InputFrame { x = float.NaN, z = float.PositiveInfinity } });
            Check(!float.IsNaN(p.position.x) && !float.IsInfinity(p.position.z), "invalid input sanitized");
            var input = new InputFrame { jump = true, dash = true, primary = true, secondary = true, choice = 2 };
            input.ConsumeEdges(); Check(!input.jump && !input.dash && !input.primary && !input.secondary && input.choice == -1, "input edges consumed");
        }
        static void Readiness()
        {
            var sim=new Simulation();sim.AddPlayer(0,"Rowan",Hero.Ember);sim.AddPlayer(1,"Willow",Hero.Echo);
            sim.Start();Check(sim.State.phase==RunPhase.Lobby,"co-op cannot start without ready players");
            sim.SetReady(0,true);Step(sim,1);Check(sim.State.phase==RunPhase.Lobby&&sim.State.readyCountdown<0,"one ready player does not start countdown");
            sim.SetReady(1,true);Step(sim,1);Check(sim.State.phase==RunPhase.Lobby&&sim.State.readyCountdown>1.8f,"both ready starts three second countdown");
            Check(!sim.SetLoadout(0,Hero.Echo,Relic.Hourglass,"Changed"),"ready loadout is locked");
            sim.SetReady(0,false);Step(sim,.1f);Check(sim.State.readyCountdown<0,"unready cancels countdown");
            Check(sim.SetLoadout(0,Hero.Echo,Relic.Hourglass,"Changed"),"unready player can change character");
            sim.SetReady(0,true);Step(sim,3.2f);Check(sim.State.phase==RunPhase.Playing,"both ready automatically starts co-op");
            Check(!sim.SetReady(0,false),"readiness cannot mutate during play");
            sim=new Simulation();sim.AddPlayer(0,"A",Hero.Ember);sim.AddPlayer(1,"B",Hero.Echo);sim.SetReady(0,true);sim.SetReady(1,true);Step(sim,.8f);
            sim.State.players.RemoveAt(1);Step(sim,1);Check(sim.State.phase==RunPhase.Lobby&&sim.State.readyCountdown<0,"disconnect cancels lobby countdown");
        }
        static void Jumping()
        {
            var sim=New();var p=sim.State.Player(0);var input=new InputFrame{jump=true,jumpHeld=true};var commands=new Dictionary<int,InputFrame>{{0,input}};
            sim.Step(1f/30,commands);input.jump=false;float peak=0;
            for(int i=0;i<35;i++){sim.Step(1f/30,commands);peak=Mathf.Max(peak,p.position.y-WorldMap.Ground(p.position));}
            Check(peak>2&&peak<2.6f,"held jump reaches a useful height");
            Check(p.grounded&&p.landSerial==1&&p.jumpSerial==1,"jump lands once on terrain");
            sim=New();p=sim.State.Player(0);input=new InputFrame{jump=true,jumpHeld=false};commands=new Dictionary<int,InputFrame>{{0,input}};sim.Step(1f/30,commands);input.jump=false;float low=0;
            for(int i=0;i<30;i++){sim.Step(1f/30,commands);low=Mathf.Max(low,p.position.y);}
            Check(low<peak*.7f,"releasing jump shortens the arc");
            p.position=WorldMap.OnGround(65,50);p.grounded=true;sim.Step(1f/30,new Dictionary<int,InputFrame>{{0,new InputFrame{jump=true,jumpHeld=true}}});
            Check(p.position.y>WorldMap.Ground(p.position)&&!p.grounded,"jump works on raised terrain");
        }
        static void Economy()
        {
            var sim=New(true);var a=sim.State.Player(0);var b=sim.State.Player(1);var chest=sim.State.chests[0];a.position=chest.position;
            Check(sim.State.chests.Count==GameData.ChestCount,"expanded map contains sixty chests");int money=sim.State.coins;
            Check(sim.TryOpenChest(a)&&sim.State.coins==money-chest.cost&&chest.owner==0,"opening chest atomically spends shared gold");
            b.position=chest.position;int after=sim.State.coins;Check(!sim.TryOpenChest(b)&&sim.State.coins==after,"opened chest cannot be bought twice");
            Check(sim.State.loot.Count==1&&chest.reward>=0&&(chest.rewardKind==RewardKind.Weapon?chest.reward<GameData.Weapons.Length:chest.reward<GameData.Items.Length&&(int)GameData.Items[chest.reward].rarity==chest.rewardRarity),"chest rolls a valid rarity-matched reward");
            Check(chest.rewardKind==RewardKind.Item&&sim.State.loot[0].position.y<chest.position.y+.8f,"chest item appears immediately near the floor");
            a.position=chest.position+Vector3.right*20;Step(sim,4);Check(sim.State.loot.Count==1&&a.items[chest.reward]==0&&b.items[chest.reward]==0,"ground loot persists without automatic delivery");
            b.position=sim.State.loot[0].origin;Check(sim.TryCollectLoot(b)&&!sim.TryCollectLoot(a),"ally may claim chest loot and claim is atomic");
            Step(sim,.4f);Check(sim.State.loot.Count==0&&b.items[chest.reward]==1&&a.items[chest.reward]==0,"reward goes to the actual collector");
            var second=sim.State.chests[1];a.position=second.position;Check(!sim.TryOpenChest(a)&&!second.opened,"insufficient gold leaves chest closed");
            a.position=Vector3.zero;sim.State.pickups.Add(new PickupState{id=500,value=7,kind=PickupKind.Gold,position=Vector3.zero});Step(sim,.4f);
            Check(sim.State.coins==after+7&&sim.State.earnedCoins==7,"gold pickups credit team wallet");
            sim.State.pickups.Add(new PickupState{id=501,value=8,kind=PickupKind.Gold,position=Vector3.forward*30});
            sim.State.pickups.Add(new PickupState{id=502,value=3,kind=PickupKind.Experience,position=Vector3.forward*30});
            float range=a.pickupRange;sim.ApplyItem(a,(int)ItemId.Magnet);Check(a.pickupRange>range&&sim.State.pickups.Count==2,"magnet increases pickup radius without consuming distant loot");
        }
        static void ChestPricesAndAuraProgress()
        {
            var sim=New(true);var a=sim.State.Player(0);var b=sim.State.Player(1);var first=sim.State.chests[0];var second=sim.State.chests[1];
            Check(first.baseCost==12&&first.cost==12&&second.cost==14,"first chest uses its original price");
            a.position=first.position;Check(sim.TryOpenChest(a)&&sim.State.coins==18&&second.cost==24&&first.cost==12,"successful purchase multiplies remaining prices by five thirds rounded up, retaining the paid price");
            b.position=second.position;Check(!sim.TryOpenChest(b)&&sim.State.openedChests==1&&second.cost==24,"failed purchase cannot increase the team price");
            sim.State.coins=1000;Check(sim.TryOpenChest(b)&&sim.State.coins==976&&sim.State.openedChests==2&&sim.State.chests[2].cost==45,"ally pays the increased price and advances the same team counter");
            Check(!sim.TryOpenChest(b)&&sim.State.openedChests==2,"repeated interaction with an opened chest cannot advance prices");
            var boss=sim.SpawnEnemy(EnemyKind.Warden);sim.DamageEnemy(boss,a,99999999);a.position=sim.State.portal.position;
            Check(sim.TryUsePortal(a)&&sim.State.openedChests==2&&sim.State.chestsOpenedInStage==0&&sim.State.chests.TrueForAll(c=>c.cost==GameData.ChestPrice(c.baseCost,sim.State.openedChests)),"portal transition preserves price progression for the new realm");
            var lumen=sim.AddPlayer(2,"Lumen",Hero.Lumen);Check(GameData.AuraUpgradeCount(lumen,0)==0,"starting aura is rank one with zero upgrades");
            sim.LearnAura(lumen,0);sim.LearnAura(lumen,0);Check(lumen.auraLevels[0]==3&&GameData.AuraUpgradeCount(lumen,0)==2,"two repeated choices produce rank three and two upgrades");
            sim.LearnAura(lumen,3);Check(lumen.currentAura==3&&lumen.auraLevels[3]==1&&GameData.AuraUpgradeCount(lumen,3)==0,"new aura becomes current without counting acquisition as an upgrade");
            var snapshot=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            Check(snapshot.Player(2).currentAura==3&&snapshot.Player(2).auraLevels[0]==3&&snapshot.chests[0].baseCost==sim.State.chests[0].baseCost&&snapshot.chests[0].cost==sim.State.chests[0].cost&&snapshot.openedChests==2,"network snapshot retains aura progress and shared chest prices");
            WorldMap.SetStage(1);
        }
        static void MeleeWindups()
        {
            var sim=New(true);foreach(var p in sim.State.players)p.weapons=new[]{-1,-1};var target=sim.State.Player(0);sim.State.Player(1).position=WorldMap.OnGround(35,0);
            var attacker=sim.SpawnEnemy(EnemyKind.Wisp);attacker.position=target.position+Vector3.forward*.8f;attacker.attackTimer=0;float health=target.hp;
            Step(sim,.5f);Check(target.hp==health,"newly arriving enemy cannot immediately damage a player");Step(sim,.2f);
            Check(attacker.meleeWindup>0&&target.hp==health,"melee attack announces a windup before damage");target.position+=Vector3.right*4;Step(sim,.4f);
            Check(target.hp==health,"moving out of a melee windup avoids its damage");attacker.position=target.position+Vector3.forward*.8f;attacker.attackTimer=0;Step(sim,.45f);
            Check(target.hp<health,"remaining inside the announced melee strike takes damage");
        }
        static void Encounters()
        {
            MeleeWindups();
            var sim=New(true);foreach(var player in sim.State.players){player.weapons=new[]{-1,-1};player.invulnerability=999;}
            sim.State.spawnTimer=0;Step(sim,2);
            Check(sim.State.wave==1&&sim.State.waveSpawned>=6&&sim.State.nextWave>57,"continuous nearby spawning begins at stage start");
            sim.State.enemies.Clear();int spawned=sim.State.waveSpawned;Step(sim,1.5f);
            Check(sim.State.waveSpawned>spawned&&sim.State.enemies.Count>0,"clearing all enemies does not pause spawning");
            sim.State.stageElapsed=59.94f;Step(sim,.033f);Check(sim.State.wave==1,"a minute wave cannot begin early");Step(sim,.1f);
            Check(sim.State.wave==2&&sim.State.waveActive&&sim.State.waveQuota>0&&sim.State.nextWave>59,"first expired minute adds a horde without requiring kills");
            float firstInterval=sim.State.spawnTimer;int previousEnemies=sim.State.enemies.Count;sim.State.stageElapsed=119.99f;Step(sim,.05f);
            Check(sim.State.wave==3&&sim.State.enemies.Count>previousEnemies&&sim.State.spawnTimer<firstInterval,"each minute adds enemies and increases spawn frequency");
            sim.State.stageElapsed=599.99f;Step(sim,.1f);Check(sim.State.wave==11&&sim.State.stageElapsed==600&&CampaignData.Remaining(sim.State.stageElapsed)==0,"ten minute countdown expires exactly once at zero");
            int finalWave=sim.State.wave;Step(sim,2);Check(sim.State.wave==finalWave&&sim.State.nextWave==0,"zero timer cannot trigger repeated minute hordes");
            sim.State.enemies.Clear();sim.State.spawnTimer=0;Step(sim,.1f);Check(sim.State.enemies.Count>0,"nearby spawning continues while players prepare to summon the boss");
            sim.State.stageElapsed=999;Step(sim,.1f);Check(!sim.State.enemies.Exists(e=>e.kind==EnemyKind.Warden),"elapsed time never auto-spawns the boss");
            var p=sim.State.Player(0);Check(!sim.TrySummonBoss(p),"statue requires proximity");p.position=sim.State.bossStatuePosition;
            Check(sim.TrySummonBoss(p)&&sim.State.enemies.FindAll(e=>e.kind==EnemyKind.Warden).Count==1&&!sim.TrySummonBoss(p),"boss statue is a single authoritative summon");
            var locations=New(true);locations.State.Player(0).position=WorldMap.OnGround(285,0);locations.State.Player(1).position=WorldMap.OnGround(275,8);
            for(int i=0;i<60;i++)
            {
                var enemy=locations.SpawnEnemy();float nearest=float.MaxValue;
                foreach(var player in locations.State.players)nearest=Mathf.Min(nearest,Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(enemy.position.x,enemy.position.z)));
                Check(nearest>=CampaignData.SpawnMinDistance-.1f&&nearest<=CampaignData.SpawnMaxDistance+.1f,"resolved spawn stays near party and outside safety radius at map edge "+i);
                locations.State.enemies.Remove(enemy);
            }
            locations.State.spawnTimer=0;foreach(var player in locations.State.players){player.weapons=new[]{-1,-1};player.invulnerability=999;}Step(locations,45);
            Check(locations.State.enemies.Count<=GameData.MaxEnemies,"continuous spawning respects the performance cap");
            sim=New(true);foreach(var player in sim.State.players){player.weapons=new[]{-1,-1};player.invulnerability=999;}p=sim.State.Player(0);
            Check(sim.State.eliteShrines.Count==4,"each realm has four elite shrines");var shrine=sim.State.eliteShrines[0];p.position=shrine.position;
            Check(sim.TryActivateEliteShrine(p)&&sim.State.enemies.FindAll(e=>e.eliteShrine==shrine.id).Count==5,"one shrine summons exactly five elite guards");
            Check(!sim.TryActivateEliteShrine(p),"elite shrine cannot summon duplicate groups");
            var guards=sim.State.enemies.FindAll(e=>e.eliteShrine==shrine.id);for(int i=0;i<4;i++)sim.DamageEnemy(guards[i],p,99999);Step(sim,.05f);
            Check(!shrine.completed&&!sim.State.chests.Exists(c=>c.free),"four kills do not award the elite chest");sim.DamageEnemy(guards[4],p,99999);Step(sim,.05f);
            var free=sim.State.chests.Find(c=>c.free);Check(shrine.completed&&free!=null&&free.cost==0&&sim.State.chests.Count==GameData.ChestCount+1,"all five elite kills create one free chest");Step(sim,.2f);
            Check(sim.State.chests.FindAll(c=>c.free).Count==1,"completed elite shrine cannot duplicate its reward");
            var ally=sim.State.Player(1);ally.position=free.position;int gold=sim.State.coins;
            Check(sim.TryOpenChest(ally)&&sim.State.coins==gold&&free.opened&&sim.State.loot.Count>0,"any ally can open the free chest without paying");
            sim=new Simulation(12);p=sim.AddPlayer(0,"Test",Hero.Ember);p.weapons=new[]{-1,-1};sim.Start();sim.State.spawnTimer=1000;
            Check(sim.State.resourceNodes.Count==72&&sim.State.resourceNodes.FindAll(n=>n.kind==PickupKind.Gold).Count==36,"map contains seventy-two evenly split GPU resource nodes");
            var node=sim.State.resourceNodes[0];p.position=node.position;gold=sim.State.coins;Check(sim.TryBreakResource(p)&&node.broken&&sim.State.pickups.Count==0&&sim.State.coins==gold+33,"gold GPU immediately credits the shared wallet");
            int count=sim.State.pickups.Count;Check(!sim.TryBreakResource(p)&&sim.State.pickups.Count==count,"broken graphics cards cannot be farmed twice");
            var blue=sim.State.resourceNodes[1];sim.DamageResource(blue,100,p);Check(blue.broken&&sim.State.pickups.FindAll(d=>d.kind==PickupKind.Experience).Count==4,"blue GPU drops experience");
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));Check(copy.resourceNodes[0].broken&&copy.eliteShrines.Count==4&&copy.bossStatuePosition==sim.State.bossStatuePosition,"snapshot retains resource nodes and encounter objects");
            sim=New(true);p=sim.State.Player(0);p.weapons=new[]{-1,-1};var origin=p.position;sim.DamagePlayer(p,5,p.position+Vector3.forward);
            Check(p.hitSlide>.2f&&p.knockback.z<0,"enemy hit starts a short backward slide");Step(sim,.4f);float slide=Vector2.Distance(new Vector2(origin.x,origin.z),new Vector2(p.position.x,p.position.z));
            Check(slide>.2f&&slide<.6f&&p.hitSlide==0&&p.grounded,"slide remains below sixty centimeters and finishes on the floor");
            var edge=WorldMap.Resolve(Vector3.zero,Vector3.right*1000,.4f);Check(new Vector2(edge.x,edge.z).magnitude<GameData.ArenaRadius-6,"collision keeps players inside the visible perimeter wall");
            var atlas=IconLibrary.ItemAtlas;Check(atlas!=null&&atlas.width==1536&&atlas.height==1024,"supplied item artwork atlas is used at its original size");
            for(int i=0;i<30;i++){var r=IconLibrary.ItemPixels(i);Check(r.x>=0&&r.y>=0&&r.xMax<=atlas.width&&r.yMax<=atlas.height,"item sprite lies inside the supplied artwork: "+i);}
            foreach(Hero hero in Enum.GetValues(typeof(Hero))){var model=Resources.Load<GameObject>("Characters/"+hero);Check(model.GetComponent<HumanRigShape>()?.bones.Length>10,"hero uses the smooth adult proportion rig: "+hero);}
            Check(Resources.Load<AudioClip>("Audio/Storm-of-the-Seven")?.length>25,"separate battle score is included");
            WorldMap.SetStage(1);
        }
        static void Crouching()
        {
            var sim=New();var p=sim.State.Player(0);var start=p.position;var input=new InputFrame{x=1,crouch=true};var commands=new Dictionary<int,InputFrame>{{0,input}};Step(sim,.5f,commands);
            float distance=Vector3.Distance(start,p.position);Check(p.crouching&&distance>1.5f&&distance<1.8f,"held crouch reduces movement speed");
            input.crouch=false;Step(sim,.1f,commands);Check(!p.crouching,"releasing crouch restores standing state");
            input.crouch=true;input.jump=input.jumpHeld=true;sim.Step(1f/30,commands);input.jump=false;Check(!p.crouching&&!p.grounded,"jump leaves crouch immediately");
            Step(sim,.2f,commands);int serial=p.jumpSerial;input.jump=true;sim.Step(1f/30,commands);input.jump=false;Check(p.jumpSerial==serial,"unblessed player cannot jump again in air");
            sim.ApplyBlessing(p,0);sim.Step(.1f,commands);input.jump=true;sim.Step(1f/30,commands);input.jump=false;Check(p.jumpSerial==serial+1&&p.airJumpsUsed==1&&p.verticalSpeed>9,"Skybound blessing grants a real extra air jump");
            input.jump=true;sim.Step(1f/30,commands);input.jump=false;Check(p.jumpSerial==serial+1,"extra jump cannot be reused before landing");
            Step(sim,1.5f,commands);Check(p.grounded&&p.airJumpsUsed==0,"landing replenishes extra jumps");
            for(int i=0;i<8;i++)sim.ApplyBlessing(p,0);Check(p.extraJumps==3&&p.blessings[0]==3,"extra air jumps cap at three");
        }
        static void Shrines()
        {
            var sim=New(true);var a=sim.State.Player(0);var b=sim.State.Player(1);var shrine=sim.State.shrines[0];a.position=shrine.position+Vector3.right;b.position=shrine.position-Vector3.right;
            var ia=new InputFrame{revive=true};var ib=new InputFrame{revive=true};var commands=new Dictionary<int,InputFrame>{{0,ia},{1,ib}};
            Check(sim.State.shrines.Count==12,"large map contains twelve blessing shrines");Step(sim,2,commands);Check(shrine.charge>1.9f&&!shrine.spent&&shrine.owner==0,"shrine channels for one owner at a time");
            Check(Mathf.Abs(Mathf.DeltaAngle(a.yaw,-90))<1,"channeling character faces the shrine");
            ia.revive=ib.revive=false;sim.Step(1f/30,commands);Check(shrine.charge==0&&shrine.owner==-1,"releasing F cancels and resets charge");
            ia.revive=true;Step(sim,2,commands);a.position+=Vector3.forward*6;sim.Step(1f/30,commands);Check(shrine.charge==0,"leaving range resets shrine charge");
            a.position=shrine.position;Step(sim,4.9f,commands);Check(!shrine.spent&&a.blessings[0]+a.blessings[1]+a.blessings[2]+a.blessings[3]+a.blessings[4]+a.blessings[5]+a.blessings[6]+a.blessings[7]==0,"no blessing before five seconds");
            Step(sim,.2f,commands);Check(shrine.spent&&shrine.buff>=0&&shrine.buff<8&&a.blessings[shrine.buff]==1&&b.blessings[shrine.buff]==0,"completed shrine awards one random buff only to channeler");
            Check(sim.State.rewards.Exists(n=>n.kind==RewardKind.Blessing&&n.owner==a.id&&n.content==shrine.buff),"structured shrine reward includes buff description ID");
            int count=a.blessings[shrine.buff];Step(sim,6,commands);Check(a.blessings[shrine.buff]==count,"spent shrine cannot award again");
            shrine=sim.State.shrines[1];a.position=shrine.position;Step(sim,1,commands);a.downed=true;sim.Step(1f/30,commands);Check(shrine.charge==0&&shrine.owner==-1,"downed owner cannot keep charging");
            var solo=New();var p=solo.State.Player(0);var s=solo.State.shrines[0];p.position=s.position;var held=new Dictionary<int,InputFrame>{{0,new InputFrame{revive=true}}};Step(solo,1,held);float charge=s.charge;solo.GrantExperience(12);Step(solo,1,held);Check(s.charge==charge,"solo level pause freezes shrine charge too");
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));Check(copy.shrines.Count==12&&copy.shrines[0].spent&&copy.Player(0).blessings.Length==8,"snapshot includes shrine state and personal buffs");
        }
        static void Presentation()
        {
            var audio=Resources.Load<AudioClip>("Audio/Stillwater-at-Dawn");Check(audio!=null&&audio.length>40&&audio.channels==2,"original stereo music is included");
            var echoPrefab=Resources.Load<GameObject>("Characters/Echo");
            Check(echoPrefab!=null&&echoPrefab.GetComponent<HeroMotion>().Hero==Hero.Echo,"editable Echo prefab keeps selected character identity");
            var brutePrefab=Resources.Load<GameObject>("Creatures/Brute");Check(brutePrefab!=null&&brutePrefab.GetComponent<CreatureMotion>().limbs.Count==4,"creature prefab preserves animation joints");
            Check(UnityEngine.Object.FindAnyObjectByType<WorldEnvironment>()!=null,"large map is present in the editable Unity scene");
            var parent=new GameObject("Presentation checks");var v=new VisualFactory();
            try
            {
                var hero=HeroMotion.Create(v,parent.transform,Hero.Ember,"Ember");Check(hero.leftKnee!=null&&hero.rightElbow!=null&&hero.cape!=null,"hero has a jointed animation rig");
                foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                {var creature=CreatureMotion.Create(v,parent.transform,kind);Check(creature.body!=null&&creature.limbs.Count>=(kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime?0:kind==EnemyKind.Bat?2:4),"creature has its role's animation joints: "+kind);}
            }
            finally{UnityEngine.Object.DestroyImmediate(parent);}
            foreach(string character in new[]{"Characters/Ember","Characters/Echo","Characters/Lumen","Creatures/Wisp","Creatures/Stalker","Creatures/Warden"})
            {
                var model=Resources.Load<GameObject>(character);var skeletal=model.GetComponent<SkeletalActor>();
                Check(skeletal!=null&&skeletal.animationPlayer.GetClipCount()>=15&&model.GetComponentsInChildren<SkinnedMeshRenderer>().Length>=1,"skeletal actor retains real animation clips: "+character);
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                bool faceted=character.StartsWith("Characters/")||character.EndsWith("Stalker")||character.EndsWith("Wisp");
                Check(faceted?skin.sharedMaterial.shader.name=="Era/Faceted Realm"&&skin.sharedMesh.vertexCount<60000&&skin.sharedMesh.vertexCount==skin.sharedMesh.triangles.Length:skin.sharedMaterial.mainTexture!=null,"bounded detailed faceted mesh or existing painted atlas: "+character);
            }
            for(int stage=1;stage<=7;stage++)
            {
                var boss=Resources.Load<GameObject>("Bosses/Stage"+stage);
                Check(boss!=null&&boss.GetComponent<CreatureMotion>().kind==EnemyKind.Warden&&boss.GetComponent<SkeletalActor>()!=null,"distinct animated boss prefab for stage "+stage);
            }
            var lumenArt=Resources.Load<GameObject>("Characters/Lumen");
            Check(lumenArt.GetComponentsInChildren<SkinnedMeshRenderer>().Length==1&&lumenArt.GetComponent<HumanRigShape>()!=null,"aura keeper costume is a single animated faceted mesh");
            Check(lumenArt.GetComponentInChildren<SkinnedMeshRenderer>().name=="Faceted silhouette / Lumen","bearded aura keeper uses the authored reference costume");
            TextLayouts();
            var environment=UnityEngine.Object.FindAnyObjectByType<WorldEnvironment>();
            Check(environment.bakedVersion==WorldEnvironment.CurrentVersion&&environment.GetComponentsInChildren<Renderer>().Length<800,"terrain, trees and grass use bounded chunk renderer count");
            Check(environment.GetComponentInChildren<LivingGrass>().patches.Length>70,"grass covers the complete realm in reusable patches");
            foreach(string name in new[]{"StoneDetail","WoodDetail","MeadowDetail","LeafDetail","StoneNormal","WoodNormal","GrassClump"})
                Check(Resources.Load<Texture2D>("Textures/"+name)?.width==512,"detailed seamless surface resource: "+name);
            foreach(string name in IconBuilder.Names)
            {
                var icon=Resources.Load<Texture2D>("Icons/"+name);Check(icon!=null&&icon.width==128,"transparent interface icon included: "+name);
                var pixels=new Texture2D(2,2);try
                {
                    pixels.LoadImage(File.ReadAllBytes("Assets/Era/Resources/Icons/"+name+".png"));var colors=pixels.GetPixels32();int visible=0;
                    foreach(var pixel in colors)if(pixel.a>0)visible++;
                    Check(colors[0].a==0&&visible>300&&visible<colors.Length*.65f,"icon has transparent padding and a non-empty complete silhouette: "+name);
                }finally{UnityEngine.Object.DestroyImmediate(pixels);}
            }
            var animated=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/Ember"));
            try
            {
                var skeletal=animated.GetComponent<SkeletalActor>();var clip=skeletal.animationPlayer.GetClip("Running_A");clip.SampleAnimation(animated,.05f);var left=skeletal.leftThigh.localRotation;clip.SampleAnimation(animated,.30f);
                Check(Quaternion.Angle(left,skeletal.leftThigh.localRotation)>5,"imported running clip actually articulates skeleton joints");
            }
            finally{UnityEngine.Object.DestroyImmediate(animated);}
        }
        static void TextLayouts()
        {
            var font=Font.CreateDynamicFontFromOSFont("Arial",20);
            try
            {
                font.RequestCharactersInTexture("Пример text",19,FontStyle.Normal);Check(HudTextLayout.Width(font,"Пример text",19,FontStyle.Normal)>40,"text measurements use real Cyrillic and Latin glyph advances");
                foreach(string value in new[]{"Хранитель аур: на каждом уровне новая аура или улучшение изученной. До пяти типов.","New minute, new horde. Enemies keep arriving while you explore.","192.168.100.250:7777/ОченьДлинноеНазваниеПерсонажа"})
                {
                    string previous=null;
                    foreach(float scale in new[]{.5f,.64f,.8f,1,1.2f})
                    {
                        var block=HudTextLayout.Fit(font,value,19,300*scale/scale,90,false);font.RequestCharactersInTexture(value,block.size,FontStyle.Normal);
                        foreach(var line in block.lines)Check(HudTextLayout.Width(font,line,block.size,FontStyle.Normal)<=300.1f,"wrapped Russian/English text and long tokens fit the panel");
                        string current=string.Join("\n",block.lines);Check(previous==null||previous==current,"line breaks remain stable across screen scales");previous=current;
                        Check(block.lines.Length*block.lineHeight<=90.1f,"text block fits available height");
                    }
                }
                Check(HudTextLayout.Fit(font,"Первая строка\n\nТретья строка",16,400,90,false).lines.Length==3,"explicit paragraphs and blank lines are retained");
            }
            finally{UnityEngine.Object.DestroyImmediate(font);}
        }
        static void Localization()
        {
            var language=I18n.Language;
            try
            {
                I18n.Language=GameLanguage.English;Check(GameData.Weapons[0].name=="Crescent Axe"&&GameData.Q(Hero.Ember)=="Sunburst","English weapon and ability names");
                I18n.Language=GameLanguage.Russian;Check(GameData.Weapons[0].name=="Лунный топор"&&GameData.Q(Hero.Ember)=="Солнечный разлом","Russian weapon and ability names switch without restarting");
                foreach(var item in GameData.Items)Check(!string.IsNullOrEmpty(item.name)&&item.description.Contains(" "),"localized chest item content exists");
                foreach(var weapon in GameData.Weapons)Check(weapon.description!=null&&weapon.description.Contains(" "),"localized weapon description exists");
                Check(I18n.T("Both companions must press Ready.")=="Оба игрока должны нажать «Готов».","lobby status translated locally");
            }
            finally{I18n.Language=language;}
        }
        static void LevelPause()
        {
            var sim=New();var p=sim.State.Player(0);p.qCooldown=5;p.weaponTimers[0]=5;
            var enemy=sim.SpawnEnemy(EnemyKind.Brute);enemy.position=p.position+Vector3.forward*9;
            sim.State.pickups.Add(new PickupState{id=700,position=p.position,kind=PickupKind.Gold,value=5});
            sim.GrantExperience(31);float time=sim.State.elapsed;var position=p.position;var enemyPosition=enemy.position;float spawn=sim.State.spawnTimer;
            Check(p.pendingLevels==2&&sim.UpgradePaused,"multiple solo levels queue their own choices");
            var input=new InputFrame{x=1,primary=true};var commands=new Dictionary<int,InputFrame>{{0,input}};Step(sim,2,commands);
            Check(sim.State.elapsed==time&&p.position==position&&enemy.position==enemyPosition,"solo upgrade freezes timer and all movement");
            Check(p.qCooldown==5&&p.weaponTimers[0]==5&&sim.State.spawnTimer==spawn&&sim.State.pickups[0].age==0,"solo upgrade freezes cooldowns, spawns and loot");
            input.choice=8;sim.Step(1f/30,commands);Check(sim.UpgradePaused&&p.pendingLevels==2,"invalid choice does not unpause solo");
            input.choice=0;input.primary=false;sim.Step(1f/30,commands);Check(sim.UpgradePaused&&p.pendingLevels==1&&sim.State.elapsed==time,"first choice keeps next queued level paused");
            sim.Step(1f/30,commands);Check(!sim.UpgradePaused&&p.pendingLevels==0&&sim.State.elapsed>time,"last valid choice resumes simulation");
            sim=New(true);p=sim.State.Player(0);sim.GrantExperience(12);Step(sim,1,new Dictionary<int,InputFrame>{{0,new InputFrame{x=1}}});
            Check(!sim.UpgradePaused&&sim.State.elapsed>.9f&&p.pendingLevels==1,"multiplayer keeps running while players choose");
            sim.State.players.RemoveAt(1);time=sim.State.elapsed;Step(sim,.5f);Check(!sim.UpgradePaused&&sim.State.elapsed>time,"host stays unpaused after companion disconnects");
        }
        static PlayerState Arm(Simulation sim,WeaponKind kind)
        {var p=sim.State.Player(0);p.weapons=new[]{(int)kind,-1};p.weaponLevels=new[]{1,0};p.weaponTimers=new float[2];return p;}
        static EnemyState Target(Simulation sim,PlayerState p,Vector3 offset)
        {var e=sim.SpawnEnemy(EnemyKind.Brute);e.position=p.position+offset;e.frozen=100;e.hp=e.maxHp=500;return e;}
        static void Weapons()
        {
            var sim=New();var p=Arm(sim,WeaponKind.Axe);var a=Target(sim,p,Vector3.forward*2);var b=Target(sim,p,new Vector3(2,0,2));var c=Target(sim,p,Vector3.back*4);
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp==500&&c.hp==500,"one axe swing hits only its primary target");
            float hp=a.hp;Step(sim,.3f);Check(a.hp==hp,"weapon respects its own cooldown");
            sim=New();p=Arm(sim,WeaponKind.Spear);a=Target(sim,p,Vector3.forward*2);b=Target(sim,p,Vector3.forward*6);c=Target(sim,p,new Vector3(3,0,4));
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp==500&&c.hp==500,"one spear thrust cannot pierce another enemy");
            sim=New();p=Arm(sim,WeaponKind.LightningStaff);a=Target(sim,p,Vector3.forward*5);b=Target(sim,p,new Vector3(3,0,5));c=Target(sim,p,new Vector3(6,0,5));
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp==500&&c.hp==500&&sim.State.effects.FindAll(e=>e.kind==10).Count==1,"staff hits one target without its ricochet item");
            sim=New();p=Arm(sim,WeaponKind.Bow);a=Target(sim,p,Vector3.forward*20);sim.Step(1f/30,new Dictionary<int,InputFrame>());
            Check(sim.State.projectiles.Count==1&&a.hp==500,"bow launches a travelling arrow at long range");Step(sim,.65f);Check(a.hp<500,"arrow hits its distant target along swept segment");
            Check(sim.GiveWeapon(p,WeaponKind.Axe)<0&&p.weapons[1]<0,"second weapon cannot be granted before level 25");
            sim.State.level=25;sim.GiveWeapon(p,WeaponKind.Axe);Check(p.weapons.Length==2&&p.weapons[0]==(int)WeaponKind.Bow&&p.weapons[1]==(int)WeaponKind.Axe&&sim.GiveWeapon(p,WeaponKind.Spear)<0,"two slots preserve the character weapon and reject a third weapon");
            int slot=Array.IndexOf(p.weapons,(int)WeaponKind.Axe);sim.GiveWeapon(p,WeaponKind.Axe);Check(p.weaponLevels[slot]==2&&p.weapons.Length==2,"duplicate weapon upgrades existing slot");
            int gold=sim.State.coins;sim.GiveWeapon(p,WeaponKind.Axe,20);Check(p.weaponLevels[slot]==5&&sim.State.coins==gold+17*8,"weapon level cap converts excess duplicates into gold");
        }
        static void Combat()
        {
            var sim = New(); var p = sim.State.Player(0);
            p.weapons=new[]{-1,-1};
            var e = sim.SpawnEnemy(EnemyKind.Brute); e.position = p.position + Vector3.forward * 3;
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [0] = new InputFrame { primary = true } });
            Check(e.hp < e.maxHp && p.qCooldown > 7.9f, "Q damage and cooldown");
            float hp = e.hp;
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [0] = new InputFrame { primary = true } });
            Check(Mathf.Approximately(e.hp, hp), "Q cannot bypass cooldown");
            sim = New(); p = sim.State.Player(0); e = sim.SpawnEnemy(EnemyKind.Wisp); e.position = p.position + Vector3.forward * 4;
            Step(sim, 2); Check(sim.State.kills > 0, "automatic weapon hits and kills");
            sim = New(); p = sim.State.Player(0); p.shield = 25;
            sim.DamagePlayer(p, 15); Check(p.hp == 100 && p.shield == 10, "shield absorbs damage");
            sim.DamagePlayer(p, 100); Check(p.hp == 100, "damage invulnerability interval");
            sim = New(true); var echo = sim.State.Player(1); e = sim.SpawnEnemy(EnemyKind.Brute); e.position = echo.position + Vector3.forward * 2;
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [1] = new InputFrame { primary = true } });
            Check(e.frozen > 2.9f && echo.qCooldown > 9.9f, "Echo freezes enemies");
            sim = New(); p = sim.State.Player(0); p.relic = Relic.HomeShard; p.hp = 20;
            sim.State.pickups.Add(new PickupState { value = 3, position = Vector3.forward * 20 });
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [0] = new InputFrame { relic = true } });
            Check(p.hp == 70 && sim.State.pickups.Count == 0 && sim.State.xp == 3 && p.relicCooldown == 25, "active Home Shard heals and collects XP");
            sim = New(); p = sim.State.Player(0); p.relic = Relic.Hourglass; p.dashCooldown = 2;
            e = sim.SpawnEnemy(EnemyKind.Brute); e.position = p.position + Vector3.forward * 7;
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [0] = new InputFrame { relic = true } });
            Check(e.frozen > 3.9f && p.dashCooldown == 0 && p.relicCooldown == 30, "active Hourglass freezes and resets dash");
        }
        static void Progression()
        {
            var sim = New(true); var a = sim.State.Player(0); var b = sim.State.Player(1);
            sim.GrantExperience(12);
            Check(sim.State.level == 2 && a.pendingLevels == 1 && b.pendingLevels == 1, "shared XP levels both players");
            Check(a.offers.Length == 3 && a.offerRarities.Length==3 && new HashSet<int>(a.offers).Count == 3, "three unique attribute offers carry rarity rolls");
            Check(!sim.ChooseItem(a, 6), "invalid upgrade rejected");
            Check(sim.ChooseItem(a, 0) && a.pendingLevels == 0 && b.pendingLevels == 1, "upgrades are individual");
            float hp=a.maxHp;sim.ApplyItem(a,2);Check(a.maxHp>hp,"stout heart increases maximum health");
            float damage = a.damage; sim.ApplyItem(a,0);Check(Mathf.Abs(a.damage-damage-.075f)<.001f,"sharp blade stacks damage");
            for(int i=0;i<60;i++)sim.ApplyItem(a,9);Check(a.projectileCount==48,"projectile count has a safe cap");
            hp=a.maxHp;sim.ApplyAttribute(a,4,3);Check(Mathf.Abs(a.maxHp-hp-40)<.001f,"legendary health attribute grants forty maximum health");
            float crit=a.critChance;sim.ApplyAttribute(a,2,2);Check(Mathf.Abs(a.critChance-crit-.15f)<.001f,"epic critical chance grants fifteen percent");
            int[] tiers=new int[5];foreach(var item in GameData.Items)tiers[(int)item.rarity]++;Check(GameData.Items.Length==ItemCatalog.Count&&Array.TrueForAll(tiers,n=>n>0),"chest catalogue contains 129 items in five rarity tiers");
            var low=New();var high=New();high.State.Player(0).luck=100;int lowHigh=0,highHigh=0,lowRare=0,highRare=0;for(int i=0;i<800;i++){int lr=low.RollRarity(low.State.Player(0),0),hr=high.RollRarity(high.State.Player(0),0);if(lr>=2)lowHigh++;if(hr>=2)highHigh++;if(lr==1)lowRare++;if(hr==1)highRare++;}
            Check(highHigh>lowHigh&&highRare<lowRare,"luck shifts rolls from low tier toward epic and legendary rewards");
            for(int stat=0;stat<GameData.Attributes.Length;stat++)
            {
                int count=a.attributes[stat],stack=a.accessories[stat*4+2];sim.ApplyAccessory(a,stat,2);
                Check(a.attributes[stat]==count+1&&a.accessories[stat*4+2]==stack+1&&!string.IsNullOrEmpty(GameData.AccessoryName(stat)),"accessory improves stat and records rarity stack "+stat);
            }
            Check(GameData.Attributes.Length==21&&GameData.AccessoryIcons.Length==21,"accessory catalogue covers all stats including gold gain");
        }
        static void AurasAndMilestone()
        {
            var sim=new Simulation(53,true);var lumen=sim.AddPlayer(0,"Lumen",Hero.Lumen);var ally=sim.AddPlayer(1,"Ally",Hero.Ember);var third=sim.AddPlayer(2,"Third",Hero.Echo);
            foreach(var player in sim.State.players)sim.SetReady(player.id,true);sim.Start();sim.State.spawnTimer=1000;
            Check(lumen.weapons.Length==2&&lumen.weapons[0]==(int)WeaponKind.Aura&&lumen.auraLevels[0]==1,"Lumen starts with aura weapon and Fortune aura");
            ally.position=lumen.position+Vector3.right;sim.RefreshAuras();Check(Mathf.Approximately(ally.auraBonuses[0],.15f),"Fortune aura gives nearby allies a fifteen percent rarity-weight bonus");
            sim.LearnAura(lumen,3);sim.RefreshAuras();float hp=ally.maxHp;Check(Mathf.Abs(hp-115)<.01f,"Vitality aura increases maximum HP");
            for(int i=0;i<50;i++)sim.RefreshAuras();Check(Mathf.Abs(ally.maxHp-hp)<.01f,"repeated aura refresh never compounds max health");
            ally.position=lumen.position+Vector3.forward*40;sim.RefreshAuras();Check(ally.auraBonuses[0]==0&&Mathf.Abs(ally.maxHp-100)<.01f,"leaving aura range removes temporary buffs");
            ally.position=lumen.position;sim.RefreshAuras();lumen.downed=true;sim.RefreshAuras();Check(ally.auraBonuses[0]==0&&Mathf.Abs(ally.maxHp-100)<.01f,"downed Lumen cannot buff allies");lumen.downed=false;
            sim.LearnAura(lumen,1);sim.LearnAura(lumen,2);sim.LearnAura(lumen,4);Check(!sim.LearnAura(lumen,5)&&Array.FindAll(lumen.auraLevels,n=>n>0).Length==5,"five-aura limit blocks learning a sixth type");
            int rank=lumen.auraLevels[0];Check(sim.LearnAura(lumen,0)&&lumen.auraLevels[0]==rank+1,"existing auras can always be improved");
            sim.GrantExperience(12);Check(lumen.offerKind==RewardKind.Accessory,"Lumen receives an accessory on each level");sim.ChooseItem(lumen,0);
            Check(lumen.offerKind==RewardKind.Aura&&lumen.pendingLevels==1&&lumen.offers.Length==5,"Lumen also chooses among owned auras once five are learned");sim.ChooseItem(lumen,0);Check(lumen.pendingLevels==0&&lumen.completedLevel==2,"accessory and aura together finish one level");
            int total=0;for(int level=2;level<26;level++)total+=12+(level-1)*7;sim.GrantExperience(total);
            bool milestone=false;int choices=0;while(ally.pendingLevels>0&&choices++<100)
            {
                if(ally.offerKind==RewardKind.Weapon){milestone=ally.completedLevel+1==25;Check(ally.offers.Length==4&&Array.IndexOf(ally.offers,ally.weapons[0])<0,"level 25 offers four distinct non-starter weapons including sword");}
                sim.ChooseItem(ally,0);
            }
            Check(milestone&&ally.weapons[1]>=0&&ally.completedLevel==26,"queued levels preserve the level-25 weapon milestone");
            int accessoryCount=0;foreach(int count in ally.attributes)accessoryCount+=count;Check(accessoryCount==25,"weapon selection does not replace the level-25 accessory");
            third.hero=Hero.Lumen;third.weapons=new[]{(int)WeaponKind.Aura,-1};third.weaponLevels=new[]{1,0};third.auraLevels[0]=1;third.position=lumen.position;
            sim.RefreshAuras();float bonus=third.auraBonuses[0];Check(Mathf.Approximately(bonus,GameData.AuraPower(0,lumen.auraLevels[0])),"same aura from multiple sources uses strongest rank without stacking");
            sim.State.players.Remove(lumen);sim.RefreshAuras();Check(Mathf.Approximately(third.auraBonuses[0],.15f),"disconnect removes only the departed aura source");
            var target=sim.SpawnEnemy();target.hp=1;sim.DamageEnemy(target,ally,100);Check(sim.State.hits.Exists(h=>h.target==target.id&&h.damage>=100)&&!sim.State.enemies.Contains(target),"lethal hits retain damage numbers after enemy removal");
            var fractions=New();var owner=fractions.State.Player(0);owner.xpGain=1.08f;owner.goldGain=1.08f;
            for(int i=0;i<10;i++){var enemy=fractions.SpawnEnemy(EnemyKind.Wisp);fractions.DamageEnemy(enemy,owner,9999);}
            int xp=0,gold=fractions.State.earnedCoins;foreach(var pickup in fractions.State.pickups){if(pickup.kind==PickupKind.Experience)xp+=pickup.value;else gold+=pickup.value;}
            Check(xp==64&&gold==43,"small XP and gold bonuses accumulate instead of disappearing to per-kill rounding");
        }
        static void Cooperation()
        {
            var sim = New(true); var a = sim.State.Player(0); var b = sim.State.Player(1);
            Check(sim.AddPlayer(2, "Third", Hero.Lumen) != null&&sim.AddPlayer(3,"Fourth",Hero.Ember)==null, "three-player limit");
            a.hp = 20; b.hp = 30;
            sim.Step(1f / 30, new Dictionary<int, InputFrame> { [1] = new InputFrame { secondary = true } });
            Check(a.hp == 55 && b.hp == 65 && a.shield > 24, "Echo heals and shields both players");
            a.invulnerability = 0; a.shield = 0; sim.DamagePlayer(a, 1000);
            Check(a.downed && sim.State.phase == RunPhase.Playing, "one downed player does not end co-op");
            b.position = a.position + Vector3.right;
            Step(sim, 2.6f, new Dictionary<int, InputFrame> { [1] = new InputFrame { revive = true } });
            Check(!a.downed && a.hp > 0 && a.invulnerability > 0, "hold F revives teammate");
            a.invulnerability = 0; sim.DamagePlayer(a, 1000); b.position = Vector3.forward * 20;
            Step(sim, 25.2f); Check(a.eliminated, "bleedout expires");
        }
        static void Campaign()
        {
            var sim=New(true);var a=sim.State.Player(0);var b=sim.State.Player(1);var c=sim.AddPlayer(2,"Lumen",Hero.Lumen);
            float previousHp=0;int serial=0;
            for(int stage=1;stage<=7;stage++)
            {
                WorldMap.SetStage(stage);float low=999,high=-999;
                for(int x=-150;x<=150;x+=30)for(int z=-150;z<=150;z+=30){float h=WorldMap.Height(x,z);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
                Check(high-low>5&&Mathf.Abs(WorldMap.Height(0,0))<.001f,"stage has traversable relief and a flat arrival clearing: "+stage);
                sim.State.spawnTimer=1000;var boss=sim.SpawnEnemy(EnemyKind.Warden);
                Check(boss.bossStage==stage&&boss.maxHp>previousHp,"boss appearance ID and health increase on stage "+stage);previousHp=boss.maxHp;
                boss.position=WorldMap.OnGround(0,10);boss.specialTimer=0;boss.frozen=0;boss.spawnGrace=0;sim.Step(1f/30,new Dictionary<int,InputFrame>());
                Check(sim.State.hazards.Count>0&&sim.State.hazards.TrueForAll(h=>h.delay>0&&h.style==stage),"boss attacks have stage-specific telegraphed hazards: "+stage);
                var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));Check(copy.stage==stage&&copy.hazards.Count==sim.State.hazards.Count,"stage and boss hazards survive snapshot: "+stage);
                sim.DamageEnemy(boss,a,999999);
                if(stage==7){Check(sim.State.phase==RunPhase.Victory&&!sim.State.portal.active,"only the seventh boss completes the campaign");break;}
                Check(sim.State.phase==RunPhase.Playing&&sim.State.portal.active&&sim.State.enemies.Count==0,"boss defeat opens a safe portal instead of ending the run: "+stage);
                var far=b.position;b.position=sim.State.portal.position+Vector3.right*20;Check(!sim.TryUsePortal(b)&&sim.State.stage==stage,"portal requires a nearby living player");
                b.position=sim.State.portal.position;c.downed=true;c.hp=0;
                int level=sim.State.level,weapon=a.weapons[0],accessories=a.attributes[0];sim.ApplyAccessory(a,0,0);
                Check(sim.TryUsePortal(b)&&sim.State.stage==stage+1&&sim.State.stageSerial==++serial,"any ally can move the party through a portal: "+stage);
                Check(sim.State.level==level&&a.weapons[0]==weapon&&a.attributes[0]==accessories+1&&!c.downed&&!c.eliminated&&c.hp>0,"party transition preserves builds and restores fallen allies");
                Check(sim.State.players.TrueForAll(p=>Vector3.Distance(p.position,Vector3.zero)<4)&&sim.State.chests.Count==GameData.ChestCount&&sim.State.shrines.Count==12&&sim.State.stageElapsed==0&&!sim.State.bossSpawned,"new stage resets enemies, treasure, clock and party positions");
                Check(!sim.TryUsePortal(b),"one interaction cannot skip two stages");
            }
            WorldMap.SetStage(1);
            var damage=new Simulation(47,true);var lumen=damage.AddPlayer(0,"Lumen",Hero.Lumen);var ally=damage.AddPlayer(1,"Ally",Hero.Ember);ally.weapons=new[]{-1,-1};damage.SetReady(0,true);damage.SetReady(1,true);damage.Start();damage.State.spawnTimer=1000;lumen.critChance=0;
            Check(Mathf.Abs(Simulation.AuraRadius(lumen)-12/1.3f)<.001f,"Lumen radius is exactly 1.3 times smaller");
            var near=damage.SpawnEnemy(EnemyKind.Brute);near.position=lumen.position+Vector3.forward*5;near.hp=near.maxHp=1000;near.frozen=10;
            var outside=damage.SpawnEnemy(EnemyKind.Brute);outside.position=lumen.position+Vector3.forward*11;outside.hp=outside.maxHp=1000;outside.frozen=10;
            Step(damage,1.02f);Check(Mathf.Abs(near.hp-990)<.01f&&outside.hp==1000,"Lumen aura deals ten base damage per second only in its reduced radius");
            damage.GrantExperience(12);damage.ChooseItem(lumen,0);Check(lumen.offerKind==RewardKind.Aura&&lumen.offers.Length==8&&Array.IndexOf(lumen.offers,0)>=0,"each Lumen level can upgrade the current aura or add a new type");
        }
        static void Endings()
        {
            var sim = New(); sim.DamagePlayer(sim.State.Player(0), 999); Step(sim, 0.05f);
            Check(sim.State.phase == RunPhase.Defeat, "solo death ends run");
            sim = New(); sim.State.stageElapsed = GameData.BossTime-.01f; Step(sim, 0.05f);
            var boss = sim.State.enemies.Find(e => e.kind == EnemyKind.Warden);
            Check(boss==null&&!sim.State.bossSpawned,"timer never summons a boss automatically");sim.State.Player(0).position=sim.State.bossStatuePosition;Check(sim.TrySummonBoss(sim.State.Player(0)),"stone statue summons boss on interaction");boss=sim.State.enemies.Find(e=>e.kind==EnemyKind.Warden);
            sim.DamageEnemy(boss, sim.State.Player(0), 999999);
            Check(sim.State.phase == RunPhase.Playing&&sim.State.portal.active, "first boss kill opens the next-realm portal");
        }
        static void Serialization()
        {
            var sim = New(true); sim.GrantExperience(12); sim.SpawnEnemy();sim.State.coins=100;sim.State.Player(0).position=sim.State.chests[0].position;sim.TryOpenChest(sim.State.Player(0));sim.State.level=25;sim.GiveWeapon(sim.State.Player(0),WeaponKind.Spear);
            var copy = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            Check(copy.players.Count == 2 && copy.enemies.Count == 1 && copy.Player(1).offers.Length == 3 && copy.level == 25, "network snapshot roundtrip");
            Check(copy.multiplayer&&copy.loot.Count==1&&Array.IndexOf(copy.Player(0).weapons,(int)WeaponKind.Spear)>=0&&copy.Player(0).weaponLevels.Length==2&&copy.Player(0).accessories.Length==84&&copy.Player(0).auraLevels.Length==8,"snapshot carries accessory stacks, aura ranks, floor loot and two weapon slots");
        }
        static bool Wait(Func<bool> ready, int milliseconds = 2500)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { if (ready()) return true; Thread.Sleep(10); }
            return false;
        }
        static void Network()
        {
            using var host = new CoopTransport(); using var client = new CoopTransport();using var second=new CoopTransport();
            host.Host(17777); client.Join("127.0.0.1", 17777);second.Join("127.0.0.1",17777);
            Check(Wait(() => host.PeerCount==2 && client.Connected&&second.Connected), "three-player loopback connections");
            string payload = new string('Ж', 50000); bool delivered = false;
            client.Send(payload);
            Check(Wait(() => { while (host.TryDequeue(out var e)) if (e.kind == "message") delivered |= e.text == payload; return delivered; }), "UTF8 frame survives TCP fragmentation");
            delivered = false; host.Send("snapshot", true);
            Check(Wait(() => { while (client.TryDequeue(out var e)) if (e.kind == "message") delivered |= e.text == "snapshot"; return delivered; }), "server snapshots reach client");
            bool other=false;Check(Wait(()=>{while(second.TryDequeue(out var e))if(e.kind=="message")other|=e.text=="snapshot";return other;}),"broadcast reaches the second guest too");
            int peer=-1;second.Send("identify");Check(Wait(()=>{while(host.TryDequeue(out var e))if(e.kind=="message"&&e.text=="identify")peer=e.peer;return peer>=0;}),"messages preserve the sending guest identity");
            host.SendTo(peer,"private");other=false;Check(Wait(()=>{while(second.TryDequeue(out var e))if(e.kind=="message")other|=e.text=="private";return other;}),"addressed messages reach their guest");
            bool leaked=false;while(client.TryDequeue(out var evt))if(evt.text=="private")leaked=true;Check(!leaked,"private guest messages do not leak to another player");
            client.Dispose();
            Check(Wait(() => host.PeerCount==1&&second.Connected), "disconnect leaves the other guest connected");
            second.Dispose();Check(Wait(()=>!host.Connected),"last guest disconnect detected");
        }
    }
}
