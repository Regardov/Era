using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era.Editor
{
    public static class RoguelikeChecks
    {
        static int count;
        static void Check(bool ok,string label){count++;if(!ok)throw new Exception("ERA roguelike check failed: "+label);}
        static Simulation New()
        {var sim=new Simulation(173);var p=sim.AddPlayer(0,"Builder",Hero.Ember);p.critChance=0;p.weapons=new[]{-1,-1};sim.Start();sim.State.spawnTimer=10000;sim.State.resourceNodes.Clear();return sim;}
        static void Step(Simulation s,float seconds)
        {for(int n=0;n<Mathf.CeilToInt(seconds*30);n++)s.Step(1f/30,new Dictionary<int,InputFrame>());}
        static EnemyState Target(Simulation s,float z=5,EnemyKind kind=EnemyKind.Skeleton)
        {var e=s.SpawnEnemy(kind);e.position=s.State.Player(0).position+Vector3.forward*z;e.frozen=100;e.spawnGrace=0;e.hp=e.maxHp=10000;return e;}
        static void Give(Simulation s,params ItemId[] items){foreach(var i in items)s.ApplyItem(s.State.Player(0),(int)i);}
        public static int Run()
        {
            count=0;Catalogue();Luck();Volleys();Statuses();Builds();EnemyRoles();Snapshots();return count;
        }
        static void Catalogue()
        {
            Check(GameData.Items.Length==ItemCatalog.Count&&Enum.GetValues(typeof(ItemId)).Length==ItemCatalog.Count,"stable 130 item IDs");
            var language=I18n.Language;
            foreach(GameLanguage lang in Enum.GetValues(typeof(GameLanguage)))
            {
                I18n.Language=lang;var names=new HashSet<string>();var descriptions=new HashSet<string>();
                for(int n=0;n<GameData.Items.Length;n++)
                {var d=GameData.Items[n];Check(names.Add(d.name)&&descriptions.Add(d.description)&&d.description.Length>3&&IconLibrary.ItemSprite(n)!=null,"unique localized item and usable icon "+lang+" / "+n);}
            }
            I18n.Language=language;
            foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))if(kind!=EnemyKind.Warden)
            {
                var model=Resources.Load<GameObject>("Creatures/"+kind);Check(model!=null&&model.GetComponent<CreatureMotion>()!=null,"runtime creature prefab "+kind);
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())if(filter.name=="Batched faceted surfaces")Check(Array.TrueForAll(filter.sharedMesh.normals,n=>n.sqrMagnitude>.5f),"valid lit mesh normals "+kind+" / "+filter.transform.parent.name);
            }
        }
        static void Luck()
        {
            foreach(float luck in new[]{0,25,100,250})
            {
                var actual=ItemCatalog.RarityProbabilities(luck);double s=1.5*Math.Log(1+luck/100.0),total=0;double[] basis={56,27,12,4,1};var expected=new double[5];
                for(int rank=0;rank<5;rank++){expected[rank]=basis[rank]*Math.Pow(1.5,-(3-(rank+1))*s);total+=expected[rank];}
                for(int rank=0;rank<5;rank++)Check(Math.Abs(actual[ItemCatalog.RarityOrder[rank]]-expected[rank]/total)<.00001,"screenshot luck formula "+luck+" / "+rank);
            }
            var normal=ItemCatalog.RarityProbabilities(0);var high=ItemCatalog.RarityProbabilities(100);var moon=ItemCatalog.RarityProbabilities(0,1);
            Check(high[3]>normal[3]&&high[1]<normal[1]&&moon[4]>normal[4],"luck and moon chests improve upper-tier probabilities");
            var sim=New();for(int n=0;n<100;n++)Check(sim.RollRarity(sim.State.Player(0),0,false)!=4,"stat accessories never use mythic mechanics tier");
        }
        static void Volleys()
        {
            Check(Mathf.Approximately(ItemCatalog.BurstInterval(2),.75f)&&Mathf.Approximately(ItemCatalog.BurstInterval(3),.5f)&&Mathf.Approximately(ItemCatalog.BurstInterval(4),1f/3),"requested geometric burst interval");
            var s=New();var p=s.State.Player(0);Give(s,ItemId.DoubleShot,ItemId.DoubleShot);p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;Target(s,20);Step(s,.033f);
            Check(p.projectileCount==3&&s.State.projectiles.Count==3,"double-shot stacks 1+1+1 against a single target");
            s=New();p=s.State.Player(0);p.projectileCount=3;p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;
            for(int n=0;n<3;n++){var e=Target(s,18);e.position+=Vector3.right*n*3;}Step(s,.033f);
            var targets=new HashSet<int>();foreach(var b in s.State.projectiles)targets.Add(b.target);Check(targets.Count==3,"homing volley prefers three different enemies");
            s=New();p=s.State.Player(0);p.projectileCount=3;p.weapons[0]=(int)WeaponKind.LightningStaff;p.weaponLevels[0]=1;Target(s,8);Step(s,.033f);
            Check(p.weaponCastSerial==3&&p.bursts.Count==0&&s.State.effects.FindAll(e=>e.kind==10).Count==3,"staff releases all three shots simultaneously even against one target");
            foreach(var kind in new[]{WeaponKind.Spear,WeaponKind.Sword})
            {
                s=New();p=s.State.Player(0);p.weapons[0]=(int)kind;p.weaponLevels[0]=1;p.projectileCount=2;Target(s,3);Step(s,.033f);
                Check(p.weaponCastSerial==1&&p.bursts.Count==1,"first melee projectile and queued followup "+kind);Step(s,.6f);Check(p.weaponCastSerial==1,"melee followup waits 0.75 s "+kind);Step(s,.17f);Check(p.weaponCastSerial==2,"second melee projectile fires after interval "+kind);
            }
        }
        static void Statuses()
        {
            var s=New();var p=s.State.Player(0);var e=Target(s);Give(s,ItemId.FlameBlade);s.DamageEnemy(e,p,10);float hp=e.hp;
            Check(e.ailments.Exists(a=>a.kind==Ailment.Burn)&&Mathf.Approximately(hp,9990),"fire applies burn without a hidden flat damage buff");Step(s,.6f);Check(e.hp<hp,"burn deals periodic damage");
            s=New();p=s.State.Player(0);e=Target(s);Give(s,ItemId.FlameBlade,ItemId.IceCore,ItemId.SteamValve);s.DamageEnemy(e,p,10);
            Check(e.hp<=9930&&!e.ailments.Exists(a=>a.kind==Ailment.Burn||a.kind==Ailment.Chill),"fire plus frost consumes statuses for steam explosion");
            s=New();p=s.State.Player(0);e=Target(s);Give(s,ItemId.VenomNeedle,ItemId.SerratedCoin);p.critChance=1;s.DamageEnemy(e,p,10);
            Check(e.ailments.Exists(a=>a.kind==Ailment.Poison)&&e.ailments.Exists(a=>a.kind==Ailment.Bleed),"poison and critical bleeding coexist");
            s=New();p=s.State.Player(0);e=Target(s);Give(s,ItemId.TimeMaster);s.DamageEnemy(e,p,100,true,true);Check(e.hp==9900&&s.State.itemFields.Count==1,"time echo is a delayed entity");Step(s,1.1f);Check(Mathf.Abs(e.hp-9865)<.01f&&s.State.itemFields.Count==0,"echo repeats 35 percent once without recursion");
            s=New();p=s.State.Player(0);e=Target(s);Give(s,ItemId.IceCore,ItemId.GlacialPrison);e.frozen=0;for(int n=0;n<3;n++)s.DamageEnemy(e,p,1);Check(e.frozen>=2,"three chilled hits produce an actual freeze");
        }
        static void Builds()
        {
            var s=New();var p=s.State.Player(0);Give(s,ItemId.InversionHeart);p.hp=50;p.shield=0;s.Heal(p,25);Check(p.hp==50&&p.shield==25,"inversion turns healing into shield");
            p.shield=100;var e=Target(s);s.Step(1f/30,new Dictionary<int,InputFrame>{{0,new InputFrame{primary=true}}});Check(p.shield<1&&e.hp<9900,"inversion consumes shield for an ability nova");
            s=New();p=s.State.Player(0);Give(s,ItemId.TemporalDebt);s.DamagePlayer(p,20);Check(p.hp==90&&p.itemValues[(int)ItemId.TemporalDebt]==10,"temporal debt delays half of incoming health damage");Step(s,3.1f);Check(p.hp==80,"unpaid temporal debt settles after three seconds");
            s=New();p=s.State.Player(0);Give(s,ItemId.TemporalDebt);s.DamagePlayer(p,20);e=Target(s);s.DamageEnemy(e,p,20000);Step(s,3.1f);Check(p.hp==88,"kill repays eight points of temporal debt");
            s=New();p=s.State.Player(0);Give(s,ItemId.PacifistEngine);p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;Target(s,20);var input=new Dictionary<int,InputFrame>{{0,new InputFrame{x=1}}};for(int n=0;n<40;n++)s.Step(1f/30,input);
            Check(s.State.projectiles.Count==0&&s.State.itemFields.Exists(f=>f.item==(int)ItemId.PacifistEngine),"pacifist changes weapons into movement mines");
            s=New();p=s.State.Player(0);Give(s,ItemId.Chronicle);p.safeTime=44.95f;Step(s,.1f);Check(p.projectileCount==2,"no-hit chronicle earns a permanent projectile");s.DamagePlayer(p,1);Check(p.safeTime==0&&p.projectileCount==2,"damage resets the next milestone, retaining earned projectiles");
            s=New();p=s.State.Player(0);Give(s,ItemId.InfinitePower,ItemId.WorldSeed);for(int n=0;n<25;n++){e=Target(s,20);s.DamageEnemy(e,p,20000);}
            Check(p.damage>1&&p.maxHp==102&&p.areaSize>1&&s.State.itemFields.Exists(f=>f.item==(int)ItemId.WorldSeed),"legendary kill milestones grow damage, health and a real root field");
            s=New();p=s.State.Player(0);Give(s,ItemId.ImmortalContract);s.DamagePlayer(p,200);Check(!p.downed&&p.maxHp==80&&p.hp==80&&p.damage>1.39f,"immortal contract trades max health for revival and permanent growth");
            s=New();p=s.State.Player(0);Give(s,ItemId.EventHorizon);s.State.projectiles.Add(new ProjectileState{id=s.State.nextId++,owner=-1,hostile=true,position=p.position+Vector3.up+Vector3.forward*3,direction=Vector3.back,damage=20,speed=10});Step(s,.1f);Check(s.State.projectiles.Count==0&&p.itemValues[(int)ItemId.EventHorizon]==20&&p.hp==100,"event horizon absorbs hostile projectiles into stored power");
        }
        static void EnemyRoles()
        {
            foreach(var kind in new[]{EnemyKind.SkeletonArcher,EnemyKind.AcidSlime,EnemyKind.CrystalGolem,EnemyKind.OgreShaman})
            {
                var s=New();var e=Target(s,12,kind);e.frozen=0;e.attackTimer=0;e.specialTimer=100;Step(s,.033f);Check(e.meleeWindup>0,"ranged attack has a visible windup "+kind);Step(s,.8f);Check(s.State.projectiles.Exists(b=>b.hostile),"ranged role launches damaging projectiles "+kind);
            }
            var sim=New();var p=sim.State.Player(0);var necro=Target(sim,12,EnemyKind.Necromancer);necro.frozen=0;necro.specialTimer=0;Step(sim,1.3f);Check(sim.State.enemies.FindAll(e=>e.kind==EnemyKind.Skeleton&&e.summonGeneration==1).Count==3,"necromancer completes summon animation before creating minions");
            sim=New();p=sim.State.Player(0);var slime=Target(sim,10,EnemyKind.Slime);sim.DamageEnemy(slime,p,20000);Check(sim.State.enemies.Count==2&&sim.State.enemies.TrueForAll(e=>e.summonGeneration==1),"slime divides into two bounded children");var child=sim.State.enemies[0];sim.DamageEnemy(child,p,20000);Check(sim.State.enemies.Count==1,"slime children do not divide recursively");
            sim=New();p=sim.State.Player(0);var stone=Target(sim,10,EnemyKind.StoneGolem);sim.DamageEnemy(stone,p,100,false);Check(Mathf.Approximately(stone.hp,9928),"stone tank has actual incoming damage reduction");
            var bat=sim.SpawnEnemy(EnemyKind.Bat);Check(bat.position.y-WorldMap.Ground(bat.position)>1.5f,"flying enemies have elevated combat positions");
            sim=New();p=sim.State.Player(0);bat=sim.SpawnEnemy(EnemyKind.Bat);bat.position=p.position+Vector3.forward;bat.position.y=WorldMap.Ground(bat.position)+1.8f;bat.spawnGrace=0;bat.attackTimer=0;Step(sim,1);Check(p.hp<100,"flying bat dives to a height that can damage a grounded player");
            sim=New();p=sim.State.Player(0);sim.State.projectiles.Add(new ProjectileState{id=sim.State.nextId++,owner=-1,hostile=true,position=p.position+Vector3.up*.95f,direction=Vector3.forward,speed=1,damage=17});Step(sim,.033f);Check(p.hp==83,"hostile swept projectile hitbox damages player");
        }
        static void Snapshots()
        {
            var sim=New();var p=sim.State.Player(0);Give(sim,ItemId.DoubleShot,ItemId.TimeMaster,ItemId.FlameBlade);var e=Target(sim);sim.DamageEnemy(e,p,20,true,true);
            p.bursts.Add(new WeaponBurstState{weapon=WeaponKind.Sword,remaining=2,interval=.5f,usedTargets=new List<int>{e.id}});
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            Check(copy.Player(0).items.Length==130&&copy.Player(0).itemTimers.Length==130&&copy.itemFields.Count==1&&copy.enemies[0].ailments.Count==1&&copy.Player(0).bursts[0].usedTargets[0]==e.id,"co-op snapshots retain items, status ownership, delayed procs and queued shots");
            Check(System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(sim.State))<512*1024,"normal roguelike snapshot fits transport frame");
            for(int id=1;id<3;id++)sim.AddPlayer(id,"Party",Hero.Echo);
            sim.State.enemies.Clear();for(int n=0;n<GameData.MaxEnemies;n++)
            {var enemy=sim.SpawnEnemy(EnemyKind.Skeleton);foreach(var owner in sim.State.players)foreach(Ailment kind in Enum.GetValues(typeof(Ailment)))enemy.ailments.Add(new AfflictionState{kind=kind,owner=owner.id,time=10,power=20});}
            for(int n=0;n<GameData.MaxProjectiles;n++)
            {var bolt=new ProjectileState{id=sim.State.nextId++,owner=n%3,life=2,damage=100};foreach(var enemy in sim.State.enemies)bolt.hitTargets.Add(enemy.id);sim.State.projectiles.Add(bolt);}
            for(int n=sim.State.itemFields.Count;n<210;n++)sim.State.itemFields.Add(new ItemFieldState{id=sim.State.nextId++,owner=n%3,item=(int)ItemId.HiveMind,time=10,power=100});
            for(int n=0;n<GameData.MaxEffects;n++)sim.State.effects.Add(new EffectState{id=sim.State.nextId++,life=.5f,radius=10});
            Check(System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(sim.State))<1024*1024,"full party snapshot with capped projectiles, every status and proc fields fits protocol 11 frame");
            sim=New();p=sim.State.Player(0);p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;p.projectileCount=3;Target(sim,20);
            for(int n=0;n<GameData.MaxProjectiles-1;n++)sim.State.projectiles.Add(new ProjectileState{id=sim.State.nextId++,position=Vector3.one*100,direction=Vector3.right,life=10});
            Step(sim,.033f);Check(p.weaponCastSerial==0,"full projectile pool defers the whole volley without consuming individual shots");sim.State.projectiles.Clear();Step(sim,.1f);Check(sim.State.projectiles.Count==3,"deferred volley retains its complete projectile count");
        }
    }
}
