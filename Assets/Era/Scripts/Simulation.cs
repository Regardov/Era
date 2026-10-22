using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    // Only the host runs this simulation. Clients send intent, never damage or positions.
    public sealed partial class Simulation
    {
        public WorldState State { get; private set; }
        readonly System.Random random;
        public Simulation(int seed = 7341, bool multiplayer = false)
        {
            WorldMap.SetStage(1);
            random = new System.Random(seed);
            State = new WorldState { seed = seed, phase = RunPhase.Lobby, multiplayer = multiplayer };
            WorldMap.PopulateChests(State);PopulateShrines();PopulateEncounters();
        }
        public PlayerState AddPlayer(int id, string name, Hero hero, Relic relic = Relic.StormCompass)
        {
            if (State.players.Count >= GameData.MaxPlayers || id<0 || id>=GameData.MaxPlayers || State.Player(id) != null) return null;
            var p = new PlayerState { id = id, name = string.IsNullOrWhiteSpace(name) ? "Wanderer" : name.Substring(0, Math.Min(18, name.Length)),
                hero = (Hero)Mathf.Clamp((int)hero,0,2), relic = relic, position = WorldMap.OnGround((id-1)*3,0) };
            if (hero == Hero.Echo) { p.maxHp = p.hp = 115; p.damage = 0.9f; }
            if (hero == Hero.Lumen) { p.maxHp=p.hp=110;p.auraLevels[0]=1; }
            GiveStarterWeapon(p);
            p.projectileSpeed=hero==Hero.Ember?1.15f:1;
            State.players.Add(p);
            ResetReadiness();
            return p;
        }
        public void Start()
        {
            if (State.phase == RunPhase.Lobby && (State.players.Count == 1&&!State.multiplayer || State.players.Count >= 2 && State.players.TrueForAll(p=>p.ready)))
                State.phase = RunPhase.Playing;
        }
        public void ResetReadiness()
        { if(State.phase!=RunPhase.Lobby)return; State.readyCountdown=-1; foreach(var p in State.players)p.ready=false; }
        public bool SetReady(int id,bool value)
        {
            var p=State.Player(id); if(p==null || State.phase!=RunPhase.Lobby)return false;
            p.ready=value; if(!value)State.readyCountdown=-1; return true;
        }
        public bool SetLoadout(int id,Hero hero,Relic relic,string name)
        {
            var p=State.Player(id); if(p==null || State.phase!=RunPhase.Lobby || p.ready)return false;
            p.hero=(Hero)Mathf.Clamp((int)hero,0,2); p.relic=(Relic)Mathf.Clamp((int)relic,0,2);
            p.name=string.IsNullOrWhiteSpace(name)?"Wanderer":name.Substring(0,Math.Min(18,name.Length));
            p.damage=p.hero==Hero.Echo?0.9f:1; p.maxHp=p.hp=p.hero==Hero.Echo?115:p.hero==Hero.Lumen?110:100;
            p.projectileSpeed=p.hero==Hero.Ember?1.15f:1;
            Array.Clear(p.auraLevels,0,p.auraLevels.Length);p.currentAura=0;if(p.hero==Hero.Lumen)p.auraLevels[0]=1;
            GiveStarterWeapon(p);
            return true;
        }
        public void Tick(float dt, IDictionary<int, InputFrame> inputs)
        {
            WorldMap.SetStage(State.stage);
            dt = Mathf.Clamp(dt, 0, 0.05f);
            if(State.phase==RunPhase.Lobby)
            {
                bool both=State.players.Count>=2 && State.players.TrueForAll(p=>p.ready);
                if(!both) { State.readyCountdown=-1; return; }
                if(State.readyCountdown<0)State.readyCountdown=3;
                State.readyCountdown=Mathf.Max(0,State.readyCountdown-dt);
                if(State.readyCountdown<=0)Start(); return;
            }
            if (State.phase != RunPhase.Playing) return;
            foreach(var player in State.players)
                if(inputs.TryGetValue(player.id,out var choice)&&choice.choice>=0)ChooseItem(player,choice.choice);
            if(UpgradePaused)return;
            RefreshAuras();
            State.elapsed += dt;
            if(!State.portal.active)State.stageElapsed+=dt;
            State.noticeTime=Mathf.Max(0,State.noticeTime-dt);
            foreach (var p in State.players)
            {
                inputs.TryGetValue(p.id, out var input);
                TickPlayer(p, input ?? new InputFrame(), dt);
            }
            TickShrines(dt,inputs);TickEnemies(dt);TickHazards(dt);
            TickProjectiles(dt);
            TickLoot(dt);
            CollectPickups(dt);
            State.effects.RemoveAll(e => (e.life -= dt) <= 0);
            State.hits.RemoveAll(e => (e.life -= dt) <= 0);
            TickEliteShrines();TickWaves(dt);
            if (State.players.TrueForAll(p => p.downed || p.eliminated)) State.phase = RunPhase.Defeat;
        }
        void TickPlayer(PlayerState p, InputFrame input, float dt)
        {
            p.slowed=Mathf.Max(0,p.slowed-dt);
            p.auraSurge=Mathf.Max(0,p.auraSurge-dt);
            p.realityBoost=Mathf.Max(0,p.realityBoost-dt);
            if(p.items[30]>0){p.realityTimer-=dt;if(p.realityTimer<=0){p.realityTimer=30;p.realityBoost=10;Effect(3,p.position,5);}}
            float reality=p.realityBoost>0?2:1;
            float recharge=p.cooldownRate*(1+p.auraBonuses[6])*reality;
            p.qCooldown = Mathf.Max(0, p.qCooldown - dt * recharge);
            p.eCooldown = Mathf.Max(0, p.eCooldown - dt * recharge);
            p.relicCooldown = Mathf.Max(0, p.relicCooldown - dt * recharge);
            p.dashCooldown = Mathf.Max(0, p.dashCooldown - dt * recharge);
            p.invulnerability = Mathf.Max(0, p.invulnerability - dt);
            p.boost = Mathf.Max(0, p.boost - dt);
            p.castTime=Mathf.Max(0,p.castTime-dt);
            p.weaponPoseTime=Mathf.Max(0,p.weaponPoseTime-dt);
            p.shield = Mathf.Max(0, p.shield - dt * 0.55f);
            if(p.hpRegen>0&&!p.downed)p.hp=Mathf.Min(p.maxHp,p.hp+p.hpRegen*p.healingPower*dt);
            if (p.eliminated) return;
            if (p.downed)
            {
                p.bleedout -= dt;
                bool reviving = false;
                foreach (var other in State.players)
                {
                    if (other.id == p.id || other.downed || other.eliminated) continue;
                    if (inputsForRevive != null && inputsForRevive.TryGetValue(other.id, out var intent) && intent.revive &&
                        Vector3.Distance(other.position, p.position) < 3.5f) reviving = true;
                }
                p.reviveProgress = reviving ? p.reviveProgress + dt : Mathf.Max(0, p.reviveProgress - dt * 0.5f);
                if (p.reviveProgress >= 2.5f)
                {
                    p.downed = false; p.hp = p.maxHp * 0.5f; p.invulnerability = 3; p.reviveProgress = 0; p.bleedout = 25;
                    Effect(3, p.position, 3);
                }
                else if (p.bleedout <= 0) { p.eliminated = true; p.downed = false; }
                return;
            }
            p.crouching=input.crouch&&p.grounded&&p.dashTime<=0;
            if(input.interact&&!TryUsePortal(p)&&!TryCollectLoot(p)&&!TrySummonBoss(p)&&!TryActivateEliteShrine(p)&&!TryBreakResource(p))TryOpenChest(p);
            Vector3 before=p.position;
            var move = Vector3.ClampMagnitude(new Vector3(Safe(input.x), 0, Safe(input.z)), 1);
            float facing=move.sqrMagnitude>0.02f?Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg:Safe(input.yaw);
            if(move.sqrMagnitude<=.02f&&p.weaponPoseTime>0)facing=p.weaponAimYaw;
            p.yaw = Mathf.MoveTowardsAngle(p.yaw,facing,720*dt);
            if (input.dash && p.dashCooldown <= 0)
            {
                p.dashDirection = move.sqrMagnitude > 0.01f ? move : Quaternion.Euler(0, p.yaw, 0) * Vector3.forward;
                p.dashTime = 0.19f; p.dashCooldown = 3; p.invulnerability = 0.28f;
                Effect(2, p.position + Vector3.up * 0.2f, 2);
            }
            if (p.dashTime > 0) { p.position=WorldMap.Resolve(p.position,p.dashDirection*24*dt,0.4f); p.dashTime -= dt; }
            else p.position=WorldMap.Resolve(p.position,move*(7.8f*p.moveSpeed*(1+p.auraBonuses[4])*reality*(p.boost>0?1.3f:1)*(p.crouching?.43f:1)*(p.slowed>0?.6f:1)*(p.hitSlide>0?.2f:1))*dt,0.4f);
            if(p.hitSlide>0){p.position=WorldMap.Resolve(p.position,p.knockback*dt,.4f);p.knockback*=Mathf.Exp(-11*dt);p.hitSlide=Mathf.Max(0,p.hitSlide-dt);}
            float ground=WorldMap.Ground(p.position);
            if(p.grounded && ground-p.position.y<0.5f)p.position.y=ground;
            p.coyoteTime=p.grounded?0.12f:Mathf.Max(0,p.coyoteTime-dt);
            p.jumpBuffer=input.jump?0.14f:Mathf.Max(0,p.jumpBuffer-dt);
            if(p.grounded)p.airJumpsUsed=0;
            bool airJump=input.jump&&!p.grounded&&p.coyoteTime<=0&&p.airJumpsUsed<p.extraJumps;
            if((p.jumpBuffer>0 && p.coyoteTime>0)||airJump)
            { if(airJump){p.airJumpsUsed++;Effect(2,p.position,1.4f);}p.verticalSpeed=10.8f;p.grounded=false;p.crouching=false;p.jumpBuffer=p.coyoteTime=0;p.jumpSerial++; }
            if(!p.grounded)
            {
                if(!input.jumpHeld && p.verticalSpeed>4.5f)p.verticalSpeed=4.5f;
                p.verticalSpeed-=(p.verticalSpeed>0?24:36)*dt;
                p.position.y+=p.verticalSpeed*dt;
                if(p.position.y<=ground) { p.position.y=ground;p.verticalSpeed=0;p.grounded=true;p.landSerial++; }
            }
            p.velocity=(p.position-before)/Mathf.Max(0.001f,dt);
            if (input.primary && p.qCooldown <= 0) CastQ(p);
            if (input.secondary && p.eCooldown <= 0) CastE(p);
            if (input.relic && p.relicCooldown <= 0) CastRelic(p);
            TickWeapons(p,dt);
            TickAuraDamage(p,dt);
            if (p.items[15] > 0 && (p.fireTimer -= dt) <= 0)
            {
                p.fireTimer = .9f;
                AreaDamage(p, 3.5f, 18 * p.items[15] * DamageMultiplier(p), 0);
                Effect(4, p.position, 3.5f);
            }
            if(p.items[21]>0&&(p.chronoTimer-=dt)<=0){p.chronoTimer=15;AreaDamage(p,9,0,3);Effect(1,p.position,9);}
            if(p.items[22]>0&&(p.portalTimer-=dt)<=0){p.portalTimer=6;foreach(var e in State.enemies)if(Vector3.Distance(e.position,p.position)<13)e.position=Vector3.MoveTowards(e.position,p.position,4);Effect(1,p.position,12);}
            if(p.items[24]>0&&(p.dragonTimer-=dt)<=0){p.dragonTimer=5;AreaDamage(p,8,70*p.items[24]*DamageMultiplier(p),0);Effect(0,p.position,8);}
            if(p.items[27]>0&&(p.blackHoleTimer-=dt)<=0){p.blackHoleTimer=4;foreach(var e in new List<EnemyState>(State.enemies))if(Vector3.Distance(e.position,p.position)<11){e.position=Vector3.MoveTowards(e.position,p.position,3);DamageEnemy(e,p,24*p.items[27]);}Effect(4,p.position,10);}
        }
        IDictionary<int, InputFrame> inputsForRevive;
        public void Step(float dt, IDictionary<int, InputFrame> inputs)
        { inputsForRevive = inputs; Tick(dt, inputs); }
        public bool UpgradePaused=>State.phase==RunPhase.Playing&&!State.multiplayer&&State.players.Exists(p=>!p.downed&&!p.eliminated&&p.pendingLevels>0);
        static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;

        void CastQ(PlayerState p)
        {
            CastPose(p,0);
            if(p.hero==Hero.Lumen)
            {
                p.qCooldown=8;AreaDamage(p,8,45*DamageMultiplier(p),0);
                foreach(var ally in State.players)if(!ally.downed&&!ally.eliminated&&Vector3.Distance(p.position,ally.position)<=8*p.areaSize)ally.hp=Mathf.Min(ally.maxHp,ally.hp+20*p.healingPower);
                Effect(3,p.position,8*p.areaSize);return;
            }
            bool ember = p.hero == Hero.Ember;
            p.qCooldown = ember ? 8 : 10;
            AreaDamage(p, ember ? 7 : 8, (ember ? 65 : 35) * DamageMultiplier(p), ember ? 0 : 3);
            Effect(ember ? 0 : 1, p.position, ember ? 7 : 8);
        }
        void CastE(PlayerState p)
        {
            CastPose(p,1);
            if(p.hero==Hero.Lumen){p.auraSurge=6;p.eCooldown=18;RefreshAuras();Effect(3,p.position,AuraRadius(p));return;}
            if (p.hero == Hero.Ember) { p.boost = 6; p.eCooldown = 18; Effect(0, p.position, 2.5f); }
            else
            {
                p.eCooldown = 16;
                foreach (var other in State.players)
                    if (!other.downed && !other.eliminated && Vector3.Distance(p.position, other.position) <= 12)
                    { other.hp = Mathf.Min(other.maxHp, other.hp + 35*p.healingPower); other.shield = Mathf.Min(200, other.shield + 25*p.healingPower); }
                Effect(3, p.position, 12);
            }
        }
        void AreaDamage(PlayerState p, float radius, float damage, float frozen)
        {
            foreach(var e in new List<EnemyState>(State.enemies))
            {
                if (Vector3.Distance(p.position, e.position) > radius*p.areaSize) continue;
                e.frozen = Mathf.Max(e.frozen, frozen);
                DamageEnemy(e, p, damage);
            }
        }
        void CastRelic(PlayerState p)
        {
            CastPose(p,2);
            p.relicCooldown = GameData.RelicDuration(p.relic);
            if (p.relic == Relic.StormCompass) { AreaDamage(p, 9, 90 * DamageMultiplier(p), 0); Effect(0, p.position, 9); }
            else if (p.relic == Relic.Hourglass)
            { AreaDamage(p, 12, 0, 4); p.dashCooldown = 0; Effect(1, p.position, 12); }
            else
            {
                p.hp = Mathf.Min(p.maxHp, p.hp + 50*p.healingPower);
                GatherExperience(); Effect(3, p.position, 5);
            }
        }
        void CastPose(PlayerState p,int kind) { p.castSerial++;p.castKind=kind;p.castTime=0.65f; }
        public bool TryOpenChest(PlayerState p)
        {
            if(State.phase!=RunPhase.Playing || p.downed || p.eliminated)return false;
            ChestState nearest=null;float distance=3.2f;
            foreach(var chest in State.chests)
            { float d=Vector3.Distance(p.position,chest.position);if(!chest.opened && d<distance){nearest=chest;distance=d;} }
            if(nearest==null)return false;
            if(State.coins<nearest.cost)
            { State.noticeKind=1;State.noticeContent=nearest.cost;State.noticeTime=2;return false; }
            State.coins-=nearest.cost;nearest.opened=true;nearest.owner=p.id;State.openedChests++;
            foreach(var chest in State.chests)if(!chest.opened&&!chest.free)chest.cost=GameData.ChestPrice(chest.baseCost,State.openedChests);
            RollChestReward(nearest,p);
            Effect(7,nearest.position+Vector3.up*0.7f,3);return true;
        }
        public EnemyState SpawnEnemy(EnemyKind? forced = null)
        {
            var kind = forced ?? (State.wave >= 3 && random.NextDouble() < 0.22 ? EnemyKind.Brute :
                State.wave >= 2 && random.NextDouble() < 0.35 ? EnemyKind.Stalker : EnemyKind.Wisp);
            float angle = (float)random.NextDouble() * Mathf.PI * 2;
            var alive = State.players.FindAll(p => !p.downed && !p.eliminated);
            Vector3 origin = alive.Count > 0 ? alive[random.Next(alive.Count)].position : Vector3.zero;
            Vector3 pos = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 21;
            var flat = Vector2.ClampMagnitude(new Vector2(pos.x, pos.z), GameData.ArenaRadius - 1);
            pos = WorldMap.Resolve(WorldMap.OnGround(flat.x,flat.y),Vector3.zero,0.6f);pos.y=WorldMap.Ground(pos);
            float scaling = (1 + (State.wave - 1) * 0.14f)*CampaignData.EnemyScale(State.stage);
            float hp = (kind == EnemyKind.Warden ? 2200 : kind == EnemyKind.Brute ? 110 : kind == EnemyKind.Stalker ? 32 : 44) * scaling;
            hp *= 1+.5f*(State.players.Count-1);
            if(kind==EnemyKind.Warden)hp=1700*CampaignData.BossHpScale(State.stage)*(1+.5f*(State.players.Count-1));
            var e = new EnemyState { id = State.nextId++, kind = kind, position = pos, hp = hp, maxHp = hp,
                speed = kind == EnemyKind.Warden ? 1.9f : kind == EnemyKind.Brute ? 1.55f : kind == EnemyKind.Stalker ? 4.1f : 2.65f,
                damage = (kind == EnemyKind.Warden ? 28 : kind == EnemyKind.Brute ? 20 : 10)*(1+.18f*(State.stage-1)), specialTimer = 4,bossStage=kind==EnemyKind.Warden?State.stage:0 };
            State.enemies.Add(e);
            return e;
        }
        void TickEnemies(float dt)
        {
            foreach (var e in new List<EnemyState>(State.enemies))
            {
                if(!State.enemies.Contains(e))continue;
                e.attackTimer -= dt;
                if (e.frozen > 0) { e.frozen -= dt; continue; }
                PlayerState nearest = null; float distance = float.MaxValue;
                foreach (var p in State.players)
                {
                    if (p.downed || p.eliminated) continue;
                    float d = (p.position - e.position).sqrMagnitude;
                    if (d < distance) { distance = d; nearest = p; }
                }
                if (nearest == null) continue;
                if (e.kind == EnemyKind.Warden)
                {
                    if (e.windup > 0)e.windup-=dt;
                    else if ((e.specialTimer -= dt) <= 0)
                    { BossAttack(e,nearest); }
                }
                float reach = e.kind == EnemyKind.Warden ? 2.3f : 1.05f;
                if (distance > reach * reach && e.windup <= 0)
                {
                    Vector3 dir = nearest.position - e.position; dir.y = 0;
                    var next=WorldMap.Resolve(e.position,dir.normalized*e.speed*dt,e.kind==EnemyKind.Brute?0.7f:0.4f);
                    if((next-e.position).sqrMagnitude<dt*dt*0.15f)
                        next=WorldMap.Resolve(e.position,Quaternion.Euler(0,e.id%2==0?80:-80,0)*dir.normalized*e.speed*dt,0.4f);
                    e.position=next;
                    // Cheap local separation prevents the entire horde collapsing into one point.
                    int index = State.enemies.IndexOf(e);
                    for (int j = Math.Max(0, index - 6); j < index; j++)
                    {
                        var delta = e.position - State.enemies[j].position;
                        if (delta.sqrMagnitude < 0.65f && delta.sqrMagnitude > 0.001f) e.position += delta.normalized * dt * 1.6f;
                    }
                    e.position=WorldMap.Resolve(e.position,Vector3.zero,0.4f);e.position.y=WorldMap.Ground(e.position);
                }
                if (distance < reach * reach && nearest.position.y-WorldMap.Ground(nearest.position) < 1.25f && e.attackTimer <= 0)
                { e.attackTimer = 1; DamagePlayer(nearest, e.damage,e.position); }
            }
        }
        void TickProjectiles(float dt)
        {
            foreach(var b in new List<ProjectileState>(State.projectiles))
            {
                if(!State.projectiles.Contains(b))continue;
                b.life -= dt;
                var projectileOwner=State.Player(b.owner);
                var end = b.position + b.direction * 34 * (projectileOwner?.projectileSpeed??1) * dt;
                bool hit = false;
                for (int j = State.enemies.Count - 1; j >= 0; j--)
                {
                    var e = State.enemies[j];
                    float radius = e.kind == EnemyKind.Warden ? 1.5f : e.kind == EnemyKind.Brute ? 0.85f : 0.6f;
                    if (SegmentDistance(b.position, end, e.position + Vector3.up * 0.7f) > radius) continue;
                    DamageEnemy(e, State.Player(b.owner), b.damage); hit = true; break;
                }
                if(!hit)foreach(var node in State.resourceNodes)if(!node.broken&&SegmentDistance(b.position,end,node.position+Vector3.up*.45f)<.8f){DamageResource(node,b.damage,projectileOwner);hit=true;break;}
                b.position = end;
                if (hit || b.life <= 0) State.projectiles.Remove(b);
            }
        }
        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            var d = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(0.0001f, d.sqrMagnitude));
            return Vector3.Distance(a + d * t, p);
        }
        public void DamageEnemy(EnemyState e, PlayerState owner, float damage, bool proc = true)
        {
            if(e==null||e.hp<=0||damage<=0||!State.enemies.Contains(e))return;
            bool critical=false;
            if(owner!=null)
            {
                if(random.NextDouble()<owner.critChance){damage*=owner.critDamage;critical=true;}
                if(owner.items[17]>0)damage*=1+.18f*owner.items[17];
                if(owner.items[18]>0)e.frozen=Mathf.Max(e.frozen,.35f+.15f*owner.items[18]);
                if(owner.items[29]>0)damage*=1+.35f*owner.items[29];
            }
            float dealt=Mathf.Min(e.hp,damage);e.hp -= damage;e.hitSerial++;
            AddHit(e.id,e.position+Vector3.up*1.4f,damage,critical,false);
            if (owner != null&&!owner.downed&&!owner.eliminated) owner.hp = Mathf.Min(owner.maxHp, owner.hp + dealt * owner.lifeSteal);
            if (e.hp > 0) return;
            State.enemies.Remove(e);
            State.kills++;
            if (owner != null) owner.kills++;
            Effect(6, e.position + Vector3.up * 0.5f, 0.8f);
            if(proc&&owner!=null&&owner.items[20]>0&&random.NextDouble()<.22)
            {var spirit=State.enemies.Find(other=>Vector3.Distance(other.position,e.position)<8);if(spirit!=null){WeaponEffect(10,e.position+Vector3.up,spirit.position+Vector3.up,1,WeaponKind.LightningStaff);DamageEnemy(spirit,owner,35*owner.items[20],false);}}
            if (e.kind == EnemyKind.Warden) { DefeatBoss(e); return; }
            float xp=(e.kind==EnemyKind.Brute?15:e.kind==EnemyKind.Stalker?9:6)*(e.eliteShrine>=0?3:1)*(owner?.xpGain??1)*(1+(owner?.auraBonuses[7]??0))+(owner?.xpRemainder??0);
            int earnedXp=Mathf.Max(1,Mathf.FloorToInt(xp));if(owner!=null)owner.xpRemainder=xp-earnedXp;
            var pickup = new PickupState { id = State.nextId++, position = e.position, value = earnedXp };
            if (State.pickups.Count < 180) State.pickups.Add(pickup);
            else { var merge=State.pickups.Find(d=>d.kind==PickupKind.Experience);if(merge!=null)merge.value+=pickup.value;else GrantExperience(pickup.value); }
            float gold=(e.kind==EnemyKind.Brute?12:4)*(owner?.goldGain??1)+(owner?.goldRemainder??0);
            int earnedGold=Mathf.Max(1,Mathf.FloorToInt(gold));if(owner!=null)owner.goldRemainder=gold-earnedGold;
            var coin=new PickupState { id=State.nextId++,position=e.position+Vector3.right*0.55f,value=earnedGold,kind=PickupKind.Gold };
            if(State.pickups.Count<260)State.pickups.Add(coin);
            else { State.coins+=coin.value;State.earnedCoins+=coin.value; }
        }
        public void DamagePlayer(PlayerState p, float damage,Vector3? source=null)
        {
            if (p.downed || p.eliminated || p.invulnerability > 0) return;
            if(random.NextDouble()<Mathf.Clamp01(p.dodge))return;
            damage*=1-Mathf.Clamp(p.armor,0,.75f);
            damage*=1-Mathf.Clamp(p.auraBonuses[5],0,.65f);
            AddHit(p.id,p.position+Vector3.up*1.8f,damage,false,true);
            float absorb = Mathf.Min(p.shield, damage); p.shield -= absorb; damage -= absorb;
            p.hp = Mathf.Max(0, p.hp - damage); p.invulnerability = 0.5f;
            var push=source.HasValue?p.position-source.Value:-(Quaternion.Euler(0,p.yaw,0)*Vector3.forward);push.y=0;if(push.sqrMagnitude<.001f)push=Vector3.back;
            p.knockback=push.normalized*4;p.hitSlide=.24f;
            if(p.grounded)Effect(2,p.position+Vector3.up*.05f,.9f);
            Effect(5, p.position + Vector3.up, 0.7f);
            if(p.hp<=0&&p.reviveCharges>0){p.reviveCharges--;p.hp=p.maxHp*.55f;p.invulnerability=3;Effect(3,p.position,4);return;}
            if (p.hp <= 0) { p.downed = true;p.knockback=Vector3.zero;p.hitSlide=0;p.crouching=false;p.chargingShrine=-1; p.bleedout = 25; p.position.y = WorldMap.Ground(p.position);p.grounded=true;p.velocity=Vector3.zero; }
        }
        void CollectPickups(float dt)
        {
            for (int i = State.pickups.Count - 1; i >= 0; i--)
            {
                var pickup = State.pickups[i];
                pickup.age+=dt;
                if(pickup.age<.25f)continue;
                foreach (var p in State.players)
                {
                    if (p.downed || p.eliminated) continue;
                    float d = Vector3.Distance(p.position, pickup.position);
                    if (d < 1.15f)
                    { State.pickups.RemoveAt(i); if(pickup.kind==PickupKind.Gold){State.coins+=pickup.value;State.earnedCoins+=pickup.value;}else GrantExperience(pickup.value); break; }
                    if (d < p.pickupRange) pickup.position = Vector3.MoveTowards(pickup.position, p.position, dt * 16);
                }
            }
        }
        public void GrantExperience(int amount)
        {
            State.xp += Mathf.Max(0, amount);
            while (State.xp >= State.xpRequired)
            {
                State.xp -= State.xpRequired; State.level++; State.xpRequired = 12 + (State.level - 1) * 7;
                foreach (var p in State.players)
                {
                    p.pendingLevels++;
                    if (!p.downed && !p.eliminated) p.hp = Mathf.Min(p.maxHp, p.hp + 12);
                    if (p.offers.Length == 0) RollOffers(p);
                }
            }
        }
        void RollOffers(PlayerState p)
        {
            var choices = new List<int>();
            int nextLevel=p.completedLevel+1;
            if(nextLevel>=GameData.SecondWeaponLevel&&p.weapons[1]<0&&p.upgradeStage==0)
            {
                p.offerKind=RewardKind.Weapon;
                for(int i=0;i<(int)WeaponKind.Aura;i++)if(Array.IndexOf(p.weapons,i)<0)choices.Add(i);
                p.offers=choices.ToArray();p.offerRarities=new int[p.offers.Length];return;
            }
            if(p.hero==Hero.Lumen&&p.upgradeStage==2)
            {
                p.offerKind=RewardKind.Aura;int count=Array.FindAll(p.auraLevels,a=>a>0).Length;
                for(int i=0;i<p.auraLevels.Length;i++)if(count<GameData.MaxAuras||p.auraLevels[i]>0)choices.Add(i);
                p.offers=choices.ToArray();p.offerRarities=new int[p.offers.Length];return;
            }
            p.offerKind=RewardKind.Accessory;
            for (int i = 0; i < GameData.Attributes.Length; i++) choices.Add(i);
            for (int i = choices.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int v = choices[i]; choices[i] = choices[j]; choices[j] = v; }
            p.offers = choices.GetRange(0, 3).ToArray();
            p.offerRarities=new int[3];for(int i=0;i<3;i++)p.offerRarities[i]=RollRarity(p,0);
        }
        public bool ChooseItem(PlayerState p, int slot)
        {
            if (p.pendingLevels <= 0 || slot < 0 || slot >= p.offers.Length || p.downed || p.eliminated) return false;
            int content=p.offers[slot];
            if(p.offerKind==RewardKind.Weapon)
            {if(GiveWeapon(p,(WeaponKind)content)<0)return false;p.upgradeStage=1;RollOffers(p);return true;}
            if(p.offerKind==RewardKind.Aura)
            {if(!LearnAura(p,content))return false;}
            else
            {
                int rarity=slot<p.offerRarities.Length?p.offerRarities[slot]:0;
                ApplyAccessory(p,content,rarity);
                if(p.hero==Hero.Lumen){p.upgradeStage=2;RollOffers(p);return true;}
            }
            p.pendingLevels--;p.completedLevel++;p.upgradeStage=0;
            if (p.pendingLevels > 0) RollOffers(p); else {p.offers = Array.Empty<int>();p.offerRarities=Array.Empty<int>();}
            return true;
        }
        public int RollRarity(PlayerState p,int chestTier)
        {
            float luck=Mathf.Max(0,p?.luck??0),fortune=1+(p?.auraBonuses[0]??0),rare=Mathf.Max(10,62-luck*3.2f-chestTier*12),uncommon=Mathf.Max(15,25+luck*.2f),epic=(10+luck*1.8f+chestTier*7)*fortune,legendary=(3+luck*1.2f+chestTier*5)*fortune;
            double roll=random.NextDouble()*(rare+uncommon+epic+legendary);if((roll-=rare)<0)return 0;if((roll-=uncommon)<0)return 1;if((roll-=epic)<0)return 2;return 3;
        }
        public void ApplyAttribute(PlayerState p,int attribute,int rarity)
        {
            if(attribute<0||attribute>=GameData.Attributes.Length)return;rarity=Mathf.Clamp(rarity,0,3);float v=GameData.Attributes[attribute].values[rarity];p.attributes[attribute]++;
            switch(attribute)
            {
                case 0:p.damage+=v;break;case 1:p.attackSpeed+=v;break;case 2:p.critChance+=v;break;case 3:p.critDamage+=v;break;
                case 4:p.maxHp+=v;p.hp=Mathf.Min(p.maxHp,p.hp+v);break;case 5:p.healingPower+=v;break;case 6:p.hpRegen+=v;break;
                case 7:p.shield+=v;break;case 8:p.armor+=v;break;case 9:p.lifeSteal+=v;break;case 10:p.moveSpeed+=v;break;
                case 11:p.dodge+=v;break;case 12:p.cooldownRate+=v;break;case 13:p.weaponRange+=v;break;case 14:p.areaSize+=v;break;
                case 15:p.projectileCount=Mathf.Min(12,p.projectileCount+Mathf.RoundToInt(v));break;case 16:p.projectileSpeed+=v;break;
                case 17:p.pickupRange+=v;break;case 18:p.luck+=v;break;case 19:p.xpGain+=v;break;case 20:p.goldGain+=v;break;
            }Effect(3,p.position,2);
        }
        public void ApplyItem(PlayerState p, int item)
        {
            if (item < 0 || item >= GameData.Items.Length) return;
            p.items[item]++;
            switch (item)
            {
                case 0:p.damage+=.15f;break;case 1:p.moveSpeed+=.10f;break;
                case 2:float hp=(p.maxHp-p.auraMaxHp)*.20f;p.maxHp+=hp;p.hp+=hp;break;case 3:p.armor+=.10f;break;case 4:p.cooldownRate+=.11f;break;
                case 5:p.goldGain+=.20f;break;case 6:p.luck+=2;break;case 7:p.hpRegen+=1;break;case 8:p.hpRegen+=3;break;
                case 9:p.projectileCount=Mathf.Min(12,p.projectileCount+1);break;case 10:p.critChance+=.25f;p.critDamage+=.5f;break;
                case 11:p.moveSpeed+=.20f;p.dodge+=.05f;break;case 12:p.lifeSteal+=.10f;break;case 13:p.critDamage+=.30f;break;
                case 14:var slots=new List<int>();for(int i=0;i<p.weapons.Length;i++)if(p.weapons[i]>=0&&p.weaponLevels[i]<GameData.MaxWeaponLevel)slots.Add(i);if(slots.Count>0){int slot=slots[random.Next(slots.Count)];GiveWeapon(p,(WeaponKind)p.weapons[slot]);}break;
                case 16:p.pickupRange*=1.5f;break;case 23:p.goldGain+=.5f;p.xpGain+=.5f;break;case 25:p.extraJumps+=2;p.dodge+=.10f;break;
                case 28:p.reviveCharges++;break;case 30:p.realityTimer=.1f;break;
            }
            Effect(3, p.position, 2);
        }
        float DamageMultiplier(PlayerState p)
        {float growth=p.items[26]>0?1+State.elapsed*.0025f*p.items[26]:1;return p.damage*(1+p.auraBonuses[1])*growth*(p.realityBoost>0?2:1);}
        void Effect(int kind, Vector3 position, float radius)
        { State.effects.Add(new EffectState { id = State.nextId++, kind = kind, position = position, radius = radius }); }
        void GatherExperience()
        {
            int xp=0;for(int i=State.pickups.Count-1;i>=0;i--)if(State.pickups[i].kind==PickupKind.Experience)
            {xp+=State.pickups[i].value;State.pickups.RemoveAt(i);}GrantExperience(xp);
        }
    }
}
