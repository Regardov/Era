using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era.Editor
{
    public static class PassiveAutumnChecks
    {
        static int assertions;
        static void Check(bool value,string label){assertions++;if(!value)throw new Exception("ERA autumn check failed: "+label);}
        public static int Run()
        {
            assertions=0;Progression();Lightning();Leaves();Buildings();WorldMap.SetStage(1);return assertions;
        }
        static void Progression()
        {
            var sim=new Simulation(817,true);var archer=sim.AddPlayer(0,"Archer",Hero.Ember);var echo=sim.AddPlayer(1,"Echo",Hero.Echo);var lumen=sim.AddPlayer(2,"Lumen",Hero.Lumen);
            float crit=archer.critChance,radius=Simulation.AuraRadius(lumen);echo.downed=true;
            int xp=0;for(int level=1;level<26;level++)xp+=GameData.ExperienceRequired(level);sim.GrantExperience(xp);
            Check(sim.State.level==26&&Mathf.Abs(archer.critChance-crit-.75f)<.00001f,"queued level-ups immediately add 25 archer crit bonuses");
            Check(echo.levelUps==25&&GameData.LightningRicochetPercent(echo)==250,"downed ally receives each shared passive level-up");echo.downed=false;
            Check(Mathf.Abs(Simulation.AuraRadius(lumen)/radius-1.75f)<.00001f&&lumen.areaSize==1,"Lumen radius grows by 3% per gained level without enlarging abilities");
            var sets=new HashSet<string>();bool sawNew=false,sawUpgrade=false,milestone=false;
            for(int guard=0;lumen.pendingLevels>0&&guard<100;guard++)
            {
                if(lumen.offerKind==RewardKind.Aura)
                {
                    Check(lumen.offers.Length==3&&new HashSet<int>(lumen.offers).Count==3,"exactly three distinct aura choices at every level");
                    var sorted=(int[])lumen.offers.Clone();Array.Sort(sorted);sets.Add(string.Join(",",sorted));
                    foreach(int aura in lumen.offers){sawNew|=lumen.auraLevels[aura]==0;sawUpgrade|=lumen.auraLevels[aura]>0;}
                    if(Array.FindAll(lumen.auraLevels,a=>a>0).Length==GameData.MaxAuras)Check(Array.TrueForAll(lumen.offers,a=>lumen.auraLevels[a]>0),"three-type cap offers owned auras only");
                }
                if(lumen.offerKind==RewardKind.Weapon)milestone=lumen.completedLevel+1==25;
                int slot=0;if(lumen.offerKind==RewardKind.Accessory)slot=Array.FindIndex(lumen.offers,a=>a!=14);
                if(lumen.offerKind==RewardKind.Aura){int fresh=Array.FindIndex(lumen.offers,a=>lumen.auraLevels[a]==0);if(fresh>=0)slot=fresh;}
                Check(sim.ChooseItem(lumen,slot),"valid queued choice accepted");
            }
            Check(lumen.completedLevel==26&&lumen.pendingLevels==0&&lumen.levelUps==25&&milestone,"aura and weapon choices finish levels without granting passive twice");
            Check(sawNew&&sawUpgrade&&sets.Count>=2,"aura offers include acquisition and upgrades before the three-type cap");
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State));
            Check(copy.Player(1).levelUps==25&&Mathf.Approximately(copy.Player(0).critChance,archer.critChance)&&Mathf.Approximately(Simulation.AuraRadius(copy.Player(2)),Simulation.AuraRadius(lumen)),"network snapshot retains all passive gains");
        }
        static void Lightning()
        {
            Check(GameData.ChanceRicochets(0,0)==0&&GameData.ChanceRicochets(100,.999999)==1&&GameData.ChanceRicochets(200,0)==2,"exact hundreds guarantee only their complete bounces");
            Check(GameData.ChanceRicochets(230,.299999)==3&&GameData.ChanceRicochets(230,.30)==2,"230% adds two guaranteed bounces and a 30% remainder");
            var sim=new Simulation(717);var p=sim.AddPlayer(0,"Echo",Hero.Echo);sim.Start();sim.State.multiplayer=true;sim.State.resourceNodes.Clear();sim.State.spawnTimer=10000;p.position=new Vector3(20,0,0);p.invulnerability=10000;p.critChance=0;
            for(int i=0;i<9;i++)sim.State.enemies.Add(new EnemyState{id=sim.State.nextId++,kind=EnemyKind.Wisp,position=new Vector3(22+i*.55f,0,3),hp=100000,maxHp=100000,frozen=10000,spawnGrace=10000});
            var inputs=new Dictionary<int,InputFrame>();
            foreach(int levels in new[]{0,10,20,50})
            {
                p.levelUps=levels;p.weaponTimers[0]=0;sim.State.hits.Clear();sim.Step(.01f,inputs);
                Check(sim.State.hits.Count==1+levels/10&&new HashSet<int>(sim.State.hits.ConvertAll(h=>h.target)).Count==sim.State.hits.Count,"weapon chain hits new targets for "+levels*10+"%");
            }
            p.levelUps=20;sim.ApplyItem(p,(int)ItemId.RicochetSeed);p.weaponTimers[0]=0;sim.State.hits.Clear();sim.Step(.01f,inputs);
            Check(sim.State.hits.Count==4,"passive chains add to Ricochet Seed");p.items[(int)ItemId.RicochetSeed]=0;
            p.levelUps=23;int extra=0;
            for(int shot=0;shot<400;shot++){p.weaponTimers[0]=0;sim.State.hits.Clear();sim.Step(.01f,inputs);Check(sim.State.hits.Count==3||sim.State.hits.Count==4,"remainder affects only the next bounce");if(sim.State.hits.Count==4)extra++;}
            Check(extra>80&&extra<160,"weapon simulation samples approximately 30% optional third ricochets");
            p.hero=Hero.Ember;p.weaponTimers[0]=0;sim.State.hits.Clear();sim.Step(.01f,inputs);Check(sim.State.hits.Count==1,"other heroes do not inherit Echo's lightning passive");
        }
        static void Leaves()
        {
            var sim=new Simulation(991);var p=sim.AddPlayer(0,"Leaves",Hero.Ember);int gold=sim.State.coins;
            var outcomes=new HashSet<PickupKind>[3];for(int i=0;i<3;i++)outcomes[i]=new HashSet<PickupKind>();
            foreach(var node in sim.State.resourceNodes)
            {
                sim.DamageResource(node,1,p);Check(!node.broken&&sim.State.coins==gold&&sim.State.pickups.Count==0,"partial leaf hit does not roll or award loot");
                sim.DamageResource(node,100,p);outcomes[node.id%3].Add(node.kind);int coins=sim.State.coins,count=sim.State.pickups.Count;
                sim.DamageResource(node,100,p);Check(sim.State.coins==coins&&sim.State.pickups.Count==count,"each pile awards once");gold=coins;sim.State.pickups.Clear();
            }
            for(int variant=0;variant<3;variant++)Check(outcomes[variant].Count==2,"each visual leaf variant independently drops gold and XP");
        }
        static void Buildings()
        {
            for(int stage=1;stage<=7;stage++)
            {
                WorldMap.SetStage(stage);Check(WorldMap.Buildings.Count>=16,"settlements distributed through realm "+stage);var quadrants=new HashSet<int>();
                foreach(var b in WorldMap.Buildings)
                {
                    quadrants.Add((b.position.x>0?1:0)+(b.position.z>0?2:0));
                    Check(!WorldMap.ClearLocation(b.position,1),"spawns cannot occupy a building");
                    var pushed=WorldMap.Resolve(b.position,Vector3.zero,.4f);Check(!WorldMap.BuildingContains(b,pushed,.399f),"shared host/client solver pushes actors outside structure");
                    Check(!WorldMap.InWater(b.position.x,b.position.z,7),"building foundation avoids streams");
                }
                Check(quadrants.Count==4,"buildings appear in every map quadrant");
            }
        }
    }
}
