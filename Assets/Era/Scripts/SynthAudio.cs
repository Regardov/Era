using UnityEngine;

namespace Era
{
    public sealed class SynthAudio : MonoBehaviour
    {
        public static SynthAudio Instance {get;private set;}
        public bool MusicPlaying=>music!=null && music.isPlaying;
        public bool MusicReady=>music!=null && music.clip!=null;
        AudioSource source,music;
        AudioClip[] clips;
        AudioClip step;
        float lastShot,lastCoin,lastHit;
        void Awake()
        {
            Instance=this;source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0;
            music=gameObject.AddComponent<AudioSource>();music.spatialBlend=0;music.loop=true;
            music.clip=Resources.Load<AudioClip>("Audio/Stillwater-at-Dawn");
            music.volume=GameApp.Instance!=null?GameApp.Instance.MusicVolume:.3f;if(music.clip!=null)music.Play();
            clips=new[]{Tone("Seeker",720,350,.11f,.13f),Tone("Sunburst",200,70,.46f,.25f,0.13f),Tone("Mote",900,1300,.12f,.12f),
                Tone("Resonance",410,820,.65f,.19f),Tone("Jump",160,500,.23f,.14f),Tone("Landing",110,50,.17f,.16f,0.65f),
                Tone("Dash",300,720,.20f,.14f,0.5f),Tone("Coin",1250,1700,.15f,.14f),Tone("Chest",410,1040,.70f,.23f),
                Tone("Level",530,1060,.85f,.19f),Tone("Menu",650,800,.065f,.10f),Tone("Hurt",140,65,.24f,.22f,.4f),Tone("Ready",660,990,.22f,.16f),Tone("Blessing",330,1320,1.3f,.23f)};
            step=Tone("Grass step",95,60,.08f,.055f,0.8f);
        }
        void Update()
        {
            var app=GameApp.Instance;if(app==null)return;
            float target=app.MusicVolume*(app.InRun?0.78f:1);
            music.volume=Mathf.Lerp(music.volume,target,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
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
        void OnDestroy(){if(Instance==this)Instance=null;if(clips!=null)foreach(var clip in clips)Destroy(clip);if(step!=null)Destroy(step);}
    }
}
