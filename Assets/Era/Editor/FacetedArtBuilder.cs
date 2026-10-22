using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Era.Editor
{
    // Original meshes inspired by the supplied silhouette/color references. Reuses only the
    // existing CC0 skeleton and clips: no smoothing, subdivision or rounded character surfaces.
    public static class FacetedArtBuilder
    {
        const string Marker="Assets/Era/Generated/FacetedActors-v11b.txt";
        static readonly Color Skin=new Color(.71f,.55f,.43f),Ivory=new Color(.83f,.80f,.64f),Leather=new Color(.24f,.13f,.085f),Gold=new Color(.72f,.48f,.17f),Dark=new Color(.075f,.09f,.10f);
        sealed class Draft
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();readonly List<BoneWeight> weights=new List<BoneWeight>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,int bone)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c});triangles.AddRange(new[]{n,n+1,n+2});
                float shade=.94f+.09f*Mathf.Sin(n*1.71f);color*=shade;color.a=1;
                for(int i=0;i<3;i++){colors.Add(color.linear);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int bone){Tri(a,b,c,color,bone);Tri(a,c,d,color,bone);}
            public void FrontTri(Vector3 a,Vector3 b,Vector3 c,Color color,int bone){if(Vector3.Cross(b-a,c-a).z<0)Tri(a,c,b,color,bone);else Tri(a,b,c,color,bone);}
            public void FrontQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int bone){FrontTri(a,b,c,color,bone);FrontTri(a,c,d,color,bone);}
            public void Box(Vector3 p,Vector3 size,Color color,int bone)
            {
                var q=new Vector3[8];for(int i=0;i<8;i++)q[i]=p+Vector3.Scale(new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1),size)*.5f;
                Quad(q[0],q[2],q[3],q[1],color,bone);Quad(q[4],q[5],q[7],q[6],color,bone);Quad(q[0],q[4],q[6],q[2],color,bone);Quad(q[1],q[3],q[7],q[5],color,bone);Quad(q[2],q[6],q[7],q[3],color,bone);Quad(q[0],q[1],q[5],q[4],color,bone);
            }
            public void Tube(Vector3 a,Vector3 b,float radiusA,float radiusB,Color color,int bone,int sides=8,float depth=1)
            {
                Vector3 axis=(b-a).normalized,u=Vector3.Cross(axis,Vector3.forward).normalized;if(u.sqrMagnitude<.1f)u=Vector3.right;Vector3 v=Vector3.Cross(axis,u);
                for(int i=0;i<sides;i++)
                {
                    float x=i*Mathf.PI*2/sides,y=(i+1)*Mathf.PI*2/sides;
                    Vector3 p=u*Mathf.Cos(x)+v*Mathf.Sin(x)*depth,q=u*Mathf.Cos(y)+v*Mathf.Sin(y)*depth;
                    Quad(a+p*radiusA,a+q*radiusA,b+q*radiusB,b+p*radiusB,color,bone);Tri(a,a+q*radiusA,a+p*radiusA,color*.85f,bone);Tri(b,b+p*radiusB,b+q*radiusB,color,bone);
                }
            }
            public void Ellipsoid(Vector3 p,Vector3 radius,Color color,int bone,int sides=8,int rings=4)
            {
                Vector3 Point(int r,int s){float a=s*Mathf.PI*2/sides,y=-Mathf.PI*.5f+r*Mathf.PI/rings;return p+Vector3.Scale(new Vector3(Mathf.Cos(a)*Mathf.Cos(y),Mathf.Sin(y),Mathf.Sin(a)*Mathf.Cos(y)),radius);}
                for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)Quad(Point(r,s),Point(r+1,s),Point(r+1,s+1),Point(r,s+1),color,bone);
            }
            public Mesh Mesh(string name,Matrix4x4[] bindposes)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        sealed class Actor
        {
            public GameObject root;public Transform[] bones;public Draft mesh=new Draft();
            public int Bone(string name){int i=Array.FindIndex(bones,b=>b.name==name);if(i<0)throw new Exception("Missing reference rig bone: "+name);return i;}
            public Vector3 P(string name)=>root.transform.InverseTransformPoint(bones[Bone(name)].position);
            public void Box(Vector3 p,Vector3 size,Color color,string bone)=>mesh.Box(p,size,color,Bone(bone));
            public void Ball(Vector3 p,Vector3 radius,Color color,string bone)=>mesh.Ellipsoid(p,radius,color,Bone(bone));
            public void Tube(Vector3 a,Vector3 b,float ra,float rb,Color color,string bone,int sides=8,float depth=1)=>mesh.Tube(a,b,ra,rb,color,Bone(bone),sides,depth);
        }
        public static void Prepare()
        {
            if(File.Exists(Marker))return;
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,"Assets/Era/Generated/FacetedActors-v11b.asset");
            var material=new Material(Shader.Find("Era/Faceted Realm")){name="Earth, linen and leather / faceted palette"};AssetDatabase.AddObjectToAsset(material,library);
            foreach(string name in new[]{"Ember","Echo","Lumen","Stalker","Wisp"})
            {
                bool hero=name=="Ember"||name=="Echo"||name=="Lumen";
                // Restore the pre-smoothing rigs as the starting point for all new silhouettes.
                string source="Assets/Era/Art/KayKit/HumanTemplates/"+(hero?name:"Echo")+".prefab";
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));root.name=name;root.transform.localScale=Vector3.one;
                try
                {
                    foreach(var shape in root.GetComponents<HumanRigShape>())Object.DestroyImmediate(shape);
                    var boneSet=new HashSet<Transform>();foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var b in skin.bones)for(var t=b;t!=null&&t!=root.transform;t=t.parent)boneSet.Add(t);
                    int Depth(Transform t){int d=0;for(;t.parent!=null;t=t.parent)d++;return d;}
                    var bones=boneSet.OrderBy(Depth).ToArray();var original=bones.Select(b=>b.localPosition).ToArray();var positions=bones.Select(b=>HumanRigShape.BonePosition(root.transform,b,true)).ToArray();
                    for(int i=0;i<bones.Length;i++)bones[i].position=root.transform.TransformPoint(positions[i]);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))Object.DestroyImmediate(renderer);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
                    var actor=new Actor{root=root,bones=bones};Body(actor,name);Costume(actor,name);
                    var go=new GameObject("Faceted silhouette / "+name);go.transform.SetParent(root.transform,false);
                    var mesh=actor.mesh.Mesh(name+" / original flat shaded costume",bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray());AssetDatabase.AddObjectToAsset(mesh,library);
                    var skinRenderer=go.AddComponent<SkinnedMeshRenderer>();skinRenderer.sharedMesh=mesh;skinRenderer.bones=bones;skinRenderer.rootBone=bones[0];skinRenderer.sharedMaterial=material;skinRenderer.localBounds=new Bounds(Vector3.up*1.4f,new Vector3(4,5,4));
                    var retarget=root.AddComponent<HumanRigShape>();retarget.bones=bones;retarget.originalLocalPositions=original;retarget.extendForearms=true;
                    var skeleton=root.GetComponent<SkeletalActor>();
                    if(hero)root.GetComponent<HeroMotion>().BindImported((Hero)Enum.Parse(typeof(Hero),name),skeleton);
                    else
                    {
                        Object.DestroyImmediate(root.GetComponent<HeroMotion>());var creature=root.AddComponent<CreatureMotion>();creature.kind=(EnemyKind)Enum.Parse(typeof(EnemyKind),name);creature.body=skeleton.hips;creature.skeletal=skeleton;
                        foreach(string bone in new[]{"upperarm.l","upperarm.r","upperleg.l","upperleg.r"})creature.limbs.Add(bones[actor.Bone(bone)]);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,"Assets/Era/Resources/"+(hero?"Characters/":"Creatures/")+name+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
            }
            File.WriteAllText(Marker,"Faceted ranger, earth monk, bearded aura keeper, wolf stalker and masked jester. Original geometry; CC0 KayKit rigs/clips retained.");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static void Body(Actor a,string name)
        {
            bool wolf=name=="Stalker",jester=name=="Wisp";
            Color cloth=wolf?new Color(.18f,.20f,.25f):jester?new Color(.055f,.23f,.25f):name=="Ember"?new Color(.19f,.31f,.20f):name=="Echo"?new Color(.42f,.27f,.16f):Ivory;
            Vector3 hip=a.P("hips"),chest=a.P("chest"),head=a.P("head")-Vector3.up*.08f;
            a.Tube(hip+Vector3.up*.03f,chest+Vector3.up*.22f,wolf?.27f:.24f,wolf?.39f:.31f,cloth,"spine",10,.68f);
            a.Tube(chest+Vector3.up*.16f,head+Vector3.up*.12f,.09f,.085f,wolf?cloth:Skin,"head");
            a.Ball(head+Vector3.up*.20f,new Vector3(.18f,.235f,.17f),wolf?cloth:jester?Ivory:Skin,"head");
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"r":"l";Vector3 shoulder=a.P("upperarm."+s),elbow=a.P("lowerarm."+s),hand=a.P("hand."+s),thigh=a.P("upperleg."+s),knee=a.P("lowerleg."+s),foot=a.P("foot."+s);
                a.Tube(shoulder,elbow,wolf?.15f:.12f,wolf?.13f:.11f,cloth,"upperarm."+s,8);a.Tube(elbow,hand,wolf?.13f:.105f,.075f,wolf?cloth:name=="Echo"?Skin:cloth,"lowerarm."+s);
                a.Ball(hand+Vector3.down*.025f,new Vector3(.075f,.11f,.07f),wolf?Dark:name=="Echo"?Skin:Leather,"hand."+s);
                a.Tube(thigh,knee,.14f,.10f,cloth*.76f,"upperleg."+s);a.Tube(knee,foot,.105f,.075f,wolf?cloth:Leather,"lowerleg."+s);
                a.Tube(foot+Vector3.down*.20f,foot+Vector3.up*.015f,.08f,.095f,wolf?Dark:Leather,"foot."+s,6);
                a.mesh.Ellipsoid(foot+new Vector3(0,-.18f,.07f),new Vector3(.095f,.065f,.17f),wolf?Dark:Leather,a.Bone("foot."+s),6,2);
                a.Tube(elbow,Vector3.Lerp(elbow,hand,.38f),.12f,.115f,name=="Echo"?Ivory:Leather,"lowerarm."+s);
                a.Tube(knee,Vector3.Lerp(knee,foot,.28f),.115f,.11f,name=="Ember"?Ivory:cloth*.8f,"lowerleg."+s);
            }
            if(!wolf)
            {
                for(int side=-1;side<=1;side+=2)a.Box(head+new Vector3(side*.065f,.23f,.158f),new Vector3(.054f,.023f,.022f),Dark,"head");
                a.Ball(head+new Vector3(0,.17f,.18f),new Vector3(.035f,.045f,.045f),jester?Ivory:Skin*.92f,"head");
            }
            a.Tube(hip+Vector3.up*.18f,hip+Vector3.up*.27f,.267f,.267f,Leather,"hips",10,.73f);
            a.Box(hip+new Vector3(0,.225f,.204f),new Vector3(.12f,.105f,.035f),Gold,"hips");a.Box(hip+new Vector3(0,.225f,.228f),new Vector3(.06f,.045f,.018f),Dark,"hips");
        }
        static void Cape(Actor a,Color color,float length,float width)
        {
            var c=a.P("chest");float bottom=c.y-length;
            a.mesh.Quad(c+new Vector3(-.25f,.13f,-.15f),new Vector3(-width,bottom,-.26f),new Vector3(0,bottom+.13f,-.39f),c+new Vector3(0,.13f,-.20f),color,a.Bone("spine"));
            a.mesh.Quad(c+new Vector3(0,.13f,-.20f),new Vector3(0,bottom+.13f,-.39f),new Vector3(width,bottom,-.26f),c+new Vector3(.25f,.13f,-.15f),color*.83f,a.Bone("spine"));
            // Double-sided cloth as geometry avoids a separate transparent material and sorting.
            a.mesh.Quad(c+new Vector3(.25f,.13f,-.15f),new Vector3(width,bottom,-.26f),new Vector3(-width,bottom,-.26f),c+new Vector3(-.25f,.13f,-.15f),color*.7f,a.Bone("spine"));
        }
        static void Robe(Actor a,Color color,float bottom,bool split)
        {
            var hip=a.P("hips");int sides=12;
            for(int i=0;i<sides;i++)
            {
                float x=i*Mathf.PI*2/sides,y=(i+1)*Mathf.PI*2/sides;float hem=bottom+(i%2)*.045f;
                if(split&&(i==2||i==3))continue;
                a.mesh.Quad(hip+new Vector3(Mathf.Cos(x)*.25f,.2f,Mathf.Sin(x)*.18f),hip+new Vector3(Mathf.Cos(y)*.25f,.2f,Mathf.Sin(y)*.18f),new Vector3(Mathf.Cos(y)*.39f,hem,Mathf.Sin(y)*.29f),new Vector3(Mathf.Cos(x)*.39f,hem,Mathf.Sin(x)*.29f),color*(i%3==0?.8f:1),a.Bone("hips"));
            }
        }
        static void Costume(Actor a,string name)
        {
            Vector3 head=a.P("head")-Vector3.up*.08f,chest=a.P("chest"),hip=a.P("hips");
            if(name=="Ember")
            {
                Color green=new Color(.15f,.27f,.20f);Cape(a,green,1.14f,.42f);Robe(a,green,.40f,true);
                // Hood panels leave a deep, angular opening around the face.
                for(int side=-1;side<=1;side+=2)
                {
                    a.mesh.FrontQuad(head+new Vector3(0,.43f,-.06f),head+new Vector3(side*.25f,.34f,-.15f),head+new Vector3(side*.25f,-.02f,.12f),head+new Vector3(side*.14f,.30f,.22f),green,a.Bone("head"));
                    a.mesh.Tri(head+new Vector3(side*.16f,.23f,0),head+new Vector3(side*.37f,.24f,.02f),head+new Vector3(side*.17f,.12f,.06f),Skin,a.Bone("head"));
                    a.Tube(a.P("upperarm."+(side<0?"r":"l")),Vector3.Lerp(a.P("upperarm."+(side<0?"r":"l")),a.P("lowerarm."+(side<0?"r":"l")),.37f),.155f,.14f,green,"upperarm."+(side<0?"r":"l"));
                }
                a.mesh.Quad(head+new Vector3(-.25f,.34f,-.15f),head+new Vector3(0,.51f,-.06f),head+new Vector3(.25f,.34f,-.15f),head+new Vector3(.19f,-.02f,-.18f),green*.85f,a.Bone("head"));
                a.Tube(chest+new Vector3(-.30f,.14f,-.26f),chest+new Vector3(-.30f,-.48f,-.30f),.115f,.095f,Leather,"spine");
                for(int i=0;i<6;i++){var p=chest+new Vector3(-.38f+i*.033f,.30f+(i%2)*.045f,-.27f);a.Tube(p,p+Vector3.down*.45f,.009f,.009f,Gold,"spine",4);a.Box(p,new Vector3(.025f,.075f,.025f),Ivory,"spine");}
                a.mesh.FrontQuad(chest+new Vector3(-.24f,.14f,.23f),hip+new Vector3(.25f,.27f,.205f),hip+new Vector3(.17f,.20f,.22f),chest+new Vector3(-.30f,.09f,.23f),Leather,a.Bone("spine"));
            }
            else if(name=="Echo")
            {
                Color earth=new Color(.42f,.28f,.17f);Robe(a,earth,.35f,true);
                a.mesh.FrontQuad(chest+new Vector3(-.24f,.17f,.23f),hip+new Vector3(.13f,.28f,.21f),hip+new Vector3(.23f,.28f,.22f),chest+new Vector3(-.15f,.21f,.23f),Ivory*.8f,a.Bone("spine"));
                a.mesh.FrontQuad(chest+new Vector3(.25f,.17f,.23f),hip+new Vector3(-.13f,.28f,.21f),hip+new Vector3(-.24f,.28f,.22f),chest+new Vector3(.15f,.21f,.23f),earth*.72f,a.Bone("spine"));
                for(int side=-1;side<=1;side+=2){a.Box(hip+new Vector3(side*.26f,.11f,.05f),new Vector3(.16f,.20f,.15f),Leather,"hips");a.Box(head+new Vector3(side*.065f,.285f,.151f),new Vector3(.07f,.019f,.026f),Dark,"head");}
                Cape(a,Leather,.53f,.29f);
            }
            else if(name=="Lumen")
            {
                Robe(a,Ivory,.10f,false);Cape(a,Leather,.89f,.44f);
                a.Tube(chest+Vector3.up*.28f,chest+Vector3.down*.20f,.14f,.43f,Leather,"chest",10,.65f);
                a.Box(chest+new Vector3(0,-.23f,.29f),new Vector3(.25f,.78f,.035f),Leather,"spine");
                a.mesh.FrontTri(head+new Vector3(-.16f,.14f,.18f),head+new Vector3(.16f,.14f,.18f),head+new Vector3(0,-.21f,.24f),Ivory,a.Bone("head"));
                a.mesh.FrontTri(head+new Vector3(-.15f,.14f,.18f),head+new Vector3(0,-.21f,.24f),head+new Vector3(-.14f,-.06f,.14f),Ivory*.77f,a.Bone("head"));
                a.Tube(head+Vector3.up*.36f,head+Vector3.up*.54f,.20f,.16f,Leather,"head",4,.75f);
                a.mesh.FrontTri(head+new Vector3(-.19f,.52f,.13f),head+new Vector3(.19f,.52f,.13f),head+new Vector3(0,.70f,.11f),Leather,a.Bone("head"));
                foreach(var mark in new[]{head+new Vector3(0,.51f,.164f),chest+new Vector3(0,-.21f,.32f)}){a.Box(mark,new Vector3(.035f,.15f,.018f),Gold,mark.y>head.y?"head":"spine");a.Box(mark+Vector3.up*.025f,new Vector3(.12f,.03f,.02f),Gold,mark.y>head.y?"head":"spine");}
                var hand=a.P("hand.l");a.Box(hand+new Vector3(0,0,.075f),new Vector3(.21f,.27f,.09f),Leather,"hand.l");a.Box(hand+new Vector3(0,0,.13f),new Vector3(.16f,.22f,.018f),Gold,"hand.l");
            }
            else if(name=="Stalker")Wolf(a,head,chest,hip);
            else Jester(a,head,chest);
        }
        static void Wolf(Actor a,Vector3 head,Vector3 chest,Vector3 hip)
        {
            Color fur=new Color(.16f,.18f,.23f),pale=new Color(.49f,.43f,.35f);Robe(a,new Color(.35f,.16f,.095f),.57f,true);
            a.Ball(head+new Vector3(0,.15f,.20f),new Vector3(.14f,.10f,.19f),pale,"head");a.Ball(head+new Vector3(0,.19f,.35f),new Vector3(.08f,.06f,.065f),Dark,"head");
            a.Ball(chest+new Vector3(0,-.11f,.20f),new Vector3(.27f,.29f,.08f),pale,"chest");
            for(int side=-1;side<=1;side+=2)
            {
                a.mesh.FrontTri(head+new Vector3(side*.08f,.35f,-.04f),head+new Vector3(side*.18f,.67f,-.02f),head+new Vector3(side*.23f,.27f,.06f),fur,a.Bone("head"));
                a.Box(head+new Vector3(side*.10f,.28f,.16f),new Vector3(.06f,.033f,.025f),new Color(.85f,.12f,.08f),"head");
                for(int i=0;i<6;i++){float y=.25f-i*.12f;a.mesh.FrontTri(chest+new Vector3(side*.24f,y,-.03f),chest+new Vector3(side*(.43f+i*.012f),y-.18f,0),chest+new Vector3(side*.22f,y-.17f,.18f),fur*(i%2==0?1.2f:.85f),a.Bone("chest"));}
                var hand=a.P("hand."+(side<0?"r":"l"));for(int i=0;i<3;i++)a.Tube(hand+new Vector3((i-1)*.04f,-.07f,.03f),hand+new Vector3((i-1)*.045f,-.18f,.10f),.022f,0,Dark,"hand."+(side<0?"r":"l"),4);
            }
            a.Tube(hip+new Vector3(0,.02f,-.16f),hip+new Vector3(.25f,-.12f,-.64f),.16f,.11f,fur,"hips",7);a.Tube(hip+new Vector3(.25f,-.12f,-.64f),hip+new Vector3(.43f,-.30f,-.99f),.11f,0,fur,"hips",6);
        }
        static void Jester(Actor a,Vector3 head,Vector3 chest)
        {
            Color teal=new Color(.045f,.22f,.25f),red=new Color(.68f,.10f,.035f);Robe(a,Ivory,.33f,true);
            for(int side=-1;side<=1;side+=2)
            {
                string arm="upperarm."+(side<0?"r":"l");var p=a.P(arm);a.Ball(Vector3.Lerp(p,a.P("lowerarm."+(side<0?"r":"l")),.45f),new Vector3(.18f,.20f,.17f),side<0?teal:red,arm);
                a.Tube(head+new Vector3(side*.11f,.40f,0),head+new Vector3(side*.37f,.56f,0),.15f,.11f,side<0?teal:red,"head",5);a.Tube(head+new Vector3(side*.37f,.56f,0),head+new Vector3(side*.54f,.19f,.02f),.11f,0,side<0?teal:red,"head",5);
                a.Ball(head+new Vector3(side*.54f,.17f,.02f),Vector3.one*.055f,Gold,"head");
                a.mesh.Tri(head+new Vector3(side*.04f,.28f,.18f),head+new Vector3(side*.10f,.20f,.18f),head+new Vector3(side*.15f,.31f,.17f),Dark,a.Bone("head"));
            }
            for(int i=0;i<12;i++){float x=i*Mathf.PI/6,y=(i+1)*Mathf.PI/6;var c=chest+Vector3.up*.19f;a.mesh.Tri(c+new Vector3(Mathf.Cos(x)*.12f,.02f,Mathf.Sin(x)*.10f),c+new Vector3(Mathf.Cos(x)*.38f,-.12f,Mathf.Sin(x)*.30f),c+new Vector3(Mathf.Cos(y)*.38f,-.12f,Mathf.Sin(y)*.30f),Ivory*(i%2==0?1:.72f),a.Bone("chest"));}
        }
    }
}
