using System;
using UnityEngine;

namespace Era
{
    // A single post-Animator composition pass. Restore in Update, before the
    // next Animator evaluation: offsets can never accumulate into the rig.
    [DefaultExecutionOrder(55),DisallowMultipleComponent]
    public sealed class ArcherMotionRig:MonoBehaviour
    {
        [Range(0,2)] public float bodyIntensity=1,clothIntensity=1;
        [Range(1,15)] public float turnLeanLimit=8;
        [Range(1,8)] public float quiverLimit=3;
        [Range(0,80)] public float upperAimLimit=70;
        public float turnSpeed=360;

        // Exact critically damped integration for a fixed target. No explicit
        // Euler spring, no physics/Cloth dependency and no frame-rate multiplier.
        [Serializable] public struct Spring
        {
            public Vector3 value,velocity;
            public Vector3 Step(Vector3 target,float frequency,float dt,float limit)
            {
                if(dt<=0)return value;
                float omega=2*Mathf.PI*frequency,e=Mathf.Exp(-omega*dt);
                Vector3 x=value-target,c=velocity+omega*x;
                value=target+(x+c*dt)*e;velocity=(velocity-omega*c*dt)*e;
                value=Vector3.ClampMagnitude(value,limit);velocity=Vector3.ClampMagnitude(velocity,limit*40);
                return value;
            }
        }
        ArcherAnimationDriver actor;
        Transform[] changed;
        Vector3[] positions;Quaternion[] rotations;
        Transform leftClavicle,rightClavicle,leftUpper,rightUpper,leftElbow,rightElbow,leftHand,rightHand,quiver;
        Transform[] cape=new Transform[6];
        Spring body,cloth,quiverSpring;
        Vector3 previousVelocity,previousPosition;
        float previousYaw,facing,aimOffset,turnRate,forwardAcceleration;
        bool initialized,applied,hasFacing;
        public float TurnRate=>turnRate;
        public float BodyLean=>body.value.z;
        public float CloakDeflection=>cloth.value.magnitude;
        public float QuiverDeflection=>quiverSpring.value.magnitude;
        public float GripError{get;private set;}
        public float AimOffset=>aimOffset;
        public float AimError{get;private set;}

