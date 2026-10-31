using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class CreatureMotion : MonoBehaviour
    {
        public EnemyKind kind;
        public Transform body;
        public List<Transform> limbs=new List<Transform>();
        public Vector3 baseScale=Vector3.one;
        public Vector3 basePosition;
        public float hit;
        public SkeletalActor skeletal;
        public GolemMotion golem;
        public static CreatureMotion Create(VisualFactory v,Transform parent,EnemyKind kind)
        {
            if(Application.isPlaying)
            {var prefab=Resources.Load<GameObject>("Creatures/"+kind);if(prefab!=null)return Instantiate(prefab,parent,false).GetComponent<CreatureMotion>();}
            if(kind>=EnemyKind.Skeleton||kind==EnemyKind.Brute)return RoguelikeCreatureArt.Create(v,parent,kind);
            var root=v.Group(kind.ToString(),parent);var m=root.gameObject.AddComponent<CreatureMotion>();m.kind=kind;m.body=v.Group("Living body",root);
            Color teal=new Color(0.40f,0.64f,0.62f),purple=new Color(0.57f,0.44f,0.62f),cream=new Color(0.88f,0.85f,0.69f);
            if(kind==EnemyKind.Wisp)
            {
                v.Ellipsoid("Wisp bell",m.body,new Vector3(0,0.98f,0),new Vector3(0.95f,1.08f,0.90f),teal);
                v.Ellipsoid("Wisp cap",m.body,new Vector3(0,1.30f,0),new Vector3(1.08f,0.45f,1.02f),teal);
                for(int i=0;i<5;i++)
                {float a=i*Mathf.PI*2/5;var limb=v.Group("Trailing petal",m.body,new Vector3(Mathf.Cos(a)*0.28f,0.65f,Mathf.Sin(a)*0.28f));v.Bone("Petal",limb,new Vector3(0,-0.21f,0),new Vector3(0.17f,0.24f,0.17f),teal);m.limbs.Add(limb);}
                Eyes(v,m.body,0.99f,0.43f,0.15f);
            }
            else if(kind==EnemyKind.Stalker)
            {
                v.Ellipsoid("Fox body",m.body,new Vector3(0,0.68f,-0.12f),new Vector3(0.67f,0.70f,1.32f),purple);
                v.Ellipsoid("Fox head",m.body,new Vector3(0,1.0f,0.53f),new Vector3(0.76f,0.68f,0.76f),purple);
                v.Ellipsoid("Muzzle",m.body,new Vector3(0,0.90f,0.88f),new Vector3(0.42f,0.30f,0.49f),cream);
                for(int s=-1;s<=1;s+=2){v.Cone("Ear",m.body,new Vector3(s*0.25f,1.24f,0.5f),0.15f,0.42f,purple,9);}
                Eyes(v,m.body,1.05f,0.85f,0.18f);
                var tail=v.Bone("Plume tail",m.body,new Vector3(0,0.86f,-0.98f),new Vector3(0.35f,0.55f,0.35f),cream);tail.transform.localRotation=Quaternion.Euler(45,0,0);
                for(int i=0;i<4;i++){var leg=v.Group("Paw joint",m.body,new Vector3(i%2==0?-0.25f:0.25f,0.65f,i<2?0.35f:-0.55f));v.Bone("Leg",leg,new Vector3(0,-0.25f,0),new Vector3(0.17f,0.27f,0.17f),purple);v.Ellipsoid("Paw",leg,new Vector3(0,-0.50f,0.06f),new Vector3(0.23f,0.16f,0.32f),cream);m.limbs.Add(leg);}
            }
            else
            {
                bool boss=kind==EnemyKind.Warden;Color flesh=boss?new Color(0.48f,0.57f,0.43f):new Color(0.68f,0.64f,0.49f);
                v.Ellipsoid("Round torso",m.body,new Vector3(0,1.02f,0),new Vector3(1.26f,1.45f,0.95f),flesh);
                v.Ellipsoid("Head",m.body,new Vector3(0,1.67f,0.12f),new Vector3(1.0f,0.78f,0.85f),flesh);
                v.Ellipsoid("Mushroom cap",m.body,new Vector3(0,2.04f,0.03f),new Vector3(1.86f,0.67f,1.52f),boss?new Color(0.55f,0.44f,0.61f):new Color(0.76f,0.47f,0.38f));
                for(int i=0;i<5;i++)v.Ellipsoid("Cap spot",m.body,new Vector3(Mathf.Cos(i*2.4f)*0.51f,2.33f,Mathf.Sin(i*2.4f)*0.38f),new Vector3(0.19f,0.06f,0.16f),cream);
                Eyes(v,m.body,1.69f,0.53f,0.21f);
                for(int i=0;i<4;i++)
                {bool arm=i<2;var joint=v.Group(arm?"Arm joint":"Leg joint",m.body,new Vector3(i%2==0?-1:1,arm?1.48f:0.62f,0));joint.localPosition=new Vector3((i%2==0?-1:1)*(arm?0.67f:0.32f),arm?1.48f:0.62f,0);v.Bone("Organic limb",joint,new Vector3(0,-0.32f,0),new Vector3(arm?0.38f:0.39f,0.40f,0.38f),flesh);m.limbs.Add(joint);}
                if(boss)
                {
                    for(int s=-1;s<=1;s+=2)
                    {v.Branch(m.body,new Vector3(s*0.5f,2.1f,0),new Vector3(s*0.85f,3.0f,0),0.16f,cream);v.Branch(m.body,new Vector3(s*0.72f,2.6f,0),new Vector3(s*1.25f,2.9f,0.1f),0.11f,cream);}
                }
                m.body.localScale=Vector3.one*(boss?2.2f:1.25f);
            }
            m.baseScale=m.body.localScale;return m;
        }
        static void Eyes(VisualFactory v,Transform body,float y,float z,float spread)
        {
            for(int s=-1;s<=1;s+=2)
            {v.Ellipsoid("Eye socket",body,new Vector3(s*spread,y,z),new Vector3(0.16f,0.20f,0.06f),new Color(0.17f,0.23f,0.24f));v.Ellipsoid("Eye light",body,new Vector3(s*spread,y,z+0.035f),new Vector3(0.055f,0.085f,0.025f),new Color(0.98f,0.78f,0.56f),true);}
        }
        bool poseBound,dying;
        float motionPhase,idleTime,moveBlend,windupMax,previousWindup,recoveryBlend,freezeTime,spawnMax;
        Quaternion bodyRotation;
        Quaternion[] limbRotations;
        Vector3[] limbPositions;
        Vector3 impactDirection;
        ActorMotionProfile profile;
        Transform secondary,headJoint;
        Quaternion secondaryRotation,headRotation;
        public float MovementBlend=>moveBlend;
        public float AttackPreparation{get;private set;}
        public float AttackRecovery=>recoveryBlend;
        void BindPose()
        {
            if(poseBound||body==null)return;
            profile=ActorMotionProfile.For(kind);baseScale=body.localScale;basePosition=body.localPosition;bodyRotation=body.localRotation;
            limbRotations=new Quaternion[limbs.Count];limbPositions=new Vector3[limbs.Count];
            for(int i=0;i<limbs.Count;i++){limbRotations[i]=limbs[i].localRotation;limbPositions[i]=limbs[i].localPosition;}
            foreach(var part in body.GetComponentsInChildren<Transform>(true))
            {
                if(part.name=="Plume tail")secondary=part;
                if(part.name=="Skull joint"||part.name=="Heavy head")headJoint=part;
            }
            if(secondary!=null)secondaryRotation=secondary.localRotation;if(headJoint!=null)headRotation=headJoint.localRotation;
            idleTime=(GetInstanceID()&255)*.03125f;motionPhase=idleTime;poseBound=true;
        }
        public void Hit(Vector3 direction)
        {
            if(dying||GameApp.Instance?.WorldPaused==true)return;
            impactDirection=transform.InverseTransformDirection(direction.sqrMagnitude>.001f?direction.normalized:-transform.forward);
            hit=1;skeletal?.Hit(direction);
        }
        public void FreezePose(float duration)
        {freezeTime=Mathf.Max(freezeTime,Mathf.Clamp(duration,0,.045f));skeletal?.FreezePose(duration);}
        public void Die()
        {
            if(dying)return;dying=true;
            if(skeletal!=null){skeletal.Die();return;}
            BindPose();CorpseFade.Begin(gameObject,profile.death,body,limbs,false);enabled=false;
        }
        public void Present(EnemyState enemy,float speed)
        {
            if(dying)return;
            if(golem!=null)
            {
                if(enemy.frozen>0||GameApp.Instance?.WorldPaused==true)return;
                float delta=ActorMotionMath.Delta;if(delta<=0)return;
                if(freezeTime>0){freezeTime=Mathf.Max(0,freezeTime-delta);return;}
                golem.Present(enemy,speed,delta,hit);hit=Mathf.Max(0,hit-delta*5.5f);return;
            }
            if(skeletal!=null){skeletal.Enemy(enemy,speed);return;}
            BindPose();if(!poseBound||enemy.frozen>0||GameApp.Instance?.WorldPaused==true)return;
            float dt=ActorMotionMath.Delta;if(dt<=0)return;
            if(freezeTime>0){freezeTime=Mathf.Max(0,freezeTime-dt);return;}
            idleTime+=dt;hit=Mathf.Max(0,hit-dt*5.5f);
            float remaining=Mathf.Max(enemy.meleeWindup,enemy.windup);
            if(remaining>previousWindup+.02f)windupMax=enemy.meleeWindup>0?Mathf.Max(enemy.attackDuration,remaining):remaining;
            if(previousWindup>0&&remaining<=0)recoveryBlend=1;
            previousWindup=remaining;
            AttackPreparation=ActorMotionMath.Anticipation(remaining,windupMax);
            recoveryBlend=Mathf.Max(ActorMotionMath.Recovery(enemy.recovery,enemy.attackStyle==3?.8f:.42f),Mathf.Max(0,recoveryBlend-dt/(enemy.kind==EnemyKind.Warden?.72f:.42f)));
            float desired=remaining>0||enemy.recovery>0||enemy.spawnGrace>0?0:Mathf.Clamp01(speed/Mathf.Max(.1f,EnemyCatalog.Speed(kind)));
            moveBlend=ActorMotionMath.Damp(moveBlend,desired,10,dt);
            bool flying=EnemyCatalog.Flying(kind),gel=kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime;
            motionPhase+=dt*profile.cadence*(flying?1:Mathf.Lerp(.12f,1,moveBlend));
            ActorMotionMath.AttackPose(remaining,windupMax,recoveryBlend,out float prep,out float strike);
            float step=Mathf.Sin(motionPhase),breath=Mathf.Sin(idleTime*profile.breath);
            float impact=ActorMotionMath.Ease(hit),weight=profile.weight;
            if(enemy.spawnGrace>0)spawnMax=Mathf.Max(spawnMax,enemy.spawnGrace);
            float arrival=spawnMax>0?1-ActorMotionMath.Ease(1-enemy.spawnGrace/spawnMax):0;
            float bob=flying?step*profile.bob:gel?Mathf.Max(0,step)*profile.bob*moveBlend:(Mathf.Abs(step)-.4f)*profile.bob*moveBlend;
            var offset=new Vector3(-impactDirection.x*impact*.035f,bob+breath*.008f*(1-moveBlend)-prep*.055f*weight-arrival*.13f,-prep*.035f+strike*.045f-impactDirection.z*impact*.035f);
            body.localPosition=basePosition+offset;
            float pitch=-prep*(18+weight*9)+strike*(22+weight*12)-impactDirection.z*impact*10;
            float yaw=enemy.attackStyle==0?-prep*23+strike*30:0;
            float roll=step*profile.sway*moveBlend-impactDirection.x*impact*9;
            if(flying){pitch+=moveBlend*9;roll+=Mathf.Sin(idleTime*1.7f)*1.5f;}
            body.localRotation=bodyRotation*Quaternion.Euler(pitch,yaw,roll);
            if(gel)
            {
                float stretch=step*moveBlend*.13f+breath*.015f-prep*.17f+strike*.12f-impact*.08f;
                body.localScale=Vector3.Scale(baseScale,new Vector3(1-stretch*.55f,1+stretch,1-stretch*.55f));
            }
            else body.localScale=Vector3.Scale(baseScale,new Vector3(1+impact*.016f+prep*.012f,1-prep*.025f-impact*.022f,1+impact*.016f+prep*.012f));
            for(int i=0;i<limbs.Count;i++)
            {
                var limb=limbs[i];if(limb==null)continue;
                bool wing=limb.name.StartsWith("Wing"),arm=limb.name.StartsWith("Arm"),petal=limb.name.StartsWith("Trailing"),paw=limb.name.StartsWith("Paw");
                bool left=limb.name.EndsWith("-1")||limb.localPosition.x<0;float side=left?-1:1;
                float gait=Mathf.Sin(motionPhase+(left?Mathf.PI:0)),swing=gait*profile.stride*moveBlend;
                Vector3 angles;
                if(wing)
                {
                    float flap=Mathf.Sin(motionPhase+(kind==EnemyKind.Gargoyle?.18f:0));
                    angles=new Vector3(prep*-12+strike*16,0,side*(flap*(kind==EnemyKind.Bat?42:28)-prep*34+strike*20));
                }
                else if(petal)angles=new Vector3(Mathf.Sin(idleTime*2.7f+i*.9f)*10-moveBlend*10,0,Mathf.Cos(idleTime*2.2f+i)*7);
                else if(paw)angles=new Vector3(Mathf.Sin(motionPhase+(i==0||i==3?0:Mathf.PI))*34*moveBlend-prep*14+strike*15,0,0);
                else if(arm)
                {
                    float raised=enemy.attackStyle==2?100:enemy.attackStyle==3?130:left?50:85;
                    angles=new Vector3(-swing*.72f-prep*raised+strike*(enemy.attackStyle==2?-20:62),prep*side*12,side*(breath*1.2f+prep*(enemy.attackStyle==2?32:15)));
                }
                else angles=new Vector3(swing-prep*6,0,-side*prep*3);
                limb.localRotation=limbRotations[i]*Quaternion.Euler(angles);
                // Foot lift is limited to the authored joint hierarchy; collision roots stay untouched.
                limb.localPosition=limbPositions[i]+(arm||wing||petal?Vector3.zero:Vector3.up*Mathf.Max(0,-gait)*.035f*moveBlend);
            }
            if(secondary!=null)secondary.localRotation=secondaryRotation*Quaternion.Euler(Mathf.Sin(idleTime*2.2f)*4+moveBlend*8,Mathf.Sin(motionPhase-.55f)*moveBlend*16,Mathf.Sin(idleTime*1.4f)*3);
            if(headJoint!=null)headJoint.localRotation=headRotation*Quaternion.Euler(-pitch*.32f,Mathf.Sin(idleTime*.8f)*1.5f*(1-moveBlend),-roll*.45f);
        }
        public void Pose(float time,bool frozen,float windup)
        {Present(new EnemyState{kind=kind,frozen=frozen?1:0,windup=windup},0);}
        public void CombatPose(EnemyState enemy,float time){Present(enemy,enemy.speed);}
    }
}
