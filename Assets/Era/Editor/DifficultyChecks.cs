using System;
using System.Collections.Generic;
using UnityEngine;
namespace Era.Editor
{
    public static class DifficultyChecks
    {
        static int count;
        static void Check(bool value,string message){count++;if(!value)throw new Exception("ERA difficulty check failed: "+message);}
        static Simulation New(int players)
        {
            var sim=new Simulation(321,players>1);
            for(int i=0;i<players;i++){var p=sim.AddPlayer(i,"Player "+i,Hero.Ember);p.weapons=new[]{-1,-1};p.invulnerability=1000;}
            foreach(var p in sim.State.players)sim.SetReady(p.id,true);
            sim.Start();sim.State.spawnTimer=1000;sim.State.resourceNodes.Clear();return sim;
        }
        static void Step(Simulation sim,int ticks,float dt=1f/30)
        {for(int i=0;i<ticks;i++)sim.Step(dt,new Dictionary<int,InputFrame>());}
        static int Spawns(int players,bool aftermath=false)
        {
            var sim=New(players);sim.State.postBossActive=aftermath;sim.State.portal.active=aftermath;
            sim.State.spawnTimer=sim.SpawnInterval;
            for(int i=0;i<330;i++){sim.State.enemies.Clear();Step(sim,1);}
            return sim.State.waveSpawned;
        }
        public static int Run()
        {
            count=0;
            int solo=Spawns(1);
            for(int n=1;n<=3;n++)
            {
                var sim=New(n);float scale=Mathf.Pow(2,n-1);
                var enemy=sim.SpawnEnemy(EnemyKind.Skeleton);var boss=sim.SpawnEnemy(EnemyKind.Warden);
                Check(Mathf.Approximately(enemy.maxHp,EnemyCatalog.Health(enemy.kind)*scale)&&Mathf.Approximately(enemy.damage,10*scale),"ordinary HP and damage x"+scale);
                Check(Mathf.Approximately(boss.maxHp,1700*scale)&&Mathf.Approximately(boss.damage,28*scale),"boss HP and melee damage x"+scale);
                Check(Mathf.Abs(sim.SpawnInterval-1.1f/scale)<.0001f,"spawn interval x"+scale);
                Check(Mathf.Abs(Spawns(n)-solo*scale)<=2,"actual spawned total scales with party size "+n);
                Check(Mathf.Abs(Spawns(n,true)-solo*scale*2)<=2,"aftermath actual rate doubles for party "+n);
                boss.spawnGrace=0;boss.specialTimer=0;boss.position=WorldMap.OnGround(0,10);Step(sim,1);
                Check(sim.State.hazards.Count>0&&Mathf.Approximately(sim.State.hazards[0].damage,26*scale),"boss special damage includes party scale "+n);
            }
            var run=New(3);var p=run.State.Player(0);var mob=run.SpawnEnemy(EnemyKind.Skeleton);
            mob.position=WorldMap.OnGround(0,8);mob.speed=0;mob.spawnGrace=1000;mob.hp=mob.maxHp*.5f;
            float hp=mob.maxHp,damage=mob.damage;var guardian=run.SpawnEnemy(EnemyKind.Warden);
            guardian.position=WorldMap.OnGround(0,12);run.DamageEnemy(guardian,p,1000000);
            Check(run.State.postBossActive&&run.State.portal.active&&run.State.enemies.Contains(mob),"defeating the boss preserves living mobs and opens the portal");
            Check(Mathf.Abs(run.SpawnInterval-1.1f/8)<.0001f,"three players and defeated boss give x8 rate");
            run.State.spawnTimer=1000;Step(run,299);
            Check(run.State.postBossGrowthSteps==0&&Mathf.Approximately(mob.maxHp,hp),"no growth before ten seconds");
            Step(run,2);Check(run.State.postBossGrowthSteps==1&&Mathf.Abs(mob.maxHp-hp*1.1f)<.001f&&Mathf.Abs(mob.hp-mob.maxHp*.5f)<.001f&&Mathf.Abs(mob.damage-damage*1.1f)<.001f,"ten seconds grows current/max HP and damage without healing percentage");
            Step(run,300);Check(run.State.postBossGrowthSteps==2&&Mathf.Abs(mob.maxHp-hp*1.21f)<.001f,"growth compounds at twenty seconds");
            var fresh=run.SpawnEnemy(EnemyKind.Skeleton);Check(Mathf.Abs(fresh.maxHp-mob.maxHp)<.001f&&Mathf.Abs(fresh.damage-mob.damage)<.001f,"fresh mobs inherit accumulated growth");
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(run.State));
            Check(copy.postBossActive&&copy.postBossGrowthSteps==2&&copy.enemyPartyScale==4&&Mathf.Approximately(copy.enemies[0].maxHp,mob.maxHp),"clients receive aftermath time, growth and enemy stats");
            run.State.players.RemoveAt(2);Step(run,1);
            Check(Mathf.Approximately(run.State.enemyPartyScale,2)&&Mathf.Abs(mob.maxHp-hp*1.21f*.5f)<.001f,"disconnect rescales living enemies to actual connected party");
            p.position=run.State.portal.position;Check(run.TryUsePortal(p)&&!run.State.postBossActive&&run.State.postBossGrowthSteps==0&&run.State.postBossElapsed==0,"next realm resets aftermath escalation");
            fresh=run.SpawnEnemy(EnemyKind.Skeleton);Check(Mathf.Abs(fresh.maxHp-EnemyCatalog.Health(fresh.kind)*1.5f*2)<.001f,"new realm retains party scaling and realm scaling only");
            run=New(3);run.State.stageElapsed=600;run.State.wave=11;run.State.postBossActive=true;run.State.portal.active=true;run.State.spawnTimer=0;
            Step(run,200,.05f);Check(run.State.enemies.Count<=GameData.MaxEnemies&&run.State.phase==RunPhase.Playing,"max-rate late aftermath respects enemy cap and remains playable");
            WorldMap.SetStage(1);return count;
        }
    }
}
