using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        static void GiveStarterWeapon(PlayerState p)
        {
            p.weapons=new[]{(int)(p.hero==Hero.Ember?WeaponKind.Bow:WeaponKind.LightningStaff),-1,-1,-1};
            p.weaponLevels=new[]{1,0,0,0};p.weaponTimers=new float[GameData.WeaponSlots];p.lastWeapon=p.weapons[0];
        }
        public int GiveWeapon(PlayerState p,WeaponKind kind,int quantity=1)
        {
            if((int)kind<0||(int)kind>=GameData.Weapons.Length||quantity<1)return -1;
            int slot=System.Array.IndexOf(p.weapons,(int)kind);
            if(slot<0)
            {
                slot=System.Array.IndexOf(p.weapons,-1);if(slot<0)return -1;
                p.weapons[slot]=(int)kind;p.weaponLevels[slot]=0;
            }
            int available=GameData.MaxWeaponLevel-p.weaponLevels[slot],given=Mathf.Min(available,quantity);
            p.weaponLevels[slot]+=given;p.weaponTimers[slot]=Mathf.Min(p.weaponTimers[slot],.12f);
            if(quantity>given)State.coins+=(quantity-given)*8;
            return slot;
        }
        void TickWeapons(PlayerState p,float dt)
        {
            for(int slot=0;slot<GameData.WeaponSlots;slot++)
            {
                int type=p.weapons[slot];if(type<0)continue;
                p.weaponTimers[slot]-=dt;if(p.weaponTimers[slot]>0)continue;
                var definition=GameData.Weapons[type];float range=definition.range+(p.weaponRange-15);
                var targets=new List<EnemyState>(State.enemies);
                targets.RemoveAll(e=>Vector3.Distance(p.position,e.position)>range);
                if(targets.Count==0){p.weaponTimers[slot]=.12f;continue;}
                targets.Sort((a,b)=>(a.position-p.position).sqrMagnitude.CompareTo((b.position-p.position).sqrMagnitude));
                int level=Mathf.Max(1,p.weaponLevels[slot]);
                float damage=definition.damage*p.damage*(1+(level-1)*.22f);
                p.weaponTimers[slot]=definition.cooldown/((1+(level-1)*.06f)*p.attackSpeed*(p.boost>0?2:1));
                p.weaponCastSerial++;p.lastWeapon=type;p.weaponPoseTime=.32f;
                Vector3 origin=p.position+Vector3.up*1.05f;
                Vector3 aim=(targets[0].position+Vector3.up*.7f-origin).normalized;
                switch((WeaponKind)type)
                {
                    case WeaponKind.Axe:
                        Vector3 forward=targets[0].position-p.position;forward.y=0;forward.Normalize();
                        foreach(var enemy in targets)
                        {
                            var direction=enemy.position-p.position;direction.y=0;
                            if(Vector3.Dot(direction.normalized,forward)<-.15f)continue;
                            DamageEnemy(enemy,p,damage);
                        }
                        WeaponEffect(9,p.position+Vector3.up*.8f,p.position+forward*range+Vector3.up*.8f,range,WeaponKind.Axe);
                        break;
                    case WeaponKind.LightningStaff:
                        Vector3 previous=origin;var chain=new List<int>();
                        int count=Mathf.Min(4,2+p.projectileCount+level/3);
                        for(int bounce=0;bounce<count;bounce++)
                        {
                            EnemyState next=null;float distance=bounce==0?range:7;
                            foreach(var enemy in State.enemies)
                            {if(chain.Contains(enemy.id))continue;float d=Vector3.Distance(previous,enemy.position+Vector3.up*.8f);if(d<distance){distance=d;next=enemy;}}
                            if(next==null)break;
                            chain.Add(next.id);var end=next.position+Vector3.up*.8f;
                            WeaponEffect(10,previous,end,1,WeaponKind.LightningStaff);DamageEnemy(next,p,damage*Mathf.Pow(.85f,bounce));previous=end;
                        }
                        break;
                    case WeaponKind.Bow:
                        int arrows=Mathf.Min(p.projectileCount,targets.Count);
                        for(int i=0;i<arrows;i++)
                        {
                            var enemy=targets[i];float flight=Vector3.Distance(origin,enemy.position)/34;
                            var lead=(p.position-enemy.position).normalized*enemy.speed*flight*.7f;
                            var direction=(enemy.position+Vector3.up*.75f+lead-origin).normalized;
                            State.projectiles.Add(new ProjectileState{id=State.nextId++,owner=p.id,target=enemy.id,position=origin,direction=direction,damage=damage,weapon=WeaponKind.Bow,life=1.5f});
                        }
                        break;
                    case WeaponKind.Spear:
                        var endPoint=origin+aim*range;
                        foreach(var enemy in targets)
                            if(SegmentDistance(origin,endPoint,enemy.position+Vector3.up*.75f)<1.05f)DamageEnemy(enemy,p,damage);
                        WeaponEffect(11,origin,endPoint,range,WeaponKind.Spear);break;
                }
            }
        }
        void WeaponEffect(int kind,Vector3 origin,Vector3 end,float radius,WeaponKind weapon)
        {State.effects.Add(new EffectState{id=State.nextId++,kind=kind,position=origin,end=end,radius=radius,weapon=weapon,life=.4f});}
        void RollChestReward(ChestState chest,PlayerState owner)
        {
            var missing=new List<int>();for(int i=0;i<GameData.Weapons.Length;i++)if(System.Array.IndexOf(owner.weapons,i)<0)missing.Add(i);
            bool weapon=missing.Count>0||random.NextDouble()<.5;
            chest.rewardKind=weapon?RewardKind.Weapon:RewardKind.Item;
            chest.reward=weapon?(missing.Count>0?missing[random.Next(missing.Count)]:random.Next(GameData.Weapons.Length)):random.Next(GameData.Items.Length);
            State.loot.Add(new LootState{id=State.nextId++,owner=owner.id,content=chest.reward,kind=chest.rewardKind,quantity=chest.tier>0?2:1,origin=chest.position,position=chest.position+Vector3.up*.8f});
        }
        void TickLoot(float dt)
        {
            for(int i=State.loot.Count-1;i>=0;i--)
            {
                var drop=State.loot[i];drop.age+=dt;var owner=State.Player(drop.owner);
                if(owner==null||owner.eliminated)
                {owner=State.players.Find(p=>!p.downed&&!p.eliminated);if(owner!=null)drop.owner=owner.id;}
                if(!drop.collecting)
                {
                    float t=Mathf.Clamp01(drop.age/.75f);
                    drop.position=drop.origin+new Vector3(.8f*t,.8f+Mathf.Sin(t*Mathf.PI)*1.7f,0);
                    if(drop.age>1.05f)drop.position.y=drop.origin.y+.8f+Mathf.Sin(drop.age*3)*.08f;
                    if(drop.age>1.1f&&owner!=null&&!owner.downed)
                    {drop.collecting=true;drop.collectOrigin=drop.position;}
                }
                else
                {
                    if(owner==null||owner.downed){drop.collecting=false;drop.collectTime=0;continue;}
                    drop.collectTime+=dt;float t=Mathf.Clamp01(drop.collectTime/.35f);
                    drop.position=Vector3.Lerp(drop.collectOrigin,owner.position+Vector3.up*1.1f,t*t);
                    if(t<1)continue;
                    if(drop.kind==RewardKind.Weapon)GiveWeapon(owner,(WeaponKind)drop.content,drop.quantity);
                    else for(int n=0;n<drop.quantity;n++)ApplyItem(owner,drop.content);
                    Reward(owner,drop.kind,drop.content,drop.quantity);
                    if(drop.quantity>1)GrantExperience(10);
                    State.noticeKind=drop.kind==RewardKind.Weapon?3:2;State.noticeOwner=owner.id;State.noticeContent=drop.content;State.noticeQuantity=drop.quantity;State.noticeTime=3;
                    Effect(3,owner.position,1.5f);State.loot.RemoveAt(i);
                }
            }
        }
    }
}
