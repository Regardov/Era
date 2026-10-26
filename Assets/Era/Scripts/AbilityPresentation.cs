using UnityEngine;
namespace Era
{
    public sealed class AbilityPresentation:MonoBehaviour
    {
        public int owner;
        MeshRenderer shield;float shieldBlend,hitFlash,healSum,healTick,speedTick,previousHp=-1,previousShield=-1,previousSpeed=1;
        bool movingFast;TrailRenderer leftTrail,rightTrail;GameObject[] saws;GameObject[] charges;
        public float ShieldHitFlash=>hitFlash;
        public bool SpeedTrails=>leftTrail!=null&&leftTrail.emitting;
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        public void ShieldImpact(){hitFlash=1;}
        void Start()
        {
            var field=FeedbackGeometry.MeshObject("Reactive shield field",transform,FeedbackGeometry.Shield,FeedbackGeometry.Barrier);shield=field.GetComponent<MeshRenderer>();field.transform.localPosition=Vector3.up*1.08f;field.transform.localScale=new Vector3(1.5f,2.5f,1.5f);
            leftTrail=Trail("Speed wake left",-.24f);rightTrail=Trail("Speed wake right",.24f);
        }
        TrailRenderer Trail(string name,float x)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(x,.45f,-.18f);
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=PresentationFx.SharedMaterial;trail.time=.17f;trail.minVertexDistance=.10f;trail.startWidth=.055f;trail.endWidth=0;trail.startColor=new Color(.76f,.91f,.92f,.45f);trail.endColor=new Color(.76f,.91f,.92f,0);trail.emitting=false;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return trail;
        }
        void Update()
        {
            var p=GameApp.Instance?.State?.Player(owner);if(p==null||shield==null)return;float dt=PresentationMotion.Delta;float intensity=PresentationMotion.Intensity;
            hitFlash=Mathf.Max(0,hitFlash-dt*5);shieldBlend=PresentationMotion.Damp(shieldBlend,(p.shield>0||hitFlash>0)&&!p.downed?1:0,10,dt);
            shield.enabled=shieldBlend>.01f&&intensity>0;block.Clear();block.SetColor("_Color",new Color(.27f,.64f,1,shieldBlend*.36f*intensity));block.SetFloat("_Hit",hitFlash);shield.SetPropertyBlock(block);
            if(previousHp>=0&&p.hp>previousHp)healSum+=p.hp-previousHp;healTick-=dt;
            if(healSum>.15f&&healTick<=0){healTick=.40f;PresentationFx.EmitStyle(p.position+Vector3.up*.65f,FeedbackStyle.Heal,.45f);SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Heal,p.position);healSum=0;}
            if(previousShield>=0&&p.shield>previousShield+1){PresentationFx.EmitStyle(p.position+Vector3.up,FeedbackStyle.Shield,.45f);SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Shield,p.position);}
            previousHp=p.hp;previousShield=p.shield;
            float speed=p.moveSpeed*(1+p.auraBonuses[4])*(p.boost>0?1.3f:1);
            bool fast=!p.downed&&!p.eliminated&&!p.crouching&&(p.dashTime>0||speed>1.05f&&new Vector2(p.velocity.x,p.velocity.z).magnitude>8.15f);
            leftTrail.emitting=rightTrail.emitting=fast&&dt>0&&intensity>0;
            if(fast&&(!movingFast||speed>previousSpeed+.04f)&&p.dashTime<=0)SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Speed,p.position);
            if(fast&&(speedTick-=dt)<=0){speedTick=.15f;PresentationFx.EmitStyle(p.position+Vector3.up*.25f,FeedbackStyle.Speed,.20f,-p.velocity.normalized);}
            movingFast=fast;previousSpeed=speed;
            PresentCharges(p);
            int count=Mathf.Min(8,p.items[(int)ItemId.OrbitalSaw]);
            if(count>0&&saws==null)
            {
                saws=new GameObject[8];var tint=new MaterialPropertyBlock();tint.SetColor("_Color",new Color(.65f,.70f,.66f,1));
                for(int i=0;i<8;i++){saws[i]=FeedbackGeometry.MeshObject("Orbital blade "+i,transform,FeedbackGeometry.Saw,FeedbackGeometry.Solid);saws[i].GetComponent<Renderer>().SetPropertyBlock(tint);}
            }
            if(saws!=null)for(int i=0;i<saws.Length;i++)
            {
                bool live=i<count&&!p.downed&&!p.eliminated;saws[i].SetActive(live);if(!live)continue;
                float a=p.combatTime*3+i*Mathf.PI*2/Mathf.Max(1,p.items[(int)ItemId.OrbitalSaw]);
                var at=p.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*3*p.areaSize;at.y=WorldMap.Ground(at)+.22f;
                saws[i].transform.position=at;saws[i].transform.rotation=Quaternion.Euler(0,p.combatTime*580,0);
            }
        }
        void PresentCharges(PlayerState p)
        {
            int runes=p.items[(int)ItemId.SpellLoom]>0?p.itemCounters[(int)ItemId.SpellLoom]:0;
            int recipe=p.items[(int)ItemId.AlchemyEngine]>0?p.itemCounters[(int)ItemId.AlchemyEngine]:0;
            int kinetic=p.items[(int)ItemId.KineticSpool]>0?p.itemCounters[(int)ItemId.KineticSpool]:0;
            float debt=p.items[(int)ItemId.TemporalDebt]>0?p.itemValues[(int)ItemId.TemporalDebt]:0;
            if(charges==null&&(runes!=0||recipe>0||kinetic>0||debt>0))
            {charges=new GameObject[6];for(int i=0;i<6;i++)charges[i]=FeedbackGeometry.MeshObject("Stored mechanic charge "+i,transform,i==5?FeedbackGeometry.Orb:FeedbackGeometry.Shard,FeedbackGeometry.Solid);}
            if(charges==null)return;
            for(int i=0;i<charges.Length;i++)
            {
                bool active=(i<3?(runes&(1<<i))!=0:i==3?recipe>0:i==4?kinetic>0:debt>0)&&!p.downed&&!p.eliminated&&PresentationMotion.Intensity>0;charges[i].SetActive(active);if(!active)continue;
                var style=i==3?(recipe==1?FeedbackStyle.Fire:recipe==2?FeedbackStyle.Frost:FeedbackStyle.Poison):i==5?FeedbackStyle.Echo:FeedbackStyle.Arcane;
                var color=MechanicFeedback.Color(style);color.a=.8f;block.Clear();block.SetColor("_Color",color);charges[i].GetComponent<Renderer>().SetPropertyBlock(block);
                charges[i].transform.localPosition=i<3?new Vector3((i-1)*.20f,2.13f,0):new Vector3((i-4)*.25f,1.25f,-.40f);
                charges[i].transform.localScale=Vector3.one*(i==5?.12f+Mathf.Clamp01(debt/80)*.15f:.16f);
                charges[i].transform.localRotation=Quaternion.Euler(0,p.combatTime*60+i*40,0);
            }
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
