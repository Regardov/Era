using UnityEngine;
using UnityEngine.Rendering;
namespace Era
{
    public enum FeedbackKind { Impact,Critical,Dust,Heal,Magic,Pickup,Summon,Death,Shield }
    public sealed class PresentationFx:MonoBehaviour
    {
        public const int Capacity=48,FrameBudget=12,ParticlesPerSlot=28;
        public static PresentationFx Instance{get;private set;}
        static Material shared;
        public static Material SharedMaterial=>shared??(shared=new Material(Shader.Find("Sprites/Default")){name="Shared restrained trails"});
        sealed class Slot
        {
            public ParticleSystem particles,sparks,smoke;public ParticleSystemRenderer meshRenderer,sparkRenderer,smokeRenderer;
            public float age,duration;public bool live;
        }
        readonly Slot[] slots=new Slot[Capacity];int cursor,budgetFrame=-1,budget;float visualTime,grassLife;Vector3 grassPosition;float grassRadius;
        public int ActiveCount{get;private set;}public int PeakCount{get;private set;}public int Emitted{get;private set;}public int Dropped{get;private set;}
        public int[] StyleEmissions{get;}=new int[25];
        void Awake()
        {
            Instance=this;
            for(int i=0;i<Capacity;i++)
            {
                var root=new GameObject("Pooled semantic feedback "+i);root.transform.SetParent(transform,false);
                var s=slots[i]=new Slot{particles=Particles(root.transform,false),sparks=Particles(root.transform,false),smoke=Particles(root.transform,true)};
                s.meshRenderer=s.particles.GetComponent<ParticleSystemRenderer>();s.sparkRenderer=s.sparks.GetComponent<ParticleSystemRenderer>();s.smokeRenderer=s.smoke.GetComponent<ParticleSystemRenderer>();
                var sparkMain=s.sparks.main;sparkMain.maxParticles=8;Stop(s);
            }
        }
        static ParticleSystem Particles(Transform parent,bool smoke)
        {
            var prefab=Resources.Load<GameObject>(smoke?"Feedback/SmokeParticle":"Feedback/CombatParticle");
            var ps=prefab!=null?Instantiate(prefab,parent,false).GetComponent<ParticleSystem>():CreateParticleTemplate(parent,smoke);
            var m=ps.main;m.maxParticles=10;m.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;ConfigureCurves(ps,smoke);return ps;
        }
        static void Stop(Slot s)
        {s.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);s.sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);s.smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);s.meshRenderer.enabled=s.sparkRenderer.enabled=s.smokeRenderer.enabled=false;s.live=false;}
        public static void ConfigureCurves(ParticleSystem ps,bool smoke)
        {
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.8f,.55f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,smoke?AnimationCurve.EaseInOut(0,.45f,1,1.55f):new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.15f,1),new Keyframe(.65f,.85f),new Keyframe(1,0)));
        }
        public static ParticleSystem CreateParticleTemplate(Transform parent,bool smoke)
        {
            var go=new GameObject(smoke?"Soft smoke":"Low-poly particles");go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=ps.main;m.loop=false;m.playOnAwake=false;m.duration=2;m.maxParticles=10;m.simulationSpace=ParticleSystemSimulationSpace.World;m.startLifetime=.7f;m.startSize=.1f;m.startSpeed=0;m.useUnscaledTime=true;m.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;m.startSize3D=!smoke;m.startRotation3D=!smoke;ConfigureCurves(ps,smoke);
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=smoke?FeedbackGeometry.Mist:FeedbackGeometry.Solid;r.renderMode=smoke?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Mesh;
            if(!smoke)r.mesh=FeedbackGeometry.Shard;r.enableGPUInstancing=false;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return ps;
        }
        public static bool Emit(Vector3 p,Color c,float radius,FeedbackKind kind,Vector3 direction=default)
        {
            var style=kind==FeedbackKind.Critical?FeedbackStyle.Critical:kind==FeedbackKind.Dust?FeedbackStyle.Dust:kind==FeedbackKind.Heal?FeedbackStyle.Heal:kind==FeedbackKind.Shield?FeedbackStyle.Shield:kind==FeedbackKind.Pickup?FeedbackStyle.Pickup:kind==FeedbackKind.Summon?FeedbackStyle.Summon:kind==FeedbackKind.Death?FeedbackStyle.Death:kind==FeedbackKind.Magic?FeedbackStyle.Arcane:FeedbackStyle.Impact;
            return EmitStyle(p,style,radius,direction,c);
        }
        public static bool EmitStyle(Vector3 position,FeedbackStyle style,float radius=1,Vector3 direction=default,Color? tint=null)
        {
            var self=Instance;if(self==null||style==FeedbackStyle.None||PresentationMotion.Intensity<=0||GameApp.Instance?.WorldPaused==true)return false;
            if(self.budgetFrame!=Time.frameCount){self.budgetFrame=Time.frameCount;self.budget=0;}
            var camera=GameApp.Instance?.View?.Camera;bool priority=style==FeedbackStyle.Critical||style==FeedbackStyle.Shield;
            if(self.budget>=(priority?FrameBudget:FrameBudget-4)||(camera!=null&&(position-camera.transform.position).sqrMagnitude>65*65)){self.Dropped++;return false;}
            int available=priority?Capacity:Capacity-8;Slot s=null;
            for(int i=0;i<available;i++){int n=(self.cursor+i)%available;if(!self.slots[n].live){s=self.slots[n];self.cursor=(n+1)%available;break;}}
            if(s==null){self.Dropped++;return false;}self.budget++;self.Emitted++;self.StyleEmissions[(int)style]++;
            radius=Mathf.Clamp(radius,.12f,12);Color color=tint??MechanicFeedback.Color(style);
            bool fire=style==FeedbackStyle.Fire||style==FeedbackStyle.Meteor,poison=style==FeedbackStyle.Poison,steam=style==FeedbackStyle.Steam,heal=style==FeedbackStyle.Heal;
            bool glint=style==FeedbackStyle.Critical||style==FeedbackStyle.Holy;
            s.age=0;s.duration=style==FeedbackStyle.Critical?.28f:style==FeedbackStyle.Speed?.4f:fire||poison||steam||heal?1.1f:.75f;s.live=true;
            s.meshRenderer.mesh=FeedbackGeometry.Particle(style);s.meshRenderer.sharedMaterial=fire?FeedbackGeometry.Fire:FeedbackGeometry.Solid;s.meshRenderer.enabled=true;
            var main=s.particles.main;main.gravityModifier=fire||poison||heal||steam?-.025f:style==FeedbackStyle.Frost?.4f:style==FeedbackStyle.Dust?.20f:.15f;s.particles.Play();
            int count=Mathf.Clamp(Mathf.RoundToInt((fire?5:heal?5:glint?3:8)*PresentationMotion.Intensity),1,9);
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f+self.Emitted*.37f,f=(i+.5f)/count;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float spread=radius>2?radius*Mathf.Sqrt(f):radius*(.3f+.5f*f);
                Vector3 at=position+radial*spread,velocity=radial*1.2f+Vector3.up*(.4f+f);Vector3 scale=Vector3.one*(.30f+f*.18f);float life=.35f+f*.25f;
                Vector3 rotation=new Vector3(i*17,i*47,i*29);
                if(fire){scale=new Vector3(.65f,.8f+f*.55f,.65f);at.y+=.02f;velocity=radial*.12f+Vector3.up*(.65f+f*.4f);rotation=new Vector3(0,i*73,0);life=.6f+f*.3f;}
                else if(poison){scale=Vector3.one*(.12f+f*.08f);at.y+=f*.3f;velocity=radial*.12f+Vector3.up*(.55f+f*.35f);life=.65f+f*.25f;}
                else if(heal){scale=Vector3.one*(.5f+f*.20f);at.y+=f*.5f;velocity=Vector3.up*(.7f+f*.45f);rotation=camera!=null?camera.transform.eulerAngles:Vector3.zero;life=.65f+f*.3f;}
                else if(style==FeedbackStyle.Speed){at=position+radial*.15f+direction*f*.6f;velocity=direction*(2+f*2);scale=new Vector3(.10f,.8f+f*.6f,.10f);rotation=Quaternion.FromToRotation(Vector3.up,direction.sqrMagnitude>.01f?direction:Vector3.back).eulerAngles;life=.15f+f*.16f;}
                else if(style==FeedbackStyle.Frost){scale=new Vector3(.28f,.65f+f*.4f,.20f);velocity=radial*1.5f+Vector3.up*(.8f+f);rotation=new Vector3(i*31,i*73,i*41);}
                else if(style==FeedbackStyle.Void){velocity=-radial*spread/.6f;scale=Vector3.one*.25f;life=.6f;}
                else if(style==FeedbackStyle.Root){scale=new Vector3(.24f,.9f,.24f);velocity=Vector3.up*.2f;rotation=new Vector3(12,i*47,18);}
                else if(glint){at=position;scale=Vector3.one*(style==FeedbackStyle.Critical?.85f:.35f);rotation=camera!=null?camera.transform.eulerAngles:Vector3.zero;velocity=style==FeedbackStyle.Critical?Vector3.zero:Vector3.up*.4f;life=style==FeedbackStyle.Critical?.16f:.55f;}
                else if(style==FeedbackStyle.Shield){scale=Vector3.one*.22f;rotation=camera!=null?camera.transform.eulerAngles:Vector3.zero;velocity=radial*.6f;life=.25f;}
                else if(style==FeedbackStyle.Impact||style==FeedbackStyle.Blade){scale=new Vector3(.055f,.24f,.055f);velocity=radial*2+Vector3.up*.5f;life=.18f+f*.1f;}
                else if(style==FeedbackStyle.Blood){scale=new Vector3(.065f,.10f,.055f);velocity=radial*1.4f+Vector3.up*.4f;life=.25f+f*.15f;}
                else if(style==FeedbackStyle.Pickup){scale=Vector3.one*.16f;velocity=Vector3.up*.8f;rotation=camera!=null?camera.transform.eulerAngles:Vector3.zero;life=.35f+f*.15f;}
                else if(style==FeedbackStyle.Mark){at=position;scale=Vector3.one*.35f;velocity=Vector3.zero;rotation=camera!=null?camera.transform.eulerAngles:Vector3.zero;rotation.z+=45;life=.55f;}
                else if(style==FeedbackStyle.Arcane||style==FeedbackStyle.Echo||style==FeedbackStyle.Summon){scale=new Vector3(.10f,.35f,.10f);velocity=radial*.3f+Vector3.up*.6f;life=.5f;}
                else if(style==FeedbackStyle.Lightning){scale=new Vector3(.12f,.65f,.12f);life=.16f;velocity=radial*3;}
                else if(steam){scale=Vector3.one*.1f;life=.2f;}
                else if(style==FeedbackStyle.Dust||style==FeedbackStyle.Death){scale=Vector3.one*(.18f+f*.15f);velocity=radial*.8f+Vector3.up*.35f;}
                s.particles.Emit(new ParticleSystem.EmitParams{position=at,velocity=velocity,startColor=fire?Color.Lerp(new Color(1,.24f,.012f,.85f),new Color(1,.48f,.045f,.85f),f):color,startLifetime=life,startSize3D=scale,rotation3D=rotation},1);
            }
            if(fire||style==FeedbackStyle.Critical||style==FeedbackStyle.Blade||style==FeedbackStyle.Lightning)
            {
                s.sparkRenderer.enabled=true;s.sparkRenderer.sharedMaterial=FeedbackGeometry.Solid;s.sparkRenderer.mesh=FeedbackGeometry.Shard;s.sparks.Play();var sm=s.sparks.main;sm.gravityModifier=fire?.12f:.35f;
                int sparks=Mathf.Clamp(Mathf.RoundToInt((fire?6:8)*PresentationMotion.Intensity),1,8);
                for(int i=0;i<sparks;i++)
                {
                    float a=i*2.399963f;var v=new Vector3(Mathf.Cos(a),.3f+(i%3)*.5f,Mathf.Sin(a))*(fire?1:4);if(fire)v.y+=1.2f;
                    s.sparks.Emit(new ParticleSystem.EmitParams{position=position,velocity=v,startColor=fire?new Color(1,.72f,.15f):style==FeedbackStyle.Critical?new Color(1,.96f,.70f):color,startLifetime=fire?.55f:.2f+(i%3)*.04f,startSize3D=new Vector3(.07f,.30f,.07f),rotation3D=Quaternion.FromToRotation(Vector3.up,v).eulerAngles},1);
                }
            }
            if(fire||poison||steam||heal||style==FeedbackStyle.Void)
            {
                s.smokeRenderer.enabled=true;s.smoke.Play();var m=s.smoke.main;m.gravityModifier=-.01f;
                int smokeCount=heal?3:poison?5:steam?8:fire?5:3;
                for(int i=0;i<smokeCount;i++)
                {
                    float a=i*2.4f;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Color c=poison?new Color(.46f,.62f,.17f,.34f):steam?new Color(.85f,.90f,.88f,.5f):heal?new Color(.5f,1,.7f,.18f):style==FeedbackStyle.Void?new Color(.21f,.12f,.33f,.28f):new Color(.22f,.24f,.23f,.32f);
                    s.smoke.Emit(new ParticleSystem.EmitParams{position=position+radial*radius*.45f+Vector3.up*(fire?.4f:.2f),velocity=radial*.10f+Vector3.up*(.45f+i*.07f),startColor=c,startSize=heal?.45f:.7f+radius*.12f,startLifetime=.8f+i*.035f},1);
                }
            }
            if(radius>=3&&(style==FeedbackStyle.Impact||fire||style==FeedbackStyle.Void)){self.grassPosition=position;self.grassRadius=radius;self.grassLife=.55f;}
            self.ActiveCount++;self.PeakCount=Mathf.Max(self.PeakCount,self.ActiveCount);return true;
        }
        static void Pause(ParticleSystem ps,bool pause){if(pause){if(!ps.isPaused&&ps.isPlaying)ps.Pause();}else if(ps.isPaused)ps.Play();}
        void Update()
        {
            float dt=PresentationMotion.Delta;visualTime+=dt;Shader.SetGlobalFloat("_EraVisualTime",visualTime);grassLife=Mathf.Max(0,grassLife-dt);
            Shader.SetGlobalVector("_GrassImpact",new Vector4(grassPosition.x,grassPosition.z,grassRadius,Mathf.Sin(grassLife/.55f*Mathf.PI)*PresentationMotion.Intensity));
            foreach(var s in slots){if(s==null||!s.live)continue;Pause(s.particles,dt==0);Pause(s.sparks,dt==0);Pause(s.smoke,dt==0);s.age+=dt;if(s.age>=s.duration){Stop(s);ActiveCount--;}}
        }
        public void Clear(){foreach(var s in slots)if(s!=null)Stop(s);ActiveCount=0;grassLife=0;}
        void OnDestroy(){if(Instance==this){Instance=null;if(shared!=null)Destroy(shared);shared=null;FeedbackGeometry.Dispose();Shader.SetGlobalVector("_GrassImpact",Vector4.zero);}}
    }
}
