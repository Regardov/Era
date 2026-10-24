using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class BodyCollisionChecks
    {
        static int count;
        static void Check(bool value,string label){count++;if(!value)throw new Exception("ERA body collision: "+label);}
        static Simulation New(EnemyKind kind,Vector3 player,Vector3 enemy)
        {
            var sim=new Simulation(501);var p=sim.AddPlayer(0,"Contact",Hero.Ember);sim.Start();sim.State.spawnTimer=100000;sim.State.resourceNodes.Clear();p.position=player;p.weapons=new[]{-1,-1};p.invulnerability=1000;
            var e=sim.SpawnEnemy(kind);e.position=enemy;e.frozen=1000;e.spawnGrace=0;return sim;
        }
        static void Walk(Simulation sim,InputFrame input,float time,float dt=1f/30)
        {var inputs=new Dictionary<int,InputFrame>{{0,input}};for(int i=0;i<Mathf.CeilToInt(time/dt);i++){sim.Step(dt,inputs);input.ConsumeEdges();}}
        public static int Run()
        {
            count=0;WorldMap.SetStage(1);
            var sim=New(EnemyKind.StoneGolem,Vector3.back*3,Vector3.zero);var p=sim.State.Player(0);var e=sim.State.enemies[0];
            Walk(sim,new InputFrame{z=1},2);float stop=p.position.z;
            Check(stop< -1.25f&&stop> -1.32f,"walking stops at the enemy capsule");Check(!ActorBodies.Overlap(ActorBodies.Player(p),p.position,ActorBodies.Enemy(e),e.position),"no player body penetration");
            var stay=p.position;Walk(sim,new InputFrame{z=1},3);Check(Vector3.Distance(stay,p.position)<.001f,"stable blocked contact without jitter");
            sim=New(EnemyKind.StoneGolem,Vector3.back*3,Vector3.zero);p=sim.State.Player(0);Walk(sim,new InputFrame{z=1,dash=true},.5f);Check(p.position.z< -1.25f,"dash does not tunnel through a body");
            sim=New(EnemyKind.StoneGolem,Vector3.back*2,Vector3.zero);p=sim.State.Player(0);p.hitSlide=.3f;p.knockback=Vector3.forward*40;Walk(sim,new InputFrame(),.4f);Check(p.position.z< -1.25f,"knockback is swept against enemies");
            sim=New(EnemyKind.Slime,Vector3.back*2,Vector3.zero);p=sim.State.Player(0);Walk(sim,new InputFrame{z=1,jump=true,jumpHeld=true},1);Check(p.position.z>2,"jump can clear a genuinely low body");
            sim=New(EnemyKind.Warden,Vector3.back*3,Vector3.zero);p=sim.State.Player(0);Walk(sim,new InputFrame{z=1,jump=true,jumpHeld=true},1);Check(p.position.z< -1.5f,"jump cannot phase through a tall boss");
            sim=New(EnemyKind.Slime,Vector3.up*3,Vector3.zero);p=sim.State.Player(0);p.grounded=false;p.verticalSpeed=-4;Walk(sim,new InputFrame(),.8f);Check(p.grounded&&p.position.y>1.1f,"falling lands on body instead of inside it");
            sim=New(EnemyKind.Bat,Vector3.zero,Vector3.up*3);p=sim.State.Player(0);float ceiling=ActorBodies.Ceiling(sim.State,p,2);Check(ceiling>0&&ceiling<1,"ascending capsule contacts flying body above");
            sim=New(EnemyKind.Bat,Vector3.back*8,Vector3.up*.7f);e=sim.State.enemies[0];e.frozen=0;e.attackStyle=4;e.recovery=.42f;
            Walk(sim,new InputFrame(),1f/30);Check(e.position.y>.7f&&e.position.y<1,"body settlement preserves the low dive impact");
            Walk(sim,new InputFrame(),.45f);Check(e.position.y>1.5f,"diving creature recovers to hover height");
            sim=New(EnemyKind.StoneGolem,new Vector3(-1.5f,0,-3),Vector3.zero);p=sim.State.Player(0);Walk(sim,new InputFrame{x=.15f,z=1},1.5f);Check(p.position.z>3,"tangential motion slides around the body");
            var slow=New(EnemyKind.StoneGolem,Vector3.back*3,Vector3.zero);Walk(slow,new InputFrame{z=1},2,1f/60);Check(Mathf.Abs(slow.State.Player(0).position.z-stop)<.005f,"same contact at 30 and 60 steps");
            sim=new Simulation(712);p=sim.AddPlayer(0,"Crowd",Hero.Ember);sim.Start();sim.State.spawnTimer=10000;p.position=Vector3.zero;p.invulnerability=1000;p.weapons=new[]{-1,-1};
            for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20;var mob=sim.SpawnEnemy(EnemyKind.Skeleton);mob.position=new Vector3(Mathf.Cos(a)*4,0,Mathf.Sin(a)*4);mob.spawnGrace=0;}
            Walk(sim,new InputFrame(),5);
            foreach(var mob in sim.State.enemies)Check(!ActorBodies.Overlap(ActorBodies.Player(p),p.position,ActorBodies.Enemy(mob),mob.position,.015f),"crowd cannot enter player / "+mob.id);
            for(int i=0;i<sim.State.enemies.Count;i++)for(int j=i+1;j<sim.State.enemies.Count;j++)Check(!ActorBodies.Overlap(ActorBodies.Enemy(sim.State.enemies[i]),sim.State.enemies[i].position,ActorBodies.Enemy(sim.State.enemies[j]),sim.State.enemies[j].position,.025f),"crowd body spacing");
            var prop=new WorldProp{position=Vector3.forward*2,size=2,kind=1};WorldMap.Props.Add(prop);
            try {sim=New(EnemyKind.Ogre,Vector3.zero,Vector3.back*2);p=sim.State.Player(0);sim.State.enemies[0].frozen=0;Walk(sim,new InputFrame{z=1},3);Check(new Vector2(p.position.x-prop.position.x,p.position.z-prop.position.z).magnitude>=2.09f,"crowd pressure cannot push player into wall");}
            finally{WorldMap.Props.Remove(prop);}
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/body-checks.json","{\"ok\":true,\"assertions\":"+count+"}");return count;
        }
        [MenuItem("ERA/Check authoritative body collision")]
        public static void RunBatch(){int assertions=Run();Debug.Log("ERA_BODY_CHECKS_OK "+assertions);}
    }
}
