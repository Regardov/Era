using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class BlenderCharacterImporter
    {
        const string Input="Assets/Era/Art/BlenderCharacters/",Library="Assets/Era/Generated/BlenderCharacters-v16.asset",Marker="Assets/Era/Generated/BlenderCharacters-v16.txt";
        public static readonly string[] Models={"Wayfarer","DuskMage","Stalker","Wisp","Ember","Lumen","Echo"};
        [Serializable]sealed class Part{public string name;public int first,count;}
        [Serializable]sealed class Model{public string name;public string[] bones;public float[] bindPositions,positions,normals,surfaces,colors,weights;public int[] joints;public Part[] parts;}
        static string Hash(string path){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static string PrefabPath(string name)=>"Assets/Era/Resources/"+(name=="Stalker"||name=="Wisp"?"Creatures/":name=="Wayfarer"||name=="DuskMage"?"Characters/Library/":"Characters/")+name+".prefab";
        [MenuItem("ERA/Import Blender character workshop")]
        public static void Prepare()
        {
            string signature="Blender character workshop v19 / smooth closed clothing / grips / "+string.Join(" / ",Models.Select(n=>Hash(Input+n+".era-mesh.json")));
            if(File.Exists(Marker)&&File.ReadAllText(Marker).Trim()==signature)return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(File.Exists(Library))AssetDatabase.DeleteAsset(Library);
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,Library);
            var material=new Material(Shader.Find("Era/Character Surface")){name="Blender / smooth cloth, skin, hair and hard metal"};AssetDatabase.AddObjectToAsset(material,library);
            Directory.CreateDirectory("Assets/Era/Resources/Characters/Library");
            foreach(string name in Models)
            {
                string file=Input+name+".era-mesh.json";var data=JsonUtility.FromJson<Model>(File.ReadAllText(file));
                string source=PrefabPath(name=="Wayfarer"?"Lumen":name=="DuskMage"?"Echo":name);
                if(name=="Wayfarer"&&AssetDatabase.LoadAssetAtPath<GameObject>(source)?.GetComponent<HeroMotion>()?.lumen!=null)source=PrefabPath("Wayfarer");
                if(name=="Lumen"&&AssetDatabase.LoadAssetAtPath<GameObject>(source)?.GetComponent<HeroMotion>()?.lumen!=null)continue;
                if(name=="Ember"&&AssetDatabase.LoadAssetAtPath<GameObject>(source)?.GetComponent<HeroMotion>()?.archer!=null)continue;
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));root.name=name;
                try
                {
                    var shape=root.GetComponent<HumanRigShape>();if(shape==null)throw new Exception("Missing animated reference rig: "+name);
                    var rig=root.GetComponent<SkeletalActor>();rig.animationPlayer.enabled=false;
                    var all=root.GetComponentsInChildren<Transform>(true);
                    var bones=data.bones.Select(b=>all.FirstOrDefault(t=>t.name==b)??throw new Exception("Missing Blender bind bone "+b)).ToArray();
                    if(data.bindPositions.Length!=bones.Length*3)throw new Exception("Invalid Blender bind positions: "+name);
                    int Depth(Transform bone){int depth=0;for(;bone!=null;bone=bone.parent)depth++;return depth;}
                    foreach(int i in Enumerable.Range(0,bones.Length).OrderBy(i=>Depth(bones[i])))
                        bones[i].position=root.transform.TransformPoint(new Vector3(data.bindPositions[i*3],data.bindPositions[i*3+1],data.bindPositions[i*3+2]));
                    // Animation clips still use their original local translations. HumanRigShape
                    // restores those before animation, then reapplies the new connected bind lengths.
                    shape.shapedLocalPositions=shape.bones.Select(b=>b.localPosition).ToArray();
                    shape.plantFeet=name!="Stalker"&&name!="Wisp";shape.footHeight=.14f;
                    foreach(string side in new[]{"l","r"})
                    {
                        var wrist=rig.Bone("hand."+side);var socket=wrist.Find("ERA palm socket")??new GameObject("ERA palm socket").transform;socket.SetParent(wrist,false);
                        socket.position=wrist.position+root.transform.TransformVector(new Vector3(side=="l"?-.075f:.075f,-.015f,.055f));socket.rotation=root.transform.rotation;
                    }
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))Object.DestroyImmediate(skin);
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))Object.DestroyImmediate(renderer);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
                    int count=data.positions.Length/3;
                    if(count==0||count%3!=0||data.colors.Length!=count*4||data.joints.Length!=count*2||data.weights.Length!=count*2)throw new Exception("Invalid Blender mesh arrays: "+name);
                    if(data.normals==null||data.normals.Length!=count*3||data.surfaces==null||data.surfaces.Length!=count*2)throw new Exception("Missing authored normals/surfaces: "+name);
                    var vertices=new Vector3[count];var normals=new Vector3[count];var surfaces=new Vector2[count];var colors=new Color[count];var weights=new BoneWeight[count];var triangles=new int[count];
                    for(int i=0;i<count;i++)
                    {
                        vertices[i]=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);
                        normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]).normalized;surfaces[i]=new Vector2(data.surfaces[i*2],data.surfaces[i*2+1]);
                        colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],data.colors[i*4+3]);
                        int a=data.joints[i*2],b=data.joints[i*2+1];float w=data.weights[i*2],v=data.weights[i*2+1];
                        if(a<0||a>=bones.Length||b<0||b>=bones.Length||Mathf.Abs(w+v-1)>.001f)throw new Exception("Invalid Blender skin weight: "+name+" / "+i);
                        weights[i]=new BoneWeight{boneIndex0=a,boneIndex1=b,weight0=w,weight1=v};triangles[i]=i;
                    }
                    var mesh=new Mesh{name=name+" / Blender smooth low-poly costume",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.normals=normals;mesh.uv2=surfaces;mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
                    var zeros=new Vector3[count];
                    foreach(string side in new[]{"l","r"})
                    {
                        var delta=new Vector3[count];var center=new Vector3(data.bindPositions[Array.IndexOf(data.bones,"hand."+side)*3],data.bindPositions[Array.IndexOf(data.bones,"hand."+side)*3+1],data.bindPositions[Array.IndexOf(data.bones,"hand."+side)*3+2])+new Vector3(side=="l"?-.075f:.075f,-.015f,.055f);
                        foreach(var detail in data.parts)if(detail.name.StartsWith("Finger "+side)||detail.name.StartsWith("Thumb "+side))for(int i=detail.first;i<detail.first+detail.count;i++){var d=vertices[i]-center;delta[i]=new Vector3(-d.x*.30f,0,-d.z*.30f);}
                        mesh.AddBlendShapeFrame("Grip."+side,100,delta,zeros,zeros);
                    }
                    var stride=new Vector3[count];var crouch=new Vector3[count];
                    foreach(var detail in data.parts)if(detail.name.StartsWith("Coat /")||detail.name.StartsWith("Cape /"))for(int i=detail.first;i<detail.first+detail.count;i++)
                    {float hem=Mathf.Clamp01((1.15f-vertices[i].y)/.95f);stride[i]=new Vector3(vertices[i].x*.10f,0,Mathf.Sign(vertices[i].z)*hem*.09f)*hem;crouch[i]=new Vector3(vertices[i].x*.12f,hem*.12f,Mathf.Sign(vertices[i].z)*.07f)*hem;}
                    mesh.AddBlendShapeFrame("StrideClearance",100,stride,zeros,zeros);mesh.AddBlendShapeFrame("CrouchClearance",100,crouch,zeros,zeros);
                    AssetDatabase.AddObjectToAsset(mesh,library);
                    var part=new GameObject("Blender character / "+name);part.transform.SetParent(root.transform,false);
                    var surface=part.AddComponent<SkinnedMeshRenderer>();surface.sharedMesh=mesh;surface.sharedMaterial=material;surface.bones=bones;surface.rootBone=rig.hips;surface.localBounds=new Bounds(Vector3.up*1.4f,new Vector3(4,5,4));
                    var metadata=root.GetComponent<AuthoredCharacter>()??root.AddComponent<AuthoredCharacter>();metadata.version=19;metadata.model=name;metadata.sourceHash=Hash(file);metadata.triangles=count/3;metadata.details=data.parts.Select(p=>p.name).ToArray();
                    if(root.GetComponent<CostumeMotion>()==null)root.AddComponent<CostumeMotion>();
                    rig.animationPlayer.enabled=true;
                    PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(name));
                    Debug.Log("ERA_BLENDER_IMPORTED "+name+" / "+count/3+" triangles / "+data.parts.Length+" details");
                }
                finally{Object.DestroyImmediate(root);}
            }
            File.WriteAllText(Marker,signature+"\n");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
    }
}
