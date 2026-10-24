using UnityEngine;
namespace Era
{
    public struct WeaponPoseSample {public Vector3 euler,offset;public float strike;}
    [DefaultExecutionOrder(75)]
    public sealed class WeaponPresentation:MonoBehaviour
    {
        public WeaponKind kind;int owner=-1,serial=-1;float age=10,clock;
        public float Anticipation{get;private set;}public float AimYaw{get;private set;}
        public WeaponPoseSample Sample{get;private set;}
        Transform stringRoot,arrow,focus;LineRenderer bowString,chargeRing;
        public void BindOwner(int id){owner=id;}
        void Start()
        {
            if(kind==WeaponKind.Bow){stringRoot=transform.Find("Bow string");bowString=stringRoot?.GetComponent<LineRenderer>();arrow=transform.Find("Nocked arrow");}
            focus=transform.Find(kind==WeaponKind.Aura?"Aura focus":"Charged heart");
            if(focus!=null){var g=new GameObject("Focus charge");g.transform.SetParent(transform,false);g.transform.localPosition=focus.localPosition;chargeRing=g.AddComponent<LineRenderer>();chargeRing.useWorldSpace=false;chargeRing.loop=true;chargeRing.positionCount=24;chargeRing.sharedMaterial=PresentationFx.SharedMaterial;chargeRing.widthMultiplier=.018f;for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;chargeRing.SetPosition(i,new Vector3(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f,0));}}
        }
        void Update()
        {
            var p=GameApp.Instance?.State?.Player(owner);if(p==null)return;float dt=PresentationMotion.Delta;if(dt<=0)return;clock+=dt;age+=dt;
            float timer=10;for(int i=0;i<p.weapons.Length;i++)if(p.weapons[i]==(int)kind)timer=p.weaponTimers[i];
            // Anticipation only when a real target is in reach; an idle weapon does not fake attacks.
            bool target=false;Vector3 nearest=Vector3.zero;float range=Mathf.Max(1,GameData.Weapons[(int)kind].range+p.weaponRange-15);float distance=range*range;
            foreach(var e in GameApp.Instance.State.enemies){float d=(e.position-p.position).sqrMagnitude;if(d<distance){distance=d;nearest=e.position;target=true;}}
            float preparing=target&&timer>0&&timer<.18f?1-timer/.18f:0;Anticipation=PresentationMotion.Damp(Anticipation,preparing,20,dt);AimYaw=target?Mathf.Atan2(nearest.x-p.position.x,nearest.z-p.position.z)*Mathf.Rad2Deg:p.yaw;
            if(serial<0)serial=p.weaponCastSerial;
            if(serial!=p.weaponCastSerial){serial=p.weaponCastSerial;if(p.lastWeapon==(int)kind){age=0;PresentationFx.Emit(transform.position,GameData.Weapons[(int)kind].color,.35f,kind==WeaponKind.LightningStaff||kind==WeaponKind.Aura?FeedbackKind.Magic:FeedbackKind.Impact,Quaternion.Euler(0,p.weaponAimYaw,0)*Vector3.forward);}}
            float strike=1-PresentationMotion.Ease(age/(kind==WeaponKind.Axe?.39f:.28f)),recoil=Mathf.Sin(Mathf.Clamp01(age/.32f)*Mathf.PI)*Mathf.Clamp01(1-age/.38f);
            Vector3 angles=Vector3.zero,offset=Vector3.zero;
            switch(kind)
            {
                case WeaponKind.Axe:angles=new Vector3(20+strike*78-Anticipation*32,35*strike-20*Anticipation,-28*strike);offset=new Vector3(.08f*strike,0,.12f*strike);break;
                case WeaponKind.Sword:angles=new Vector3(16+strike*58-Anticipation*22,60*strike-30*Anticipation,-20*strike);break;
                case WeaponKind.Spear:angles=new Vector3(68+strike*19,0,0);offset=Vector3.forward*(strike*.3f-Anticipation*.12f);break;
                case WeaponKind.Bow:angles=new Vector3(-recoil*7,0,recoil*4);offset=Vector3.back*recoil*.065f;break;
                case WeaponKind.LightningStaff:angles=new Vector3(20+strike*32-Anticipation*17,0,recoil*5);break;
                case WeaponKind.Aura:angles=new Vector3(-Anticipation*15+strike*22,0,0);break;
            }
            Sample=new WeaponPoseSample{euler=angles,offset=offset,strike=strike};
            if(bowString!=null){bowString.SetPosition(1,new Vector3(-Anticipation*.18f,.48f,0));if(arrow!=null){arrow.localPosition=Vector3.left*Anticipation*.18f;arrow.gameObject.SetActive(age>.13f);}}
            if(chargeRing!=null){float pulse=.03f+Anticipation*.5f+strike*.4f;var c=GameData.Weapons[(int)kind].color;c.a=pulse*PresentationMotion.Intensity;chargeRing.startColor=chargeRing.endColor=c;chargeRing.transform.localRotation=Quaternion.Euler(0,clock*45,0);chargeRing.transform.localScale=Vector3.one*(.8f+pulse*.7f);}
        }
    }
}
