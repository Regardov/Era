using System;
using UnityEngine;

namespace Era
{
    // IDs are stable across inventory, host snapshots and the reference artwork's first 31 cells.
    public enum ItemId
    {
        SharpBlade, SwiftBoots, StoutHeart, SteelShield, Hourglass, GoldenCoin, BeginnersLuck, HeartyMeal,
        SmallPotion, DoubleShot, SpikedGloves, WingedBoots, VampireFang, TrueSight, BookOfKnowledge,
        FireSphere, Magnet, FlameBlade, IceCore, StormAmulet, Necronomicon, Chronosphere, ChaosPortal,
        CrownOfGreed, DragonBreath, AngelWings, InfinitePower, BlackHole, Rebirth, TimeMaster, RealityCube,
        CopperSpring, Spyglass, ChalkCircle, WindFletching, ScholarSeal, FeatherAnklet, IronBrooch,
        HealersRibbon, DewBead, NomadPin, DuelistKnot, CartographerSeal, LeadPendulum, WardensBadge,
        ApprenticeCord, BloodGlass, CometDust, MerchantScale, SilverThread, SteadyHand,
        GlassCannon, LeadBoots, BloodEngine, SniperOath, BrawlerOath, ThornMail, FrostBell,
        VenomNeedle, SerratedCoin, ThunderRod, DashFlint, LandingStone, RetaliationSeal, SoulJar,
        PowderKeg, HuntersDebt, SilentVow, LoadedDice, HeavyArrow, RicochetSeed,
        SteamValve, StormConductor, SepticWound, ShatterLens, AshGarden, BloodCompass,
        ExecutionLedger, EchoChamber, Alternator, KineticSpool, GlacialPrison, Capacitor,
        WarDrum, HuntingPack, MirrorShard, OrbitalSaw, Thunderstep, SacrificialBrazier,
        MiserFuse, VoidAnchor, PhoenixAsh, PlagueDoctor, RedThread, SpellLoom, FrostfireCovenant,
        DuelistClock, Overgrowth, SoulTithe, BrokenMetronome, StarMap,
        PacifistEngine, GravePact, TemporalDebt, MobiusQuiver, ParasiteCrown, InversionHeart,
        EventHorizon, GlassWorld, WalkingCathedral, BloodMoon, StormCircuit, SplitSoul,
        WinterSleep, LivingArsenal, VoidShepherd, AlchemyEngine, FateLoom, HiveMind,
        WorldSeed, LastPilgrim, DebtOfKing, Chronicle, Ouroboros, ArchitectReign, EclipseCrown,
        ImmortalContract, SingularityTestament, SevenfoldOath, EndlessRefrain
    }

