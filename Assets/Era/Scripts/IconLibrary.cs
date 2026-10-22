using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public static class IconLibrary
    {
        static readonly Dictionary<string,Texture2D> Cache=new Dictionary<string,Texture2D>();
        public static Texture2D ItemAtlas=>Resources.Load<Texture2D>("UI/item-atlas");
        static readonly float[][] ItemX={new float[]{208,339,470,601,732,863,994,1125,1256,1387},new float[]{251,412,588,761,935,1108,1282},new float[]{250,421,598,771,945,1124,1302},new float[]{249,422,602,780,958,1137,1315}};
        public static Rect ItemPixels(int item)
        {
            int row=item<10?0:item<17?1:item<24?2:3,col=item-(row==0?0:row==1?10:row==2?17:24);
            float y=row==0?20:row==1?252:row==2?516:761,w=row==0?113:row==1?134:row==2?148:159,h=row==0?98:row==1?102:row==2?109:110;
            return new Rect(ItemX[row][col],1024-y-h,w,h);
        }
        public static Rect ItemUV(int item){var rect=ItemPixels(item);return new Rect(rect.x/1536,rect.y/1024,rect.width/1536,rect.height/1024);}
        static readonly Sprite[] ItemSprites=new Sprite[31];
        public static Sprite ItemSprite(int item)=>ItemSprites[item]!=null?ItemSprites[item]:(ItemSprites[item]=Sprite.Create(ItemAtlas,ItemPixels(item),Vector2.one*.5f,140,0,SpriteMeshType.FullRect));
        public static Texture2D Get(string name){if(!Cache.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>("Icons/"+name);Cache[name]=texture;}return texture;}
    }
}
