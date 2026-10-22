using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    // Both variants share six renderers and cached meshes/materials across all thirty chests.
    public static class ChestArt
    {
        static readonly Mesh[][] meshes=new Mesh[2][];
        static readonly Material[][] materials=new Material[2][];
        public static Transform Create(VisualFactory factory,Transform parent,ChestState state,out Transform lid)
        {
            int variant=state.tier>0?1:0;
            if(meshes[variant]==null)Build(variant);
            var root=factory.Group(variant==1?"Moon reliquary / silver and violet ash":"Wayfarer's coffer / oak and brass",parent,state.position);
            lid=factory.Group("Animated lid hinge",root,new Vector3(0,.86f,-.51f));
            for(int i=0;i<6;i++)
            {
                var part=factory.Group(new[]{"Oak planks and carved feet","Forged fittings and rivets","Velvet-lined hollow interior","Barrel lid / individual staves","Arched metal straps and hinges","Faceted lock jewel"}[i],i==3||i==4?lid:root);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh=meshes[variant][i];
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials[variant][i];
            }
            factory.Ring("Soft treasure marker",root,1.04f,variant==1?new Color(.66f,.56f,.94f):GameData.Amber,.023f).transform.localPosition=Vector3.up*.045f;
            return root;
        }
        static Material Finish(string name,Color color,float metal,float gloss)
        {
            var result=new Material(Shader.Find("Standard")){name=name,color=color};
            result.SetFloat("_Metallic",metal);result.SetFloat("_Glossiness",gloss);return result;
        }
        static void Build(int variant)
        {
            var wood=new Draft();var iron=new Draft();var lining=new Draft();
            var lidWood=new Draft(new Vector3(0,-.86f,.51f));var lidIron=new Draft(new Vector3(0,-.86f,.51f));var jewel=new Draft();
            for(int row=0;row<3;row++)
            {
                float y=.31f+row*.211f;
                for(int sign=-1;sign<=1;sign+=2)
                {
                    wood.Box(new Vector3(0,y,sign*.468f),new Vector3(1.43f,.202f,.10f),.018f);
                    wood.Box(new Vector3(sign*.67f,y,0),new Vector3(.10f,.202f,.85f),.015f);
                }
            }
            wood.Box(new Vector3(0,.205f,0),new Vector3(1.39f,.12f,.96f),.025f);
            lining.Box(new Vector3(0,.28f,0),new Vector3(1.22f,.035f,.80f),.01f);
            for(int sign=-1;sign<=1;sign+=2)
            {
                lining.Box(new Vector3(0,.52f,sign*.409f),new Vector3(1.22f,.48f,.014f),.005f);
                lining.Box(new Vector3(sign*.61f,.52f,0),new Vector3(.014f,.48f,.80f),.005f);
                iron.Box(new Vector3(0,.83f,sign*.49f),new Vector3(1.48f,.055f,.055f),.014f);
                iron.Box(new Vector3(sign*.715f,.83f,0),new Vector3(.055f,.055f,.99f),.014f);
                for(int x=-1;x<=1;x+=2)
                {
                    wood.Box(new Vector3(x*.61f,.12f,sign*.38f),new Vector3(.21f,.22f,.20f),.05f);
                    iron.Box(new Vector3(x*.61f,.065f,sign*.38f),new Vector3(.23f,.065f,.22f),.018f);
                    iron.Box(new Vector3(x*.49f,.51f,sign*.527f),new Vector3(.095f,.66f,.028f),.009f);
                    for(int rivet=0;rivet<3;rivet++)iron.Box(new Vector3(x*.49f,.28f+rivet*.245f,sign*.55f),new Vector3(.036f,.036f,.026f),.011f);
                    iron.Box(new Vector3(x*.682f,.30f,sign*.46f),new Vector3(.12f,.12f,.12f),.024f);
                }
                // Side handles have a real opening rather than a solid primitive silhouette.
                for(int side=-1;side<=1;side+=2)iron.Box(new Vector3(sign*.737f,.59f,side*.17f),new Vector3(.05f,.11f,.095f),.018f);
                for(int segment=0;segment<14;segment++)
                {
                    float a=segment*Mathf.PI*2/14,b=(segment+1)*Mathf.PI*2/14;
                    iron.Bar(new Vector3(sign*.78f,.47f+Mathf.Sin(a)*.12f,Mathf.Cos(a)*.17f),new Vector3(sign*.78f,.47f+Mathf.Sin(b)*.12f,Mathf.Cos(b)*.17f),.027f);
                }
            }
            const int staves=9;
            for(int i=0;i<staves;i++)
            {
                float a=(i+.5f)*Mathf.PI/staves;
                Vector3 tangent=new Vector3(0,.34f*Mathf.Cos(a),-.51f*Mathf.Sin(a));
                float length=tangent.magnitude*Mathf.PI/staves;
                lidWood.Box(new Vector3(0,.86f+Mathf.Sin(a)*.34f,Mathf.Cos(a)*.51f),new Vector3(1.49f,.075f,length-.006f),.012f,Quaternion.FromToRotation(Vector3.forward,tangent));
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                lidWood.Cap(sign*.716f,.86f,.50f,.327f,sign);
                for(int i=0;i<16;i++)
                {
                    float a=(i+.5f)*Mathf.PI/16;
                    Vector3 normal=new Vector3(0,Mathf.Sin(a)/.34f,Mathf.Cos(a)/.51f).normalized;
                    Vector3 tangent=new Vector3(0,.34f*Mathf.Cos(a),-.51f*Mathf.Sin(a));
                    var center=new Vector3(sign*.49f,.86f+Mathf.Sin(a)*.34f,Mathf.Cos(a)*.51f)+normal*.055f;
                    lidIron.Box(center,new Vector3(.10f,.031f,tangent.magnitude*Mathf.PI/16+.008f),.008f,Quaternion.FromToRotation(Vector3.forward,tangent));
                    if(i%4==1)lidIron.Box(center+normal*.024f,new Vector3(.034f,.034f,.034f),.01f);
                }
                // The hinge barrels remain on the base while their leaves rotate with the lid.
                iron.Box(new Vector3(sign*.49f,.83f,-.553f),new Vector3(.18f,.095f,.095f),.035f);
                lidIron.Box(new Vector3(sign*.49f,.94f,-.52f),new Vector3(.12f,.16f,.035f),.012f);
            }
            lidWood.Box(new Vector3(0,.87f,0),new Vector3(1.43f,.035f,.99f),.008f);
            iron.Box(new Vector3(0,.71f,.554f),new Vector3(.21f,.26f,.062f),.035f);
            lidIron.Box(new Vector3(0,.91f,.557f),new Vector3(.135f,.21f,.041f),.024f);
            // An eight-face jewel makes the two rarities easy to distinguish at gameplay distance.
            jewel.Gem(new Vector3(0,.727f,.612f),new Vector3(variant==1?.075f:.049f,variant==1?.105f:.07f,.035f));
            var oak=Finish(variant==1?"Violet ash / painted grain":"Honey oak / painted grain",variant==1?new Color(.38f,.27f,.49f):new Color(.56f,.29f,.115f),0,.19f);
            oak.mainTexture=Resources.Load<Texture2D>("Textures/WoodDetail");
            var normalMap=Resources.Load<Texture2D>("Textures/WoodNormal");if(normalMap!=null){oak.SetTexture("_BumpMap",normalMap);oak.SetFloat("_BumpScale",.4f);oak.EnableKeyword("_NORMALMAP");}
            var metal=Finish(variant==1?"Brushed moon silver":"Antique warm brass",variant==1?new Color(.68f,.76f,.87f):new Color(.72f,.48f,.19f),.68f,.36f);
            var velvet=Finish("Recessed velvet lining",variant==1?new Color(.14f,.08f,.23f):new Color(.11f,.18f,.14f),0,.03f);
            var gem=Finish("Inset luminous jewel",variant==1?new Color(.58f,.30f,.95f):new Color(.14f,.67f,.47f),.28f,.65f);
            gem.EnableKeyword("_EMISSION");gem.SetColor("_EmissionColor",gem.color*.35f);
            meshes[variant]=new[]{wood.Mesh("Beveled planks"),iron.Mesh("Riveted fittings"),lining.Mesh("Hollow lining"),lidWood.Mesh("Nine curved lid staves"),lidIron.Mesh("Arched bands"),jewel.Mesh("Inset jewel")};
            materials[variant]=new[]{oak,metal,velvet,oak,metal,gem};
        }
        sealed class Draft
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> triangles=new List<int>();readonly Vector3 offset;
            public Draft(Vector3 offset=default){this.offset=offset;}
            static Vector3 Axis(int axis)=>axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var swap=b;b=d;d=swap;}
                int n=vertices.Count;vertices.Add(a+offset);vertices.Add(b+offset);vertices.Add(c+offset);vertices.Add(d+offset);
                float w=(b-a).magnitude*1.7f,h=(d-a).magnitude*1.7f;
                uv.Add(Vector2.zero);uv.Add(new Vector2(w,0));uv.Add(new Vector2(w,h));uv.Add(new Vector2(0,h));
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var swap=b;b=c;c=swap;}
                int n=vertices.Count;vertices.Add(a+offset);vertices.Add(b+offset);vertices.Add(c+offset);uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.up);triangles.AddRange(new[]{n,n+1,n+2});
            }
            public void Box(Vector3 center,Vector3 size,float bevel,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                Vector3 half=size*.5f;float b=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.9f);
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    int u=(axis+1)%3,v=(axis+2)%3;Vector3 face=Axis(axis)*half[axis]*sign,du=Axis(u)*(half[u]-b),dv=Axis(v)*(half[v]-b);
                    Quad(center+rotation*(face-du-dv),center+rotation*(face+du-dv),center+rotation*(face+du+dv),center+rotation*(face-du+dv),rotation*Axis(axis)*sign);
                }
                for(int axis=0;axis<3;axis++)for(int su=-1;su<=1;su+=2)for(int sv=-1;sv<=1;sv+=2)
                {
                    int u=(axis+1)%3,v=(axis+2)%3;Vector3 edge=Axis(axis)*(half[axis]-b);
                    Vector3 a=Axis(u)*su*half[u]+Axis(v)*sv*(half[v]-b),c=Axis(u)*su*(half[u]-b)+Axis(v)*sv*half[v];
                    Quad(center+rotation*(a-edge),center+rotation*(a+edge),center+rotation*(c+edge),center+rotation*(c-edge),rotation*(Axis(u)*su+Axis(v)*sv));
                }
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var corner=new Vector3(x*(half.x-b),y*(half.y-b),z*(half.z-b));
                    Triangle(center+rotation*(corner+Vector3.right*x*b),center+rotation*(corner+Vector3.up*y*b),center+rotation*(corner+Vector3.forward*z*b),rotation*new Vector3(x,y,z));
                }
            }
            public void Bar(Vector3 from,Vector3 to,float width)=>Box((from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)+.008f),width*.24f,Quaternion.FromToRotation(Vector3.forward,to-from));
            public void Cap(float x,float y,float rz,float ry,int sign)
            {
                for(int i=0;i<16;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Triangle(new Vector3(x,y,0),new Vector3(x,y+Mathf.Sin(a)*ry,Mathf.Cos(a)*rz),new Vector3(x,y+Mathf.Sin(b)*ry,Mathf.Cos(b)*rz),Vector3.right*sign);}
            }
            public void Gem(Vector3 center,Vector3 extent)
            {
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)Triangle(center+Vector3.right*extent.x*x,center+Vector3.up*extent.y*y,center+Vector3.forward*extent.z*z,new Vector3(x,y,z));
            }
            public Mesh Mesh(string name)
            {
                var result=new Mesh{name=name};result.SetVertices(vertices);result.SetUVs(0,uv);result.SetTriangles(triangles,0);result.RecalculateNormals();result.RecalculateTangents();result.RecalculateBounds();return result;
            }
        }
    }
}
