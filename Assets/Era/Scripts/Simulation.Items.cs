using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        static int Stack(PlayerState p,ItemId item)=>p==null?0:p.items[(int)item];
        static int I(ItemId item)=>(int)item;
        static bool Has(PlayerState p,ItemId item)=>Stack(p,item)>0;
        static float Power(PlayerState p,ItemId item)=>1+.6f*Mathf.Log(Mathf.Max(1,Stack(p,item)));
        bool Ready(PlayerState p,ItemId item,float cooldown)
        {int i=I(item);if(!Has(p,item)||p.itemTimers[i]>0)return false;p.itemTimers[i]=cooldown;return true;}
        void PayHealth(PlayerState p,float value)=>p.hp=Mathf.Max(1,p.hp-value);
        public void Heal(PlayerState p,float value,bool lifesteal=false)
        {
            if(p==null||p.downed||p.eliminated||value<=0)return;
            value*=p.healingPower;
            if(Has(p,ItemId.InversionHeart)){p.shield=Mathf.Min(800,p.shield+value);return;}
            float excess=Mathf.Max(0,p.hp+value-p.maxHp);p.hp=Mathf.Min(p.maxHp,p.hp+value);
            if(lifesteal&&Has(p,ItemId.ParasiteCrown))
            {
                int i=I(ItemId.ParasiteCrown);p.itemValues[i]+=excess;
                if(p.itemValues[i]>=15){p.itemValues[i]-=15;var target=FindItemTarget(p.position,24);if(target!=null){DamageEnemy(target,p,50*Stack(p,ItemId.ParasiteCrown)*DamageMultiplier(p),false);WeaponEffect(10,p.position+Vector3.up,target.position+Vector3.up,1,WeaponKind.LightningStaff);}}
            }
        }
        EnemyState FindItemTarget(Vector3 origin,float range,int except=-1,bool farthest=false)
        {
            EnemyState found=null;float best=farthest?0:range*range;
            foreach(var e in State.enemies){if(e.id==except||e.hp<=0)continue;float d=(e.position-origin).sqrMagnitude;if(d>range*range)continue;if(farthest?d>=best:d<best){found=e;best=d;}}return found;
        }
        int Layers(EnemyState e,Ailment kind,int owner=-1)
        {if(EnemyCatalog.EffectImmune(e.kind))return 0;int total=0;foreach(var a in e.ailments)if(a.kind==kind&&a.time>0&&(owner<0||a.owner==owner))total+=a.stacks;return total;}
        void FreezeEnemy(EnemyState e,float duration)
        {if(!EnemyCatalog.EffectImmune(e.kind))e.frozen=Mathf.Max(e.frozen,duration);}
        void Afflict(EnemyState e,PlayerState p,Ailment kind,float time,float power=0,int maxStacks=1)
        {
            if(e==null||p==null||e.hp<=0||EnemyCatalog.EffectImmune(e.kind))return;
            var a=e.ailments.Find(s=>s.kind==kind&&s.owner==p.id);
            if(a==null){a=new AfflictionState{kind=kind,owner=p.id};e.ailments.Add(a);}else a.stacks=Mathf.Min(maxStacks,a.stacks+1);
            a.time=Mathf.Max(a.time,time);a.power=Mathf.Max(a.power,power);
        }
        void Burst(PlayerState p,Vector3 position,float radius,float damage,Ailment? ailment=null,float duration=3)
        {
            foreach(var e in new List<EnemyState>(State.enemies))if(Vector3.Distance(position,e.position)<=radius*p.areaSize+EnemyRadius(e)*.3f)
            {if(ailment.HasValue)Afflict(e,p,ailment.Value,duration,ailment==Ailment.Burn?6:ailment==Ailment.Poison?4:0,5);DamageEnemy(e,p,damage,false);}
            Effect(ailment==Ailment.Chill?1:4,position,radius*p.areaSize);
        }
        ItemFieldState Field(PlayerState p,ItemId item,Vector3 position,float time,float radius,float power,int target=-1)
        {
            if((item==ItemId.GravePact||item==ItemId.HuntingPack||item==ItemId.HiveMind)&&State.itemFields.FindAll(f=>f.owner==p.id&&(f.item==I(ItemId.GravePact)||f.item==I(ItemId.HuntingPack)||f.item==I(ItemId.HiveMind))).Count>=6)return null;
            // Bound persistent proc entities per owner, keeping multiplayer snapshots predictable.
            if(State.itemFields.FindAll(f=>f.owner==p.id).Count>=70)return null;
            var field=new ItemFieldState{id=State.nextId++,owner=p.id,item=I(item),position=position,time=time,radius=radius,power=power,target=target};
            State.itemFields.Add(field);return field;
        }
        void TickAfflictions(float dt)
        {
            foreach(var e in new List<EnemyState>(State.enemies))
            {
                if(EnemyCatalog.EffectImmune(e.kind)){e.frozen=0;e.ailments.Clear();continue;}
                foreach(var a in new List<AfflictionState>(e.ailments))
                {
                    a.time-=dt;a.tick-=dt;
                    if(a.kind==Ailment.Doom&&e.frozen<=0)
                    {var owner=State.Player(a.owner);float power=a.power;a.power=0;a.time=0;if(owner!=null)Burst(owner,e.position,5,power);}
                    if(a.tick<=0&&a.power>0&&(a.kind==Ailment.Burn||a.kind==Ailment.Poison||a.kind==Ailment.Bleed))
                    {a.tick=.5f;DamageEnemy(e,State.Player(a.owner),a.power*a.stacks*.5f,false);}
                    if(!State.enemies.Contains(e))break;
                }
                e.ailments.RemoveAll(a=>a.time<=0);
            }
        }
        void TickItemFields(float dt)
        {
            foreach(var f in new List<ItemFieldState>(State.itemFields))
            {
                if(!State.itemFields.Contains(f))continue;
                var p=State.Player(f.owner);if(p==null){State.itemFields.Remove(f);continue;}
                if(p.downed||p.eliminated)continue;
                f.time-=dt;f.tick-=dt;var item=(ItemId)f.item;
                if(item==ItemId.TimeMaster||item==ItemId.SplitSoul)
                {
                    if(f.time>0)continue;var target=State.enemies.Find(e=>e.id==f.target);
                    if(target!=null&&Vector3.Distance(f.position,target.position)<40){WeaponEffect(10,f.position+Vector3.up,target.position+Vector3.up,1,WeaponKind.LightningStaff);DamageEnemy(target,p,f.power,false);}
                }
                else if(item==ItemId.MiserFuse||item==ItemId.PacifistEngine)
                {
                    if(!f.armed){f.tick=.8f;f.armed=true;}
                    if(f.tick<=0&&FindItemTarget(f.position,2.5f)!=null){Burst(p,f.position,f.radius,f.power);f.time=0;}
                }
                else if(item==ItemId.BlackHole||item==ItemId.ChaosPortal||item==ItemId.VoidShepherd)
                {
                    foreach(var e in new List<EnemyState>(State.enemies))if(!EnemyCatalog.EffectImmune(e.kind)&&(e.position-f.position).sqrMagnitude<f.radius*f.radius)
                    {var pull=f.position-e.position;pull.y=0;e.position=MoveEnemyBody(e,pull.normalized*Mathf.Min(pull.magnitude,4*dt));e.position.y=WorldMap.Ground(e.position)+(IsFlying(e.kind)?1.8f:0);}
                    if(f.time<=0&&item!=ItemId.ChaosPortal)Burst(p,f.position,f.radius,f.power);
                }
                else if(item==ItemId.StarMap||item==ItemId.EndlessRefrain)
                {if(f.time<=0)Burst(p,f.position,f.radius,f.power);}
                else if(item==ItemId.HuntingPack||item==ItemId.GravePact||item==ItemId.HiveMind||item==ItemId.ArchitectReign)
                {
                    var target=State.enemies.Find(e=>e.id==f.target&&e.hp>0&&(e.position-f.position).sqrMagnitude<625);
                    if(target==null)
                    {
                        var reserved=new HashSet<int>();foreach(var other in State.itemFields)if(other!=f&&other.owner==f.owner&&other.item==f.item)reserved.Add(other.target);
                        target=State.enemies.Find(e=>!reserved.Contains(e.id)&&(e.position-f.position).sqrMagnitude<625)??FindItemTarget(f.position,25);f.target=target?.id??-1;
                    }
                    if(item!=ItemId.ArchitectReign)
                    {
                        Vector3 follow=target?.position??p.position;
                        if((follow-f.position).sqrMagnitude>2)f.position=Vector3.MoveTowards(f.position,follow,dt*(item==ItemId.GravePact?9:12));
                        f.position.y=WorldMap.Ground(f.position)+.8f;
                    }
                    if(target!=null&&f.tick<=0&&(item==ItemId.ArchitectReign||(f.position-target.position).sqrMagnitude<12))
                    {
                        f.tick=1;float power=f.power*DamageMultiplier(p)*(item==ItemId.ArchitectReign?1+p.itemValues[I(ItemId.ArchitectReign)]:1);
                        WeaponEffect(10,f.position+Vector3.up,target.position+Vector3.up,1,WeaponKind.LightningStaff);
                        if(item==ItemId.HiveMind)Afflict(target,p,Ailment.Poison,5,6*Stack(p,ItemId.HiveMind),8);
                        DamageEnemy(target,p,power,false);
                    }
                }
                else if(f.tick<=0)
                {
                    f.tick=.5f;
                    bool root=item==ItemId.Overgrowth||item==ItemId.WorldSeed;
                    Burst(p,f.position,f.radius,f.power*.5f,root?Ailment.Chill:Ailment.Burn,2);
                }
                if(f.time<=0)State.itemFields.Remove(f);
            }
        }
        void TickItems(PlayerState p,float dt)
        {
            for(int i=0;i<p.itemTimers.Length;i++)p.itemTimers[i]=Mathf.Max(0,p.itemTimers[i]-dt);
            float distance=new Vector2(p.velocity.x,p.velocity.z).magnitude*dt;
            p.distanceWalked+=distance;p.combatTime+=dt;p.safeTime+=dt;
            p.stillTime=distance<.015f?p.stillTime+dt:0;
            if(Has(p,ItemId.GlassWorld)){p.maxHp=p.hp=1;p.auraMaxHp=0;}
            if(Has(p,ItemId.RealityCube))
            {
                int phase=Mathf.FloorToInt(p.combatTime/12)%2;
                if(phase==1){float before=p.hp+p.shield;Heal(p,p.maxHp*.03f*Stack(p,ItemId.RealityCube)*dt);p.itemValues[I(ItemId.RealityCube)]+=Mathf.Max(0,p.hp+p.shield-before);}
                else if(p.itemValues[I(ItemId.RealityCube)]>0){float power=p.itemValues[I(ItemId.RealityCube)]*4;p.itemValues[I(ItemId.RealityCube)]=0;Burst(p,p.position,10,power);}
            }
            if(Ready(p,ItemId.FireSphere,4)){foreach(var e in State.enemies)if(Vector3.Distance(p.position,e.position)<4*p.areaSize)Afflict(e,p,Ailment.Burn,3,8*Stack(p,ItemId.FireSphere)*DamageMultiplier(p),5);Effect(4,p.position,4*p.areaSize);}
            if(Ready(p,ItemId.FrostBell,8))Burst(p,p.position,7,0,Ailment.Chill,3+Stack(p,ItemId.FrostBell));
            if(Ready(p,ItemId.ThunderRod,6)){var e=FindItemTarget(p.position,20,-1,true);if(e!=null){Afflict(e,p,Ailment.Shock,6);DamageEnemy(e,p,45*Stack(p,ItemId.ThunderRod)*DamageMultiplier(p),false);WeaponEffect(10,p.position+Vector3.up,e.position+Vector3.up,1,WeaponKind.LightningStaff);}}
            if(Ready(p,ItemId.Chronosphere,15)){foreach(var e in State.enemies)if(Vector3.Distance(e.position,p.position)<9*p.areaSize)FreezeEnemy(e,2+Stack(p,ItemId.Chronosphere));Effect(1,p.position,9*p.areaSize);}
            float vortexBonus=p.itemValues[I(ItemId.SingularityTestament)];
            if(Ready(p,ItemId.ChaosPortal,6)){var e=FindItemTarget(p.position,20);if(e!=null)Field(p,ItemId.ChaosPortal,e.position,3,4+Stack(p,ItemId.ChaosPortal)+vortexBonus,0);}
            if(Ready(p,ItemId.BlackHole,10)){var e=FindItemTarget(p.position,20);if(e!=null)Field(p,ItemId.BlackHole,e.position,3,7+vortexBonus,100*Stack(p,ItemId.BlackHole)*DamageMultiplier(p));}
            if(Ready(p,ItemId.DragonBreath,7))
            {
                var forward=Quaternion.Euler(0,p.yaw,0)*Vector3.forward;
                foreach(var e in new List<EnemyState>(State.enemies)){var delta=e.position-p.position;delta.y=0;if(delta.magnitude<10*p.areaSize&&Vector3.Dot(delta.normalized,forward)>.55f){Afflict(e,p,Ailment.Burn,4,10*Stack(p,ItemId.DragonBreath),5);DamageEnemy(e,p,60*Stack(p,ItemId.DragonBreath)*DamageMultiplier(p),false);}}
                WeaponEffect(11,p.position+Vector3.up,p.position+forward*10+Vector3.up,10,WeaponKind.LightningStaff);
            }
            Travel(p,ItemId.KineticSpool,distance,20,()=>p.itemCounters[I(ItemId.KineticSpool)]++);
            Travel(p,ItemId.PacifistEngine,distance,8,()=>DropMovementMine(p));
            Travel(p,ItemId.VoidShepherd,distance,12,()=>Field(p,ItemId.VoidShepherd,p.position,4,6+vortexBonus,75*Stack(p,ItemId.VoidShepherd)*DamageMultiplier(p)));
            Travel(p,ItemId.LastPilgrim,distance,100,()=>{p.itemCounters[I(ItemId.LastPilgrim)]++;p.damage+=.03f*Stack(p,ItemId.LastPilgrim);if(p.itemCounters[I(ItemId.LastPilgrim)]%5==0)Burst(p,p.position,12,200*Stack(p,ItemId.LastPilgrim)*DamageMultiplier(p));});
            if(Has(p,ItemId.WarDrum)&&p.itemTimers[I(ItemId.WarDrum)]<=0)p.itemCounters[I(ItemId.WarDrum)]=0;
            if(p.stillTime>=2&&Ready(p,ItemId.VoidAnchor,3))
            {foreach(var e in State.enemies)if(!EnemyCatalog.EffectImmune(e.kind)&&Vector3.Distance(e.position,p.position)<6)e.position=MoveEnemyBody(e,Vector3.ClampMagnitude(p.position-e.position,2));Burst(p,p.position,6,60*Stack(p,ItemId.VoidAnchor)*DamageMultiplier(p));}
            if(p.stillTime>=2&&Ready(p,ItemId.WalkingCathedral,1))
            {foreach(var ally in State.players)if(Vector3.Distance(ally.position,p.position)<7*p.areaSize)Heal(ally,4*Stack(p,ItemId.WalkingCathedral));Burst(p,p.position,7,18*Stack(p,ItemId.WalkingCathedral)*DamageMultiplier(p));}
            if(Has(p,ItemId.ArchitectReign)&&p.stillTime>=8&&Ready(p,ItemId.ArchitectReign,8)&&State.itemFields.FindAll(f=>f.owner==p.id&&f.item==I(ItemId.ArchitectReign)).Count<5)
                Field(p,ItemId.ArchitectReign,p.position,36000,1,22*Stack(p,ItemId.ArchitectReign));
            if(Ready(p,ItemId.EventHorizon,6))
            {float power=p.itemValues[I(ItemId.EventHorizon)];p.itemValues[I(ItemId.EventHorizon)]=0;if(power>0)Burst(p,p.position,8,power*1.5f);}
            if(Ready(p,ItemId.StormCircuit,2))
            {
                var charged=State.enemies.FindAll(e=>Layers(e,Ailment.Shock)>0);
                for(int i=0;i<charged.Count&&i<24;i++){var a=charged[i];var b=charged[(i+1)%charged.Count];if(a==b||Vector3.Distance(a.position,b.position)>12)continue;WeaponEffect(10,a.position+Vector3.up,b.position+Vector3.up,1,WeaponKind.LightningStaff);DamageEnemy(b,p,35*Stack(p,ItemId.StormCircuit)*DamageMultiplier(p),false);}
            }
            if(Has(p,ItemId.TemporalDebt)&&p.itemTimers[I(ItemId.TemporalDebt)]<=0&&p.itemValues[I(ItemId.TemporalDebt)]>0)
            {float debt=p.itemValues[I(ItemId.TemporalDebt)];p.itemValues[I(ItemId.TemporalDebt)]=0;p.safeTime=0;ApplyHealthDamage(p,debt);if(p.downed)return;}
            if(Ready(p,ItemId.DebtOfKing,30))
            {int shares=Mathf.Max(0,State.openedChests-p.itemCounters[I(ItemId.DebtOfKing)]);State.coins+=shares*2*Stack(p,ItemId.DebtOfKing);State.earnedCoins+=shares*2*Stack(p,ItemId.DebtOfKing);p.damage+=shares*.01f*Stack(p,ItemId.DebtOfKing);}
            if(Has(p,ItemId.Chronicle)&&p.safeTime>=45)
            {p.safeTime-=45;p.projectileCount=Mathf.Min(48,p.projectileCount+Stack(p,ItemId.Chronicle));p.itemCounters[I(ItemId.Chronicle)]++;Effect(3,p.position,3);}
            if(Has(p,ItemId.EclipseCrown))
            {
                int i=I(ItemId.EclipseCrown),cycle=Mathf.FloorToInt(p.combatTime/24);
                p.itemCounters[i]=Mathf.Max(p.itemCounters[i],cycle);
                if(p.combatTime%24>=12&&Ready(p,ItemId.EclipseCrown,1))
                {float power=Mathf.Min(p.itemValues[i],Mathf.Max(20,p.itemValues[i]/3));p.itemValues[i]-=power;if(power>0){Burst(p,p.position,9,power);Heal(p,power*.08f);}}
            }
            if(Has(p,ItemId.OrbitalSaw)&&Ready(p,ItemId.OrbitalSaw,.4f))
            {int count=Stack(p,ItemId.OrbitalSaw);for(int n=0;n<count;n++){float a=p.combatTime*3+n*Mathf.PI*2/count;var pos=p.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*3*p.areaSize;Burst(p,pos,1.15f,22*DamageMultiplier(p));}}
            bool landed=p.landSerial!=p.lastLand;
            if(landed)
            {
                if(Ready(p,ItemId.LandingStone,2))Burst(p,p.position,4,30*Stack(p,ItemId.LandingStone)*DamageMultiplier(p));
                if(Has(p,ItemId.AngelWings)&&p.itemCounters[I(ItemId.AngelWings)]>0){p.itemCounters[I(ItemId.AngelWings)]=0;Burst(p,p.position,6,55*Stack(p,ItemId.AngelWings)*DamageMultiplier(p));}
            }
            if(!p.grounded&&p.airJumpsUsed>0)p.itemCounters[I(ItemId.AngelWings)]=1;
            p.lastLand=p.landSerial;p.lastJump=p.jumpSerial;
        }
        void Travel(PlayerState p,ItemId id,float distance,float threshold,Action trigger)
        {
            if(!Has(p,id))return;int i=I(id);p.itemValues[i]+=distance;
            if(p.itemValues[i]>=threshold){p.itemValues[i]-=threshold;trigger();}
        }
        void DropMovementMine(PlayerState p)
        {
            float power=80*Power(p,ItemId.PacifistEngine)*DamageMultiplier(p);
            if(p.itemCounters[I(ItemId.KineticSpool)]>0){p.itemCounters[I(ItemId.KineticSpool)]--;power*=1+.8f*Stack(p,ItemId.KineticSpool);}
            Field(p,ItemId.PacifistEngine,p.position,8,4,power);
        }
        void OnItemDash(PlayerState p,Vector3 origin)
        {
            if(Ready(p,ItemId.DashFlint,2))Field(p,ItemId.DashFlint,origin,3,3,16*Stack(p,ItemId.DashFlint)*DamageMultiplier(p));
            if(Ready(p,ItemId.Thunderstep,3)){Burst(p,origin,3,45*Stack(p,ItemId.Thunderstep)*DamageMultiplier(p),Ailment.Shock);Field(p,ItemId.StarMap,WorldMap.Resolve(origin,p.dashDirection*4.5f,.4f),.2f,3,45*Stack(p,ItemId.Thunderstep)*DamageMultiplier(p));}
            if(Has(p,ItemId.GlassWorld)){p.invulnerability=.6f;Burst(p,p.position,8,100*Power(p,ItemId.GlassWorld)*DamageMultiplier(p));}
        }
        void OnItemCast(PlayerState p,int kind)
        {
            if(Has(p,ItemId.SilentVow))PayHealth(p,5*Stack(p,ItemId.SilentVow));
            if(Has(p,ItemId.SacrificialBrazier)){PayHealth(p,5);Burst(p,p.position,8,0,Ailment.Burn,4);}
            if(Has(p,ItemId.InversionHeart)&&p.shield>=80)
            {float shield=p.shield;p.shield=0;Burst(p,p.position,8,shield*(1+.5f*(Stack(p,ItemId.InversionHeart)-1)),Ailment.Shock);}
            if(Has(p,ItemId.Capacitor)&&p.shield>0)
            {float spent=Mathf.Min(p.shield,20*Stack(p,ItemId.Capacitor));p.shield-=spent;Burst(p,p.position,7,spent*2,Ailment.Shock);}
            if(Has(p,ItemId.SpellLoom))p.itemCounters[I(ItemId.SpellLoom)]|=1<<kind;
            if(Has(p,ItemId.EndlessRefrain))
            {
                int i=I(ItemId.EndlessRefrain);p.itemCounters[i]++;p.itemValues[i]+=40*DamageMultiplier(p);
                if(p.itemCounters[i]%10==0)
                {int waves=Mathf.Min(12,2+p.itemCounters[i]/10);float power=p.itemValues[i]*Stack(p,ItemId.EndlessRefrain)/waves;p.itemValues[i]=0;for(int n=0;n<waves;n++)Field(p,ItemId.EndlessRefrain,p.position,.4f+n*.4f,10,power);}
            }
        }
        bool WeaponsSuppressed(PlayerState p)=>Has(p,ItemId.PacifistEngine)||(Has(p,ItemId.RealityCube)&&Mathf.FloorToInt(p.combatTime/12)%2==1);
        float OnItemVolley(PlayerState p,int slot,EnemyState target)
        {
            float multiplier=1;if(p.weapons[slot]==(int)WeaponKind.Bow&&Has(p,ItemId.HeavyArrow))multiplier+=.3f*Power(p,ItemId.HeavyArrow);
            if(Has(p,ItemId.BloodEngine))PayHealth(p,Stack(p,ItemId.BloodEngine));
            if(Has(p,ItemId.SilentVow))multiplier+=.35f*Power(p,ItemId.SilentVow);
            if(Has(p,ItemId.BloodMoon)){PayHealth(p,p.maxHp*.02f);multiplier+=.6f*Power(p,ItemId.BloodMoon);}
            if(Has(p,ItemId.LivingArsenal)&&p.weapons[1]>=0)multiplier+=.7f*Stack(p,ItemId.LivingArsenal);
            if(Has(p,ItemId.KineticSpool)&&p.itemCounters[I(ItemId.KineticSpool)]>0){p.itemCounters[I(ItemId.KineticSpool)]--;multiplier+=.8f*Stack(p,ItemId.KineticSpool);}
            if(Has(p,ItemId.SpellLoom)&&p.itemCounters[I(ItemId.SpellLoom)]==7){p.itemCounters[I(ItemId.SpellLoom)]=0;multiplier+=2*Stack(p,ItemId.SpellLoom);}
            if(Has(p,ItemId.BrokenMetronome)){int i=I(ItemId.BrokenMetronome);if(++p.itemCounters[i]%2==1)multiplier+=.6f*Stack(p,ItemId.BrokenMetronome);else p.shield=Mathf.Min(800,p.shield+12*Stack(p,ItemId.BrokenMetronome));}
            if(Has(p,ItemId.Alternator)&&p.alternatingSlot>=0&&p.alternatingSlot!=slot)
            {int i=I(ItemId.Alternator);if(++p.itemCounters[i]>=4){p.itemCounters[i]=0;Burst(p,p.position,7,90*Stack(p,ItemId.Alternator)*DamageMultiplier(p));}}p.alternatingSlot=slot;
            if(Has(p,ItemId.StarMap)&&++p.itemCounters[I(ItemId.StarMap)]%8==0&&target!=null)Field(p,ItemId.StarMap,target.position,.8f,5,140*Stack(p,ItemId.StarMap)*DamageMultiplier(p));
            if(Has(p,ItemId.SplitSoul)&&target!=null)Field(p,ItemId.SplitSoul,p.position,.7f,0,GameData.Weapons[p.weapons[slot]].damage*DamageMultiplier(p)*.6f*Stack(p,ItemId.SplitSoul)*multiplier,target.id);
            return multiplier;
        }
        int VolleyCount(PlayerState p)
        {
            int count=p.projectileCount;
            if(Has(p,ItemId.RealityCube)&&Mathf.FloorToInt(p.combatTime/12)%2==0)count*=1+Stack(p,ItemId.RealityCube);
            if(Has(p,ItemId.LivingArsenal)&&p.weapons[1]>=0)count+=2*Stack(p,ItemId.LivingArsenal);
            return Mathf.Clamp(count,1,48);
        }
        float ItemHitMultiplier(PlayerState p,EnemyState e)
        {
            float multiplier=1,dist=Vector3.Distance(p.position,e.position);
            if(Has(p,ItemId.SniperOath))multiplier*=dist>12?1+.45f*Power(p,ItemId.SniperOath):dist<5?.75f:1;
            if(Has(p,ItemId.BrawlerOath))multiplier*=dist<4?1+.4f*Power(p,ItemId.BrawlerOath):dist>10?.65f:1;
            if(Has(p,ItemId.SepticWound)&&Layers(e,Ailment.Poison)>0&&Layers(e,Ailment.Bleed)>0)multiplier*=1+Mathf.Min(1.2f,.12f*(Layers(e,Ailment.Poison)+Layers(e,Ailment.Bleed)))*Stack(p,ItemId.SepticWound);
            return multiplier;
        }
        void OnItemHit(PlayerState p,EnemyState e,float damage,bool critical,bool weapon)
        {
            if(weapon&&Has(p,ItemId.TimeMaster))Field(p,ItemId.TimeMaster,p.position,1,0,damage*Mathf.Min(1,.2f+.1f*(Stack(p,ItemId.TimeMaster)-1)),e.id);
            if(Has(p,ItemId.EclipseCrown)&&p.combatTime%24<12)p.itemValues[I(ItemId.EclipseCrown)]+=damage*(.2f+.05f*p.itemCounters[I(ItemId.EclipseCrown)])*Stack(p,ItemId.EclipseCrown);
            if(e.hp<=0)return;
            if(Has(p,ItemId.FlameBlade))Afflict(e,p,Ailment.Burn,3,6*Stack(p,ItemId.FlameBlade)*DamageMultiplier(p),5);
            if(Has(p,ItemId.IceCore))Afflict(e,p,Ailment.Chill,1+Stack(p,ItemId.IceCore));
            if(Has(p,ItemId.VenomNeedle))Afflict(e,p,Ailment.Poison,5,4*Stack(p,ItemId.VenomNeedle)*DamageMultiplier(p),8);
            if(critical&&Has(p,ItemId.SerratedCoin))Afflict(e,p,Ailment.Bleed,4,8*Stack(p,ItemId.SerratedCoin)*DamageMultiplier(p),5);
            if(Has(p,ItemId.FateLoom)&&critical)Afflict(e,p,Ailment.Mark,8);
            if(Has(p,ItemId.AlchemyEngine)&&p.itemCounters[I(ItemId.AlchemyEngine)]>0)
            {int recipe=(p.itemCounters[I(ItemId.AlchemyEngine)]-1)%3;p.itemCounters[I(ItemId.AlchemyEngine)]=0;Afflict(e,p,recipe==0?Ailment.Burn:recipe==1?Ailment.Chill:Ailment.Poison,6,18*Stack(p,ItemId.AlchemyEngine),8);}
            if(Has(p,ItemId.FrostfireCovenant))
            {int count=++p.itemCounters[I(ItemId.FrostfireCovenant)];Afflict(e,p,count%2==1?Ailment.Burn:Ailment.Chill,3,8*Stack(p,ItemId.FrostfireCovenant),5);if(count%6==0)Burst(p,e.position,3,35*Power(p,ItemId.FrostfireCovenant)*DamageMultiplier(p));}
            if(Has(p,ItemId.GlacialPrison)&&Layers(e,Ailment.Chill)>0)
            {var mark=e.ailments.Find(a=>a.kind==Ailment.Chill&&a.owner==p.id);if(mark!=null&&++mark.hits>=3){mark.hits=0;FreezeEnemy(e,1+Stack(p,ItemId.GlacialPrison));}}
            if(Has(p,ItemId.WinterSleep)&&e.frozen>0)
            {var sleep=e.ailments.Find(a=>a.kind==Ailment.Doom&&a.owner==p.id);if(sleep==null){Afflict(e,p,Ailment.Doom,60);sleep=e.ailments.Find(a=>a.kind==Ailment.Doom&&a.owner==p.id);}sleep.power+=damage*.5f*Stack(p,ItemId.WinterSleep);}
            if(Has(p,ItemId.SteamValve)&&Layers(e,Ailment.Chill)>0&&Layers(e,Ailment.Burn)>0&&!e.ailments.Exists(a=>a.kind==Ailment.Mark&&a.owner==-10))
            {e.ailments.RemoveAll(a=>a.kind==Ailment.Burn||a.kind==Ailment.Chill);e.ailments.Add(new AfflictionState{kind=Ailment.Mark,owner=-10,time=3});Burst(p,e.position,3,35*Power(p,ItemId.SteamValve)*DamageMultiplier(p));}
            if(Has(p,ItemId.ExecutionLedger)&&++p.itemCounters[I(ItemId.ExecutionLedger)]%5==0)Afflict(e,p,Ailment.Mark,8);
            if(Has(p,ItemId.ExecutionLedger)&&Layers(e,Ailment.Mark,p.id)>0&&e.hp/e.maxHp<.18f)
            {e.ailments.RemoveAll(a=>a.kind==Ailment.Mark&&a.owner==p.id);DamageEnemy(e,p,e.kind==EnemyKind.Warden?60*Stack(p,ItemId.ExecutionLedger):e.hp+1,false);}
            if(Has(p,ItemId.BloodCompass)&&Layers(e,Ailment.Bleed)>0&&Ready(p,ItemId.BloodCompass,.5f))Heal(p,3*Stack(p,ItemId.BloodCompass));
            if(Has(p,ItemId.EchoChamber))
            {int i=I(ItemId.EchoChamber);p.itemCounters[i]=p.lastVictim==e.id?p.itemCounters[i]+1:1;if(p.itemCounters[i]>=3){p.itemCounters[i]=0;Burst(p,e.position,3,45*Power(p,ItemId.EchoChamber)*DamageMultiplier(p));}}
            if(Has(p,ItemId.RedThread))
            {
                int i=I(ItemId.RedThread);if(p.lastVictim!=e.id&&State.enemies.Exists(a=>a.id==p.lastVictim)){p.itemCounters[i]=p.lastVictim;p.itemValues[i]=e.id;p.itemTimers[i]=4;}
                if(p.itemTimers[i]>0){int other=e.id==p.itemCounters[i]?(int)p.itemValues[i]:e.id==(int)p.itemValues[i]?p.itemCounters[i]:-1;var link=State.enemies.Find(a=>a.id==other);if(link!=null){WeaponEffect(10,e.position+Vector3.up,link.position+Vector3.up,1,WeaponKind.LightningStaff);DamageEnemy(link,p,damage*.2f*Power(p,ItemId.RedThread),false);}}
            }
            p.lastVictim=e.id;
            if(critical&&e.frozen>0&&Ready(p,ItemId.ShatterLens,3))Burst(p,e.position,4,45*Power(p,ItemId.ShatterLens)*DamageMultiplier(p));
            if(critical&&Ready(p,ItemId.MirrorShard,.4f)){var next=FindItemTarget(e.position,15,e.id);if(next!=null){WeaponEffect(10,e.position+Vector3.up,next.position+Vector3.up,1,WeaponKind.Bow);DamageEnemy(next,p,damage*.5f*Stack(p,ItemId.MirrorShard),false);}}
            if(Has(p,ItemId.StormConductor)&&Layers(e,Ailment.Shock)>0&&Ready(p,ItemId.StormConductor,1.5f))ChainItem(p,e.position,e.id,2,damage*.25f*Power(p,ItemId.StormConductor),false);
            if(Has(p,ItemId.StormAmulet)&&++p.itemCounters[I(ItemId.StormAmulet)]%4==0)ChainItem(p,e.position,e.id,1,24*Stack(p,ItemId.StormAmulet)*DamageMultiplier(p),true);
        }
        void ChainItem(PlayerState p,Vector3 origin,int except,int count,float damage,bool shock)
        {
            var used=new HashSet<int>{except};
            for(int n=0;n<count;n++)
            {var next=State.enemies.Find(e=>!used.Contains(e.id)&&Vector3.Distance(origin,e.position)<10);if(next==null)break;used.Add(next.id);if(shock)Afflict(next,p,Ailment.Shock,6);WeaponEffect(10,origin+Vector3.up,next.position+Vector3.up,1,WeaponKind.LightningStaff);DamageEnemy(next,p,damage,false);origin=next.position;}
        }
        void OnItemKill(PlayerState p,EnemyState e)
        {
            if(p==null)return;
            bool burning=Layers(e,Ailment.Burn)>0,poison=Layers(e,Ailment.Poison)>0;
            if(Ready(p,ItemId.SoulJar,3))Heal(p,6*Stack(p,ItemId.SoulJar));
            if(Ready(p,ItemId.PowderKeg,2))Burst(p,e.position,3,35*Stack(p,ItemId.PowderKeg)*DamageMultiplier(p));
            if(burning&&Has(p,ItemId.AshGarden))Field(p,ItemId.AshGarden,e.position,5,3,12*Stack(p,ItemId.AshGarden)*DamageMultiplier(p));
            if(burning&&Has(p,ItemId.SacrificialBrazier))Heal(p,10*Stack(p,ItemId.SacrificialBrazier));
            if(Has(p,ItemId.TemporalDebt))p.itemValues[I(ItemId.TemporalDebt)]=Mathf.Max(0,p.itemValues[I(ItemId.TemporalDebt)]-8*Stack(p,ItemId.TemporalDebt));
            if(Has(p,ItemId.BloodMoon))Heal(p,p.maxHp*.02f*Power(p,ItemId.BloodMoon));
            if(Has(p,ItemId.WarDrum)){int i=I(ItemId.WarDrum);p.itemCounters[i]=Mathf.Min(10,p.itemCounters[i]+1);p.itemTimers[i]=5;}
            if(Has(p,ItemId.Necronomicon)&&++p.itemCounters[I(ItemId.Necronomicon)]%3==0)ChainItem(p,e.position,e.id,3,35*Stack(p,ItemId.Necronomicon)*DamageMultiplier(p),false);
            if(Has(p,ItemId.HuntingPack)&&++p.itemCounters[I(ItemId.HuntingPack)]%5==0)Field(p,ItemId.HuntingPack,e.position,8,1,20*Stack(p,ItemId.HuntingPack));
            if(Has(p,ItemId.GravePact)&&++p.itemCounters[I(ItemId.GravePact)]%6==0)for(int n=0;n<Stack(p,ItemId.GravePact);n++)Field(p,ItemId.GravePact,e.position,8,1,18);
            if(poison&&Has(p,ItemId.HiveMind))Field(p,ItemId.HiveMind,e.position,6,1,8*Stack(p,ItemId.HiveMind));
            if(poison&&Has(p,ItemId.PlagueDoctor))
            {float power=0;foreach(var a in e.ailments)if(a.kind==Ailment.Poison)power+=a.power*a.stacks*.5f;foreach(var next in State.enemies)if(Vector3.Distance(e.position,next.position)<5)Afflict(next,p,Ailment.Poison,4,power*Stack(p,ItemId.PlagueDoctor),1);Heal(p,4*Stack(p,ItemId.PlagueDoctor));}
            if(Has(p,ItemId.SoulTithe)&&++p.itemCounters[I(ItemId.SoulTithe)]%10==0){PayHealth(p,5);p.maxHp+=3*Stack(p,ItemId.SoulTithe);}
            if(Has(p,ItemId.InfinitePower)&&++p.itemCounters[I(ItemId.InfinitePower)]%20==0)p.damage+=.04f*Stack(p,ItemId.InfinitePower);
            if(Has(p,ItemId.WorldSeed)&&++p.itemCounters[I(ItemId.WorldSeed)]%25==0)
            {int growth=p.itemCounters[I(ItemId.WorldSeed)]/25;p.maxHp+=2*Stack(p,ItemId.WorldSeed);p.areaSize+=.02f*Stack(p,ItemId.WorldSeed);Field(p,ItemId.WorldSeed,e.position,6,4+growth*.2f,(12+growth*4)*Stack(p,ItemId.WorldSeed)*DamageMultiplier(p));}
            if(Has(p,ItemId.Ouroboros)&&++p.itemCounters[I(ItemId.Ouroboros)]%50==0)
            {int cycles=p.itemCounters[I(ItemId.Ouroboros)]/50;Heal(p,p.maxHp);Burst(p,p.position,10+cycles*.5f,(160+cycles*40)*Stack(p,ItemId.Ouroboros)*DamageMultiplier(p),Ailment.Mark,8);}
            if(Has(p,ItemId.ArchitectReign))p.itemValues[I(ItemId.ArchitectReign)]+=.005f*Stack(p,ItemId.ArchitectReign);
            if(e.ailments.Count>0&&Has(p,ItemId.SingularityTestament)&&++p.itemCounters[I(ItemId.SingularityTestament)]%30==0)
            {p.itemValues[I(ItemId.SingularityTestament)]+=.5f*Stack(p,ItemId.SingularityTestament);Field(p,ItemId.BlackHole,e.position,2,7+p.itemValues[I(ItemId.SingularityTestament)],(100+30*p.itemCounters[I(ItemId.SingularityTestament)]/30)*DamageMultiplier(p));}
        }
        void OnItemPickup(PlayerState p,PickupState pickup)
        {
            if(pickup.kind==PickupKind.Gold)
            {
                if(Has(p,ItemId.CrownOfGreed))
                {int i=I(ItemId.CrownOfGreed);p.itemValues[i]+=pickup.value;if(p.itemValues[i]>=100){p.itemValues[i]-=100;Burst(p,p.position,7,60*Power(p,ItemId.CrownOfGreed)*DamageMultiplier(p));}}
                if(Ready(p,ItemId.MiserFuse,1))Field(p,ItemId.MiserFuse,p.position,6,4,55*Stack(p,ItemId.MiserFuse)*DamageMultiplier(p));
                if(Has(p,ItemId.AlchemyEngine)){int i=I(ItemId.AlchemyEngine);p.itemValues[i]=(p.itemValues[i]+1)%3;p.itemCounters[i]=(int)p.itemValues[i]+1;}
            }
            if(Has(p,ItemId.Overgrowth)&&++p.itemCounters[I(ItemId.Overgrowth)]%8==0)Field(p,ItemId.Overgrowth,p.position,5,4,10*Stack(p,ItemId.Overgrowth)*DamageMultiplier(p));
        }
        public void GrantGold(int value,PlayerState recipient=null)
        {
            if(value<=0)return;
            State.coins+=value;State.earnedCoins+=value;
            if(recipient==null||recipient.downed||recipient.eliminated)recipient=State.players.Find(p=>!p.downed&&!p.eliminated);
            if(recipient!=null)OnItemPickup(recipient,new PickupState{kind=PickupKind.Gold,value=value,position=recipient.position});
        }
        void OnItemHurt(PlayerState p,float damage)
        {
            p.safeTime=0;
            if(Has(p,ItemId.EventHorizon))p.itemValues[I(ItemId.EventHorizon)]=Mathf.Min(80*Power(p,ItemId.EventHorizon),p.itemValues[I(ItemId.EventHorizon)]+damage*.5f);
            if(Ready(p,ItemId.ThornMail,2))Burst(p,p.position,4,damage*.8f*Stack(p,ItemId.ThornMail));
            if(Ready(p,ItemId.RetaliationSeal,8)){p.shield+=18*Stack(p,ItemId.RetaliationSeal);Burst(p,p.position,5,28*Stack(p,ItemId.RetaliationSeal)*DamageMultiplier(p));}
        }
        void ApplyHealthDamage(PlayerState p,float damage)
        {
            p.hp=Mathf.Max(0,p.hp-damage);if(p.hp>0)return;
            if(Has(p,ItemId.PhoenixAsh)&&p.itemCounters[I(ItemId.PhoenixAsh)]==0){p.itemCounters[I(ItemId.PhoenixAsh)]=1;Burst(p,p.position,10,180*Stack(p,ItemId.PhoenixAsh)*DamageMultiplier(p),Ailment.Burn,4);}
            if(p.reviveCharges>0){p.reviveCharges--;p.hp=p.maxHp*.55f;p.damage+=.25f;p.invulnerability=3;Effect(3,p.position,4);return;}
            if(Has(p,ItemId.ImmortalContract)&&p.itemCounters[I(ItemId.ImmortalContract)]<Stack(p,ItemId.ImmortalContract))
            {p.itemCounters[I(ItemId.ImmortalContract)]++;p.maxHp=Mathf.Max(1,p.maxHp*.8f);p.hp=p.maxHp;p.damage+=.4f;p.invulnerability=3;Effect(3,p.position,5);return;}
            p.downed=true;p.knockback=Vector3.zero;p.hitSlide=0;p.crouching=false;p.chargingShrine=-1;p.bleedout=25;p.position.y=WorldMap.Ground(p.position);p.grounded=true;p.velocity=Vector3.zero;p.bursts.Clear();
        }
        public void ApplyItem(PlayerState p,int item)
        {
            if(p==null||item<0||item>=GameData.Items.Length)return;
            p.items[item]++;var id=(ItemId)item;
            switch(id)
            {
                case ItemId.SharpBlade:p.damage+=.075f;break;
                case ItemId.SwiftBoots:p.moveSpeed+=.10f;break;
                case ItemId.StoutHeart:float hp=(p.maxHp-p.auraMaxHp)*.1f;p.maxHp+=hp;Heal(p,hp);break;
                case ItemId.SteelShield:p.shield+=35;break;
                case ItemId.Hourglass:p.cooldownRate+=.055f;break;
                case ItemId.GoldenCoin:p.goldGain+=.1f;break;
                case ItemId.BeginnersLuck:p.luck+=7.5f;break;
                case ItemId.HeartyMeal:p.hpRegen+=1;break;
                case ItemId.SmallPotion:p.healingPower+=.09f;break;
                case ItemId.DoubleShot:p.projectileCount=Mathf.Min(48,p.projectileCount+1);break;
                case ItemId.SpikedGloves:p.critChance+=.06f;break;
                case ItemId.WingedBoots:p.moveSpeed+=.08f;p.dodge=GameData.CombineEvasion(p.dodge,.05f);break;
                case ItemId.TrueSight:p.critDamage+=.15f;break;
                case ItemId.BookOfKnowledge:
                    var slots=new List<int>();for(int n=0;n<p.weapons.Length;n++)if(p.weapons[n]>=0&&p.weaponLevels[n]<GameData.MaxWeaponLevel)slots.Add(n);
                    if(slots.Count>0)GiveWeapon(p,(WeaponKind)p.weapons[slots[random.Next(slots.Count)]]);else GrantExperience(30);break;
                case ItemId.Magnet:p.pickupRange*=1.25f;break;
                case ItemId.CrownOfGreed:p.goldGain+=.25f;p.xpGain+=.25f;break;
                case ItemId.AngelWings:p.extraJumps+=p.items[item]==1?2:1;break;
                case ItemId.Rebirth:p.reviveCharges++;break;
                case ItemId.CopperSpring:p.attackSpeed+=.075f;break;
                case ItemId.Spyglass:p.weaponRange+=3;break;
                case ItemId.ChalkCircle:p.areaSize+=.09f;break;
                case ItemId.WindFletching:p.projectileSpeed+=.125f;break;
                case ItemId.ScholarSeal:p.xpGain+=.09f;break;
                case ItemId.FeatherAnklet:p.dodge=GameData.CombineEvasion(p.dodge,.08f);break;
                case ItemId.IronBrooch:p.armor+=.08f;break;
                case ItemId.HealersRibbon:p.healingPower+=.1f;p.hpRegen+=.4f;break;
                case ItemId.DewBead:p.maxHp+=12;p.hpRegen+=.3f;Heal(p,12);break;
                case ItemId.NomadPin:p.moveSpeed+=.06f;p.pickupRange+=1;break;
                case ItemId.DuelistKnot:p.critChance+=.05f;p.attackSpeed+=.06f;break;
                case ItemId.CartographerSeal:p.luck+=8;p.weaponRange+=1.5f;break;
                case ItemId.LeadPendulum:p.damage+=.08f;p.projectileSpeed+=.06f;break;
                case ItemId.WardensBadge:p.maxHp+=10;p.shield+=20;Heal(p,10);break;
                case ItemId.ApprenticeCord:p.cooldownRate+=.06f;p.healingPower+=.08f;break;
                case ItemId.BloodGlass:p.lifeSteal+=.02f;p.critDamage+=.06f;break;
                case ItemId.CometDust:p.xpGain+=.1f;p.pickupRange+=1.2f;break;
                case ItemId.MerchantScale:p.goldGain+=.06f;p.luck+=7;break;
                case ItemId.SilverThread:p.armor+=.04f;p.dodge=GameData.CombineEvasion(p.dodge,.04f);break;
                case ItemId.SteadyHand:p.weaponRange+=2;p.critChance+=.03f;break;
                case ItemId.GlassCannon:p.damage+=.35f;p.maxHp=Mathf.Max(1,p.maxHp*.75f);p.hp=Mathf.Min(p.hp,p.maxHp);break;
                case ItemId.LeadBoots:p.armor+=.14f;p.moveSpeed=Mathf.Max(.4f,p.moveSpeed-.12f);break;
                case ItemId.BloodEngine:p.attackSpeed+=.3f;break;
                case ItemId.HuntersDebt:p.damage+=.3f;p.goldGain=Mathf.Max(.2f,p.goldGain-.3f);break;
                case ItemId.LoadedDice:p.luck+=30;p.xpGain=Mathf.Max(.2f,p.xpGain-.2f);break;
                case ItemId.HeavyArrow:p.projectileSpeed=Mathf.Max(.4f,p.projectileSpeed-.2f);break;
                case ItemId.GlassWorld:p.maxHp=p.hp=1;p.auraMaxHp=0;break;
                case ItemId.DebtOfKing:if(p.items[item]==1)p.itemCounters[item]=State.openedChests;break;
            }
            Effect(3,p.position,2);
        }
    }
}
