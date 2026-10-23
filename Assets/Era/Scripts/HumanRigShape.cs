using UnityEngine;

namespace Era
{
    // Keep the authored skeletal clips, but remap their pose to adult proportions after animation.
    [DefaultExecutionOrder(70)]
    public sealed class HumanRigShape:MonoBehaviour
    {
        public Transform[] bones;
        public Vector3[] originalLocalPositions;
        public Vector3[] shapedLocalPositions;
        public bool extendForearms;
        public bool plantFeet;
        public float footHeight=.14f;
        Transform leftFoot,rightFoot,pelvis;
        Vector3[] targets;
        public static Vector3 Shape(Vector3 point,bool head=false,bool halo=false)
        {
            float y=head?1.85f+(point.y-1.3f)*.32f:point.y<.6f?point.y*1.8f:point.y<1.3f?1.08f+(point.y-.6f)*1.1f:1.85f+(point.y-1.3f)*.32f;
            return new Vector3(point.x*(halo?.60f:head?.30f:.78f),y,point.z*(halo?.60f:head?.26f:.85f));
        }
        public static bool HeadBone(Transform bone)
        {for(var t=bone;t!=null;t=t.parent)if(t.name=="head")return true;return false;}
        public static Vector3 BonePosition(Transform root,Transform bone,bool extend)
        {
            var point=Shape(root.InverseTransformPoint(bone.position),HeadBone(bone));
            if(extend)for(var t=bone;t!=null&&t!=root;t=t.parent)
            {
                if(t.name!="hand.l"&&t.name!="hand.r")continue;
                var wrist=Shape(root.InverseTransformPoint(t.position));var elbow=Shape(root.InverseTransformPoint(t.parent.position));
                point+=(wrist-elbow).normalized*.17f;break;
            }
            return point;
        }
        void Update()
        {
            if(bones==null||GameApp.Instance?.WorldPaused==true)return;
            for(int i=0;i<bones.Length;i++)bones[i].localPosition=originalLocalPositions[i];
        }
        void LateUpdate()
        {
            if(bones==null||GameApp.Instance?.WorldPaused==true)return;
            ApplyPose();
            var actor=GetComponent<SkeletalActor>();
            if(plantFeet&&actor?.GroundedPose==true)PlantFeet(actor.CrouchBlend);
        }
        public void PlantFeet(float crouch=0)
        {
            if(!plantFeet||bones==null)return;
            if(leftFoot==null)foreach(var bone in bones)
            {if(bone.name=="foot.l")leftFoot=bone;else if(bone.name=="foot.r")rightFoot=bone;else if(bone.name=="hips")pelvis=bone;}
            if(leftFoot==null||rightFoot==null||pelvis==null)return;
            // The thigh/knee crouch overlay adds 65 degrees of net ankle pitch.
            // Counter it at the feet so the soles retain the sampled walking pose.
            leftFoot.localRotation*=Quaternion.Euler(-65*crouch,0,0);
            rightFoot.localRotation*=Quaternion.Euler(-65*crouch,0,0);
            float lowest=Mathf.Min(transform.InverseTransformPoint(leftFoot.position).y,transform.InverseTransformPoint(rightFoot.position).y);
            pelvis.position+=transform.TransformVector(Vector3.up*Mathf.Clamp(footHeight-lowest,-.45f,.65f));
        }
        public void ApplyPose()
        {
            if(shapedLocalPositions!=null&&shapedLocalPositions.Length==bones.Length)
            {
                // Retarget local bind lengths. Global height remapping scaled crouching heads
                // differently from the torso and opened a gap at the neck.
                for(int i=0;i<bones.Length;i++)
                {var delta=bones[i].localPosition-originalLocalPositions[i];bones[i].localPosition=shapedLocalPositions[i]+(bones[i].name=="hips"?delta:Vector3.zero);}
                return;
            }
            targets??=new Vector3[bones.Length];
            for(int i=0;i<bones.Length;i++)targets[i]=BonePosition(transform,bones[i],extendForearms);
            for(int i=0;i<bones.Length;i++)bones[i].position=transform.TransformPoint(targets[i]);
        }
    }
}
