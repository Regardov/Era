using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Era.Editor
{
    // Focused offline GLB authoring importer for the included, uncompressed CC0 KayKit files.
    // Produces native Unity meshes, bind poses, legacy skeletal clips, materials and prefabs.
    public static class KayKitImporter
    {
        [Serializable]class Document{public Node[] nodes;public MeshDef[] meshes;public Accessor[] accessors;public View[] bufferViews;public Skin[] skins;public Clip[] animations;public Image[] images;}
        [Serializable]class Node{public string name;public int meshPlusOne,skinPlusOne;public int[] children;public float[] translation,rotation,scale;}
        [Serializable]class MeshDef{public string name;public Primitive[] primitives;}
        [Serializable]class Primitive{public Attributes attributes;public int indices;}
        [Serializable]class Attributes{public int POSITION,NORMAL,TEXCOORD_0,JOINTS_0,WEIGHTS_0;}
        [Serializable]class Accessor{public int bufferView,byteOffset,componentType,count;public string type;}
        [Serializable]class View{public int byteOffset,byteLength,byteStride;}
        [Serializable]class Skin{public int[] joints;public int inverseBindMatrices;}
        [Serializable]class Image{public int bufferView;public string name;}
        [Serializable]class Clip{public string name;public Channel[] channels;public Sampler[] samplers;}
        [Serializable]class Channel{public int sampler;public Target target;}
        [Serializable]class Target{public int node;public string path;}
        [Serializable]class Sampler{public int input,output;public string interpolation;}
        static readonly HashSet<string> UsedClips=new HashSet<string>{"Idle","Walking_A","Running_A","Jump_Start","Jump_Idle","Jump_Land","Dodge_Forward","Death_A","Death_A_Pose","Hit_A","Interact","PickUp","Spellcast_Raise","Spellcast_Shoot","Spellcasting","1H_Melee_Attack_Chop","1H_Melee_Attack_Stab","2H_Ranged_Shoot","2H_Ranged_Aiming","Skeletons_Idle","Skeletons_Walking","Skeletons_Running","Unarmed_Melee_Attack_Punch_A"};
        const string Root="Assets/Era/Art/KayKit";
        [MenuItem("ERA/Import textured KayKit actors")]
        public static void Prepare()
        {
            if(File.Exists(Root+"/Imported-v1.txt")){RepairAttachments();return;}
            string[] models={"Rogue_Hooded","Mage","Skeleton_Minion","Skeleton_Warrior","Skeleton_Rogue","Skeleton_Mage"};
            string[] paths={"Characters/Ember","Characters/Echo","Creatures/Wisp","Creatures/Brute","Creatures/Stalker","Creatures/Warden"};
            Directory.CreateDirectory(Root+"/Textures");Directory.CreateDirectory(Root+"/Native");
            for(int i=0;i<models.Length;i++)Import(models[i],paths[i],i);
            File.WriteAllText(Root+"/Imported-v1.txt","KayKit CC0 native import v1. See LICENSE and Docs/THIRD-PARTY.md.");RepairAttachments();AssetDatabase.Refresh();
        }
        static bool Held(Transform item)
        {for(var parent=item.parent;parent!=null;parent=parent.parent)if(parent.name=="handslot.r"||parent.name=="handslot.l")return true;return false;}
        static void RepairAttachments()
        {
            if(File.Exists(Root+"/Attachments-v2.txt"))return;
            foreach(string name in new[]{"Characters/Ember","Characters/Echo","Creatures/Wisp","Creatures/Brute","Creatures/Stalker","Creatures/Warden"})
            {
                string path="Assets/Era/Resources/"+name+".prefab";var instance=PrefabUtility.LoadPrefabContents(path);
                try{foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))renderer.gameObject.SetActive(!Held(renderer.transform));PrefabUtility.SaveAsPrefabAsset(instance,path);}
                finally{PrefabUtility.UnloadPrefabContents(instance);}
            }
            File.WriteAllText(Root+"/Attachments-v2.txt","Keep authored hats, cloaks and body attachments. Hide only included handheld accessories.");
        }
        static void Import(string model,string prefabPath,int actor)
        {
            byte[] bytes=File.ReadAllBytes(Root+"/Source/"+model+".glb");int jsonLength=BitConverter.ToInt32(bytes,12),binStart=28+jsonLength;
            if(BitConverter.ToUInt32(bytes,0)!=0x46546c67)throw new Exception("Invalid GLB "+model);
            string json=Encoding.UTF8.GetString(bytes,20,jsonLength);
            json=Regex.Replace(json,"\"(mesh|skin)\"\\s*:\\s*(\\d+)",m=>"\""+m.Groups[1].Value+"PlusOne\":"+(int.Parse(m.Groups[2].Value)+1));
            var doc=JsonUtility.FromJson<Document>(json);var data=new Data(doc,bytes,binStart);
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();string libPath=Root+"/Native/"+model+".asset";AssetDatabase.CreateAsset(library,libPath);
            var prefab=new GameObject(model);var nodes=new Transform[doc.nodes.Length];
            try
            {
                foreach(var image in doc.images)
                {
                    var view=doc.bufferViews[image.bufferView];var imageBytes=new byte[view.byteLength];Array.Copy(bytes,binStart+view.byteOffset,imageBytes,0,view.byteLength);
                    string texturePath=Root+"/Textures/"+image.name+".png";File.WriteAllBytes(texturePath,imageBytes);AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                }
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+doc.images[0].name+".png");
                var material=new Material(Shader.Find("Standard")){name=model+" / Painted atlas",mainTexture=texture};material.SetFloat("_Glossiness",.22f);AssetDatabase.AddObjectToAsset(material,library);
                for(int n=0;n<nodes.Length;n++){nodes[n]=new GameObject(doc.nodes[n].name).transform;nodes[n].SetParent(prefab.transform,false);}
                for(int n=0;n<nodes.Length;n++)if(doc.nodes[n].children!=null)foreach(int child in doc.nodes[n].children)nodes[child].SetParent(nodes[n],false);
                for(int n=0;n<nodes.Length;n++)
                {
                    var item=doc.nodes[n];var t=nodes[n];t.localPosition=V(item.translation);t.localRotation=Q(item.rotation);t.localScale=item.scale==null?Vector3.one:new Vector3(item.scale[0],item.scale[1],item.scale[2]);
                }
                for(int n=0;n<nodes.Length;n++)
                {
                    var item=doc.nodes[n];if(item.meshPlusOne==0)continue;
                    var definition=doc.meshes[item.meshPlusOne-1];int part=0;
                    foreach(var primitive in definition.primitives)
                    {
                        var attributes=primitive.attributes;var positions=data.Floats(attributes.POSITION,3);var normals=data.Floats(attributes.NORMAL,3);var uv=data.Floats(attributes.TEXCOORD_0,2);int count=positions.Length/3;
                        var vertices=new Vector3[count];var ns=new Vector3[count];var coords=new Vector2[count];
                        for(int v=0;v<count;v++){vertices[v]=new Vector3(positions[v*3],positions[v*3+1],-positions[v*3+2]);ns[v]=new Vector3(normals[v*3],normals[v*3+1],-normals[v*3+2]);coords[v]=new Vector2(uv[v*2],1-uv[v*2+1]);}
                        var indices=data.Ints(primitive.indices,1);for(int k=0;k<indices.Length;k+=3){int swap=indices[k+1];indices[k+1]=indices[k+2];indices[k+2]=swap;}
                        var mesh=new Mesh{name=item.name+" mesh",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=ns;mesh.uv=coords;mesh.triangles=indices;
                        var target=part++==0?nodes[n].gameObject:new GameObject("Mesh part");if(target!=nodes[n].gameObject)target.transform.SetParent(nodes[n],false);
                        if(item.skinPlusOne>0)
                        {
                            var skin=doc.skins[item.skinPlusOne-1];var matrices=data.Floats(skin.inverseBindMatrices,16);var bind=new Matrix4x4[skin.joints.Length];var bones=new Transform[skin.joints.Length];
                            var reflection=Matrix4x4.Scale(new Vector3(1,1,-1));for(int b=0;b<bones.Length;b++){bones[b]=nodes[skin.joints[b]];var matrix=new Matrix4x4();for(int row=0;row<4;row++)for(int col=0;col<4;col++)matrix[row,col]=matrices[b*16+col*4+row];bind[b]=reflection*matrix*reflection;}
                            var joints=data.Ints(attributes.JOINTS_0,4);var weights=data.Floats(attributes.WEIGHTS_0,4);var bw=new BoneWeight[count];
                            for(int v=0;v<count;v++)bw[v]=new BoneWeight{boneIndex0=joints[v*4],boneIndex1=joints[v*4+1],boneIndex2=joints[v*4+2],boneIndex3=joints[v*4+3],weight0=weights[v*4],weight1=weights[v*4+1],weight2=weights[v*4+2],weight3=weights[v*4+3]};
                            mesh.bindposes=bind;mesh.boneWeights=bw;var renderer=target.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=bones[0];renderer.sharedMaterial=material;renderer.updateWhenOffscreen=true;renderer.localBounds=new Bounds(Vector3.up,new Vector3(5,5,5));
                        }
                        else
                        {target.AddComponent<MeshFilter>().sharedMesh=mesh;target.AddComponent<MeshRenderer>().sharedMaterial=material;if(Held(target.transform))target.SetActive(false);}
                        mesh.RecalculateBounds();AssetDatabase.AddObjectToAsset(mesh,library);
                    }
                }
                var animation=prefab.AddComponent<Animation>();animation.playAutomatically=false;animation.cullingType=AnimationCullingType.AlwaysAnimate;
                foreach(var source in doc.animations)
                {
                    if(!UsedClips.Contains(source.name))continue;var clip=new AnimationClip{name=source.name,legacy=true,frameRate=30};
                    bool loop=source.name.Contains("Idle")||source.name.Contains("Walking")||source.name.Contains("Running")||source.name=="Spellcasting";clip.wrapMode=loop?WrapMode.Loop:WrapMode.ClampForever;
                    foreach(var channel in source.channels)
                    {
                        var sampler=source.samplers[channel.sampler];var times=data.Floats(sampler.input,1);int components=channel.target.path=="rotation"?4:3;var values=data.Floats(sampler.output,components);string path=AnimationUtility.CalculateTransformPath(nodes[channel.target.node],prefab.transform);
                        string property=channel.target.path=="rotation"?"localRotation":channel.target.path=="translation"?"localPosition":"localScale";
                        for(int axis=0;axis<components;axis++)
                        {
                            var keys=new Keyframe[times.Length];for(int key=0;key<times.Length;key++)
                            {float value=values[key*components+axis];if(channel.target.path=="translation"&&axis==2||channel.target.path=="rotation"&&axis<2)value=-value;keys[key]=new Keyframe(times[key],value);}
                            var curve=new AnimationCurve(keys);for(int key=0;key<keys.Length;key++){AnimationUtility.SetKeyLeftTangentMode(curve,key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,key,AnimationUtility.TangentMode.Linear);}
                            clip.SetCurve(path,typeof(Transform),property+"."+"xyzw"[axis],curve);
                        }
                    }
                    clip.EnsureQuaternionContinuity();AssetDatabase.AddObjectToAsset(clip,library);animation.AddClip(clip,clip.name);if(clip.name=="Idle")animation.clip=clip;
                }
                // Model-forward in glTF is -Z after handedness conversion; gameplay-forward is +Z.
                foreach(Transform child in prefab.transform)child.localRotation=Quaternion.Euler(0,180,0)*child.localRotation;
                var skeletal=prefab.AddComponent<SkeletalActor>();skeletal.animationPlayer=animation;skeletal.Bind();
                if(actor<2){var hero=prefab.AddComponent<HeroMotion>();hero.BindImported((Hero)actor,skeletal);}
                else
                {
                    var creature=prefab.AddComponent<CreatureMotion>();creature.kind=(EnemyKind)(actor-2);creature.skeletal=skeletal;creature.body=prefab.transform.Find("Rig");creature.baseScale=Vector3.one;
                    foreach(var t in nodes)if(t.name.StartsWith("upperleg")||t.name.StartsWith("upperarm"))creature.limbs.Add(t);
                    float scale=actor==3?1.5f:actor==5?2.6f:1.0f;prefab.transform.localScale=Vector3.one*scale;
                }
                string destination="Assets/Era/Resources/"+prefabPath+".prefab";PrefabUtility.SaveAsPrefabAsset(prefab,destination);AssetDatabase.SaveAssets();
                Debug.Log("ERA_MODEL_IMPORTED "+model+" / "+animation.GetClipCount()+" skeletal clips / "+prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Length+" skinned meshes");
            }
            finally{UnityEngine.Object.DestroyImmediate(prefab);}
        }
        static Vector3 V(float[] v)=>v==null?Vector3.zero:new Vector3(v[0],v[1],-v[2]);
        static Quaternion Q(float[] q)=>q==null?Quaternion.identity:new Quaternion(-q[0],-q[1],q[2],q[3]);
        sealed class Data
        {
            readonly Document doc;readonly byte[] bytes;readonly int start;
            public Data(Document d,byte[] b,int s){doc=d;bytes=b;start=s;}
            float Read(Accessor a,int index,int component,int count)
            {
                var view=doc.bufferViews[a.bufferView];int size=a.componentType==5126||a.componentType==5125?4:a.componentType==5123?2:1;int at=start+view.byteOffset+a.byteOffset+index*(view.byteStride>0?view.byteStride:size*count)+component*size;
                switch(a.componentType){case 5126:return BitConverter.ToSingle(bytes,at);case 5125:return BitConverter.ToUInt32(bytes,at);case 5123:return BitConverter.ToUInt16(bytes,at);case 5121:return bytes[at];default:throw new Exception("Unsupported GLB component "+a.componentType);}
            }
            public float[] Floats(int accessor,int components){var a=doc.accessors[accessor];var result=new float[a.count*components];for(int i=0;i<a.count;i++)for(int c=0;c<components;c++)result[i*components+c]=Read(a,i,c,components);return result;}
            public int[] Ints(int accessor,int components){var values=Floats(accessor,components);var result=new int[values.Length];for(int i=0;i<result.Length;i++)result[i]=(int)values[i];return result;}
        }
    }
}
