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
        static void Note(float[] audio,int midi,float start,float duration,float volume,float pan,int voice)
        {
            float f=440*Mathf.Pow(2,(midi-69)/12f);int count=(int)(duration*Rate),offset=(int)(start*Rate);
            float l=Mathf.Sqrt(1-pan),r=Mathf.Sqrt(pan);
            for(int i=0;i<count;i++)
            {
                float t=i/(float)Rate,p=2*Mathf.PI*f*t;
                float env=voice==0?Mathf.Min(1,t/0.7f)*Mathf.Pow(Mathf.Max(0,1-t/duration),1.4f):Mathf.Min(1,t*100)*Mathf.Exp(-t*(voice==1?3.3f:2));
                float s=voice==0?(Mathf.Sin(p)+Mathf.Sin(p*1.003f)*0.5f+Mathf.Sin(p*2)*0.18f)/1.68f:voice==1?(Mathf.Sin(p)+Mathf.Sin(p*2.005f)*0.28f+Mathf.Sin(p*3)*0.08f):Mathf.Sin(p)+Mathf.Sin(p*2)*0.12f;
                s*=env*volume;int at=((offset+i)%(audio.Length/2))*2;audio[at]+=s*l;audio[at+1]+=s*r;
                int echo=(at+(int)(Rate*0.23f)*2)%audio.Length;audio[echo]+=s*r*0.19f;audio[echo+1]+=s*l*0.19f;
            }
        }
        static void Percussion(float[] audio,float start,int kind,float volume)
        {
            int count=(int)(Rate*0.12f),offset=(int)(start*Rate);var rng=new System.Random(offset);
            for(int i=0;i<count;i++)
            {float t=i/(float)Rate;float s=kind==0?Mathf.Sin(2*Mathf.PI*(65*t-70*t*t))*Mathf.Exp(-t*32):((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*70)*0.24f;s*=0.16f*volume;int at=(offset+i)*2;if(at+1<audio.Length){audio[at]+=s;audio[at+1]+=s;}}
        }
        public static void WriteWave(string path)
        {
            var samples=Compose();Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var file=new BinaryWriter(File.Create(path));int bytes=samples.Length*2;
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+bytes);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)2);file.Write(Rate);file.Write(Rate*4);file.Write((short)4);file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(bytes);foreach(float s in samples)file.Write((short)(Mathf.Clamp(s,-1,1)*32767));
        }
    }
}
