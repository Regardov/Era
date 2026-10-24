using UnityEngine;
using UnityEngine.Rendering;
namespace Era
{
    public enum FeedbackKind { Impact,Critical,Dust,Heal,Magic,Pickup,Summon,Death,Shield }
    // Fixed capacity, no per-impact GameObjects, materials, coroutines or particle arrays.
    public sealed class PresentationFx:MonoBehaviour
    {
        public const int Capacity=48,FrameBudget=12;
        public static PresentationFx Instance{get;private set;}
        static Material shared;
        public static Material SharedMaterial {get {if(shared==null)shared=new Material(Shader.Find("Sprites/Default")){name="ERA shared transparent feedback"};return shared;}}
        sealed class Slot {public Transform root;public ParticleSystem particles;public LineRenderer ring;public float age,duration,radius;public Color color;public FeedbackKind kind;}
        readonly Slot[] slots=new Slot[Capacity];
        int cursor,budgetFrame=-1,budget;float visualTime,grassLife;Vector3 grassPosition;float grassRadius;
        public int ActiveCount{get;private set;}public int PeakCount{get;private set;}public int Emitted{get;private set;}public int Dropped{get;private set;}
        void Awake()
        {
            Instance=this;
            for(int i=0;i<Capacity;i++)
            {
                var root=new GameObject("Pooled feedback "+i);root.transform.SetParent(transform,false);
                var ps=root.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var m=ps.main;m.loop=false;m.playOnAwake=false;m.duration=1;m.maxParticles=28;m.simulationSpace=ParticleSystemSimulationSpace.World;m.startLifetime=.35f;m.startSize=.07f;m.startSpeed=2;m.gravityModifier=.5f;m.useUnscaledTime=true;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,.05f));
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=SharedMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var ringObject=new GameObject("Impact contour");ringObject.transform.SetParent(root.transform,false);var line=ringObject.AddComponent<LineRenderer>();line.sharedMaterial=SharedMaterial;line.useWorldSpace=false;line.loop=true;line.positionCount=40;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                for(int p=0;p<40;p++){float a=p*Mathf.PI*2/40;line.SetPosition(p,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
                slots[i]=new Slot{root=root.transform,particles=ps,ring=line};root.SetActive(false);
            }
        }
        public static bool Emit(Vector3 position,Color color,float radius,FeedbackKind kind,Vector3 direction=default)
        {
            var self=Instance;if(self==null||PresentationMotion.Intensity<=0||GameApp.Instance?.WorldPaused==true)return false;
            if(self.budgetFrame!=Time.frameCount){self.budgetFrame=Time.frameCount;self.budget=0;}
            var camera=GameApp.Instance?.View?.Camera;
            if(self.budget>=FrameBudget||(camera!=null&&(position-camera.transform.position).sqrMagnitude>70*70)){self.Dropped++;return false;}
            Slot slot=null;for(int i=0;i<Capacity;i++){int n=(self.cursor+i)%Capacity;if(!self.slots[n].root.gameObject.activeSelf){slot=self.slots[n];self.cursor=(n+1)%Capacity;break;}}
            if(slot==null){self.Dropped++;return false;}self.budget++;self.Emitted++;
            slot.age=0;slot.duration=kind==FeedbackKind.Summon?.75f:kind==FeedbackKind.Death?.6f:kind==FeedbackKind.Pickup?.3f:.44f;slot.radius=Mathf.Clamp(radius,.15f,12);slot.color=color;slot.kind=kind;
            slot.root.position=position;slot.root.gameObject.SetActive(true);slot.ring.enabled=kind!=FeedbackKind.Impact&&kind!=FeedbackKind.Pickup;
            slot.ring.transform.localPosition=Vector3.up*.06f;slot.ring.transform.localScale=Vector3.one*.04f;slot.ring.widthMultiplier=.045f;
            slot.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);slot.particles.Play();
            int count=Mathf.RoundToInt((kind==FeedbackKind.Critical||kind==FeedbackKind.Death?18:kind==FeedbackKind.Pickup?5:11)*PresentationMotion.Intensity);
            bool lift=kind==FeedbackKind.Heal||kind==FeedbackKind.Magic||kind==FeedbackKind.Summon||kind==FeedbackKind.Shield;
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f+self.Emitted*.31f;float spread=.4f+(i%5)*.14f;
                Vector3 velocity=new Vector3(Mathf.Cos(a)*spread,lift?1.2f+(i%3)*.25f:.5f+(i%4)*.4f,Mathf.Sin(a)*spread)*Mathf.Min(3,slot.radius);
                velocity+=direction*(kind==FeedbackKind.Impact?2:.4f);
                var ep=new ParticleSystem.EmitParams{position=position+Vector3.up*.12f,velocity=velocity,startColor=color,startLifetime=slot.duration*(.65f+(i%4)*.1f),startSize=(kind==FeedbackKind.Dust?.14f:.06f)*(1+(i%3)*.25f)};
                slot.particles.Emit(ep,1);
            }
            if(radius>=3&&(kind==FeedbackKind.Critical||kind==FeedbackKind.Summon||kind==FeedbackKind.Death||kind==FeedbackKind.Magic)){self.grassPosition=position;self.grassRadius=Mathf.Min(radius,12);self.grassLife=.55f;}
            self.ActiveCount++;self.PeakCount=Mathf.Max(self.PeakCount,self.ActiveCount);return true;
        }
        void Update()
        {
            float dt=PresentationMotion.Delta;visualTime+=dt;
            Shader.SetGlobalFloat("_EraVisualTime",visualTime);grassLife=Mathf.Max(0,grassLife-dt);
            Shader.SetGlobalVector("_GrassImpact",new Vector4(grassPosition.x,grassPosition.z,grassRadius,Mathf.Sin(grassLife/.55f*Mathf.PI)*PresentationMotion.Intensity));
            foreach(var s in slots)
            {
                if(s==null||!s.root.gameObject.activeSelf)continue;
                if(dt==0){if(!s.particles.isPaused)s.particles.Pause();continue;}if(s.particles.isPaused)s.particles.Play();
                s.age+=dt;float t=Mathf.Clamp01(s.age/s.duration);
                if(t>=1){s.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);s.root.gameObject.SetActive(false);ActiveCount--;continue;}
                float r=s.radius*Mathf.Lerp(.12f,1,PresentationMotion.Out(t));s.ring.transform.localScale=Vector3.one*r;
                Color c=s.color;c.a=(1-t)*(s.kind==FeedbackKind.Dust?.25f:.6f)*PresentationMotion.Intensity;s.ring.startColor=s.ring.endColor=c;s.ring.widthMultiplier=Mathf.Lerp(.09f,.012f,t);
            }
        }
        public void Clear()
        {foreach(var s in slots)if(s!=null){s.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);s.root.gameObject.SetActive(false);}ActiveCount=0;grassLife=0;}
        void OnDestroy(){if(Instance==this){Instance=null;if(shared!=null)Destroy(shared);shared=null;Shader.SetGlobalVector("_GrassImpact",Vector4.zero);}}
    }
}
