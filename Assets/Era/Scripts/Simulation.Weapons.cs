using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        void TickWeapons(PlayerState p,float dt)
        {
            if(WeaponsSuppressed(p)){p.bursts.Clear();return;}
            foreach(var burst in new List<WeaponBurstState>(p.bursts))
            {
                burst.delay-=dt;
                // Sub-frame intervals retain all shots; 48 is the shared projectile safety limit.
                while(burst.delay<=0&&burst.remaining>0)
                {FireWeaponShot(p,burst);burst.remaining--;burst.delay+=Mathf.Max(.001f,burst.interval);}
                if(burst.remaining<=0)p.bursts.Remove(burst);
            }
            for(int slot=0;slot<GameData.WeaponSlots;slot++)
            {
                int type=p.weapons[slot];if(type<0||type==(int)WeaponKind.Aura)continue;
                p.weaponTimers[slot]-=dt;
                if(p.weaponTimers[slot]>0||p.bursts.Exists(b=>b.slot==slot))continue;
                if(Has(p,ItemId.LivingArsenal)&&p.weapons[1]>=0&&p.weapons[0]!=(int)WeaponKind.Aura&&slot!=Mathf.FloorToInt(p.combatTime/5)%2)continue;
                var definition=GameData.Weapons[type];float range=Mathf.Max(1,definition.range+p.weaponRange-15);
                var targets=WeaponTargets(p,range);
                if(targets.Count==0)
                {
                    ResourceNodeState node=null;float nearest=Mathf.Min(10,range);
                    foreach(var candidate in State.resourceNodes)if(!candidate.broken){float distance=Vector3.Distance(p.position,candidate.position);if(distance<nearest){nearest=distance;node=candidate;}}
                    if(node!=null)
                    {
                        float resourceDamage=GameData.WeaponBaseDamage(p.hero,(WeaponKind)type)*DamageMultiplier(p);p.weaponTimers[slot]=definition.cooldown/Mathf.Max(.1f,p.attackSpeed);WeaponPose(p,(WeaponKind)type,node.position);
                        DamageResource(node,resourceDamage,p);WeaponEffect(10,p.position+Vector3.up,node.position+Vector3.up*.45f,nearest,(WeaponKind)type);
                    }
                    else p.weaponTimers[slot]=.12f;
                    continue;
                }
                int level=Mathf.Max(1,p.weaponLevels[slot]);
                if(type==(int)WeaponKind.Bow&&State.projectiles.Count+VolleyCount(p)>GameData.MaxProjectiles){p.weaponTimers[slot]=.05f;continue;}
                float speed=p.attackSpeed*(1+p.auraBonuses[2])*(1+Mathf.Min(.6f,.03f*p.itemCounters[I(ItemId.WarDrum)]*Power(p,ItemId.WarDrum)))*(p.boost>0?2:1);
                p.weaponTimers[slot]=definition.cooldown/Mathf.Max(.1f,(1+(level-1)*.06f)*speed);
                float damage=GameData.WeaponBaseDamage(p.hero,(WeaponKind)type)*DamageMultiplier(p)*(1+(level-1)*.22f)*OnItemVolley(p,slot,targets[0]);
                int count=VolleyCount(p);var shot=new WeaponBurstState{slot=slot,weapon=(WeaponKind)type,total=count,remaining=count,range=range,damage=damage,interval=ItemCatalog.BurstInterval(count)};
                if(type==(int)WeaponKind.Spear||type==(int)WeaponKind.Sword||type==(int)WeaponKind.Axe)
                {FireWeaponShot(p,shot);shot.remaining--;if(shot.remaining>0){shot.delay=shot.interval;p.bursts.Add(shot);}}
                else
                    for(int n=0;n<count;n++){FireWeaponShot(p,shot);shot.remaining--;}
            }
        }
        List<EnemyState> WeaponTargets(PlayerState p,float range)
        {
            var targets=State.enemies.FindAll(e=>e.hp>0&&Vector3.Distance(p.position,e.position)<=range+EnemyRadius(e)*.25f);
            targets.Sort((a,b)=>(a.position-p.position).sqrMagnitude.CompareTo((b.position-p.position).sqrMagnitude));return targets;
        }
        void WeaponPose(PlayerState p,WeaponKind kind,Vector3 target)
        {
            p.weaponCastSerial++;p.lastWeapon=(int)kind;p.weaponPoseTime=.32f;
            var direction=target-p.position;p.weaponAimYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
        }
        void FireWeaponShot(PlayerState p,WeaponBurstState shot)
        {
            var targets=WeaponTargets(p,shot.range);if(targets.Count==0)return;
            // Prefer a new primary target per projectile, then wrap only after all available targets.
            var target=targets.Find(e=>!shot.usedTargets.Contains(e.id));
            if(target==null){shot.usedTargets.Clear();target=targets[0];}
            shot.usedTargets.Add(target.id);WeaponPose(p,shot.weapon,target.position);
            Vector3 origin=p.hero==Hero.Ember&&shot.weapon==WeaponKind.Bow?ArcherShotProfile.Origin(p.position,p.weaponAimYaw):p.position+Vector3.up*1.35f,aim=(EnemyAim(target)-origin).normalized;
            if(shot.weapon==WeaponKind.Bow)
            {
                float flight=Vector3.Distance(origin,EnemyAim(target))/(34*p.projectileSpeed);
                var lead=(p.position-target.position).normalized*target.speed*flight*.4f;
                State.projectiles.Add(new ProjectileState{id=State.nextId++,owner=p.id,target=target.id,position=origin,direction=(EnemyAim(target)+lead-origin).normalized,damage=shot.damage,weapon=WeaponKind.Bow,life=2.5f,
                    pierce=0,bounces=Mathf.Min(4,Stack(p,ItemId.RicochetSeed))});
            }
            else if(shot.weapon==WeaponKind.LightningStaff)
            {
                var chain=new HashSet<int>();Vector3 previous=origin;EnemyState next=target;
                int count=1+Mathf.Min(4,Stack(p,ItemId.RicochetSeed))+GameData.ChanceRicochets(GameData.LightningRicochetPercent(p),random.NextDouble());
                for(int bounce=0;bounce<count&&next!=null;bounce++)
                {
                    chain.Add(next.id);Vector3 end=EnemyAim(next);WeaponEffect(10,previous,end,1,WeaponKind.LightningStaff);DamageEnemy(next,p,shot.damage*Mathf.Pow(.65f,bounce),true,true);previous=end;
                    next=null;float nearest=7;
                    foreach(var candidate in State.enemies)if(!chain.Contains(candidate.id)){float d=Vector3.Distance(previous,EnemyAim(candidate));if(d<nearest){nearest=d;next=candidate;}}
                }
            }
            else if(shot.weapon==WeaponKind.Spear)
            {
                var end=origin+aim*shot.range;
                DamageEnemy(target,p,shot.damage,true,true);
                WeaponEffect(11,origin,end,shot.range,WeaponKind.Spear);
            }
            else
            {
                var forward=Vector3.ProjectOnPlane(aim,Vector3.up).normalized;
                DamageEnemy(target,p,shot.damage,true,true);
                WeaponEffect(9,p.position+Vector3.up*.8f,p.position+forward*shot.range+Vector3.up*.8f,shot.range,shot.weapon);
            }
        }
        void TickProjectiles(float dt)
        {
            foreach(var b in new List<ProjectileState>(State.projectiles))
            {
                if(!State.projectiles.Contains(b))continue;
                var owner=State.Player(b.owner);b.life-=dt;
                if(b.hostile||owner==null||b.life<=0){State.projectiles.Remove(b);continue;}
                if(b.returning)
                {
                    var catchPoint=owner.position+Vector3.up;
                    var returned=Vector3.MoveTowards(b.position,catchPoint,b.speed*owner.projectileSpeed*dt);
                    b.direction=(catchPoint-b.position).normalized;b.position=returned;
                    if(Vector3.Distance(returned,catchPoint)<.65f)
                    {
                        int i=I(ItemId.MobiusQuiver);owner.itemCounters[i]++;
                        if(owner.itemCounters[i]>=3){owner.itemCounters[i]-=3;for(int slot=0;slot<owner.weaponTimers.Length;slot++)owner.weaponTimers[slot]=Mathf.Max(0,owner.weaponTimers[slot]-Mathf.Min(.75f,.25f*Power(owner,ItemId.MobiusQuiver)));}
                        State.projectiles.Remove(b);
                    }
                    continue;
                }
                var target=State.enemies.Find(e=>e.id==b.target&&e.hp>0&&!b.hitTargets.Contains(e.id));
                if(target==null)
                {
                    var reserved=new HashSet<int>();foreach(var other in State.projectiles)if(other!=b&&other.owner==b.owner&&!other.returning)reserved.Add(other.target);
                    target=State.enemies.Find(e=>!reserved.Contains(e.id)&&!b.hitTargets.Contains(e.id)&&Vector3.Distance(e.position,b.position)<25)??State.enemies.Find(e=>!b.hitTargets.Contains(e.id)&&Vector3.Distance(e.position,b.position)<25);
                    b.target=target?.id??-1;
                }
                if(target!=null)b.direction=Vector3.RotateTowards(b.direction,(EnemyAim(target)-b.position).normalized,dt*8,0).normalized;
                var end=b.position+b.direction*b.speed*owner.projectileSpeed*dt;
                EnemyState impact=null;float first=float.MaxValue;
                foreach(var e in State.enemies)
                {
                    if(b.hitTargets.Contains(e.id)||SegmentDistance(b.position,end,EnemyAim(e))>EnemyRadius(e))continue;
                    float along=Vector3.Dot(EnemyAim(e)-b.position,b.direction);
                    if(along<first){first=along;impact=e;}
                }
                if(impact!=null)
                {
                    b.hitTargets.Add(impact.id);b.position=EnemyAim(impact);DamageEnemy(impact,owner,b.damage,true,true);
                    var next=b.bounces>0?FindItemTarget(b.position,10,impact.id):null;
                    if(next!=null&&b.hitTargets.Contains(next.id))next=State.enemies.Find(e=>!b.hitTargets.Contains(e.id)&&Vector3.Distance(e.position,b.position)<10);
                    if(next!=null){b.bounces--;b.target=next.id;b.direction=(EnemyAim(next)-b.position).normalized;b.damage*=.65f;}
                    else if(Has(owner,ItemId.MobiusQuiver)){b.returning=true;b.life=2;}
                    else State.projectiles.Remove(b);
                    continue;
                }
                bool hitResource=false;
                foreach(var node in State.resourceNodes)if(!node.broken&&SegmentDistance(b.position,end,node.position+Vector3.up*.45f)<.8f){DamageResource(node,b.damage,owner);hitResource=true;break;}
                b.position=end;
                if(hitResource)State.projectiles.Remove(b);
                else if(Has(owner,ItemId.MobiusQuiver)&&b.life<.7f){b.returning=true;b.life=2;}
            }
        }
    }
}
