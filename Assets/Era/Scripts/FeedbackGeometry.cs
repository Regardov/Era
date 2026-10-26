using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public static class FeedbackGeometry
    {
        static Mesh shard,cross,orb,shield,saw,flame,flameCluster,flash,wolf,spirit;static Material solid,mist,barrier,fire;static Texture2D soft;
        static Material MaterialCopy(string path,string shader,string name){var source=Resources.Load<Material>(path);return source!=null?new Material(source){name=name}:new Material(Shader.Find(shader)){name=name};}
        public static Material Solid=>solid??(solid=MaterialCopy("Feedback/Particles","Era/Feedback Solid","Shared low-poly particles"));
        public static Material Mist
        {
            get
            {
                if(mist!=null)return mist;var template=Resources.Load<Material>("Feedback/Smoke");if(template!=null)return mist=new Material(template){name="Shared soft smoke"};soft=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft smoke silhouette",wrapMode=TextureWrapMode.Clamp};var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r*r),2));}
                soft.SetPixels(pixels);soft.Apply(false,true);mist=new Material(Shader.Find("Sprites/Default")){name="Shared soft smoke",mainTexture=soft};return mist;
            }
        }
        public static Material Barrier=>barrier??(barrier=MaterialCopy("Feedback/Shield","Era/Protective Field","Shared protective field"));
        public static Material Fire=>fire??(fire=MaterialCopy("Feedback/Flame","Era/Living Flame","Shared living flame"));
        public static Mesh Particle(FeedbackStyle style)=>style==FeedbackStyle.Fire||style==FeedbackStyle.Meteor?Flame:style==FeedbackStyle.Critical||style==FeedbackStyle.Holy||style==FeedbackStyle.Shield||style==FeedbackStyle.Pickup?Flash:style==FeedbackStyle.Heal||style==FeedbackStyle.Mark?Cross:style==FeedbackStyle.Poison||style==FeedbackStyle.Void?Orb:Shard;
        public static Mesh Flame
        {
            get
            {
                if(flame!=null)return flame;const int sides=6;var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
                var heights=new[]{0f,.25f,.65f,1.0f};var radii=new[]{.13f,.28f,.19f,.075f};
                for(int r=0;r<4;r++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*radii[r]+heights[r]*heights[r]*.12f,heights[r],Mathf.Sin(a)*radii[r]));colors.Add(Color.Lerp(new Color(1,.33f,.025f),new Color(.90f,.075f,.008f),r/3f));}
                v.Add(new Vector3(.23f,1.35f,0));colors.Add(new Color(.9f,.06f,.008f,.2f));v.Add(Vector3.zero);colors.Add(new Color(1,.52f,.045f));
                for(int i=0;i<sides;i++){int n=(i+1)%sides;t.AddRange(new[]{25,i,n});for(int r=0;r<3;r++){int a=r*sides+i,b=r*sides+n;t.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}t.AddRange(new[]{18+i,24,18+n});}
                flame=Make("Curved flame tongue",v.ToArray(),t.ToArray());flame.SetColors(colors);return flame;
            }
        }
        public static Mesh FlameCluster
        {
            get
            {
                if(flameCluster!=null)return flameCluster;var parts=new CombineInstance[3];
                for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;parts[i]=new CombineInstance{mesh=Flame,transform=Matrix4x4.TRS(new Vector3(Mathf.Cos(a)*.28f,0,Mathf.Sin(a)*.28f),Quaternion.Euler(0,i*120,0),new Vector3(.8f,.65f+i*.17f,.8f))};}
                flameCluster=new Mesh{name="Three living flame tongues"};flameCluster.CombineMeshes(parts);return flameCluster;
            }
        }
        public static Mesh Flash
        {
            get
            {
                if(flash!=null)return flash;var v=new List<Vector3>{Vector3.zero};var t=new List<int>();
                for(int i=0;i<16;i++){float a=i*Mathf.PI/8,r=i%4==0?.65f:i%2==0?.34f:.075f;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
                for(int i=0;i<16;i++)t.AddRange(new[]{0,1+i,1+(i+1)%16});return flash=Make("Short impact glint",v.ToArray(),t.ToArray());
            }
        }
        public static Mesh SpectralWolf=>wolf??(wolf=SummonMesh(true));
        public static Mesh SpectralSpirit=>spirit??(spirit=SummonMesh(false));
        static Mesh SummonMesh(bool animal)
        {
            var parts=new List<CombineInstance>();
            void Part(Mesh mesh,Vector3 p,Vector3 scale,Vector3 rotation=default)=>parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,Quaternion.Euler(rotation),scale)});
            if(animal)
            {
                Part(Orb,new Vector3(0,.65f,-.06f),new Vector3(.52f,.55f,1.05f));Part(Orb,new Vector3(0,.86f,.48f),new Vector3(.39f,.42f,.42f));Part(Orb,new Vector3(0,.79f,.72f),new Vector3(.27f,.23f,.40f));
                for(int side=-1;side<=1;side+=2){Part(Shard,new Vector3(side*.14f,1.10f,.48f),new Vector3(.20f,.40f,.17f),new Vector3(-12,0,-side*12));for(int leg=0;leg<2;leg++)Part(Shard,new Vector3(side*.20f,.30f,leg==0?.30f:-.36f),new Vector3(.17f,.60f,.19f),new Vector3(leg==0?-17:20,0,side*9));}
                Part(Shard,new Vector3(0,.73f,-.78f),new Vector3(.24f,.75f,.22f),new Vector3(64,0,0));
            }
            else
            {
                Part(Orb,new Vector3(0,.80f,0),new Vector3(.52f,.80f,.40f));Part(Orb,new Vector3(0,1.35f,0),Vector3.one*.38f);Part(Shard,new Vector3(0,.25f,0),new Vector3(.65f,.95f,.5f));
                for(int side=-1;side<=1;side+=2)Part(Shard,new Vector3(side*.35f,.75f,.02f),new Vector3(.20f,.74f,.20f),new Vector3(15,0,side*24));
            }
            var result=new Mesh{name=animal?"Batched spectral wolf":"Batched revenant silhouette"};result.CombineMeshes(parts.ToArray());return result;
        }
        public static Mesh Shard
        {
            get
            {
                if(shard!=null)return shard;var v=new[]{Vector3.up*.7f,Vector3.down*.45f,Vector3.right*.32f,Vector3.forward*.32f,Vector3.left*.32f,Vector3.back*.32f};var t=new[]{0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};return shard=Make("Ice / spark / flame shard",v,t);
            }
        }
        public static Mesh Orb=>orb??(orb=Sphere("Smooth low-poly mote",8,5));
        public static Mesh Shield=>shield??(shield=Sphere("Closed protective envelope",20,12));
        public static Mesh Cross
        {
            get
            {
                if(cross!=null)return cross;var vertices=new List<Vector3>();var triangles=new List<int>();
                Box(vertices,triangles,new Vector3(.17f,.65f,.10f));Box(vertices,triangles,new Vector3(.58f,.17f,.10f));cross=Make("Restoration cross",vertices.ToArray(),triangles.ToArray());return cross;
            }
        }
        public static Mesh Saw
        {
            get
            {
                if(saw!=null)return saw;var vertices=new List<Vector3>();var t=new List<int>();const int sides=32;
                for(int layer=0;layer<2;layer++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,r=i%2==0?.56f:.39f;vertices.Add(new Vector3(Mathf.Cos(a)*r,layer==0?-.045f:.045f,Mathf.Sin(a)*r));}
                vertices.Add(Vector3.down*.045f);vertices.Add(Vector3.up*.045f);
                for(int i=0;i<sides;i++){int n=(i+1)%sides;t.AddRange(new[]{64,i,n,65,n+sides,i+sides,i,i+sides,n,n,i+sides,n+sides});}
                return saw=Make("Closed orbital saw",vertices.ToArray(),t.ToArray());
            }
        }
        static void Box(List<Vector3> vertices,List<int> t,Vector3 size)
        {
            int at=vertices.Count;for(int i=0;i<8;i++)vertices.Add(Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),size));
            foreach(int i in new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5})t.Add(at+i);
        }
        static Mesh Sphere(string name,int sides,int rings)
        {
            var v=new List<Vector3>();var t=new List<int>();v.Add(Vector3.down*.5f);
            for(int r=1;r<rings;r++)for(int s=0;s<sides;s++){float y=-Mathf.PI*.5f+r*Mathf.PI/rings,a=s*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*Mathf.Cos(y),Mathf.Sin(y),Mathf.Sin(a)*Mathf.Cos(y))*.5f);}
            int top=v.Count;v.Add(Vector3.up*.5f);
            for(int s=0;s<sides;s++){int n=(s+1)%sides;t.AddRange(new[]{0,1+s,1+n});for(int r=0;r<rings-2;r++){int a=1+r*sides+s,b=1+r*sides+n;t.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}int last=1+(rings-2)*sides;t.AddRange(new[]{last+s,top,last+n});}
            return Make(name,v.ToArray(),t.ToArray());
        }
        static Mesh Make(string name,Vector3[] v,int[] t){var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        public static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go;}
        public static void Dispose(){foreach(var o in new Object[]{shard,cross,orb,shield,saw,flame,flameCluster,flash,wolf,spirit,solid,mist,barrier,fire,soft})if(o!=null)Object.Destroy(o);shard=cross=orb=shield=saw=flame=flameCluster=flash=wolf=spirit=null;solid=mist=barrier=fire=null;soft=null;}
    }
}
