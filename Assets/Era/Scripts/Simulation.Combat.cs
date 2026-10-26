using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        static void GiveStarterWeapon(PlayerState p)
        {
            p.weapons=new[]{(int)(p.hero==Hero.Lumen?WeaponKind.Aura:p.hero==Hero.Ember?WeaponKind.Bow:WeaponKind.LightningStaff),-1};
            p.weaponLevels=new[]{1,0};p.weaponTimers=new float[GameData.WeaponSlots];p.lastWeapon=p.weapons[0];
        }
        public int GiveWeapon(PlayerState p,WeaponKind kind,int quantity=1)
        {
            if((int)kind<0||(int)kind>=GameData.Weapons.Length||quantity<1)return -1;
            int slot=System.Array.IndexOf(p.weapons,(int)kind);
            if(slot<0)
            {
                if(State.level<GameData.SecondWeaponLevel||kind==WeaponKind.Aura)return -1;
                slot=System.Array.IndexOf(p.weapons,-1);if(slot<0)return -1;
                p.weapons[slot]=(int)kind;p.weaponLevels[slot]=0;
            }
            int available=GameData.MaxWeaponLevel-p.weaponLevels[slot],given=Mathf.Min(available,quantity);
            p.weaponLevels[slot]+=given;p.weaponTimers[slot]=Mathf.Min(p.weaponTimers[slot],.12f);
            if(quantity>given)State.coins+=(quantity-given)*8;
            return slot;
        }
        void WeaponEffect(int kind,Vector3 origin,Vector3 end,float radius,WeaponKind weapon,FeedbackStyle style=FeedbackStyle.None)
        {if(State.effects.Count>=GameData.MaxEffects)State.effects.RemoveAt(0);State.effects.Add(new EffectState{id=State.nextId++,kind=kind,position=origin,end=end,radius=radius,weapon=weapon,life=.4f,style=style});}
        void RollChestReward(ChestState chest,PlayerState owner)
        {
            chest.rewardKind=RewardKind.Item;
            chest.rewardRarity=RollRarity(owner,chest.tier);
            var pool=new List<int>();for(int i=0;i<GameData.Items.Length;i++)if((int)GameData.Items[i].rarity==chest.rewardRarity)pool.Add(i);
            chest.reward=pool[random.Next(pool.Count)];
            var floor=WorldMap.Resolve(chest.position,Vector3.forward*1.7f,.3f);floor.y=WorldMap.Ground(floor);
            State.loot.Add(new LootState{id=State.nextId++,owner=-1,content=chest.reward,kind=chest.rewardKind,quantity=1,origin=floor,position=floor+Vector3.up*.55f});
        }
        public bool TryCollectLoot(PlayerState player)
        {
            if(State.phase!=RunPhase.Playing||player.downed||player.eliminated)return false;
            LootState closest=null;float range=2.5f;
            foreach(var drop in State.loot)
            {
                float d=Vector3.Distance(player.position,drop.position);
                if(!drop.collecting&&d<range){range=d;closest=drop;}
            }
            if(closest==null)return false;
            closest.owner=player.id;closest.collecting=true;closest.collectTime=0;closest.collectOrigin=closest.position;return true;
        }
        void TickLoot(float dt)
        {
            for(int i=State.loot.Count-1;i>=0;i--)
            {
                var drop=State.loot[i];drop.age+=dt;var owner=State.Player(drop.owner);
                if(!drop.collecting)
                {
                    drop.position=drop.origin+Vector3.up*(.55f+Mathf.Sin(drop.age*3)*.06f);
                }
                else
                {
                    if(owner==null||owner.downed||owner.eliminated){drop.collecting=false;drop.owner=-1;drop.collectTime=0;drop.position=drop.origin+Vector3.up*.55f;continue;}
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
