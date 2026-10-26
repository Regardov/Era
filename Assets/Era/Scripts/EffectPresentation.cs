using UnityEngine;
using UnityEngine.Rendering;
namespace Era
{
    // Lines are reserved for actual slashes, thrusts, links and lightning paths.
    public sealed class EffectPresentation:MonoBehaviour
    {
        public const int Capacity=96;
        sealed class Slot {public LineRenderer line,branch;public int kind,id;public Vector3 start,end;public float radius,age,duration;public FeedbackStyle style;public Color color;}
        readonly Slot[] slots=new Slot[Capacity];int cursor;
        public int ActiveCount{get;private set;}public int Dropped{get;private set;}
        LineRenderer Line(string name)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=PresentationFx.SharedMaterial;
            line.useWorldSpace=true;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=2;line.enabled=false;return line;
        }
        void Awake(){for(int i=0;i<Capacity;i++)slots[i]=new Slot{line=Line("Pooled attack path "+i),branch=Line("Lightning branch "+i)};}
        public void Play(EffectState e)
        {
            var camera=GameApp.Instance?.View?.Camera;if(camera!=null&&(e.position-camera.transform.position).sqrMagnitude>70*70)return;
            var style=MechanicFeedback.Style(e);bool path=e.kind>=9&&e.kind<=11;
            Vector3 point=path?e.end:e.position;
            if(style==FeedbackStyle.Fire&&path)
            {
                Vector3 forward=e.end-e.position;
                for(int i=0;i<5;i++)PresentationFx.EmitStyle(e.position+forward*(i/5f),style,.3f+i*.28f,forward.normalized);
            }
            else PresentationFx.EmitStyle(point,style,Mathf.Min(e.radius,10),(e.end-e.position).normalized);
            if(!path||style==FeedbackStyle.Lightning||style==FeedbackStyle.Echo||style==FeedbackStyle.Blood||style==FeedbackStyle.Summon)
                SynthAudio.Instance?.PlaySemantic(style,point);
            if((!path&&style!=FeedbackStyle.Lightning)||style==FeedbackStyle.Fire)return;
            Slot s=null;for(int i=0;i<Capacity;i++){int n=(cursor+i)%Capacity;if(!slots[n].line.enabled){s=slots[n];cursor=(n+1)%Capacity;break;}}
            if(s==null){Dropped++;return;}
            s.kind=e.kind;s.id=e.id;s.style=style;s.start=e.position;s.end=path?e.end:e.position+Vector3.up*6;
            s.radius=e.radius;s.duration=style==FeedbackStyle.Lightning?.22f:style==FeedbackStyle.Echo?.55f:.34f;s.age=0;s.color=MechanicFeedback.Color(style);
            s.line.loop=false;s.line.positionCount=e.kind==9&&style==FeedbackStyle.Blade?20:12;s.line.enabled=true;
            s.branch.enabled=style==FeedbackStyle.Lightning;s.branch.loop=false;s.branch.positionCount=5;ActiveCount++;Render(s);
        }
        void Update(){float dt=PresentationMotion.Delta;if(dt<=0)return;foreach(var s in slots){if(!s.line.enabled)continue;s.age+=dt;if(s.age>=s.duration){s.line.enabled=s.branch.enabled=false;ActiveCount--;continue;}Render(s);}}
        void Render(Slot s)
        {
            float t=Mathf.Clamp01(s.age/s.duration);Vector3 forward=(s.end-s.start).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
            int count=s.line.positionCount;
            if(s.kind==9&&s.style==FeedbackStyle.Blade)
            {
                float head=Mathf.Lerp(-1.25f,1.25f,PresentationMotion.Out(t/.55f));
                for(int i=0;i<count;i++){float a=head-Mathf.Lerp(.05f,1.7f,Mathf.Clamp01(t*8))*(1-i/(float)(count-1));s.line.SetPosition(i,s.start+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*s.radius);}
            }
            else for(int i=0;i<count;i++)
            {
                float f=i/(float)(count-1),wave=(i==0||i==count-1)?0:Mathf.Sin(i*13.8f+s.id+Mathf.Floor(t*14));
                Vector3 p=Vector3.Lerp(s.start,s.end,f);
                if(s.style==FeedbackStyle.Lightning)p+=side*wave*.24f*(1-t);
                else if(s.style==FeedbackStyle.Echo||s.style==FeedbackStyle.Summon||s.style==FeedbackStyle.Blood)p+=Vector3.up*Mathf.Sin(f*Mathf.PI)*(.35f+wave*.08f);
                else p=Vector3.Lerp(s.start,s.end,Mathf.Lerp(t*.75f,1,f));
                s.line.SetPosition(i,p);
            }
            float alpha=(1-t)*(1-t)*(.25f+.75f*PresentationMotion.Intensity);Color c=s.color;c.a=alpha;
            s.line.startColor=s.kind==9?new Color(c.r,c.g,c.b,0):c;s.line.endColor=c;s.line.widthMultiplier=Mathf.Max(.004f,(1-t)*(s.kind==9?.12f:s.style==FeedbackStyle.Lightning?.045f:.04f));
            if(s.branch.enabled)
            {
                Vector3 middle=Vector3.Lerp(s.start,s.end,.58f),end=middle+side*.65f+Vector3.up*.35f;
                for(int i=0;i<5;i++)s.branch.SetPosition(i,Vector3.Lerp(middle,end,i/4f)+Vector3.up*(i%2)*.12f);
                s.branch.startColor=s.branch.endColor=c;s.branch.widthMultiplier=s.line.widthMultiplier*.45f;
            }
        }
        public void Clear(){foreach(var s in slots)if(s!=null)s.line.enabled=s.branch.enabled=false;ActiveCount=0;}
    }
}
