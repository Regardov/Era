using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class RoguelikeArtBuilder
    {
        const string Marker="Assets/Era/Generated/RoguelikeActors-v13.txt";
        public static void Prepare()
        {
            if(File.Exists(Marker))return;
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,"Assets/Era/Generated/RoguelikeActors-v13.asset");
            var parent=new GameObject("Creature authoring");var v=new VisualFactory();
            try
            {
                foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                {
                    if(kind<EnemyKind.Skeleton&&kind!=EnemyKind.Brute)continue;
                    var m=RoguelikeCreatureArt.Create(v,parent.transform,kind);
                    Compact(m);
                    foreach(var f in m.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))AssetDatabase.AddObjectToAsset(f.sharedMesh,library);
                    foreach(var r in m.GetComponentsInChildren<Renderer>())foreach(var mat in r.sharedMaterials)if(mat!=null&&!AssetDatabase.Contains(mat))AssetDatabase.AddObjectToAsset(mat,library);
                    PrefabUtility.SaveAsPrefabAsset(m.gameObject,"Assets/Era/Resources/Creatures/"+kind+".prefab");UnityEngine.Object.DestroyImmediate(m.gameObject);
                }
                File.WriteAllText(Marker,"Original low-poly undead anatomy, layered stone/crystal golems, gel slimes, articulated ogres and flying wings. Flat normals; 12-20 radial sides and 6-12 rings.");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            }
            finally{UnityEngine.Object.DestroyImmediate(parent);}
        }
        static void Compact(CreatureMotion motion)
        {
            var groups=new Dictionary<Transform,Dictionary<Material,List<CombineInstance>>>();
            var filters=motion.GetComponentsInChildren<MeshFilter>();
            foreach(var f in filters)
            {
                var renderer=f.GetComponent<MeshRenderer>();if(renderer==null)continue;
                Transform joint=motion.body;for(var t=f.transform;t!=null&&t!=motion.transform;t=t.parent)if(motion.limbs.Contains(t)){joint=t;break;}
                if(!groups.TryGetValue(joint,out var materials)){materials=new Dictionary<Material,List<CombineInstance>>();groups[joint]=materials;}
                if(!materials.TryGetValue(renderer.sharedMaterial,out var meshes)){meshes=new List<CombineInstance>();materials[renderer.sharedMaterial]=meshes;}
                meshes.Add(new CombineInstance{mesh=f.sharedMesh,transform=joint.worldToLocalMatrix*f.transform.localToWorldMatrix});
            }
            foreach(var f in filters){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            foreach(var joint in groups)foreach(var mat in joint.Value)
            {
                var go=new GameObject("Batched faceted surfaces");go.transform.SetParent(joint.Key,false);var mesh=new Mesh{name=motion.kind+" / "+joint.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(mat.Value.ToArray(),true,true);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat.Key;
            }
        }
    }
}
