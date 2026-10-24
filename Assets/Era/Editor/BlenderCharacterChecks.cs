using System;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class BlenderCharacterChecks
    {
        static int count;
        static void Check(bool value,string message){count++;if(!value)throw new Exception("ERA Blender character check failed: "+message);}
        public static int Run()
        {
            count=0;
            foreach(string name in BlenderCharacterImporter.Models)
            {
                string resource=(name=="Stalker"||name=="Wisp"?"Creatures/":name=="Wayfarer"||name=="DuskMage"?"Characters/Library/":"Characters/")+name;
                var source=Resources.Load<GameObject>(resource);Check(source!=null,"prefab exists: "+name);
                var metadata=source.GetComponent<AuthoredCharacter>();var skin=source.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;
                Check(metadata!=null&&metadata.version==19&&metadata.model==name&&metadata.sourceHash.Length==64,"Blender source provenance: "+name);
                if(name!="Stalker"&&name!="Wisp")Check(metadata.details.Any(d=>d.StartsWith("Hair / continuous fitted scalp")),"continuous hair covers the scalp: "+name);
                Check(metadata.details.Length>=65&&metadata.triangles>=1500&&metadata.triangles<7000,"named costume details and bounded polygon budget: "+name);
                Check(mesh.vertexCount==metadata.triangles*3&&mesh.vertexCount<60000&&mesh.colors.Length==mesh.vertexCount,"smooth low-poly surfaces carry their authored palette: "+name);
                Check(mesh.bounds.min.y>=-.08f&&mesh.bounds.max.y>2&&mesh.bounds.max.y<3,"boots meet the floor and proportions stay human scale: "+name);
                Check(mesh.normals.Length==mesh.vertexCount&&mesh.normals.All(n=>n.sqrMagnitude>.98f&&n.sqrMagnitude<1.02f),"authored unit normals preserved: "+name);
                Check(mesh.GetBlendShapeIndex("Grip.r")>=0&&mesh.GetBlendShapeIndex("StrideClearance")>=0,"hand and cloth shape keys: "+name);
                Check(mesh.bindposes.Length==skin.bones.Length&&mesh.boneWeights.All(w=>w.boneIndex0<skin.bones.Length&&w.boneIndex1<skin.bones.Length&&Mathf.Abs(w.weight0+w.weight1-1)<.001f),"all skin weights bind to real animation bones: "+name);
                Check(mesh.boneWeights.Any(w=>w.weight0>.1f&&w.weight1>.1f),"neck and cloth use blended skinning: "+name);
                Check(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.y))&&mesh.colors.All(c=>c.a>.99f),"finite geometry and opaque materials: "+name);
                var root=Object.Instantiate(source);var baked=new Mesh();
                try
                {
                    var rig=root.GetComponent<HumanRigShape>();var actor=root.GetComponent<SkeletalActor>();actor.animationPlayer.enabled=false;
                    foreach(string clipName in new[]{"Idle","Running_A","Jump_Idle","1H_Melee_Attack_Chop","2H_Ranged_Shoot"})
                    {
                        var clip=actor.animationPlayer.GetClip(clipName);Check(clip!=null,"retained gameplay clip "+clipName+" / "+name);
                        for(int i=0;i<rig.bones.Length;i++)rig.bones[i].localPosition=rig.originalLocalPositions[i];
                        clip.SampleAnimation(root,.18f);rig.ApplyPose();root.GetComponentInChildren<SkinnedMeshRenderer>().BakeMesh(baked);
                        Check(baked.vertexCount==mesh.vertexCount&&baked.vertices.All(v=>!float.IsNaN(v.x)&&v.magnitude<6),"clip deforms the imported model without exploding: "+clipName+" / "+name);
                    }
                    Check(Vector3.Distance(actor.Bone("head").position,actor.Bone("chest").position)<.34f,"short connected neck is preserved under animation: "+name);
                    if(rig.plantFeet)
                    {
                        rig.PlantFeet();float lowest=Mathf.Min(root.transform.InverseTransformPoint(actor.Bone("foot.l").position).y,root.transform.InverseTransformPoint(actor.Bone("foot.r").position).y);
                        Check(Mathf.Abs(lowest-rig.footHeight)<.001f,"grounded stance keeps the lowest foot on its bind floor: "+name);
                        foreach(string clipName in new[]{"Idle","Walking_A"})foreach(float t in new[]{0f,.18f,.36f})foreach(float blend in new[]{.25f,.5f,1f})
                        {
                            for(int i=0;i<rig.bones.Length;i++)rig.bones[i].localPosition=rig.originalLocalPositions[i];
                            actor.animationPlayer.GetClip(clipName).SampleAnimation(root,t);rig.ApplyPose();rig.PlantFeet();rig.ApplyCrouch(blend);
                            foreach(string side in new[]{"l","r"})
                            {
                                var hip=actor.Bone("upperleg."+side).position;var knee=actor.Bone("lowerleg."+side).position;var foot=actor.Bone("foot."+side).position;
                                var line=foot-hip;var bend=Vector3.ProjectOnPlane(knee-hip,line);
                                Check(Vector3.Dot(bend,root.transform.forward)>.01f,"crouch knee bends forward: "+name+" / "+clipName+" / "+side);
                                if(blend==1)Check(Mathf.Abs(root.transform.InverseTransformPoint(foot).y-rig.footHeight)<.005f,"crouch ankle remains planted: "+name+" / "+side);
                            }
                        }
                    }
                }
                finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);}
            }
            return count;
        }
    }
}
