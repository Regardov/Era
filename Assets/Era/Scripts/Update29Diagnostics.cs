using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(112)]
    public sealed class Update29Diagnostics:MonoBehaviour
    {
        [Serializable]public sealed class Report
        {
            public string version;public bool ok,transparent=true,echo=true,finite=true,wand=true,oneHanded=true,layouts=true,fifthChoice;
            public int errors,suppliedIcons,nativeScreens,auditedLines,echoTriangles,motionFrames;public float maxGripError,leftHandTravel,rightHandTravel;public string[] textOverflows;
        }
        readonly Report report=new Report();
        readonly string[] names={"Echo-idle","Echo-walk","Echo-run","Echo-cast","Echo-crouch","Echo-jump","Supplied-icons","Weapons-Lumen-RU","Weapons-Lumen-EN","Weapons-Echo-RU","Weapons-Ember-RU","Weapons-Online-RU","Weapons-Online-EN","Choose-fifth"};
        GameApp App=>GameApp.Instance;GameHud hud;string folder;float started,phaseAt;int phase=-1,expectedFifth;bool captured;
        Vector3 leftMin=Vector3.one*1000,leftMax=Vector3.one*-1000,rightMin=Vector3.one*1000,rightMax=Vector3.one*-1000;
        Mesh baked;WorldEnvironment environment;GUIStyle galleryLabel;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:"Artifacts/Update029";Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;Application.targetFrameRate=60;
            App.SelectedHero=Hero.Echo;App.Solo();App.CameraPitch=35;App.CameraYaw=0;hud=App.GetComponent<GameHud>();hud.AuditTextLayout=true;
            environment=FindAnyObjectByType<WorldEnvironment>();baked=new Mesh();started=Time.realtimeSinceStartup;
            foreach(var id in IconLibrary.SuppliedObjects)
            {
                var texture=IconLibrary.ItemTexture((int)id);if(texture==null){report.transparent=false;continue;}report.suppliedIcons++;
                var pixels=texture.GetPixels32();int clear=pixels.Count(p=>p.a==0);report.transparent&=clear>pixels.Length*.4f;
                for(int x=0;x<texture.width;x++)report.transparent&=pixels[x].a==0&&pixels[(texture.height-1)*texture.width+x].a==0;
                for(int y=0;y<texture.height;y++)report.transparent&=pixels[y*texture.width].a==0&&pixels[(y+1)*texture.width-1].a==0;
            }
            gameObject.AddComponent<Update29InputStage>().owner=this;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        void Enter()
        {
            phaseAt=Time.realtimeSinceStartup;captured=false;
            if(phase<=5){App.LocalPlayer.position=WorldMap.OnGround(0,0);App.LocalPlayer.velocity=Vector3.zero;App.LocalInput.jump|=phase==5;App.LocalInput.primary|=phase==3;return;}
            if(phase==6){App.Paused=false;hud.enabled=false;return;}
            hud.enabled=true;App.Paused=false;App.View.Camera.orthographic=false;App.View.Camera.GetComponent<StillwaterLens>().enabled=true;typeof(GameApp).GetProperty(nameof(GameApp.Online)).SetValue(App,false);
            App.SelectedHero=phase==9?Hero.Echo:phase==10?Hero.Ember:Hero.Lumen;App.Solo();
            I18n.Language=phase==8||phase==12?GameLanguage.English:GameLanguage.Russian;
            App.State.level=24;App.LocalPlayer.completedLevel=24;App.Simulation.GrantExperience(App.State.xpRequired-App.State.xp);
            int count=App.LocalPlayer.hero==Hero.Lumen?5:4;report.layouts&=App.LocalPlayer.offerKind==RewardKind.Weapon&&App.LocalPlayer.offers.Length==count;
            bool online=phase==11||phase==12;typeof(GameApp).GetProperty(nameof(GameApp.Online)).SetValue(App,online);
            var layout=UpgradeLayout.Create(count,online);
            foreach(var card in layout.cards)report.layouts&=card.yMax<900&&card.xMax<1600&&card.yMax<layout.panel.yMax;
            if(phase==13){expectedFifth=App.LocalPlayer.offers[4];App.Choose(4);}
        }
        public void Prepare()
        {
            if(!enabled||Time.realtimeSinceStartup-started<2)return;
            if(phase<0||Time.realtimeSinceStartup-phaseAt>2){if(++phase>=names.Length){Finish();return;}Enter();}
            App.State.enemies.Clear();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;
            var input=App.LocalInput;input.x=0;input.z=phase==1?.28f:phase==2?1:0;input.yaw=0;input.crouch=phase==4;input.jumpHeld=false;
            if(phase<=5)App.LocalPlayer.pendingLevels=0;
        }
        void LateUpdate()
        {
            if(!enabled||phase<0||Time.realtimeSinceStartup-phaseAt<.2f)return;
            if(phase<=5)
            {
                var actor=FindObjectsByType<HeroMotion>(FindObjectsSortMode.None).FirstOrDefault(h=>h.Hero==Hero.Echo&&h.name==App.LocalPlayer.name);
                if(actor!=null)
                {
                    var metadata=actor.GetComponent<AuthoredCharacter>();report.echo&=metadata.model=="Echo"&&metadata.version==29;report.echoTriangles=metadata.triangles;
                    var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(baked);report.finite&=baked.vertices.All(v=>ActorMotionMath.Finite(v)&&v.magnitude<6);
                    var grip=FindObjectsByType<WeaponGrip>(FindObjectsSortMode.None).FirstOrDefault(g=>g.owner==App.LocalId);
                    report.wand&=grip!=null&&grip.name.StartsWith("EchoWand");if(grip!=null){report.oneHanded&=!grip.IsTwoHanded;report.maxGripError=Mathf.Max(report.maxGripError,grip.PrimaryError);}
                    if(phase==1||phase==2)
                    {
                        report.motionFrames++;
                        var left=actor.transform.InverseTransformPoint(actor.skeletal.Bone("hand.l").position);var right=actor.transform.InverseTransformPoint(actor.skeletal.Bone("hand.r").position);
                        leftMin=Vector3.Min(leftMin,left);leftMax=Vector3.Max(leftMax,left);rightMin=Vector3.Min(rightMin,right);rightMax=Vector3.Max(rightMax,right);
                    }
                    var camera=App.View.Camera;var at=actor.transform.position;
                    camera.orthographic=true;camera.orthographicSize=1.65f;camera.GetComponent<StillwaterLens>().enabled=false;
                    camera.transform.position=at+new Vector3(2.8f,1.8f,5.5f);camera.transform.LookAt(at+Vector3.up*1.1f);
                }
            }
            if(phase==13)report.fifthChoice=App.LocalPlayer.weapons[1]==expectedFifth&&App.LocalPlayer.offerKind==RewardKind.Accessory&&App.LocalPlayer.pendingLevels==1;
            if(!captured&&Time.realtimeSinceStartup-phaseAt>.85f)
            {
                captured=true;if(phase>=6&&phase<=12)StartCoroutine(NativeCapture(names[phase]));else hud.RequestCapture(Path.Combine(folder,names[phase]+".png"));
            }
        }
        IEnumerator NativeCapture(string name)
        {
            yield return new WaitForEndOfFrame();
            var rt=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();var pixels=texture.GetPixels32();
                if(pixels.Where((p,index)=>index%101==0).Count(p=>p.r+p.g+p.b>40)>100)report.nativeScreens++;
                if(SystemInfo.graphicsUVStartsAtTop){for(int y=0;y<rt.height/2;y++)for(int x=0;x<rt.width;x++){int a=y*rt.width+x,b=(rt.height-1-y)*rt.width+x;var tmp=pixels[a];pixels[a]=pixels[b];pixels[b]=tmp;}texture.SetPixels32(pixels);texture.Apply();}
                File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        void OnGUI()
        {
            if(phase!=6)return;var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1600f,Screen.height/900f,1));
            galleryLabel??=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.UpperCenter,wordWrap=true};
            for(int pane=0;pane<2;pane++)
            {
                float x=20+pane*790;GUI.color=pane==0?new Color(.07f,.10f,.13f):new Color(.88f,.89f,.86f);GUI.DrawTexture(new Rect(x,20,770,860),Texture2D.whiteTexture);
                GUI.color=Color.white;galleryLabel.normal.textColor=pane==0?Color.white:Color.black;
                GUI.Label(new Rect(x+20,80,730,50),pane==0?"ПРОЗРАЧНОСТЬ · ТЁМНАЯ ПОДЛОЖКА":"ПРОЗРАЧНОСТЬ · СВЕТЛАЯ ПОДЛОЖКА",galleryLabel);
                for(int i=0;i<IconLibrary.SuppliedObjects.Length;i++)
                {
                    var id=IconLibrary.SuppliedObjects[i];float left=x+27+i%5*148,top=210+i/5*290;
                    GUI.DrawTexture(new Rect(left,top,124,124),IconLibrary.ItemTexture((int)id),ScaleMode.ScaleToFit,true);
                    GUI.Label(new Rect(left-5,top+144,134,55),GameData.Items[(int)id].name,galleryLabel);
                }
            }
            GUI.matrix=previous;GUI.color=Color.white;
        }
        void Finish()
        {
            report.version=Application.version;report.leftHandTravel=(leftMax-leftMin).magnitude;report.rightHandTravel=(rightMax-rightMin).magnitude;
            report.auditedLines=hud.AuditedTextLines;report.textOverflows=hud.TextOverflows.ToArray();
            report.ok=report.errors==0&&report.echo&&report.echoTriangles>6000&&report.finite&&report.wand&&report.oneHanded&&report.maxGripError<.003f&&report.motionFrames>20&&report.leftHandTravel>.10f&&report.leftHandTravel<2&&report.rightHandTravel>.10f&&report.rightHandTravel<2&&report.layouts&&report.fifthChoice&&report.suppliedIcons==10&&report.transparent&&report.nativeScreens==7&&report.textOverflows.Length==0&&report.auditedLines>100;
            File.WriteAllText(Path.Combine(folder,"update29.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_UPDATE29 "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;if(baked!=null)Destroy(baked);}
    }
    [DefaultExecutionOrder(-120)]public sealed class Update29InputStage:MonoBehaviour{public Update29Diagnostics owner;void Update(){if(owner!=null&&owner.enabled)owner.Prepare();}}
}
