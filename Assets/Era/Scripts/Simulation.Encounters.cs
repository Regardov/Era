using System;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        void PopulateEncounters()
        {
            State.bossStatuePosition=WorldMap.RandomClearLocation(random,4,State.playerSpawnPosition,45);
            State.resourceNodes.Clear();State.eliteShrines.Clear();
            for(int i=0;i<4;i++)
            {
                float a=(i+.5f)*Mathf.PI*.5f;var p=WorldMap.Resolve(WorldMap.OnGround(Mathf.Cos(a)*175,Mathf.Sin(a)*175),Vector3.zero,3);p.y=WorldMap.Ground(p);
                State.eliteShrines.Add(new EliteShrineState{id=State.nextId++,position=p});
            }
            for(int i=0;i<GameData.ResourceNodeCount;i++)
            {
                float a=i*2.399963f+State.stage*.31f,r=i<2?13:30+Mathf.Sqrt((i-2)/(float)(GameData.ResourceNodeCount-3))*(GameData.ArenaRadius-52);
                var p=WorldMap.Resolve(WorldMap.OnGround(Mathf.Cos(a)*r,Mathf.Sin(a)*r),Vector3.zero,1);p.y=WorldMap.Ground(p);
                State.resourceNodes.Add(new ResourceNodeState{id=State.nextId++,position=p,kind=i%2==0?PickupKind.Gold:PickupKind.Experience});
            }
        }
        void TickWaves(float dt)
        {
            bool aftermath=State.postBossActive;
            int minute=Mathf.Min(10,Mathf.FloorToInt(State.stageElapsed/60));
            if(!aftermath&&minute>State.wave-1)
            {
                State.wave=minute+1;State.waveSpawned=0;
                State.waveQuota=(10+minute*3)*Mathf.RoundToInt(PartyMultiplier);
                State.spawnTimer=0;
                State.notice=I18n.P("A new horde arrives. Enemy spawns accelerate!","Приближается новая орда. Враги появляются чаще!");State.noticeKind=0;State.noticeTime=4;
            }
            State.waveActive=aftermath||State.waveQuota>0;
            State.nextWave=aftermath||minute>=10?0:60-State.stageElapsed%60;
            // Recycle distant ordinary enemies so travelling players still meet a nearby horde.
            State.enemies.RemoveAll(e=>e.kind!=EnemyKind.Warden&&e.eliteShrine<0&&!State.players.Exists(p=>!p.downed&&!p.eliminated&&Vector2.Distance(new Vector2(e.position.x,e.position.z),new Vector2(p.position.x,p.position.z))<48));
            State.spawnTimer-=dt;
            while(State.spawnTimer<=0&&State.enemies.Count<GameData.MaxEnemies)
            {
                // Carry the fractional remainder, including several due batches in a slow tick.
                State.spawnTimer+=SpawnInterval;
                int batch=2+(State.waveQuota>0?3+minute/3:0);
                for(int i=0;i<batch&&State.enemies.Count<GameData.MaxEnemies;i++)
                {
                    var e=SpawnEnemy();e.wave=State.wave;State.waveSpawned++;
                    if(State.waveQuota>0)State.waveQuota--;
                }
            }
            if(State.spawnTimer<=0)State.spawnTimer=SpawnInterval;
            State.waveActive=aftermath||State.waveQuota>0;
        }
        public bool TrySummonBoss(PlayerState p)
        {
            if(State.phase!=RunPhase.Playing||p.downed||p.eliminated||State.bossSpawned||State.portal.active||Vector3.Distance(p.position,State.bossStatuePosition)>4)return false;
            if(State.eliteShrines.Exists(s=>s.active&&!s.completed))
            {State.notice=I18n.P("Finish the active elite shrine first.","Сначала завершите активное испытание элиты.");State.noticeKind=0;State.noticeTime=3;return true;}
            State.enemies.RemoveAll(e=>e.wave>0);State.projectiles.Clear();State.bossSpawned=true;var boss=SpawnEnemy(EnemyKind.Warden);boss.position=WorldMap.Resolve(State.bossStatuePosition,Vector3.forward*8,2);boss.position.y=WorldMap.Ground(boss.position);
            Effect(12,State.bossStatuePosition,7);return true;
        }
        public bool TryActivateEliteShrine(PlayerState p)
        {
            if(State.phase!=RunPhase.Playing||p.downed||p.eliminated||State.bossSpawned||State.portal.active)return false;
            foreach(var shrine in State.eliteShrines)
            {
                if(shrine.active||shrine.completed||Vector3.Distance(p.position,shrine.position)>3.8f)continue;
                shrine.active=true;
                for(int i=0;i<5;i++)
                {
                    float a=i*Mathf.PI*2/5;var e=SpawnEnemy(i%2==0?EnemyKind.Brute:EnemyKind.Stalker);e.eliteShrine=shrine.id;e.hp*=3;e.maxHp=e.hp;e.damage*=1.45f;e.speed*=1.08f;
                    e.position=WorldMap.Resolve(shrine.position,new Vector3(Mathf.Cos(a)*6,0,Mathf.Sin(a)*6),.8f);e.position.y=WorldMap.Ground(e.position);e.attackTimer=1;
                }
                Effect(12,shrine.position,7);return true;
            }
            return false;
        }
        void TickEliteShrines()
        {
            foreach(var shrine in State.eliteShrines)
            {
                if(!shrine.active||shrine.completed||State.enemies.Exists(e=>e.eliteShrine==shrine.id))continue;
                shrine.completed=true;
                var position=WorldMap.Resolve(shrine.position,Vector3.forward*3,1);position.y=WorldMap.Ground(position);
                State.chests.Add(new ChestState{id=State.nextId++,position=position,tier=1,free=true,cost=0,baseCost=0});Effect(7,position,4);
                State.notice=I18n.P("Elite trial complete. A free chest awaits.","Элита побеждена. Вас ждёт бесплатный сундук.");State.noticeKind=0;State.noticeTime=5;
            }
        }
        public bool TryBreakResource(PlayerState p)
        {
            if(p.downed||p.eliminated||State.phase!=RunPhase.Playing)return false;
            foreach(var node in State.resourceNodes)if(!node.broken&&Vector3.Distance(p.position,node.position)<2.7f)
            {DamageResource(node,100,p);p.weaponPoseTime=.3f;p.weaponCastSerial++;return true;}
            return false;
        }
        public void DamageResource(ResourceNodeState node,float damage,PlayerState owner)
        {
            if(node.broken||damage<=0)return;node.hp-=damage;node.hitSerial++;if(node.hp>0)return;node.broken=true;
            int value=node.kind==PickupKind.Gold?28+State.stage*5:18+State.stage*4;
            if(node.kind==PickupKind.Gold)GrantGold(value,owner);
            else for(int i=0;i<4;i++)
            {float a=i*Mathf.PI*.5f;State.pickups.Add(new PickupState{id=State.nextId++,kind=node.kind,value=value/4+(i<value%4?1:0),position=node.position+new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a))});}
            Effect(7,node.position+Vector3.up*.3f,1.8f);
        }
    }
}
