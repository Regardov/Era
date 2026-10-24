using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class CharacterBodyBuilder
    {
        const string Marker="Assets/Era/Generated/BodyColliders-v19.txt";
        public static void Prepare()
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");layers.GetArrayElementAtIndex(8).stringValue="ERA Player Body";layers.GetArrayElementAtIndex(9).stringValue="ERA Enemy Body";tags.ApplyModifiedPropertiesWithoutUndo();
            Physics.IgnoreLayerCollision(8,8,true);Physics.IgnoreLayerCollision(8,9,false);Physics.IgnoreLayerCollision(9,9,false);Physics.IgnoreLayerCollision(8,0,false);Physics.IgnoreLayerCollision(9,0,false);
            if(File.Exists(Marker))
            {
                bool ready=true;foreach(string folder in new[]{"Assets/Era/Resources/Characters","Assets/Era/Resources/Creatures","Assets/Era/Resources/Bosses"})foreach(string path in Directory.GetFiles(folder,"*.prefab",SearchOption.AllDirectories))ready&=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ActorBodyCollider>()!=null;
                if(ready)return;
            }
            foreach(string folder in new[]{"Assets/Era/Resources/Characters","Assets/Era/Resources/Creatures","Assets/Era/Resources/Bosses"})foreach(string path in Directory.GetFiles(folder,"*.prefab",SearchOption.AllDirectories))
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body=ActorBodyCollider.Attach(root);var hero=root.GetComponent<HeroMotion>();var mob=root.GetComponent<CreatureMotion>();
                    if(hero!=null)body.Present(new PlayerState());else if(mob!=null)body.Present(new EnemyState{kind=mob.kind,hp=1});
                    var animation=root.GetComponent<Animation>();if(animation!=null)animation.cullingType=AnimationCullingType.AlwaysAnimate;
                    var controller=root.GetComponent<Animator>();if(controller!=null)controller.applyRootMotion=false;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(Marker,"Shared host capsule shapes; kinematic Unity query bodies; enabled player/enemy and enemy/enemy contacts; no root motion.");AssetDatabase.SaveAssets();
        }
        public static void SmoothOrganicSurfaces(CreatureMotion motion)
        {
            if(motion.kind==EnemyKind.StoneGolem||motion.kind==EnemyKind.CrystalGolem||motion.kind==EnemyKind.Gargoyle)return;
            foreach(var filter in motion.GetComponentsInChildren<MeshFilter>())
            {
                string name=filter.gameObject.name.ToLowerInvariant();if(name.Contains("blade")||name.Contains("tooth")||name.Contains("tusk")||name.Contains("fang")||name.Contains("plate")||name.Contains("buckle")||filter.GetComponentInParent<WeaponGripPoints>()!=null)continue;
                var mesh=filter.sharedMesh;if(mesh==null)continue;var vertices=mesh.vertices;var source=mesh.normals;if(source.Length!=vertices.Length){mesh.RecalculateNormals();source=mesh.normals;}var normals=new Vector3[source.Length];
                var groups=new Dictionary<Vector3Int,List<int>>();for(int i=0;i<vertices.Length;i++){var key=Vector3Int.RoundToInt(vertices[i]*100000);if(!groups.TryGetValue(key,out var group)){group=new List<int>();groups[key]=group;}group.Add(i);}
                float threshold=Mathf.Cos(65*Mathf.Deg2Rad);
                for(int i=0;i<vertices.Length;i++){var sum=Vector3.zero;foreach(int j in groups[Vector3Int.RoundToInt(vertices[i]*100000)])if(Vector3.Dot(source[i],source[j])>=threshold)sum+=source[j];normals[i]=sum.sqrMagnitude>.000001f?sum.normalized:source[i];}mesh.normals=normals;
            }
        }
    }
}
