using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    // Chunked meshes keep a complete meadow cheap: no individual grass GameObjects.
    public static class SceneryBuilder
    {
        sealed class Draft
        {
            public readonly List<Vector3> v=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<Color> colors=new List<Color>();
            readonly List<int> triangles=new List<int>();
            public int Add(Vector3 point,Vector2 tex,Color color){int at=v.Count;v.Add(point);uv.Add(tex);colors.Add(color.linear);return at;}
            public void Tri(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            public void Blade(Vector3 p,float height,float width,float angle,Color color,float lean)
            {
                var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width;
                var bend=new Vector3(-side.z,0,side.x).normalized*lean;
                int a=Add(p-side,Vector2.zero,color),b=Add(p+side,Vector2.right,color);
                int c=Add(p+Vector3.up*height*.58f+bend*.3f-side*.55f,new Vector2(0,.58f),color),d=Add(p+Vector3.up*height*.58f+bend*.3f+side*.55f,new Vector2(1,.58f),color);
                int e=Add(p+Vector3.up*height+bend,new Vector2(.5f,1),color);
                Tri(a,c,b);Tri(b,c,d);Tri(c,e,d);
            }
            public void Tuft(Vector3 p,float height,float width,float angle,Color color)
            {
                Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width;
                int a=Add(p-side,Vector2.zero,color),b=Add(p+side,Vector2.right,color);
                int c=Add(p-side+Vector3.up*height,Vector2.up,color),d=Add(p+side+Vector3.up*height,Vector2.one,color);
                Tri(a,c,b);Tri(b,c,d);
            }
            public void Tube(Vector3 from,Vector3 to,float radius,float endRadius,Color color,int sides=8)
            {
                Vector3 direction=(to-from).normalized,side=Vector3.Cross(direction,Vector3.forward).normalized;
                if(side.sqrMagnitude<.1f)side=Vector3.right;Vector3 other=Vector3.Cross(direction,side);int first=v.Count;
                for(int r=0;r<2;r++)for(int s=0;s<=sides;s++)
                {float a=s*Mathf.PI*2/sides;Add((r==0?from:to)+(side*Mathf.Cos(a)+other*Mathf.Sin(a))*(r==0?radius:endRadius),new Vector2(s/(float)sides,r*(to-from).magnitude*.6f),color);}
                for(int s=0;s<sides;s++){int a=first+s,b=first+s+1,c=first+s+sides+1,d=c+1;Tri(a,c,b);Tri(b,c,d);}
            }
            public void Boulder(Vector3 center,Vector3 scale,Color color,int seed,bool leaves=false)
            {
                const int sides=12,rings=7;int start=v.Count;
                for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
                {
                    float y=-Mathf.PI*.5f+r*Mathf.PI/rings,a=s*Mathf.PI*2/sides;
                    float shape=1+Mathf.Sin(a*3+seed*.6f)*.1f+Mathf.Cos(y*4+seed)*.07f;
                    var local=new Vector3(Mathf.Cos(a)*Mathf.Cos(y),Mathf.Sin(y),Mathf.Sin(a)*Mathf.Cos(y));
                    float shade=Mathf.Lerp(.73f,1.16f,(local.y+1)*.5f);
                    Add(center+Vector3.Scale(local,scale)*shape,new Vector2(s/(float)sides,r/(float)rings),color*shade);
                }
                for(int r=0;r<rings;r++)for(int s=0;s<sides;s++){int a=start+r*(sides+1)+s,b=a+1,c=a+sides+1,d=c+1;Tri(a,c,b);Tri(b,c,d);}
            }
            public GameObject Save(string name,Transform parent,Material material,Vector3 offset=default,bool shadows=true)
            {
                if(v.Count==0)return null;
                var mesh=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=offset;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
                return go;
            }
        }
        sealed class Cell {public readonly Draft bark=new Draft(),leaves=new Draft(),rocks=new Draft();}
        public static void Build(Transform root,int stage)
        {
            var ground=new GameObject("01 / Sculpted hills").transform;ground.SetParent(root,false);
            var props=new GameObject("02 / Bark, foliage and weathered rocks").transform;props.SetParent(root,false);
            var grass=new GameObject("03 / Meadow carpet").transform;grass.SetParent(root,false);
            Color grassColor=CampaignData.GrassColors[stage-1],leafColor=CampaignData.LeafColors[stage-1];
            var terrainMat=new Material(Shader.Find("Era/Realm Terrain")){name="Terrain / "+stage};terrainMat.SetTexture("_MainTex",Resources.Load<Texture2D>("Textures/MeadowDetail"));terrainMat.SetTexture("_StoneTex",Resources.Load<Texture2D>("Textures/StoneDetail"));terrainMat.SetColor("_Color",grassColor.linear*1.2f);
            var barkMat=Surface("Bark",1,Resources.Load<Texture2D>("Textures/WoodDetail"),Resources.Load<Texture2D>("Textures/WoodNormal"));
            var rockMat=Surface("Stone",0,Resources.Load<Texture2D>("Textures/StoneDetail"),Resources.Load<Texture2D>("Textures/StoneNormal"));rockMat.SetColor("_MossColor",grassColor);
            var leafMat=new Material(Shader.Find("Era/Realm Leaves")){name="Canopy / "+stage};leafMat.mainTexture=Resources.Load<Texture2D>("Textures/LeafDetail");
            var grassMat=new Material(Shader.Find("Era/Meadow Grass")){name="Living grass / "+stage};grassMat.mainTexture=Resources.Load<Texture2D>("Textures/GrassClump");
            const int side=20;const float width=40;int chunks=Mathf.CeilToInt((GameData.ArenaRadius+12)*2/width);float extent=chunks*width*.5f;
            for(int cx=0;cx<chunks;cx++)for(int cz=0;cz<chunks;cz++)
            {
                float nearX=Mathf.Max(0,Mathf.Abs(-extent+(cx+.5f)*width)-width*.5f),nearZ=Mathf.Max(0,Mathf.Abs(-extent+(cz+.5f)*width)-width*.5f);
                if(nearX*nearX+nearZ*nearZ>(GameData.ArenaRadius+12)*(GameData.ArenaRadius+12))continue;
                var d=new Draft();
                for(int z=0;z<=side;z++)for(int x=0;x<=side;x++)
                {float wx=-extent+cx*width+x*width/side,wz=-extent+cz*width+z*width/side;d.Add(WorldMap.OnGround(wx,wz),new Vector2(wx*.2f,wz*.2f),Color.white);}
                for(int z=0;z<side;z++)for(int x=0;x<side;x++){int a=z*(side+1)+x,b=a+1,c=a+side+1;d.Tri(a,c,b);d.Tri(b,c,c+1);}
                d.Save("Hills / "+cx+" / "+cz,ground,terrainMat);
            }
            var cells=new Dictionary<Vector2Int,Cell>();
            foreach(var prop in WorldMap.Props)
            {
                var key=new Vector2Int(Mathf.FloorToInt(prop.position.x/40),Mathf.FloorToInt(prop.position.z/40));if(!cells.TryGetValue(key,out var cell)){cell=new Cell();cells[key]=cell;}
                int seed=Mathf.RoundToInt(prop.angle*13+prop.position.x*7);var random=new System.Random(seed);
                if(prop.kind==0)Tree(cell,prop,random,stage,leafColor);
                else
                {
                    Color stone=Color.Lerp(new Color(.43f,.45f,.43f),CampaignData.Color(stage),.10f);
                    cell.rocks.Boulder(prop.position+Vector3.up*.55f*prop.size,new Vector3(1.05f,.88f,.91f)*prop.size,stone,seed);
                    cell.rocks.Boulder(prop.position+new Vector3(.82f,.22f,-.35f)*prop.size,new Vector3(.55f,.4f,.53f)*prop.size,stone*.9f,seed+19);
                }
            }
            foreach(var pair in cells)
            {var c=pair.Value;string suffix=pair.Key.ToString();c.bark.Save("Textured trunks "+suffix,props,barkMat);c.leaves.Save("Living canopies "+suffix,props,leafMat);c.rocks.Save("Moss and fractured stone "+suffix,props,rockMat);}
            var patches=new List<Renderer>();var rng=new System.Random(7521+stage*913);
            for(int cx=0;cx<chunks;cx++)for(int cz=0;cz<chunks;cz++)
            {
                var blades=new Draft();var origin=new Vector3(-extent+cx*width,0,-extent+cz*width);
                for(int x=0;x<40;x++)for(int z=0;z<40;z++)
                {
                    float wx=origin.x+x+(float)rng.NextDouble(),wz=origin.z+z+(float)rng.NextDouble();
                    if(wx*wx+wz*wz>GameData.ArenaRadius*GameData.ArenaRadius)continue;
                    bool path=Mathf.Abs(wz-Mathf.Sin(wx*.026f)*8)<1.4f;
                    if(path&&rng.NextDouble()<.94)continue;
                    var point=WorldMap.OnGround(wx,wz)-origin;float height=.36f+(float)rng.NextDouble()*.24f;
                    Color tint=Color.Lerp(grassColor*.77f,grassColor*1.32f,(float)rng.NextDouble());float angle=(float)rng.NextDouble()*Mathf.PI*2;
                    for(int card=0;card<2;card++)blades.Tuft(point,height,.31f,angle+card*1.6f,tint);
                }
                var patch=blades.Save("Reactive meadow / "+cx+" / "+cz,grass,grassMat,origin,false);if(patch!=null)patches.Add(patch.GetComponent<Renderer>());
            }
            var living=grass.gameObject.AddComponent<LivingGrass>();living.patches=patches.ToArray();
        }
        static Material Surface(string name,int kind,Texture2D texture,Texture2D normal)
        {var mat=new Material(Shader.Find("Era/Realm Surface")){name=name,mainTexture=texture};mat.SetFloat("_Kind",kind);mat.SetTexture("_BumpMap",normal);return mat;}
        static void Tree(Cell cell,WorldProp prop,System.Random random,int stage,Color leaf)
        {
            Vector3 p=prop.position;float scale=prop.size,height=(4.5f+(float)random.NextDouble()*2)*scale;float lean=((float)random.NextDouble()-.5f)*.6f;
            Color bark=new Color(.50f,.39f,.28f);Vector3 middle=p+new Vector3(lean,height*.52f,0),top=p+new Vector3(lean*.6f,height,0);
            cell.bark.Tube(p,middle,.31f*scale,.22f*scale,bark);cell.bark.Tube(middle,top,.22f*scale,.07f*scale,bark);
            for(int r=0;r<4;r++){float a=r*Mathf.PI*.5f+prop.angle;cell.bark.Tube(p+new Vector3(Mathf.Cos(a),.015f,Mathf.Sin(a))*scale,p+Vector3.up*.8f*scale,.10f*scale,.18f*scale,bark);}
            bool pine=stage==3||stage==6||random.NextDouble()<.22;
            if(pine)
            {
                for(int band=0;band<4;band++)
                {
                    float h=height*(.38f+band*.17f),radius=(1.6f-band*.3f)*scale;Vector3 center=p+new Vector3(lean*.5f,h,0);
                    cell.leaves.Boulder(center,new Vector3(radius,height*.19f,radius),leaf*(1+band*.035f),prop.kind+band*39,true);
                }
                return;
            }
            for(int b=0;b<6;b++)
            {
                float a=b*Mathf.PI/3+prop.angle*Mathf.Deg2Rad,reach=(1.1f+(float)random.NextDouble()*.65f)*scale;
                var end=p+new Vector3(Mathf.Cos(a)*reach,height*(.64f+(b%3)*.09f),Mathf.Sin(a)*reach);
                cell.bark.Tube(middle,end,.14f*scale,.035f*scale,bark);
                cell.leaves.Boulder(end+Vector3.up*.42f*scale,new Vector3(1.25f,.93f,1.16f)*scale,leaf,41+b*71,true);
            }
            cell.leaves.Boulder(top,new Vector3(1.25f,1.1f,1.2f)*scale,leaf*1.06f,719,true);
        }
    }
}
