using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(85)]
    public sealed class CostumeMotion:MonoBehaviour
    {
        SkinnedMeshRenderer skin;SkeletalActor actor;int stride=-1,crouch=-1;float strideBlend;public int owner=-1;
        void Awake(){skin=GetComponentInChildren<SkinnedMeshRenderer>();actor=GetComponent<SkeletalActor>();if(skin!=null){stride=skin.sharedMesh.GetBlendShapeIndex("StrideClearance");crouch=skin.sharedMesh.GetBlendShapeIndex("CrouchClearance");}}
        void LateUpdate()
        {
            if(skin==null||actor==null||GameApp.Instance?.WorldPaused==true)return;var p=GameApp.Instance?.State?.Player(owner);
            float speed=p==null?0:Vector3.ProjectOnPlane(p.velocity,Vector3.up).magnitude;strideBlend=PresentationMotion.Damp(strideBlend,Mathf.Clamp01(speed/7.8f),10,PresentationMotion.Delta);
            if(stride>=0)skin.SetBlendShapeWeight(stride,strideBlend*100);if(crouch>=0)skin.SetBlendShapeWeight(crouch,actor.CrouchBlend*100);
        }
    }
}
