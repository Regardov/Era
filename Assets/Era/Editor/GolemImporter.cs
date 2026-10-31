using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class GolemImporter
    {
        const string Source="Assets/Era/Art/Creatures/StoneGolem/Golem.era-mesh.json",Prefab="Assets/Era/Resources/Creatures/StoneGolem.prefab",Library="Assets/Era/Art/Creatures/StoneGolem/GolemMeshes.asset";
        [Serializable]sealed class Joint{public string name,parent;public float[] position;}
        [Serializable]sealed class Part{public string name,joint;public float[] vertices,normals,colors;}
        [Serializable]sealed class Model{public string source_sha256;public int triangles;public float height;public Joint[] joints;public Part[] parts;}
        static Vector3 V(float[] v,int i=0)=>new Vector3(v[i],v[i+1],v[i+2]);
        public static void Prepare()
        {
            string signature;using(var sha=SHA256.Create())signature=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Source))).Replace("-","").ToLowerInvariant();
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)?.GetComponent<GolemMotion>();if(existing!=null&&existing.sourceHash==signature)return;
            var data=JsonUtility.FromJson<Model>(File.ReadAllText(Source));if(data.triangles!=2880||data.joints.Length!=15)throw new Exception("Unexpected local golem source");
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var motion=root.GetComponent<CreatureMotion>();if(motion.skeletal!=null)throw new Exception("Do not replace an unreviewed skeletal controller");
                Object.DestroyImmediate(motion.body.gameObject);motion.limbs.Clear();
                var body=new GameObject("Golem V2 / articulated stones").transform;body.SetParent(root.transform,false);motion.body=body;motion.baseScale=Vector3.one;motion.basePosition=Vector3.zero;
                var actor=root.GetComponent<GolemMotion>()??root.AddComponent<GolemMotion>();
                var joints=new Dictionary<string,Transform>();foreach(var j in data.joints)
                {
                    var t=new GameObject(j.name).transform;t.SetParent(string.IsNullOrEmpty(j.parent)?body:joints[j.parent],false);
                    t.position=root.transform.TransformPoint(V(j.position));joints.Add(j.name,t);
                }
                var library=AssetDatabase.LoadAssetAtPath<WorldAssetLibrary>(Library);if(library==null){library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,Library);}
                foreach(var old in AssetDatabase.LoadAllAssetsAtPath(Library))if(old!=library)Object.DestroyImmediate(old,true);
                var material=new Material(Shader.Find("Era/Character Surface")){name="Golem / authored stone, moss and amber palette",color=Color.white};AssetDatabase.AddObjectToAsset(material,library);
                foreach(var group in data.parts.GroupBy(p=>p.joint))
                {
                    var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
                    foreach(var p in group)for(int i=0;i<p.vertices.Length/3;i++){vertices.Add(V(p.vertices,i*3));normals.Add(V(p.normals,i*3));colors.Add(new Color(p.colors[i*4],p.colors[i*4+1],p.colors[i*4+2],1));}
                    var mesh=new Mesh{name="Golem / "+group.Key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(1,Enumerable.Repeat(new Vector2(.08f,0),vertices.Count).ToList());mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateBounds();AssetDatabase.AddObjectToAsset(mesh,library);
                    var go=new GameObject("Stone surface / "+group.Key);go.transform.SetParent(joints[group.Key],false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                actor.joints=joints.Values.ToArray();actor.sourceHash=signature;actor.sourceTriangles=data.triangles;motion.golem=actor;
                foreach(string n in new[]{"UpperArm_L","UpperArm_R","Thigh_L","Thigh_R","Torso","Head"})motion.limbs.Add(joints[n]);
                ActorBodyCollider.Attach(root).Present(new EnemyState{kind=EnemyKind.StoneGolem,hp=100});
                var authored=root.GetComponent<AuthoredCharacter>()??root.AddComponent<AuthoredCharacter>();authored.model="StoneGolem";authored.version=29;authored.sourceHash=data.source_sha256;authored.triangles=data.triangles;authored.details=data.parts.Select(p=>p.name).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();Debug.Log("ERA_GOLEM_IMPORTED 62 source pieces / 2880 triangles / 15 articulated render joints");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
