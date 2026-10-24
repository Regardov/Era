using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(60)]
    public sealed class SkeletalActor:MonoBehaviour
    {
        public Animation animationPlayer;
        public Transform hips,spine,leftThigh,rightThigh,leftKnee,rightKnee,rightHand;
        string locomotion="",upper="";
        readonly HashSet<string> mixed=new HashSet<string>();
        float actionTime,crouch,landingTime,takeoffTime,poseClock,airBlend,verticalPose,runPose,slide,aimTwist,draw;
        float smoothedSpeed,lastSpeed,lastYaw,turnLean,accelLean,hitTime,freezeTime,previousWindup,windupDuration,attackRecovery,charge,arrival;
        float previousHp=-1,spawnDuration;
        int attackSerial,castSerial,jumpSerial,landSerial,bossStage,attackStyle;
        bool wantsCrouch,heroPose,airborne,initialized,enemyBound,dead,frozen,downed;
        Transform rigBone,leftArm,rightArm,head;
        Vector3 rigScale=Vector3.one,hitDirection;
        ActorMotionProfile profile;
        EnemyKind enemyKind;
        public float JumpBlend=>airBlend;
        public float CrouchBlend=>crouch;
        public bool GroundedPose=>!dead&&!downed&&!airborne&&(heroPose||locomotion=="Idle");
        public float WindupBlend=>charge;
        public float RecoveryBlend=>attackRecovery;
        public bool PoseFrozen=>frozen||freezeTime>0;
        public Transform Bone(string name){foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        public void Bind(){hips=Bone("hips");spine=Bone("spine");leftThigh=Bone("upperleg.l");rightThigh=Bone("upperleg.r");leftKnee=Bone("lowerleg.l");rightKnee=Bone("lowerleg.r");rightHand=Bone("handslot.r");}
        void Awake(){Initialize();Stand();if(animationPlayer!=null){animationPlayer.Play(Clip("Idle"));animationPlayer.Sample();}}
        void Initialize()
        {
            if(initialized)return;
            if(animationPlayer==null)animationPlayer=GetComponent<Animation>();
            if(hips==null)Bind();
            rigBone=Bone("Rig");if(rigBone!=null)rigScale=rigBone.localScale;
            leftArm=Bone("upperarm.l");rightArm=Bone("upperarm.r");head=Bone("head");
            poseClock=(GetInstanceID()&255)*.03125f;lastYaw=transform.eulerAngles.y;
            profile=ActorMotionProfile.For(EnemyKind.Stalker);initialized=true;
        }
        string Clip(string name)
        {if(animationPlayer==null)return "";return animationPlayer[name]!=null?name:animationPlayer["Idle"]!=null?"Idle":animationPlayer.clip!=null?animationPlayer.clip.name:"";}
        void Base(string name,float speed=1,float fade=.16f)
        {
            if(animationPlayer==null)return;name=Clip(name);if(name.Length==0)return;
            var state=animationPlayer[name];state.layer=0;
            if(locomotion!=name)
            {
                // Keep the footfall phase when changing walk/run; one-shot states restart once.
                float phase=locomotion.Length>0&&animationPlayer[locomotion]!=null?animationPlayer[locomotion].normalizedTime%1:0;
                state.normalizedTime=name.Contains("Walking")||name.Contains("Running")?phase:0;
                animationPlayer.CrossFade(name,fade,PlayMode.StopSameLayer);locomotion=name;
            }
            state.speed=ActorMotionMath.Damp(state.speed,speed,12,ActorMotionMath.Delta);
        }
        void Action(string name,float hold=.6f)
        {
            if(animationPlayer==null||dead)return;name=Clip(name);if(name.Length==0||name=="Idle")return;
            var state=animationPlayer[name];state.layer=1;
            if(spine!=null&&mixed.Add(name))state.AddMixingTransform(spine,true);
            state.blendMode=AnimationBlendMode.Blend;state.speed=Mathf.Clamp(state.length/Mathf.Max(.12f,hold),.6f,2.6f);state.time=0;
            animationPlayer.CrossFade(name,.055f,PlayMode.StopSameLayer);upper=name;actionTime=hold;
        }
        void ClearUpper(float duration=.14f)
        {if(upper.Length>0&&animationPlayer!=null)animationPlayer.Blend(upper,0,duration);upper="";actionTime=0;}
        public void Stand()
        {
            Initialize();if(animationPlayer==null||dead)return;
            animationPlayer.enabled=true;wantsCrouch=false;heroPose=true;airborne=false;downed=false;runPose=0;
            Base("Idle",1,.22f);
        }
        public void Hero(PlayerState p)
        {
            Initialize();if(animationPlayer==null||dead)return;
            if(GameApp.Instance?.WorldPaused==true){animationPlayer.enabled=false;return;}
            animationPlayer.enabled=freezeTime<=0;frozen=false;wantsCrouch=p.crouching;
            float dt=ActorMotionMath.Delta,speed=new Vector2(p.velocity.x,p.velocity.z).magnitude;
            heroPose=!p.downed;downed=p.downed;airborne=!p.grounded;verticalPose=p.verticalSpeed;
            smoothedSpeed=ActorMotionMath.Damp(smoothedSpeed,speed,12,dt);runPose=Mathf.Clamp01(smoothedSpeed/7.8f);
            if(dt>0)
            {
                float turn=Mathf.DeltaAngle(lastYaw,p.yaw)/dt;
                turnLean=ActorMotionMath.Damp(turnLean,Mathf.Clamp(turn*.018f,-7,7)*runPose,9,dt);
                accelLean=ActorMotionMath.Damp(accelLean,Mathf.Clamp((speed-lastSpeed)/dt*.18f,-7,8),7,dt);
            }
            lastYaw=p.yaw;lastSpeed=speed;
            slide=ActorMotionMath.Damp(slide,Mathf.Clamp01(p.hitSlide/.24f),18,dt);
            aimTwist=ActorMotionMath.Damp(aimTwist,p.weaponPoseTime>0?Mathf.Clamp(Mathf.DeltaAngle(p.yaw,p.weaponAimYaw),-60,60):0,20,dt);
            draw=ActorMotionMath.Damp(draw,p.lastWeapon==(int)WeaponKind.Bow?Mathf.Sin(Mathf.Clamp01(p.weaponPoseTime/.32f)*Mathf.PI):0,24,dt);
            if(previousHp>=0&&p.hp<previousHp)Hit(p.knockback.sqrMagnitude>.001f?p.knockback:-transform.forward);
            previousHp=p.hp;
            if(p.downed)
            {wantsCrouch=false;ClearUpper();Base("Death_A_Pose",1,.24f);attackSerial=p.weaponCastSerial;castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;return;}
            if(p.landSerial!=landSerial)landingTime=.32f;
            if(p.jumpSerial!=jumpSerial){animationPlayer.Rewind(Clip("Jump_Start"));takeoffTime=.14f;}
            if(p.dashTime>0)Base("Dodge_Forward",1.6f,.07f);
            else if(!p.grounded)Base(p.verticalSpeed>4?"Jump_Start":"Jump_Idle",1,.1f);
            else if(landingTime>.12f)Base("Jump_Land",1.2f,.065f);
            else Base(smoothedSpeed>.22f?(p.crouching||smoothedSpeed<3.3f?"Walking_A":"Running_A"):"Idle",smoothedSpeed>.22f?Mathf.Clamp(smoothedSpeed/(p.crouching?2.8f:smoothedSpeed<3.3f?2.2f:5.2f),.55f,1.65f):1);
            if(p.chargingShrine>=0){if(upper!="Spellcasting")Action("Spellcasting",.85f);actionTime=.3f;}
            else if(p.castSerial!=castSerial)Action(p.castKind==0?"Spellcast_Shoot":"Spellcast_Raise",.65f);
            else if(p.weaponCastSerial!=attackSerial)
            {
                string clip=p.lastWeapon==(int)WeaponKind.Axe||p.lastWeapon==(int)WeaponKind.Sword?"1H_Melee_Attack_Chop":p.lastWeapon==(int)WeaponKind.Spear?"1H_Melee_Attack_Stab":p.lastWeapon==(int)WeaponKind.Bow?"2H_Ranged_Shoot":"Spellcast_Shoot";
                Action(clip,p.lastWeapon==(int)WeaponKind.Axe?.43f:p.lastWeapon==(int)WeaponKind.Spear?.31f:.38f);
            }
            attackSerial=p.weaponCastSerial;castSerial=p.castSerial;jumpSerial=p.jumpSerial;landSerial=p.landSerial;
        }
        public void Enemy(EnemyState enemy,float speed)
        {
            Initialize();if(animationPlayer==null||dead)return;
            if(!enemyBound||enemyKind!=enemy.kind){profile=ActorMotionProfile.For(enemy.kind);enemyKind=enemy.kind;enemyBound=true;}
            heroPose=false;downed=false;airborne=false;wantsCrouch=false;bossStage=enemy.bossStage;attackStyle=enemy.attackStyle;
            frozen=enemy.frozen>0;animationPlayer.enabled=!frozen&&freezeTime<=0&&GameApp.Instance?.WorldPaused!=true;
            if(frozen||GameApp.Instance?.WorldPaused==true)return;
            float dt=ActorMotionMath.Delta,remaining=Mathf.Max(enemy.windup,enemy.meleeWindup);
            if(enemy.spawnGrace>0)spawnDuration=Mathf.Max(spawnDuration,enemy.spawnGrace);
            arrival=spawnDuration>0?1-ActorMotionMath.Ease(1-enemy.spawnGrace/spawnDuration):0;
            if(remaining>previousWindup+.02f)windupDuration=enemy.meleeWindup>0?Mathf.Max(enemy.attackDuration,remaining):remaining;
            bool released=previousWindup>0&&remaining<=0;
            if(released){Action(enemy.kind==EnemyKind.Warden||attackStyle==2?"Spellcast_Raise":attackStyle==3?"1H_Melee_Attack_Chop":"Unarmed_Melee_Attack_Punch_A",enemy.kind==EnemyKind.Warden?.7f:.42f);attackRecovery=1;}
            previousWindup=remaining;
            charge=ActorMotionMath.Anticipation(remaining,windupDuration);
            attackRecovery=Mathf.Max(ActorMotionMath.Recovery(enemy.recovery,attackStyle==3?.8f:.42f),Mathf.Max(0,attackRecovery-dt/(enemy.kind==EnemyKind.Warden?.72f:.42f)));
            float moving=remaining>0||enemy.recovery>0||enemy.spawnGrace>0?0:speed;
            smoothedSpeed=ActorMotionMath.Damp(smoothedSpeed,moving,10,dt);runPose=Mathf.Clamp01(smoothedSpeed/Mathf.Max(.1f,EnemyCatalog.Speed(enemy.kind)));
            // Windup keeps its own continuous pose; frozen actors retain the exact sampled pose.
            Base(remaining>0?"Idle":smoothedSpeed>.18f?(enemy.kind==EnemyKind.Stalker?"Running_A":"Walking_A"):"Idle",smoothedSpeed>.18f?Mathf.Clamp(smoothedSpeed/(enemy.kind==EnemyKind.Stalker?3.1f:1.8f),.55f,1.7f):1);
        }
        // Kept for older callers; the state overload carries complete attack phase information.
        public void Enemy(float speed,bool isFrozen,float windup,float attackTimer)
        {Enemy(new EnemyState{kind=enemyKind,speed=speed,frozen=isFrozen?1:0,windup=windup,attackTimer=attackTimer},speed);}
        public void Hit(){Hit(-transform.forward);}
        public void Hit(Vector3 direction)
        {
            if(dead||GameApp.Instance?.WorldPaused==true)return;
            hitDirection=transform.InverseTransformDirection(direction.sqrMagnitude>.001f?direction.normalized:-transform.forward);
            hitTime=.24f;
        }
        public void FreezePose(float seconds)
        {if(!dead&&GameApp.Instance?.WorldPaused!=true)freezeTime=Mathf.Max(freezeTime,Mathf.Clamp(seconds,0,.045f));}
        public void Die()
        {
            if(dead)return;dead=true;ClearUpper(0);animationPlayer.enabled=true;animationPlayer.Stop();
            string death=Clip("Death_A");if(death.Length>0){animationPlayer[death].layer=0;animationPlayer[death].speed=1;animationPlayer[death].time=0;animationPlayer.Play(death);}
            if(rigBone!=null)rigBone.localScale=rigScale;
            CorpseFade.Begin(gameObject,enemyBound?profile.death:ActorDeathStyle.Collapse,null,null,true);
            enabled=false;
        }
        void LateUpdate()
        {
            if(animationPlayer==null||dead)return;
            float dt=ActorMotionMath.Delta;if(dt<=0){animationPlayer.enabled=false;return;}
            if(frozen){animationPlayer.enabled=false;return;}
            if(freezeTime>0){freezeTime=Mathf.Max(0,freezeTime-dt);animationPlayer.enabled=false;return;}
            animationPlayer.enabled=true;poseClock+=dt;
            landingTime=Mathf.Max(0,landingTime-dt);takeoffTime=Mathf.Max(0,takeoffTime-dt);hitTime=Mathf.Max(0,hitTime-dt);
            if(actionTime>0){actionTime-=dt;if(actionTime<=0)ClearUpper();}
            crouch=ActorMotionMath.Damp(crouch,wantsCrouch?1:0,14,dt);
            if(hips==null||spine==null||downed)return;
            float breath=Mathf.Sin(poseClock*(heroPose?2.2f:profile.breath));
            float intensity=.45f+.55f*PresentationMotion.Intensity;
            float hit=ActorMotionMath.Recovery(hitTime,.24f)*intensity;
            spine.localRotation*=Quaternion.Euler(breath*(1-runPose)*.65f-hitDirection.z*hit*9,hitDirection.x*hit*5,-hitDirection.x*hit*8);
            if(head!=null)head.localRotation*=Quaternion.Euler(-breath*.4f+hitDirection.z*hit*4,Mathf.Sin(poseClock*.57f)*.6f*(1-runPose),hitDirection.x*hit*3);
            if(heroPose)
            {
                spine.localRotation*=Quaternion.Euler(-slide*12+accelLean*.65f,aimTwist*.65f,-turnLean);
                if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(-draw*8,draw*7,0);
                if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(-draw*4,0,-draw*5);
                airBlend=ActorMotionMath.Damp(airBlend,airborne?1:0,14,dt);
                float rising=ActorMotionMath.Ease(Mathf.InverseLerp(-5,8,verticalPose));
                float tuck=airBlend*(.35f+rising*.65f),impact=airborne?0:Mathf.Sin((1-landingTime/.32f)*Mathf.PI)*Mathf.Clamp01(landingTime/.10f);
                float launch=takeoffTime/.14f;
                hips.localPosition+=Vector3.up*(airBlend*.055f-impact*.13f-launch*.035f);
                spine.localRotation*=Quaternion.Euler(airBlend*(runPose*9-rising*6)+impact*14,0,0);
                Rotate(leftThigh,new Vector3(-tuck*21-impact*19,0,airBlend*-3));Rotate(rightThigh,new Vector3(-tuck*14-impact*19,0,airBlend*3));
                Rotate(leftKnee,new Vector3(tuck*38+impact*34+slide*18,0,0));Rotate(rightKnee,new Vector3(tuck*29+impact*34+slide*12,0,0));
                Rotate(leftArm,new Vector3(-airBlend*17-impact*9,0,-airBlend*13));Rotate(rightArm,new Vector3(-airBlend*12-impact*9,0,airBlend*13));
                if(rigBone!=null)rigBone.localScale=Vector3.Scale(rigScale,new Vector3(1+impact*.022f-airBlend*.008f,1-impact*.035f+airBlend*.022f,1+impact*.022f));
            }
            else
            {
                bool boss=enemyKind==EnemyKind.Warden;
                float weight=boss?1.15f:profile.weight,prep=charge,release=attackRecovery;
                float side=boss&&(bossStage==2||bossStage==6)?1:boss&&bossStage==5?-1:0;
                float raising=boss&&(bossStage==3||bossStage==7)||attackStyle==2?1:0;
                spine.localRotation*=Quaternion.Euler(-prep*(18+weight*7)+release*(21+weight*8),-prep*side*24+release*side*30,Mathf.Sin(poseClock*profile.cadence)*profile.sway*runPose);
                Rotate(rightArm,new Vector3(-prep*(65+weight*30)+release*(raising>0?-28:32),side*prep*18,prep*(15+raising*35)));
                Rotate(leftArm,new Vector3(-prep*(45+weight*35)+release*(raising>0?-28:20),-side*prep*12,-prep*(15+raising*35)));
                hips.localPosition+=Vector3.up*(-prep*.06f*weight+breath*.006f-arrival*.12f);
                if(rigBone!=null)rigBone.localScale=Vector3.Scale(rigScale,new Vector3(1+prep*.018f,1-prep*.027f,1+prep*.018f));
            }
        }
        static void Rotate(Transform bone,Vector3 degrees){if(bone!=null)bone.localRotation*=Quaternion.Euler(degrees);}
    }

    public sealed class CorpseFade:MonoBehaviour
    {
        const int Capacity=32;
        static readonly List<CorpseFade> active=new List<CorpseFade>();
        float time,duration;ActorDeathStyle style;Vector3 scale,position;Quaternion rotation;
        Transform body;Vector3 bodyPosition,bodyScale;Quaternion bodyRotation;
        Transform[] joints;Quaternion[] rotations;Vector3[] positions;
        Animation player;bool authored,ready;
        public static int ActiveCount=>active.Count;
        public static CorpseFade Begin(GameObject root,ActorDeathStyle style,Transform body,IList<Transform> limbs,bool authored)
        {
            var previous=root.GetComponent<CorpseFade>();if(previous!=null)return previous;
            for(int i=active.Count-1;i>=0;i--)if(active[i]==null)active.RemoveAt(i);
            while(active.Count>=Capacity){var oldest=active[0];active.RemoveAt(0);if(oldest!=null)Destroy(oldest.gameObject);}
            var fade=root.AddComponent<CorpseFade>();fade.style=style;fade.body=body;fade.authored=authored;
            fade.scale=root.transform.localScale;fade.position=root.transform.position;fade.rotation=root.transform.rotation;
            fade.duration=style==ActorDeathStyle.Sovereign?2.2f:style==ActorDeathStyle.Evaporate?.85f:1.35f;
            if(body!=null){fade.bodyPosition=body.localPosition;fade.bodyScale=body.localScale;fade.bodyRotation=body.localRotation;}
            if(limbs!=null)
            {
                int count=Mathf.Min(12,limbs.Count);fade.joints=new Transform[count];fade.rotations=new Quaternion[count];fade.positions=new Vector3[count];
                for(int i=0;i<count;i++){fade.joints[i]=limbs[i];fade.rotations[i]=limbs[i].localRotation;fade.positions[i]=limbs[i].localPosition;}
            }
            fade.player=root.GetComponent<Animation>();fade.ready=true;active.Add(fade);return fade;
        }
        void Update()
        {
            if(!ready)return;float dt=ActorMotionMath.Delta;if(player!=null)player.enabled=dt>0;
            if(dt<=0)return;time+=dt;
            float progress=Mathf.Clamp01(time/duration),fall=ActorMotionMath.Ease(time/(style==ActorDeathStyle.Sovereign?1:.42f));
            float vanish=ActorMotionMath.Ease(Mathf.InverseLerp(style==ActorDeathStyle.Evaporate?.18f:.65f,1,progress));
            transform.localScale=scale*(1-vanish);
            if(style==ActorDeathStyle.Evaporate)transform.position=position+Vector3.up*(progress*.65f);
            else if(style==ActorDeathStyle.WingFall)transform.position=position-Vector3.up*(fall*.8f);
            else transform.position=position-Vector3.up*(vanish*.28f);
            if(!authored&&body!=null)
            {
                if(style==ActorDeathStyle.Deflate)
                {body.localScale=Vector3.Scale(bodyScale,new Vector3(1+fall*.4f,1-fall*.86f,1+fall*.4f));body.localPosition=bodyPosition;}
                else
                {body.localRotation=bodyRotation*Quaternion.Euler(style==ActorDeathStyle.Shatter?fall*32:fall*72,0,style==ActorDeathStyle.WingFall?fall*24:fall*11);body.localPosition=bodyPosition+Vector3.down*fall*.12f;}
                if(joints!=null)for(int i=0;i<joints.Length;i++)if(joints[i]!=null)
                {
                    float side=i%2==0?-1:1;
                    joints[i].localRotation=rotations[i]*Quaternion.Euler(fall*(style==ActorDeathStyle.Shatter?35:18),side*fall*13,side*fall*(style==ActorDeathStyle.WingFall?55:30));
                    if(style==ActorDeathStyle.Shatter)joints[i].localPosition=positions[i]+new Vector3(side*.25f,-.35f,(i%3-1)*.15f)*fall;
                }
            }
            if(time>=duration)Destroy(gameObject);
        }
        void OnDestroy(){active.Remove(this);}
    }
}
