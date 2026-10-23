using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public enum GameLanguage { English, Russian }
    public static class I18n
    {
        public static GameLanguage Language=GameLanguage.English;
        public static bool Russian=>Language==GameLanguage.Russian;
        public static string P(string english,string russian)=>Russian?russian:english;
        public static string T(string english)=>Russian&&english!=null&&Ru.TryGetValue(english,out var value)?value:english;
        public static void Load()=>Language=(GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt("era.language",Application.systemLanguage==SystemLanguage.Russian?1:0),0,1);
        public static string[] Keys=>new List<string>(Ru.Keys).ToArray();
        static readonly Dictionary<string,string> Ru=new Dictionary<string,string>
        {
            {"Aura Conduit","Проводник аур"},{"Empowers nearby allies and the bearer. Choose an aura every level.","Усиливает ближайших союзников и носителя. Каждый уровень — выбор ауры."},
            {"10 base damage per second and buffs within 9.2 m. Choose or upgrade one aura per level.","10 базового урона в секунду и баффы в радиусе 9.2 м. Каждый уровень — новая аура или улучшение."},
            {"E R A","Э Р А"},{"S T I L L W A T E R   R E A C H","Т И Х И Й   П Р Е Д Е Л"},
            {"A little light.\nA very big adventure.","Немного света.\nБольшое приключение."},
            {"Wander a forgotten world. Gather a little courage. Face the rift together.","Исследуйте забытый мир. Найдите в себе смелость. Встретьте разлом вместе."},
            {"Play solo","Играть одному"},{"Play with a companion","Играть вдвоём"},{"Settings","Настройки"},{"Quit","Выйти"},{"Back","Назад"},
            {"A 3D survivor for one or two wanderers","3D-выживание для одного или двух игроков"},
            {"A WORLD WORTH GETTING LOST IN","МИР, В КОТОРОМ ХОЧЕТСЯ ЗАДЕРЖАТЬСЯ"},
            {"Explore the groves. Find treasure.\nClose the rift before it takes you.","Исследуйте рощи. Находите сокровища.\nЗакройте разлом, пока не поздно."},
            {"CHOOSE YOUR WANDERER","ВЫБЕРИТЕ СВОЕГО ГЕРОЯ"},{"Ember","Искра"},{"Echo","Эхо"},{"EMBER","ИСКРА"},{"ECHO","ЭХО"},
            {"A spark of courage.","Искра отваги."},{"A quiet kind of magic.","Магия тихого света."},
            {"EMBER  /  FIRE WEAVER  /  100 HP","ИСКРА  /  МАГИЯ ОГНЯ  /  100 ОЗ"},{"ECHO  /  FROST KEEPER  /  115 HP","ЭХО  /  ХРАНИТЕЛЬ ЛЬДА  /  115 ОЗ"},
            {"Fast, fearless and just a little reckless. Turn a tiny spark into a storm.","Быстрая, бесстрашная и немного безрассудная. Превратите крошечную искру в бурю."},
            {"Keep your companion close. A little frost and kindness go a long way.","Держитесь рядом с напарником. Немного льда и заботы творят чудеса."},
            {"TAKE ONE RELIC  /  PRESS R TO USE IT","ВЫБЕРИТЕ АРТЕФАКТ  /  АКТИВАЦИЯ НА R"},
            {"Confirm loadout","Подтвердить выбор"},{"Begin the journey","Начать приключение"},{"Enter the lobby","Войти в лобби"},
            {"FOLLOW THE WARMTH","СЛЕДУЙТЕ ЗА ТЕПЛОМ"},{"LEAVE A LITTLE LIGHT","ОСТАВЬТЕ НЕМНОГО СВЕТА"},
            {"Sunburst clears the way.\nOverdrive keeps you moving.","Солнечный разлом расчищает путь.\nПерегрузка ускоряет атаки."},
            {"Frost Bloom holds enemies still.\nResonance heals you both.","Ледяной цветок сковывает врагов.\nРезонанс лечит вас обоих."},
            {"BETTER TOGETHER","ВМЕСТЕ ЛУЧШЕ"},{"Two wanderers. One shared adventure.","Два странника. Одно приключение."},
            {"Host a new lobby","Создать лобби"},{"Or join your companion","Или присоединитесь к напарнику"},{"HOST IP ADDRESS","IP ХОСТА"},
            {"Choose a character & join","Выбрать героя и подключиться"},
            {"Connect over LAN or a shared VPN.\nThe journey starts when both players are ready.","Играйте в одной локальной или VPN-сети.\nЗабег начнётся после готовности обоих."},
            {"A COMPANION AWAITS","ВАС ЖДЁТ НАПАРНИК"},{"Finding your way...","Ищем дорогу..."},{"Better together.","Вместе лучше."},
            {"Let your companion know you made it.","Сообщите напарнику, что вы на месте."},{"A little room for a friend","Место для друга"},
            {"Waiting for a companion...","Ожидание напарника..."},{"READY","ГОТОВ"},{"NOT READY","НЕ ГОТОВ"},
            {"Both players must press Ready to begin.","Для запуска оба игрока должны нажать «Готов»."},{"Not ready yet","Отменить готовность"},{"I'm ready","Я готов"},
            {"Change character","Сменить героя"},{"Leave lobby","Покинуть лобби"},{"SHARED GOLD. SHARED EXPERIENCE.","ОБЩЕЕ ЗОЛОТО. ОБЩИЙ ОПЫТ."},
            {"Your build is your own.\nYour companion always has your back.","У каждого — свой набор улучшений.\nНапарник всегда прикроет спину."},
            {"Sunburst","Солнечный разлом"},{"Frost Bloom","Ледяной цветок"},{"Overdrive","Перегрузка"},{"Resonance","Резонанс"},
            {"Storm Compass","Компас бури"},{"Hourglass","Песочные часы"},{"Home Shard","Осколок дома"},{"Dash","Рывок"},
            {"Deal 90 damage within 9 m. 24 s cooldown.","90 урона в радиусе 9 м. Перезарядка 24 с."},
            {"Freeze enemies within 12 m for 4 s. Refresh dash. 30 s cooldown.","Заморозка на 4 с в радиусе 12 м и сброс рывка. Перезарядка 30 с."},
            {"Restore 50 health and gather all experience. 25 s cooldown.","Восстановление 50 здоровья и сбор всего опыта. Перезарядка 25 с."},
            {"65 damage within 7 m. 8 s cooldown.","65 урона в радиусе 7 м. Перезарядка 8 с."},
            {"35 damage and a 3 s freeze within 8 m. 10 s cooldown.","35 урона и заморозка на 3 с в радиусе 8 м. Перезарядка 10 с."},
            {"Double fire rate and +30% movement for 6 s. 18 s cooldown.","Удвоенный темп огня и +30% скорости на 6 с. Перезарядка 18 с."},
            {"Restore 35 health and give 25 shield to allies within 12 m. 16 s cooldown.","35 здоровья и 25 щита союзникам в радиусе 12 м. Перезарядка 16 с."},
            {"Cinder Heart","Сердце углей"},{"Dawn Shard","Осколок зари"},{"Copper Coil","Медная катушка"},{"Wind Bell","Колокол ветра"},
            {"Moon Lantern","Лунный фонарь"},{"Quartz Lens","Кварцевая линза"},{"Living Amber","Живой янтарь"},{"Satellite Seed","Семя спутника"},
            {"+25 maximum health. Immediately restore 35 health.","+25 к максимальному здоровью. Сразу восстанавливает 35 здоровья."},
            {"+20% weapon and ability damage.","+20% к урону оружия и способностей."},{"+15% automatic attack speed.","+15% к скорости автоматических атак."},
            {"+8% movement speed. Cooldowns recover 10% faster.","+8% к скорости бега. Способности заряжаются на 10% быстрее."},
            {"+2 m pickup radius. Gather all experience on the map now.","+2 м к радиусу подбора. Сразу собирает весь опыт на карте."},
            {"+1 bolt per volley and +2 m range. Up to 5 bolts.","+1 стрела в залпе и +2 м к дальности. До 5 стрел."},
            {"Heal for 2% of damage dealt. Stacks with each copy.","Лечение на 2% нанесённого урона. Эффект складывается."},
            {"Every 0.7 s, deal 12 damage per stack to enemies within 3.5 m.","Каждые 0,7 с наносит 12 урона за копию врагам в радиусе 3,5 м."},
            {"Each slash hits one enemy. Extra projectiles repeat the slash with a delay.","Каждый взмах поражает одного врага. Дополнительные удары повторяются с задержкой."},
            {"Crescent Axe","Лунный топор"},{"Lightning Staff","Посох молний"},{"Wildwood Bow","Лесной лук"},{"Dawn Spear","Копьё зари"},
            {"Dawn Sword","Меч зари"},{"Sweeping sword cuts. Extra projectiles repeat the slash with a delay.","Размашистые удары мечом. Дополнительные снаряды повторяют взмах с задержкой."},
            {"Each heavy swing hits one enemy. Extra strikes follow in sequence.","Каждый тяжёлый взмах поражает одного врага. Дополнительные удары идут очередью."},
            {"Each beam hits one enemy. Extra beams fire simultaneously; Ricochet Seed adds chain hits.","Один луч — одна цель. Все дополнительные лучи бьют одновременно; Семя рикошета добавляет переходы."},
            {"Fast arrows seek distant targets. Extra bolts add arrows.","Быстрые стрелы поражают дальние цели. Дополнительные снаряды добавляют стрелы."},
            {"Each thrust hits one enemy. Extra thrusts follow in a short sequence.","Каждый выпад поражает одного врага. Дополнительные выпады идут короткой очередью."},
            {"Companion disconnected","Напарник отключился"},{"Hold F to revive your companion","Удерживайте F, чтобы поднять напарника"},
            {"THE STILLWATER WARDEN","СТРАЖ ТИХОГО ПРЕДЕЛА"},{"THE WARDEN","СТРАЖ"},{"Chest","Сундук"},{"Moon chest","Лунный сундук"},
            {"A LITTLE STRONGER","ВЫ СТАЛИ СИЛЬНЕЕ"},{"Choose an upgrade","Выберите улучшение"},
            {"The world is paused until you choose.","Игра на паузе, пока вы не сделаете выбор."},{"The fight continues. Choose when ready.","Бой продолжается. Выбирайте на ходу."},
            {"Little things, great power.","Маленькие вещи — большая сила."},{"WEAPONS","ОРУЖИЕ"},{"MEMORIES","УЛУЧШЕНИЯ"},
            {"Empty slot","Пустой слот"},{"Find weapons in chests","Ищите оружие в сундуках"},{"Take a breath.","Переведите дух."},
            {"Make yourself at home.","Устройтесь поудобнее."},{"Your companion's world keeps moving.","У напарника игра продолжается."},
            {"Your solo adventure is paused.","Одиночный забег приостановлен."},{"A little music. A little magic.","Немного музыки. Немного магии."},
            {"Music","Музыка"},{"Sound effects","Звуковые эффекты"},{"Mouse sensitivity","Чувствительность мыши"},{"Language","Язык"},
            {"Keep wandering","Продолжить"},{"Save & return","Сохранить и вернуться"},{"Return to main menu","В главное меню"},
            {"A little light wins.","Свет снова победил."},{"Every ending is a beginning.","Каждый конец — новое начало."},
            {"The Warden has fallen. Stillwater can breathe again.","Страж повержен. Тихий предел снова свободен."},
            {"Rest a moment. There is always another way through.","Отдохните немного. Новый путь обязательно найдётся."},
            {"TIME","ВРЕМЯ"},{"KILLS","УБИЙСТВА"},{"GOLD FOUND","ЗОЛОТО"},{"CHESTS","СУНДУКИ"},{"Return to the sanctuary","Вернуться в убежище"},
            {"Keep a little hope.","Не теряйте надежду."},{"You need a helping hand.","Вам нужна помощь."},
            {"Your companion can still close the rift.","Напарник ещё может закрыть разлом."},
            {"THE WAKING SANCTUARY","ПРОБУЖДАЮЩЕЕСЯ УБЕЖИЩЕ"},{"LAVENDER GROVE","ЛАВАНДОВАЯ РОЩА"},{"AMBERWILDS","ЯНТАРНЫЕ ЗЕМЛИ"},{"THE FALLEN GARDENS","ЗАБЫТЫЕ САДЫ"},{"MOSS LIGHT MEADOWS","СВЕТЛЫЕ ЛУГА"},
            {"Share your local IP with your companion.","Передайте напарнику свой локальный IP."},{"Port 7777 is in use. Close the other lobby and try again.","Порт 7777 занят. Закройте другое лобби и попробуйте снова."},
            {"Enter the host computer's IP address.","Введите IP компьютера хоста."},{"Finding your companion...","Ищем напарника..."},{"Waiting for another companion.","Ожидаем нового напарника."},
            {"Both companions must press Ready.","Оба игрока должны нажать «Готов»."},{"Game versions do not match.","Версии игры не совпадают."},
            {"The run has already begun. Join the next lobby.","Забег уже начался. Подключитесь в следующем лобби."},
            {"Your companion disconnected. You can finish this run alone.","Напарник отключился. Вы можете закончить забег в одиночку."},
            {"The host is not responding. Connection closed.","Хост не отвечает. Соединение закрыто."},
            {"Invalid game data. Use the same game version on both computers.","Ошибка данных игры. Используйте одинаковые версии на обоих компьютерах."},
            {"Host unavailable. Check the IP, TCP port 7777 and your firewall.","Хост недоступен. Проверьте IP, порт TCP 7777 и брандмауэр."},
            {"Connection to your companion was lost.","Соединение с напарником потеряно."},{"Could not accept the connection.","Не удалось принять подключение."}
        };
    }
}
