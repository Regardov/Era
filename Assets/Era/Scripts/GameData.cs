using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public enum Hero { Ember, Echo, Lumen }
    public enum Relic { StormCompass, Hourglass, HomeShard }
    public enum RunPhase { Lobby, Playing, Victory, Defeat }
    public enum EnemyKind { Wisp, Brute, Stalker, Warden, Skeleton, SkeletonArcher, Revenant, Necromancer, StoneGolem, CrystalGolem, Slime, AcidSlime, Ogre, OgreShaman, Bat, Gargoyle }
    public enum PickupKind { Experience, Gold }
    public enum MenuPage { Home, Characters, Connection, HostLobby, JoinLobby, Settings, Catalogue }
    public enum SessionMode { Solo, Host, Join }
    public enum WeaponKind { Axe, LightningStaff, Bow, Spear, Aura, Sword }
    public enum RewardKind { Item, Weapon, Blessing, Accessory, Aura }
    public enum ItemRarity { Rare, Uncommon, Epic, Legendary, Mythic }

    [Serializable]
    public sealed class InputFrame
    {
        public float x, z, yaw;
        public bool jump, jumpHeld, dash, primary, secondary, relic, revive, interact, crouch;
        public int choice = -1;
        public InputFrame Copy() => (InputFrame)MemberwiseClone();
        public void ConsumeEdges() { jump = dash = primary = secondary = relic = interact = false; choice = -1; }
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int id;
        public string name;
        public Hero hero;
        public Relic relic;
        public bool ready, grounded = true;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 knockback;
        public float hitSlide;
        public float weaponAimYaw;
        public float yaw, verticalSpeed;
        public float jumpBuffer, coyoteTime, castTime;
        public int jumpSerial, landSerial, castSerial, castKind;
        public float hp = 100, maxHp = 100, shield;
        public float attackTimer, qCooldown, eCooldown, relicCooldown, dashCooldown, dashTime, invulnerability, boost;
        public Vector3 dashDirection;
        public bool downed, eliminated;
        public float bleedout = 25, reviveProgress;
        public int kills, pendingLevels;
        public int[] items = new int[ItemCatalog.Count];
        public float[] itemTimers = new float[ItemCatalog.Count], itemValues = new float[ItemCatalog.Count];
        public int[] itemCounters = new int[ItemCatalog.Count];
        public float stillTime, distanceWalked, combatTime, safeTime, lastHp, storedDamage;
        public int lastVictim=-1, alternatingSlot=-1, lastLand, lastJump;
        public List<WeaponBurstState> bursts = new List<WeaponBurstState>();
        public int[] attributes = new int[21];
        public int[] accessories = new int[84]; // One stack counter per stat and rarity.
        public int completedLevel = 1, upgradeStage;
        public RewardKind offerKind = RewardKind.Accessory;
        public int[] auraLevels = new int[8];
        public int currentAura;
        public float[] auraBonuses = new float[8];
        public float auraMaxHp, auraSurge, auraDamageTimer, slowed;
        public int[] offers = Array.Empty<int>();
        public int[] offerRarities = Array.Empty<int>();
        public float damage = 1, attackSpeed = 1, moveSpeed = 1, pickupRange = 3, cooldownRate = 1;
        public float lifeSteal, weaponRange = 15, orbitTimer, critChance=.05f,critDamage=1.5f,healingPower=1,hpRegen,armor,dodge,areaSize=1,projectileSpeed=1,luck,xpGain=1,goldGain=1;
        public float fireTimer,dragonTimer,blackHoleTimer,chronoTimer,portalTimer,realityTimer,realityBoost,xpRemainder,goldRemainder;
        public int reviveCharges;
        public int projectileCount = 1;
        public int[] weapons = { -1, -1 };
        public int[] weaponLevels = new int[2];
        public float[] weaponTimers = new float[2];
        public int weaponCastSerial, lastWeapon = -1;
        public float weaponPoseTime;
        public bool crouching;
        public int extraJumps,airJumpsUsed;
        public int[] blessings=new int[8];
        public int chargingShrine=-1;
    }

    [Serializable]
    public sealed class EnemyState
    {
        public int id, hitSerial, bossStage, attackCycle, wave, eliteShrine=-1;
        public EnemyKind kind;
        public Vector3 position;
        public float hp, maxHp, speed, damage, attackTimer, frozen, windup, specialTimer, spawnGrace, meleeWindup;
        public float recovery, attackDuration, facing, contactTimer;
        public int attackSerial, attackStyle, summonGeneration;
        public Vector3 attackDirection;
        public List<AfflictionState> ailments = new List<AfflictionState>();
        public Vector3 attackPosition;
    }

    [Serializable]
    public sealed class ProjectileState
    {
        public int id, owner, target;
        public Vector3 position, direction;
        public float damage, life = 2.5f;
        public bool hostile;
        public float speed=34;
        public int pierce, bounces;
        public bool returning;
        public List<int> hitTargets = new List<int>();
        public WeaponKind weapon = WeaponKind.Bow;
    }

    [Serializable] public sealed class WeaponBurstState
    {
        public int slot, remaining, total;
        public WeaponKind weapon;
        public float delay, interval, damage, range;
        public List<int> usedTargets=new List<int>();
    }
    public enum Ailment { Burn, Poison, Bleed, Chill, Shock, Mark, Doom }
    [Serializable] public sealed class AfflictionState
    {
        public Ailment kind; public int owner, stacks=1, hits; public float time, power, tick=.5f;
    }
    [Serializable] public sealed class ItemFieldState
    {
        public int id, owner, item, target=-1; public Vector3 position, end;
        public float time, tick, power, radius; public bool armed;
    }

    [Serializable]
    public sealed class PickupState
    {
        public int id, value;
        public PickupKind kind;
        public Vector3 position;
        public float age;
    }

    [Serializable]
    public sealed class ChestState
    {
        public int id, cost, baseCost, tier, owner = -1, reward = -1, rewardRarity;
        public Vector3 position;
        public bool opened, free;
        public RewardKind rewardKind;
    }

    [Serializable]
    public sealed class LootState
    {
        public int id, owner, content, quantity = 1;
        public RewardKind kind;
        public Vector3 origin, position, collectOrigin;
        public float age, collectTime;
        public bool collecting;
    }

    [Serializable]
    public sealed class HitState
    {
        public int id, target;
        public Vector3 position;
        public float damage, life = .9f;
        public bool critical, player, evaded;
    }

    [Serializable] public sealed class PortalState
    {
        public bool active;
        public Vector3 position;
    }
    [Serializable] public sealed class HazardState
    {
        public int id, style;
        public Vector3 position;
        public float radius, delay, life, damage, tick;
        public bool triggered;
    }

    [Serializable]
    public sealed class EffectState
    {
        public int id, kind;
        public Vector3 position, end;
        public float radius, life = 0.55f;
        public WeaponKind weapon;
    }

    [Serializable]public sealed class ShrineState
    {
        public int id,style,owner=-1,buff=-1;
        public Vector3 position;
        public float charge;
        public bool spent;
    }
    [Serializable]public sealed class RewardNotice
    {
        public int id,owner,content,rarity,quantity=1;
        public RewardKind kind;
        public float life=8;
    }

    [Serializable]
    public sealed class ResourceNodeState
    {
        public int id,hitSerial;
        public Vector3 position;
        public PickupKind kind;
        public float hp=24;
        public bool broken;
    }
    [Serializable]public sealed class EliteShrineState
    {
        public int id;
        public Vector3 position;
        public bool active,completed;
    }
    [Serializable]
    public sealed class WorldState
    {
        public RunPhase phase;
        public int seed = 7341, wave = 1, level = 1, xp, xpRequired = 12, kills, nextId = 10;
        public float elapsed, stageElapsed, spawnTimer, nextWave = 60;
        public int spawnCursor;
        public int stage = 1, stageSerial;
        public PortalState portal = new PortalState();
        public bool bossSpawned;
        public Vector3 bossStatuePosition;
        public int waveQuota,waveSpawned;
        public bool waveActive;
        public int coins = 30, earnedCoins, openedChests, chestsOpenedInStage;
        public float readyCountdown = -1, noticeTime;
        public string notice = "";
        public int noticeKind, noticeOwner, noticeContent, noticeQuantity;
        public bool multiplayer;
        public List<PlayerState> players = new List<PlayerState>();
        public List<EnemyState> enemies = new List<EnemyState>();
        public List<ProjectileState> projectiles = new List<ProjectileState>();
        public List<PickupState> pickups = new List<PickupState>();
        public List<EffectState> effects = new List<EffectState>();
        public List<HitState> hits = new List<HitState>();
        public List<HazardState> hazards = new List<HazardState>();
        public List<ChestState> chests = new List<ChestState>();
        public List<LootState> loot = new List<LootState>();
        public List<ShrineState> shrines = new List<ShrineState>();
        public List<RewardNotice> rewards = new List<RewardNotice>();
        public List<ResourceNodeState> resourceNodes = new List<ResourceNodeState>();
        public List<EliteShrineState> eliteShrines = new List<EliteShrineState>();
        public List<ItemFieldState> itemFields = new List<ItemFieldState>();
        public PlayerState Player(int id) => players.Find(p => p.id == id);
    }

    public sealed class ItemDefinition
    {
        readonly string englishName,russianName,englishDescription,russianDescription;
        public string name => I18n.P(englishName,russianName);
        public string description => I18n.P(englishDescription,russianDescription);
        public readonly ItemRarity rarity;
        public readonly string icon, synergy, stacking;
        public Color color=>GameData.RarityColor(rarity);
        public ItemDefinition(string en,string ru,string enDescription,string ruDescription,ItemRarity rarity,string icon="amulet",string synergy="",string stacking="")
        {englishName=en;russianName=ru;englishDescription=enDescription;russianDescription=ruDescription;this.rarity=rarity;this.icon=icon;this.synergy=synergy;this.stacking=stacking;}
    }

    public sealed class AttributeDefinition
    {
        readonly string englishName,russianName;
        public string name=>I18n.P(englishName,russianName);
        public readonly float[] values;
        public readonly bool percent;
        public AttributeDefinition(string en,string ru,bool percent,params float[] values)
        {englishName=en;russianName=ru;this.percent=percent;this.values=values;if(values.Length>=2){float low=values[0];values[0]=values[1];values[1]=low;}}
        public string Value(int rarity)
        {
            float value=values[Mathf.Clamp(rarity,0,values.Length-1)];
            return percent?"+"+Mathf.RoundToInt(value*100)+"%":"+"+value.ToString(value%1==0?"0":"0.##");
        }
    }

    public sealed class WeaponDefinition
    {
        readonly string englishName, englishDescription;
        public string name=>I18n.T(englishName);
        public string description=>I18n.T(englishDescription);
        public readonly string icon;
        public readonly float damage, cooldown, range;
        public readonly Color color;
        public WeaponDefinition(string name,string description,string icon,float damage,float cooldown,float range,Color color)
        {englishName=name;englishDescription=description;this.icon=icon;this.damage=damage;this.cooldown=cooldown;this.range=range;this.color=color;}
    }

    public static class GameData
    {
        public const int Port = 7777;
        public const int Protocol = 12;
        public const int MaxPlayers = 3;
        public const float ArenaRadius = 300;
        public const int ChestCount=240,ResourceNodeCount=72;
        public static WeaponKind StarterWeapon(Hero hero)=>hero==Hero.Lumen?WeaponKind.Aura:hero==Hero.Ember?WeaponKind.Bow:WeaponKind.LightningStaff;
        public static string PassiveDescription(Hero hero)=>hero==Hero.Lumen?I18n.P("Aura keeper: one aura decision at each level. Add a new buff or improve an owned aura; up to five types.","Хранитель аур: на каждом уровне новая аура или улучшение изученной. До пяти типов; бонусы получают союзники в радиусе."):hero==Hero.Ember?I18n.P("Hunter's training: arrows travel 15% faster, making distant targets easier to hit.","Охотничья подготовка: снаряды летят на 15% быстрее — легче попадать по дальним целям."):I18n.P("Hardiness: 115 starting health, 15 more than the base hero. Base outgoing damage is 90%.","Закалка: 115 начальных ХП, на 15 больше базового героя. Базовый исходящий урон — 90%.");
        public const float BossTime = 150;
        public const int MaxEnemies = 90;
        public const int MaxProjectiles=384,MaxEffects=320;
        public static readonly Color Amber = new Color(0.93f, 0.65f, 0.49f);
        public static readonly Color Mint = new Color(0.57f, 0.78f, 0.70f);
        public const int WeaponSlots = 2, MaxWeaponLevel = 5, SecondWeaponLevel = 25, MaxAuras = 5;
        public static readonly Color[] RarityColors={new Color(.10f,.60f,1),new Color(.10f,.88f,.31f),new Color(.76f,.08f,1),new Color(1,.73f,.06f),new Color(1,.22f,.40f)};
        public static readonly string[] RarityNamesEn={"RARE","UNCOMMON","EPIC","LEGENDARY","MYTHIC"};
        public static readonly string[] RarityNamesRu={"РЕДКОЕ","НЕОБЫЧНОЕ","ЭПИЧЕСКОЕ","ЛЕГЕНДАРНОЕ","МИФИЧЕСКОЕ"};
        public static Color RarityColor(ItemRarity rarity)=>RarityColors[(int)rarity];
        public static string RarityName(int rarity)=>I18n.P(RarityNamesEn[Mathf.Clamp(rarity,0,4)],RarityNamesRu[Mathf.Clamp(rarity,0,4)]);
        public static readonly string[] BlessingIcons={"jump","bell","coil","dawn","heart","shield","hourglass","lantern"};
        public static string BlessingName(int kind)=>I18n.P(new[]{"Skybound","Windstride","Frenzy","Might","Vitality","Stoneguard","Quickening","Star Magnet"}[kind],new[]{"Небесный шаг","Поступь ветра","Неистовство","Мощь","Жизненная сила","Каменная защита","Ускорение","Звёздный магнит"}[kind]);
        public static string BlessingDescription(int kind)=>I18n.P(new[]{"+1 extra jump in the air. Stacks up to 3.","+15% movement speed for this run.","+20% automatic attack speed for this run.","+25% weapon and ability damage for this run.","+30 maximum health and restore 50 health.","Gain 50 shield. Absorbs incoming damage.","Abilities recharge 15% faster for this run.","+3 m pickup radius for this run."}[kind],new[]{"+1 прыжок в воздухе. Можно получить до 3.","+15% к скорости бега до конца забега.","+20% к скорости автоматических атак до конца забега.","+25% к урону оружия и способностей до конца забега.","+30 к максимальному здоровью и лечение на 50.","50 щита, поглощающего входящий урон.","Способности заряжаются на 15% быстрее до конца забега.","+3 м к радиусу подбора до конца забега."}[kind]);
        public static readonly WeaponDefinition[] Weapons =
        {
            new WeaponDefinition("Crescent Axe","Each heavy swing hits one enemy. Extra strikes follow in sequence.","axe",35,1.15f,4.8f,Amber),
            new WeaponDefinition("Lightning Staff","Each beam hits one enemy. Extra beams fire simultaneously; Ricochet Seed adds chain hits.","lightning",24,1.05f,17,new Color(.69f,.66f,.90f)),
            new WeaponDefinition("Wildwood Bow","Fast arrows seek distant targets. Extra bolts add arrows.","bow",26,.76f,25,Mint),
            new WeaponDefinition("Dawn Spear","Each thrust hits one enemy. Extra thrusts follow in a short sequence.","spear",38,1.0f,7.5f,new Color(.85f,.75f,.50f)),
            new WeaponDefinition("Aura Conduit","10 base damage per second and buffs within 9.2 m. Choose or upgrade one aura per level.","aura",10,1,12/1.3f,new Color(.87f,.76f,1)),
            new WeaponDefinition("Dawn Sword","Each slash hits one enemy. Extra projectiles repeat the slash with a delay.","blade",30,.85f,4.5f,new Color(.9f,.84f,.66f))
        };
        public static readonly ItemDefinition[] Items = ItemCatalog.Build();
        public static readonly AttributeDefinition[] Attributes=
        {
            new AttributeDefinition("Damage","Урон",true,.06f,.12f,.20f,.30f),
            new AttributeDefinition("Attack speed","Скорость атаки",true,.05f,.10f,.18f,.28f),
            new AttributeDefinition("Critical chance","Шанс крита",true,.03f,.07f,.15f,.25f),
            new AttributeDefinition("Critical damage","Критический урон",true,.15f,.30f,.50f,.75f),
            new AttributeDefinition("Health","Здоровье",false,10,20,30,40),
            new AttributeDefinition("Healing","ХП / лечение",true,.08f,.16f,.28f,.40f),
            new AttributeDefinition("Health regeneration","Реген HP",false,.25f,.5f,1,2),
            new AttributeDefinition("Shield","Щит",false,15,30,50,80),
            new AttributeDefinition("Armor","Броня",true,.04f,.08f,.14f,.22f),
            new AttributeDefinition("Lifesteal","Вампиризм",true,.015f,.03f,.06f,.10f),
            new AttributeDefinition("Movement speed","Скорость",true,.04f,.08f,.14f,.22f),
            new AttributeDefinition("Evasion","Уклонение",true,.03f,.06f,.10f,.16f),
            new AttributeDefinition("Cooldown reduction","Снижение КД",true,.05f,.10f,.18f,.28f),
            new AttributeDefinition("Range","Дальность",false,1,2,4,6),
            new AttributeDefinition("Area","Радиус",true,.05f,.10f,.20f,.35f),
            new AttributeDefinition("Projectile count","Кол-во снарядов",false,1,1,2,3),
            new AttributeDefinition("Projectile speed","Скорость снаряда",true,.08f,.15f,.25f,.40f),
            new AttributeDefinition("Magnet","Магнит",false,.75f,1.5f,3,5),
            new AttributeDefinition("Luck","Удача",false,1,2,4,7),
            new AttributeDefinition("Experience","Опыт",true,.08f,.15f,.25f,.40f),
            new AttributeDefinition("Gold gain","Доход золота",true,.08f,.15f,.25f,.40f)
        };
        public static readonly string[] ItemIcons=Array.ConvertAll(Items,item=>item.icon);
        public static readonly string[] AccessoryIcons={"blade","gloves","ring","target","amulet","potion","leaf","shield","armor","fang","boots","wingboots","hourglass","lens","fireorb","stars","arrow","magnet","clover","book","gold"};
        static readonly string[] AccessoriesEn={"Warrior's charm","Quickstrike gloves","Precision ring","Hunter's lens","Life amulet","Healer's pendant","Living sprout","Guardian's seal","Iron brooch","Blood ruby","Traveler's boots","Feather anklet","Time locket","Eagle monocle","Orbit bracelet","Quiver charm","Wind fletching","Lodestone","Lucky clover","Scholar's tome","Merchant's purse"};
        static readonly string[] AccessoriesRu={"Талисман воина","Перчатки быстроты","Кольцо точности","Линза охотника","Амулет жизни","Подвеска целителя","Живой росток","Печать стража","Железная брошь","Кровавый рубин","Сапоги странника","Браслет пера","Медальон времени","Монокль орла","Орбитальный браслет","Оберег колчана","Оперение ветра","Магнитный камень","Клевер удачи","Том учёного","Кошель торговца"};
        public static string AccessoryName(int stat)=>I18n.P(AccessoriesEn[stat],AccessoriesRu[stat]);
        public static string AccessoryDescription(int stat,int rarity)=>Attributes[stat].name+" "+Attributes[stat].Value(rarity);
        public static readonly string[] AuraIcons={"clover","blade","gloves","heart","wingboots","shield","hourglass","book"};
        public static readonly Color[] AuraColors={new Color(.46f,1,.48f),new Color(1,.48f,.30f),new Color(1,.80f,.25f),new Color(1,.4f,.61f),new Color(.3f,.9f,1),new Color(.42f,.62f,1),new Color(.8f,.48f,1),new Color(1,.93f,.55f)};
        public static readonly float[] AuraBase={.15f,.12f,.12f,.15f,.10f,.10f,.12f,.15f};
        public static int AuraUpgradeCount(PlayerState player,int aura)=>Mathf.Max(0,player.auraLevels[aura]-1);
        public static string AuraProgress(PlayerState player,int aura)=>I18n.P("Rank ","Ранг ")+player.auraLevels[aura]+I18n.P(" · Upgrades: "," · Улучшений: ")+AuraUpgradeCount(player,aura);
        // S[n] = ceil(S[n-1] * 5/3). Round each step identically and saturate safely.
        public static int ChestPrice(int baseCost,int openedCount)
        {long price=Mathf.Max(1,baseCost);for(int n=0;n<Mathf.Max(0,openedCount)&&price<int.MaxValue;n++)price=System.Math.Min(int.MaxValue,(price*5+2)/3);return (int)price;}
        public static float EvasionChance(float value)=>Mathf.Clamp(value,0,.95f);
        public static float CombineEvasion(float current,float gain)=>Mathf.Min(.95f,EvasionChance(current)+(1-EvasionChance(current))*Mathf.Clamp01(gain));
        public static int LifestealHealth(float chance)=>1+Mathf.CeilToInt(Mathf.Max(0,chance-1)/.5f);
        public static string AuraName(int aura)=>I18n.P(new[]{"Fortune aura","Might aura","Haste aura","Vitality aura","Wind aura","Protection aura","Quickening aura","Wisdom aura"}[aura],new[]{"Аура удачи","Аура мощи","Аура быстроты","Аура жизни","Аура ветра","Аура защиты","Аура ускорения","Аура мудрости"}[aura]);
        public static float AuraPower(int aura,int rank)=>AuraBase[aura]*(1+.2f*(Mathf.Max(1,rank)-1));
        public static string AuraDescription(int aura,int rank)=>I18n.P(new[]{"Better epic / legendary drop odds","Damage","Attack speed","Maximum health","Movement speed","Incoming damage reduced","Ability recharge speed","Experience gained"}[aura],new[]{"Шанс эпических / легендарных предметов","Урон","Скорость атаки","Максимальное ХП","Скорость бега","Снижение входящего урона","Скорость перезарядки","Получаемый опыт"}[aura])+" +"+Mathf.RoundToInt(AuraPower(aura,rank)*100)+"%";
        public static string HeroName(Hero hero) => hero==Hero.Lumen?I18n.P("LUMEN","ЛЮМЕН"):I18n.T(hero == Hero.Ember ? "EMBER" : "ECHO");
        public static Color HeroColor(Hero hero) => hero==Hero.Lumen?new Color(.87f,.76f,1):hero == Hero.Ember ? Amber : Mint;
        public static string Q(Hero hero) => hero==Hero.Lumen?I18n.P("Light pulse","Импульс света"):I18n.T(hero == Hero.Ember ? "Sunburst" : "Frost Bloom");
        public static string E(Hero hero) => hero==Hero.Lumen?I18n.P("Convergence","Созвучие"):I18n.T(hero == Hero.Ember ? "Overdrive" : "Resonance");
        public static string QIcon(Hero hero)=>hero==Hero.Lumen?"aura":hero==Hero.Ember?"sunburst":"frost";
        public static string EIcon(Hero hero)=>hero==Hero.Lumen?"resonance":hero==Hero.Ember?"overdrive":"resonance";
        public static string RelicIcon(Relic relic)=>relic==Relic.StormCompass?"compass":relic==Relic.Hourglass?"hourglass":"home";
        public static string RelicName(Relic relic) => I18n.T(relic == Relic.StormCompass ? "Storm Compass" : relic == Relic.Hourglass ? "Hourglass" : "Home Shard");
        public static float RelicDuration(Relic relic) => relic == Relic.StormCompass ? 24 : relic == Relic.Hourglass ? 30 : 25;
        public static string RelicDescription(Relic relic) => I18n.T(relic == Relic.StormCompass
            ? "Deal 90 damage within 9 m. 24 s cooldown."
            : relic == Relic.Hourglass ? "Freeze enemies within 12 m for 4 s. Refresh dash. 30 s cooldown."
            : "Restore 50 health and gather all experience. 25 s cooldown.");
        public static string QDescription(Hero hero) => hero==Hero.Lumen?I18n.P("45 damage within 8 m; heal nearby allies for 20. 8 s cooldown.","45 урона в радиусе 8 м; лечит союзников на 20 ХП. КД 8 с."):I18n.T(hero == Hero.Ember
            ? "65 damage within 7 m. 8 s cooldown."
            : "35 damage and a 3 s freeze within 8 m. 10 s cooldown.");
        public static string EDescription(Hero hero) => hero==Hero.Lumen?I18n.P("For 6 s, auras are 50% stronger and reach 4.6 m farther. 18 s cooldown.","На 6 с ауры сильнее на 50%, радиус больше на 4.6 м. КД 18 с."):I18n.T(hero == Hero.Ember
            ? "Double fire rate and +30% movement for 6 s. 18 s cooldown."
            : "Restore 35 health and give 25 shield to allies within 12 m. 16 s cooldown.");
    }
}
