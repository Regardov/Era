using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(80)]
    public sealed class MusicDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report
        {
            public bool ok,ready,menu,streaming,transitions=true,pause,mute,returnToMenu,rapidChanges,loop;
            public int realmTracks,sources;public string[] tracks=new string[MusicCatalog.Count];public float[] rms=new float[MusicCatalog.Count];
        }
        readonly Report report=new Report();readonly float[] samples=new float[2048];
        GameApp App=>GameApp.Instance;SynthAudio Audio=>SynthAudio.Instance;
        string folder;int phase,realm=1;float started,phaseAt,clock,output;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            started=phaseAt=Time.realtimeSinceStartup;App.MusicVolume=.3f;App.Volume=0;I18n.Language=GameLanguage.Russian;
            report.ready=Audio.MusicReady;report.streaming=true;
            for(int i=0;i<MusicCatalog.Count;i++){var clip=Resources.Load<AudioClip>(MusicCatalog.Path(i));report.tracks[i]=clip?.name;report.streaming&=clip!=null&&clip.loadType==AudioClipLoadType.Streaming;}
        }
        void Next(){phase++;phaseAt=Time.realtimeSinceStartup;}
        bool Settled(int index)=>Audio.CurrentMusicIndex==index&&!Audio.MusicTransitioning&&Audio.MusicPlaying&&output>.0001f;
        void SetRealm(int stage)
        {App.State.stage=stage;App.State.stageSerial++;WorldMap.SetStage(stage);App.LocalPlayer.position=WorldMap.OnGround(0,0);}
        void Update()
        {
            AudioListener.GetOutputData(samples,0);double energy=0;foreach(float sample in samples)energy+=sample*sample;output=(float)Math.Sqrt(energy/samples.Length);
            int selected=Audio.CurrentMusicIndex;if(selected>=0)report.rms[selected]=Mathf.Max(report.rms[selected],output);
            if(App.State!=null){App.State.spawnTimer=10000;App.LocalPlayer.invulnerability=10000;App.LocalPlayer.weaponTimers[0]=10000;}
            float age=Time.realtimeSinceStartup-phaseAt;
            if(phase==0&&Settled(0)){report.menu=true;App.Solo();Next();}
            else if(phase==1&&Settled(realm))
            {report.realmTracks++;if(realm<7){SetRealm(++realm);phaseAt=Time.realtimeSinceStartup;}else{App.Paused=true;clock=Audio.MusicClock;Next();}}
            else if(phase==2&&age>.8f)
            {report.pause=Audio.MusicClock>clock+.2f&&Audio.MusicGain<.23f&&Audio.MusicGain>.01f;App.MusicVolume=0;Next();}
            else if(phase==3&&age>.5f)
            {report.mute=Audio.MusicGain==0&&output<.0001f;App.MusicVolume=.3f;App.Paused=false;Next();}
            else if(phase==4&&age>.6f)
            {foreach(var source in Audio.GetComponents<AudioSource>())if(source.clip!=null&&source.clip.name==MusicCatalog.Files[7])source.time=source.clip.length-.12f;Next();}
            else if(phase==5&&age>.5f)
            {report.loop=Audio.MusicClock<1&&Audio.MusicPlaying&&output>.0001f;App.ReturnToMenu();Next();}
            else if(phase==6&&Settled(0)){report.returnToMenu=true;App.Solo();Next();}
            else if(phase==7&&Settled(1)){SetRealm(2);Next();}
            else if(phase==8&&age>.2f){SetRealm(3);Next();}
            else if(phase==9&&age>.2f){SetRealm(4);Next();}
            else if(phase==10&&Settled(4))
            {report.rapidChanges=true;report.sources=Audio.GetComponents<AudioSource>().Length;report.ok=report.ready&&report.menu&&report.streaming&&report.transitions&&report.pause&&report.mute&&report.returnToMenu&&report.rapidChanges&&report.loop&&report.realmTracks==7&&report.sources==3;Finish();}
            if(phase==1&&age>.2f)report.transitions&=Audio.MusicPlaying;
            if(Time.realtimeSinceStartup-started>95||age>12){report.ok=false;Finish();}
        }
        void Finish(){File.WriteAllText(Path.Combine(folder,"smoke-music.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_MUSIC "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);}
    }
}
