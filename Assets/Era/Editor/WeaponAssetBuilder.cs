using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class WeaponAssetBuilder
    {
        const string Folder="Assets/Era/Resources/Weapons/",Marker="Assets/Era/Generated/Weapons-v19.txt",Signature="smooth weapons / fitted sockets / v19.3";
        static readonly Color Wood=new Color(.29f,.17f,.085f),Grip=new Color(.15f,.085f,.05f),Gold=new Color(.78f,.53f,.19f),Metal=new Color(.66f,.74f,.76f);
        internal sealed class Draft
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();readonly List<Vector2> surfaces=new List<Vector2>();
            int Vertex(Vector3 p,Vector3 n,Color c,bool metal){int at=vertices.Count;vertices.Add(p);normals.Add(n);colors.Add(c.linear);surfaces.Add(metal?new Vector2(.38f,.72f):new Vector2(.10f,0));return at;}
            void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,bool metal){var cross=Vector3.Cross(b-a,c-a);float length=Mathf.Sqrt(cross.sqrMagnitude);if(length<1e-10f)return;var n=cross/length;int first=Vertex(a,n,color,metal);Vertex(b,n,color,metal);Vertex(c,n,color,metal);triangles.AddRange(new[]{first,first+1,first+2});}
            public void Tube(Vector3[] centers,float[] radius,Color color,bool metal=false,int sides=12)
            {
                int first=vertices.Count;
                for(int row=0;row<centers.Length;row++)
                {
                    Vector3 axis=(centers[Mathf.Min(row+1,centers.Length-1)]-centers[Mathf.Max(0,row-1)]).normalized,u=Vector3.Cross(axis,Vector3.forward).normalized;if(u.sqrMagnitude<.01f)u=Vector3.right;Vector3 v=Vector3.Cross(axis,u);
                    for(int s=0;s<sides;s++){float a=s*Mathf.PI*2/sides;Vector3 n=u*Mathf.Cos(a)+v*Mathf.Sin(a);Vertex(centers[row]+n*radius[row],n,color,metal);}
                }
                for(int row=0;row<centers.Length-1;row++)for(int s=0;s<sides;s++){int a=first+row*sides+s,b=first+row*sides+(s+1)%sides;triangles.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});}
                for(int end=0;end<2;end++){int row=end==0?0:centers.Length-1;for(int s=0;s<sides;s++){Vector3 a=vertices[first+row*sides+s],b=vertices[first+row*sides+(s+1)%sides];if(end==0)Tri(centers[row],b,a,color,metal);else Tri(centers[row],a,b,color,metal);}}
            }
            public void Blade(Vector2[] outline,float thickness,Color color)
            {
                float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-b.x*a.y;}if(area<0)Array.Reverse(outline);
                Vector2 center=Vector2.zero;foreach(var p in outline)center+=p;center/=outline.Length;
                for(int face=-1;face<=1;face+=2)for(int i=0;i<outline.Length;i++)
                {
                    Vector2 a=outline[i],b=outline[(i+1)%outline.Length],ia=Vector2.Lerp(a,center,.09f),ib=Vector2.Lerp(b,center,.09f);
                    Vector3 pa=new Vector3(a.x,a.y,face*thickness*.20f),pb=new Vector3(b.x,b.y,face*thickness*.20f),qa=new Vector3(ia.x,ia.y,face*thickness),qb=new Vector3(ib.x,ib.y,face*thickness),ridge=new Vector3(center.x,center.y,face*thickness*1.25f);
                    if(face>0){Tri(pa,pb,qb,color*1.12f,true);Tri(pa,qb,qa,color*1.12f,true);Tri(qa,qb,ridge,color,true);}else{Tri(pb,pa,qa,color*.9f,true);Tri(pb,qa,qb,color*.9f,true);Tri(qb,qa,ridge,color*.95f,true);}
                    Vector3 backA=new Vector3(a.x,a.y,-face*thickness*.20f),backB=new Vector3(b.x,b.y,-face*thickness*.20f);if(face>0){Tri(pa,backA,backB,color,true);Tri(pa,backB,pb,color,true);}
                }
            }
            public Mesh Mesh(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetNormals(normals);m.SetColors(colors);m.SetUVs(1,surfaces);m.SetTriangles(triangles,0);m.RecalculateBounds();return m;}
            public void Feather(float angle)
            {
                var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);var side=new Vector3(-radial.y,radial.x,0)*.003f;
                var edge=new[]{Vector3.back*.36f+radial*.012f,Vector3.back*.39f+radial*.10f,Vector3.back*.20f+radial*.075f,Vector3.back*.16f+radial*.012f};var color=new Color(.71f,.83f,.69f);
                for(int face=-1;face<=1;face+=2)for(int i=1;i<3;i++)
                {var a=edge[0]+side*face;var b=edge[i]+side*face;var c=edge[i+1]+side*face;if(face>0)Tri(a,c,b,color,false);else Tri(a,b,c,color,false);}
                for(int i=0;i<4;i++){int j=(i+1)%4;Tri(edge[i]-side,edge[j]-side,edge[j]+side,color,false);Tri(edge[i]-side,edge[j]+side,edge[i]+side,color,false);}
            }
        }
        static Transform Child(Transform parent,string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        public static void Prepare()
        {
            if(File.Exists(Marker)&&File.ReadAllText(Marker).Trim()==Signature)return;Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            const string libraryPath="Assets/Era/Generated/Weapons-v19.asset";if(File.Exists(libraryPath))AssetDatabase.DeleteAsset(libraryPath);
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,libraryPath);var material=new Material(Shader.Find("Era/Character Surface")){name="Carved wood, leather and honed steel"};AssetDatabase.AddObjectToAsset(material,library);
            foreach(WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                var root=new GameObject(kind.ToString());var draft=new Draft();Build(draft,kind);
                var mesh=draft.Mesh(kind+" / shaped silhouette and bevels");AssetDatabase.AddObjectToAsset(mesh,library);var surface=Child(root.transform,"Crafted weapon",Vector3.zero);surface.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
                var profile=ScriptableObject.CreateInstance<WeaponGripProfile>();profile.name=kind+" / grip";profile.kind=kind;profile.primaryPoint=kind==WeaponKind.Bow?new Vector3(.36f,.48f,0):new Vector3(0,.05f,0);profile.secondaryPoint=kind==WeaponKind.Bow?new Vector3(0,.48f,0):new Vector3(0,kind==WeaponKind.Spear?.38f:.5f,0);
                profile.twoHanded=kind==WeaponKind.Bow||kind==WeaponKind.Spear||kind==WeaponKind.LightningStaff;profile.primaryLeft=kind==WeaponKind.Bow;profile.fingerClosure=kind==WeaponKind.Bow?.7f:.16f;
                profile.aimEuler=new Vector3(0,kind==WeaponKind.Bow?-90:0,kind==WeaponKind.Bow?-5:-10);
                profile.holdFromChest=kind==WeaponKind.Bow?new Vector3(-.12f,.03f,.38f):kind==WeaponKind.Spear?new Vector3(.06f,-.16f,.10f):new Vector3(.08f,-.25f,.17f);
                AssetDatabase.AddObjectToAsset(profile,library);var points=root.AddComponent<WeaponGripPoints>();points.profile=profile;points.primary=Child(root.transform,"Grip / primary",profile.primaryPoint);points.secondary=Child(root.transform,"Grip / secondary",profile.secondaryPoint);
                root.AddComponent<WeaponPresentation>().kind=kind;
                if(kind==WeaponKind.Bow)
                {
                    var line=Child(root.transform,"Bow string",Vector3.zero).gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=3;line.widthMultiplier=.009f;line.sharedMaterial=material;line.SetPositions(new[]{new Vector3(0,-.24f,0),new Vector3(0,.48f,0),new Vector3(0,1.2f,0)});
                    var arrow=Child(root.transform,"Nocked arrow",new Vector3(.1f,.48f,0));var d=new Draft();Arrow(d);var a=d.Mesh("Nocked arrow");AssetDatabase.AddObjectToAsset(a,library);arrow.gameObject.AddComponent<MeshFilter>().sharedMesh=a;arrow.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;arrow.localRotation=Quaternion.Euler(0,90,0);arrow.localScale=Vector3.one*.7f;
                }
                if(kind==WeaponKind.LightningStaff||kind==WeaponKind.Aura){var focus=Child(root.transform,kind==WeaponKind.Aura?"Aura focus":"Charged heart",Vector3.up*(kind==WeaponKind.Aura?.35f:1.43f));}
                PrefabUtility.SaveAsPrefabAsset(root,Folder+kind+".prefab");UnityEngine.Object.DestroyImmediate(root);
            }
            var arrowRoot=new GameObject("Wildwood arrow");var arrowDraft=new Draft();Arrow(arrowDraft);var arrowMesh=arrowDraft.Mesh("Wildwood arrow / steel and feather");AssetDatabase.AddObjectToAsset(arrowMesh,library);arrowRoot.AddComponent<MeshFilter>().sharedMesh=arrowMesh;arrowRoot.AddComponent<MeshRenderer>().sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(arrowRoot,Folder+"Arrow.prefab");UnityEngine.Object.DestroyImmediate(arrowRoot);
            File.WriteAllText(Marker,Signature);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static void Build(Draft d,WeaponKind kind)
        {
            if(kind==WeaponKind.Bow)
            {
                var points=new Vector3[25];var radius=new float[25];for(int i=0;i<25;i++){float a=Mathf.Lerp(-Mathf.PI/2,Mathf.PI/2,i/24f);points[i]=new Vector3(Mathf.Cos(a)*.36f,Mathf.Sin(a)*.72f+.48f,Mathf.Sin(a*2)*.018f);radius[i]=.018f+Mathf.Cos(a)*.013f;}d.Tube(points,radius,Wood,false,10);d.Tube(new[]{new Vector3(.36f,.35f,0),new Vector3(.37f,.48f,0),new Vector3(.36f,.62f,0)},new[]{.035f,.039f,.035f},Grip,false,12);return;
            }
            if(kind==WeaponKind.Aura){d.Tube(new[]{Vector3.up*.05f,Vector3.up*.18f,Vector3.up*.35f,Vector3.up*.52f,Vector3.up*.65f},new[]{.025f,.11f,.17f,.11f,.001f},new Color(.27f,.72f,.65f),true,8);return;}
            float top=kind==WeaponKind.Spear?1.85f:kind==WeaponKind.LightningStaff?1.4f:.72f;
            d.Tube(new[]{Vector3.down*.32f,Vector3.down*.15f,Vector3.up*.15f,Vector3.up*(top*.7f),Vector3.up*top},new[]{.043f,.037f,.036f,.031f,.028f},Wood);
            d.Tube(new[]{Vector3.down*.18f,Vector3.zero,Vector3.up*.16f},new[]{.043f,.047f,.041f},Grip);
            for(int i=0;i<5;i++){float y=-.14f+i*.066f;d.Tube(new[]{Vector3.up*y,Vector3.up*(y+.012f)},new[]{.048f,.047f},Gold,true);}
            if(kind==WeaponKind.Axe)d.Blade(new[]{new Vector2(.02f,.65f),new Vector2(.15f,.92f),new Vector2(.43f,.96f),new Vector2(.60f,.78f),new Vector2(.56f,.42f),new Vector2(.36f,.31f),new Vector2(.14f,.46f)},.037f,Metal);
            else if(kind==WeaponKind.Sword)
            {
                d.Blade(new[]{new Vector2(-.065f,.25f),new Vector2(-.085f,1.20f),new Vector2(0,1.48f),new Vector2(.085f,1.20f),new Vector2(.065f,.25f)},.028f,Metal);
                d.Tube(new[]{new Vector3(-.25f,.17f,0),new Vector3(-.12f,.22f,0),new Vector3(.12f,.22f,0),new Vector3(.25f,.17f,0)},new[]{.025f,.04f,.04f,.025f},Gold,true,10);
            }
            else if(kind==WeaponKind.Spear)d.Blade(new[]{new Vector2(0,2.43f),new Vector2(.17f,2.02f),new Vector2(.07f,1.82f),new Vector2(-.07f,1.82f),new Vector2(-.17f,2.02f)},.035f,Metal);
            else
            {
                for(int side=-1;side<=1;side+=2)d.Tube(new[]{Vector3.up*1.13f,new Vector3(side*.15f,1.3f,0),new Vector3(side*.18f,1.55f,0),new Vector3(side*.08f,1.68f,0)},new[]{.035f,.034f,.025f,.009f},Gold,true,10);
                d.Tube(new[]{Vector3.up*1.25f,Vector3.up*1.42f,Vector3.up*1.64f},new[]{.01f,.13f,.001f},new Color(.50f,.46f,.8f),true,8);
            }
        }
        static void Arrow(Draft d)
        {
            d.Tube(new[]{Vector3.back*.40f,Vector3.zero,Vector3.forward*.35f},new[]{.013f,.016f,.012f},Wood,false,8);
            d.Tube(new[]{Vector3.forward*.30f,Vector3.forward*.37f,Vector3.forward*.55f},new[]{.035f,.055f,.001f},Metal,true,6);
            for(int i=0;i<3;i++)d.Feather(i*Mathf.PI*2/3);
        }
        public static void InspectNormals()
        {
            foreach(WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))foreach(var f in Resources.Load<GameObject>("Weapons/"+kind).GetComponentsInChildren<MeshFilter>())
            {var ns=f.sharedMesh.normals;int bad=Array.FindIndex(ns,n=>n.sqrMagnitude<.5f);if(bad>=0)Debug.Log("WEAPON_NORMAL_BAD "+kind+" / "+f.name+" / "+bad+" / "+ns[bad]);}
            foreach(var f in Resources.Load<GameObject>("Creatures/Necromancer").GetComponentsInChildren<MeshFilter>())
            {var ns=f.sharedMesh.normals;int bad=Array.FindIndex(ns,n=>n.sqrMagnitude<.5f);if(bad>=0)Debug.Log("NPC_NORMAL_BAD "+f.transform.parent.name+" / "+f.sharedMesh.name+" / "+bad+" / "+ns[bad]+" / "+f.sharedMesh.vertices[bad]);}
        }
    }
}