        void Bind(ArcherAnimationDriver source)
        {
            if(initialized)return;actor=source;
            leftClavicle=actor.Bone("Clavicle.L");rightClavicle=actor.Bone("Clavicle.R");
            leftUpper=actor.Bone("UpperArm.L");leftElbow=actor.Bone("Forearm.L");leftHand=actor.Bone("Hand.L");
            rightUpper=actor.Bone("UpperArm.R");rightElbow=actor.Bone("Forearm.R");rightHand=actor.Bone("Hand.R");
            quiver=actor.Bone("QuiverAttach");
            string[] capeNames={"Cape1.L","Cape2.L","Cape1.C","Cape2.C","Cape1.R","Cape2.R"};
            for(int i=0;i<cape.Length;i++)cape[i]=actor.Bone(capeNames[i]);
            changed=new[]{actor.pelvis,actor.spine,actor.chest,actor.head,leftClavicle,rightClavicle,
                leftUpper,leftElbow,leftHand,rightUpper,rightElbow,rightHand,actor.leftThigh,actor.leftShin,actor.leftFoot,
                actor.rightThigh,actor.rightShin,actor.rightFoot,cape[0],cape[1],cape[2],cape[3],cape[4],cape[5],quiver};
            positions=new Vector3[changed.Length];rotations=new Quaternion[changed.Length];initialized=true;
        }
        void Update()
        {
            if(GameApp.Instance?.WorldPaused!=true)Restore();
        }
        public void Restore()
        {
            if(!applied)return;
            for(int i=0;i<changed.Length;i++)if(changed[i]!=null){changed[i].localRotation=rotations[i];changed[i].localPosition=positions[i];}
            applied=false;
        }
        public void ResetMotion()
        {
            Restore();body=cloth=quiverSpring=default;previousVelocity=Vector3.zero;hasFacing=false;aimOffset=0;turnRate=0;
        }
        public float Facing(ArcherAnimationDriver source,PlayerState p,float aimYaw,bool aiming,float dt)
        {
            Bind(source);if(!hasFacing){facing=p.yaw;hasFacing=true;}
            float difference=Mathf.DeltaAngle(p.yaw,aimYaw);
            // Beyond the comfortable twist, rotate the visual body gradually.
            // Directional locomotion still follows the actual world velocity.
            float target=p.yaw+(aiming?Mathf.Sign(difference)*Mathf.Max(0,Mathf.Abs(difference)-55):0);
            facing=Mathf.MoveTowardsAngle(facing,target,turnSpeed*Mathf.Clamp(dt,0,.1f));
            return facing;
        }
        void Rotate(Transform bone,Vector3 euler)
        {
            if(bone==null)return;
            var frame=actor.actorRoot.rotation;
            bone.rotation=(frame*Quaternion.Euler(euler)*Quaternion.Inverse(frame))*bone.rotation;
        }
        static Vector3 ReachCorrection(Transform upper,Transform elbow,Transform hand,Vector3 goal)
        {
            float reach=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position)-.001f;
            var delta=goal-upper.position;
            return delta.magnitude>reach?delta.normalized*reach-delta:Vector3.zero;
        }
        public void Compose(ArcherAnimationDriver source,PlayerState p,float aimYaw,float aimWeight,bool locomotion,float dt)
        {
            if(!enabled||dt<=0)return;Bind(source);dt=Mathf.Min(dt,.1f);
            if(applied)Restore();
            for(int i=0;i<changed.Length;i++)if(changed[i]!=null){positions[i]=changed[i].localPosition;rotations[i]=changed[i].localRotation;}
            applied=true;
            Transform root=actor.actorRoot;
            float yaw=root.eulerAngles.y;
            if((root.position-previousPosition).sqrMagnitude>9){body=cloth=quiverSpring=default;previousVelocity=p.velocity;previousYaw=yaw;}
            turnRate=PresentationMotion.Damp(turnRate,Mathf.Clamp(Mathf.DeltaAngle(previousYaw,yaw)/dt,-480,480),12,dt);
            var acceleration=root.InverseTransformDirection((p.velocity-previousVelocity)/dt);
            forwardAcceleration=PresentationMotion.Damp(forwardAcceleration,Mathf.Clamp(acceleration.z,-18,18),10,dt);
            previousVelocity=p.velocity;previousYaw=yaw;previousPosition=root.position;
            float speed=Vector3.ProjectOnPlane(p.velocity,Vector3.up).magnitude;
            float run=Mathf.Clamp01(speed/7.8f),weight=locomotion&&p.grounded&&!p.crouching?1:0;
            float phase=actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime*2*Mathf.PI;
            float wave=Mathf.Sin(phase),step=Mathf.Sin(phase*2-.3f);
            var lean=body.Step(new Vector3(Mathf.Clamp(forwardAcceleration*.17f,-3,3),0,-Mathf.Clamp(turnRate*.022f,-turnLeanLimit,turnLeanLimit))*run*weight,3.5f,dt,turnLeanLimit+3)*bodyIntensity;

            var leftFoot=actor.leftFoot.position;var rightFoot=actor.rightFoot.position;
            var leftFootRotation=actor.leftFoot.rotation;var rightFootRotation=actor.rightFoot.rotation;
            var leftPosition=leftHand.position;var rightPosition=rightHand.position;
            var leftRotation=leftHand.rotation;var rightRotation=rightHand.rotation;
            var headRotation=actor.head.rotation;var chestPosition=actor.chest.position;
            var leftPole=leftElbow.position;var rightPole=rightElbow.position;
            Rotate(actor.pelvis,lean*.15f);
            Rotate(actor.spine,lean*.65f);
            // Spine remains on the base layer; recover the small chest/shoulder
            // part of the gait that the shooting override would otherwise erase.
            Rotate(actor.chest,lean*.2f+new Vector3(-1.8f*step,-3.2f*wave,1.1f*wave)*run*aimWeight*weight*bodyIntensity);
            Rotate(leftClavicle,new Vector3(.8f*step,0,1.5f*wave)*run*aimWeight*weight*bodyIntensity);
            Rotate(rightClavicle,new Vector3(-.8f*step,0,-1.5f*wave)*run*aimWeight*weight*bodyIntensity);
            float target=Mathf.Clamp(Mathf.DeltaAngle(yaw,aimYaw),-upperAimLimit,upperAimLimit)*aimWeight;
            aimOffset=PresentationMotion.Damp(aimOffset,target,20,dt);
            Rotate(actor.pelvis,new Vector3(0,aimOffset*.08f,0));
            Rotate(actor.spine,new Vector3(0,aimOffset*.32f,0));
            Rotate(actor.chest,new Vector3(0,aimOffset*.60f,0));
            if(aimWeight>.01f)
            {
                var aim=Quaternion.AngleAxis(aimOffset,root.up);
                // Keep the authored draw-hand/nock relationship. Body and
                // clavicles can move without dragging the bow around with them.
                // Keep the draw forearm below the hood rim during the gait.
                // Both hands move together, so string/nock spacing is intact.
                var drift=(actor.chest.position-chestPosition)*.25f-root.up*.032f*aimWeight;
                var leftGoal=chestPosition+aim*(leftPosition-chestPosition)+drift;
                var rightGoal=chestPosition+aim*(rightPosition-chestPosition)+drift;
                var leftBend=chestPosition+aim*(leftPole-chestPosition);
                var rightBend=chestPosition+aim*(rightPole-chestPosition);
                // Translate both goals together into the reachable overlap.
                // Clamping each hand separately would pull the nock away from
                // the draw hand during a bank or a large aiming twist.
                for(int i=0;i<8;i++)
                {
                    var correction=ReachCorrection(leftUpper,leftElbow,leftHand,leftGoal);
                    leftGoal+=correction;rightGoal+=correction;
                    correction=ReachCorrection(rightUpper,rightElbow,rightHand,rightGoal);
                    leftGoal+=correction;rightGoal+=correction;
                }
                ArmPoseIK.Solve(leftUpper,leftElbow,leftHand,leftGoal,leftBend);
                ArmPoseIK.Solve(rightUpper,rightElbow,rightHand,rightGoal,rightBend);
                leftHand.rotation=aim*leftRotation;rightHand.rotation=aim*rightRotation;
                GripError=Mathf.Max(Vector3.Distance(leftHand.position,leftGoal),Vector3.Distance(rightHand.position,rightGoal));
                actor.head.rotation=Quaternion.Slerp(actor.head.rotation,aim*headRotation,.85f);
                AimError=Mathf.Abs(Mathf.DeltaAngle(yaw+aimOffset,aimYaw));
            }
            else {GripError=0;AimError=0;}
            // Preserve both authored ankle targets while the pelvis banks.
            if(weight>0)
            {
                ArmPoseIK.Solve(actor.leftThigh,actor.leftShin,actor.leftFoot,leftFoot,actor.leftThigh.position+root.forward);
                ArmPoseIK.Solve(actor.rightThigh,actor.rightShin,actor.rightFoot,rightFoot,actor.rightThigh.position+root.forward);
                actor.leftFoot.rotation=leftFootRotation;actor.rightFoot.rotation=rightFootRotation;
            }
            var clothTarget=new Vector3(3.5f*run+Mathf.Clamp(forwardAcceleration*.15f,-1.5f,2.8f),-turnRate*.008f,turnRate*.016f)*weight;
            var fabric=cloth.Step(clothTarget,2.5f,dt,12)*clothIntensity;
            for(int i=0;i<3;i++)
            {
                float offset=(i-1)*.8f;
                float flutter=(.18f+run*1.1f)*Mathf.Sin(phase*1.1f+offset)*weight*clothIntensity;
                Rotate(cape[2*i],new Vector3(fabric.x+flutter,fabric.y*.4f,fabric.z*.55f));
                Rotate(cape[2*i+1],new Vector3(fabric.x*1.45f+flutter*1.6f,fabric.y,fabric.z*(1.05f+offset*.08f)));
            }
            var rigid=quiverSpring.Step(new Vector3(Mathf.Clamp(forwardAcceleration*.06f,-1,1),-turnRate*.005f,turnRate*.003f)*weight,5.5f,dt,quiverLimit);
            Rotate(quiver,rigid);
        }
    }
}
