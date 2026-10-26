using UnityEngine;
namespace Era
{
    // Persistent state accents. Existing cast/effect serials remain the only activation source.
    public sealed class AbilityPresentation:MonoBehaviour
    {
        public int owner;
        LineRenderer shield,channel;float shieldBlend,chargeBlend,dashTick,clock;int cast=-1;Vector3 previous;float previousHp=-1;
        void Start(){shield=Ring("Shield envelope",1,64);channel=Ring("Channel sigil",.7f,48);previous=transform.position;}
        LineRenderer Ring(string name,float r,int count)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=PresentationFx.SharedMaterial;line.useWorldSpace=false;line.loop=true;line.positionCount=count;line.widthMultiplier=.024f;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}return line;
        }
        void Update()
        {
            var p=GameApp.Instance?.State?.Player(owner);if(p==null||shield==null)return;float dt=PresentationMotion.Delta;clock+=dt;
            shieldBlend=PresentationMotion.Damp(shieldBlend,p.shield>0&&!p.downed?1:0,10,dt);
            bool sustained=p.chargingShrine>=0||p.boost>0||p.auraSurge>0;
            chargeBlend=PresentationMotion.Damp(chargeBlend,!p.downed&&(p.castTime>0||sustained)?1:0,12,dt);
            float intensity=PresentationMotion.Intensity;var color=GameData.HeroColor(p.hero);color.a=chargeBlend*.45f*intensity;
            channel.startColor=channel.endColor=color;channel.transform.localPosition=Vector3.up*.15f;channel.transform.localScale=Vector3.one*(1+Mathf.Sin(clock*2.4f)*.035f);channel.widthMultiplier=.025f+chargeBlend*.02f;
            color=new Color(.35f,.72f,1,shieldBlend*.45f*intensity);shield.startColor=shield.endColor=color;shield.transform.localPosition=Vector3.up*1.1f;shield.transform.localRotation=Quaternion.Euler(70,clock*18,15);shield.transform.localScale=Vector3.one*(.78f+.06f*Mathf.Sin(clock*1.8f));
            if(cast<0)cast=p.castSerial;
            if(cast!=p.castSerial){cast=p.castSerial;if(GetComponent<HeroMotion>()?.archer==null)PresentationFx.Emit(p.position+Vector3.up,GameData.HeroColor(p.hero),1.1f,p.hero==Hero.Lumen?FeedbackKind.Heal:p.hero==Hero.Echo?FeedbackKind.Shield:FeedbackKind.Magic);}
            if(previousHp>=0&&p.hp>previousHp+.1f)PresentationFx.Emit(p.position,GameData.Mint,.7f,FeedbackKind.Heal);previousHp=p.hp;
            if(p.dashTime>0&&(dashTick-=dt)<=0){dashTick=.075f;PresentationFx.Emit(previous+Vector3.up*.25f,GameData.HeroColor(p.hero),.32f,FeedbackKind.Dust,(previous-transform.position).normalized);}previous=transform.position;
        }
    }
    public sealed class PickupRetirement:MonoBehaviour
    {
        static int count;float age;int owner;Vector3 origin,scale;Color color;
        public static int ActiveCount=>count;
        public static void Begin(GameObject go,int player)
        {
            if(count>=48){Object.Destroy(go);return;}var r=go.AddComponent<PickupRetirement>();r.owner=player;r.origin=go.transform.position;r.scale=go.transform.localScale;r.color=go.name.Contains("Gold")?GameData.Amber:new Color(.2f,.6f,1);count++;
        }
        void Update()
        {
            age+=PresentationMotion.Delta;var player=GameApp.Instance?.State?.Player(owner);if(player==null){Destroy(gameObject);return;}
            float t=PresentationMotion.Ease(age/.18f);transform.position=Vector3.Lerp(origin,player.position+Vector3.up,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.15f;transform.localScale=scale*(1-t*.85f);
            if(age>=.18f){PresentationFx.Emit(transform.position,color,.25f,FeedbackKind.Pickup);Destroy(gameObject);}
        }
        void OnDestroy(){count=Mathf.Max(0,count-1);}
    }
}
