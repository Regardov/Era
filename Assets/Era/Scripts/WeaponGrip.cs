using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class WeaponGrip:MonoBehaviour
    {
        public Transform actor,hand;public int owner;public WeaponKind kind;
        WeaponPresentation presentation;WeaponGripPoints points;SkeletalActor skeleton;LumenAnimationDriver mage;
        Transform rightUpper,rightElbow,rightWrist,leftUpper,leftElbow,leftWrist,rightPalm,leftPalm,chest;
        Transform[] modified;Quaternion[] before;bool applied,initialized;
        SkinnedMeshRenderer skin;int leftFingers=-1,rightFingers=-1;
        public float PrimaryError{get;private set;}public float SecondaryError{get;private set;}
        public bool IsTwoHanded=>points?.profile!=null&&points.profile.twoHanded;
        void Bind()
        {
            if(initialized||actor==null)return;points=GetComponent<WeaponGripPoints>();presentation=GetComponent<WeaponPresentation>();skeleton=actor.GetComponent<SkeletalActor>();
            mage=actor.GetComponent<HeroMotion>()?.lumen;
            if(skeleton==null&&mage==null)return;
            rightUpper=mage!=null?mage.Bone("UpperArm.R"):skeleton.Bone("upperarm.r");rightElbow=mage!=null?mage.Bone("Forearm.R"):skeleton.Bone("lowerarm.r");rightWrist=mage!=null?mage.Bone("Hand.R"):skeleton.Bone("hand.r");leftUpper=mage!=null?mage.Bone("UpperArm.L"):skeleton.Bone("upperarm.l");leftElbow=mage!=null?mage.Bone("Forearm.L"):skeleton.Bone("lowerarm.l");leftWrist=mage!=null?mage.Bone("Hand.L"):skeleton.Bone("hand.l");chest=mage!=null?mage.Bone("Chest"):skeleton.Bone("chest");
            rightPalm=rightWrist?.Find("ERA palm socket");leftPalm=leftWrist?.Find("ERA palm socket");
            if(rightPalm==null||leftPalm==null||points==null||points.profile==null)return;
            modified=new[]{rightUpper,rightElbow,rightWrist,leftUpper,leftElbow,leftWrist};before=new Quaternion[modified.Length];skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
            // Keep the visual outside either IK chain: solving the supporting hand must
            // never move the weapon that supplies that hand's target.
            transform.SetParent(actor,true);
            if(skin!=null){leftFingers=skin.sharedMesh.GetBlendShapeIndex("Grip.l");rightFingers=skin.sharedMesh.GetBlendShapeIndex("Grip.r");}initialized=true;
        }
        void Update(){if(applied&&GameApp.Instance?.WorldPaused!=true&&skeleton?.PoseFrozen!=true)Restore();}
        void Restore(){if(!applied||modified==null)return;for(int i=0;i<modified.Length;i++)if(modified[i]!=null)modified[i].localRotation=before[i];applied=false;}
        void LateUpdate()
        {
            var app=GameApp.Instance;var p=app?.State?.Player(owner);if(p==null||actor==null||hand==null||app.WorldPaused)return;Bind();if(!initialized||skeleton?.PoseFrozen==true)return;
            if(mage!=null&&kind==WeaponKind.Aura)
            {
                bool channel=p.castTime>0||p.auraSurge>0;
                foreach(var renderer in GetComponentsInChildren<Renderer>())renderer.enabled=!channel;
                if(channel)return;
            }
            for(int i=0;i<modified.Length;i++)before[i]=modified[i].localRotation;applied=true;
            presentation?.BindOwner(owner);var sample=presentation!=null?presentation.Sample:default;var profile=points.profile;
            float yaw=p.weaponPoseTime>0?p.weaponAimYaw:actor.eulerAngles.y;if(presentation!=null&&presentation.Anticipation>.02f)yaw=presentation.AimYaw;
            var aim=Quaternion.Euler(0,yaw,0);var target=aim*Quaternion.Euler(profile.aimEuler+sample.euler);
            transform.rotation=Quaternion.Slerp(transform.rotation,target,1-Mathf.Exp(-38*PresentationMotion.Delta));
            bool left=profile.primaryLeft;var primaryPalm=left?leftPalm:rightPalm;var secondaryPalm=left?rightPalm:leftPalm;
            if(profile.twoHanded&&!p.downed){Vector3 goal=chest.position+actor.rotation*profile.holdFromChest+aim*Vector3.ClampMagnitude(sample.offset,.17f);SolveHand(left,goal,transform.rotation);}
            AlignWrist(left,transform.rotation);AlignWeapon(primaryPalm);
            if(profile.twoHanded&&!p.downed)
            {
                for(int i=0;i<3;i++)
                {
                    var upper=left?rightUpper:leftUpper;var elbow=left?rightElbow:leftElbow;var wrist=left?rightWrist:leftWrist;
                    Vector3 palmOffset=secondaryPalm.position-wrist.position;Vector3 targetWrist=points.secondary.position-palmOffset;
                    float reach=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position)-.004f;var delta=targetWrist-upper.position;
                    if(delta.magnitude>reach){Vector3 shift=-delta.normalized*(delta.magnitude-reach);SolveHand(left,primaryPalm.position+shift,transform.rotation);AlignWrist(left,transform.rotation);AlignWeapon(primaryPalm);}
                    SolveHand(!left,points.secondary.position,transform.rotation);AlignWrist(!left,transform.rotation);
                }
            }
            PrimaryError=Vector3.Distance(points.primary.position,primaryPalm.position);SecondaryError=profile.twoHanded&&!p.downed?Vector3.Distance(points.secondary.position,secondaryPalm.position):0;
            if(skin!=null){if(leftFingers>=0)skin.SetBlendShapeWeight(leftFingers,(left||profile.twoHanded?profile.fingerClosure:0)*100);if(rightFingers>=0)skin.SetBlendShapeWeight(rightFingers,(!left||profile.twoHanded?profile.fingerClosure:0)*100);}
        }
        void AlignWeapon(Transform palm){transform.position+=palm.position-points.primary.position;}
        void AlignWrist(bool left,Quaternion rotation){var wrist=left?leftWrist:rightWrist;var palm=left?leftPalm:rightPalm;wrist.rotation=rotation*Quaternion.Inverse(palm.localRotation);}
        void SolveHand(bool left,Vector3 palmTarget,Quaternion rotation)
        {
            var upper=left?leftUpper:rightUpper;var elbow=left?leftElbow:rightElbow;var wrist=left?leftWrist:rightWrist;var palm=left?leftPalm:rightPalm;
            Quaternion wristRotation=rotation*Quaternion.Inverse(palm.localRotation);Vector3 offset=wristRotation*Vector3.Scale(palm.localPosition,wrist.lossyScale);
            Vector3 pole=upper.position+actor.right*(left?-.5f:.5f)-actor.up*.6f-actor.forward*.15f;
            ArmPoseIK.Solve(upper,elbow,wrist,palmTarget-offset,pole);wrist.rotation=wristRotation;
        }
        void OnDestroy(){Restore();if(skin!=null){if(leftFingers>=0)skin.SetBlendShapeWeight(leftFingers,0);if(rightFingers>=0)skin.SetBlendShapeWeight(rightFingers,0);}}
        void OnDisable(){Restore();}
    }
}
