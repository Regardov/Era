using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class LumenAnimationChecks
    {
        public static int RunPrefab(GameObject prefab)
        {
            int count=0;void Check(bool value,string message){count++;if(!value)throw new Exception("ERA Lumen check failed: "+message);}
            var root=UnityEngine.Object.Instantiate(prefab);var baked=new Mesh();
            try
            {
                var hero=root.GetComponent<HeroMotion>();var actor=hero?.lumen;var meta=root.GetComponent<AuthoredCharacter>();
                Check(hero!=null&&hero.Hero==Hero.Lumen&&actor!=null&&hero.archer==null&&hero.skeletal==null,"dedicated Lumen visual adapter");
                Check(meta.model=="Lumen"&&meta.version==10&&meta.sourceHash.Length==64,"V10 source provenance");
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    Check(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("ArtSource/Characters/AuraWeaver/ReleaseV10/AuraWeaver_Lumen_V10.blend"))).Replace("-","").ToLowerInvariant()==meta.sourceHash,"actual saved Blender source hash");
                Check(actor.animator.avatar!=null&&actor.animator.avatar.isValid&&!actor.animator.avatar.isHuman&&!actor.animator.applyRootMotion,"Generic Avatar without root motion");
                Check(actor.clips.Length==11,"eleven retained mage clips");
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();Check(skins.Length==26,"logical costume mesh count");
                foreach(var skin in skins)
                {
                    var mesh=skin.sharedMesh;
                    Check(mesh.bindposes.Length==skin.bones.Length&&skin.bones.All(b=>b!=null),"valid skin bindposes "+skin.name);
                    Check(mesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f),"normalized skin weights "+skin.name);
                    Check(skin.sharedMaterials.All(m=>m!=null&&m.shader!=null)&&mesh.normals.Length==mesh.vertexCount,"authored palette and normals "+skin.name);
                }
                foreach(var entry in actor.clips)
                {
                    Check(entry.clip!=null&&!entry.clip.empty&&actor.animator.runtimeAnimatorController.animationClips.Contains(entry.clip),"assigned clip "+entry.name);
                    Check(AnimationUtility.GetCurveBindings(entry.clip).All(b=>b.path.Length==0||actor.transform.Find(b.path)!=null),"exact Generic curve paths "+entry.name);
                    foreach(float t in new[]{0f,.25f,.5f,.75f,1f})
                    {
                        actor.SampleClip(entry.name,entry.clip.length*t);
                        foreach(var skin in skins){skin.BakeMesh(baked);Check(baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<8),"finite deformation "+entry.name+" / "+skin.name);}
                    }
                }
                Check(actor.Bone("Hand.L").Find("ERA palm socket")!=null&&actor.Bone("Hand.R").Find("ERA palm socket")!=null,"independent weapon palm sockets");
                return count;
            }
            finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
