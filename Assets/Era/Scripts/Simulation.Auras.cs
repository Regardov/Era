using System;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        public static float AuraRadius(PlayerState p)
        {
            int slot=Array.IndexOf(p.weapons,(int)WeaponKind.Aura);
            return slot<0?0:Mathf.Max(3,(12+(p.weaponLevels[slot]-1)*1.5f+p.weaponRange-15)*p.areaSize+(p.auraSurge>0?6:0))/1.3f;
        }

        void TickAuraDamage(PlayerState p,float dt)
        {
            if(p.hero!=Hero.Lumen||AuraRadius(p)<=0)return;
            p.auraDamageTimer+=dt;
            // Four pulses per second keep damage independent of FPS and enemy count.
            while(p.auraDamageTimer>=.25f)
            {
                p.auraDamageTimer-=.25f;float radius=AuraRadius(p);
                foreach(var enemy in new System.Collections.Generic.List<EnemyState>(State.enemies))
                    if(Vector3.Distance(p.position,enemy.position)<=radius)DamageEnemy(enemy,p,2.5f*DamageMultiplier(p));
            }
        }

        public bool LearnAura(PlayerState p,int aura)
        {
            if(p.hero!=Hero.Lumen||aura<0||aura>=p.auraLevels.Length)return false;
            if(p.auraLevels[aura]==0&&Array.FindAll(p.auraLevels,n=>n>0).Length>=GameData.MaxAuras)return false;
            p.auraLevels[aura]++;p.currentAura=aura;RefreshAuras();Reward(p,RewardKind.Aura,aura,1);return true;
        }

        // Recompute from sources every tick: leaving range, death and disconnect remove buffs.
        // Identical auras use the strongest source so multiple Lumens cannot compound them.
        public void RefreshAuras()
        {
            foreach(var target in State.players)
            {
                Array.Clear(target.auraBonuses,0,target.auraBonuses.Length);
                if(!target.downed&&!target.eliminated)
                    foreach(var source in State.players)
                    {
                        if(source.hero!=Hero.Lumen||source.downed||source.eliminated)continue;
                        float radius=AuraRadius(source);if(radius<=0||Vector3.Distance(source.position,target.position)>radius)continue;
                        for(int a=0;a<source.auraLevels.Length;a++)if(source.auraLevels[a]>0)
                            target.auraBonuses[a]=Mathf.Max(target.auraBonuses[a],GameData.AuraPower(a,source.auraLevels[a])*(source.auraSurge>0?1.5f:1));
                    }
                float baseHp=target.maxHp-target.auraMaxHp;
                target.auraMaxHp=baseHp*target.auraBonuses[3];target.maxHp=baseHp+target.auraMaxHp;
                target.hp=Mathf.Min(target.hp,target.maxHp);
            }
        }

        public void ApplyAccessory(PlayerState p,int stat,int rarity)
        {
            if(stat<0||stat>=GameData.Attributes.Length)return;
            rarity=Mathf.Clamp(rarity,0,3);p.accessories[stat*4+rarity]++;
            ApplyAttribute(p,stat,rarity);Reward(p,RewardKind.Accessory,stat,1,rarity);
        }

        void AddHit(int target,Vector3 position,float damage,bool critical,bool player)
        {
            if(State.hits.Count>=160)State.hits.RemoveAt(0);
            State.hits.Add(new HitState{id=State.nextId++,target=target,position=position,damage=damage,critical=critical,player=player});
        }
    }
}
