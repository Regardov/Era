using UnityEngine;
namespace Era
{
    // Cached relief chart; live markers are drawn separately without another world camera.
    public static class RealmCartography
    {
        public const int Resolution=384;
        public static float Extent=>GameData.ArenaRadius+14;
        public static Vector2 UV(Vector3 p)=>new Vector2(.5f+p.x/(Extent*2),.5f+p.z/(Extent*2));
        public static Texture2D Create()
        {
            int n=Resolution;float step=Extent*2/(n-1);var heights=new float[n*n];var pixels=new Color32[n*n];
            Color realm=CampaignData.GrassColors[WorldMap.Stage-1],low=Color.Lerp(new Color(.19f,.28f,.23f),realm,.17f),high=Color.Lerp(new Color(.40f,.47f,.32f),realm,.18f),water=new Color(.25f,.56f,.59f);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)heights[y*n+x]=WorldMap.Height(x*step-Extent,y*step-Extent);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float wx=x*step-Extent,wz=y*step-Extent,radius=Mathf.Sqrt(wx*wx+wz*wz),h=heights[y*n+x];
                float slope=(heights[y*n+Mathf.Min(n-1,x+1)]-heights[y*n+Mathf.Max(0,x-1)])*.17f;
                Color c=Color.Lerp(low,high,Mathf.InverseLerp(-4,10,h))*Mathf.Clamp(1-slope,.75f,1.2f);
                float contour=Mathf.Abs(Mathf.Repeat(h,2)-1);
                c=Color.Lerp(c,new Color(.55f,.60f,.40f),Mathf.SmoothStep(0,.22f,contour-.82f)*.28f);
                if(Mathf.Abs(wz-Mathf.Sin(wx*.026f)*8)<2)c=Color.Lerp(c,new Color(.67f,.59f,.40f),.50f);
                for(int river=0;river<2;river++)
                {float distance=Mathf.Abs(wx-WorldMap.RiverCenter(wz,river))-WorldMap.RiverWidth(wz,river);if(distance<step*.8f)c=Color.Lerp(water*.85f,water,Mathf.Clamp01(distance+1));}
                if(radius>GameData.ArenaRadius-3)c=Color.Lerp(c,new Color(.65f,.66f,.51f),Mathf.Clamp01(1-Mathf.Abs(radius-GameData.ArenaRadius)/3));
                if(radius>GameData.ArenaRadius+3)c=new Color(.09f,.145f,.16f);
                pixels[y*n+x]=c;
            }
            foreach(var prop in WorldMap.Props)
            {
                Vector2 uv=UV(prop.position);int px=Mathf.RoundToInt(uv.x*(n-1)),py=Mathf.RoundToInt(uv.y*(n-1)),radius=prop.kind==0?Mathf.Max(1,Mathf.RoundToInt(prop.size*1.15f)):1;
                Color tint=prop.kind==0?Color.Lerp(low,new Color(.10f,.22f,.19f),.7f):prop.kind==1?new Color(.47f,.48f,.40f):low;
                for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                {int x=px+dx,y=py+dy;if(x<0||y<0||x>=n||y>=n||dx*dx+dy*dy>radius*radius)continue;pixels[y*n+x]=tint*(dy>0?1.1f:.85f);}
            }
            foreach(var building in WorldMap.Buildings)
            {
                var uv=UV(building.position);int px=Mathf.RoundToInt(uv.x*(n-1)),py=Mathf.RoundToInt(uv.y*(n-1));
                for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){int x=px+dx,y=py+dy;if(x>=0&&y>=0&&x<n&&y<n)pixels[y*n+x]=building.ruined?new Color(.58f,.55f,.43f):new Color(.66f,.35f,.20f);}
            }
            var texture=new Texture2D(n,n,TextureFormat.RGB24,false){name="Realm "+WorldMap.Stage+" / relief chart",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
    }
}
