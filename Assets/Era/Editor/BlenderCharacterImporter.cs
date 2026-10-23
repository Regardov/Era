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
        [Serializable]sealed class Model{public string name;public string[] bones;public float[] bindPositions,positions,colors,weights;public int[] joints;public Part[] parts;}
        static string Hash(string path){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static string PrefabPath(string name)=>"Assets/Era/Resources/"+(name=="Stalker"||name=="Wisp"?"Creatures/":name=="Wayfarer"||name=="DuskMage"?"Characters/Library/":"Characters/")+name+".prefab";
        [MenuItem("ERA/Import Blender character workshop")]
        public static void Prepare()
        {
            string signature="Blender character workshop v16 / "+string.Join(" / ",Models.Select(n=>Hash(Input+n+".era-mesh.json")));
            if(File.Exists(Marker)&&File.ReadAllText(Marker).Trim()==signature)return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(File.Exists(Library))AssetDatabase.DeleteAsset(Library);
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,Library);
            var material=new Material(Shader.Find("Era/Faceted Realm")){name="Blender / matte linen, leather, skin and enamel"};material.SetFloat("_Cull",0);AssetDatabase.AddObjectToAsset(material,library);
            Directory.CreateDirectory("Assets/Era/Resources/Characters/Library");
            foreach(string name in Models)
            {
                string file=Input+name+".era-mesh.json";var data=JsonUtility.FromJson<Model>(File.ReadAllText(file));
                string source=PrefabPath(name=="Wayfarer"?"Lumen":name=="DuskMage"?"Echo":name);
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
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))Object.DestroyImmediate(skin);
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))Object.DestroyImmediate(renderer);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
                    int count=data.positions.Length/3;
                    if(count==0||count%3!=0||data.colors.Length!=count*4||data.joints.Length!=count*2||data.weights.Length!=count*2)throw new Exception("Invalid Blender mesh arrays: "+name);
                    var vertices=new Vector3[count];var colors=new Color[count];var weights=new BoneWeight[count];var triangles=new int[count];
                    for(int i=0;i<count;i++)
                    {
                        vertices[i]=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);
                        colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],data.colors[i*4+3]);
                        int a=data.joints[i*2],b=data.joints[i*2+1];float w=data.weights[i*2],v=data.weights[i*2+1];
                        if(a<0||a>=bones.Length||b<0||b>=bones.Length||Mathf.Abs(w+v-1)>.001f)throw new Exception("Invalid Blender skin weight: "+name+" / "+i);
                        weights[i]=new BoneWeight{boneIndex0=a,boneIndex1=b,weight0=w,weight1=v};triangles[i]=i;
                    }
                    var mesh=new Mesh{name=name+" / Blender authored flat surfaces",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                    AssetDatabase.AddObjectToAsset(mesh,library);
                    var part=new GameObject("Blender character / "+name);part.transform.SetParent(root.transform,false);
                    var surface=part.AddComponent<SkinnedMeshRenderer>();surface.sharedMesh=mesh;surface.sharedMaterial=material;surface.bones=bones;surface.rootBone=rig.hips;surface.localBounds=new Bounds(Vector3.up*1.4f,new Vector3(4,5,4));
                    var metadata=root.GetComponent<AuthoredCharacter>()??root.AddComponent<AuthoredCharacter>();metadata.model=name;metadata.sourceHash=Hash(file);metadata.triangles=count/3;metadata.details=data.parts.Select(p=>p.name).ToArray();
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
