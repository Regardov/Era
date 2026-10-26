using System;
using System.IO;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(50)]
    public sealed class ChestDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report {public bool ok,prices,auras,snapshot,models,opened,choices;public int firstPrice,nextMoonPrice,upgrades;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;
        string folder;float start;int phase;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            start=Time.realtimeSinceStartup;I18n.Language=GameLanguage.Russian;App.SelectedHero=Hero.Lumen;App.PlayerName="Люмен";App.Solo();
            var p=App.LocalPlayer;p.position=WorldMap.OnGround(-.5f,2.2f);p.invulnerability=999;
            App.State.spawnTimer=1000;App.State.coins=1000;App.State.chests.Clear();
            App.State.chests.Add(new ChestState{id=App.State.nextId++,baseCost=12,cost=12,position=WorldMap.OnGround(0,4)});
            App.State.chests.Add(new ChestState{id=App.State.nextId++,baseCost=35,cost=35,tier=1,position=WorldMap.OnGround(2.5f,4)});
            App.Simulation.LearnAura(p,1);App.Simulation.LearnAura(p,1);App.Simulation.LearnAura(p,2);App.Simulation.LearnAura(p,3);App.Simulation.LearnAura(p,4);App.Simulation.LearnAura(p,0);App.Simulation.LearnAura(p,0);
            report.auras=p.currentAura==0&&p.auraLevels[0]==3&&GameData.AuraUpgradeCount(p,0)==2&&GameData.AuraUpgradeCount(p,2)==0;
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Update()
        {
            float age=Time.realtimeSinceStartup-start;
            if(age>40){Finish();return;}
            if(phase==0&&age>1.5f){Shot("chests-closed-v09.png");phase++;}
            else if(phase==1&&age>3)
            {
                report.firstPrice=App.State.chests[0].cost;
                report.prices=App.Simulation.TryOpenChest(App.LocalPlayer)&&App.State.coins==988&&App.State.openedChests==1;
                report.nextMoonPrice=App.State.chests[1].cost;report.prices&=report.nextMoonPrice==59;
                App.LocalPlayer.position=WorldMap.OnGround(2.5f,2.2f);phase++;
            }
            else if(phase==2&&age>4.5f)
            {
                Shot("chests-open-v09.png");
                var coffer=GameObject.Find("Wayfarer's coffer / oak and brass");var moon=GameObject.Find("Moon reliquary / silver and violet ash");
                report.models=coffer!=null&&moon!=null&&coffer.GetComponentsInChildren<MeshFilter>().Length==6&&moon.GetComponentsInChildren<MeshFilter>().Length==6;
                report.opened=coffer!=null&&Quaternion.Angle(coffer.transform.Find("Animated lid hinge").localRotation,Quaternion.Euler(-105,0,0))<1;
                if(coffer!=null)foreach(var renderer in coffer.GetComponentsInChildren<MeshRenderer>())report.models&=renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.isSupported;
                var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(App.State));report.snapshot=copy.openedChests==1&&copy.chests[1].cost==59&&copy.Player(0).auraLevels[0]==3&&copy.Player(0).currentAura==0;
                phase++;
            }
            else if(phase==3&&age>6){App.Simulation.GrantExperience(GameData.ExperienceRequired(1));App.Simulation.ChooseItem(App.LocalPlayer,0);report.choices=App.LocalPlayer.offerKind==RewardKind.Aura&&App.LocalPlayer.offers.Length==3;phase++;}
            else if(phase==4&&age>7.5f){Shot("aura-choice-v09.png");phase++;}
            else if(phase==5&&age>9){var p=App.LocalPlayer;int aura=p.offers[0],rank=p.auraLevels[aura];App.Simulation.ChooseItem(p,0);App.Inventory=true;report.upgrades=GameData.AuraUpgradeCount(p,aura);report.auras&=p.auraLevels[aura]==rank+1&&report.upgrades==rank;phase++;}
            else if(phase==6&&age>10.5f){Shot("aura-inventory-v09.png");phase++;}
            else if(phase==7&&age>12)Finish();
        }
        void LateUpdate()
        {
            if(App.View?.Camera==null)return;var camera=App.View.Camera;camera.transform.position=new Vector3(5.2f,3.2f,8.9f);camera.transform.LookAt(new Vector3(1.2f,.65f,4));camera.fieldOfView=42;
        }
        void Finish()
        {
            report.ok=phase>=7&&report.prices&&report.auras&&report.snapshot&&report.models&&report.opened&&report.choices;
            File.WriteAllText(Path.Combine(folder,"smoke-chests.json"),JsonUtility.ToJson(report,true));Application.Quit(report.ok?0:2);enabled=false;
        }
    }
}
