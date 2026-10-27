using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era.Editor
{
    public static class LootBalanceChecks
    {
        static int count;
        static void Check(bool ok,string label){count++;if(!ok)throw new Exception("ERA loot balance check failed: "+label);}
        static Simulation New(Hero hero=Hero.Ember,int seed=932)
        {
            var s=new Simulation(seed);var p=s.AddPlayer(0,"Loot balance",hero);
            p.weapons=new[]{-1,-1};p.critChance=0;s.Start();s.State.spawnTimer=100000;s.State.resourceNodes.Clear();return s;
        }
        static void Give(Simulation s,ItemId item,int copies=1)
        {for(int i=0;i<copies;i++)s.ApplyItem(s.State.Player(0),(int)item);}
        static EnemyState Target(Simulation s,float distance=5)
        {
            var e=s.SpawnEnemy(EnemyKind.Skeleton);e.position=s.State.Player(0).position+Vector3.forward*distance;
            e.hp=e.maxHp=10000;e.frozen=100;e.spawnGrace=100;return e;
        }
        public static int Run()
        {
            count=0;CatalogueAndDrops();WorldAndMovement();CombatAndRepeats();WorldMap.SetStage(1);return count;
        }
        static void CatalogueAndDrops()
        {
            int[] tiers=new int[5];int retired=0;var available=new HashSet<int>();
            var s=New();var p=s.State.Player(0);
            for(int i=0;i<ItemCatalog.Count;i++)
            {
                if(ItemCatalog.IsAvailable(i)){tiers[(int)GameData.Items[i].rarity]++;available.Add(i);}
                else
                {
                    retired++;Check(GameData.Items[i].rarity==ItemRarity.Rare,"only rare items retired");
                    s.ApplyItem(p,i);Check(p.items[i]==0,"retired item cannot be granted: "+(ItemId)i);
                }
            }
            Check(retired==13&&tiers[(int)ItemRarity.Rare]==10,"remove 13 of 23 rare items, more than half");
            Check(available.Count==116&&available.Count==ItemCatalog.ActiveCount,"visible catalogue has 116 items");
            Check(tiers[1]==33&&tiers[2]==38&&tiers[4]==21&&tiers[3]==14,"all other tiers retain their distinct mechanics");
            Check(!ItemCatalog.IsAvailable(-1)&&!ItemCatalog.IsAvailable(ItemCatalog.Count),"invalid item IDs rejected");
            var seen=new HashSet<int>();
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))foreach(int tier in new[]{0,1})foreach(int luck in new[]{0,100})
            {
                s=New(hero,932+(int)hero*100+tier*10+luck);p=s.State.Player(0);p.luck=luck;
                for(int roll=0;roll<600;roll++)
                {
                    s.State.chests.Clear();s.State.loot.Clear();s.State.effects.Clear();s.State.coins=int.MaxValue;
                    var chest=new ChestState{id=s.State.nextId++,position=p.position,tier=tier,baseCost=0,cost=0};s.State.chests.Add(chest);
                    Check(s.TryOpenChest(p),"real chest opens during seeded loot sampling");
                    Check(available.Contains(chest.reward)&&(int)GameData.Items[chest.reward].rarity==chest.rewardRarity,"real chest only rolls available matching-tier items");
                    Check(s.State.loot.Count==1&&s.State.loot[0].content==chest.reward,"floor reward matches the chest roll");seen.Add(chest.reward);
                }
            }
            Check(seen.SetEquals(available),"7200 actual chest rolls cover all 116 active items and no retired items");
        }
        static void WorldAndMovement()
        {
            for(int stage=1;stage<=CampaignData.StageCount;stage++)
            {
                var s=New();s.State.stage=stage;WorldMap.SetStage(stage);s.State.chests.Clear();WorldMap.PopulateChests(s.State);
                Check(s.State.chests.Count==30,"30 ordinary and moon chests in realm "+stage);
                int moons=0;foreach(var chest in s.State.chests)
                {if(chest.tier==1)moons++;Check(Mathf.Abs(chest.position.y-WorldMap.Ground(chest.position))<.001f,"chest sits on terrain in realm "+stage);}
                Check(moons==6,"six moon chests included in the total of thirty");
            }
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))foreach(bool crouch in new[]{false,true})
            {
                var s=New(hero);var p=s.State.Player(0);p.position=s.State.playerSpawnPosition;
                var start=p.position;s.Step(.05f,new Dictionary<int,InputFrame>{{0,new InputFrame{x=1,crouch=crouch}}});
                float travelled=new Vector2(p.position.x-start.x,p.position.z-start.z).magnitude;
                Check(Mathf.Abs(travelled-7.8f*1.06f*.05f*(crouch?.43f:1))<.001f,"actual movement is six percent faster: "+hero+" / crouch "+crouch);
            }
        }
        static void CombatAndRepeats()
        {
            var s=New();var p=s.State.Player(0);Give(s,ItemId.FateLoom);p.critChance=1;p.critDamage=2;var e=Target(s);
            for(int i=0;i<20;i++)s.DamageEnemy(e,p,1);
            Check(e.hp==9960,"Fate Loom preserves an archer's 100 percent critical chance");
            s=New();p=s.State.Player(0);Give(s,ItemId.FateLoom);p.critDamage=2;e=Target(s);
            for(int i=0;i<8;i++)s.DamageEnemy(e,p,1);
            Check(e.hp==9990,"Fate Loom still guarantees every fourth hit without random crits");
            foreach(int copies in new[]{1,5,20})
            {
                s=New();p=s.State.Player(0);Give(s,ItemId.SepticWound,copies);e=Target(s);
                e.ailments.Add(new AfflictionState{kind=Ailment.Poison,owner=0,stacks=8,time=5});
                e.ailments.Add(new AfflictionState{kind=Ailment.Bleed,owner=0,stacks=5,time=5});
                s.DamageEnemy(e,p,100);Check(Mathf.Abs(10000-e.hp-180)<.01f,"Septic Wound never exceeds 80 percent extra damage: "+copies);
                s=New();p=s.State.Player(0);Give(s,ItemId.MirrorShard,copies);p.critChance=1;p.critDamage=1;e=Target(s);var other=Target(s,7);
                s.DamageEnemy(e,p,100);float shard=10000-other.hp;
                Check(shard>=49.99f&&shard<=100.01f,"mirror repeat damage remains between 50 and 100 percent: "+copies);
                s=New();p=s.State.Player(0);Give(s,ItemId.EclipseCrown,copies);p.combatTime=2400;p.itemCounters[(int)ItemId.EclipseCrown]=100;e=Target(s);
                s.DamageEnemy(e,p,100);Check(Mathf.Abs(p.itemValues[(int)ItemId.EclipseCrown]-75)<.01f,"late eclipse storage capped at 75 percent including repeats");
                s=New();p=s.State.Player(0);Give(s,ItemId.MobiusQuiver,copies);p.weaponTimers[0]=1;p.itemCounters[(int)ItemId.MobiusQuiver]=2;
                s.State.projectiles.Add(new ProjectileState{id=s.State.nextId++,owner=0,returning=true,life=2,position=p.position+Vector3.up});
                s.Step(1f/30,new Dictionary<int,InputFrame>());
                Check(s.State.projectiles.Count==0&&p.weaponTimers[0]>=.249f&&p.weaponTimers[0]<=.751f,"three caught arrows accelerate cooldown within bounded repeat bonus");
            }
            s=New();p=s.State.Player(0);Give(s,ItemId.FlameBlade);e=Target(s);s.DamageEnemy(e,p,1);
            Check(e.ailments.Exists(a=>a.kind==Ailment.Burn&&Mathf.Approximately(a.power,9)),"epic burn deals nine damage per second per layer");
            s=New();p=s.State.Player(0);Give(s,ItemId.StormAmulet);e=Target(s);var second=Target(s,7);
            for(int i=0;i<4;i++)s.DamageEnemy(e,p,1);
            Check(second.hp==9964,"fourth hit actually deals the new 36 damage lightning proc");
            s=New();p=s.State.Player(0);Give(s,ItemId.SoulJar);p.hp=50;e=Target(s);s.DamageEnemy(e,p,20000);
            Check(p.hp==58,"rare kill healing restores eight health");
            s=New();p=s.State.Player(0);Give(s,ItemId.RetaliationSeal);s.DamagePlayer(p,10);
            Check(p.shield==25,"rare retaliation grants the improved protective shield");
            s=New();p=s.State.Player(0);Give(s,ItemId.BookOfKnowledge);p.weapons=new[]{(int)WeaponKind.Bow,-1};p.weaponLevels[0]=GameData.MaxWeaponLevel;s.State.level=20;s.State.xp=0;s.State.xpRequired=GameData.ExperienceRequired(20);
            Give(s,ItemId.BookOfKnowledge);Check(s.State.xp==s.State.xpRequired/4,"maxed weapons convert the book into meaningful level-scaled XP");
        }
    }
}
