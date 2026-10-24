using UnityEngine;

namespace Era
{
    public static class CampaignData
    {
        public const int StageCount=7;
        public const float StageDuration=600;
        public const float SpawnMinDistance=12, SpawnMaxDistance=19;
        public static float Remaining(float elapsed)=>Mathf.Max(0,StageDuration-elapsed);
        static readonly string[] NamesEn={"Mistveil Meadows","Amberwood","Azure Highlands","Cinder Crags","Twilight Mire","Stormpass Citadel","Heart of the Rift"};
        static readonly string[] NamesRu={"Туманные луга","Янтарный лес","Лазурные нагорья","Пепельные скалы","Сумеречная топь","Грозовой перевал","Сердце разлома"};
        static readonly string[] BossesEn={"Grove Warden","Antlered Executioner","Glacial Oracle","Forge Colossus","Mire Matriarch","Storm Paladin","Rift Sovereign"};
        static readonly string[] BossesRu={"Страж рощи","Рогатый палач","Ледяной оракул","Кузнечный колосс","Королева топи","Рыцарь грозы","Владыка разлома"};
        public static readonly Color[] Colors={new Color(.38f,.69f,.40f),new Color(.96f,.59f,.19f),new Color(.40f,.75f,.94f),new Color(.92f,.30f,.13f),new Color(.68f,.37f,.77f),new Color(.41f,.54f,1),new Color(.77f,.44f,.95f)};
        public static readonly Color[] GrassColors={new Color(.34f,.53f,.22f),new Color(.57f,.47f,.20f),new Color(.33f,.54f,.48f),new Color(.36f,.35f,.22f),new Color(.28f,.39f,.33f),new Color(.35f,.43f,.43f),new Color(.35f,.30f,.51f)};
        public static readonly Color[] LeafColors={new Color(.24f,.46f,.23f),new Color(.72f,.40f,.10f),new Color(.27f,.48f,.43f),new Color(.37f,.26f,.20f),new Color(.34f,.29f,.44f),new Color(.22f,.34f,.39f),new Color(.38f,.25f,.56f)};
        static int Index(int stage)=>Mathf.Clamp(stage-1,0,StageCount-1);
        public static string Name(int stage)=>I18n.P(NamesEn[Index(stage)],NamesRu[Index(stage)]);
        public static string BossName(int stage)=>I18n.P(BossesEn[Index(stage)],BossesRu[Index(stage)]);
        public static Color Color(int stage)=>Colors[Index(stage)];
        public static float EnemyScale(int stage)=>Mathf.Pow(1.5f,Mathf.Max(0,stage-1));
        public static float BossHealth(int stage)=>stage<=1?10000:25000*Mathf.Pow(2,Mathf.Clamp(stage-2,0,StageCount-2));
    }
}
