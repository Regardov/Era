using UnityEngine;
namespace Era
{
    public static class MusicCatalog
    {
        public const int Count=8;
        public static readonly string[] Files={"Menu-Homeward-Light","Realm01-Mistveil","Realm02-Amberwood","Realm03-Azure","Realm04-Cinder","Realm05-Twilight","Realm06-Stormpass","Realm07-Rift"};
        static readonly string[] TitlesEn={"Homeward Light","Dew of Mistveil","Dance of Amber Boughs","Glass Summits","Pulse of the Cinder Forge","Lanterns in the Mire","March through the Storm","Heart of the Rift"};
        static readonly string[] TitlesRu={"Свет у порога","Роса туманных лугов","Танец янтарных ветвей","Стеклянные вершины","Пульс пепельной кузни","Фонари сумеречной топи","Шествие сквозь грозу","Сердце разлома"};
        public static string Path(int index)=>"Audio/"+Files[Mathf.Clamp(index,0,Count-1)];
        public static string Title(int index)=>I18n.P(TitlesEn[Mathf.Clamp(index,0,Count-1)],TitlesRu[Mathf.Clamp(index,0,Count-1)]);
        public static int Select(bool inRun,int stage)=>inRun?Mathf.Clamp(stage,1,CampaignData.StageCount):0;
    }
}
