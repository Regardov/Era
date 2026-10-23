using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        void DefeatBoss(EnemyState boss)
        {
            foreach(var p in State.players)if(Has(p,ItemId.SevenfoldOath)){int n=Stack(p,ItemId.SevenfoldOath);p.projectileCount=Mathf.Min(48,p.projectileCount+n);p.damage+=.15f*n;p.itemCounters[I(ItemId.SevenfoldOath)]++;}
            State.bossSpawned=true;
            State.enemies.Clear();State.projectiles.Clear();State.hazards.Clear();State.itemFields.Clear();foreach(var p in State.players)p.bursts.Clear();
            State.coins+=60+State.stage*20;State.earnedCoins+=60+State.stage*20;
            GrantExperience(60+State.stage*45);
            if(State.stage>=CampaignData.StageCount){State.phase=RunPhase.Victory;return;}
            var position=WorldMap.Resolve(boss.position,Vector3.zero,2);position.y=WorldMap.Ground(position);
            State.portal.position=position;State.portal.active=true;
            State.notice=I18n.P("Boss defeated. A portal to the next realm has opened.","Босс повержен. Открылся портал в следующий мир.");State.noticeKind=0;State.noticeTime=8;
        }

        public bool TryUsePortal(PlayerState p)
        {
            if(State.phase!=RunPhase.Playing||!State.portal.active||p.downed||p.eliminated||Vector3.Distance(p.position,State.portal.position)>3.5f||State.stage>=CampaignData.StageCount)return false;
            State.stage++;State.stageSerial++;WorldMap.SetStage(State.stage);State.stageElapsed=0;State.wave=1;State.nextWave=60;State.waveQuota=State.waveSpawned=0;State.waveActive=false;State.spawnCursor=0;
            State.portal.active=false;State.bossSpawned=false;State.spawnTimer=0;State.chestsOpenedInStage=0;
            State.enemies.Clear();State.projectiles.Clear();State.pickups.Clear();State.effects.Clear();State.hits.Clear();State.hazards.Clear();State.chests.Clear();State.loot.Clear();State.shrines.Clear();State.itemFields.Clear();
            WorldMap.PopulateChests(State);PopulateShrines();PopulateEncounters();
            foreach(var player in State.players)
            {
                player.position=WorldMap.OnGround((player.id-1)*3,0);player.velocity=Vector3.zero;player.verticalSpeed=0;
                player.downed=player.eliminated=false;player.grounded=true;player.crouching=false;player.bleedout=25;player.reviveProgress=0;player.chargingShrine=-1;
                player.hp=player.maxHp;player.invulnerability=3;player.auraDamageTimer=0;
                player.knockback=Vector3.zero;player.hitSlide=0;
                player.bursts.Clear();player.stillTime=0;player.itemCounters[I(ItemId.PhoenixAsh)]=0;
                if(Has(player,ItemId.SevenfoldOath))Field(player,ItemId.EndlessRefrain,player.position,2,25,150*Stack(player,ItemId.SevenfoldOath)*player.itemCounters[I(ItemId.SevenfoldOath)]);
            }
            RefreshAuras();return true;
        }

        void BossAttack(EnemyState boss,PlayerState target)
        {
            int stage=Mathf.Max(1,boss.bossStage);boss.attackCycle++;
            float damage=26*(1+(stage-1)*.20f),delay=Mathf.Max(.8f,1.5f-stage*.07f);
            boss.windup=delay;boss.attackPosition=target.position;boss.specialTimer=Mathf.Max(2.6f,5.5f-stage*.35f);
            var towards=(target.position-boss.position);towards.y=0;if(towards.sqrMagnitude<.01f)towards=Vector3.forward;towards.Normalize();
            switch(stage)
            {
                case 1:Hazard(target.position,4.5f,delay,damage,stage);break;
                case 2:
                    for(int i=0;i<4;i++)Hazard(target.position+towards*(i-1)*4,2.5f,delay+i*.16f,damage,stage);break;
                case 3:
                    Hazard(boss.position,5.5f,delay,damage,stage);
                    foreach(var player in State.players)if(!player.downed&&!player.eliminated)Hazard(player.position+player.velocity*.3f,2.8f,delay+.3f,damage*.8f,stage);break;
                case 4:
                    Hazard(boss.position,3.5f,delay,damage,stage);
                    for(int i=0;i<4;i++){float a=i*Mathf.PI/2;Hazard(target.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*5,3.2f,delay+.2f,damage,stage);}break;
                case 5:
                    for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Hazard(target.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*3.5f,3.3f,delay,damage*.23f,stage,5);}break;
                case 6:
                    foreach(var player in State.players)if(!player.downed&&!player.eliminated)
                        for(int i=0;i<3;i++)Hazard(player.position+player.velocity*(.3f+i*.2f),2.4f,delay+i*.38f,damage,stage);break;
                default:
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4+boss.attackCycle*.32f;Hazard(boss.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*8,2.7f,delay+(i%2)*.25f,damage,stage);}
                    Hazard(target.position,3.1f,delay+.4f,damage,stage);
                    if(boss.attackCycle%2==0&&State.enemies.Count<GameData.MaxEnemies-3)for(int i=0;i<3;i++)SpawnEnemy(EnemyKind.Stalker);break;
            }
        }
        void Hazard(Vector3 position,float radius,float delay,float damage,int style,float duration=.2f)
        {
            position.y=WorldMap.Ground(position);
            State.hazards.Add(new HazardState{id=State.nextId++,position=position,radius=radius,delay=delay,damage=damage,style=style,life=duration});
        }
        void TickHazards(float dt)
        {
            for(int i=State.hazards.Count-1;i>=0;i--)
            {
                var h=State.hazards[i];h.delay-=dt;if(h.delay>0)continue;
                h.life-=dt;h.tick-=dt;
                if(h.tick<=0)
                {
                    h.tick=.65f;h.triggered=true;
                    foreach(var p in State.players)if(!p.downed&&!p.eliminated&&Vector3.Distance(p.position,h.position)<h.radius&&p.position.y-WorldMap.Ground(p.position)<1.3f)
                    {DamagePlayer(p,h.damage);if(h.style==3)p.slowed=1.5f;}
                    Effect(h.style==3?1:5,h.position,h.radius);
                }
                if(h.life<=0)State.hazards.RemoveAt(i);
            }
        }
    }
}
