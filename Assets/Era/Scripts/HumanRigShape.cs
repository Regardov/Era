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
        Quaternion leftSole,rightSole;
        bool solesBound;
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
            if(plantFeet&&actor?.GroundedPose==true){PlantFeet();ApplyCrouch(actor.CrouchBlend);}
        }
        public void PlantFeet(float crouch=0)
        {
            if(!plantFeet||bones==null)return;
            if(leftFoot==null)foreach(var bone in bones)
            {if(bone.name=="foot.l")leftFoot=bone;else if(bone.name=="foot.r")rightFoot=bone;else if(bone.name=="hips")pelvis=bone;}
            if(leftFoot==null||rightFoot==null||pelvis==null)return;
            float lowest=Mathf.Min(transform.InverseTransformPoint(leftFoot.position).y,transform.InverseTransformPoint(rightFoot.position).y);
            pelvis.position+=transform.TransformVector(Vector3.up*Mathf.Clamp(footHeight-lowest,-.45f,.65f));
        }
        void Awake()
        {
            var actor=GetComponent<SkeletalActor>();
            var l=actor?.Bone("foot.l");var r=actor?.Bone("foot.r");
            if(l!=null&&r!=null){leftSole=Quaternion.Inverse(transform.rotation)*l.rotation;rightSole=Quaternion.Inverse(transform.rotation)*r.rotation;solesBound=true;}
        }
        public void ApplyCrouch(float blend)
        {
            blend=Mathf.Clamp01(blend);if(blend<=0||!plantFeet)return;
            var actor=GetComponent<SkeletalActor>();if(actor==null||leftFoot==null||rightFoot==null||pelvis==null)return;
            Vector3 left=transform.InverseTransformPoint(leftFoot.position),right=transform.InverseTransformPoint(rightFoot.position);
            // Retain the walk cycle's small stride, but keep both ankles on the floor.
            left=Vector3.Lerp(left,new Vector3(-.18f,footHeight,Mathf.Clamp(left.z,-.16f,.20f)),blend);
            right=Vector3.Lerp(right,new Vector3(.18f,footHeight,Mathf.Clamp(right.z,-.16f,.20f)),blend);
            var lRotation=leftFoot.rotation;var rRotation=rightFoot.rotation;
            pelvis.position+=transform.TransformVector(new Vector3(0,-.28f*blend,-.12f*blend));
            actor.spine.rotation=Quaternion.AngleAxis(14*blend,transform.right)*actor.spine.rotation;
            SolveLeg(actor.leftThigh,actor.leftKnee,leftFoot,transform.TransformPoint(left));
            SolveLeg(actor.rightThigh,actor.rightKnee,rightFoot,transform.TransformPoint(right));
            leftFoot.rotation=solesBound?Quaternion.Slerp(lRotation,transform.rotation*leftSole,blend):lRotation;
            rightFoot.rotation=solesBound?Quaternion.Slerp(rRotation,transform.rotation*rightSole,blend):rRotation;
        }
        void SolveLeg(Transform thigh,Transform knee,Transform foot,Vector3 target)
        {
            var hip=thigh.position;float upper=Vector3.Distance(hip,knee.position),lower=Vector3.Distance(knee.position,foot.position);
            var delta=target-hip;float distance=Mathf.Clamp(delta.magnitude,.01f,upper+lower-.0001f);var axis=delta.normalized;
            var forward=Vector3.ProjectOnPlane(transform.forward,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var joint=hip+axis*along+forward*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            thigh.rotation=Quaternion.FromToRotation(knee.position-hip,joint-hip)*thigh.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;
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
