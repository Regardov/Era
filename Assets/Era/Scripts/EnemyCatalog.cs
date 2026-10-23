using UnityEngine;

namespace Era
{
    public static class EnemyCatalog
    {
        public static bool Flying(EnemyKind kind)=>kind==EnemyKind.Bat||kind==EnemyKind.Gargoyle;
        public static bool Ranged(EnemyKind kind)=>kind==EnemyKind.SkeletonArcher||kind==EnemyKind.AcidSlime||kind==EnemyKind.CrystalGolem||kind==EnemyKind.OgreShaman;
        public static bool Summoner(EnemyKind kind)=>kind==EnemyKind.Necromancer||kind==EnemyKind.OgreShaman;
        public static float Radius(EnemyKind kind)=>kind==EnemyKind.Warden?1.5f:kind==EnemyKind.StoneGolem||kind==EnemyKind.Ogre||kind==EnemyKind.Brute?1.05f:kind==EnemyKind.CrystalGolem||kind==EnemyKind.OgreShaman?.85f:kind==EnemyKind.Bat?.5f:.65f;
        public static float Reach(EnemyKind kind)=>kind==EnemyKind.Warden?2.8f:kind==EnemyKind.Ogre||kind==EnemyKind.Brute?3.2f:kind==EnemyKind.StoneGolem?2.8f:kind==EnemyKind.Revenant?2.3f:1.5f;
        public static float Health(EnemyKind kind)
        {
            switch(kind){case EnemyKind.Skeleton:return 42;case EnemyKind.SkeletonArcher:return 35;case EnemyKind.Revenant:return 125;case EnemyKind.Necromancer:return 80;case EnemyKind.StoneGolem:return 260;case EnemyKind.CrystalGolem:return 150;case EnemyKind.Slime:return 65;case EnemyKind.AcidSlime:return 75;case EnemyKind.Ogre:return 230;case EnemyKind.OgreShaman:return 135;case EnemyKind.Bat:return 24;case EnemyKind.Gargoyle:return 100;case EnemyKind.Brute:return 110;case EnemyKind.Stalker:return 32;default:return 44;}
        }
        public static float Speed(EnemyKind kind)
        {
            switch(kind){case EnemyKind.StoneGolem:return 1.25f;case EnemyKind.CrystalGolem:return 1.6f;case EnemyKind.Ogre:return 1.65f;case EnemyKind.OgreShaman:return 1.8f;case EnemyKind.Bat:return 4.6f;case EnemyKind.Gargoyle:return 3.3f;case EnemyKind.Stalker:return 4.1f;case EnemyKind.Brute:return 1.55f;case EnemyKind.Revenant:return 2.1f;case EnemyKind.Slime:return 2.2f;default:return 2.65f;}
        }
        public static string Name(EnemyKind kind)=>I18n.P(new[]{"Jester","Brute","Stalker","Realm Warden","Skeleton","Skeleton archer","Revenant","Necromancer","Stone golem","Crystal golem","Splitting slime","Acid slime","Ogre crusher","Ogre shaman","Blood bat","Gargoyle"}[(int)kind],new[]{"Шут","Громила","Преследователь","Хранитель мира","Скелет","Скелет-лучник","Ревенант","Некромант","Каменный голем","Кристальный голем","Делящийся слайм","Кислотный слайм","Огр-крушитель","Огр-шаман","Кровавая летучая мышь","Горгулья"}[(int)kind]);
    }
}
