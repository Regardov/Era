using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        static bool IsFlying(EnemyKind kind)=>EnemyCatalog.Flying(kind);
        static float EnemyRadius(EnemyState e)=>EnemyCatalog.Radius(e.kind)*(e.summonGeneration>0&&e.kind==EnemyKind.Slime?.6f:1);
        static Vector3 EnemyAim(EnemyState e)=>e.position+Vector3.up*(e.kind==EnemyKind.StoneGolem||e.kind==EnemyKind.Ogre?1.3f:e.kind==EnemyKind.Bat?.2f:e.kind==EnemyKind.Slime||e.kind==EnemyKind.AcidSlime?.5f:.9f);
        EnemyKind RollEnemyKind()
        {
            // A mixed roster is available from the first realm; stronger roles become more common later.
            EnemyKind[] common={EnemyKind.Skeleton,EnemyKind.Wisp,EnemyKind.Slime,EnemyKind.Bat,EnemyKind.Stalker,EnemyKind.SkeletonArcher,EnemyKind.AcidSlime};
            EnemyKind[] advanced={EnemyKind.Revenant,EnemyKind.StoneGolem,EnemyKind.CrystalGolem,EnemyKind.Ogre,EnemyKind.Necromancer,EnemyKind.OgreShaman,EnemyKind.Gargoyle,EnemyKind.Brute};
            return random.NextDouble()<Mathf.Min(.55f,.2f+State.stage*.03f+State.wave*.02f)?advanced[random.Next(advanced.Length)]:common[random.Next(common.Length)];
        }
        void TickEnemies(float dt)
        {
            foreach(var e in new List<EnemyState>(State.enemies))
            {
                if(!State.enemies.Contains(e))continue;
                e.contactTimer=Mathf.Max(0,e.contactTimer-dt);e.recovery=Mathf.Max(0,e.recovery-dt);
                if(e.spawnGrace>0){e.spawnGrace=Mathf.Max(0,e.spawnGrace-dt);continue;}
                if(e.frozen>0){e.frozen=Mathf.Max(0,e.frozen-dt);continue;}
                e.attackTimer-=dt;
                PlayerState nearest=null;float distance=float.MaxValue;
                foreach(var p in State.players)if(!p.downed&&!p.eliminated){var offset=p.position-e.position;offset.y=0;float d=offset.magnitude;if(d<distance){distance=d;nearest=p;}}
                if(nearest==null)continue;
                if(e.kind==EnemyKind.Warden)
                {if(e.windup>0)e.windup-=dt;else if((e.specialTimer-=dt)<=0)BossAttack(e,nearest);}
                if(e.meleeWindup>0)
                {
                    e.meleeWindup=Mathf.Max(0,e.meleeWindup-dt);
                    if(e.meleeWindup<=0){ResolveEnemyAttack(e);e.recovery=e.attackStyle==3?.8f:.42f;}
                    continue;
                }
                if(e.recovery>0)continue;
                if(EnemyCatalog.Summoner(e.kind)&&(e.specialTimer-=dt)<=0)
                {BeginEnemyAttack(e,nearest,2,1.1f);e.specialTimer=e.kind==EnemyKind.Necromancer?9:12;continue;}
                float reach=EnemyCatalog.Reach(e.kind);
                if(e.attackTimer<=0&&distance<reach)
                {
                    int style=e.kind==EnemyKind.Ogre||e.kind==EnemyKind.Brute||e.kind==EnemyKind.StoneGolem||e.kind==EnemyKind.CrystalGolem?3:IsFlying(e.kind)?4:0;
                    float duration=style==3?.9f:style==4?.8f:e.kind==EnemyKind.Revenant?.6f:.4f;
                    BeginEnemyAttack(e,nearest,style,duration);continue;
                }
                Vector3 dir=nearest.position-e.position;dir.y=0;
                if(dir.sqrMagnitude>.01f)e.facing=Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg;
                float pace=e.speed*(Layers(e,Ailment.Chill)>0?.65f:1);
                if(e.kind==EnemyKind.Slime||e.kind==EnemyKind.AcidSlime)pace*=Mathf.Sin(State.elapsed*5+e.id)>.1f?1.6f:.2f;
                if(distance<reach*.8f)dir=Vector3.zero;
                if(e.windup<=0&&dir.sqrMagnitude>.01f)
                {
                    var next=MoveEnemyBody(e,dir.normalized*pace*dt);
                    if((next-e.position).sqrMagnitude<dt*dt*.15f&&distance>reach*1.2f)
                        next=MoveEnemyBody(e,Quaternion.Euler(0,e.id%2==0?65:-65,0)*dir.normalized*pace*dt);
                    e.position=next;
                }
                e.position.y=WorldMap.Ground(e.position)+(IsFlying(e.kind)?1.8f+Mathf.Sin(State.elapsed*3+e.id)*.2f:0);

            }
        }
        void BeginEnemyAttack(EnemyState e,PlayerState target,int style,float duration)
        {
            e.attackStyle=style;e.attackDuration=e.meleeWindup=duration;e.attackPosition=target.position;e.attackDirection=Vector3.ProjectOnPlane(target.position-e.position,Vector3.up).normalized;
            if(e.attackDirection.sqrMagnitude<.01f)e.attackDirection=Vector3.forward;
            e.facing=Mathf.Atan2(e.attackDirection.x,e.attackDirection.z)*Mathf.Rad2Deg;e.attackSerial++;e.attackTimer=style==3?2.3f:1.35f;
        }
        void ResolveEnemyAttack(EnemyState e)
        {
            if(e.attackStyle==2)
            {
                int amount=e.kind==EnemyKind.Necromancer?3:2;
                for(int n=0;n<amount&&State.enemies.Count<GameData.MaxEnemies;n++)
                {var minion=SpawnEnemy(e.kind==EnemyKind.Necromancer?EnemyKind.Skeleton:EnemyKind.Slime);minion.summonGeneration=1;minion.position=WorldMap.Resolve(e.position,Quaternion.Euler(0,n*360f/amount,0)*Vector3.forward*2.5f,.5f);minion.position.y=WorldMap.Ground(minion.position);minion.spawnGrace=.7f;}
                Effect(1,e.position,3);return;
            }
            float reach=EnemyCatalog.Reach(e.kind);
            Vector3 origin=e.position;
            if(e.attackStyle==4){float dive=Vector3.ProjectOnPlane(e.attackPosition-e.position,Vector3.up).magnitude;e.position=MoveEnemyBody(e,e.attackDirection*Mathf.Min(4,dive));e.position.y=WorldMap.Ground(e.position)+.7f;origin=e.position;reach=2.4f;}
            Vector3 end=origin+e.attackDirection*reach;
            foreach(var p in State.players)
            {
                if(p.downed||p.eliminated)continue;
                Vector3 delta=p.position-origin;float height=p.position.y-WorldMap.Ground(p.position);delta.y=0;
                // Attacks use the locked direction and actual weapon/body segment; sidestepping the windup works.
                bool inShape=e.attackStyle==3?Vector3.Distance(p.position,e.attackPosition)<3.1f&&delta.magnitude<reach+1:SegmentDistance(origin+Vector3.up*.9f,end+Vector3.up*.9f,p.position+Vector3.up*.9f)<.9f+EnemyRadius(e)*.3f&&Vector3.Dot(delta.normalized,e.attackDirection)>-.1f;
                if(inShape&&height<(e.attackStyle==4?2.4f:1.3f))DamagePlayer(p,e.damage,e.position);
            }
            if(e.attackStyle==3){Effect(5,e.attackPosition,3);if(e.kind==EnemyKind.StoneGolem)Hazard(e.attackPosition,3,.25f,e.damage*.5f,4);}
            else WeaponEffect(9,origin+Vector3.up*.8f,end+Vector3.up*.8f,reach,WeaponKind.Sword);
        }
        void OnEnemyDeath(EnemyState e)
        {
            if(e.kind==EnemyKind.Slime&&e.summonGeneration==0)
                for(int n=0;n<2&&State.enemies.Count<GameData.MaxEnemies;n++)
                {var child=SpawnEnemy(EnemyKind.Slime);child.summonGeneration=1;child.hp=child.maxHp=e.maxHp*.3f;child.position=WorldMap.Resolve(e.position,Vector3.right*(n==0?-1:1),.4f);child.position.y=WorldMap.Ground(child.position);child.spawnGrace=.4f;child.damage*=.6f;child.speed*=1.3f;}
            if(e.kind==EnemyKind.AcidSlime)Hazard(e.position,2.8f,.8f,e.damage*.35f,5,4);
        }
    }
}
