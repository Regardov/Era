using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(60)]
    public sealed class SkeletalActor:MonoBehaviour
    {
        public Animation animationPlayer;
        public Transform hips,spine,leftThigh,rightThigh,leftKnee,rightKnee,rightHand;
        string locomotion="",upper="";float actionTime,crouch,landingTime;int attackSerial,castSerial,jumpSerial,landSerial;float previousAttack;bool wantsCrouch;
        Transform rigBone,leftArm,rightArm;Vector3 rigScale=Vector3.one;
        bool heroPose,airborne;float airBlend,verticalPose,runPose,takeoffTime;
        float slide,aimTwist,draw;
        float enemyWindup;
        public float JumpBlend=>airBlend;
        public Transform Bone(string name){foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        public void Bind(){hips=Bone("hips");spine=Bone("spine");leftThigh=Bone("upperleg.l");rightThigh=Bone("upperleg.r");leftKnee=Bone("lowerleg.l");rightKnee=Bone("lowerleg.r");rightHand=Bone("handslot.r");}
        void Awake(){if(animationPlayer==null)animationPlayer=GetComponent<Animation>();rigBone=Bone("Rig");if(rigBone!=null)rigScale=rigBone.localScale;leftArm=Bone("upperarm.l");rightArm=Bone("upperarm.r");Stand();animationPlayer.Play(Clip("Idle"));animationPlayer.Sample();}
        string Clip(string name)=>animationPlayer[name]!=null?name:animationPlayer["Idle"]!=null?"Idle":animationPlayer.clip.name;
        void Base(string name,float speed=1)
        {name=Clip(name);if(locomotion!=name){animationPlayer.CrossFade(name,.12f);locomotion=name;}animationPlayer[name].speed=speed;}
        void Action(string name,float hold=.6f)
        {
            name=Clip(name);var state=animationPlayer[name];state.layer=1;if(spine!=null)state.AddMixingTransform(spine,true);state.blendMode=AnimationBlendMode.Blend;state.speed=1.2f;state.time=0;animationPlayer.CrossFade(name,.07f);upper=name;actionTime=hold;
        }
        public void Stand(){if(animationPlayer==null)return;animationPlayer.enabled=true;wantsCrouch=false;Base("Idle");}
        public void Hero(PlayerState p)
        {
            if(GameApp.Instance?.WorldPaused==true){animationPlayer.enabled=false;return;}animationPlayer.enabled=true;wantsCrouch=p.crouching;
            float speed=new Vector2(p.velocity.x,p.velocity.z).magnitude;
            heroPose=!p.downed;airborne=!p.grounded;verticalPose=p.verticalSpeed;runPose=Mathf.Clamp01(speed/7.8f);
            slide=Mathf.Clamp01(p.hitSlide/.24f);aimTwist=p.weaponPoseTime>0?Mathf.Clamp(Mathf.DeltaAngle(p.yaw,p.weaponAimYaw),-60,60):0;draw=p.lastWeapon==(int)WeaponKind.Bow?Mathf.Sin(Mathf.Clamp01(p.weaponPoseTime/.32f)*Mathf.PI):0;
            if(p.downed){wantsCrouch=false;Base("Death_A_Pose");return;}
            if(p.landSerial!=landSerial)landingTime=.28f;
            if(p.jumpSerial!=jumpSerial){animationPlayer.Rewind(Clip("Jump_Start"));takeoffTime=.12f;}
            if(slide>0)Base("Hit_A",.8f);
            else if(p.dashTime>0)Base("Dodge_Forward",1.6f);
            else if(!p.grounded)Base(p.verticalSpeed>4?"Jump_Start":"Jump_Idle");
            else if(landingTime>0){Base("Jump_Land",1.15f);}
            else Base(speed>.2f?(p.crouching?"Walking_A":"Running_A"):"Idle",speed>.2f?Mathf.Clamp(speed/(p.crouching?2.8f:5.2f),.6f,1.65f):1);
            if(p.chargingShrine>=0){if(upper!="Spellcasting")Action("Spellcasting",10);actionTime=.3f;}
            else if(p.castSerial!=castSerial)Action(p.castKind==0?"Spellcast_Shoot":"Spellcast_Raise",.65f);
            else if(p.weaponCastSerial!=attackSerial)Action(p.lastWeapon==(int)WeaponKind.Axe?"1H_Melee_Attack_Chop":p.lastWeapon==(int)WeaponKind.Spear?"1H_Melee_Attack_Stab":p.lastWeapon==(int)WeaponKind.Bow?"2H_Ranged_Shoot":"Spellcast_Shoot",.48f);
            attackSerial=p.weaponCastSerial;castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;
        }
        public void Enemy(float speed,bool frozen,float windup,float attackTimer)
        {
            heroPose=false;
            wantsCrouch=false;animationPlayer.enabled=!frozen;if(frozen)return;
            Base(windup>0?"Spellcasting":speed>.05f?"Walking_A":"Idle",speed>.05f?Mathf.Clamp(speed/2,.6f,1.7f):1);
            if(enemyWindup>0&&windup<=0)Action("Unarmed_Melee_Attack_Punch_A",.5f);enemyWindup=windup;previousAttack=attackTimer;
        }
        public void Hit(){if(GameApp.Instance?.WorldPaused!=true)Action("Hit_A",.17f);}
        public void Die(){animationPlayer.enabled=true;animationPlayer.Stop();animationPlayer.Play(Clip("Death_A"));enabled=false;gameObject.AddComponent<CorpseFade>();}
        void LateUpdate()
        {
            if(GameApp.Instance?.WorldPaused==true||!animationPlayer.enabled)return;
            landingTime=Mathf.Max(0,landingTime-Time.deltaTime);
            takeoffTime=Mathf.Max(0,takeoffTime-Time.deltaTime);
            if(actionTime>0){actionTime-=Time.deltaTime;if(actionTime<=0&&upper.Length>0){animationPlayer.Blend(upper,0,.12f);upper="";}}
            crouch=Mathf.MoveTowards(crouch,wantsCrouch?1:0,Time.deltaTime*8);
            if(crouch>.001f&&hips!=null)
            {
                hips.localPosition+=Vector3.down*(.22f*crouch);spine.localRotation*=Quaternion.Euler(16*crouch,0,0);
                leftThigh.localRotation*=Quaternion.Euler(-65*crouch,0,0);rightThigh.localRotation*=Quaternion.Euler(-65*crouch,0,0);
                leftKnee.localRotation*=Quaternion.Euler(130*crouch,0,0);rightKnee.localRotation*=Quaternion.Euler(130*crouch,0,0);
            }
            if(heroPose&&hips!=null)
            {
                spine.localRotation*=Quaternion.Euler(-slide*12,aimTwist*.65f,0);
                leftKnee.localRotation*=Quaternion.Euler(slide*22,0,0);rightKnee.localRotation*=Quaternion.Euler(slide*15,0,0);
                if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(-draw*8,draw*7,0);
                if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(-draw*4,0,-draw*5);
                airBlend=Mathf.MoveTowards(airBlend,airborne?1:0,Time.deltaTime*9);
                float rising=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-5,8,verticalPose));
                float tuck=airBlend*(.35f+rising*.65f),impact=airborne?0:Mathf.Sin((1-landingTime/.28f)*Mathf.PI)*Mathf.Clamp01(landingTime/.08f);
                float launch=takeoffTime/.12f;
                hips.localPosition+=Vector3.up*(airBlend*.055f-impact*.16f-launch*.035f);
                spine.localRotation*=Quaternion.Euler(airBlend*(runPose*9-rising*6)+impact*14,0,0);
                leftThigh.localRotation*=Quaternion.Euler(-tuck*21-impact*19,0,airBlend*-3);
                rightThigh.localRotation*=Quaternion.Euler(-tuck*14-impact*19,0,airBlend*3);
                leftKnee.localRotation*=Quaternion.Euler(tuck*38+impact*34,0,0);
                rightKnee.localRotation*=Quaternion.Euler(tuck*29+impact*34,0,0);
                if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(-airBlend*17-impact*9,0,-airBlend*13);
                if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(-airBlend*12-impact*9,0,airBlend*13);
                if(rigBone!=null)rigBone.localScale=Vector3.Scale(rigScale,new Vector3(1+impact*.035f-airBlend*.012f,1-impact*.05f+airBlend*.035f,1+impact*.035f));
            }
            else if(enemyWindup>0&&spine!=null)
            {
                float charge=Mathf.Clamp01(enemyWindup/.5f);spine.localRotation*=Quaternion.Euler(-22*charge,0,-8*charge);
                if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(-85*charge,0,15*charge);
                if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(-55*charge,0,-15*charge);
            }
        }
    }
    public sealed class CorpseFade:MonoBehaviour
    {
        float time;Vector3 scale;
        void Start(){scale=transform.localScale;}
        void Update(){time+=Time.deltaTime;if(time>1.1f){transform.position-=Vector3.up*Time.deltaTime*.8f;transform.localScale=scale*Mathf.Lerp(1,0,(time-1.1f)/.6f);}if(time>1.7f)Destroy(gameObject);}
    }
}
