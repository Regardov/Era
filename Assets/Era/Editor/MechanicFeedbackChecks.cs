using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class MechanicFeedbackChecks
    {
        static int count;
        static void Check(bool value,string label){count++;if(!value)throw new Exception("ERA mechanic feedback: "+label);}
        static Simulation New(params ItemId[] items)
        {
            var s=new Simulation(712);var p=s.AddPlayer(0,"Feedback",Hero.Ember);s.Start();s.State.spawnTimer=10000;s.State.resourceNodes.Clear();p.position=WorldMap.OnGround(105,80);p.weapons=new[]{-1,-1};p.critChance=0;
            foreach(var item in items)s.ApplyItem(p,(int)item);s.State.effects.Clear();return s;
        }
        static EnemyState Target(Simulation s,EnemyKind kind=EnemyKind.Skeleton)
        {var e=s.SpawnEnemy(kind);e.position=WorldMap.OnGround(105,84);e.hp=e.maxHp=10000;e.frozen=0;e.spawnGrace=0;return e;}
        static void Step(Simulation s,int frames=1){for(int i=0;i<frames;i++)s.Step(1f/30,new Dictionary<int,InputFrame>());}
        public static int Run()
        {
            count=0;
            foreach(ItemId item in Enum.GetValues(typeof(ItemId)))Check(MechanicFeedback.Reviewed((int)item),"reviewed real item "+item);
            var hazards=new[]{FeedbackStyle.Impact,FeedbackStyle.Impact,FeedbackStyle.Frost,FeedbackStyle.Fire,FeedbackStyle.Poison,FeedbackStyle.Lightning,FeedbackStyle.Void};
            for(int i=0;i<hazards.Length;i++)Check(MechanicFeedback.Hazard(i+1)==hazards[i],"boss attack matches its real theme: "+(i+1));
            foreach(var pair in new[]{(ItemId.FlameBlade,Ailment.Burn,FeedbackStyle.Fire),(ItemId.IceCore,Ailment.Chill,FeedbackStyle.Frost),(ItemId.VenomNeedle,Ailment.Poison,FeedbackStyle.Poison)})
            {
                var s=New(pair.Item1);var e=Target(s);s.DamageEnemy(e,s.State.Player(0),10);
                Check(e.ailments.Exists(a=>a.kind==pair.Item2&&a.visual==pair.Item3),"actual status owns its visual: "+pair.Item1);
                var boss=Target(s,EnemyKind.Warden);s.DamageEnemy(boss,s.State.Player(0),10);Check(boss.ailments.Count==0,"boss retains status immunity: "+pair.Item1);
            }
            var steam=New(ItemId.FlameBlade,ItemId.IceCore,ItemId.SteamValve);var victim=Target(steam);steam.DamageEnemy(victim,steam.State.Player(0),10);
            Check(steam.State.effects.Exists(e=>e.style==FeedbackStyle.Steam)&&!victim.ailments.Exists(a=>a.kind==Ailment.Burn||a.kind==Ailment.Chill),"steam requires and consumes fire plus frost");
            var echo=New(ItemId.TimeMaster);victim=Target(echo);var player=echo.State.Player(0);echo.DamageEnemy(victim,player,100,true,true);
            Check(echo.State.itemFields.Count==1&&echo.State.itemFields[0].visual==FeedbackStyle.Echo,"delayed echo retains source style");Step(echo,32);
            Check(echo.State.effects.Exists(e=>e.style==FeedbackStyle.Echo)&&Mathf.Abs(victim.hp-9880)<.01f,"echo occurs only at delayed damage and preserves twenty percent");
            var root=New(ItemId.Overgrowth);root.State.itemFields.Add(new ItemFieldState{id=900,owner=0,item=(int)ItemId.Overgrowth,position=WorldMap.OnGround(105,84),time=2,radius=4,power=10,visual=FeedbackStyle.Root});victim=Target(root);Step(root);
            Check(victim.ailments.Exists(a=>a.kind==Ailment.Chill&&a.visual==FeedbackStyle.Root),"root slow remains distinct from frost");
            var shield=New();player=shield.State.Player(0);player.shield=20;shield.DamagePlayer(player,12);
            Check(Mathf.Abs(player.shield-8)<.01f&&shield.State.hits.Exists(h=>Mathf.Abs(h.absorbed-12)<.01f),"shield hit reports actual absorbed amount");
            shield.State.hits.Clear();Step(shield);Check(shield.State.hits.Count==0,"passive shield decay cannot trigger impact");
            var crit=New();player=crit.State.Player(0);player.critChance=1;victim=Target(crit,EnemyKind.Slime);crit.DamageEnemy(victim,player,10);
            Check(crit.State.hits.Exists(h=>h.critical&&h.position.y<victim.position.y+1.2f),"real critical uses the short enemy hit point");
            var pickup=New();player=pickup.State.Player(0);pickup.ApplyItem(player,(int)ItemId.SharpBlade);
            Check(pickup.State.effects.Exists(e=>e.style==FeedbackStyle.Pickup)&&!pickup.State.effects.Exists(e=>MechanicFeedback.Style(e)==FeedbackStyle.Heal),"stat pickup cannot invent healing");
            var state=new WorldState();state.effects.Add(new EffectState{style=FeedbackStyle.Steam});state.hits.Add(new HitState{absorbed=12});state.itemFields.Add(new ItemFieldState{visual=FeedbackStyle.Lightning});var enemy=new EnemyState();enemy.ailments.Add(new AfflictionState{visual=FeedbackStyle.Root});state.enemies.Add(enemy);
            var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(state));Check(copy.effects[0].style==FeedbackStyle.Steam&&copy.hits[0].absorbed==12&&copy.itemFields[0].visual==FeedbackStyle.Lightning&&copy.enemies[0].ailments[0].visual==FeedbackStyle.Root,"network snapshot preserves semantic feedback");
            Check(PresentationFx.Capacity*PresentationFx.ParticlesPerSlot<=1344&&SynthAudio.CombatVoices==16&&SynthAudio.CombatFrameBudget==5,"bounded particle and audio budgets");
            foreach(bool conifer in new[]{false,true})Tree(conifer);
            WorldMap.SetStage(1);Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/feedback-checks.json","{\"ok\":true,\"assertions\":"+count+"}");return count;
        }
        static void Tree(bool conifer)
        {
            var root=SceneryBuilder.TreePreview(null,conifer);
            try
            {
                int triangles=0;
                foreach(var f in root.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=f.sharedMesh;var v=mesh.vertices;var uv=mesh.uv;var t=mesh.triangles;triangles+=t.Length/3;bool crown=f.name=="Dense crown";
                    if(crown)
                    {
                        var material=f.GetComponent<Renderer>().sharedMaterial;
                        Check(material.GetTag("RenderType",false)=="Transparent"&&material.renderQueue>=3000&&material.GetFloat("_Opacity")>.3f&&material.GetFloat("_Opacity")<.8f,"canopy uses translucent leaf material");
                        Check(Array.TrueForAll(uv,q=>q.x>-.5f)&&mesh.colors.Length==v.Length,"canopy contains individual colored leaves without a solid shell");
                        for(int i=0;i<t.Length;i+=3)Check(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-12f,"nondegenerate individual leaf surface");
                        Check(mesh.bounds.size.x>3.5f&&mesh.bounds.size.y>4&&t.Length/3>6000,"lush volumetric crown silhouette");continue;
                    }
                    var weld=new Dictionary<Vector3Int,int>();var indices=new int[v.Length];
                    for(int i=0;i<v.Length;i++){var k=Vector3Int.RoundToInt(v[i]*10000);if(!weld.TryGetValue(k,out int id)){id=weld.Count;weld.Add(k,id);}indices[i]=id;}
                    var edges=new Dictionary<(int,int),(int count,int winding)>();double volume=0;int faces=0;
                    for(int i=0;i<t.Length;i+=3)
                    {
                        int a=t[i],b=t[i+1],c=t[i+2];if(crown&&uv[a].x>=0)continue;faces++;
                        Check(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude>1e-12f,"nondegenerate closed tree surface");volume+=Vector3.Dot(v[a],Vector3.Cross(v[b],v[c]))/6.0;
                        Edge(edges,indices[a],indices[b]);Edge(edges,indices[b],indices[c]);Edge(edges,indices[c],indices[a]);
                    }
                    foreach(var edge in edges.Values)Check(edge.count==2&&edge.winding==0,"every solid tree edge has two consistently wound faces");
                    Check(volume>1&&faces>200,"outward dense closed volume "+f.name);
                    if(crown)Check(mesh.bounds.size.x>3.5f&&mesh.bounds.size.y>4,"volumetric crown silhouette");
                }
                Check(triangles<22000&&root.GetComponentsInChildren<Renderer>().Length==2,"two batched renderers and bounded lush tree geometry");
            }
            finally
            {
                foreach(var f in root.GetComponentsInChildren<MeshFilter>())UnityEngine.Object.DestroyImmediate(f.sharedMesh);
                foreach(var r in root.GetComponentsInChildren<Renderer>())UnityEngine.Object.DestroyImmediate(r.sharedMaterial);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        static void Edge(Dictionary<(int,int),(int count,int winding)> edges,int a,int b)
        {var key=(Mathf.Min(a,b),Mathf.Max(a,b));edges.TryGetValue(key,out var old);edges[key]=(old.count+1,old.winding+(a<b?1:-1));}
        [MenuItem("ERA/Check mechanic feedback and closed trees")]
        public static void RunBatch(){Debug.Log("ERA_FEEDBACK_CHECKS_OK "+Run());}
    }
}
