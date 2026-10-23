using System;
using System.Collections.Generic;
using UnityEngine;
namespace Era.Editor
{
    public static class BalanceChecks
    {
        static int count;
        static void Check(bool value,string label){count++;if(!value)throw new Exception("ERA balance check failed: "+label);}
        static Simulation New(){var s=new Simulation(401);var p=s.AddPlayer(0,"Balance",Hero.Ember);p.weapons=new[]{-1,-1};p.critChance=0;s.Start();s.State.spawnTimer=10000;s.State.resourceNodes.Clear();return s;}
        static EnemyState Target(Simulation s,float z){var e=s.SpawnEnemy(EnemyKind.Skeleton);e.position=s.State.Player(0).position+Vector3.forward*z;e.hp=e.maxHp=1000000;e.frozen=100;return e;}
        static void Step(Simulation s,float time){for(int n=0;n<Mathf.CeilToInt(time*30);n++)s.Step(1f/30,new Dictionary<int,InputFrame>());}
        public static int Run()
        {
            count=0;Economy();Healing();Evasion();Projectiles();Models();return count;
        }
        static void Economy()
        {
            int[] expected={25,42,70,117,195,325,542};for(int n=0;n<expected.Length;n++)Check(GameData.ChestPrice(25,n)==expected[n],"recursive screenshot price with upward rounding "+n);
            Check(GameData.ChestPrice(25,int.MaxValue)==int.MaxValue,"geometric prices saturate without integer overflow");
            var s=New();Check(s.State.chests.Count==240,"four times as many ordinary-world chests");
            var p=s.State.Player(0);s.State.coins=10000;p.position=s.State.chests[0].position;s.TryOpenChest(p);
            Check(s.State.chestsOpenedInStage==1&&s.State.openedChests==1&&s.State.chests[1].cost==24,"opening updates the shared realm progression");
            var boss=s.SpawnEnemy(EnemyKind.Warden);s.DamageEnemy(boss,p,99999999);p.position=s.State.portal.position;s.TryUsePortal(p);
            Check(s.State.chestsOpenedInStage==0&&s.State.openedChests==1&&s.State.chests.TrueForAll(c=>c.cost==c.baseCost),"travel resets prices while preserving total chest statistics");
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
        static void Models()
        {
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/"+hero));
                try
                {
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
