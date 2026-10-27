using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class ItemArtDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report
        {
            public bool ok,transparent,distinct,allSprites,launches,colors,styles,collection,snapshot,catalogue,bounded;
            public int icons,activeIcons,errors,peakParticles;public string version;public string[] captures;
        }
        readonly Report report=new Report();readonly List<string> captures=new List<string>();
        readonly ItemId[] examples={ItemId.SharpBlade,ItemId.DoubleShot,ItemId.Chronosphere,ItemId.BlackHole,ItemId.RealityCube};
        GameApp App=>GameApp.Instance;string folder;float started,launched;int phase;Vector3 origin;bool gallery;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;started=Time.realtimeSinceStartup;report.version=Application.version;report.allSprites=report.transparent=report.distinct=true;
            var hashes=new HashSet<string>();
            for(int i=0;i<ItemCatalog.Count;i++)
            {
                var t=IconLibrary.ItemTexture(i);var sprite=IconLibrary.ItemSprite(i);report.allSprites&=t!=null&&sprite!=null&&sprite.texture==t;
                if(t==null)continue;report.icons++;if(ItemCatalog.IsAvailable(i))report.activeIcons++;
                var pixels=t.GetPixels32();int transparent=0;uint hash=2166136261;
                foreach(var pixel in pixels){if(pixel.a==0)transparent++;unchecked{hash=(hash^pixel.r)*16777619;hash=(hash^pixel.g)*16777619;hash=(hash^pixel.b)*16777619;hash=(hash^pixel.a)*16777619;}}
                report.transparent&=transparent>pixels.Length*.4f;report.distinct&=hashes.Add(hash.ToString("X8"));
            }
            App.SetLanguage(GameLanguage.Russian);App.SelectedHero=Hero.Ember;App.Solo();App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.invulnerability=10000;App.State.spawnTimer=100000;App.State.resourceNodes.Clear();App.State.enemies.Clear();App.State.chests.Clear();App.State.loot.Clear();
            var random=new System.Random(705);
            for(int attempt=0;attempt<32;attempt++){origin=WorldMap.RandomClearLocation(random,14,Vector3.zero,30);bool bush=false;foreach(var prop in WorldMap.Props)if(prop.kind==2&&Vector3.Distance(prop.position,origin)<10)bush=true;if(!bush)break;}
            App.LocalPlayer.position=WorldMap.OnGround(origin.x,origin.z-13);
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)report.errors++;}
        void Capture(string name)
        {captures.Add(name);App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));}
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;
            if(App.UpgradeSelection)App.Choose(0);
            if(phase<4&&age>1+phase*1.1f)
            {string name="item-catalogue-"+(phase+1)+"-v025.png";captures.Add(name);App.GetComponent<GameHud>().CaptureItemCatalogue(Path.Combine(folder,name),phase);phase++;}
            else if(phase==4&&age>5.8f)
            {
                App.Inventory=false;App.Page=MenuPage.Home;gallery=true;
                for(int rank=0;rank<5;rank++)
                {
                    Vector3 point=WorldMap.OnGround(origin.x+(rank-2)*3.2f,origin.z+2);
                    var chest=new ChestState{id=App.State.nextId++,position=point,cost=0,baseCost=0,tier=rank%2};App.State.chests.Add(chest);App.LocalPlayer.position=point;App.Simulation.TryOpenChest(App.LocalPlayer);
                    var drop=App.State.loot[App.State.loot.Count-1];drop.content=(int)examples[rank];chest.reward=drop.content;chest.rewardRarity=(int)GameData.Items[drop.content].rarity;
                    do{drop.id=App.State.nextId++;}while(LootMotion.Evaluate(drop).variant!=rank);
                    drop.age=0;
                }
                App.LocalPlayer.position=WorldMap.OnGround(origin.x,origin.z-13);launched=Time.realtimeSinceStartup;phase++;
            }
            else if(phase==5&&Time.realtimeSinceStartup-launched>.20f){Capture("loot-ejection-v025.png");phase++;}
            else if(phase==6&&Time.realtimeSinceStartup-launched>.58f){Capture("loot-arcs-v025.png");phase++;}
            else if(phase==7&&Time.realtimeSinceStartup-launched>1.65f)
            {
                Capture("loot-rarity-glow-v025.png");var views=FindObjectsByType<LootPresentation>(FindObjectsSortMode.None);report.launches=App.State.loot.Count==5&&views.Length==5;report.colors=report.styles=report.bounded=true;
                var variants=new HashSet<int>();foreach(var view in views){report.colors&=view.GlowColor==GameData.RarityColor((ItemRarity)ItemCatalog.RarityOrder[view.Rank]);variants.Add(view.Variant);report.bounded&=view.ParticleLimit==24;}
                report.styles=variants.Count==5;report.peakParticles=0;foreach(var view in views)foreach(var ps in view.GetComponentsInChildren<ParticleSystem>())report.peakParticles+=ps.particleCount;
                report.bounded&=report.peakParticles<=120;
                var clone=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(App.State));report.snapshot=clone.loot.Count==5&&clone.loot.TrueForAll(l=>l.fromChest&&l.launchOrigin!=Vector3.zero);phase++;
            }
            else if(phase==8&&Time.realtimeSinceStartup-launched>2.8f)
            {
                var drop=App.State.loot[0];App.LocalPlayer.position=drop.position;report.collection=App.Simulation.TryCollectLoot(App.LocalPlayer);App.LocalPlayer.position=WorldMap.OnGround(origin.x,origin.z-13);phase++;
            }
            else if(phase==9&&Time.realtimeSinceStartup-launched>3.5f){report.collection&=App.State.loot.Count==4;Capture("loot-collection-v025.png");phase++;}
            else if(phase==10&&Time.realtimeSinceStartup-launched>4.3f)
            {
                report.catalogue=true;for(int i=1;i<=4;i++)report.catalogue&=File.Exists(Path.Combine(folder,"item-catalogue-"+i+"-v025.png"));
                report.captures=captures.ToArray();report.ok=report.errors==0&&report.icons==129&&report.activeIcons==116&&report.transparent&&report.distinct&&report.allSprites&&report.launches&&report.colors&&report.styles&&report.collection&&report.snapshot&&report.catalogue&&report.bounded;
                File.WriteAllText(Path.Combine(folder,"item-art-runtime.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ITEM_ART "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
            }
            if(age>35){report.ok=false;File.WriteAllText(Path.Combine(folder,"item-art-runtime.json"),JsonUtility.ToJson(report,true));Application.Quit(3);}
        }
        void LateUpdate()
        {
            if(!gallery||App.View?.Camera==null)return;var camera=App.View.Camera;camera.transform.position=origin+new Vector3(.5f,5.4f,-10);camera.transform.LookAt(origin+new Vector3(0,1.1f,2.8f));camera.fieldOfView=55;
            // Apply the gallery camera before GameHud takes its real RenderTexture capture.
            foreach(var view in FindObjectsByType<LootPresentation>(FindObjectsSortMode.None))foreach(var drop in App.State.loot)if(LootMotion.Rank(LootMotion.Rarity(drop))==view.Rank)view.Present(drop,camera);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
