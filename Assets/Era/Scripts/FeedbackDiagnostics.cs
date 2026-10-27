using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(85)]
    public sealed class FeedbackDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report
        {
            public bool ok,statuses,critical,shield,heal,speed,spatial,cleanup,bounded=true,prefabs,fireTint=true;
            public int errors,peakParticles,peakSlots,peakVoices,played,dropped,stressEnemies,fireEmissions,poisonEmissions,fireSamples;public float audioRms;public string[] particleEvidence;
        }
        readonly Report report=new Report();readonly float[] samples=new float[1024];
        readonly EnemyState[] targets=new EnemyState[4];readonly ParticleSystem.Particle[] tintSamples=new ParticleSystem.Particle[10];GameApp App=>GameApp.Instance;string folder;float started,nextPulse;int phase;float oldVfx;Vector3 origin;bool stressShot;
        Vector3 Ground(float x,float z)=>WorldMap.OnGround(origin.x+x,origin.z+z);
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            Application.logMessageReceived+=Log;oldVfx=PresentationMotion.Intensity;PresentationMotion.Intensity=.85f;App.MusicVolume=0;App.Volume=.7f;App.SelectedHero=Hero.Ember;App.Solo();
            origin=WorldMap.RandomClearLocation(new System.Random(312),13,Vector3.zero,35);
            App.State.spawnTimer=100000;App.State.resourceNodes.Clear();App.State.enemies.Clear();App.State.chests.Clear();App.LocalPlayer.position=Ground(0,-3);App.LocalPlayer.weapons=new[]{-1,-1};App.LocalPlayer.critChance=0;
            for(int i=0;i<targets.Length;i++){var e=App.Simulation.SpawnEnemy(i==1?EnemyKind.CrystalGolem:EnemyKind.Skeleton);e.position=Ground((i-1.5f)*3.2f,4);e.hp=e.maxHp=100000;e.speed=0;e.attackTimer=1000;e.spawnGrace=0;targets[i]=e;}
            report.prefabs=Resources.Load<GameObject>("Feedback/CombatParticle")!=null&&Resources.Load<GameObject>("Feedback/SmokeParticle")!=null;
            report.spatial=true;int voices=0;foreach(var source in SynthAudio.Instance.GetComponentsInChildren<AudioSource>())if(source.name.StartsWith("Pooled spatial")){voices++;report.spatial&=source.spatialBlend>.8f&&source.maxDistance==45&&source.dopplerLevel==0;}report.spatial&=voices==16;
            started=Time.realtimeSinceStartup;
        }
        void Log(string m,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)report.errors++;}
        void Statuses()
        {
            var p=App.LocalPlayer;var sim=App.Simulation;
            for(int i=0;i<3;i++)
            {
                p.items=new int[ItemCatalog.Count];sim.ApplyItem(p,(int)(i==0?ItemId.FlameBlade:i==1?ItemId.IceCore:ItemId.VenomNeedle));
                if(i==1)sim.ApplyItem(p,(int)ItemId.GlacialPrison);
                for(int hit=0;hit<(i==1?3:1);hit++)sim.DamageEnemy(targets[i],p,1);
            }
            p.items=new int[ItemCatalog.Count];sim.ApplyItem(p,(int)ItemId.StormAmulet);for(int i=0;i<4;i++)sim.DamageEnemy(targets[3],p,1);
            p.items=new int[ItemCatalog.Count];
        }
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;var p=App.LocalPlayer;var s=App.State;var sim=App.Simulation;
            if(age<6&&age>1&&(nextPulse-=Time.unscaledDeltaTime)<=0){nextPulse=1.3f;Statuses();}
            if(phase==0&&age>2){ParticleEvidence();Shot("feedback-statuses-v021.png");phase++;}
            else if(phase==1&&age>3)
            {
                report.statuses=targets[0].ailments.Exists(a=>a.visual==FeedbackStyle.Fire)&&targets[1].frozen>0&&targets[2].ailments.Exists(a=>a.visual==FeedbackStyle.Poison)&&s.enemies.Exists(e=>e.ailments.Exists(a=>a.visual==FeedbackStyle.Lightning));
                p.critChance=1;sim.DamageEnemy(targets[3],p,10);report.critical=s.hits.Exists(h=>h.critical);p.critChance=0;
                p.invulnerability=0;p.shield=40;sim.DamagePlayer(p,12);report.shield=s.hits.Exists(h=>h.player&&h.absorbed>0);p.knockback=Vector3.zero;p.hitSlide=0;phase++;
            }
            else if(phase==2&&age>3.04f){Shot("feedback-critical-shield-v021.png");phase++;}
            else if(phase==3&&age>4)
            {p.hp=50;float hp=p.hp;sim.Heal(p,20);report.heal=p.hp>hp;phase++;}
            else if(phase==4&&age>4.2f){Shot("feedback-healing-v021.png");phase++;}
            else if(phase==5&&age>5)
            {
                p.eCooldown=0;sim.Step(1f/30,new Dictionary<int,InputFrame>{{0,new InputFrame{secondary=true,x=1}}});App.LocalInput.x=1;phase++;
            }
            else if(phase==6&&age>5.4f)
            {
                foreach(var a in FindObjectsByType<AbilityPresentation>(FindObjectsSortMode.None))if(a.owner==p.id)report.speed|=a.SpeedTrails;
                Shot("feedback-speed-v021.png");App.LocalInput.x=0;phase++;
            }
            else if(phase==7&&age>7)
            {
                // Actual acquisition + timers exercise saw, fire field, roots, vortex, meteor and summons.
                foreach(var item in new[]{ItemId.OrbitalSaw,ItemId.ChaosPortal,ItemId.Thunderstep,ItemId.HuntingPack,ItemId.Overgrowth,ItemId.TimeMaster,ItemId.SpellLoom,ItemId.KineticSpool})sim.ApplyItem(p,(int)item);
                p.dashCooldown=0;sim.Step(1f/30,new Dictionary<int,InputFrame>{{0,new InputFrame{dash=true,x=-1}}});
                p.weapons[0]=(int)WeaponKind.Bow;p.weaponLevels[0]=1;p.weaponTimers[0]=0;
                for(int i=0;i<5;i++){var e=sim.SpawnEnemy(EnemyKind.Skeleton);e.position=p.position+Vector3.forward*6;sim.DamageEnemy(e,p,1000000);}
                for(int i=0;i<8;i++)s.pickups.Add(new PickupState{id=s.nextId++,position=p.position,kind=PickupKind.Experience,value=1});phase++;
            }
            else if(phase==8&&age>8){Shot("feedback-item-mechanics-v021.png");phase++;}
            else if(phase==9&&age>12)
            {
                p.weapons=new[]{-1,-1};p.items=new int[ItemCatalog.Count];p.invulnerability=1000;
                while(s.enemies.Count<GameData.MaxEnemies){var e=sim.SpawnEnemy(EnemyKind.Skeleton);e.position=Ground((s.enemies.Count%15-7)*1.3f,7+s.enemies.Count/15*2);e.hp=100000;e.speed=0;e.attackTimer=1000;e.ailments.Add(new AfflictionState{kind=Ailment.Burn,visual=FeedbackStyle.Fire,owner=0,time=20,power=1});}
                report.stressEnemies=s.enemies.Count;phase++;
            }
            if(phase==10&&age<15)
            {
                // Synthetic overload verifies fixed pools independently of the real-proc gallery.
                for(int i=0;i<100;i++){PresentationFx.EmitStyle(p.position+Vector3.forward*4,(FeedbackStyle)(1+i%24),.5f);SynthAudio.Instance.PlaySemantic((FeedbackStyle)(1+i%24),p.position);}
            }
            else if(phase==10&&age>=15){s.enemies.Clear();s.itemFields.Clear();s.effects.Clear();s.hits.Clear();s.pickups.Clear();s.loot.Clear();s.phase=RunPhase.Lobby;s.stageSerial++;phase++;}
            if(phase==10&&age>14&&!stressShot){stressShot=true;Shot("feedback-stress-v021.png");}
            if(phase==11&&age>17)
            {
                report.cleanup=PresentationFx.Instance.ActiveCount==0&&FindObjectsByType<StatusPresentation>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<ItemFieldPresentation>(FindObjectsSortMode.None).Length==0;
                report.played=SynthAudio.Instance.CombatPlayed;report.dropped=SynthAudio.Instance.CombatDropped;report.peakVoices=SynthAudio.Instance.PeakCombatVoices;
                report.fireEmissions=PresentationFx.Instance.StyleEmissions[(int)FeedbackStyle.Fire];report.poisonEmissions=PresentationFx.Instance.StyleEmissions[(int)FeedbackStyle.Poison];
                report.ok=report.errors==0&&report.statuses&&report.critical&&report.shield&&report.heal&&report.speed&&report.spatial&&report.prefabs&&report.cleanup&&report.bounded&&report.fireTint&&report.fireSamples>0&&report.played>15&&report.dropped>0&&report.audioRms>.0001f&&report.stressEnemies==90;
                Finish();return;
            }
            int particles=0;foreach(var ps in PresentationFx.Instance.GetComponentsInChildren<ParticleSystem>())
            {particles+=ps.particleCount;if(ps.particleCount==0||ps.GetComponent<Renderer>().sharedMaterial.shader.name!="Era/Living Flame")continue;int n=ps.GetParticles(tintSamples);for(int i=0;i<n;i++){Color c=tintSamples[i].startColor;report.fireSamples++;report.fireTint&=c.r>c.g*1.15f&&c.g>c.b*1.5f;}}
            report.peakParticles=Mathf.Max(report.peakParticles,particles);report.peakSlots=Mathf.Max(report.peakSlots,PresentationFx.Instance.ActiveCount);report.bounded&=particles<=1344&&report.peakSlots<=48&&SynthAudio.Instance.ActiveCombatVoices<=16;
            AudioListener.GetOutputData(samples,0);double energy=0;foreach(float sample in samples)energy+=sample*sample;report.audioRms=Mathf.Max(report.audioRms,(float)Math.Sqrt(energy/samples.Length));
            if(age>40)Finish();
        }
        void LateUpdate(){if(phase<=10){var c=App.View.Camera;c.transform.position=Ground(0,-10)+Vector3.up*6.5f;c.transform.LookAt(Ground(0,3)+Vector3.up*.9f);c.fieldOfView=54;}}
        void ParticleEvidence()
        {
            var rows=new List<string>();var particles=new ParticleSystem.Particle[18];
            foreach(var ps in PresentationFx.Instance.GetComponentsInChildren<ParticleSystem>())
            {
                int n=ps.GetParticles(particles);if(n==0)continue;var renderer=ps.GetComponent<ParticleSystemRenderer>();var p=particles[0];
                rows.Add(ps.name+" space="+ps.main.simulationSpace+" size="+p.GetCurrentSize3D(ps)+" color="+p.GetCurrentColor(ps)+" viewport="+App.View.Camera.WorldToViewportPoint(renderer.bounds.center)+" shader="+renderer.sharedMaterial.shader.name+" mesh="+renderer.mesh?.name);
            }
            report.particleEvidence=rows.ToArray();
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name));
        void Finish(){File.WriteAllText(Path.Combine(folder,"smoke-feedback.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_FEEDBACK "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);}
        void OnDestroy(){Application.logMessageReceived-=Log;PresentationMotion.Intensity=oldVfx;}
    }
}
