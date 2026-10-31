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
        [Serializable]sealed class Joint{public string name,parent;public float[] position;}
        [Serializable]sealed class Part{public string name,joint;public float[] vertices,normals,colors;}
        [Serializable]sealed class Model{public string source_sha256;public int triangles;public float height;public Joint[] joints;public Part[] parts;}
        static Vector3 V(float[] v,int i=0)=>new Vector3(v[i],v[i+1],v[i+2]);
        public static void Prepare()
        {
            Import(EnemyKind.StoneGolem,"Assets/Era/Art/Creatures/StoneGolem/Golem.era-mesh.json","Assets/Era/Art/Creatures/StoneGolem/GolemMeshes.asset");
            Import(EnemyKind.CrystalGolem,"Assets/Era/Art/Creatures/CrystalGolem/CrystalGolem.era-mesh.json","Assets/Era/Art/Creatures/CrystalGolem/CrystalGolemMeshes.asset");
            Import(EnemyKind.Bat,"Assets/Era/Art/Creatures/BloodBat/BloodBat.era-mesh.json","Assets/Era/Art/Creatures/BloodBat/BloodBatMeshes.asset");
        }
        static void Import(EnemyKind kind,string source,string libraryPath)
        {
            string prefab="Assets/Era/Resources/Creatures/"+kind+".prefab";
            string signature;using(var sha=SHA256.Create())signature=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant();
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);var oldGolem=existing?.GetComponent<GolemMotion>();var oldBat=existing?.GetComponent<BatMotion>();
            if((kind==EnemyKind.Bat?oldBat?.sourceHash:oldGolem?.sourceHash)==signature)return;
            var data=JsonUtility.FromJson<Model>(File.ReadAllText(source));int expected=kind==EnemyKind.Bat?9:15;
            if(data.triangles<1000||data.triangles>6000||data.joints.Length!=expected)throw new Exception("Invalid articulated creature: "+kind);
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var motion=root.GetComponent<CreatureMotion>();if(motion.skeletal!=null)throw new Exception("Unreviewed skeletal creature: "+kind);
                Object.DestroyImmediate(motion.body.gameObject);motion.limbs.Clear();
                var body=new GameObject(kind+" / library articulated model").transform;body.SetParent(root.transform,false);motion.body=body;motion.baseScale=Vector3.one;motion.basePosition=Vector3.zero;
                var joints=new Dictionary<string,Transform>();foreach(var j in data.joints){var t=new GameObject(j.name).transform;t.SetParent(string.IsNullOrEmpty(j.parent)?body:joints[j.parent],false);t.position=root.transform.TransformPoint(V(j.position));joints.Add(j.name,t);}
                var library=AssetDatabase.LoadAssetAtPath<WorldAssetLibrary>(libraryPath);if(library==null){library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}
                foreach(var old in AssetDatabase.LoadAllAssetsAtPath(libraryPath))if(old!=library)Object.DestroyImmediate(old,true);
                var material=new Material(Shader.Find("Era/Character Surface")){name=kind+" / authored palette",color=Color.white,enableInstancing=true};AssetDatabase.AddObjectToAsset(material,library);
                foreach(var group in data.parts.GroupBy(p=>p.joint))
                {
                    var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
                    foreach(var p in group)for(int i=0;i<p.vertices.Length/3;i++){vertices.Add(V(p.vertices,i*3));normals.Add(V(p.normals,i*3));colors.Add(new Color(p.colors[i*4],p.colors[i*4+1],p.colors[i*4+2],1));}
                    var mesh=new Mesh{name=kind+" / "+group.Key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(1,Enumerable.Repeat(new Vector2(kind==EnemyKind.CrystalGolem?.32f:.08f,0),vertices.Count).ToList());mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateBounds();AssetDatabase.AddObjectToAsset(mesh,library);
                    var go=new GameObject("Surface / "+group.Key);go.transform.SetParent(joints[group.Key],false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                if(kind==EnemyKind.Bat)
                {
                    var actor=root.GetComponent<BatMotion>()??root.AddComponent<BatMotion>();actor.joints=joints.Values.ToArray();actor.sourceHash=signature;actor.sourceTriangles=data.triangles;motion.bat=actor;
                    foreach(string n in new[]{"Wing_L","Wing_R","Tip_L","Tip_R","Foot_L","Foot_R"})motion.limbs.Add(joints[n]);
                }
                else
                {
                    var actor=root.GetComponent<GolemMotion>()??root.AddComponent<GolemMotion>();actor.joints=joints.Values.ToArray();actor.sourceHash=signature;actor.sourceTriangles=data.triangles;motion.golem=actor;
                    foreach(string n in new[]{"UpperArm_L","UpperArm_R","Thigh_L","Thigh_R","Torso","Head"})motion.limbs.Add(joints[n]);
                }
                ActorBodyCollider.Attach(root).Present(new EnemyState{kind=kind,hp=100});
                var authored=root.GetComponent<AuthoredCharacter>()??root.AddComponent<AuthoredCharacter>();authored.model=kind.ToString();authored.version=30;authored.sourceHash=data.source_sha256;authored.triangles=data.triangles;authored.details=data.parts.Select(p=>p.name).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,prefab);AssetDatabase.SaveAssets();Debug.Log("ERA_LIBRARY_CREATURE_IMPORTED "+kind+" / "+data.triangles+" triangles / "+data.joints.Length+" joints");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
