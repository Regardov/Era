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
        struct FootContact {public bool active;public Vector3 anchor;public Quaternion rootRotation;}
        FootContact leftContact,rightContact;
        Vector3 previousVelocity,previousPosition;
        float previousYaw,facing,aimOffset,turnRate,forwardAcceleration;
        bool initialized,applied,hasFacing;
        Vector3 hoodCenter,hoodRadii;bool hoodBound;float elbowAvoidance;
        Vector3[] drawHandPoints=Array.Empty<Vector3>();float wristRoll;
        public float ArmClearancePenalty{get;private set;}
        public float TurnRate=>turnRate;
        public float BodyLean=>body.value.z;
        public float CloakDeflection=>cloth.value.magnitude;
        public float QuiverDeflection=>quiverSpring.value.magnitude;
        public float GripError{get;private set;}
        public float AimOffset=>aimOffset;
        public float AimError{get;private set;}
        public float FootPlantError{get;private set;}
        public bool LeftPlanted{get;private set;}
        public bool RightPlanted{get;private set;}

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
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(skin.name=="Hood"&&actor.head!=null)
            {
                var mesh=new Mesh();skin.BakeMesh(mesh,true);var bounds=new Bounds();bool first=true;
                foreach(var v in mesh.vertices){var p=actor.head.InverseTransformPoint(skin.transform.TransformPoint(v));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                hoodCenter=bounds.center;hoodRadii=bounds.extents+Vector3.one*(.025f/Mathf.Max(.01f,actor.head.lossyScale.x));hoodBound=!first;
                if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);break;
            }
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(skin.name=="Arm_R")
            {
                var mesh=new Mesh();skin.BakeMesh(mesh,true);var points=new System.Collections.Generic.List<Vector3>();
                foreach(var v in mesh.vertices){var delta=skin.transform.TransformPoint(v)-rightHand.position;if(delta.magnitude<.17f)points.Add(Quaternion.Inverse(rightHand.rotation)*delta);}
                drawHandPoints=points.ToArray();if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);break;
            }
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
            Restore();body=cloth=quiverSpring=default;leftContact=rightContact=default;previousVelocity=Vector3.zero;hasFacing=false;aimOffset=0;turnRate=0;
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
        Vector3 ElbowPosition(Vector3 goal,Vector3 pole)
        {
            var origin=rightUpper.position;float a=Vector3.Distance(origin,rightElbow.position),b=Vector3.Distance(rightElbow.position,rightHand.position);
            var delta=goal-origin;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);var axis=delta.normalized;
            var bend=Vector3.ProjectOnPlane(pole-origin,axis).normalized;float along=(a*a-b*b+d*d)/(2*d);
            return origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        }
        float ForearmCost(Vector3 elbow,Vector3 wrist)
        {
            float cost=0;
            for(int i=0;i<5;i++)
            {
                float t=i*.225f;
                var p=actor.head.InverseTransformPoint(Vector3.Lerp(elbow,wrist,t))-hoodCenter;
                p=new Vector3(p.x/Mathf.Max(.01f,hoodRadii.x),p.y/Mathf.Max(.01f,hoodRadii.y),p.z/Mathf.Max(.01f,hoodRadii.z));
                float overlap=Mathf.Max(0,1-p.magnitude);cost+=overlap*overlap;
            }
            return cost;
        }
        float HandCost(Vector3 wrist,Quaternion orientation)
        {
            float cost=0;
            foreach(var offset in drawHandPoints)
            {
                var p=actor.head.InverseTransformPoint(wrist+orientation*offset)-hoodCenter;
                p=new Vector3(p.x/Mathf.Max(.01f,hoodRadii.x),p.y/Mathf.Max(.01f,hoodRadii.y),p.z/Mathf.Max(.01f,hoodRadii.z));
                float overlap=Mathf.Max(0,1-p.magnitude);cost+=overlap*overlap;
            }
            return cost/Mathf.Max(1,drawHandPoints.Length);
        }
        Vector3 ClearDrawElbow(Vector3 goal,Vector3 pole,float dt)
        {
            if(!hoodBound)return pole;var axis=(goal-rightUpper.position).normalized;float best=0,bestScore=float.MaxValue;
            for(int angle=-60;angle<=60;angle+=5)
            {
                var candidate=rightUpper.position+Quaternion.AngleAxis(angle,axis)*(pole-rightUpper.position);
                float score=ForearmCost(ElbowPosition(goal,candidate),goal)+Mathf.Abs(angle)*.000002f;
                if(score<bestScore){bestScore=score;best=angle;}
            }
            elbowAvoidance=PresentationMotion.Damp(elbowAvoidance,best,24,dt);
            var result=rightUpper.position+Quaternion.AngleAxis(elbowAvoidance,axis)*(pole-rightUpper.position);
            ArmClearancePenalty=ForearmCost(ElbowPosition(goal,result),goal);return result;
        }
        float Ground(PlayerState p,Vector3 point)
        {
            var state=GameApp.Instance?.State;
            return state!=null?ActorBodies.Support(state,p,point,p.position.y+.18f):actor.actorRoot.position.y;
        }
        void PlantFoot(PlayerState p,Transform thigh,Transform shin,Transform foot,Vector3 original,Quaternion originalRotation,float phase,float duty,float speed,ref FootContact contact,out bool planted)
        {
            var root=actor.actorRoot;float offset=.105f*actor.transform.lossyScale.y;
            float weight=0;
            if(speed>.2f&&phase<duty)
            {
                if(!contact.active){contact.active=true;contact.anchor=original;contact.anchor.y=Ground(p,original)+offset;contact.rootRotation=root.rotation;}
                weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(phase/.035f,(duty-phase)/.045f)));
                float reach=Vector3.Distance(thigh.position,shin.position)+Vector3.Distance(shin.position,foot.position);
                if(Vector3.Distance(thigh.position,contact.anchor)>reach*.99f){contact.active=false;weight=0;}
            }
            else contact.active=false;
            Vector3 goal=original;
            // Modest terrain correction on support feet. Swing feet and all
            // airborne poses retain their authored arc, with no ground pinning.
            if(speed<=.2f||weight>0)
            {
                goal.y=Mathf.Clamp(Ground(p,goal)+offset,original.y-.16f,original.y+.16f);
                goal=Vector3.Lerp(goal,contact.anchor,weight);
            }
            ArmPoseIK.Solve(thigh,shin,foot,goal,thigh.position+root.forward*.5f+root.right*(foot==actor.leftFoot?-.03f:.03f));
            var rotation=originalRotation;
            if(weight>0)rotation=Quaternion.Slerp(rotation,contact.rootRotation*Quaternion.Inverse(root.rotation)*originalRotation,weight);
            if(speed<=.2f||weight>.5f)
            {
                float dx=(Ground(p,goal+root.right*.06f)-Ground(p,goal-root.right*.06f))/.12f;
                float dz=(Ground(p,goal+root.forward*.08f)-Ground(p,goal-root.forward*.08f))/.16f;
                var normal=(root.up-root.right*dx-root.forward*dz).normalized;
                rotation=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(root.up,normal),25)*rotation;
            }
            foot.rotation=rotation;planted=weight>.95f;
            if(planted)FootPlantError=Mathf.Max(FootPlantError,Vector3.Distance(foot.position,contact.anchor));
        }
        public void Compose(ArcherAnimationDriver source,PlayerState p,float aimYaw,float aimWeight,bool locomotion,float dt)
        {
            if(!enabled||dt<=0)return;Bind(source);dt=Mathf.Min(dt,.1f);
            if(applied)Restore();
            for(int i=0;i<changed.Length;i++)if(changed[i]!=null){positions[i]=changed[i].localPosition;rotations[i]=changed[i].localRotation;}
            applied=true;
            Transform root=actor.actorRoot;
            float yaw=root.eulerAngles.y;
            if((root.position-previousPosition).sqrMagnitude>9){body=cloth=quiverSpring=default;leftContact=rightContact=default;previousVelocity=p.velocity;previousYaw=yaw;}
            turnRate=PresentationMotion.Damp(turnRate,Mathf.Clamp(Mathf.DeltaAngle(previousYaw,yaw)/dt,-480,480),12,dt);
            var acceleration=root.InverseTransformDirection((p.velocity-previousVelocity)/dt);
            forwardAcceleration=PresentationMotion.Damp(forwardAcceleration,Mathf.Clamp(acceleration.z,-18,18),10,dt);
            previousVelocity=p.velocity;previousYaw=yaw;previousPosition=root.position;
            float speed=Vector3.ProjectOnPlane(p.velocity,Vector3.up).magnitude;
            float run=Mathf.Clamp01(speed/7.8f),weight=locomotion&&p.grounded&&!p.crouching?1:0;
            float equipmentWeight=locomotion||actor.IsAirborne?1:0;
            float phase=actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime*2*Mathf.PI;
            float wave=Mathf.Sin(phase),step=Mathf.Sin(phase*2-.3f);
            var lean=body.Step(new Vector3(Mathf.Clamp(forwardAcceleration*.17f,-3,3),0,-Mathf.Clamp(turnRate*.022f,-turnLeanLimit,turnLeanLimit))*run*weight,3.5f,dt,turnLeanLimit+3)*bodyIntensity;

            var leftFoot=actor.leftFoot.position;var rightFoot=actor.rightFoot.position;
            var leftFootRotation=actor.leftFoot.rotation;var rightFootRotation=actor.rightFoot.rotation;
            var leftPosition=leftHand.position;var rightPosition=rightHand.position;
            var leftRotation=leftHand.rotation;var rightRotation=rightHand.rotation;
            var muzzlePosition=actor.arrowSpawn!=null?actor.arrowSpawn.position:leftPosition;
            var muzzleAxis=actor.arrowSpawn!=null?actor.arrowSpawn.up:root.forward;
            var nockPosition=actor.arrowNock!=null?actor.arrowNock.position:rightPosition;
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
                var drift=(actor.chest.position-chestPosition)*.25f+(-root.up*.055f+root.forward*.025f)*aimWeight;
                var leftGoal=chestPosition+aim*(leftPosition-chestPosition)+drift;
                var rightGoal=chestPosition+aim*(rightPosition-chestPosition)+drift;
                var leftBend=chestPosition+aim*(leftPole-chestPosition);
                var rightBend=chestPosition+aim*(rightPole-chestPosition);
                var handAim=aim;
                var drawOrientation=aim*rightRotation;
                if(actor.IsReleasing&&actor.arrowSpawn!=null)
                {
                    // The base-layer spine still leans during a run/jump. Keep
                    // both hands and the bow together while registering the
                    // release marker to the deterministic gameplay profile.
                    // Reach clamping below keeps the arms anatomical.
                    var predictedMuzzle=leftGoal+aim*(muzzlePosition-leftPosition);
                    var fireOrigin=ArcherShotProfile.Origin(root.position,aimYaw);
                    var align=Quaternion.FromToRotation(aim*muzzleAxis,Quaternion.Euler(0,aimYaw,0)*Vector3.forward);
                    leftGoal=fireOrigin+align*(leftGoal-predictedMuzzle);
                    rightGoal=fireOrigin+align*(rightGoal-predictedMuzzle);
                    leftBend=fireOrigin+align*(leftBend-predictedMuzzle);
                    rightBend=fireOrigin+align*(rightBend-predictedMuzzle);
                    handAim=align*aim;
                    drawOrientation=handAim*rightRotation;
                }
                if(hoodBound&&!actor.IsReleasing&&actor.arrowNock!=null)
                {
                    // Roll the wrist about the actual finger/nock contact,
                    // rather than pulling the fingers away from the string.
                    var grip=Quaternion.Inverse(rightRotation)*(nockPosition-rightPosition);
                    var nock=leftGoal+handAim*(nockPosition-leftPosition);float best=0,score=float.MaxValue;
                    for(int angle=-30;angle<=30;angle+=5)
                    {
                        var candidate=Quaternion.AngleAxis(angle,handAim*muzzleAxis)*handAim*rightRotation;
                        float cost=HandCost(nock-candidate*grip,candidate)+Mathf.Abs(angle)*.000002f;
                        if(cost<score){score=cost;best=angle;}
                    }
                    wristRoll=PresentationMotion.Damp(wristRoll,best,24,dt);
                    drawOrientation=Quaternion.AngleAxis(wristRoll,handAim*muzzleAxis)*handAim*rightRotation;
                    rightGoal=nock-drawOrientation*grip;
                }
                // Translate both goals together into the reachable overlap.
                // Clamping each hand separately would pull the nock away from
                // the draw hand during a bank or a large aiming twist.
                for(int i=0;i<(actor.IsReleasing?1:8);i++)
                {
                    var correction=ReachCorrection(leftUpper,leftElbow,leftHand,leftGoal);
                    leftGoal+=correction;if(!actor.IsReleasing)rightGoal+=correction;
                    correction=ReachCorrection(rightUpper,rightElbow,rightHand,rightGoal);
                    if(!actor.IsReleasing)leftGoal+=correction;rightGoal+=correction;
                }
                // Once released the draw hand is free of the nock; its reach
                // clamp must not drag the bow away from the firing origin.
                rightBend=ClearDrawElbow(rightGoal,rightBend,dt);
                ArmPoseIK.Solve(leftUpper,leftElbow,leftHand,leftGoal,leftBend);
                ArmPoseIK.Solve(rightUpper,rightElbow,rightHand,rightGoal,rightBend);
                leftHand.rotation=handAim*leftRotation;rightHand.rotation=drawOrientation;
                GripError=Mathf.Max(Vector3.Distance(leftHand.position,leftGoal),Vector3.Distance(rightHand.position,rightGoal));
                actor.head.rotation=Quaternion.Slerp(actor.head.rotation,aim*headRotation,.85f);
                AimError=Mathf.Abs(Mathf.DeltaAngle(yaw+aimOffset,aimYaw));
            }
            else {GripError=0;AimError=0;}
            // Preserve both authored ankle targets while the pelvis banks.
            FootPlantError=0;LeftPlanted=RightPlanted=false;
            if(weight>0&&(actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion")||actor.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Archer_Land")))
            {
                float gait=Mathf.InverseLerp(actor.walkSpeed,actor.runSpeed,speed);
                var local=root.InverseTransformDirection(p.velocity);var direction=new Vector2(local.x,local.z);
                float duty=Mathf.Lerp(.62f/actor.DirectionalScale(direction,false),.22f/actor.DirectionalScale(direction,true),gait);
                float cycle=Mathf.Repeat(actor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);
                PlantFoot(p,actor.leftThigh,actor.leftShin,actor.leftFoot,leftFoot,leftFootRotation,cycle,duty,speed,ref leftContact,out bool leftPlanted);
                PlantFoot(p,actor.rightThigh,actor.rightShin,actor.rightFoot,rightFoot,rightFootRotation,Mathf.Repeat(cycle+.5f,1),duty,speed,ref rightContact,out bool rightPlanted);
                LeftPlanted=leftPlanted;RightPlanted=rightPlanted;
            }
            else leftContact=rightContact=default;
            float air=actor.IsAirborne?Mathf.Clamp(p.verticalSpeed*.16f,-2.2f,2.2f):0;
            var clothTarget=new Vector3(3.5f*run+Mathf.Clamp(forwardAcceleration*.15f,-1.5f,2.8f)+air,-turnRate*.008f,turnRate*.016f)*equipmentWeight;
            var fabric=cloth.Step(clothTarget,2.5f,dt,12)*clothIntensity;
            for(int i=0;i<3;i++)
            {
                float offset=(i-1)*.8f;
                float flutter=(.18f+run*1.1f)*Mathf.Sin(phase*1.1f+offset)*equipmentWeight*clothIntensity;
                Rotate(cape[2*i],new Vector3(fabric.x+flutter,fabric.y*.4f,fabric.z*.55f));
                Rotate(cape[2*i+1],new Vector3(fabric.x*1.45f+flutter*1.6f,fabric.y,fabric.z*(1.05f+offset*.08f)));
            }
            var rigid=quiverSpring.Step(new Vector3(Mathf.Clamp(forwardAcceleration*.06f,-1,1)+air*.15f,-turnRate*.005f,turnRate*.003f)*equipmentWeight,5.5f,dt,quiverLimit);
            Rotate(quiver,rigid);
        }
    }
}
