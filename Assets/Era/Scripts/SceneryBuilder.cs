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
                Branch(new[]{from,to},new[]{radius,endRadius},color,sides);
            }
            public void Branch(Vector3[] points,float[] radii,Color color,int sides=8)
            {
                int first=v.Count;float length=0;Vector3 side=Vector3.zero;
                for(int r=0;r<points.Length;r++)
                {
                    var tangent=(points[Mathf.Min(r+1,points.Length-1)]-points[Mathf.Max(0,r-1)]).normalized;
                    side=Vector3.ProjectOnPlane(side,tangent).normalized;
                    if(side.sqrMagnitude<.1f){side=Vector3.Cross(tangent,Vector3.forward).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;}
                    Vector3 other=Vector3.Cross(tangent,side);if(r>0)length+=Vector3.Distance(points[r-1],points[r]);
                    for(int s=0;s<=sides;s++){float a=(s%sides)*Mathf.PI*2/sides;Add(points[r]+(side*Mathf.Cos(a)+other*Mathf.Sin(a))*radii[r],new Vector2(s/(float)sides,length*.6f),color);}
                }
                // Outward winding and shared joint rings: no open or inverted tube segments.
                for(int r=0;r<points.Length-1;r++)for(int s=0;s<sides;s++){int a=first+r*(sides+1)+s,b=a+1,c=a+sides+1;Tri(a,b,c);Tri(b,c+1,c);}
                int bottom=Add(points[0],Vector2.zero,color),top=Add(points[points.Length-1],Vector2.one,color);
                for(int s=0;s<sides;s++){Tri(bottom,first+s+1,first+s);int a=first+(points.Length-1)*(sides+1)+s;Tri(top,a,a+1);}
            }
            public void Crown(Vector3 center,Vector3 scale,Color color,int seed)
            {
                const int sides=12,rings=6;int bottom=Add(center-Vector3.up*scale.y,new Vector2(-1,0),color*.78f),first=v.Count;
                for(int r=1;r<rings;r++)for(int s=0;s<=sides;s++)
                {
                    float y=-Mathf.PI*.5f+r*Mathf.PI/rings,a=(s%sides)*Mathf.PI*2/sides;
                    float wobble=1+Mathf.Sin(a*3+seed*.71f)*.10f+Mathf.Sin(a*5+y*3+seed)*.055f;
                    var local=new Vector3(Mathf.Cos(a)*Mathf.Cos(y)*wobble,Mathf.Sin(y),Mathf.Sin(a)*Mathf.Cos(y)*wobble);
                    Add(center+Vector3.Scale(local,scale),new Vector2(-1,r/(float)rings),color*Mathf.Lerp(.78f,1.1f,(local.y+1)*.5f));
                }
                for(int s=0;s<sides;s++)Tri(bottom,first+s,first+s+1);
                for(int r=0;r<rings-2;r++)for(int s=0;s<sides;s++){int a=first+r*(sides+1)+s,c=a+sides+1;Tri(a,c,a+1);Tri(a+1,c,c+1);}
                int top=Add(center+Vector3.up*scale.y,new Vector2(-1,1),color*1.1f);
                for(int s=0;s<sides;s++){int a=first+(rings-2)*(sides+1)+s;Tri(a,top,a+1);}
            }
            public void Leaf(Vector3 root,Vector3 direction,Vector3 side,float length,float width,Color color)
            {
                direction.Normalize();side.Normalize();var fold=Vector3.Cross(direction,side)*width*.22f;
                int a=Add(root,new Vector2(.5f,0),color*.76f);
                int b=Add(root+direction*length*.38f-side*width,new Vector2(0,.38f),color);
                int c=Add(root+direction*length*.38f+fold,new Vector2(.5f,.38f),color*1.06f);
                int d=Add(root+direction*length*.38f+side*width,new Vector2(1,.38f),color);
                int e=Add(root+direction*length*.75f-side*width*.57f,new Vector2(.21f,.75f),color*1.07f);
                int f=Add(root+direction*length*.75f+side*width*.57f,new Vector2(.79f,.75f),color*1.07f);
                int g=Add(root+direction*length+fold*.3f,new Vector2(.5f,1),color*1.12f);
                Tri(a,b,c);Tri(a,c,d);Tri(b,e,c);Tri(c,f,d);Tri(c,e,g);Tri(c,g,f);
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
            var terrainMat=new Material(Shader.Find("Era/Realm Terrain")){name="Terrain / "+stage};terrainMat.SetTexture("_MainTex",Resources.Load<Texture2D>("Textures/MeadowDetail"));terrainMat.SetTexture("_StoneTex",Resources.Load<Texture2D>("Textures/StoneDetail"));terrainMat.SetColor("_Color",grassColor.linear*1.45f);terrainMat.SetFloat("_RealmStage",stage);
            var barkMat=Surface("Bark",1,Resources.Load<Texture2D>("Textures/WoodDetail"),Resources.Load<Texture2D>("Textures/WoodNormal"));
            var rockMat=Surface("Stone",0,Resources.Load<Texture2D>("Textures/StoneDetail"),Resources.Load<Texture2D>("Textures/StoneNormal"));rockMat.SetColor("_MossColor",grassColor);
            var leafMat=new Material(Shader.Find("Era/Realm Leaves")){name="Canopy / "+stage};leafMat.mainTexture=Resources.Load<Texture2D>("Textures/LeafDetail");
            var grassMat=new Material(Shader.Find("Era/Meadow Grass")){name="Living grass blades / "+stage};
            BuildStreams(root,stage);
            const int side=40;const float width=40;int chunks=Mathf.CeilToInt((GameData.ArenaRadius+12)*2/width);float extent=chunks*width*.5f;
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
                var key=new Vector2Int(Mathf.FloorToInt(prop.position.x/60),Mathf.FloorToInt(prop.position.z/60));if(!cells.TryGetValue(key,out var cell)){cell=new Cell();cells[key]=cell;}
                int seed=Mathf.RoundToInt(prop.angle*13+prop.position.x*7);var random=new System.Random(seed);
                if(prop.kind==0)Tree(cell,prop,random,stage,leafColor);
                else if(prop.kind==2)Bush(cell,prop,leafColor,seed);
                else
                {
                    Color stone=Color.Lerp(new Color(.43f,.45f,.43f),CampaignData.Color(stage),.10f);
                    cell.rocks.Boulder(prop.position+Vector3.up*.55f*prop.size,new Vector3(1.05f,.88f,.91f)*prop.size,stone,seed);
                    cell.rocks.Boulder(prop.position+new Vector3(.82f,.22f,-.35f)*prop.size,new Vector3(.55f,.4f,.53f)*prop.size,stone*.9f,seed+19);
                }
            }
            for(int river=0;river<2;river++)for(int z=-275;z<275;z+=9)
            {
                float bank=(z%2==0?-1:1)*(WorldMap.RiverWidth(z,river)+.75f),x=WorldMap.RiverCenter(z,river)+bank;
                var point=WorldMap.OnGround(x,z);var key=new Vector2Int(Mathf.FloorToInt(x/60),Mathf.FloorToInt(z/60));
                if(!cells.TryGetValue(key,out var cell)){cell=new Cell();cells[key]=cell;}
                cell.rocks.Boulder(point+Vector3.up*.14f,new Vector3(.48f,.26f,.37f),new Color(.39f,.44f,.37f),z+river*913);
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
                    if(WorldMap.InWater(wx,wz,.25f))continue;
                    bool path=Mathf.Abs(wz-Mathf.Sin(wx*.026f)*8)<1.4f;
                    if(path&&rng.NextDouble()<.94)continue;
                    var point=WorldMap.OnGround(wx,wz)-origin;
                    float meadow=Mathf.PerlinNoise(wx*.065f+stage*11,wz*.065f+9);
                    float height=.24f+(float)rng.NextDouble()*.22f+meadow*.15f;
                    bool reeds=WorldMap.InWater(wx,wz,1.7f);if(reeds)height*=2;
                    Color tint=Color.Lerp(grassColor*.82f,grassColor*1.10f,meadow+(float)rng.NextDouble()*.15f);
                    float angle=(float)rng.NextDouble()*Mathf.PI*2;
                    for(int blade=0;blade<3;blade++)
                    {
                        float a=angle+blade*2.39996f;var offset=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.13f;
                        blades.Blade(point+offset,height*(.7f+(float)rng.NextDouble()*.5f),.03f+(float)rng.NextDouble()*.03f,a,tint,.12f+(float)rng.NextDouble()*.15f);
                    }
                }
                var patch=blades.Save("Reactive meadow / "+cx+" / "+cz,grass,grassMat,origin,false);if(patch!=null)patches.Add(patch.GetComponent<Renderer>());
            }
            var living=grass.gameObject.AddComponent<LivingGrass>();living.patches=patches.ToArray();
        }
        static Material Surface(string name,int kind,Texture2D texture,Texture2D normal)
        {var mat=new Material(Shader.Find("Era/Realm Surface")){name=name,mainTexture=texture};mat.SetFloat("_Kind",kind);mat.SetTexture("_BumpMap",normal);return mat;}
        public static GameObject TreePreview(Transform parent,bool conifer)
        {
            var root=new GameObject(conifer?"Closed conifer preview":"Closed broadleaf preview");root.transform.SetParent(parent,false);var cell=new Cell();
            var prop=new WorldProp{position=WorldMap.OnGround(0,0),size=1,angle=42};Tree(cell,prop,new System.Random(4817),conifer?3:1,CampaignData.LeafColors[conifer?2:0],conifer);
            var bark=Surface("Preview bark",1,Resources.Load<Texture2D>("Textures/WoodDetail"),Resources.Load<Texture2D>("Textures/WoodNormal"));var leaf=new Material(Shader.Find("Era/Realm Leaves"));leaf.mainTexture=Resources.Load<Texture2D>("Textures/LeafDetail");
            cell.bark.Save("Closed trunk",root.transform,bark);cell.leaves.Save("Dense crown",root.transform,leaf);return root;
        }
        static void Bush(Cell cell,WorldProp prop,Color leaf,int seed)
        {
            float s=prop.size;var random=new System.Random(seed);
            for(int branch=0;branch<7;branch++)
            {
                float a=branch*2.39996f+prop.angle*Mathf.Deg2Rad;var tip=prop.position+new Vector3(Mathf.Cos(a)*.48f,.5f+branch*.08f,Mathf.Sin(a)*.48f)*s;
                cell.bark.Tube(prop.position,tip,.035f*s,.012f*s,new Color(.37f,.29f,.18f),5);
                Spray(cell.leaves,tip,new Vector3(.48f,.32f,.48f)*s,leaf,random,22,false);
            }
        }
        static float Range(System.Random random,float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        static void Spray(Draft draft,Vector3 center,Vector3 spread,Color tint,System.Random random,int count,bool needles)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f+Range(random,-.5f,.5f),y=Range(random,-.7f,1),r=Mathf.Sqrt(1-y*y);
                var direction=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                var root=center+Vector3.Scale(direction,spread)*Range(random,.25f,1);
                var forward=(direction+Vector3.up*Range(random,.15f,.7f)).normalized;
                var side=Quaternion.AngleAxis(Range(random,-70,70),forward)*Vector3.Cross(forward,Vector3.up).normalized;
                float size=Range(random,.30f,.55f)*(spread.x+spread.z);
                draft.Leaf(root,forward,side,size, size*(needles?.11f:.32f),tint*Range(random,.78f,1.18f));
            }
        }
        static void BuildStreams(Transform root,int stage)
        {
            var streams=new GameObject("04 / Flowing streams").transform;streams.SetParent(root,false);
            var material=new Material(Shader.Find("Era/Stream Water")){name="Clear flowing water / "+stage};
            material.SetColor("_Color",stage>=5?new Color(.10f,.22f,.35f):new Color(.07f,.32f,.34f));
            material.SetColor("_ShallowColor",stage>=5?new Color(.35f,.56f,.68f):new Color(.38f,.73f,.66f));
            material.SetFloat("_Clarity",.42f);
            for(int river=0;river<2;river++)for(int chunk=-8;chunk<8;chunk++)
            {
                var draft=new Draft();
                for(int z=0;z<=40;z++)for(int x=0;x<=8;x++)
                {
                    float wz=chunk*40+z,wx=WorldMap.RiverCenter(wz,river)+(x/4f-1)*WorldMap.RiverWidth(wz,river);
                    draft.Add(new Vector3(wx,WorldMap.RiverWaterHeight(wz,river)+.025f,wz),new Vector2(x/8f,wz*.14f),Color.white);
                }
                for(int z=0;z<40;z++)for(int x=0;x<8;x++)
                {
                    int a=z*9+x;Vector3 center=draft.v[a];
                    if(new Vector2(center.x,center.z).magnitude>GameData.ArenaRadius-6)continue;
                    draft.Tri(a,a+9,a+1);draft.Tri(a+1,a+9,a+10);
                }
                draft.Save("Stream "+river+" / "+chunk,streams,material,default,false);
            }
        }
        static void Tree(Cell cell,WorldProp prop,System.Random random,int stage,Color leaf,bool? conifer=null)
        {
            Vector3 p=prop.position;float scale=prop.size,height=Range(random,6.4f,8.6f)*scale,angle=prop.angle*Mathf.Deg2Rad;
            Color bark=Color.Lerp(new Color(.39f,.31f,.23f),new Color(.62f,.52f,.38f),Range(random,0,1));
            var lean=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*Range(random,.18f,.65f)*scale;
            var trunk=new Vector3[7];var radii=new float[7];trunk[0]=p-Vector3.up*.18f;radii[0]=.43f*scale;
            for(int segment=1;segment<=6;segment++)
            {
                float t=segment/6f;var next=p+Vector3.up*height*t+lean*(t*t)+new Vector3(Mathf.Sin(t*5+angle),0,Mathf.Cos(t*4+angle))*.10f*scale*t;
                trunk[segment]=next;radii[segment]=Mathf.Lerp(.34f,.025f,t)*scale;
            }
            cell.bark.Branch(trunk,radii,bark,10);Vector3 previous=trunk[6];
            for(int r=0;r<6;r++)
            {
                float a=r*Mathf.PI/3+angle,reach=Range(random,.7f,1.25f)*scale;
                var root=WorldMap.OnGround(p.x+Mathf.Cos(a)*reach,p.z+Mathf.Sin(a)*reach)+Vector3.up*.025f;
                var knee=Vector3.Lerp(root,p,.65f)+Vector3.up*.23f*scale;
                cell.bark.Branch(new[]{root-Vector3.up*.06f,knee,p+Vector3.up*.65f*scale},new[]{.025f*scale,.12f*scale,.20f*scale},bark,8);
            }
            bool pine=conifer??(stage==3||stage==6||random.NextDouble()<.22);
            if(pine)
            {
                for(int band=0;band<7;band++)
                {
                    float t=.27f+band*.103f,reach=(2.3f-band*.28f)*scale;var start=p+Vector3.up*height*t+lean*t*t;
                    cell.leaves.Crown(start+Vector3.up*.3f*scale,new Vector3(reach,.86f*scale,reach),leaf*(.88f+band*.025f),band*71+(int)prop.angle);
                    for(int b=0;b<6;b++)
                    {
                        float a=angle+b*Mathf.PI/3+band*.72f;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                        var end=start+radial*reach+Vector3.up*height*.035f;
                        cell.bark.Tube(start,end,.085f*scale*(1-t),.012f*scale,bark,6);
                        for(int twig=0;twig<3;twig++)
                        {
                            var tip=Vector3.Lerp(start,end,.42f+twig*.25f)+Vector3.up*.15f*scale;
                            if(twig==2)Spray(cell.leaves,tip,new Vector3(.50f,.30f,.50f)*scale*(1-band*.07f),leaf*(.84f+band*.035f),random,7,true);
                        }
                    }
                }
                Spray(cell.leaves,previous,new Vector3(.25f,.5f,.25f)*scale,leaf,random,20,true);
                return;
            }
            int limbs=random.Next(8,12);
            cell.leaves.Crown(p+Vector3.up*height*.73f+lean*.5f,new Vector3(1.9f,1.75f,1.9f)*scale,leaf,(int)prop.angle);
            for(int b=0;b<limbs;b++)
            {
                float a=angle+b*2.399963f,t=.34f+b/(float)limbs*.48f,reach=Range(random,1.8f,2.9f)*scale*(1-(t-.34f)*.6f);
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var start=p+Vector3.up*height*t+lean*t*t;
                var elbow=start+radial*reach*.62f+Vector3.up*height*.10f;
                var tip=start+radial*reach+Vector3.up*height*Range(random,.15f,.25f);
                float thickness=(.15f-b*.007f)*scale;
                cell.bark.Branch(new[]{start,elbow,tip},new[]{thickness,thickness*.55f,.02f*scale},bark,8);
                cell.leaves.Crown(tip+Vector3.up*.22f*scale,new Vector3(1.20f,1.02f,1.18f)*scale,leaf*(.95f+t*.08f),b*97+(int)prop.angle);
                for(int fork=0;fork<4;fork++)
                {
                    float turn=a+(fork-1.5f)*.70f;var twigStart=Vector3.Lerp(elbow,tip,.25f+fork*.18f);
                    var end=twigStart+new Vector3(Mathf.Cos(turn)*.8f,Range(random,.25f,.8f),Mathf.Sin(turn)*.8f)*scale;
                    cell.bark.Tube(twigStart,end,.035f*scale,.008f*scale,bark,5);
                    if(fork%2==0)Spray(cell.leaves,end,new Vector3(.65f,.44f,.65f)*scale,leaf*(.94f+t*.15f),random,8,false);
                }
            }
            cell.leaves.Crown(previous-Vector3.up*.35f*scale,new Vector3(1.1f,1.2f,1.08f)*scale,leaf*1.04f,719+(int)prop.angle);
        }
    }
}
