using System;
using System.Linq;
using UnityEngine;

namespace Era
{
    // Visual adapter only: Simulation remains the owner of auras, casts and damage.
    [DefaultExecutionOrder(65),DisallowMultipleComponent]
    public sealed class LumenAnimationDriver:MonoBehaviour
    {
        public Animator animator;
        public Transform actorRoot;
        public ArcherClipReference[] clips=Array.Empty<ArcherClipReference>();
        public float walkSpeed=1.10f,runSpeed=3.45f;
        int owner=-1,castSerial=-1,jumpSerial=-1,landSerial=-1;
        float lastHp=-1,speed,crouch;
        bool dead,manual,applied,standing;
        Transform[] changed;Vector3[] positions;Quaternion[] rotations;
        public Transform Bone(string name)=>GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        void Awake(){Bind();Stand();}
        void Bind()
        {
            if(animator==null)animator=GetComponent<Animator>();
            if(actorRoot==null)actorRoot=GetComponentInParent<HeroMotion>()?.transform??transform;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            if(changed!=null)return;
            changed=new[]{"Pelvis","Thigh.L","Shin.L","Foot.L","Thigh.R","Shin.R","Foot.R"}.Select(Bone).ToArray();
            positions=new Vector3[changed.Length];rotations=new Quaternion[changed.Length];
        }
        public void Stand()
        {
            Bind();Restore();manual=false;dead=false;owner=-1;lastHp=-1;speed=crouch=0;
            if(standing)return;standing=true;
            animator.enabled=true;animator.speed=1;animator.SetFloat("Speed",0);animator.SetFloat("LocomotionRate",1);
            animator.SetBool("IsGrounded",true);animator.SetBool("AuraChannel",false);animator.Play("Locomotion",0,0);
        }
        public void Present(PlayerState p)
        {
            Bind();manual=false;standing=false;
            var app=GameApp.Instance;bool paused=app!=null&&app.WorldPaused;animator.speed=paused?0:1;if(paused)return;
            // Share the fixed-tick interpolation used by the camera, including 150+ FPS.
            if(app!=null&&app.IsHost&&ReferenceEquals(p,app.LocalPlayer))actorRoot.position=app.LocalPresentationPosition(p);
            bool arriving=owner!=p.id;owner=p.id;
            if(arriving){castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;lastHp=p.hp;}
            if(dead&&!p.downed){Stand();owner=p.id;castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;}
            animator.enabled=true;
            float target=Vector3.ProjectOnPlane(p.velocity,Vector3.up).magnitude;
            speed=PresentationMotion.Damp(speed,p.grounded?target:0,12,PresentationMotion.Delta);
            crouch=PresentationMotion.Damp(crouch,p.crouching&&p.grounded?1:0,14,PresentationMotion.Delta);
            animator.SetFloat("Speed",speed);
            float gait=target<walkSpeed?Mathf.Lerp(.65f,1,target/Mathf.Max(.01f,walkSpeed)):Mathf.Clamp(target/runSpeed,.8f,1.65f);
            animator.SetFloat("LocomotionRate",target<.05f?1:gait);
            animator.SetBool("IsGrounded",p.grounded);animator.SetFloat("VerticalSpeed",p.verticalSpeed);
            animator.SetBool("AuraChannel",!p.downed&&p.grounded&&target<.08f&&p.auraSurge>0);
            if(p.downed&&!dead){animator.SetTrigger("Death");dead=true;}
            if(!dead)
            {
                if(p.jumpSerial!=jumpSerial)animator.SetTrigger("Jump");
                if(p.castSerial!=castSerial)animator.SetTrigger("Cast");
                else if(lastHp>=0&&p.hp<lastHp-.01f)animator.SetTrigger("Hit");
            }
            castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;lastHp=p.hp;
        }
        void Update(){Restore();}
        void LateUpdate()
        {
            if(manual||dead||crouch<.001f||!animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))return;
            for(int i=0;i<changed.Length;i++){positions[i]=changed[i].localPosition;rotations[i]=changed[i].localRotation;}
            applied=true;ApplyCrouch(crouch);
        }
        void Restore(){if(!applied)return;for(int i=0;i<changed.Length;i++){changed[i].localPosition=positions[i];changed[i].localRotation=rotations[i];}applied=false;}
        public void ApplyCrouch(float blend)
        {
            Bind();var lf=Bone("Foot.L");var rf=Bone("Foot.R");var l=lf.position;var rr=rf.position;var lq=lf.rotation;var rq=rf.rotation;
            Bone("Pelvis").position+=actorRoot.TransformVector(new Vector3(0,-.24f,-.08f)*Mathf.Clamp01(blend));
            ArmPoseIK.Solve(Bone("Thigh.L"),Bone("Shin.L"),lf,l,Bone("Thigh.L").position+actorRoot.forward);
            ArmPoseIK.Solve(Bone("Thigh.R"),Bone("Shin.R"),rf,rr,Bone("Thigh.R").position+actorRoot.forward);
            lf.rotation=lq;rf.rotation=rq;
        }
        public void SampleClip(string name,float time)
        {
            Bind();Restore();manual=true;animator.enabled=false;
            var clip=clips.First(c=>c.name==name).clip;clip.SampleAnimation(gameObject,Mathf.Clamp(time,0,clip.length));
        }
    }
}
