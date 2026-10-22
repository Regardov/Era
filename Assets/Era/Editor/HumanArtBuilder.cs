using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Era.Editor
{
    public static class HumanArtBuilder
    {
        const string Marker="Assets/Era/Generated/Humans-v12.txt";
        sealed class Part {public Mesh mesh;public Transform transform;public SkinnedMeshRenderer skin;public MeshFilter filter;public Vector3[] points;public bool head,halo;}
        public static void Prepare()
        {
            if(File.Exists(Marker))return;
            const string templates="Assets/Era/Art/KayKit/HumanTemplates";Directory.CreateDirectory(templates);AssetDatabase.Refresh();
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,AssetDatabase.GenerateUniqueAssetPath("Assets/Era/Generated/HumanSurfaces-v12.asset"));
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                string path="Assets/Era/Resources/Characters/"+hero+".prefab",baseline=templates+"/"+hero+".prefab";
                if(!File.Exists(baseline)){var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));PrefabUtility.SaveAsPrefabAsset(original,baseline);Object.DestroyImmediate(original);}
                var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(baseline));actor.name=hero.ToString();
                try
                {
                    foreach(var t in actor.GetComponentsInChildren<Transform>(true))if(t.name=="Ivory shoulder mantle")t.gameObject.SetActive(false);
                    var parts=new List<Part>();var boneSet=new HashSet<Transform>();
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        foreach(var b in skin.bones)for(var t=b;t!=null&&t!=actor.transform;t=t.parent)boneSet.Add(t);
                        parts.Add(new Part{mesh=skin.sharedMesh,transform=skin.transform,skin=skin});
                    }
                    foreach(var filter in actor.GetComponentsInChildren<MeshFilter>(true))parts.Add(new Part{mesh=filter.sharedMesh,transform=filter.transform,filter=filter});
                    foreach(var part in parts)
                    {
                        part.head=part.transform.name.IndexOf("head",StringComparison.OrdinalIgnoreCase)>=0||part.transform.name.IndexOf("hood",StringComparison.OrdinalIgnoreCase)>=0||part.transform.name.IndexOf("hair",StringComparison.OrdinalIgnoreCase)>=0||part.transform.name.IndexOf("hat",StringComparison.OrdinalIgnoreCase)>=0;
                        part.halo=part.transform.name.Contains("Halo");part.points=part.mesh.vertices;
                        var skinWeights=part.mesh.boneWeights;
                        for(int i=0;i<part.points.Length;i++)
                        {
                            Vector3 original=actor.transform.InverseTransformPoint(part.transform.TransformPoint(part.points[i]));var shaped=HumanRigShape.Shape(original,part.head,part.halo);
                            if(part.skin!=null&&skinWeights.Length==part.points.Length)
                            {
                                var weight=skinWeights[i];int[] indices={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] values={weight.weight0,weight.weight1,weight.weight2,weight.weight3};Vector3 shrink=Vector3.zero;
                                for(int j=0;j<4;j++)if(values[j]>0){var bone=part.skin.bones[indices[j]];bool hand=bone.name.StartsWith("hand."),foot=bone.name.StartsWith("foot.");if(hand||foot){var anchor=HumanRigShape.Shape(actor.transform.InverseTransformPoint(bone.position));shrink+=(anchor-shaped)*values[j]*(hand?.42f:.23f);}}
                                shaped+=shrink;
                            }
                            if(original.y<.005f)shaped.y=0;part.points[i]=shaped;
                        }
                    }
                    int Depth(Transform t){int d=0;while(t.parent!=null){d++;t=t.parent;}return d;}
                    var bones=boneSet.OrderBy(Depth).ToArray();var originalLocals=bones.Select(b=>b.localPosition).ToArray();var positions=bones.Select(b=>HumanRigShape.Shape(actor.transform.InverseTransformPoint(b.position),HumanRigShape.HeadBone(b))).ToArray();
                    for(int i=0;i<bones.Length;i++)bones[i].position=actor.transform.TransformPoint(positions[i]);
                    foreach(var part in parts)
                    {
                        for(int i=0;i<part.points.Length;i++)part.points[i]=part.transform.InverseTransformPoint(actor.transform.TransformPoint(part.points[i]));
                        var mesh=Smooth(part.mesh,part.points);mesh.name=hero+" / smooth "+part.transform.name;
                        if(part.skin!=null)
                        {
                            mesh.bindposes=part.skin.bones.Select(b=>b.worldToLocalMatrix*part.transform.localToWorldMatrix).ToArray();part.skin.sharedMesh=mesh;part.skin.localBounds=new Bounds(Vector3.up*1.2f,new Vector3(4,5,4));
                        }
                        else part.filter.sharedMesh=mesh;
                        AssetDatabase.AddObjectToAsset(mesh,library);
                    }
                    foreach(var line in actor.GetComponentsInChildren<LineRenderer>(true))if(line.name.Contains("halo")){line.transform.localPosition=HumanRigShape.Shape(line.transform.localPosition,true,true);line.transform.localScale*=.60f;}
                    var shape=actor.AddComponent<HumanRigShape>();shape.bones=bones;shape.originalLocalPositions=originalLocals;
                    PrefabUtility.SaveAsPrefabAsset(actor,path);
                }
                finally{Object.DestroyImmediate(actor);}
            }
            File.WriteAllText(Marker,"Three adult-proportioned heroes; two surface subdivisions, seam-welded smooth normals, original skeletal clips retained.");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static BoneWeight Blend(BoneWeight a,BoneWeight b)
        {
            var values=new Dictionary<int,float>();
            void Add(int bone,float weight){if(weight>0)values[bone]=(values.TryGetValue(bone,out var old)?old:0)+weight*.5f;}
            Add(a.boneIndex0,a.weight0);Add(a.boneIndex1,a.weight1);Add(a.boneIndex2,a.weight2);Add(a.boneIndex3,a.weight3);Add(b.boneIndex0,b.weight0);Add(b.boneIndex1,b.weight1);Add(b.boneIndex2,b.weight2);Add(b.boneIndex3,b.weight3);
            var top=values.OrderByDescending(p=>p.Value).Take(4).ToArray();float total=top.Sum(p=>p.Value);var result=new BoneWeight();
            if(top.Length>0){result.boneIndex0=top[0].Key;result.weight0=top[0].Value/total;}if(top.Length>1){result.boneIndex1=top[1].Key;result.weight1=top[1].Value/total;}if(top.Length>2){result.boneIndex2=top[2].Key;result.weight2=top[2].Value/total;}if(top.Length>3){result.boneIndex3=top[3].Key;result.weight3=top[3].Value/total;}return result;
        }
        static Mesh Smooth(Mesh source,Vector3[] points)
        {
            var v=new List<Vector3>(points);var uv=new List<Vector2>(source.uv.Length==points.Length?source.uv:new Vector2[points.Length]);var weights=new List<BoneWeight>(source.boneWeights.Length==points.Length?source.boneWeights:new BoneWeight[points.Length]);var tris=source.triangles;
            for(int pass=0;pass<2;pass++)
            {
                var mids=new Dictionary<ulong,int>();var indices=new List<int>(tris.Length*4);
                int Mid(int a,int b){ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(mids.TryGetValue(key,out int found))return found;int index=v.Count;v.Add((v[a]+v[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);weights.Add(Blend(weights[a],weights[b]));mids[key]=index;return index;}
                for(int i=0;i<tris.Length;i+=3){int a=tris[i],b=tris[i+1],c=tris[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);indices.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}tris=indices.ToArray();
            }
            var groups=new Dictionary<Vector3Int,int>();var group=new int[v.Count];var centers=new List<Vector3>();
            for(int i=0;i<v.Count;i++){var key=Vector3Int.RoundToInt(v[i]*10000);if(!groups.TryGetValue(key,out int g)){g=centers.Count;groups[key]=g;centers.Add(v[i]);}group[i]=g;}
            var neighbors=new HashSet<int>[centers.Count];for(int i=0;i<neighbors.Length;i++)neighbors[i]=new HashSet<int>();
            void Edge(int a,int b){a=group[a];b=group[b];if(a!=b){neighbors[a].Add(b);neighbors[b].Add(a);}}
            for(int i=0;i<tris.Length;i+=3){Edge(tris[i],tris[i+1]);Edge(tris[i+1],tris[i+2]);Edge(tris[i+2],tris[i]);}
            var relaxed=centers.ToArray();for(int i=0;i<relaxed.Length;i++){if(neighbors[i].Count<3)continue;Vector3 average=Vector3.zero;foreach(int n in neighbors[i])average+=centers[n];relaxed[i]=Vector3.Lerp(centers[i],average/neighbors[i].Count,.32f);}
            for(int i=0;i<v.Count;i++)v[i]=relaxed[group[i]];
            var mesh=new Mesh{indexFormat=v.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.boneWeights=weights.ToArray();mesh.RecalculateNormals();
            var normals=mesh.normals;var sums=new Vector3[centers.Count];for(int i=0;i<normals.Length;i++)sums[group[i]]+=normals[i];for(int i=0;i<normals.Length;i++)normals[i]=sums[group[i]].normalized;mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
}
