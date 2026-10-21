using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public enum Hero { Ember, Echo }
    public enum Relic { StormCompass, Hourglass, HomeShard }
    public enum RunPhase { Lobby, Playing, Victory, Defeat }
    public enum EnemyKind { Wisp, Brute, Stalker, Warden }
    public enum PickupKind { Experience, Gold }
    public enum MenuPage { Home, Characters, Connection, HostLobby, JoinLobby, Settings }
    public enum SessionMode { Solo, Host, Join }
    public enum WeaponKind { Axe, LightningStaff, Bow, Spear }
    public enum RewardKind { Item, Weapon, Blessing }

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
        public float yaw, verticalSpeed;
        public float jumpBuffer, coyoteTime, castTime;
        public int jumpSerial, landSerial, castSerial, castKind;
        public float hp = 100, maxHp = 100, shield;
        public float attackTimer, qCooldown, eCooldown, relicCooldown, dashCooldown, dashTime, invulnerability, boost;
        public Vector3 dashDirection;
        public bool downed, eliminated;
        public float bleedout = 25, reviveProgress;
        public int kills, pendingLevels;
        public int[] items = new int[8];
        public int[] offers = Array.Empty<int>();
        public float damage = 1, attackSpeed = 1, moveSpeed = 1, pickupRange = 3, cooldownRate = 1;
        public float lifeSteal, weaponRange = 15, orbitTimer;
        public int projectileCount = 1;
        public int[] weapons = { -1, -1, -1, -1 };
        public int[] weaponLevels = new int[4];
        public float[] weaponTimers = new float[4];
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
        public int id;
        public EnemyKind kind;
        public Vector3 position;
        public float hp, maxHp, speed, damage, attackTimer, frozen, windup, specialTimer;
        public Vector3 attackPosition;
    }

    [Serializable]
    public sealed class ProjectileState
    {
        public int id, owner, target;
        public Vector3 position, direction;
        public float damage, life = 2.5f;
        public bool hostile;
        public WeaponKind weapon = WeaponKind.Bow;
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
        public int id, cost, tier, owner = -1, reward = -1;
        public Vector3 position;
        public bool opened;
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
        public int id,owner,content,quantity=1;
        public RewardKind kind;
        public float life=8;
    }

    [Serializable]
    public sealed class WorldState
    {
        public RunPhase phase;
        public int seed = 7341, wave = 1, level = 1, xp, xpRequired = 12, kills, nextId = 10;
        public float elapsed, spawnTimer, nextWave = 30;
        public bool bossSpawned;
        public int coins = 30, earnedCoins, openedChests;
        public float readyCountdown = -1, noticeTime;
        public string notice = "";
        public int noticeKind, noticeOwner, noticeContent, noticeQuantity;
        public bool multiplayer;
        public List<PlayerState> players = new List<PlayerState>();
        public List<EnemyState> enemies = new List<EnemyState>();
        public List<ProjectileState> projectiles = new List<ProjectileState>();
        public List<PickupState> pickups = new List<PickupState>();
        public List<EffectState> effects = new List<EffectState>();
        public List<ChestState> chests = new List<ChestState>();
        public List<LootState> loot = new List<LootState>();
        public List<ShrineState> shrines = new List<ShrineState>();
        public List<RewardNotice> rewards = new List<RewardNotice>();
        public PlayerState Player(int id) => players.Find(p => p.id == id);
    }

    public sealed class ItemDefinition
    {
        readonly string englishName, englishDescription;
        public string name => I18n.T(englishName);
        public string description => I18n.T(englishDescription);
        public readonly string symbol;
        public readonly Color color;
        public ItemDefinition(string name, string description, string symbol, Color color)
        { englishName = name; englishDescription = description; this.symbol = symbol; this.color = color; }
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
        public const int Protocol = 4;
        public const float ArenaRadius = 210;
        public const float BossTime = 300;
        public const int MaxEnemies = 90;
        public static readonly Color Amber = new Color(0.93f, 0.65f, 0.49f);
        public static readonly Color Mint = new Color(0.57f, 0.78f, 0.70f);
        public const int WeaponSlots = 4, MaxWeaponLevel = 5;
        public static readonly string[] ItemIcons = { "heart", "dawn", "coil", "bell", "lantern", "lens", "amber", "satellite" };
        public static readonly string[] BlessingIcons={"jump","bell","coil","dawn","heart","shield","hourglass","lantern"};
        public static string BlessingName(int kind)=>I18n.P(new[]{"Skybound","Windstride","Frenzy","Might","Vitality","Stoneguard","Quickening","Star Magnet"}[kind],new[]{"Небесный шаг","Поступь ветра","Неистовство","Мощь","Жизненная сила","Каменная защита","Ускорение","Звёздный магнит"}[kind]);
        public static string BlessingDescription(int kind)=>I18n.P(new[]{"+1 extra jump in the air. Stacks up to 3.","+15% movement speed for this run.","+20% automatic attack speed for this run.","+25% weapon and ability damage for this run.","+30 maximum health and restore 50 health.","Gain 50 shield. Absorbs incoming damage.","Abilities recharge 15% faster for this run.","+3 m pickup radius for this run."}[kind],new[]{"+1 прыжок в воздухе. Можно получить до 3.","+15% к скорости бега до конца забега.","+20% к скорости автоматических атак до конца забега.","+25% к урону оружия и способностей до конца забега.","+30 к максимальному здоровью и лечение на 50.","50 щита, поглощающего входящий урон.","Способности заряжаются на 15% быстрее до конца забега.","+3 м к радиусу подбора до конца забега."}[kind]);
        public static readonly WeaponDefinition[] Weapons =
        {
            new WeaponDefinition("Crescent Axe","A sweeping cleave through nearby enemies.","axe",35,1.15f,4.8f,Amber),
            new WeaponDefinition("Lightning Staff","Lightning chains between up to four nearby enemies.","lightning",24,1.05f,17,new Color(.69f,.66f,.90f)),
            new WeaponDefinition("Wildwood Bow","Fast arrows seek distant targets. Extra bolts add arrows.","bow",26,.76f,25,Mint),
            new WeaponDefinition("Dawn Spear","A piercing thrust hits every enemy along its path.","spear",38,1.0f,7.5f,new Color(.85f,.75f,.50f))
        };
        public static readonly ItemDefinition[] Items =
        {
            new ItemDefinition("Cinder Heart", "+25 maximum health. Immediately restore 35 health.", "+", Amber),
            new ItemDefinition("Dawn Shard", "+20% weapon and ability damage.", "/", new Color(0.92f, 0.58f, 0.57f)),
            new ItemDefinition("Copper Coil", "+15% automatic attack speed.", "=", Amber),
            new ItemDefinition("Wind Bell", "+8% movement speed. Cooldowns recover 10% faster.", "~", Mint),
            new ItemDefinition("Moon Lantern", "+2 m pickup radius. Gather all experience on the map now.", "*", new Color(0.70f, 0.69f, 0.88f)),
            new ItemDefinition("Quartz Lens", "+1 bolt per volley and +2 m range. Up to 5 bolts.", "<>", Mint),
            new ItemDefinition("Living Amber", "Heal for 2% of damage dealt. Stacks with each copy.", "o", new Color(0.78f, 0.84f, 0.58f)),
            new ItemDefinition("Satellite Seed", "Every 0.7 s, deal 12 damage per stack to enemies within 3.5 m.", "@", new Color(0.77f, 0.65f, 0.86f))
        };
        public static string HeroName(Hero hero) => I18n.T(hero == Hero.Ember ? "EMBER" : "ECHO");
        public static Color HeroColor(Hero hero) => hero == Hero.Ember ? Amber : Mint;
        public static string Q(Hero hero) => I18n.T(hero == Hero.Ember ? "Sunburst" : "Frost Bloom");
        public static string E(Hero hero) => I18n.T(hero == Hero.Ember ? "Overdrive" : "Resonance");
        public static string QIcon(Hero hero)=>hero==Hero.Ember?"sunburst":"frost";
        public static string EIcon(Hero hero)=>hero==Hero.Ember?"overdrive":"resonance";
        public static string RelicIcon(Relic relic)=>relic==Relic.StormCompass?"compass":relic==Relic.Hourglass?"hourglass":"home";
        public static string RelicName(Relic relic) => I18n.T(relic == Relic.StormCompass ? "Storm Compass" : relic == Relic.Hourglass ? "Hourglass" : "Home Shard");
        public static float RelicDuration(Relic relic) => relic == Relic.StormCompass ? 24 : relic == Relic.Hourglass ? 30 : 25;
        public static string RelicDescription(Relic relic) => I18n.T(relic == Relic.StormCompass
            ? "Deal 90 damage within 9 m. 24 s cooldown."
            : relic == Relic.Hourglass ? "Freeze enemies within 12 m for 4 s. Refresh dash. 30 s cooldown."
            : "Restore 50 health and gather all experience. 25 s cooldown.");
        public static string QDescription(Hero hero) => I18n.T(hero == Hero.Ember
            ? "65 damage within 7 m. 8 s cooldown."
            : "35 damage and a 3 s freeze within 8 m. 10 s cooldown.");
        public static string EDescription(Hero hero) => I18n.T(hero == Hero.Ember
            ? "Double fire rate and +30% movement for 6 s. 18 s cooldown."
            : "Restore 35 health and give 25 shield to allies within 12 m. 16 s cooldown.");
    }
}
