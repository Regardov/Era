using UnityEngine;
using UnityEngine.Rendering;
namespace Era
{
    // One pool for existing simulation effects; presentation never generates damage.
    public sealed class EffectPresentation:MonoBehaviour
    {
        public const int Capacity=96;
        sealed class Slot {public LineRenderer line;public int kind,id;public Vector3 start,end;public float radius,age,duration;public Color color;}
        readonly Slot[] slots=new Slot[Capacity];int cursor;
        public int ActiveCount{get;private set;}public int Dropped{get;private set;}
        void Awake()
        {
            for(int i=0;i<Capacity;i++){var g=new GameObject("Pooled spell contour "+i);g.transform.SetParent(transform,false);var line=g.AddComponent<LineRenderer>();line.sharedMaterial=PresentationFx.SharedMaterial;line.useWorldSpace=true;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=2;line.enabled=false;slots[i]=new Slot{line=line};}
        }
        public void Play(EffectState e)
        {
            var camera=GameApp.Instance?.View?.Camera;if(camera!=null&&(e.position-camera.transform.position).sqrMagnitude>85*85)return;
            Slot slot=null;for(int i=0;i<Capacity;i++){int n=(cursor+i)%Capacity;if(!slots[n].line.enabled){slot=slots[n];cursor=(n+1)%Capacity;break;}}
            if(slot==null){Dropped++;return;}
            slot.kind=e.kind;slot.id=e.id;slot.start=e.position;slot.end=e.end;slot.radius=e.radius;slot.duration=e.kind>=9&&e.kind<=11?.4f:.55f;slot.age=Mathf.Max(0,slot.duration-e.life);
            slot.color=e.kind>=9&&e.kind<=11?GameData.Weapons[(int)e.weapon].color:e.kind==12?new Color(.3f,.66f,1):e.kind==1?new Color(.4f,.8f,1):e.kind==3?GameData.Mint:e.kind==5?new Color(.91f,.43f,.44f):GameData.Amber;
            slot.line.enabled=true;slot.line.loop=e.kind<9||e.kind==12;slot.line.positionCount=e.kind==9?20:e.kind==10?12:e.kind==11?2:48;ActiveCount++;
            var kind=e.kind==2?FeedbackKind.Dust:e.kind==3?FeedbackKind.Heal:e.kind==6?FeedbackKind.Death:e.kind==7?FeedbackKind.Pickup:e.kind==12?FeedbackKind.Summon:e.kind>=9?FeedbackKind.Impact:FeedbackKind.Magic;
            PresentationFx.Emit(e.kind>=9&&e.kind<=11?e.end:e.position,slot.color,Mathf.Min(e.radius,8),kind,(e.end-e.position).normalized);
            Render(slot);
        }
        void Update(){float dt=PresentationMotion.Delta;if(dt<=0)return;foreach(var s in slots){if(!s.line.enabled)continue;s.age+=dt;if(s.age>=s.duration){s.line.enabled=false;ActiveCount--;continue;}Render(s);}}
        void Render(Slot s)
        {
            float t=Mathf.Clamp01(s.age/s.duration);Vector3 forward=(s.end-s.start).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;
            if(s.kind==9)
            {
                float head=Mathf.Lerp(-1.25f,1.25f,PresentationMotion.Out(t/.5f));
                for(int i=0;i<20;i++){float a=head-Mathf.Lerp(.05f,1.8f,Mathf.Clamp01(t*9))*(1-i/19f);s.line.SetPosition(i,s.start+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*s.radius);}
            }
            else if(s.kind==10){for(int i=0;i<12;i++){float f=i/11f;float jitter=(i==0||i==11)?0:Mathf.Sin(i*13.8f+s.id+Mathf.Floor(t*12))*.24f*(1-t);s.line.SetPosition(i,Vector3.Lerp(s.start,s.end,f)+side*jitter);}}
            else if(s.kind==11){s.line.SetPosition(0,Vector3.Lerp(s.start,s.end,PresentationMotion.Ease(t)*.85f));s.line.SetPosition(1,s.end);}
            else
            {
                float r=Mathf.Lerp(.12f,s.radius,PresentationMotion.Out(t));
                for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;var p=s.start+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);p.y=WorldMap.Ground(p)+.07f;s.line.SetPosition(i,p);}
            }
            float strength=.35f+.65f*PresentationMotion.Intensity;Color c=s.color;c.a=(1-t)*(1-t)*strength;s.line.startColor=s.kind==9?new Color(c.r,c.g,c.b,0):c;s.line.endColor=c;s.line.widthMultiplier=Mathf.Max(.006f,(1-t)*(s.kind==9?.20f:s.kind==10?.085f:.09f)*strength);
        }
        public void Clear(){foreach(var s in slots)if(s!=null)s.line.enabled=false;ActiveCount=0;}
    }
}
