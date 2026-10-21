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
            sim.Start(); sim.State.spawnTimer = 1000; return sim;
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
            Readiness();Movement();Jumping();Crouching();Shrines();Combat();Weapons();Progression();LevelPause();Economy();Cooperation();Endings();Serialization();Presentation();Localization();Network();
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
            Check(sim.State.chests.Count==30,"large map contains thirty chests");int money=sim.State.coins;
            Check(sim.TryOpenChest(a)&&sim.State.coins==money-chest.cost&&chest.owner==0,"opening chest atomically spends shared gold");
            b.position=chest.position;int after=sim.State.coins;Check(!sim.TryOpenChest(b)&&sim.State.coins==after,"opened chest cannot be bought twice");
            Check(chest.rewardKind==RewardKind.Weapon&&sim.State.loot.Count==1&&Array.IndexOf(a.weapons,chest.reward)<0,"chest reward starts as visible loot before equipping");
            Step(sim,.4f);Check(sim.State.loot.Count==1&&sim.State.loot[0].position.y>chest.position.y+1,"reward rises out of chest");
            Step(sim,1.3f);Check(sim.State.loot.Count==0&&Array.IndexOf(a.weapons,chest.reward)>=0&&Array.IndexOf(b.weapons,chest.reward)<0,"animated reward equips only on opener after flying to them");
            var second=sim.State.chests[1];a.position=second.position;Check(!sim.TryOpenChest(a)&&!second.opened,"insufficient gold leaves chest closed");
            a.position=Vector3.zero;sim.State.pickups.Add(new PickupState{id=500,value=7,kind=PickupKind.Gold,position=Vector3.zero});Step(sim,.4f);
            Check(sim.State.coins==after+7&&sim.State.earnedCoins==7,"gold pickups credit team wallet");
            sim.State.pickups.Add(new PickupState{id=501,value=8,kind=PickupKind.Gold,position=Vector3.forward*30});
            sim.State.pickups.Add(new PickupState{id=502,value=3,kind=PickupKind.Experience,position=Vector3.forward*30});
            int xp=sim.State.xp;sim.ApplyItem(a,4);Check(sim.State.xp==xp+3&&sim.State.pickups.Count==1&&sim.State.pickups[0].kind==PickupKind.Gold,"experience magnet does not convert gold into XP");
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
                {var creature=CreatureMotion.Create(v,parent.transform,kind);Check(creature.limbs.Count>=4,"organic creature model has animated limbs: "+kind);}
            }
            finally{UnityEngine.Object.DestroyImmediate(parent);}
            foreach(string character in new[]{"Characters/Ember","Characters/Echo","Creatures/Wisp","Creatures/Brute","Creatures/Stalker","Creatures/Warden"})
            {
                var model=Resources.Load<GameObject>(character);var skeletal=model.GetComponent<SkeletalActor>();
                Check(skeletal!=null&&skeletal.animationPlayer.GetClipCount()>=15&&model.GetComponentsInChildren<SkinnedMeshRenderer>().Length>=6,"textured skeletal actor has real animation clips: "+character);
                Check(model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.mainTexture!=null,"painted atlas is assigned: "+character);
            }
            foreach(string name in IconBuilder.Names){var icon=Resources.Load<Texture2D>("Icons/"+name);Check(icon!=null&&icon.width==128,"transparent interface icon included: "+name);}
            var animated=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/Ember"));
            try
            {
                var skeletal=animated.GetComponent<SkeletalActor>();var clip=skeletal.animationPlayer.GetClip("Running_A");clip.SampleAnimation(animated,.05f);var left=skeletal.leftThigh.localRotation;clip.SampleAnimation(animated,.30f);
                Check(Quaternion.Angle(left,skeletal.leftThigh.localRotation)>5,"imported running clip actually articulates skeleton joints");
            }
            finally{UnityEngine.Object.DestroyImmediate(animated);}
        }
        static void Localization()
        {
            var language=I18n.Language;
            try
            {
                I18n.Language=GameLanguage.English;Check(GameData.Weapons[0].name=="Crescent Axe"&&GameData.Q(Hero.Ember)=="Sunburst","English weapon and ability names");
                I18n.Language=GameLanguage.Russian;Check(GameData.Weapons[0].name=="Лунный топор"&&GameData.Q(Hero.Ember)=="Солнечный разлом","Russian weapon and ability names switch without restarting");
                foreach(var item in GameData.Items)Check(!string.IsNullOrEmpty(item.name)&&item.description.Contains(" "),"localized memory content exists");
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
        {var p=sim.State.Player(0);p.weapons=new[]{(int)kind,-1,-1,-1};p.weaponLevels=new[]{1,0,0,0};p.weaponTimers=new float[4];return p;}
        static EnemyState Target(Simulation sim,PlayerState p,Vector3 offset)
        {var e=sim.SpawnEnemy(EnemyKind.Brute);e.position=p.position+offset;e.frozen=100;e.hp=e.maxHp=500;return e;}
        static void Weapons()
        {
            var sim=New();var p=Arm(sim,WeaponKind.Axe);var a=Target(sim,p,Vector3.forward*2);var b=Target(sim,p,new Vector3(2,0,2));var c=Target(sim,p,Vector3.back*4);
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp<500&&c.hp==500,"axe cleaves a forward arc and leaves targets behind untouched");
            float hp=a.hp;Step(sim,.3f);Check(a.hp==hp,"weapon respects its own cooldown");
            sim=New();p=Arm(sim,WeaponKind.Spear);a=Target(sim,p,Vector3.forward*2);b=Target(sim,p,Vector3.forward*6);c=Target(sim,p,new Vector3(3,0,4));
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp<500&&c.hp==500,"spear pierces multiple enemies on a line");
            sim=New();p=Arm(sim,WeaponKind.LightningStaff);a=Target(sim,p,Vector3.forward*5);b=Target(sim,p,new Vector3(3,0,5));c=Target(sim,p,new Vector3(6,0,5));
            sim.Step(1f/30,new Dictionary<int,InputFrame>());Check(a.hp<500&&b.hp<500&&c.hp<500&&sim.State.effects.FindAll(e=>e.kind==10).Count==3,"staff chains lightning between three distinct targets");
            sim=New();p=Arm(sim,WeaponKind.Bow);a=Target(sim,p,Vector3.forward*20);sim.Step(1f/30,new Dictionary<int,InputFrame>());
            Check(sim.State.projectiles.Count==1&&a.hp==500,"bow launches a travelling arrow at long range");Step(sim,.65f);Check(a.hp<500,"arrow hits its distant target along swept segment");
            for(int i=0;i<4;i++)sim.GiveWeapon(p,(WeaponKind)i);Check(new HashSet<int>(p.weapons).Count==4&&p.weapons.Length==4,"all four weapon types occupy exactly four slots");
            int slot=Array.IndexOf(p.weapons,(int)WeaponKind.Axe);sim.GiveWeapon(p,WeaponKind.Axe);Check(p.weaponLevels[slot]==2&&p.weapons.Length==4,"duplicate weapon upgrades existing slot");
            int gold=sim.State.coins;sim.GiveWeapon(p,WeaponKind.Axe,20);Check(p.weaponLevels[slot]==5&&sim.State.coins==gold+17*8,"weapon level cap converts excess duplicates into gold");
        }
        static void Combat()
        {
            var sim = New(); var p = sim.State.Player(0);
            p.weapons=new[]{-1,-1,-1,-1};
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
            Check(a.offers.Length == 3 && new HashSet<int>(a.offers).Count == 3, "three unique upgrade offers");
            Check(!sim.ChooseItem(a, 6), "invalid upgrade rejected");
            Check(sim.ChooseItem(a, 0) && a.pendingLevels == 0 && b.pendingLevels == 1, "upgrades are individual");
            sim.ApplyItem(a, 0); Check(a.maxHp >= 125, "health item");
            float damage = a.damage; sim.ApplyItem(a, 1); Check(Mathf.Abs(a.damage - damage - 0.2f) < 0.001f, "damage item stacks");
            for (int i = 0; i < 10; i++) sim.ApplyItem(a, 5);
            Check(a.projectileCount == 5, "projectile cap");
            sim.State.pickups.Add(new PickupState { value = 4 }); int xp = sim.State.xp;
            sim.ApplyItem(a, 4); Check(sim.State.pickups.Count == 0 && sim.State.xp == xp + 4, "lantern grants all arena XP");
        }
        static void Cooperation()
        {
            var sim = New(true); var a = sim.State.Player(0); var b = sim.State.Player(1);
            Check(sim.AddPlayer(2, "Third", Hero.Ember) == null, "two-player limit");
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
        static void Endings()
        {
            var sim = New(); sim.DamagePlayer(sim.State.Player(0), 999); Step(sim, 0.05f);
            Check(sim.State.phase == RunPhase.Defeat, "solo death ends run");
            sim = New(); sim.State.elapsed = 299.99f; Step(sim, 0.05f);
            var boss = sim.State.enemies.Find(e => e.kind == EnemyKind.Warden);
            Check(boss != null && sim.State.bossSpawned, "boss appears at five minutes");
            sim.DamageEnemy(boss, sim.State.Player(0), 999999);
            Check(sim.State.phase == RunPhase.Victory, "boss kill wins run");
        }
        static void Serialization()
        {
            var sim = New(true); sim.GrantExperience(12); sim.SpawnEnemy();sim.State.coins=100;sim.State.Player(0).position=sim.State.chests[0].position;sim.TryOpenChest(sim.State.Player(0));sim.GiveWeapon(sim.State.Player(0),WeaponKind.Spear);
            var copy = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            Check(copy.players.Count == 2 && copy.enemies.Count == 1 && copy.Player(1).offers.Length == 3 && copy.level == 2, "network snapshot roundtrip");
            Check(copy.multiplayer&&copy.loot.Count==1&&Array.IndexOf(copy.Player(0).weapons,(int)WeaponKind.Spear)>=0&&copy.Player(0).weaponLevels.Length==4,"snapshot carries multiplayer mode, animated rewards and weapon slots");
        }
        static bool Wait(Func<bool> ready, int milliseconds = 2500)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { if (ready()) return true; Thread.Sleep(10); }
            return false;
        }
        static void Network()
        {
            using var host = new CoopTransport(); using var client = new CoopTransport();
            host.Host(17777); client.Join("127.0.0.1", 17777);
            Check(Wait(() => host.Connected && client.Connected), "loopback connection");
            string payload = new string('Ж', 50000); bool delivered = false;
            client.Send(payload);
            Check(Wait(() => { while (host.TryDequeue(out var e)) if (e.kind == "message") delivered |= e.text == payload; return delivered; }), "UTF8 frame survives TCP fragmentation");
            delivered = false; host.Send("snapshot", true);
            Check(Wait(() => { while (client.TryDequeue(out var e)) if (e.kind == "message") delivered |= e.text == "snapshot"; return delivered; }), "server snapshots reach client");
            client.Dispose();
            Check(Wait(() => !host.Connected), "disconnect detected");
        }
    }
}
