using UnityEngine;

namespace Era
{
    public sealed partial class SynthAudio : MonoBehaviour
    {
        public static SynthAudio Instance {get;private set;}
        public bool MusicPlaying=>musicSources!=null&&((musicSources[0].isPlaying&&musicSources[0].volume>.001f)||(musicSources[1].isPlaying&&musicSources[1].volume>.001f));
        public bool MusicReady {get{if(scores==null||scores.Length!=MusicCatalog.Count)return false;foreach(var score in scores)if(score==null)return false;return true;}}
        public bool BattlePlaying=>CurrentMusicIndex>0&&MusicPlaying;
        public int CurrentMusicIndex {get;private set;}=-1;
        public bool MusicTransitioning=>transition<1;
        public float MusicClock=>musicSources!=null?musicSources[activeSource].time:0;
        public float MusicGain=>musicSources!=null?Mathf.Max(musicSources[0].volume,musicSources[1].volume):0;
        public const float CrossfadeSeconds=2.4f;
        AudioSource source;
        AudioSource[] musicSources;
        AudioClip[] scores;
        readonly int[] sourceTracks={-1,-1};
        readonly float[] weights=new float[2],startWeights=new float[2];
        int activeSource;float transition=1,duck=1;
        AudioClip[] clips;
        AudioClip step;
        float lastShot,lastCoin,lastHit;
        void Awake()
        {
            Instance=this;source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0;
            scores=new AudioClip[MusicCatalog.Count];for(int i=0;i<scores.Length;i++)scores[i]=Resources.Load<AudioClip>(MusicCatalog.Path(i));
            musicSources=new AudioSource[2];
            for(int i=0;i<2;i++)
            {
                var player=gameObject.AddComponent<AudioSource>();player.playOnAwake=false;player.spatialBlend=0;player.loop=true;
                player.ignoreListenerPause=true;player.priority=0;player.dopplerLevel=0;player.volume=0;musicSources[i]=player;
            }
            if(MusicReady)SwitchMusic(0);else Debug.LogError("ERA: one or more soundtrack clips are missing");
            clips=new[]{Tone("Seeker",720,350,.11f,.13f),Tone("Sunburst",200,70,.46f,.25f,0.13f),Tone("Mote",900,1300,.12f,.12f),
                Tone("Resonance",410,820,.65f,.19f),Tone("Jump",160,500,.23f,.14f),Tone("Landing",110,50,.17f,.16f,0.65f),
                Tone("Dash",300,720,.20f,.14f,0.5f),Tone("Coin",1250,1700,.15f,.14f),Tone("Chest",410,1040,.70f,.23f),
                Tone("Level",530,1060,.85f,.19f),Tone("Menu",650,800,.065f,.10f),Tone("Hurt",140,65,.24f,.22f,.4f),Tone("Ready",660,990,.22f,.16f),Tone("Blessing",330,1320,1.3f,.23f)};
            step=Tone("Grass step",95,60,.08f,.055f,0.8f);
            PrepareCombatAudio();
        }
        void Update()
        {
            var app=GameApp.Instance;if(app==null)return;
            int desired=MusicCatalog.Select(app.InRun,app.State?.stage??1);
            // Reverse an existing fade immediately. Other rapid changes coalesce to the latest realm.
            if(desired!=CurrentMusicIndex&&(!MusicTransitioning||sourceTracks[0]==desired||sourceTracks[1]==desired))SwitchMusic(desired);
            float dt=Time.unscaledDeltaTime;transition=Mathf.Min(1,transition+dt/CrossfadeSeconds);
            float blend=Mathf.SmoothStep(0,1,transition);
            float targetDuck=app.WorldPaused?.48f:app.InRun&&app.State.phase!=RunPhase.Playing?.65f:1;
            duck=Mathf.Lerp(duck,targetDuck,1-Mathf.Exp(-dt*3));
            for(int i=0;i<2;i++)
            {
                weights[i]=Mathf.Lerp(startWeights[i],i==activeSource?1:0,blend);
                musicSources[i].volume=Mathf.Sqrt(Mathf.Max(0,weights[i]))*Mathf.Clamp(app.MusicVolume,0,.7f)*duck;
                if(transition>=1&&i!=activeSource&&musicSources[i].isPlaying){musicSources[i].Stop();musicSources[i].clip=null;sourceTracks[i]=-1;}
            }
        }
        void SwitchMusic(int index)
        {
            if(!MusicReady)return;
            int next=sourceTracks[0]==index?0:sourceTracks[1]==index?1:weights[0]<=weights[1]?0:1;
            if(sourceTracks[next]!=index)
            {
                var player=musicSources[next];player.Stop();player.clip=scores[index];player.volume=0;player.time=0;player.Play();sourceTracks[next]=index;weights[next]=0;
            }
            for(int i=0;i<2;i++)startWeights[i]=weights[i];
            activeSource=next;CurrentMusicIndex=index;transition=0;
        }
        static AudioClip Tone(string name,float start,float end,float length,float volume,float noise=0)
        {
            const int rate=22050;var data=new float[(int)(length*rate)];float phase=0;var rng=new System.Random(name.Length*83);
            for(int i=0;i<data.Length;i++)
            {float t=i/(float)data.Length;phase+=Mathf.Lerp(start,end,t)*Mathf.PI*2/rate;float env=Mathf.Min(1,t*35)*Mathf.Pow(1-t,2);float sample=Mathf.Sin(phase)+Mathf.Sin(phase*2.005f)*0.20f;sample=Mathf.Lerp(sample,(float)rng.NextDouble()*2-1,noise);data[i]=sample*env*volume;}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Play(int id)
        {
            if(id<0 || id>=clips.Length || GameApp.Instance==null)return;
            if(id==0 && Time.unscaledTime-lastShot<0.10f)return;if(id==7 && Time.unscaledTime-lastCoin<0.08f)return;
            if(id==2 && Time.unscaledTime-lastHit<.10f)return;if(id==2)lastHit=Time.unscaledTime;
            if(id==0)lastShot=Time.unscaledTime;if(id==7)lastCoin=Time.unscaledTime;
            source.PlayOneShot(clips[id],GameApp.Instance.Volume);
        }
        public void Footstep(){if(GameApp.Instance!=null)source.PlayOneShot(step,GameApp.Instance.Volume);}
        void OnDestroy(){DisposeCombatAudio();if(Instance==this)Instance=null;if(clips!=null)foreach(var clip in clips)Destroy(clip);if(step!=null)Destroy(step);}
    }
}
