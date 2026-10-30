using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(98)]
    public sealed class PolishDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string version;public bool ok,aura,range,following,paused,cleanup,nativeScreens;
            public int errors,renderers,vertices,auditedLines;public string[] textOverflows;
        }
        readonly Report report=new Report{range=true,following=true,paused=true};
        readonly string[] names={"Golden-aura","Aura-surge","Aura-moving","Russian-cards","English-cards","Online-cards","Inventory","Cleanup"};
        GameApp App=>GameApp.Instance;GameHud hud;string folder;float started,phaseAt,previousClock;int phase=-1,nativeGood;bool shot,nativeShot;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:"Artifacts/Polish028";Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;Application.targetFrameRate=60;I18n.Language=GameLanguage.Russian;
            App.SelectedHero=Hero.Lumen;App.Solo();App.CameraPitch=43;App.CameraYaw=25;
            App.State.spawnTimer=100000;App.State.enemies.Clear();App.State.resourceNodes.Clear();App.LocalPlayer.invulnerability=10000;
            hud=App.GetComponent<GameHud>();hud.AuditTextLayout=true;started=Time.realtimeSinceStartup;gameObject.AddComponent<PolishInputStage>().owner=this;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        public void Prepare()
        {
            if(!enabled)return;App.LocalInput.x=0;App.LocalInput.z=phase==2?1:0;App.LocalInput.yaw=0;
        }
        void Enter()
        {
            phaseAt=Time.realtimeSinceStartup;shot=nativeShot=false;previousClock=-1;
            if(phase==7){App.ReturnToMenu();return;}
            var p=App.LocalPlayer;p.pendingLevels=0;p.velocity=Vector3.zero;App.Inventory=phase==6;
            typeof(GameApp).GetProperty(nameof(GameApp.Online)).SetValue(App,phase==5);
            I18n.Language=phase==4?GameLanguage.English:GameLanguage.Russian;
            p.auraSurge=phase==1?4:0;
            if(phase>=3&&phase<=5)
            {
                p.pendingLevels=1;p.completedLevel=3;p.upgradeStage=2;p.offerKind=RewardKind.Aura;
                p.offers=new[]{0,4,7};p.offerRarities=new int[3];
            }
        }
        void LateUpdate()
        {
            if(Time.realtimeSinceStartup-started<2)return;
            if(phase<0||Time.realtimeSinceStartup-phaseAt>2.5f){if(++phase>=names.Length){Finish();return;}Enter();}
            if(phase==7){report.cleanup=FindObjectsByType<LumenAuraVisual>(FindObjectsSortMode.None).Length==0;return;}
            if(Time.realtimeSinceStartup-phaseAt<.12f)return;
            var visual=FindAnyObjectByType<LumenAuraVisual>();
            if(visual!=null)
            {
                report.aura=true;report.range&=Mathf.Abs(visual.Radius-Simulation.AuraRadius(App.LocalPlayer))<.001f;
                report.following&=Vector3.Distance(visual.transform.position,App.View.PlayerVisualPosition(App.LocalPlayer))<.01f;
                if(App.WorldPaused&&previousClock>=0)report.paused&=Mathf.Abs(visual.AnimationClock-previousClock)<.00001f;
                previousClock=visual.AnimationClock;
                report.renderers=Mathf.Max(report.renderers,visual.GetComponentsInChildren<MeshRenderer>().Length);
                report.vertices=Mathf.Max(report.vertices,visual.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount));
            }
            if(!shot&&Time.realtimeSinceStartup-phaseAt>.7f){shot=true;hud.RequestCapture(Path.Combine(folder,names[phase]+".png"));}
            if(!nativeShot&&Time.realtimeSinceStartup-phaseAt>1.4f&&(phase==3||phase==4||phase==5)){nativeShot=true;StartCoroutine(NativeCapture(names[phase]));}
        }
        IEnumerator NativeCapture(string name)
        {
            yield return new WaitForEndOfFrame();
            var rt=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                var pixels=image.GetPixels32();int lit=0;for(int i=0;i<pixels.Length;i+=101)if(pixels[i].r+pixels[i].g+pixels[i].b>40)lit++;
                if(SystemInfo.graphicsUVStartsAtTop)
                {
                    for(int y=0;y<rt.height/2;y++)for(int x=0;x<rt.width;x++){int a=y*rt.width+x,b=(rt.height-1-y)*rt.width+x;var temp=pixels[a];pixels[a]=pixels[b];pixels[b]=temp;}
                    image.SetPixels32(pixels);image.Apply();
                }
                if(lit>100)nativeGood++;File.WriteAllBytes(Path.Combine(folder,"Native-"+name+".png"),image.EncodeToPNG());Destroy(image);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        void Finish()
        {
            report.version=Application.version;report.auditedLines=hud.AuditedTextLines;report.textOverflows=hud.TextOverflows.ToArray();report.nativeScreens=nativeGood==3;
            report.ok=report.errors==0&&report.aura&&report.range&&report.following&&report.paused&&report.cleanup&&report.renderers<=10&&report.vertices<2000&&report.auditedLines>100&&report.textOverflows.Length==0&&report.nativeScreens;
            File.WriteAllText(Path.Combine(folder,"polish.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_POLISH "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
    [DefaultExecutionOrder(-120)] public sealed class PolishInputStage:MonoBehaviour{public PolishDiagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}}
}
