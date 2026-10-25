using UnityEngine;
namespace Era
{
    public sealed partial class GameHud
    {
        Texture2D mapTexture;int mapStage=-1;
        void MapLine(Vector2 a,Vector2 b,Color color,float width=1.5f)
        {
            int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/width);
            for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(a,b,i/(float)Mathf.Max(1,steps));Dot(p.x-width*.5f,p.y-width*.5f,width,color);}
        }
        void Minimap(WorldState s,float x,float y,float size,bool local=false)
        {
            if(mapTexture==null||mapStage!=s.stage){if(mapTexture!=null)Destroy(mapTexture);mapTexture=RealmCartography.Create();mapStage=s.stage;}
            var player=App.LocalPlayer;float padding=local?12:20;var area=new Rect(x+padding,y+padding,size-padding*2,size-padding*2);
            float radius=local?82:RealmCartography.Extent;Vector3 center=local&&player!=null?player.position:Vector3.zero;
            if(local){float limit=RealmCartography.Extent-radius;center.x=Mathf.Clamp(center.x,-limit,limit);center.z=Mathf.Clamp(center.z,-limit,limit);}
            Vector2 uv=RealmCartography.UV(center);float span=radius/(RealmCartography.Extent*2);var crop=new Rect(uv.x-span,uv.y-span,span*2,span*2);
            Box(x,y,size,size,new Color(.065f,.11f,.13f,1));
            if(capturing)capture.Rect(area,WithAlpha(Color.white),mapTexture,crop);
            else{var old=GUI.color;GUI.color=WithAlpha(Color.white);GUI.DrawTextureWithTexCoords(area,mapTexture,crop);GUI.color=old;}
            for(int line=1;line<4;line++)
            {Flat(area.x+line*area.width/4,area.y,1,area.height,new Color(.75f,.83f,.72f,.10f));Flat(area.x,area.y+line*area.height/4,area.width,1,new Color(.75f,.83f,.72f,.10f));}
            Vector2 Point(Vector3 p)=>new Vector2(area.center.x+(p.x-center.x)*area.width/(radius*2),area.center.y-(p.z-center.z)*area.height/(radius*2));
            bool Visible(Vector2 p)=>p.x>area.x+7&&p.x<area.xMax-7&&p.y>area.y+7&&p.y<area.yMax-7;
            Vector2 Bound(Vector2 p)=>new Vector2(Mathf.Clamp(p.x,area.x+2,area.xMax-2),Mathf.Clamp(p.y,area.y+2,area.yMax-2));
            void Marker(Vector3 position,float width,Color color,int kind)
            {
                Vector2 p=Point(position);if(!Visible(p))return;
                if(kind==0){Flat(p.x-width/2-1,p.y-width/2-1,width+2,width+2,Panel);Flat(p.x-width/2,p.y-width/2,width,width,color);Flat(p.x-width/2+1,p.y-width/2+1,width-2,width-2,Panel);}
                else{Dot(p.x-width/2-1,p.y-width/2-1,width+2,Panel);Dot(p.x-width/2,p.y-width/2,width,color);}
            }
            foreach(var landmark in WorldMap.Landmarks)Marker(landmark,local?4:5,new Color(.76f,.80f,.68f),1);
            foreach(var chest in s.chests)if(!chest.opened)Marker(chest.position,local?5:4,chest.tier>0?Lilac:Sage,0);
            foreach(var shrine in s.shrines)if(!shrine.spent)Marker(shrine.position,local?7:6,Blue,1);
            if(!s.bossSpawned&&!s.portal.active)Marker(s.bossStatuePosition,9,Red,0);
            if(s.portal.active){Marker(s.portal.position,12,Lilac,1);Marker(s.portal.position,5,Panel,1);}
            foreach(var e in s.enemies)Marker(e.position,e.kind==EnemyKind.Warden?9:3,Red,e.kind==EnemyKind.Warden?0:1);
            foreach(var ally in s.players)if(!ally.eliminated)
            {
                Vector2 p=Point(ally.position);if(!Visible(p))continue;bool own=ally.id==App.LocalId;
                Color color=ally.downed?Red:own?GameData.Mint:Lilac;float yaw=(own?App.CameraYaw:ally.yaw)*Mathf.Deg2Rad;
                var forward=new Vector2(Mathf.Sin(yaw),-Mathf.Cos(yaw));var side=new Vector2(-forward.y,forward.x);
                if(own){MapLine(p,Bound(p+(forward*.86f+side*.5f)*25),new Color(color.r,color.g,color.b,.35f),1);MapLine(p,Bound(p+(forward*.86f-side*.5f)*25),new Color(color.r,color.g,color.b,.35f),1);}
                Dot(p.x-6,p.y-6,12,Panel);Dot(p.x-3.5f,p.y-3.5f,7,color);
                MapLine(Bound(p+forward*10),Bound(p+forward*4+side*3),color,2);MapLine(Bound(p+forward*10),Bound(p+forward*4-side*3),color,2);
            }
            Text(I18n.P("N","С"),x+size/2-12,y+1,24,padding-1,local?10:12,Sage,true,TextAnchor.UpperCenter);
            Text(I18n.P("S","Ю"),x+size/2-12,y+size-padding,24,padding,local?10:12,Muted,true,TextAnchor.LowerCenter);
            Text(I18n.P("W","З"),x+1,y+size/2-9,padding-2,18,local?9:12,Muted,true,TextAnchor.MiddleCenter);
            Text(I18n.P("E","В"),x+size-padding+1,y+size/2-9,padding-2,18,local?9:12,Muted,true,TextAnchor.MiddleCenter);
            Flat(area.x+7,area.yMax-12,area.width*25/(radius*2),2,Ink);Text("25 "+I18n.P("m","м"),area.x+7,area.yMax-29,50,15,9,Ink);
            if(local)Text(I18n.P("TAB · REALM MAP","TAB · КАРТА МИРА"),x,y+size+6,size,19,10,Muted,true,TextAnchor.UpperCenter);
        }
        void MapLegend(float x,float y)
        {
            Color[] colors={GameData.Mint,Sage,Blue,Red};string[] labels={I18n.P("You","Вы"),I18n.P("Chest","Сундук"),I18n.P("Shrine","Алтарь"),I18n.P("Boss","Босс")};
            for(int i=0;i<4;i++){Dot(x+i*90,y+4,5,colors[i]);Text(labels[i],x+i*90+10,y,78,18,10,Muted);}
        }
    }
}
