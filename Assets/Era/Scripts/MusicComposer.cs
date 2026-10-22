using System;
using System.IO;
using UnityEngine;

namespace Era
{
    // Original score: 16 bars in D minor, hand-written melody, soft bell/pad/bass arrangement.
    public static class MusicComposer
    {
        public const int Rate=22050;
        public const float BPM=88;
        public const int Bars=16;
        public static float[] Compose()
        {
            float beat=60/BPM,length=Bars*4*beat;
            var audio=new float[Mathf.CeilToInt(length*Rate)*2];
            int[][] chords={new[]{50,57,60,64},new[]{46,53,57,60},new[]{53,60,64,67},new[]{48,55,57,62}};
            int[] melody={74,0,77,76,74,0,72,69, 70,0,74,72,69,0,65,69, 72,0,76,77,76,0,72,69, 67,0,69,72,74,0,72,0};
            for(int bar=0;bar<Bars;bar++)
            {
                var chord=chords[bar%4];float start=bar*4*beat;
                foreach(int n in chord)Note(audio,n,start,beat*4.5f,0.045f,0.5f,0);
                Note(audio,chord[0]-12,start,beat*1.9f,0.12f,0.5f,2);
                Note(audio,chord[0]-12,start+beat*2,beat*1.8f,0.085f,0.5f,2);
                for(int i=0;i<8;i++)
                {
                    int n=chord[(i+bar)%4]+12;Note(audio,n,start+i*beat/2,beat*1.7f,0.055f,i%2==0?0.30f:0.70f,1);
                    int lead=melody[(bar%4)*8+i];if(lead>0)Note(audio,lead+(bar>=8&&bar<12?12:0),start+i*beat/2,beat*1.4f,0.07f,0.52f,1);
                }
                if(bar>=4)for(int b=0;b<4;b++)Percussion(audio,start+b*beat,b%2==0?0:1,bar>=12?0.65f:0.45f);
            }
            // Soft limiter and a seamless, short wrap of the final reverb tails.
            for(int i=0;i<audio.Length;i++)audio[i]=(float)Math.Tanh(audio[i]*1.25f)*0.72f;
            return audio;
        }
        public static float[] ComposeBattle()
        {
            const float beat=60f/128;var audio=new float[Mathf.CeilToInt(16*4*beat*Rate)*2];
            int[][] chords={new[]{38,45,50,53},new[]{34,41,46,50},new[]{41,48,53,57},new[]{36,43,48,52}};
            int[] theme={74,77,81,79,77,74,72,69,70,74,77,76,74,72,69,65};
            for(int bar=0;bar<16;bar++)
            {
                var chord=chords[bar%4];float start=bar*4*beat;
                foreach(int n in chord)Note(audio,n+12,start,beat*4,.045f,.5f,4);
                for(int eighth=0;eighth<8;eighth++)
                {
                    float at=start+eighth*beat*.5f;Note(audio,chord[eighth%4]+24,at,beat*.48f,.062f,eighth%2==0?.22f:.78f,4);
                    Note(audio,chord[0],at,beat*.5f,.075f,.5f,2);
                    if(bar>=4&&eighth%2==0)Note(audio,theme[(bar%2)*8+eighth],at,beat*1.1f,bar>=12?.10f:.075f,.52f,3);
                    Percussion(audio,at,eighth%4==0?0:eighth%4==2?1:2,eighth%4==0?2.1f:.8f);
                }
                if(bar%4==0)Percussion(audio,start,3,1.4f);
            }
            for(int i=0;i<audio.Length;i++)audio[i]=(float)Math.Tanh(audio[i]*1.7f)*.78f;return audio;
        }
        static void Note(float[] audio,int midi,float start,float duration,float volume,float pan,int voice)
        {
            float f=440*Mathf.Pow(2,(midi-69)/12f);int count=(int)(duration*Rate),offset=(int)(start*Rate);
            float l=Mathf.Sqrt(1-pan),r=Mathf.Sqrt(pan);
            for(int i=0;i<count;i++)
            {
                float t=i/(float)Rate,p=2*Mathf.PI*f*t;
                float env=voice==0?Mathf.Min(1,t/0.7f)*Mathf.Pow(Mathf.Max(0,1-t/duration),1.4f):Mathf.Min(1,t*100)*Mathf.Exp(-t*(voice==1?3.3f:2));
                float s=voice==0?(Mathf.Sin(p)+Mathf.Sin(p*1.003f)*0.5f+Mathf.Sin(p*2)*0.18f)/1.68f:voice==1?(Mathf.Sin(p)+Mathf.Sin(p*2.005f)*0.28f+Mathf.Sin(p*3)*0.08f):Mathf.Sin(p)+Mathf.Sin(p*2)*0.12f;
                if(voice>=3){env=Mathf.Min(1,t/(voice==3?.045f:.025f))*Mathf.Pow(Mathf.Max(0,1-t/duration),.7f);s=(Mathf.Sin(p)+.37f*Mathf.Sin(p*2)+.20f*Mathf.Sin(p*3)+.12f*Mathf.Sin(p*4)+Mathf.Sin(p*1.004f)*.25f)/1.7f;}
                s*=env*volume;int at=((offset+i)%(audio.Length/2))*2;audio[at]+=s*l;audio[at+1]+=s*r;
                int echo=(at+(int)(Rate*0.23f)*2)%audio.Length;audio[echo]+=s*r*0.19f;audio[echo+1]+=s*l*0.19f;
            }
        }
        static void Percussion(float[] audio,float start,int kind,float volume)
        {
            int count=(int)(Rate*(kind==3?.75f:.18f)),offset=(int)(start*Rate);var rng=new System.Random(offset);
            for(int i=0;i<count;i++)
            {float t=i/(float)Rate;float noise=(float)rng.NextDouble()*2-1;float s=kind==0?Mathf.Sin(2*Mathf.PI*(78*t-70*t*t))*Mathf.Exp(-t*24):kind==1?noise*Mathf.Exp(-t*38)*.38f:noise*Mathf.Exp(-t*(kind==3?7:85))*.20f;s*=0.20f*volume;int at=(offset+i)*2;if(at+1<audio.Length){audio[at]+=s;audio[at+1]+=s;}}
        }
        public static void WriteWave(string path)
        {
            WriteSamples(path,Compose());
        }
        public static void WriteBattleWave(string path)=>WriteSamples(path,ComposeBattle());
        static void WriteSamples(string path,float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var file=new BinaryWriter(File.Create(path));int bytes=samples.Length*2;
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+bytes);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)2);file.Write(Rate);file.Write(Rate*4);file.Write((short)4);file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(bytes);foreach(float s in samples)file.Write((short)(Mathf.Clamp(s,-1,1)*32767));
        }
    }
}
