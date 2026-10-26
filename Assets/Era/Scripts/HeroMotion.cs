using UnityEngine;

namespace Era
{
    // A jointed, code-authored animation rig: locomotion blends, jump tuck, landing spring and casting.
    public sealed class HeroMotion : MonoBehaviour
    {
        public Transform rig,hips,spine,head,leftArm,rightArm,leftElbow,rightElbow,leftThigh,rightThigh,leftKnee,rightKnee,cape;
        float stride,run,landing,lastGround=1;
        [SerializeField]Hero heroType;
        public Hero Hero=>heroType;
        public SkeletalActor skeletal;
        public ArcherAnimationDriver archer;
        public void BindArcher(ArcherAnimationDriver actor)
        {
            heroType=Hero.Ember;archer=actor;skeletal=null;
            rig=actor.transform;hips=actor.pelvis;spine=actor.spine;head=actor.head;
            leftArm=actor.Bone("UpperArm.L");rightArm=actor.Bone("UpperArm.R");
            leftElbow=actor.Bone("Hand.L");rightElbow=actor.Bone("Hand.R");
            leftThigh=actor.leftThigh;rightThigh=actor.rightThigh;leftKnee=actor.leftShin;rightKnee=actor.rightShin;
            cape=actor.Bone("CloakRoot")??spine;
        }
        public void BindImported(Hero hero,SkeletalActor actor)
        {
            heroType=hero;skeletal=actor;rig=actor.Bone("Rig");hips=actor.hips;spine=actor.spine;head=actor.Bone("head");leftArm=actor.Bone("upperarm.l");rightArm=actor.Bone("upperarm.r");leftElbow=actor.Bone("handslot.l");rightElbow=actor.rightHand;
            leftThigh=actor.leftThigh;rightThigh=actor.rightThigh;leftKnee=actor.leftKnee;rightKnee=actor.rightKnee;cape=actor.Bone("Rogue_Cape")??spine;
        }
        public static HeroMotion Create(VisualFactory v,Transform parent,Hero hero,string name)
        {
            if(Application.isPlaying)
            {
                var prefab=Resources.Load<GameObject>("Characters/"+hero);
                if(prefab!=null){var copy=Instantiate(prefab,parent,false);copy.name=name;return copy.GetComponent<HeroMotion>();}
            }
            var root=v.Group(name,parent);var m=root.gameObject.AddComponent<HeroMotion>();m.heroType=hero;
            Color accent=GameData.HeroColor(hero),cloth=hero==Hero.Lumen?new Color(.77f,.73f,.88f):hero==Hero.Ember?new Color(0.65f,0.36f,0.28f):new Color(0.31f,0.48f,0.51f);
            Color cream=new Color(0.88f,0.83f,0.70f),dark=new Color(0.25f,0.31f,0.31f),skin=new Color(0.88f,0.70f,0.54f);
            m.rig=v.Group("Animation Rig",root);m.hips=v.Group("Hips",m.rig,new Vector3(0,1.01f,0));
            v.Ellipsoid("Tunic skirt",m.hips,new Vector3(0,0.04f,0),new Vector3(0.74f,0.56f,0.58f),cloth);
            m.spine=v.Group("Spine",m.hips,new Vector3(0,0.12f,0));
            v.Ellipsoid("Jacket",m.spine,new Vector3(0,0.25f,0),new Vector3(0.76f,0.79f,0.58f),cloth);
            v.Ellipsoid("Scarf",m.spine,new Vector3(0,0.55f,0),new Vector3(0.72f,0.19f,0.61f),accent);
            v.Bone("Scarf tail",m.spine,new Vector3(-0.17f,0.30f,0.30f),new Vector3(0.14f,0.2f,0.06f),accent);
            m.cape=v.Group("Cape sway",m.spine,new Vector3(0,0.47f,-0.29f));
            var cloak=v.Cone("Short cloak",m.cape,new Vector3(0,-0.62f,0),0.41f,0.65f,Color.Lerp(cloth,cream,0.25f),12);cloak.transform.localScale=new Vector3(1,1,0.55f);
            m.head=v.Group("Head",m.spine,new Vector3(0,0.83f,0));
            v.Ellipsoid("Hood",m.head,Vector3.zero,new Vector3(0.74f,0.72f,0.69f),cloth);
            v.Ellipsoid("Face",m.head,new Vector3(0,-0.035f,0.225f),new Vector3(0.51f,0.48f,0.31f),skin);
            v.Ellipsoid("Nose",m.head,new Vector3(0,-0.07f,0.39f),new Vector3(0.11f,0.10f,0.09f),skin);
            for(int i=-1;i<=1;i+=2)
            {
                v.Ellipsoid("Eye",m.head,new Vector3(i*0.115f,0.015f,0.365f),new Vector3(0.055f,0.075f,0.025f),dark);
                v.Ellipsoid("Cheek",m.head,new Vector3(i*0.17f,-0.10f,0.351f),new Vector3(0.08f,0.04f,0.025f),new Color(0.87f,0.54f,0.46f));
            }
            if(hero==Hero.Lumen)
            {
                v.Ring("Lumen halo",m.head,.49f,new Color(1,.88f,.55f),.045f).transform.localPosition=Vector3.up*.52f;
                v.Ellipsoid("Light sigil",m.spine,new Vector3(0,.35f,.32f),Vector3.one*.22f,accent,true);
                for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;WorldVfx.Crystal(v,m.head,"Halo prism",new Vector3(Mathf.Cos(a)*.49f,.52f,Mathf.Sin(a)*.49f),.13f,GameData.AuraColors[i]);}
            }
            else if(hero==Hero.Echo)
            {
                v.Shape("Hat brim",PrimitiveType.Cylinder,m.head,new Vector3(0,0.28f,0),new Vector3(1.05f,0.045f,0.98f),cloth);
                var hat=v.Cone("Soft pointed hat",m.head,new Vector3(0,0.30f,0),0.38f,0.60f,cloth,16);hat.transform.localRotation=Quaternion.Euler(0,0,-11);
                v.Ellipsoid("Moon brooch",m.head,new Vector3(0,0.36f,0.38f),Vector3.one*0.12f,GameData.Mint,true);
            }
            else
            {
                v.Ellipsoid("Hood crown",m.head,new Vector3(0,0.21f,-0.04f),new Vector3(0.62f,0.29f,0.63f),accent);
                v.Ellipsoid("Ember brooch",m.spine,new Vector3(0,0.45f,0.32f),Vector3.one*0.13f,GameData.Amber,true);
            }
            m.leftArm=Arm(v,m.spine,-1,cloth,skin,out m.leftElbow);m.rightArm=Arm(v,m.spine,1,cloth,skin,out m.rightElbow);
            m.leftThigh=Leg(v,m.hips,-1,dark,cream,out m.leftKnee);m.rightThigh=Leg(v,m.hips,1,dark,cream,out m.rightKnee);
            var staff=v.Group("Wand",m.rightElbow,new Vector3(0,-0.34f,0.08f));
            v.Shape("Ash wood",PrimitiveType.Cylinder,staff,new Vector3(0,0.41f,0),new Vector3(0.055f,0.73f,0.055f),new Color(0.42f,0.31f,0.25f));
            v.Ellipsoid("Focus",staff,new Vector3(0,1.16f,0),new Vector3(0.20f,0.28f,0.20f),accent,true);
            return m;
        }
        static Transform Arm(VisualFactory v,Transform parent,int side,Color cloth,Color skin,out Transform elbow)
        {
            var arm=v.Group(side<0?"Shoulder.L":"Shoulder.R",parent,new Vector3(side*0.41f,0.45f,0));
            v.Bone("Sleeve",arm,new Vector3(0,-0.19f,0),new Vector3(0.24f,0.22f,0.24f),cloth);
            elbow=v.Group("Elbow",arm,new Vector3(0,-0.38f,0));
            v.Bone("Forearm",elbow,new Vector3(0,-0.13f,0),new Vector3(0.17f,0.17f,0.17f),cloth);
            v.Ellipsoid("Hand",elbow,new Vector3(0,-0.29f,0),new Vector3(0.18f,0.19f,0.18f),skin);return arm;
        }
        static Transform Leg(VisualFactory v,Transform parent,int side,Color pants,Color cuff,out Transform knee)
        {
            var thigh=v.Group(side<0?"Thigh.L":"Thigh.R",parent,new Vector3(side*0.19f,-0.02f,0));
            v.Bone("Trouser",thigh,new Vector3(0,-0.21f,0),new Vector3(0.25f,0.24f,0.25f),pants);
            knee=v.Group("Knee",thigh,new Vector3(0,-0.42f,0));
            v.Bone("Shin",knee,new Vector3(0,-0.18f,0),new Vector3(0.21f,0.21f,0.21f),pants);
            v.Ellipsoid("Boot",knee,new Vector3(0,-0.40f,0.07f),new Vector3(0.27f,0.22f,0.43f),pants*0.8f);
            v.Ellipsoid("Boot cuff",knee,new Vector3(0,-0.20f,0),new Vector3(0.25f,0.09f,0.25f),cuff);return thigh;
        }
        public void Pose(Vector3 velocity,bool grounded,float vertical,float cast,bool downed,bool dashing)
        {
            if(archer!=null){archer.Stand();return;}
            if(skeletal!=null){skeletal.Stand();return;}
            if(GameApp.Instance?.WorldPaused==true)return;
            float time=GameApp.Instance?.InRun==true?GameApp.Instance.State.elapsed:Time.unscaledTime;
            float dt=Mathf.Min(Time.unscaledDeltaTime,0.05f),speed=new Vector2(velocity.x,velocity.z).magnitude;
            run=Mathf.Lerp(run,Mathf.Clamp01(speed/7),1-Mathf.Exp(-dt*9));stride+=dt*speed*1.8f;
            if(grounded && lastGround<0.5f)landing=1;lastGround=grounded?1:0;landing=Mathf.Max(0,landing-dt*4.5f);
            float a=Mathf.Sin(stride),b=Mathf.Sin(stride+Mathf.PI),air=grounded?0:1,spell=Mathf.Sin(Mathf.Clamp01(cast/0.65f)*Mathf.PI);
            hips.localPosition=new Vector3(0,1.01f+Mathf.Abs(a)*run*0.055f-landing*0.16f+Mathf.Sin(time*2)*0.012f,0);
            rig.localRotation=Quaternion.Slerp(rig.localRotation,Quaternion.Euler(downed?0:dashing?28:run*7,0,downed?82:0),1-Mathf.Exp(-dt*13));
            spine.localRotation=Quaternion.Euler(landing*14,spell*18,a*run*2);
            head.localRotation=Quaternion.Euler(-run*4,Mathf.Sin(time*0.8f)*2,0);
            float tuck=air*(vertical>0?0.8f:0.45f);
            leftThigh.localRotation=Quaternion.Euler(a*34*run-tuck*30,0,0);rightThigh.localRotation=Quaternion.Euler(b*34*run-tuck*23,0,0);
            leftKnee.localRotation=Quaternion.Euler(Mathf.Max(0,-a)*48*run+tuck*62+landing*20,0,0);
            rightKnee.localRotation=Quaternion.Euler(Mathf.Max(0,-b)*48*run+tuck*65+landing*20,0,0);
            leftArm.localRotation=Quaternion.Euler(-a*32*run-air*22-spell*55,0,-8-spell*18);
            rightArm.localRotation=Quaternion.Euler(-b*27*run-air*18-spell*100,0,8+spell*12);
            leftElbow.localRotation=Quaternion.Euler(-12-run*10-spell*28,0,0);rightElbow.localRotation=Quaternion.Euler(-13-run*18-spell*30,0,0);
            cape.localRotation=Quaternion.Euler(-run*18-air*20+Mathf.Sin(time*3)*3,a*run*4,Mathf.Sin(time*2)*3);
        }
    }
}
