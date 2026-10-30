using System;
using System.IO;
using UnityEngine;

namespace Era
{
    // One rendered process exercises seven realms and three authoritative player states.
    // It never launches additional game instances or changes saved preferences.
    public sealed class CampaignDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report {public bool ok,jump,grass,lumenModel,keptBuilds,shaders;public int realms,bosses,portals;}
        GameApp App=>GameApp.Instance;
        readonly Report report=new Report();string folder;float start,phaseAt;int phase;EnemyState boss;bool jumpSent;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            start=Time.realtimeSinceStartup;I18n.Language=GameLanguage.Russian;App.SelectedHero=Hero.Lumen;App.PlayerName="Люмен";App.Solo();
            App.State.multiplayer=true;App.Simulation.AddPlayer(1,"Эхо",Hero.Echo);App.Simulation.AddPlayer(2,"Искра",Hero.Ember);App.Simulation.ApplyAccessory(App.LocalPlayer,0,0);
            report.keptBuilds=true;report.shaders=true;SetUpRealm();
        }
        void Shot(string name){if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));}
        void SetUpRealm()
        {
            foreach(var p in App.State.players){p.invulnerability=999;p.position=WorldMap.OnGround(-51+p.id*2,-19+p.id);p.hp=p.maxHp;}
            App.CameraYaw=0;App.CameraPitch=22;App.State.spawnTimer=1000;App.State.stageElapsed=GameData.BossTime-.01f;
            App.State.resourceNodes.Clear();App.LocalPlayer.position=App.State.bossStatuePosition;App.Simulation.TrySummonBoss(App.LocalPlayer);foreach(var p in App.State.players)p.position=WorldMap.OnGround(-51+p.id*2,-19+p.id);
            boss=null;phase=0;phaseAt=Time.realtimeSinceStartup;jumpSent=false;report.realms++;
        }
        void Update()
        {
            if(Time.realtimeSinceStartup-start>65){Finish(false);return;}
            foreach(var p in App.State.players){p.invulnerability=999;for(int i=0;p.pendingLevels>0&&i<32;i++)App.Simulation.ChooseItem(p,0);}
            float age=Time.realtimeSinceStartup-phaseAt;
            if(phase==0)
            {
                boss=App.State.enemies.Find(e=>e.kind==EnemyKind.Warden);if(boss==null)return;
                boss.position=WorldMap.OnGround(-51,-2);boss.frozen=20;boss.hp=boss.maxHp=10000;report.bosses++;phase=1;phaseAt=Time.realtimeSinceStartup;return;
            }
            if(phase==1)
            {
                if(App.State.stage==1&&age>.45f&&!jumpSent){App.LocalInput.jump=true;App.LocalInput.jumpHeld=true;jumpSent=true;}
                if(App.State.stage==1&&age>.70f&&age<1.0f)
                {
                    report.jump|=!App.LocalPlayer.grounded;Shot("jump-v08.png");
                }
                if(age<1.7f)return;
                App.LocalInput.jumpHeld=false;
                var model=UnityEngine.Object.FindObjectsByType<HeroMotion>(FindObjectsSortMode.None);foreach(var hero in model)if(hero.Hero==Hero.Lumen&&hero.lumen!=null&&hero.lumen.animator.avatar.isValid&&hero.lumen.clips.Length==11)report.lumenModel=true;
                var grass=UnityEngine.Object.FindAnyObjectByType<LivingGrass>();report.grass|=grass!=null&&grass.patches.Length>70&&Shader.GetGlobalVector("_GrassBend0").w==1;
                foreach(string shader in new[]{"Era/Meadow Grass","Era/Realm Surface","Era/Realm Terrain","Era/Actor Satin","Era/Rift Portal"})report.shaders&=Shader.Find(shader)!=null&&Shader.Find(shader).isSupported;
                Shot("realm-"+App.State.stage+"-v08.png");phase=2;phaseAt=Time.realtimeSinceStartup;return;
            }
            if(phase==2&&age>.25f)
            {
                App.Simulation.DamageEnemy(boss,App.LocalPlayer,999999);phase=3;phaseAt=Time.realtimeSinceStartup;
                if(App.State.stage<7){App.LocalPlayer.position=WorldMap.OnGround(boss.position.x,boss.position.z-3);App.State.Player(1).position=App.State.portal.position;}
                return;
            }
            if(phase==3)
            {
                if(App.State.stage==7){Finish(App.State.phase==RunPhase.Victory);return;}
                if(age<.65f)return;Shot("portal-"+App.State.stage+"-v08.png");phase=4;phaseAt=Time.realtimeSinceStartup;return;
            }
            if(phase==4&&age>.2f)
            {
                int count=App.LocalPlayer.attributes[0],level=App.State.level;App.State.Player(2).downed=true;App.State.Player(2).hp=0;
                bool moved=App.Simulation.TryUsePortal(App.State.Player(1));if(!moved){Finish(false);return;}
                report.portals++;report.keptBuilds&=App.LocalPlayer.attributes[0]==count&&App.State.level==level&&!App.State.Player(2).downed;SetUpRealm();
            }
        }
        void Finish(bool victory)
        {
            report.ok=victory&&report.realms==7&&report.bosses==7&&report.portals==6&&report.jump&&report.grass&&report.lumenModel&&report.keptBuilds&&report.shaders;
            File.WriteAllText(Path.Combine(folder,"smoke-campaign.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_CAMPAIGN "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
        }
    }
}