    public static class ItemCatalog
    {
        public const int Count=130;
        const ItemRarity U=ItemRarity.Uncommon,R=ItemRarity.Rare,E=ItemRarity.Epic,M=ItemRarity.Mythic,L=ItemRarity.Legendary;
        static ItemDefinition D(string en,string ru,string english,string russian,ItemRarity rarity,string icon,string synergy="",string stacks="")
            =>new ItemDefinition(en,ru,english,russian,rarity,icon,synergy,stacks);
        public static ItemDefinition[] Build()=>new[]
        {
            D("Sharp Blade","Острый клинок","+15% damage.","+15% урона.",U,"blade"),
            D("Swift Boots","Быстрые ботинки","+10% movement speed.","+10% скорости бега.",U,"boots"),
            D("Stout Heart","Крепкое сердце","+20% base maximum health.","+20% текущего максимума ХП без бонуса ауры.",U,"heart"),
            D("Steel Shield","Стальной щит","Gain 35 shield.","Получить 35 щита.",U,"shield"),
            D("Hourglass","Песочные часы","+11% ability recharge speed.","+11% скорости восстановления способностей.",U,"hourglass"),
            D("Golden Coin","Золотая монета","+20% gold gain.","+20% получаемого золота.",U,"gold"),
            D("Beginner's Luck","Удача новичка","+15 percentage points of luck.","+15 процентных пунктов удачи.",U,"clover"),
            D("Hearty Meal","Сытный обед","Regenerate 1 HP each second.","Регенерация +1 ХП/с.",U,"meal"),
            D("Small Potion","Малое зелье","+18% healing power.","+18% силы лечения.",U,"potion"),
            D("Double Shot","Дабл-шот","+1 projectile per volley. Two copies give three projectiles.","+1 снаряд к каждой атаке оружия. Два подбора дают 3 снаряда: посох сразу, меч и копьё очередью.",R,"stars","Меткие залпы и эффекты при попадании.","Каждая копия добавляет ровно 1 снаряд; общий предел 48."),
            D("Spiked Gloves","Шипованные перчатки","+12% critical chance.","+12% шанса критического удара.",U,"gloves"),
            D("Winged Boots","Крылатые ботинки","+8% speed and +5% evasion.","+8% скорости бега и +5% уклонения.",U,"wingboots"),
            D("Vampire Fang","Вампирский клык","+6% lifesteal.","+6% вампиризма.",U,"fang"),
            D("True Sight","Точный прицел","+30% critical damage.","+30% критического урона.",U,"target"),
            D("Book of Knowledge","Книга знаний","Upgrade a random equipped weapon. At max level gain 30 XP.","Улучшает случайное оружие на уровень. Если всё улучшено — 30 опыта.",R,"book"),
            D("Fire Sphere","Огненная сфера","Every 4 s, burn enemies within 4 m for 8 damage/s for 3 s.","Каждые 4 с поджигает врагов в 4 м: 8 урона/с на 3 с.",R,"fireorb","Паровой клапан, Пепельный сад.","+8 урона/с за копию."),
            D("Magnet","Магнит","+50% pickup radius.","+50% радиуса подбора.",U,"magnet"),
            D("Flame Blade","Клинок пламени","Hits burn for 6 damage/s for 3 s. Up to 5 burn stacks.","Попадания поджигают на 3 с: 6 урона/с, до 5 слоёв горения.",E,"flameblade","Лёд создаёт пар; пепел растит огненные поля.","+6 урона/с за копию."),
            D("Ice Core","Ледяное ядро","Hits chill for 2 s, slowing movement by 35%.","Попадания охлаждают на 2 с и замедляют на 35%.",E,"icecore","Ледяная тюрьма, Зимний сон, Паровой клапан.","+1 с длительности за копию."),
            D("Storm Amulet","Грозовой амулет","Every fourth hit shocks a second enemy for 24 damage.","Каждое 4-е попадание бьёт второго врага молнией на 24 урона и оставляет заряд.",E,"stormamulet","Проводник грозы, Грозовой контур.","+24 урона за копию."),
            D("Necronomicon","Некрономикон","Every third kill sends a spirit through up to 3 enemies.","Каждое 3-е убийство выпускает духа: 35 урона по трём соседним врагам.",E,"necronomicon","Могильный договор, Охотничья стая.","+35 урона за копию."),
            D("Chronosphere","Хроносфера","Every 15 s freezes enemies within 9 m for 3 s.","Каждые 15 с замораживает врагов в 9 м на 3 с.",E,"chronosphere","Линза раскола, Зимний сон.","+1 с заморозки за копию."),
            D("Chaos Portal","Портал хаоса","Every 6 s opens a 3 s vortex at the nearest enemy.","Каждые 6 с открывает воронку на 3 с у ближайшего врага; стягивает цели в 5 м.",E,"portal","Поля огня, пилы и атаки по площади.","+1 м радиуса за копию."),
            D("Crown of Greed","Корона жадности","+50% gold and XP, but take 25% more damage. Every 100 gold collected releases an 80-damage nova.","+50% золота и опыта, но входящий урон +25%. Каждые 100 подобранных золотых выпускают волну на 80 урона в 7 м.",E,"crown","Долг короля, Фитиль скряги."),
            D("Dragon Breath","Драконий дух","Every 7 s burns a forward cone for 60 damage and ignites it.","Каждые 7 с выдыхает пламя в конусе 10 м: 60 урона и горение.",E,"dragon","Пепельный сад; направление задаёт поворот героя.","+60 урона за копию."),
            D("Angel Wings","Ангельские крылья","Two air jumps; landing after an air jump releases a holy wave.","Даёт 2 воздушных прыжка. Приземление после воздушного прыжка выпускает волну на 55 урона в 6 м.",M,"wings","Камень приземления, Поступь грома.","+1 прыжок и +55 урона волны за повтор."),
            D("Infinite Power","Бесконечная мощь","Every 20 kills permanently adds 4% damage for the run.","Каждые 20 ваших убийств навсегда до конца забега добавляют 4% урона.",L,"infinity","Билды массового уничтожения.","Прирост за порог умножается на число копий."),
            D("Black Hole","Чёрная дыра","Every 10 s creates a singularity: pulls, then implodes after 3 s.","Каждые 10 с создаёт сингулярность: 3 с стягивает врагов и схлопывается на 100 урона.",M,"blackhole","Завещание сингулярности расширяет радиус.","+100 урона схлопывания за копию."),
            D("Rebirth","Перерождение","One automatic revival. Consuming it permanently adds 25% damage.","Один автоматический подъём с 55% ХП. Потраченный заряд навсегда даёт +25% урона.",L,"rebirth","Бессмертный договор.","Каждая копия даёт отдельный заряд."),
            D("Time Master","Повелитель времени","Weapon hits echo after 1 s for 35% damage, without triggering more echoes.","Попадания оружия повторяются через 1 с на 35% урона; эхо не запускает новое эхо.",M,"timemaster","Раскол души, Временной долг.","+35% урона эха за копию."),
            D("Reality Cube","Куб реальности","Every 12 s alternates an assault phase with a healing sanctuary phase.","Каждые 12 с меняет закон мира: штурм даёт двойной залп; убежище блокирует автоатаки, лечит 3% ХП/с и заряжает взрыв из лечения.",L,"cube","Сердце инверсии, Ходячий собор.","+1 залп в штурме; +3% лечения/с за копию."),
            D("Copper Spring","Медная пружина","+15% attack speed.","+15% скорости автоатак.",U,"coil"),
            D("Spyglass","Подзорная труба","+3 m weapon range.","+3 м дальности оружия.",U,"lens"),
            D("Chalk Circle","Меловой круг","+18% effect area.","+18% радиуса эффектов.",U,"ring"),
            D("Wind Fletching","Оперение ветра","+25% projectile speed.","+25% скорости снарядов.",U,"arrow"),
            D("Scholar Seal","Печать учёного","+18% experience gain.","+18% получаемого опыта.",U,"book"),
            D("Feather Anklet","Браслет пера","+8% evasion.","+8% уклонения.",U,"wingboots"),
            D("Iron Brooch","Железная брошь","+8% armor.","+8% брони.",U,"armor"),
            D("Healer's Ribbon","Лента лекаря","+10% healing and +0.4 HP/s.","+10% силы лечения и +0,4 ХП/с.",U,"potion"),
            D("Dew Bead","Бусина росы","+12 max HP and +0.3 HP/s.","+12 максимальных ХП и +0,3 ХП/с.",U,"leaf"),
            D("Nomad Pin","Знак кочевника","+6% speed and +1 m pickup range.","+6% скорости бега и +1 м подбора.",U,"boots"),
            D("Duelist Knot","Узел дуэлянта","+5% critical chance and +6% attack speed.","+5% шанса крита и +6% скорости атаки.",U,"gloves"),
            D("Cartographer Seal","Печать картографа","+8 luck and +1.5 m range.","+8 удачи и +1,5 м дальности оружия.",U,"compass"),
            D("Lead Pendulum","Свинцовый маятник","+8% damage and +12% projectile speed.","+8% урона и +12% скорости снарядов.",U,"arrow"),
            D("Warden's Badge","Знак дозорного","+10 max HP and 20 shield.","+10 максимальных ХП и 20 щита.",U,"shield"),
            D("Apprentice Cord","Шнур ученика","+6% recharge and +8% healing.","+6% скорости восстановления способностей и +8% лечения.",U,"hourglass"),
            D("Blood Glass","Кровавое стекло","+2% lifesteal and +12% critical damage.","+2% вампиризма и +12% критического урона.",U,"fang"),
            D("Comet Dust","Кометная пыль","+10% XP and +1.2 m pickup range.","+10% опыта и +1,2 м подбора.",U,"stars"),
            D("Merchant Scale","Весы торговца","+12% gold and +7 luck.","+12% золота и +7 удачи.",U,"gold"),
            D("Silver Thread","Серебряная нить","+4% armor and +4% evasion.","+4% брони и +4% уклонения.",U,"armor"),
            D("Steady Hand","Твёрдая рука","+2 m range and +3% critical chance.","+2 м дальности оружия и +3% шанса крита.",U,"target"),
            D("Glass Cannon","Стеклянная пушка","+55% damage, lose 25% maximum HP.","+55% урона, но максимум ХП уменьшается на 25%.",R,"blade"),
            D("Lead Boots","Свинцовые сапоги","+22% armor, -18% movement speed.","+22% брони, но -18% скорости бега.",R,"armor"),
            D("Blood Engine","Кровяной двигатель","+60% attack speed; weapon volleys cost 2 HP, nonlethal.","+60% скорости атаки; каждый залп оружия стоит 2 ХП, не убивает.",R,"heart","Клык и лечение компенсируют цену."),
            D("Sniper Oath","Клятва снайпера","+75% damage beyond 12 m; -40% within 5 m.","+75% урона дальше 12 м; -40% ближе 5 м.",R,"target","Подзорная труба, дальнобойные залпы."),
            D("Brawler Oath","Клятва драчуна","+60% damage within 4 m; -50% beyond 10 m.","+60% урона в 4 м; -50% дальше 10 м.",R,"axe","Броня и атаки по площади."),
            D("Thorn Mail","Шипастый панцирь","Taking a hit reflects 80% of its damage within 4 m. 2 s cooldown.","Полученный удар отражает 80% его урона по врагам в 4 м. КД 2 с.",R,"armor","Усиление защиты и ответные атаки."),
            D("Frost Bell","Морозный колокол","Every 8 s chills enemies within 7 m for 4 s.","Каждые 8 с охлаждает врагов в 7 м на 4 с.",R,"bell","Ледяная тюрьма, Паровой клапан."),
            D("Venom Needle","Ядовитая игла","Hits poison for 4 damage/s for 5 s, up to 8 layers.","Удары накладывают яд: 4 урона/с на 5 с, до 8 слоёв.",R,"fang","Септическая рана, Чумной доктор.","+4 урона/с за копию."),
            D("Serrated Coin","Зазубренная монета","Critical hits cause 8 damage/s bleeding for 4 s, up to 5 layers.","Криты вызывают кровотечение: 8 урона/с на 4 с, до 5 слоёв.",R,"gold","Кровавый компас, Септическая рана."),
            D("Thunder Rod","Громовой стержень","Every 6 s strikes the farthest enemy within 20 m for 45 and shocks it.","Каждые 6 с бьёт самого дальнего врага в 20 м на 45 урона и заряжает его.",R,"lightning","Проводник грозы, Грозовой контур."),
            D("Dash Flint","Кремень рывка","Dashing leaves a burning patch for 3 s. 2 s cooldown.","Рывок оставляет огненное пятно на 3 с. КД 2 с.",R,"dash","Пепельный сад, Двигатель пацифиста."),
            D("Landing Stone","Камень приземления","Landing deals 30 damage within 4 m. 2 s cooldown.","Приземление наносит 30 урона в 4 м. КД 2 с.",R,"jump","Ангельские крылья."),
            D("Retaliation Seal","Печать возмездия","Taking damage grants 18 shield and a 28-damage pulse. 8 s cooldown.","Получение урона даёт 18 щита и волну на 28 урона в 5 м. КД 8 с.",R,"shield","Конденсатор, Сердце инверсии."),
            D("Soul Jar","Сосуд душ","A kill heals 6 HP. 3 s cooldown.","Убийство лечит 6 ХП. КД 3 с.",R,"potion","Кровавая луна, Паразитическая корона."),
            D("Powder Keg","Пороховая бочка","A kill explodes for 35 damage within 3 m. 2 s cooldown.","Убийство взрывает цель: 35 урона в 3 м. КД 2 с.",R,"fireorb","Стягивание целей воронками."),
            D("Hunter's Debt","Долг охотника","+45% damage, -40% gold gain.","+45% урона, но -40% получаемого золота.",R,"gold"),
            D("Silent Vow","Обет молчания","+70% weapon damage; Q/E/R also cost 8 HP, nonlethal.","+70% урона оружия; Q/E/R дополнительно стоят 8 ХП, не убивают.",R,"book","Оружие и эхо; осторожнее с заклинаниями."),
            D("Loaded Dice","Краплёные кости","+45 luck, -30% XP gain.","+45 удачи, но -30% получаемого опыта.",R,"clover"),
            D("Heavy Arrow","Тяжёлая стрела","Arrows pierce 1 extra enemy; -35% projectile speed.","Стрелы пробивают ещё 1 врага, но скорость снарядов -35%.",R,"arrow","Дабл-шот, Лента Мёбиуса.","+1 пробитие за копию."),
            D("Ricochet Seed","Семя рикошета","Arrows bounce to another enemy on impact.","Стрелы после попадания рикошетят в другую цель на 70% урона.",R,"leaf","Яд, горение и многозарядность.","+1 рикошет за копию."),
            D("Steam Valve","Паровой клапан","Burn plus chill consumes both for a 60-damage steam explosion. 2 s per target.","Горение и холод расходуются на паровой взрыв: 60 урона в 3 м. КД на цели 2 с.",E,"fireorb","Клинок пламени + Ледяное ядро."),
            D("Storm Conductor","Проводник грозы","Hitting a shocked enemy chains 40% damage to 2 other targets. 1 s cooldown.","Удар по заряженному врагу передаёт 40% урона ещё двум целям. КД 1 с.",E,"coil","Грозовой амулет, Громовой стержень."),
            D("Septic Wound","Септическая рана","Poisoned bleeding enemies take +12% damage per combined layer, up to +120%.","Враги с ядом и кровотечением получают +12% урона за суммарный слой, до +120%.",E,"fang","Ядовитая игла + Зазубренная монета."),
            D("Shatter Lens","Линза раскола","Critical hits on frozen enemies shatter for 70 area damage. 3 s cooldown.","Крит по замороженной цели раскалывает лёд: 70 урона в 4 м. КД 3 с.",E,"icecore","Хроносфера, Ледяная тюрьма."),
            D("Ash Garden","Пепельный сад","Burning deaths leave 5 s fire patches dealing 12 damage/s.","Горящие враги после смерти оставляют огненный сад на 5 с: 12 урона/с в 3 м.",E,"flameblade","Сбор врагов воронкой."),
            D("Blood Compass","Кровавый компас","Hits on bleeding targets heal 3 HP. 0.5 s cooldown.","Попадания по кровоточащим целям лечат 3 ХП. КД 0,5 с.",E,"compass","Зазубренная монета, Кровавая луна."),
            D("Execution Ledger","Реестр казней","Every fifth hit marks its target; marked enemies below 18% HP are executed. Bosses take 60 damage instead.","Каждый 5-й удар ставит метку. Помеченная цель ниже 18% ХП казнится; боссы вместо казни получают 60 урона.",E,"book","Много снарядов быстро заполняют реестр."),
            D("Echo Chamber","Эхо-камера","Three consecutive hits on the same target detonate for 80 damage within 3 m.","Три последовательных попадания по одной цели вызывают взрыв на 80 урона в 3 м.",E,"resonance","Эхо Повелителя времени не сбивает серию."),
            D("Alternator","Переключатель","Alternating weapon slots builds charges; at 4 charges discharge 90 damage nearby.","Чередование двух слотов оружия копит заряды; 4 заряда выпускают 90 урона в 7 м.",E,"coil","Второе оружие, Живой арсенал."),
            D("Kinetic Spool","Кинетическая катушка","Every 20 m travelled stores a charge: next weapon volley deals +80% damage.","Каждые 20 м пути копит заряд: следующий залп оружия получает +80% урона.",E,"boots","Дальний странник, Двигатель пацифиста."),
            D("Glacial Prison","Ледяная тюрьма","Three hits on a chilled target freeze it for 2 s.","Три попадания по охлаждённой цели замораживают её на 2 с.",E,"frost","Ледяное ядро, Зимний сон."),
            D("Capacitor","Конденсатор","Casting an ability spends up to 20 shield to shock enemies for twice the shield spent.","Применение способности расходует до 20 щита и бьёт врагов в 7 м на двойную потраченную величину, оставляя заряд.",E,"shield","Печать возмездия, Сердце инверсии."),
            D("War Drum","Боевой барабан","Kills give +5% attack speed for 5 s, up to 10 charges.","Убийства дают +5% скорости атаки на 5 с, до 10 зарядов.",E,"bell","Серии убийств продлевают все заряды."),
            D("Hunting Pack","Охотничья стая","Every 5 kills summons a spirit for 8 s that bites every second.","Каждые 5 убийств призывают духа на 8 с; он раз в секунду кусает ближайшую цель на 20 урона.",E,"necronomicon","Могильный договор, Единый рой."),
            D("Mirror Shard","Зеркальный осколок","Critical hits fire a shard at a different enemy for 50% damage. 0.4 s cooldown.","Крит выпускает осколок в другую цель на 50% урона. КД 0,4 с; осколки не порождают осколки.",E,"stars","Высокий шанс крита и несколько целей."),
            D("Orbital Saw","Орбитальная пила","A blade orbits 3 m away and hits intersected enemies every 0.4 s for 22 damage.","Пила вращается в 3 м вокруг героя и задевает врагов на 22 урона каждые 0,4 с.",E,"satellite","Чёрная дыра, ближний бой.","Каждая копия добавляет отдельную пилу."),
            D("Thunderstep","Поступь грома","Dash origin and destination discharge for 45 damage and shock. 3 s cooldown.","Начало и конец рывка бьют на 45 урона в 3 м и заряжают врагов. КД 3 с.",E,"dash","Грозовой контур."),
            D("Sacrificial Brazier","Жертвенная жаровня","Abilities cost 5 HP and burn enemies within 8 m; burning kills heal 10 HP.","Способности стоят 5 ХП и поджигают врагов в 8 м; убийство горящей цели лечит 10 ХП.",E,"flameblade","Огненные сборки, Пепельный сад."),
            D("Miser Fuse","Фитиль скряги","Gold pickups place a mine: arms in 0.8 s, lasts 6 s, explodes for 55. 1 s cooldown.","Подбор золота ставит мину: взвод 0,8 с, жизнь 6 с, взрыв на 55 урона. КД 1 с.",E,"gold","Золото превращает маршрут подбора в ловушку."),
            D("Void Anchor","Якорь пустоты","After standing still for 2 s, every 3 s pulls enemies into a 60-damage pulse.","После 2 с без движения каждые 3 с стягивает врагов в 6 м и наносит 60 урона.",E,"blackhole","Ходячий собор, Правление архитектора."),
            D("Phoenix Ash","Пепел феникса","A lethal hit releases a 180-damage burning nova before downing you. Once per realm.","Смертельный удар выпускает огненную волну на 180 урона в 10 м перед падением. Раз за мир.",E,"rebirth","Перерождение позволяет пережить волну."),
            D("Plague Doctor","Чумной доктор","Poisoned deaths spread half their poison power to enemies within 5 m and heal 4 HP.","Смерть отравленной цели переносит половину силы яда на врагов в 5 м и лечит 4 ХП.",E,"potion","Ядовитая игла, Единый рой."),
            D("Red Thread","Красная нить","Every hit links its target to the previous target for 4 s; 30% damage crosses the link.","Попадание связывает цель с предыдущей на 4 с; 30% урона передаётся по связи.",E,"ring","Приоритет разных целей и Дабл-шот."),
            D("Spell Loom","Ткацкий станок чар","Q, E and R each add a different rune; weave all three to empower the next volley by 200%.","Q, E и R оставляют разные руны. Соберите все три: следующий залп получит +200% урона.",E,"book","Быстрое восстановление способностей."),
            D("Frostfire Covenant","Договор инея и пламени","Hits alternate burn and chill. Every sixth hit bursts for 50 damage.","Удары чередуют горение и холод. Каждый 6-й удар взрывается на 50 урона в 3 м.",E,"frost","Паровой клапан; цепочки статусов."),
            D("Duelist Clock","Часы дуэлянта","Evading a hit refreshes dash and reduces Q/E cooldowns by 2 s.","Уклонение от удара обновляет рывок и сокращает КД Q/E на 2 с.",E,"hourglass","Уклонение и Поступь грома.","Ещё -2 с КД Q/E за копию."),
            D("Overgrowth","Разрастание","Every 8 pickups plants a 5 s root field slowing enemies and dealing 10 damage/s.","Каждые 8 подборов растят корни на 5 с: замедление и 10 урона/с в 4 м.",E,"leaf","Магнит, Семя мира."),
            D("Soul Tithe","Десятина душ","Every 10 kills spends 5 current HP to permanently gain 3 max HP.","Каждые 10 убийств тратят 5 текущих ХП и навсегда дают 3 максимальных ХП.",E,"heart","Лечение поддерживает наращивание здоровья."),
            D("Broken Metronome","Сломанный метроном","Odd volleys deal +60% damage, even volleys grant 12 shield.","Нечётные залпы дают +60% урона, чётные дают 12 щита.",E,"hourglass","Несколько оружий ускоряют смену тактов."),
            D("Star Map","Карта звёзд","Every 8 volleys calls a meteor at the aimed enemy after 0.8 s for 140 damage.","Каждые 8 залпов вызывают метеор в точку цели через 0,8 с: 140 урона в 5 м.",E,"stars","Замедление удерживает врагов под метеором."),
            D("Pacifist Engine","Двигатель пацифиста","Disables weapon autoattacks. Every 8 m travelled drops a mine dealing 120 damage; abilities still work.","Отключает автоатаки оружия. Каждые 8 м движения оставляет мину на 120 урона; способности работают.",M,"boots","Скорость, Кинетическая катушка, Дальний странник."),
            D("Grave Pact","Могильный договор","Kills collect souls. Every 6 souls forms a 12 s revenant that hunts independently.","Убийства собирают души. Из 6 душ возникает призрак на 12 с: преследует врагов и бьёт на 32 урона/с.",M,"necronomicon","Охотничья стая; призраки наследуют урон.","+1 призрак за срабатывание за копию."),
            D("Temporal Debt","Временной долг","Defers 50% incoming damage for 3 s. Kills repay 8 debt; unpaid debt bypasses shield.","Откладывает 50% входящего урона на 3 с. Убийства гасят 8 долга; остаток бьёт сквозь щит.",M,"chronosphere","Быстрые убийства отменяют отложенную цену.","Доля отсрочки растёт до 85%; +8 погашения за копию."),
            D("Mobius Quiver","Лента Мёбиуса","Arrows return after 0.6 s, can hit targets again and refill 1 HP when caught.","Стрелы через 0,6 с возвращаются к герою, снова поражают цели и лечат на 1 ХП при ловле.",M,"infinity","Пробивание, эффекты попадания.","+1 ХП за пойманную стрелу за копию."),
            D("Parasite Crown","Паразитическая корона","Lifesteal overhealing fills a brood; every 15 excess HP launches a parasite for 50 damage.","Избыточное лечение вампиризмом кормит выводок: 15 лишних ХП выпускают паразита на 50 урона.",M,"crown","Вампиризм, Кровавое стекло.","+50 урона паразита за копию."),
            D("Inversion Heart","Сердце инверсии","Healing becomes shield. At 80 shield, next ability consumes it for an area blast; HP heals only via realm travel.","Лечение превращается в щит. При 80 щита следующая способность расходует его на взрыв в 8 м. ХП восстанавливается переходом мира.",M,"heart","Конденсатор и избыточное лечение.","+50% урона взрыва за копию."),
            D("Event Horizon","Горизонт событий","A moving 5 m field absorbs hostile projectiles; every 4 s releases their stored damage as a nova.","Поле 5 м поглощает вражеские снаряды; каждые 4 с выпускает накопленный урон волной.",M,"blackhole","Ренджевики становятся источником урона.","+2 м поля за копию."),
            D("Glass World","Стеклянный мир","Maximum HP becomes 1. Dash shatters the world for 160 damage, with 1 s protection. No passive invincibility.","Максимум ХП становится 1. Рывок раскалывает мир на 160 урона в 8 м и даёт 1 с защиты.",M,"stars","Уклонение, обновление рывка и щиты.","+160 урона за копию."),
            D("Walking Cathedral","Ходячий собор","Standing 2 s consecrates a 7 m area: heals allies 4 HP/s and damages enemies 18/s; movement cancels it.","После 2 с неподвижности освящает 7 м: лечит союзников на 4 ХП/с, врагам наносит 18/с. Движение прерывает службу.",M,"shrine","Якорь пустоты, Правление архитектора."),
            D("Blood Moon","Кровавая луна","Every volley spends 3% max HP, deals double damage, and kills heal 4% max HP. Cannot pay below 1 HP.","Залп тратит 3% максимальных ХП и наносит двойной урон; убийства лечат 4% максимальных ХП. Цена не опускает ХП ниже 1.",M,"heart","Вампиризм и Двигатель крови.","Ещё +50% урона и +2% лечения за повтор."),
            D("Storm Circuit","Грозовой контур","Shocked enemies form a network: every 2 s each arcs to one other charged target for 35 damage.","Заряженные враги образуют сеть: раз в 2 с каждый соединяется с другой заряженной целью молнией на 35 урона.",M,"coil","Любой источник электрических зарядов."),
            D("Split Soul","Раскол души","Every volley leaves a shadow at your previous position; after 0.7 s it repeats 60% damage toward that target.","Каждый залп оставляет тень на месте героя; через 0,7 с она повторяет 60% урона в прежнюю цель из старой позиции.",M,"timemaster","Подвижные билды и Повелитель времени."),
            D("Winter Sleep","Зимний сон","Frozen enemies store half received damage; thawing releases it within 5 m.","Замороженные враги копят половину полученного урона и при оттаивании выпускают его в 5 м.",M,"icecore","Ледяная тюрьма, Хроносфера."),
            D("Living Arsenal","Живой арсенал","Two weapon slots alternate solo stances every 5 s; the active one gains 2 projectiles and +70% damage.","Два слота оружия сменяют стойку каждые 5 с: стреляет только активный, получая +2 снаряда и +70% урона. Аура не отключается.",M,"blade","Переключатель, Дабл-шот."),
            D("Void Shepherd","Пастырь пустоты","Every 12 m travelled leaves a 4 s vortex behind you; it pulls enemies and bursts for 75 damage.","Каждые 12 м пути оставляет позади воронку на 4 с: притягивает врагов, затем взрывается на 75 урона.",M,"portal","Мины, огненные следы, Дальний странник."),
            D("Alchemy Engine","Алхимический двигатель","Gold pickups rotate fire, frost and venom recipes; next hit applies the selected recipe at triple strength.","Каждый подбор золота меняет рецепт: огонь, лёд, яд. Следующее попадание применяет выбранный рецепт втрое сильнее.",M,"potion","Паровой клапан и Септическая рана."),
            D("Fate Loom","Станок судьбы","Replaces random weapon criticals: every fourth hit is guaranteed critical and marks its victim.","Заменяет случайные криты: каждое 4-е попадание гарантированно критует и помечает жертву.",M,"clover","Реестр казней, Зеркальный осколок.","Повторы сокращают цикл до минимума 2 удара."),
            D("Hive Mind","Единый рой","Poisoned kills hatch a 6 s drone; drones seek different enemies and inject venom once per second.","Убийство отравленного врага выводит трутня на 6 с. Трутни выбирают разные цели и раз в секунду впрыскивают яд.",M,"leaf","Ядовитая игла, Чумной доктор."),
            D("World Seed","Семя мира","Every 25 kills grows the seed: permanent +2 max HP, +2% area and a growing root pulse.","Каждые 25 убийств выращивают семя: навсегда +2 максимальных ХП, +2% радиуса; пускает всё более сильную корневую волну.",L,"leaf","Массовые убийства и поля корней."),
            D("Last Pilgrim","Последний странник","Every 100 m travelled grants permanent +3% damage. Every fifth milestone releases a 200-damage trail nova.","Каждые 100 м пути навсегда дают +3% урона. Каждый 5-й рубеж выпускает дорожную волну на 200 урона.",L,"compass","Двигатель пацифиста, Пастырь пустоты."),
            D("Debt of the King","Долг короля","Each chest your party buys invests one share. Every 30 s each share pays 2 gold and permanently +1% damage to you.","Каждая покупка сундука командой вкладывает долю. Раз в 30 с каждая доля приносит 2 золота и навсегда +1% вашего урона.",L,"gold","Раннее получение и поиск сундуков."),
            D("Chronicle","Летопись неуязвимого","Survive 45 s without taking damage to gain a permanent projectile. Taking a hit resets only the current timer.","45 с без полученного урона дают постоянный дополнительный снаряд. Попадание сбрасывает только текущий отсчёт.",L,"book","Дальний бой, уклонение и контроль."),
            D("Ouroboros","Уроборос","Every 50 kills consumes the cycle: full healing and an expanding doom wave, growing by 40 damage each cycle.","Каждые 50 убийств замыкает цикл: полное лечение и волна рока, растущая на 40 урона с каждым циклом.",L,"infinity","Проклятия и массовые убийства."),
            D("Architect's Reign","Правление архитектора","Every 8 s standing still builds a turret for this realm. Up to 5; kills permanently raise all turret damage.","Каждые 8 с неподвижности строит башню до конца мира, максимум 5. Убийства навсегда увеличивают урон всех ваших башен.",L,"shrine","Якорь пустоты, Ходячий собор."),
            D("Eclipse Crown","Корона затмения","12 s daylight stores 20% damage dealt. 12 s night releases it as pulses and healing; each cycle increases storage by 5%.","12 с дня копят 20% нанесённого урона; 12 с ночи тратят запас на волны и лечение. Каждый цикл повышает долю накопления на 5%.",L,"crown","Стабильный урон днём, осторожное движение ночью."),
            D("Immortal Contract","Бессмертный договор","On lethal damage, sacrifice 20% max HP to revive and permanently gain 40% damage. Once per copy.","При смертельном ударе жертвует 20% максимальных ХП, поднимает и навсегда даёт +40% урона. Раз на копию.",L,"rebirth","Сборки через жертву и восстановление."),
            D("Singularity Testament","Завещание сингулярности","Every 30 afflicted kills grows all vortex radii by 0.5 m and creates an implosion with increasing damage.","Каждые 30 убийств врагов со статусами увеличивают все ваши воронки на 0,5 м и создают всё более сильное схлопывание.",L,"blackhole","Яд, горение и Чёрная дыра."),
            D("Sevenfold Oath","Семикратная клятва","Each boss defeated while owned adds a permanent projectile and 15% damage; entering a new realm sends an oath nova.","Каждый побеждённый с предметом босс даёт постоянный снаряд и +15% урона. Вход в следующий мир выпускает волну клятвы.",L,"dawn","Получение в ранних мирах окупается к финалу."),
            D("Endless Refrain","Бесконечный рефрен","Every 10 abilities replays their accumulated power as 3 delayed waves. Each completed refrain adds another wave, up to 12.","Каждые 10 способностей повторяют накопленную силу тремя отложенными волнами. Каждый завершённый рефрен добавляет волну, до 12.",L,"resonance","Быстрое восстановление и Станок чар.")
        };

        // The screenshot uses i=1..5 in ascending rarity order. Serialized enum values stay stable.
        public static readonly int[] RarityOrder={1,0,2,4,3};
        public static float[] RarityProbabilities(float luck,int chestTier=0,float fortune=1,bool mythic=true)
        {
            double s=1.5*Math.Log(1+Math.Max(-99.9,Math.Min(100000,luck))/100.0),total=0;
            double[] basis={27,56,12,1,4};var weights=new double[5];var result=new float[5];
            for(int rank=0;rank<5;rank++)
            {
                int r=RarityOrder[rank];
                double b=basis[r]*(rank>=2?Math.Max(.01,fortune):1)*(chestTier>0?Math.Pow(1.6,rank):1);
                weights[r]=r==4&&!mythic?0:b*Math.Pow(1.5,-(3-(rank+1))*s);total+=weights[r];
            }
            for(int i=0;i<5;i++)result[i]=(float)(weights[i]/total);return result;
        }
        public static float BurstInterval(int projectileCount)=>projectileCount<2?0:.75f/Mathf.Pow(1.5f,projectileCount-2);
    }
}
