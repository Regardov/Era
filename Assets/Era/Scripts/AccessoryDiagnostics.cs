using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(98)]
    public sealed class AccessoryDiagnostics:MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string version;public bool ok,slots,levels,locked,auraLimit,snapshot,unlimited;
            public int errors;public int[] equipped,ranks;public string[] captures;
        }
        readonly Report report=new Report();readonly int[] build={2,4,8,12};
        readonly string[] captures={"Inventory.png","Accessory-upgrade.png","Aura-upgrade.png","Hud.png","Fps-settings.png"};
        GameApp App=>GameApp.Instance;string folder;int phase=-1;float started,phaseAt;bool captured;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:"Artifacts/Accessories027";Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;App.SelectedHero=Hero.Lumen;App.Solo();App.SetFrameLimit(0);
            var sim=App.Simulation;var p=App.LocalPlayer;p.invulnerability=10000;App.State.spawnTimer=100000;App.State.enemies.Clear();App.State.resourceNodes.Clear();
            for(int i=0;i<build.Length;i++)sim.ApplyAccessory(p,build[i],i);
            for(int level=0;level<10;level++)
            {
                sim.GrantExperience(App.State.xpRequired-App.State.xp);
                while(p.pendingLevels>0)
                {
                    int choice=p.offerKind==RewardKind.Aura?Mathf.Max(0,Array.FindIndex(p.offers,a=>p.auraLevels[a]==0)):level%p.offers.Length;
                    if(!sim.ChooseItem(p,choice))throw new InvalidOperationException("Accessory capture fixture could not select a valid upgrade");
                }
            }
            report.version=Application.version;report.slots=p.accessorySlots.SequenceEqual(build);report.levels=p.accessoryLevels.Sum()==14;
            report.locked=!sim.ApplyAccessory(p,17,3)&&p.accessorySlots.SequenceEqual(build);
            report.auraLimit=p.auraLevels.Count(l=>l>0)==3&&!sim.LearnAura(p,Array.FindIndex(p.auraLevels,l=>l==0));
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(App.State)).Player(0);
            report.snapshot=copy.accessorySlots.SequenceEqual(p.accessorySlots)&&copy.accessoryLevels.SequenceEqual(p.accessoryLevels);
            report.unlimited=App.FrameLimit==0&&Application.targetFrameRate==-1&&QualitySettings.vSyncCount==0;
            report.equipped=(int[])p.accessorySlots.Clone();report.ranks=(int[])p.accessoryLevels.Clone();started=Time.realtimeSinceStartup;
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors++;}
        void LateUpdate()
        {
            if(Time.realtimeSinceStartup-started<2)return;
            if(phase<0||Time.realtimeSinceStartup-phaseAt>1.2f)
            {
                if(++phase>=captures.Length){Finish();return;}phaseAt=Time.realtimeSinceStartup;captured=false;
                App.Inventory=phase==0;App.Paused=phase==4;
                if(phase==1)App.Simulation.GrantExperience(App.State.xpRequired-App.State.xp);
                if(phase==2)App.Simulation.ChooseItem(App.LocalPlayer,0);
                if(phase==3)App.Simulation.ChooseItem(App.LocalPlayer,0);
            }
            if(!captured&&Time.realtimeSinceStartup-phaseAt>.2f){captured=true;App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,captures[phase]));}
        }
        void Finish()
        {
            report.captures=captures;report.ok=report.slots&&report.levels&&report.locked&&report.auraLimit&&report.snapshot&&report.unlimited&&report.errors==0&&captures.All(c=>File.Exists(Path.Combine(folder,c)));
            File.WriteAllText(Path.Combine(folder,"accessories.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ACCESSORIES "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
