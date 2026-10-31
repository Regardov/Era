using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class WorldTreeCollisionBuilder
    {
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static Vector2[] Hull(List<Vector2> points)
        {
            var p=points.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();if(p.Length<3)return Array.Empty<Vector2>();
            var h=new List<Vector2>();foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],v)<=.00001f)h.RemoveAt(h.Count-1);h.Add(v);}
            int lower=h.Count;for(int i=p.Length-2;i>=0;i--){while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p[i])<=.00001f)h.RemoveAt(h.Count-1);h.Add(p[i]);}
            h.RemoveAt(h.Count-1);return h.ToArray();
        }
        static List<Vector3> Clip(List<Vector3> points,float height,bool above)
        {
            var result=new List<Vector3>();for(int i=0;i<points.Count;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Count];bool ia=above?a.y>=height:a.y<=height,ib=above?b.y>=height:b.y<=height;
                if(ia)result.Add(a);if(ia!=ib)result.Add(Vector3.Lerp(a,b,(height-a.y)/(b.y-a.y)));
            }
            return result;
        }
        public static void Bake(GameObject tree)
        {
            var output=new List<WorldTreeCollisionData.Hull>();var lod=tree.transform.Find("Tree LOD0");
            foreach(var f in lod.GetComponentsInChildren<MeshFilter>())
            {
                bool root=f.name.StartsWith("YGG_Root_");if(!root&&!f.name.Contains("AncientTrunk")&&!f.name.Contains("IntertwinedLeaders"))continue;
                var mesh=f.sharedMesh;var raw=mesh.vertices;var vertices=new Vector3[raw.Length];
                var weld=new Dictionary<Vector3Int,int>();var ids=new int[raw.Length];var parent=new int[raw.Length];
                int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
                void Join(int x,int y){parent[Find(x)]=Find(y);}
                for(int i=0;i<raw.Length;i++)
                {
                    parent[i]=i;var p=f.transform.TransformPoint(raw[i]);var key=Vector3Int.RoundToInt(p*10000);
                    if(weld.TryGetValue(key,out int id))ids[i]=id;else{weld[key]=i;ids[i]=i;}
                    if(root)p.y=p.y*WorldTreeLayout.RootCompression(new Vector2(p.x,p.z).magnitude)-.035f;
                    vertices[i]=p;
                }
                var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3){Join(ids[tris[i]],ids[tris[i+1]]);Join(ids[tris[i]],ids[tris[i+2]]);}
                var pieces=new Dictionary<int,List<Vector2>>();
                for(int i=0;i<tris.Length;i+=3)
                {
                    var cut=Clip(Clip(new List<Vector3>{vertices[tris[i]],vertices[tris[i+1]],vertices[tris[i+2]]},.30f,true),2.3f,false);
                    if(cut.Count==0)continue;int id=Find(ids[tris[i]]);if(!pieces.TryGetValue(id,out var points)){points=new List<Vector2>();pieces[id]=points;}
                    foreach(var p in cut)points.Add(new Vector2(p.x,p.z));
                }
                foreach(var piece in pieces)
                {
                    var points=Hull(piece.Value);if(points.Length<3)continue;float area=0;for(int i=0;i<points.Length;i++)area+=points[i].x*points[(i+1)%points.Length].y-points[i].y*points[(i+1)%points.Length].x;
                    if(area<.08f)continue;
                    output.Add(new WorldTreeCollisionData.Hull{source=f.name+" / "+piece.Key,points=points,min=new Vector2(points.Min(v=>v.x),points.Min(v=>v.y)),max=new Vector2(points.Max(v=>v.x),points.Max(v=>v.y))});
                }
            }
            const string path="Assets/Era/Resources/Environment/YggdrasilCollision.asset";
            var data=AssetDatabase.LoadAssetAtPath<WorldTreeCollisionData>(path);if(data==null){data=ScriptableObject.CreateInstance<WorldTreeCollisionData>();AssetDatabase.CreateAsset(data,path);}
            data.hulls=output.ToArray();data.height=WorldTreeLayout.Height;data.crown=WorldTreeLayout.CrownDiameter;EditorUtility.SetDirty(data);WorldTreeLayout.InvalidateCollision();
            if(data.hulls.Length<10)throw new Exception("Incomplete world-tree collision geometry");
            Debug.Log("ERA_TREE_COLLISION_BAKED "+output.Count+" measured trunk/root pieces");
        }
    }
}
