using System;
using UnityEngine;
namespace Era
{
    public sealed partial class SynthAudio
    {
        public const int CombatVoices=16,CombatFrameBudget=5;
        readonly AudioSource[] combatSources=new AudioSource[CombatVoices];
        readonly int[] combatKinds=new int[CombatVoices];readonly float[] nextCue=new float[32];
        AudioClip[] combatClips;int cueFrame=-1,cueBudget;
        public int CombatPlayed{get;private set;}public int CombatDropped{get;private set;}public int PeakCombatVoices{get;private set;}
        public int ActiveCombatVoices{get{int n=0;foreach(var s in combatSources)if(s!=null&&s.isPlaying)n++;return n;}}
        void PrepareCombatAudio()
        {
            combatClips=new AudioClip[32];for(int i=0;i<combatClips.Length;i++)combatClips[i]=CombatClip(i);
            for(int i=0;i<CombatVoices;i++)
            {
                var go=new GameObject("Pooled spatial combat voice "+i);go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=.82f;
                s.rolloffMode=AudioRolloffMode.Logarithmic;s.minDistance=2.5f;s.maxDistance=45;s.dopplerLevel=0;s.priority=150;s.reverbZoneMix=.12f;combatSources[i]=s;combatKinds[i]=-1;
            }
        }
        public void PlayAttack(WeaponKind kind,Vector3 position)
        {if(kind==WeaponKind.LightningStaff||kind==WeaponKind.Aura)return;PlayCombat(kind==WeaponKind.Bow?26:27,position,.055f,.08f,3);}
        public void PlaySemantic(FeedbackStyle style,Vector3 position)
        {
            if(style==FeedbackStyle.None||style==FeedbackStyle.Pickup||style==FeedbackStyle.Death)return;
            float interval=style==FeedbackStyle.Heal?.32f:style==FeedbackStyle.Fire||style==FeedbackStyle.Poison?.22f:style==FeedbackStyle.Speed?.55f:style==FeedbackStyle.Lightning?.12f:.09f;
            float volume=style==FeedbackStyle.Critical?.16f:style==FeedbackStyle.Impact?.10f:style==FeedbackStyle.Speed?.16f:style==FeedbackStyle.Heal?.12f:.18f;
            PlayCombat((int)style,position,volume,interval,style==FeedbackStyle.Impact?3:2);
        }
        public bool PlayCombat(int cue,Vector3 position,float volume,float interval,int identicalLimit)
        {
            var app=GameApp.Instance;if(app==null||app.Volume<=0||app.WorldPaused||combatClips==null)return false;
            if(cueFrame!=Time.frameCount){cueFrame=Time.frameCount;cueBudget=0;}
            if(cue<0||cue>=combatClips.Length||cueBudget>=CombatFrameBudget||Time.unscaledTime<nextCue[cue]){CombatDropped++;return false;}
            var camera=app.View?.Camera;if(camera!=null&&(position-camera.transform.position).sqrMagnitude>45*45){CombatDropped++;return false;}
            int same=0,free=-1;for(int i=0;i<CombatVoices;i++){if(combatSources[i].isPlaying){if(combatKinds[i]==cue)same++;}else if(free<0)free=i;}
            if(same>=identicalLimit||free<0){CombatDropped++;return false;}
            var voice=combatSources[free];voice.transform.position=position;voice.clip=combatClips[cue];voice.volume=Mathf.Clamp01(volume*app.Volume);voice.pitch=.96f+(CombatPlayed%5)*.02f;voice.Play();combatKinds[free]=cue;
            nextCue[cue]=Time.unscaledTime+interval;cueBudget++;CombatPlayed++;PeakCombatVoices=Mathf.Max(PeakCombatVoices,ActiveCombatVoices);return true;
        }
        static AudioClip CombatClip(int cue)
        {
            const int rate=22050;var style=(FeedbackStyle)cue;
            float duration=cue==26?.25f:cue==27?.23f:style==FeedbackStyle.Heal?.65f:style==FeedbackStyle.Void?.70f:style==FeedbackStyle.Fire?.52f:style==FeedbackStyle.Speed?.25f:.36f;
            var data=new float[Mathf.RoundToInt(duration*rate)];var random=new System.Random(9471+cue*177);float low=0,last=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,u=t/duration,noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.09f);float high=noise-last;last=noise;
                float attack=Mathf.Min(1,t*140),env=attack*Mathf.Pow(Mathf.Max(0,1-u),2),sample;
                if(cue==26)sample=Mathf.Sin(2*Mathf.PI*(310*t-230*t*t))*Mathf.Exp(-t*21)+high*.14f*Mathf.Exp(-t*30);
                else if(cue==27||style==FeedbackStyle.Speed)sample=(low*.85f+high*.09f)*Mathf.Sin(Mathf.PI*u)*2;
                else if(style==FeedbackStyle.Fire||style==FeedbackStyle.Meteor)sample=low*2*Mathf.Exp(-t*3)+Mathf.Sin(2*Mathf.PI*(58*t-17*t*t))*.32f*Mathf.Exp(-t*8)+high*.09f*(Mathf.Sin(t*630)> .93f?1:0);
                else if(style==FeedbackStyle.Frost)sample=(Mathf.Sin(t*2*Mathf.PI*1830)+Mathf.Sin(t*2*Mathf.PI*2647)*.4f)*Mathf.Exp(-t*15)*.33f+high*.08f*Mathf.Exp(-t*24);
                else if(style==FeedbackStyle.Lightning)sample=high*.28f*(.25f+.75f*Mathf.Abs(Mathf.Sin(t*67)))+Mathf.Sin(2*Mathf.PI*(1400*t-1200*t*t))*.13f*Mathf.Exp(-t*16);
                else if(style==FeedbackStyle.Poison)sample=Mathf.Sin(2*Mathf.PI*(180*t+90*t*t)+Mathf.Sin(t*80)*1.2f)*Mathf.Exp(-t*12)*.5f+low*.25f;
                else if(style==FeedbackStyle.Heal||style==FeedbackStyle.Holy||style==FeedbackStyle.Summon)sample=(Mathf.Sin(t*2*Mathf.PI*523.25f)+Mathf.Sin(t*2*Mathf.PI*659.25f)*.6f+Mathf.Sin(t*2*Mathf.PI*783.99f)*.4f)*Mathf.Exp(-t*4)*.25f;
                else if(style==FeedbackStyle.Gold||style==FeedbackStyle.Arcane)sample=(Mathf.Sin(t*2*Mathf.PI*1174.66f)*.3f+Mathf.Sin(t*2*Mathf.PI*1760)*.13f)*Mathf.Exp(-t*11)+high*.04f*Mathf.Exp(-t*30);
                else if(style==FeedbackStyle.Blood)sample=low*Mathf.Exp(-t*12)+Mathf.Sin(t*2*Mathf.PI*170+Mathf.Sin(t*125)*1.5f)*Mathf.Exp(-t*23)*.24f;
                else if(style==FeedbackStyle.Shield)sample=(Mathf.Sin(t*2*Mathf.PI*330)+Mathf.Sin(t*2*Mathf.PI*497)*.4f)*Mathf.Exp(-t*9)*.55f+high*.12f*Mathf.Exp(-t*35);
                else if(style==FeedbackStyle.Critical)sample=high*.3f*Mathf.Exp(-t*60)+(Mathf.Sin(t*2*Mathf.PI*1600)+Mathf.Sin(t*2*Mathf.PI*2380)*.3f)*Mathf.Exp(-t*24)*.32f;
                else if(style==FeedbackStyle.Void||style==FeedbackStyle.Echo)sample=Mathf.Sin(2*Mathf.PI*(90*t-35*t*t))*Mathf.Exp(-t*4)*.55f+low*.4f*Mathf.Sin(Mathf.PI*u);
                else if(style==FeedbackStyle.Steam)sample=(low*.4f+high*.17f)*Mathf.Exp(-t*5);
                else sample=Mathf.Sin(2*Mathf.PI*(95*t-60*t*t))*Mathf.Exp(-t*25)*.6f+low*Mathf.Exp(-t*25)*.4f;
                data[i]=Mathf.Clamp(sample*env,-.85f,.85f);
            }
            var clip=AudioClip.Create("ERA semantic cue "+cue,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void DisposeCombatAudio(){if(combatClips!=null)foreach(var c in combatClips)if(c!=null)Destroy(c);}
    }
}
