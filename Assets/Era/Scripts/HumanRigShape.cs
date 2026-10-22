using UnityEngine;

namespace Era
{
    // Keep the authored skeletal clips, but remap their pose to adult proportions after animation.
    [DefaultExecutionOrder(70)]
    public sealed class HumanRigShape:MonoBehaviour
    {
        public Transform[] bones;
        public Vector3[] originalLocalPositions;
        Vector3[] targets;
        public static Vector3 Shape(Vector3 point,bool head=false,bool halo=false)
        {
            float y=head?1.85f+(point.y-1.3f)*.32f:point.y<.6f?point.y*1.8f:point.y<1.3f?1.08f+(point.y-.6f)*1.1f:1.85f+(point.y-1.3f)*.32f;
            return new Vector3(point.x*(halo?.60f:head?.30f:.78f),y,point.z*(halo?.60f:head?.26f:.85f));
        }
        public static bool HeadBone(Transform bone)
        {for(var t=bone;t!=null;t=t.parent)if(t.name=="head")return true;return false;}
        void Update()
        {
            if(bones==null||GameApp.Instance?.WorldPaused==true)return;
            for(int i=0;i<bones.Length;i++)bones[i].localPosition=originalLocalPositions[i];
        }
        void LateUpdate()
        {
            if(bones==null||GameApp.Instance?.WorldPaused==true)return;
            targets??=new Vector3[bones.Length];
            for(int i=0;i<bones.Length;i++)targets[i]=Shape(transform.InverseTransformPoint(bones[i].position),HeadBone(bones[i]));
            for(int i=0;i<bones.Length;i++)bones[i].position=transform.TransformPoint(targets[i]);
        }
    }
}
