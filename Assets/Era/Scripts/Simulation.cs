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
            random = new System.Random(seed);
            State = new WorldState { seed = seed, phase = RunPhase.Lobby, multiplayer = multiplayer };
            WorldMap.PopulateChests(State);PopulateShrines();
        }
        public PlayerState AddPlayer(int id, string name, Hero hero, Relic relic = Relic.StormCompass)
        {
            if (State.players.Count >= 2 || State.Player(id) != null) return null;
            var p = new PlayerState { id = id, name = string.IsNullOrWhiteSpace(name) ? "Wanderer" : name.Substring(0, Math.Min(18, name.Length)),
                hero = hero, relic = relic, position = new Vector3(id == 0 ? -1.5f : 1.5f, 0, 0) };
            if (hero == Hero.Echo) { p.maxHp = p.hp = 115; p.damage = 0.9f; }
            GiveStarterWeapon(p);
            State.players.Add(p);
            ResetReadiness();
            return p;
        }
        public void Start()
        {
            if (State.phase == RunPhase.Lobby && (State.players.Count == 1 || State.players.Count == 2 && State.players.TrueForAll(p=>p.ready)))
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
            p.hero=hero==Hero.Echo?Hero.Echo:Hero.Ember; p.relic=(Relic)Mathf.Clamp((int)relic,0,2);
            p.name=string.IsNullOrWhiteSpace(name)?"Wanderer":name.Substring(0,Math.Min(18,name.Length));
            p.damage=p.hero==Hero.Echo?0.9f:1; p.maxHp=p.hp=p.hero==Hero.Echo?115:100;
            GiveStarterWeapon(p);
            return true;
        }
        public void Tick(float dt, IDictionary<int, InputFrame> inputs)
        {
            dt = Mathf.Clamp(dt, 0, 0.05f);
            if(State.phase==RunPhase.Lobby)
            {
                bool both=State.players.Count==2 && State.players.TrueForAll(p=>p.ready);
                if(!both) { State.readyCountdown=-1; return; }
                if(State.readyCountdown<0)State.readyCountdown=3;
                State.readyCountdown=Mathf.Max(0,State.readyCountdown-dt);
                if(State.readyCountdown<=0)Start(); return;
            }
            if (State.phase != RunPhase.Playing) return;
            foreach(var player in State.players)
                if(inputs.TryGetValue(player.id,out var choice)&&choice.choice>=0)ChooseItem(player,choice.choice);
            if(UpgradePaused)return;
            State.elapsed += dt;
            State.noticeTime=Mathf.Max(0,State.noticeTime-dt);
            State.wave = 1 + Mathf.FloorToInt(State.elapsed / 30);
            State.nextWave = 30 - State.elapsed % 30;
            foreach (var p in State.players)
            {
                inputs.TryGetValue(p.id, out var input);
                TickPlayer(p, input ?? new InputFrame(), dt);
            }
            TickShrines(dt,inputs);TickEnemies(dt);
            TickProjectiles(dt);
            TickLoot(dt);
            CollectPickups(dt);
            State.effects.RemoveAll(e => (e.life -= dt) <= 0);
            State.spawnTimer -= dt;
            if (State.spawnTimer <= 0 && !State.bossSpawned)
            {
                State.spawnTimer = Mathf.Max(0.25f, 1.35f - State.wave * 0.105f);
                int count = Mathf.Min(5, 1 + State.wave / 3 + (State.players.Count == 2 ? 1 : 0));
                for (int i = 0; i < count && State.enemies.Count < GameData.MaxEnemies; i++) SpawnEnemy();
            }
            if (State.elapsed >= GameData.BossTime && !State.bossSpawned)
            {
                State.bossSpawned = true;
                SpawnEnemy(EnemyKind.Warden);
            }
            if (State.players.TrueForAll(p => p.downed || p.eliminated)) State.phase = RunPhase.Defeat;
        }
        void TickPlayer(PlayerState p, InputFrame input, float dt)
        {
            p.qCooldown = Mathf.Max(0, p.qCooldown - dt * p.cooldownRate);
            p.eCooldown = Mathf.Max(0, p.eCooldown - dt * p.cooldownRate);
            p.relicCooldown = Mathf.Max(0, p.relicCooldown - dt * p.cooldownRate);
            p.dashCooldown = Mathf.Max(0, p.dashCooldown - dt * p.cooldownRate);
            p.invulnerability = Mathf.Max(0, p.invulnerability - dt);
            p.boost = Mathf.Max(0, p.boost - dt);
            p.castTime=Mathf.Max(0,p.castTime-dt);
            p.weaponPoseTime=Mathf.Max(0,p.weaponPoseTime-dt);
            p.shield = Mathf.Max(0, p.shield - dt * 0.55f);
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
            if(input.interact)TryOpenChest(p);
            Vector3 before=p.position;
            var move = Vector3.ClampMagnitude(new Vector3(Safe(input.x), 0, Safe(input.z)), 1);
            float facing=move.sqrMagnitude>0.02f?Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg:Safe(input.yaw);
            p.yaw = Mathf.MoveTowardsAngle(p.yaw,facing,720*dt);
            if (input.dash && p.dashCooldown <= 0)
            {
                p.dashDirection = move.sqrMagnitude > 0.01f ? move : Quaternion.Euler(0, p.yaw, 0) * Vector3.forward;
                p.dashTime = 0.19f; p.dashCooldown = 3; p.invulnerability = 0.28f;
                Effect(2, p.position + Vector3.up * 0.2f, 2);
            }
            if (p.dashTime > 0) { p.position=WorldMap.Resolve(p.position,p.dashDirection*24*dt,0.4f); p.dashTime -= dt; }
            else p.position=WorldMap.Resolve(p.position,move*(7.8f*p.moveSpeed*(p.boost>0?1.3f:1)*(p.crouching?.43f:1))*dt,0.4f);
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
            if (p.items[7] > 0 && (p.orbitTimer -= dt) <= 0)
            {
                p.orbitTimer = 0.7f;
                AreaDamage(p, 3.5f, 12 * p.items[7] * p.damage, 0);
                Effect(4, p.position, 3.5f);
            }
        }
        IDictionary<int, InputFrame> inputsForRevive;
        public void Step(float dt, IDictionary<int, InputFrame> inputs)
        { inputsForRevive = inputs; Tick(dt, inputs); }
        public bool UpgradePaused=>State.phase==RunPhase.Playing&&!State.multiplayer&&State.players.Exists(p=>!p.downed&&!p.eliminated&&p.pendingLevels>0);
        static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;

        void CastQ(PlayerState p)
        {
            CastPose(p,0);
            bool ember = p.hero == Hero.Ember;
            p.qCooldown = ember ? 8 : 10;
            AreaDamage(p, ember ? 7 : 8, (ember ? 65 : 35) * p.damage, ember ? 0 : 3);
            Effect(ember ? 0 : 1, p.position, ember ? 7 : 8);
        }
        void CastE(PlayerState p)
        {
            CastPose(p,1);
            if (p.hero == Hero.Ember) { p.boost = 6; p.eCooldown = 18; Effect(0, p.position, 2.5f); }
            else
            {
                p.eCooldown = 16;
                foreach (var other in State.players)
                    if (!other.downed && !other.eliminated && Vector3.Distance(p.position, other.position) <= 12)
                    { other.hp = Mathf.Min(other.maxHp, other.hp + 35); other.shield = Mathf.Min(75, other.shield + 25); }
                Effect(3, p.position, 12);
            }
        }
        void AreaDamage(PlayerState p, float radius, float damage, float frozen)
        {
            for (int i = State.enemies.Count - 1; i >= 0; i--)
            {
                var e = State.enemies[i];
                if (Vector3.Distance(p.position, e.position) > radius) continue;
                e.frozen = Mathf.Max(e.frozen, frozen);
                DamageEnemy(e, p, damage);
            }
        }
        void CastRelic(PlayerState p)
        {
            CastPose(p,2);
            p.relicCooldown = GameData.RelicDuration(p.relic);
            if (p.relic == Relic.StormCompass) { AreaDamage(p, 9, 90 * p.damage, 0); Effect(0, p.position, 9); }
            else if (p.relic == Relic.Hourglass)
            { AreaDamage(p, 12, 0, 4); p.dashCooldown = 0; Effect(1, p.position, 12); }
            else
            {
                p.hp = Mathf.Min(p.maxHp, p.hp + 50);
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
            float scaling = 1 + (State.wave - 1) * 0.14f;
            float hp = (kind == EnemyKind.Warden ? 2200 : kind == EnemyKind.Brute ? 110 : kind == EnemyKind.Stalker ? 32 : 44) * scaling;
            hp *= State.players.Count == 2 ? 1.5f : 1;
            var e = new EnemyState { id = State.nextId++, kind = kind, position = pos, hp = hp, maxHp = hp,
                speed = kind == EnemyKind.Warden ? 1.9f : kind == EnemyKind.Brute ? 1.55f : kind == EnemyKind.Stalker ? 4.1f : 2.65f,
                damage = kind == EnemyKind.Warden ? 28 : kind == EnemyKind.Brute ? 20 : 10, specialTimer = 4 };
            State.enemies.Add(e);
            return e;
        }
        void TickEnemies(float dt)
        {
            foreach (var e in State.enemies)
            {
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
                    if (e.windup > 0)
                    {
                        e.windup -= dt;
                        if (e.windup <= 0)
                        {
                            Effect(5, e.attackPosition, 5);
                            foreach (var p in State.players)
                                if (Vector3.Distance(p.position, e.attackPosition) < 5) DamagePlayer(p, 36);
                        }
                    }
                    else if ((e.specialTimer -= dt) <= 0)
                    { e.specialTimer = 5; e.windup = 1.35f; e.attackPosition = nearest.position; e.attackPosition.y = WorldMap.Ground(nearest.position); }
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
                { e.attackTimer = 1; DamagePlayer(nearest, e.damage); }
            }
        }
        void TickProjectiles(float dt)
        {
            for (int i = State.projectiles.Count - 1; i >= 0; i--)
            {
                var b = State.projectiles[i];
                b.life -= dt;
                var end = b.position + b.direction * 34 * dt;
                bool hit = false;
                for (int j = State.enemies.Count - 1; j >= 0; j--)
                {
                    var e = State.enemies[j];
                    float radius = e.kind == EnemyKind.Warden ? 1.5f : e.kind == EnemyKind.Brute ? 0.85f : 0.6f;
                    if (SegmentDistance(b.position, end, e.position + Vector3.up * 0.7f) > radius) continue;
                    DamageEnemy(e, State.Player(b.owner), b.damage); hit = true; break;
                }
                b.position = end;
                if (hit || b.life <= 0) State.projectiles.RemoveAt(i);
            }
        }
        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            var d = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(0.0001f, d.sqrMagnitude));
            return Vector3.Distance(a + d * t, p);
        }
        public void DamageEnemy(EnemyState e, PlayerState owner, float damage)
        {
            e.hp -= damage;
            if (owner != null) owner.hp = Mathf.Min(owner.maxHp, owner.hp + damage * owner.lifeSteal);
            if (e.hp > 0) return;
            State.enemies.Remove(e);
            State.kills++;
            if (owner != null) owner.kills++;
            Effect(6, e.position + Vector3.up * 0.5f, 0.8f);
            if (e.kind == EnemyKind.Warden) { State.phase = RunPhase.Victory; return; }
            var pickup = new PickupState { id = State.nextId++, position = e.position, value = e.kind == EnemyKind.Brute ? 5 : 2 };
            if (State.pickups.Count < 180) State.pickups.Add(pickup);
            else { var merge=State.pickups.Find(d=>d.kind==PickupKind.Experience);if(merge!=null)merge.value+=pickup.value;else GrantExperience(pickup.value); }
            var coin=new PickupState { id=State.nextId++,position=e.position+Vector3.right*0.55f,value=e.kind==EnemyKind.Brute?12:4,kind=PickupKind.Gold };
            if(State.pickups.Count<260)State.pickups.Add(coin);
            else { State.coins+=coin.value;State.earnedCoins+=coin.value; }
        }
        public void DamagePlayer(PlayerState p, float damage)
        {
            if (p.downed || p.eliminated || p.invulnerability > 0) return;
            float absorb = Mathf.Min(p.shield, damage); p.shield -= absorb; damage -= absorb;
            p.hp = Mathf.Max(0, p.hp - damage); p.invulnerability = 0.5f;
            Effect(5, p.position + Vector3.up, 0.7f);
            if (p.hp <= 0) { p.downed = true;p.crouching=false;p.chargingShrine=-1; p.bleedout = 25; p.position.y = WorldMap.Ground(p.position);p.grounded=true;p.velocity=Vector3.zero; }
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
            for (int i = 0; i < GameData.Items.Length; i++) if (i != 5 || p.projectileCount < 5) choices.Add(i);
            for (int i = choices.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int v = choices[i]; choices[i] = choices[j]; choices[j] = v; }
            p.offers = choices.GetRange(0, 3).ToArray();
        }
        public bool ChooseItem(PlayerState p, int slot)
        {
            if (p.pendingLevels <= 0 || slot < 0 || slot >= p.offers.Length || p.downed || p.eliminated) return false;
            ApplyItem(p, p.offers[slot]); p.pendingLevels--;
            if (p.pendingLevels > 0) RollOffers(p); else p.offers = Array.Empty<int>();
            return true;
        }
        public void ApplyItem(PlayerState p, int item)
        {
            if (item < 0 || item >= GameData.Items.Length) return;
            p.items[item]++;
            switch (item)
            {
                case 0: p.maxHp += 25; p.hp = Mathf.Min(p.maxHp, p.hp + 35); break;
                case 1: p.damage += 0.2f; break;
                case 2: p.attackSpeed += 0.15f; break;
                case 3: p.moveSpeed += 0.08f; p.cooldownRate += 0.1f; break;
                case 4:
                    p.pickupRange += 2; GatherExperience(); break;
                case 5: p.projectileCount = Mathf.Min(5, p.projectileCount + 1); p.weaponRange += 2; break;
                case 6: p.lifeSteal += 0.02f; break;
            }
            Effect(3, p.position, 2);
        }
        void Effect(int kind, Vector3 position, float radius)
        { State.effects.Add(new EffectState { id = State.nextId++, kind = kind, position = position, radius = radius }); }
        void GatherExperience()
        {
            int xp=0;for(int i=State.pickups.Count-1;i>=0;i--)if(State.pickups[i].kind==PickupKind.Experience)
            {xp+=State.pickups[i].value;State.pickups.RemoveAt(i);}GrantExperience(xp);
        }
    }
}
