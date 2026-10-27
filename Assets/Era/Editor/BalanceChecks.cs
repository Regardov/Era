using System;
using System.Collections.Generic;
using UnityEngine;
namespace Era.Editor
{
    public static class BalanceChecks
    {
        static int count;
        static void Check(bool value,string label){count++;if(!value)throw new Exception("ERA balance check failed: "+label);}
        static Simulation New(){var s=new Simulation(401);var p=s.AddPlayer(0,"Balance",Hero.Ember);p.position=WorldMap.OnGround(0,0);p.weapons=new[]{-1,-1};p.critChance=0;s.Start();s.State.spawnTimer=10000;s.State.resourceNodes.Clear();return s;}
        static EnemyState Target(Simulation s,float z){var e=s.SpawnEnemy(EnemyKind.Skeleton);e.position=s.State.Player(0).position+Vector3.forward*z;e.hp=e.maxHp=1000000;e.frozen=100;return e;}
        static void Step(Simulation s,float time){for(int n=0;n<Mathf.CeilToInt(time*30);n++)s.Step(1f/30,new Dictionary<int,InputFrame>());}
        public static int Run()
        {
            count=0;Economy();Healing();Evasion();Projectiles();Models();CurrentRules();SofterMobsAndHeroDamage();return count;
        }
        static void SofterMobsAndHeroDamage()
        {
            float[] previousHp={44,110,32,44,42,35,125,80,260,150,65,75,230,135,24,100};
            var s=New();foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var enemy=s.SpawnEnemy(kind);float expected=kind==EnemyKind.Warden?CampaignData.BossHealth(1):previousHp[(int)kind]*.92f;
                Check(Mathf.Abs(enemy.maxHp-expected)<.001f,"actual spawned HP reduced eight percent except boss: "+kind);s.State.enemies.Clear();
            }
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                s=new Simulation(675);var p=s.AddPlayer(0,"Damage",hero);p.position=Vector3.zero;p.critChance=0;p.weapons=new[]{(int)WeaponKind.LightningStaff,-1};p.weaponLevels=new[]{1,0};s.State.resourceNodes.Clear();s.Start();s.State.spawnTimer=100000;
                var e=new EnemyState{id=s.State.nextId++,kind=EnemyKind.Wisp,position=Vector3.forward*3,hp=10000,maxHp=10000,frozen=10000,spawnGrace=10000};s.State.enemies.Add(e);
                s.Step(.01f,new Dictionary<int,InputFrame>());float expected=(hero==Hero.Lumen?24:36)*(hero==Hero.Echo?.9f:1);
                Check(Mathf.Abs(10000-e.hp-expected)<.001f,"hero-specific flat weapon damage and Echo multiplier: "+hero);
                p.weapons=new[]{-1,-1};e.hp=10000;s.Step(.01f,new Dictionary<int,InputFrame>{{0,new InputFrame{primary=true}}});
                float q=(hero==Hero.Ember?77:hero==Hero.Echo?47*.9f:45);Check(Mathf.Abs(10000-e.hp-q)<.001f,"Q gains twelve base damage except Lumen: "+hero);
                if(hero==Hero.Lumen)
                {
                    p.weapons=new[]{(int)WeaponKind.Aura,-1};p.weaponLevels=new[]{1,0};p.qCooldown=100;p.auraDamageTimer=0;e.hp=10000;
                    for(int n=0;n<20;n++)s.Step(.05f,new Dictionary<int,InputFrame>());
                    Check(Mathf.Abs(10000-e.hp-10)<.001f,"Lumen aura remains ten base DPS");
                }
            }
        }
        static void Economy()
        {
            int[] expected={25,42,70,117,195,325,542};for(int n=0;n<expected.Length;n++)Check(GameData.ChestPrice(25,n)==expected[n],"recursive screenshot price with upward rounding "+n);
            Check(GameData.ChestPrice(25,int.MaxValue)==int.MaxValue,"geometric prices saturate without integer overflow");
            var s=New();Check(s.State.chests.Count==30,"thirty ordinary-world chests");
            var p=s.State.Player(0);s.State.coins=10000;p.position=s.State.chests[0].position;s.TryOpenChest(p);
            Check(s.State.chestsOpenedInStage==1&&s.State.openedChests==1&&s.State.chests[1].cost==20,"opening updates the shared realm progression");
            var boss=s.SpawnEnemy(EnemyKind.Warden);s.DamageEnemy(boss,p,99999999);p.position=s.State.portal.position;s.TryUsePortal(p);
            Check(s.State.chestsOpenedInStage==0&&s.State.openedChests==1&&s.State.chests.TrueForAll(c=>c.cost==GameData.ChestPrice(c.baseCost,s.State.openedChests)),"travel keeps cumulative prices and total chest statistics");
        }
        static void Healing()
        {
            var s=New();var p=s.State.Player(0);var e=Target(s,5);p.hp=20;p.lifeSteal=1;
            s.DamageEnemy(e,p,10,true,true);Check(p.hp==21,"100 percent vampirism heals one HP instead of a damage fraction");
            s.DamageEnemy(e,p,1000,true,true);Check(p.hp==22,"lifesteal is independent of strike damage");
            s.DamageEnemy(e,p,10,false);s.DamageEnemy(e,p,10,true,false);Check(p.hp==22,"secondary, status and area ability damage do not trigger weapon lifesteal");
            float[] rates={1,1.01f,1.5f,1.5001f,2,2.01f};int[] health={1,2,2,3,3,4};for(int n=0;n<rates.Length;n++){p.lifeSteal=rates[n];float before=p.hp;s.DamageEnemy(e,p,1,true,true);Check(Mathf.Approximately(p.hp-before,health[n]),"lifesteal overheal tier "+rates[n]);}
            p.lifeSteal=.25f;int procs=0;for(int n=0;n<2000;n++){p.hp=20;s.DamageEnemy(e,p,1,true,true);if(p.hp>20)procs++;}Check(procs>420&&procs<580,"quarter chance lifesteal produces a quarter of successful procs");
        }
        static void Evasion()
        {
            Check(Mathf.Approximately(GameData.CombineEvasion(.2f,.2f),.36f),"evasion gain acts on the remaining probability");
            Check(GameData.CombineEvasion(.9f,1)==.95f&&GameData.EvasionChance(5)==.95f,"evasion can never reach 100 percent");
            var s=New();var p=s.State.Player(0);p.dodge=.2f;s.ApplyItem(p,(int)ItemId.WingedBoots);Check(Mathf.Approximately(p.dodge,.24f),"item evasion uses the same diminishing formula");
            p.dodge=1;int hits=0;for(int n=0;n<2000;n++){p.hp=100;p.invulnerability=0;s.DamagePlayer(p,1);if(p.hp<100)hits++;}
            Check(hits>50&&hits<160&&s.State.hits.Exists(h=>h.evaded&&h.player&&h.damage==0),"95 percent evade still takes hits and emits an evade notice");
            p.shield=10;p.dodge=0;p.invulnerability=0;p.hp=50;s.DamagePlayer(p,6);Check(p.shield==4&&p.hp==50,"visible shield is the same authoritative damage absorber");
        }
        static void Projectiles()
        {
            var s=New();var p=s.State.Player(0);var a=Target(s,5);var b=Target(s,7);
            s.State.projectiles.Add(new ProjectileState{id=s.State.nextId++,owner=p.id,target=a.id,position=p.position+Vector3.up*.9f,direction=Vector3.forward,damage=20,pierce=99});Step(s,.5f);
            Check(a.hp==999980&&b.hp==1000000&&s.State.projectiles.Count==0,"an ordinary arrow damages exactly one enemy even with obsolete pierce data");
            s=New();p=s.State.Player(0);a=Target(s,5);b=Target(s,7);p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;s.ApplyItem(p,(int)ItemId.RicochetSeed);Step(s,.5f);
            Check(a.hp<1000000&&b.hp<1000000&&1000000-b.hp<1000000-a.hp,"ricochet item grants one weaker hit on a different enemy");
            foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {s=New();p=s.State.Player(0);p.invulnerability=100;var enemy=s.SpawnEnemy(kind);enemy.position=p.position+Vector3.forward;enemy.spawnGrace=0;enemy.specialTimer=100;Step(s,2);Check(!s.State.projectiles.Exists(pr=>pr.hostile),"no enemy projectile producer: "+kind);}
        }
        static void CurrentRules()
        {
            var cases=new (ItemId item,Func<PlayerState,float> stat,float delta)[]{
                (ItemId.SharpBlade,p=>p.damage,.10f),(ItemId.StoutHeart,p=>p.maxHp,10),
                (ItemId.Hourglass,p=>p.cooldownRate,.055f),(ItemId.GoldenCoin,p=>p.goldGain,.1f),
                (ItemId.BeginnersLuck,p=>p.luck,7.5f),(ItemId.SmallPotion,p=>p.healingPower,.15f),
                (ItemId.SpikedGloves,p=>p.critChance,.06f),(ItemId.TrueSight,p=>p.critDamage,.20f),
                (ItemId.Magnet,p=>p.pickupRange,.75f),(ItemId.CopperSpring,p=>p.attackSpeed,.10f),
                (ItemId.ChalkCircle,p=>p.areaSize,.09f),(ItemId.WindFletching,p=>p.projectileSpeed,.125f),
                (ItemId.ScholarSeal,p=>p.xpGain,.09f),(ItemId.LeadPendulum,p=>p.projectileSpeed,.06f),
                (ItemId.BloodGlass,p=>p.critDamage,.06f),(ItemId.MerchantScale,p=>p.goldGain,.06f)};
            foreach(var test in cases){var check=New();var player=check.State.Player(0);float before=test.stat(player);check.ApplyItem(player,(int)test.item);Check(Mathf.Abs(test.stat(player)-before-test.delta)<.0001f,"current percentage buff: "+test.item);}
            var s=New();var p=s.State.Player(0);s.ApplyItem(p,(int)ItemId.SwiftBoots);s.ApplyItem(p,(int)ItemId.SteelShield);
            Check(Mathf.Approximately(p.moveSpeed,1.1f)&&p.shield==35,"ten percent and flat bonuses stay unchanged");
            float crit=p.critDamage;s.ApplyAccessory(p,3,(int)ItemRarity.Uncommon);
            Check(Mathf.Approximately(p.critDamage-crit,.075f)&&GameData.Attributes[3].Value(1).Replace(',','.')=="+7.5%","uncommon accessory has the exact fractional nerf in gameplay and text");
            crit=p.critDamage;s.ApplyAccessory(p,3,(int)ItemRarity.Rare);Check(Mathf.Approximately(p.critDamage-crit,.3f),"rare accessory bonuses stay unchanged");
            Check(GameData.Items.Length==129&&Enum.GetNames(typeof(ItemId)).Length==129&&!Array.Exists(Enum.GetNames(typeof(ItemId)),n=>n=="VampireFang"),"fang is removed from the game ID list");
            Check(IconLibrary.ItemPixels((int)ItemId.TrueSight)==new Rect(0,0,256,256)&&IconLibrary.ItemTexture((int)ItemId.CopperSpring).name=="CopperSpring","individual transparent artwork maps stable IDs by name");
            for(int stage=1;stage<=7;stage++)
            {
                s=New();s.State.stage=stage;WorldMap.SetStage(stage);var normal=s.SpawnEnemy(EnemyKind.Skeleton);var guardian=s.SpawnEnemy(EnemyKind.Warden);float scale=Mathf.Pow(1.5f,stage-1);
                float[] bossHp={2500,5000,5500,6500,7500,10000,12000};
                Check(Mathf.Abs(normal.maxHp-EnemyCatalog.Health(EnemyKind.Skeleton)*scale)<.01f&&guardian.maxHp==bossHp[stage-1],"normal realm scaling and explicit boss HP for realm "+stage);
                for(int party=2;party<=3;party++)
                {
                    var coop=new Simulation(741+stage,true);for(int id=0;id<party;id++)coop.AddPlayer(id,"Boss HP",(Hero)id);
                    coop.State.stage=stage;WorldMap.SetStage(stage);var partyBoss=coop.SpawnEnemy(EnemyKind.Warden);
                    Check(partyBoss.maxHp==bossHp[stage-1]*Mathf.Pow(2,party-1),"boss HP retains co-op scaling in realm "+stage+" / party "+party);
                }
            }
            s=New();p=s.State.Player(0);p.goldGain=1.5f;var e=s.SpawnEnemy(EnemyKind.Skeleton);e.position=p.position+Vector3.forward*250;s.DamageEnemy(e,p,999999);
            Check(s.State.coins==36&&s.State.earnedCoins==6&&!s.State.pickups.Exists(c=>c.kind==PickupKind.Gold),"distant kills credit gold immediately with the owner's multiplier");
            var node=new ResourceNodeState{kind=PickupKind.Gold,hp=100,position=p.position+Vector3.forward*250};s.DamageResource(node,100,p);s.DamageResource(node,100,p);
            int leafGold=node.kind==PickupKind.Gold?33:0;
            Check(s.State.coins==36+leafGold&&s.State.earnedCoins==6+leafGold&&!s.State.pickups.Exists(c=>c.kind==PickupKind.Gold),"leaf gold credits once without floor bundles");
            s.State.pickups.Add(new PickupState{kind=PickupKind.Gold,value=7,position=p.position+Vector3.forward*290});Step(s,.04f);
            Check(s.State.coins==43+leafGold&&!s.State.pickups.Exists(c=>c.kind==PickupKind.Gold),"legacy gold converts regardless of distance and age");
            s.ApplyItem(p,(int)ItemId.MiserFuse);s.ApplyItem(p,(int)ItemId.AlchemyEngine);s.GrantGold(10,p);
            Check(s.State.itemFields.Exists(f=>f.item==(int)ItemId.MiserFuse)&&p.itemCounters[(int)ItemId.AlchemyEngine]>0,"automatic gold retains mine and alchemy synergies");
            s=New();p=s.State.Player(0);p.relic=Relic.Hourglass;p.critChance=1;
            var boss=s.SpawnEnemy(EnemyKind.Warden);boss.position=WorldMap.OnGround(p.position.x,p.position.z+5);boss.spawnGrace=0;boss.speed=0;boss.attackTimer=boss.specialTimer=100;
            var other=s.SpawnEnemy(EnemyKind.Skeleton);other.position=p.position+Vector3.right*3;other.spawnGrace=0;other.speed=0;
            foreach(var item in new[]{ItemId.FlameBlade,ItemId.IceCore,ItemId.VenomNeedle,ItemId.SerratedCoin,ItemId.Chronosphere,ItemId.GlacialPrison,ItemId.WinterSleep,ItemId.FateLoom,ItemId.ExecutionLedger})s.ApplyItem(p,(int)item);
            s.Step(1f/30,new Dictionary<int,InputFrame>{{0,new InputFrame{relic=true}}});
            Check(boss.frozen==0&&other.frozen>3.9f,"hourglass and chronosphere only freeze normal enemies");
            float hp=boss.hp;for(int n=0;n<12;n++)s.DamageEnemy(boss,p,1,true,true);
            Check(boss.hp<hp&&boss.frozen==0&&boss.ailments.Count==0,"weapon damage works while boss statuses are rejected");
            boss.frozen=999;boss.ailments.Add(new AfflictionState{kind=Ailment.Poison,power=99999,time=100});hp=boss.hp;s.DamageEnemy(boss,p,1,false);
            Check(boss.frozen==0&&boss.ailments.Count==0&&Mathf.Approximately(boss.hp,hp-1),"stale status data cannot bypass boss immunity");
            var origin=boss.position;s.State.itemFields.Add(new ItemFieldState{id=s.State.nextId++,owner=p.id,item=(int)ItemId.ChaosPortal,position=boss.position+Vector3.right*3,radius=10,time=3});Step(s,.2f);
            Check(Vector3.Distance(boss.position,origin)<.001f,"vortices cannot pull a boss");
            boss.position=p.position+Vector3.forward;boss.position.y=WorldMap.Ground(boss.position);boss.attackTimer=0;Step(s,1);
            Check(boss.attackSerial>0&&boss.frozen==0,"boss keeps attacking under control-item pressure");WorldMap.SetStage(1);
        }
        static void Models()
        {
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/"+hero));
                try
                {
                    var archer=root.GetComponent<HeroMotion>()?.archer;
                    if(archer!=null)
                    {
                        Check(archer.animator!=null&&archer.animator.avatar.isValid&&archer.pelvis!=null,"Generic bind skeleton saved for "+hero);
                        archer.SampleClip("Archer_Idle",0);var bones=archer.GetComponentsInChildren<Transform>(true);var positions=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)positions[i]=bones[i].localPosition;
                        archer.ApplyCrouch(1);bool genericConnected=true;for(int i=0;i<bones.Length;i++)if(bones[i]!=archer.pelvis)genericConnected&=Vector3.Distance(bones[i].localPosition,positions[i])<.0001f;
                        Check(genericConnected,"Generic crouch preserves child bind offsets including head and neck: "+hero);
                        var skins=archer.GetComponentsInChildren<SkinnedMeshRenderer>();Check(skins.Length>0&&Array.TrueForAll(skins,skin=>skin.bones.Length==skin.sharedMesh.bindposes.Length&&Array.TrueForAll(skin.bones,bone=>bone!=null)),"Generic costume binds to connected real skeleton bones: "+hero);
                        continue;
                    }
                    var rig=root.GetComponent<HumanRigShape>();Check(rig.shapedLocalPositions!=null&&rig.shapedLocalPositions.Length==rig.bones.Length,"connected bind positions saved for "+hero);
                    var skeleton=root.GetComponent<SkeletalActor>();skeleton.animationPlayer.enabled=false;
                    for(int i=0;i<rig.bones.Length;i++)rig.bones[i].localPosition=rig.originalLocalPositions[i];
                    skeleton.hips.localPosition+=Vector3.down*.22f;skeleton.spine.localRotation*=Quaternion.Euler(16,0,0);rig.ApplyPose();
                    bool connected=true;for(int i=0;i<rig.bones.Length;i++)if(rig.bones[i].name!="hips")connected&=Vector3.Distance(rig.bones[i].localPosition,rig.shapedLocalPositions[i])<.0001f;
                    Check(connected,"crouch preserves every child bind length, including head and neck: "+hero);
                    var mesh=root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;Check(Array.Exists(mesh.boneWeights,w=>w.weight0>.1f&&w.weight1>.1f),"neck bridge blends head and torso bones: "+hero);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
    }
}
