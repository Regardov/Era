using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    public sealed class VisualFactory
    {
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public Material Material(Color color,bool glow=false)
        {
            string key=ColorUtility.ToHtmlStringRGBA(color)+(glow?"glow":"matte");
            if(materials.TryGetValue(key,out var cached))return cached;
            var m=new Material(Shader.Find("Standard")){name="ERA "+key,color=color};
            m.SetFloat("_Glossiness",0.12f);
            if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.75f);}
            materials[key]=m;return m;
        }
        public Transform Group(string name,Transform parent,Vector3 position=default)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        public GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Color color,bool glow=false,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Material(color,glow);
            var c=go.GetComponent<Collider>();if(!collider && c!=null){if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);}
            return go;
        }
        public GameObject Ellipsoid(string name,Transform parent,Vector3 pos,Vector3 scale,Color color,bool glow=false)
            => Shape(name,PrimitiveType.Sphere,parent,pos,scale,color,glow);
        public GameObject Bone(string name,Transform parent,Vector3 pos,Vector3 scale,Color color)
            => Shape(name,PrimitiveType.Capsule,parent,pos,scale,color);
        public GameObject Ring(string name,Transform parent,float radius,Color color,float width=0.07f)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.loop=true;line.positionCount=80;line.widthMultiplier=width;
            line.sharedMaterial=Material(color,true);line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            for(int i=0;i<80;i++){float a=i*Mathf.PI*2/80;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}return go;
        }
        public GameObject Cone(string name,Transform parent,Vector3 pos,float radius,float height,Color color,int sides=8)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;
            var mesh=new Mesh{name=name};var verts=new List<Vector3>();var tris=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int v=verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));verts.Add(Vector3.up*height);verts.Add(new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius));
                tris.Add(v);tris.Add(v+1);tris.Add(v+2);
            }
            mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(color);return go;
        }
        public GameObject Rock(string name,Transform parent,Vector3 pos,Vector3 scale,Color color,int seed)
        {
            const int sides=9,rings=4;var random=new System.Random(seed);var points=new Vector3[rings,sides];
            float[] heights={-.46f,-.28f,.12f,.43f};float[] radii={.62f,.96f,1f,.67f};
            for(int ring=0;ring<rings;ring++)for(int side=0;side<sides;side++)
            {
                float angle=(side+(ring%2)*.13f)*Mathf.PI*2/sides;
                float radius=radii[ring]*(.84f+(float)random.NextDouble()*.28f);
                points[ring,side]=new Vector3(Mathf.Cos(angle)*radius,heights[ring]+((float)random.NextDouble()-.5f)*.08f,Mathf.Sin(angle)*radius);
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int ring=0;ring<rings-1;ring++)for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;AddFace(vertices,triangles,points[ring,side],points[ring+1,side],points[ring,next]);
                AddFace(vertices,triangles,points[ring,next],points[ring+1,side],points[ring+1,next]);
            }
            var top=new Vector3(((float)random.NextDouble()-.5f)*.18f,.57f,((float)random.NextDouble()-.5f)*.18f);
            var bottom=new Vector3(0,-.48f,0);
            for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;AddFace(vertices,triangles,points[rings-1,side],top,points[rings-1,next]);
                AddFace(vertices,triangles,points[0,next],bottom,points[0,side]);
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(color);return go;
        }
        static void AddFace(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c)
        {
            int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(at);triangles.Add(at+1);triangles.Add(at+2);
        }
        public void Branch(Transform parent,Vector3 from,Vector3 to,float width,Color color)
        {
            var delta=to-from;var go=Shape("Branch",PrimitiveType.Cylinder,parent,(from+to)/2,new Vector3(width,delta.magnitude/2,width),color);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
        }
    }
}
