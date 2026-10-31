using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class ArenaView : MonoBehaviour
    {
        public Camera Camera {get;private set;}
        public HeroMotion Preview {get;private set;}
        public Vector3 CameraFollowTarget {get;private set;}
        public Vector3 CameraFocus {get;private set;}
        public Vector3 PlayerHealthAnchor(PlayerState player)
        {
            return PlayerVisualPosition(player)+Vector3.up*(player.crouching?2.1f:2.6f);
        }
        public Vector3 PlayerVisualPosition(PlayerState player)=>actors.TryGetValue(player.id,out var actor)?actor.root.transform.position:player.position;
        readonly VisualFactory v=new VisualFactory();
        EncounterView encounters;
        readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
        readonly Dictionary<int,GameObject> drops=new Dictionary<int,GameObject>(),loot=new Dictionary<int,GameObject>();
        readonly Dictionary<int,ProjectileVisual> bullets=new Dictionary<int,ProjectileVisual>();
        readonly HashSet<int> effectIds=new HashSet<int>(),hitIds=new HashSet<int>();
        readonly List<int> retiredIds=new List<int>();
        PresentationFx feedback;EffectPresentation spellEffects;float cameraHeight=1.65f,portalAge;
        readonly Dictionary<int,ChestVisual> chests=new Dictionary<int,ChestVisual>();
        readonly Dictionary<int,ShrineVisual> shrines=new Dictionary<int,ShrineVisual>();
        readonly Dictionary<int,GameObject> auraRings=new Dictionary<int,GameObject>();
        readonly Dictionary<int,GameObject> hazardRings=new Dictionary<int,GameObject>();
        readonly Dictionary<int,GameObject> itemFields=new Dictionary<int,GameObject>();
        WorldEnvironment environment;WorldTreeView worldTree;GameObject portalVisual;int presentedStage=-1,presentedSerial=-1;
        readonly MaterialPropertyBlock hitBlock=new MaterialPropertyBlock();
        readonly HashSet<int> seen=new HashSet<int>();
        Transform dynamicRoot,previewRoot;
        float shake,footstep;
        int lastCoins,lastLevel=1;
        class Actor {public GameObject root,weapon;public ActorBodyCollider body;public HeroMotion hero;public CreatureMotion creature;public Transform telegraph,attackTelegraph;public float hp=-1,hitFlash;public int jump,land,cast,weaponKind=-1,weaponSerial,hitSerial;public Hero kind;public Renderer[] renderers;public Color[][] colors;public Vector3 lastPosition;}
        class ChestVisual {public GameObject root;public Transform lid;public float openness;public bool announced;}
        void Awake()
        {
            if(FindAnyObjectByType<WorldEnvironment>()==null)new GameObject("World / Stillwater Reach").AddComponent<WorldEnvironment>().Build();
            WorldEnvironment.ApplyAtmosphere();environment=FindAnyObjectByType<WorldEnvironment>();if(environment!=null)SurfaceDetail.Apply(environment.transform);
            var cam=new GameObject("Player camera");Camera=cam.AddComponent<Camera>();cam.tag="MainCamera";cam.AddComponent<AudioListener>();
            Camera.fieldOfView=60;Camera.nearClipPlane=0.13f;Camera.farClipPlane=520;
            Camera.backgroundColor=new Color(0.64f,0.77f,0.78f);Camera.clearFlags=CameraClearFlags.Skybox;Camera.allowHDR=true;
            Camera.depthTextureMode=DepthTextureMode.Depth|DepthTextureMode.DepthNormals;
            cam.AddComponent<StillwaterLens>();
            dynamicRoot=new GameObject("Runtime / Heroes, creatures & loot").transform;
            var fxRoot=new GameObject("Reusable combat feedback");fxRoot.transform.SetParent(transform,false);feedback=fxRoot.AddComponent<PresentationFx>();spellEffects=fxRoot.AddComponent<EffectPresentation>();
            previewRoot=new GameObject("Character presentation").transform;
            Camera.transform.position=new Vector3(10,6,-14);
        }
        void LateUpdate()
        {
            var app=GameApp.Instance;if(app==null)return;
            int stage=app.State?.stage??1;
            if(presentedStage!=stage||presentedSerial!=(app.State?.stageSerial??0))
            {
                ClearActors();WorldMap.SetStage(stage);
                if(environment==null||environment.stage!=stage||environment.bakedVersion!=WorldEnvironment.CurrentVersion)
                {
                    if(environment!=null){environment.gameObject.SetActive(false);Destroy(environment.gameObject);}
                    environment=new GameObject("World / "+CampaignData.Name(stage)).AddComponent<WorldEnvironment>();environment.Build();
                }
                presentedStage=stage;presentedSerial=app.State?.stageSerial??0;
                if(app.LocalPlayer!=null)Camera.transform.position=app.LocalPlayer.position+new Vector3(0,5,-8);
            }
            if(worldTree==null)worldTree=new GameObject("World tree / persistent centre landmark").AddComponent<WorldTreeView>();
            worldTree.Present(stage,app.InRun);
            if(!app.InRun)
            {
                Camera.fieldOfView=app.Page==MenuPage.Characters?42:50;
                if(Preview==null || Preview.Hero!=app.SelectedHero)
                {if(Preview!=null)Destroy(Preview.gameObject);Preview=HeroMotion.Create(v,previewRoot,app.SelectedHero,"Selected wanderer");Preview.transform.position=new Vector3(1,0,-1);Preview.transform.localScale=Vector3.one*2;}
                Preview.gameObject.SetActive(true);Preview.Pose(Vector3.zero,true,0,0,false,false);
                Preview.transform.rotation=Quaternion.Euler(0,150+Mathf.Sin(Time.unscaledTime*0.3f)*7,0);
                bool character=app.Page==MenuPage.Characters || app.State!=null;
                Preview.transform.position=character?new Vector3(-.4f,0,0):new Vector3(1,0,-1);Preview.transform.localScale=Vector3.one*(character?1.65f:2);
                var target=character?new Vector3(4.2f,2.9f,-6.3f):new Vector3(12,7,-18);
                Camera.transform.position=Vector3.Lerp(Camera.transform.position,target,1-Mathf.Exp(-Time.unscaledDeltaTime*3));
                Camera.transform.LookAt(character?new Vector3(.15f,1.65f,0):new Vector3(-3,1.6f,3));
                if(app.State!=null)SyncChests(app.State);
                return;
            }
            if(Preview!=null)Preview.gameObject.SetActive(false);
            Camera.fieldOfView=60;
            var state=app.State;float blend=1-Mathf.Exp(-PresentationMotion.Delta*20);
            seen.Clear();
            foreach(var p in state.players)
            {
                seen.Add(p.id);
                if(!actors.TryGetValue(p.id,out var actor) || actor.kind!=p.hero)
                {
                    if(actor!=null)Destroy(actor.root);
                    var hero=HeroMotion.Create(v,dynamicRoot,p.hero,p.name);actor=new Actor{root=hero.gameObject,hero=hero,kind=p.hero,hp=p.hp,jump=p.jumpSerial,land=p.landSerial,cast=p.castSerial};actors[p.id]=actor;actor.root.transform.position=p.position;
                    v.Ring("Ally marker",actor.root.transform,0.67f,GameData.HeroColor(p.hero),0.035f).transform.localPosition=Vector3.up*0.045f;
                }
                actor.body??=ActorBodyCollider.Attach(actor.root);actor.body.Present(p);var costume=actor.root.GetComponent<CostumeMotion>();if(costume!=null)costume.owner=p.id;
                actor.root.SetActive(!p.eliminated);
                actor.root.transform.position=app.IsHost&&p.id==app.LocalId?app.LocalPresentationPosition(p):Vector3.Lerp(actor.root.transform.position,p.position,blend);
                actor.root.transform.rotation=actor.hero.archer!=null?Quaternion.Euler(0,actor.hero.archer.ResolveFacing(p,PresentationMotion.Delta),0):Quaternion.Slerp(actor.root.transform.rotation,Quaternion.Euler(0,p.yaw,0),blend);
                if(actor.root.GetComponent<AbilityPresentation>()==null)actor.root.AddComponent<AbilityPresentation>().owner=p.id;
                if(actor.hero.archer!=null)actor.hero.archer.Present(p);else if(actor.hero.lumen!=null)actor.hero.lumen.Present(p);else if(actor.hero.skeletal!=null)actor.hero.skeletal.Hero(p);else actor.hero.Pose(p.velocity,p.grounded,p.verticalSpeed,Mathf.Max(p.castTime,p.weaponPoseTime*2),p.downed,p.dashTime>0);
                if(actor.weaponKind!=p.lastWeapon&&p.lastWeapon>=0)
                {
                    if(actor.weapon!=null){actor.weapon.SetActive(false);Destroy(actor.weapon);}var wand=actor.hero.rightElbow.Find("Wand");if(wand!=null)wand.gameObject.SetActive(false);
                    actor.weapon=null;actor.weaponKind=p.lastWeapon;
                    // The authored archer bow/string are already skinned and animated.
                    // Legacy WeaponGrip would fight the baked Generic arm animation.
                    if(actor.hero.archer==null||p.lastWeapon!=(int)WeaponKind.Bow)
                    {
                        bool rune=actor.hero.lumen!=null&&p.lastWeapon==(int)WeaponKind.Aura;
                        actor.weapon=rune?LumenRuneFocus.Create(actor.hero.lumen,actor.root.transform,p.id):WeaponVisuals.Create(v,actor.hero.rightElbow,(WeaponKind)p.lastWeapon);
                        if(!rune){actor.weapon.transform.localPosition=actor.hero.skeletal!=null||actor.hero.archer!=null||actor.hero.lumen!=null?Vector3.zero:new Vector3(0,-.30f,.07f);if(actor.hero.skeletal!=null){actor.weapon.transform.localRotation=Quaternion.Euler(90,0,0);actor.weapon.transform.localScale=Vector3.one*.8f;}}
                        if(actor.hero.archer==null&&!rune){var grip=actor.weapon.AddComponent<WeaponGrip>();grip.actor=actor.root.transform;grip.hand=actor.hero.rightElbow;grip.owner=p.id;grip.kind=(WeaponKind)p.lastWeapon;}
                        actor.weapon.GetComponent<WeaponPresentation>()?.BindOwner(p.id);
                    }
                }
                if(p.weaponCastSerial!=actor.weaponSerial){SynthAudio.Instance?.PlayAttack((WeaponKind)p.lastWeapon,p.position);actor.weaponSerial=p.weaponCastSerial;}
                if(p.id==app.LocalId)
                {
                    if(p.jumpSerial!=actor.jump)SynthAudio.Instance?.Play(4);
                    if(p.landSerial!=actor.land){SynthAudio.Instance?.Play(5);PresentationFx.Emit(p.position,GameData.Amber,.85f,FeedbackKind.Dust);}

                    if(p.hp<actor.hp){shake=Mathf.Max(shake,Mathf.Clamp01((actor.hp-p.hp)/p.maxHp)*.6f);}
                    if(!app.WorldPaused && p.grounded && p.velocity.sqrMagnitude>4 && (footstep-=Time.unscaledDeltaTime)<=0)
                    {footstep=0.29f;SynthAudio.Instance?.Footstep();}
                }
                actor.jump=p.jumpSerial;actor.land=p.landSerial;actor.cast=p.castSerial;actor.hp=p.hp;
            }
            foreach(var e in state.enemies)
            {
                seen.Add(e.id);
                if(!actors.TryGetValue(e.id,out var actor))
                {var creature=e.kind==EnemyKind.Warden?BossArt.Create(v,dynamicRoot,Mathf.Max(1,e.bossStage)):CreatureMotion.Create(v,dynamicRoot,e.kind);actor=new Actor{root=creature.gameObject,creature=creature};actor.renderers=actor.root.GetComponentsInChildren<Renderer>();actor.colors=new Color[actor.renderers.Length][];for(int r=0;r<actor.renderers.Length;r++){var mats=actor.renderers[r].sharedMaterials;actor.colors[r]=new Color[mats.Length];for(int m=0;m<mats.Length;m++)actor.colors[r][m]=mats[m]!=null&&mats[m].HasProperty("_Color")?mats[m].color:Color.white;}actors[e.id]=actor;actor.root.transform.position=e.position;var target=state.players.Find(p=>!p.downed&&!p.eliminated);if(target!=null){var facing=target.position-e.position;facing.y=0;if(facing.sqrMagnitude>.01f)actor.root.transform.rotation=Quaternion.LookRotation(facing);}}
                var visualBefore=actor.root.transform.position;
                var direction=e.position-actor.root.transform.position;direction.y=0;
                if(e.eliteShrine>=0&&actor.root.transform.Find("Elite guardian halo")==null){actor.root.transform.localScale*=1.2f;v.Ring("Elite guardian halo",actor.root.transform,.9f,new Color(1,.25f,.12f),.065f).transform.localPosition=Vector3.up*.06f;}
                if(direction.sqrMagnitude>0.0004f)actor.root.transform.rotation=Quaternion.Slerp(actor.root.transform.rotation,Quaternion.LookRotation(direction),blend);
                actor.root.transform.position=Vector3.Lerp(actor.root.transform.position,e.position,blend);
                if(e.kind>=EnemyKind.Skeleton||e.kind==EnemyKind.Brute)actor.root.transform.rotation=Quaternion.Slerp(actor.root.transform.rotation,Quaternion.Euler(0,e.facing,0),blend);
                if(e.kind==EnemyKind.Slime&&e.summonGeneration>0)actor.root.transform.localScale=Vector3.one*.6f;
                if(e.hitSerial!=actor.hitSerial){if(!EnemyCatalog.EffectImmune(e.kind))actor.creature.Hit(app.LocalPlayer!=null?(e.position-app.LocalPlayer.position).normalized:-actor.root.transform.forward);actor.hitFlash=.13f;}actor.hp=e.hp;actor.hitSerial=e.hitSerial;
                bool flashed=actor.hitFlash>0;if(!app.WorldPaused)actor.hitFlash=Mathf.Max(0,actor.hitFlash-Time.unscaledDeltaTime);
                if(flashed)for(int r=0;r<actor.renderers.Length;r++)for(int m=0;m<actor.colors[r].Length;m++){if(actor.hitFlash<=0){actor.renderers[r].SetPropertyBlock(null,m);continue;}hitBlock.Clear();hitBlock.SetColor("_Color",Color.Lerp(actor.colors[r][m],Color.white,Mathf.Clamp01(actor.hitFlash/.13f)*.55f*(.35f+.65f*PresentationMotion.Intensity)));actor.renderers[r].SetPropertyBlock(hitBlock,m);}
                if(actor.telegraph==null&&e.spawnGrace>0){actor.telegraph=v.Ring("Enemy arrival warning",dynamicRoot,.75f,new Color(.91f,.32f,.15f),.045f).transform;actor.telegraph.position=e.position+Vector3.up*.06f;}
                if(actor.telegraph!=null&&e.spawnGrace<=0){Destroy(actor.telegraph.gameObject);actor.telegraph=null;}
                if(e.meleeWindup>0)
                {
                    if(actor.attackTelegraph==null)actor.attackTelegraph=v.Ring("Attack hitbox warning",dynamicRoot,1,new Color(1,.19f,.08f),.07f).transform;
                    Vector3 center=e.attackStyle==3||e.attackStyle==1?e.attackPosition:e.position+e.attackDirection*EnemyCatalog.Reach(e.kind)*.5f;center.y=WorldMap.Ground(center)+.08f;
                    actor.attackTelegraph.position=center;actor.attackTelegraph.localScale=Vector3.one*(e.attackStyle==3?3:e.attackStyle==1?.8f:e.attackStyle==2?2.5f:EnemyCatalog.Reach(e.kind)*.65f);
                }
                else if(actor.attackTelegraph!=null){Destroy(actor.attackTelegraph.gameObject);actor.attackTelegraph=null;}
                // Articulated creatures use render displacement, not sparse 30 Hz state deltas.
                bool articulated=actor.creature.golem!=null||actor.creature.bat!=null;
                var travel=articulated?actor.root.transform.position-visualBefore:e.position-actor.lastPosition;if(articulated)travel.y=0;
                float measured=PresentationMotion.Delta>0?Mathf.Min(e.speed*(articulated?1.6f:1),travel.magnitude/PresentationMotion.Delta):0;
                actor.body??=ActorBodyCollider.Attach(actor.root);actor.body.Present(e);
                var status=actor.root.GetComponent<StatusPresentation>();if(status==null)status=actor.root.AddComponent<StatusPresentation>();status.Present(e);
                actor.creature.Present(e,measured);actor.lastPosition=e.position;
            }
            var dead=new List<int>();foreach(var pair in actors)if(!seen.Contains(pair.Key))dead.Add(pair.Key);
            foreach(int id in dead)
            {var a=actors[id];a.body?.Retire();if(a.telegraph!=null)Destroy(a.telegraph.gameObject);if(a.attackTelegraph!=null)Destroy(a.attackTelegraph.gameObject);if(a.creature!=null)a.creature.Die();else Destroy(a.root);actors.Remove(id);}
            SyncHits(state);
            SyncAuras(state);SyncPortal(state);SyncHazards(state);SyncItemFields(state);SyncProjectiles(state,blend);SyncPickups(state,blend);SyncLoot(state);SyncEffects(state);SyncChests(state);
            encounters??=new EncounterView(v,dynamicRoot);encounters.Present(state);
            if(state.coins>lastCoins)SynthAudio.Instance?.Play(7);
            if(state.level>lastLevel)SynthAudio.Instance?.Play(9);
            lastCoins=state.coins;lastLevel=state.level;FollowCamera(app);
        }
        void FollowCamera(GameApp app)
        {
            var p=app.LocalPlayer;if(p==null)return;
            if(p.eliminated){var partner=app.State.players.Find(other=>!other.eliminated&&!other.downed);if(partner!=null)p=partner;}
            Vector3 target=actors.TryGetValue(p.id,out var actor)?actor.root.transform.position:p.position;
            // Camera and visible actor must share one presentation timeline.
            // The archer now interpolates its root too; a separate focus would
            // hide camera jitter while leaving the character stepping at 30 Hz.
            CameraFollowTarget=target;
            cameraHeight=PresentationMotion.Damp(cameraHeight,p.crouching?1.12f:1.65f,12,PresentationMotion.Delta);
            // Orbit around the visible character, including when looking upward.
            // Terrain shortens the boom rather than decoupling the view from its pivot.
            float lookPitch=Mathf.Clamp(app.CameraPitch,GameApp.MinCameraPitch,GameApp.MaxCameraPitch);
            float orbitPitch=lookPitch;
            var pivot=target+Vector3.up*cameraHeight;var rotation=Quaternion.Euler(orbitPitch,app.CameraYaw,0);
            var desired=pivot+rotation*new Vector3(0,0.25f,-8.7f);
            Vector3 ray=(desired-pivot).normalized;float distance=Vector3.Distance(pivot,desired),safe=distance;
            foreach(var prop in WorldMap.Props)
            {
                var center=prop.position+Vector3.up*(prop.kind==0?4.2f:0.7f)*prop.size;
                safe=CameraClearance(pivot,ray,safe,center,(prop.kind==0?2.3f:0.95f)*prop.size);
            }

            foreach(var enemy in app.State.enemies)
                safe=CameraClearance(pivot,ray,safe,enemy.position+Vector3.up*(enemy.kind==EnemyKind.Warden?2.8f:enemy.kind==EnemyKind.Brute?1.5f:0.9f),enemy.kind==EnemyKind.Warden?2.2f:enemy.kind==EnemyKind.Brute?1.15f:0.5f);
            float lastClear=0;
            for(int i=1;i<=48;i++)
            {
                float along=safe*i/48f;var sample=pivot+ray*along;
                if(sample.y<WorldMap.Ground(sample)+.65f||WorldTreeLayout.ContainsTrunk(sample,.25f))
                {
                    float lo=lastClear,hi=along;
                    for(int j=0;j<10;j++){float mid=(lo+hi)*.5f;var q=pivot+ray*mid;if(q.y<WorldMap.Ground(q)+.65f||WorldTreeLayout.ContainsTrunk(q,.25f))hi=mid;else lo=mid;}
                    safe=lo;break;
                }
                lastClear=along;
            }
            desired=pivot+ray*Mathf.Max(.05f,safe);
            var nextCamera=Vector3.Lerp(Camera.transform.position,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            nextCamera.y=Mathf.Max(nextCamera.y,WorldMap.Ground(nextCamera)+.65f);
            // Collision corrections apply immediately; never smooth through a solid root.
            nextCamera=WorldTreeLayout.ResolveTrunk(nextCamera,.25f);
            Camera.transform.position=nextCamera;CameraFocus=pivot;
            Camera.transform.LookAt(pivot);
            if(shake>0){shake=Mathf.Max(0,shake-PresentationMotion.Delta);float t=app.State.elapsed;Camera.transform.position+=new Vector3(Mathf.Sin(t*53),Mathf.Sin(t*67+.4f),0)*shake*.12f*PresentationMotion.Intensity;}
        }
        static float CameraClearance(Vector3 pivot,Vector3 direction,float distance,Vector3 center,float radius)
        {
            var delta=center-pivot;float along=Vector3.Dot(delta,direction);
            if(along<1.8f||along>distance+radius)return distance;
            float side=delta.sqrMagnitude-along*along;if(side>=radius*radius)return distance;
            return Mathf.Min(distance,Mathf.Max(.05f,along-Mathf.Sqrt(radius*radius-side)-0.35f));
        }
        void SyncItemFields(WorldState state)
        {
            seen.Clear();foreach(var f in state.itemFields)
            {
                seen.Add(f.id);if(!itemFields.TryGetValue(f.id,out var go)){go=new GameObject("Item mechanic / "+(ItemId)f.item);go.transform.SetParent(dynamicRoot,false);go.AddComponent<ItemFieldPresentation>();itemFields[f.id]=go;}
                go.GetComponent<ItemFieldPresentation>().Present(f,state);
            }RemoveAbsent(itemFields);
        }
        void SyncProjectiles(WorldState state,float blend)
        {
            seen.Clear();foreach(var b in state.projectiles)
            {
                seen.Add(b.id);if(!bullets.TryGetValue(b.id,out var visual)){visual=ProjectileVisual.Create(v,dynamicRoot,b,state);bullets[b.id]=visual;}
                visual.Sync(b,blend,PresentationMotion.Delta);
            }
            retiredIds.Clear();foreach(var pair in bullets)if(!seen.Contains(pair.Key)){pair.Value.Retire(pair.Value.LikelyImpactOnRemoval);retiredIds.Add(pair.Key);}foreach(int id in retiredIds)bullets.Remove(id);
        }
        void SyncPickups(WorldState state,float blend)
        {
            seen.Clear();foreach(var p in state.pickups)
            {
                seen.Add(p.id);if(!drops.TryGetValue(p.id,out var go))
                {
                    go=p.kind==PickupKind.Gold?v.Shape("Gold petal",PrimitiveType.Cylinder,dynamicRoot,p.position,new Vector3(0.29f,0.045f,0.29f),new Color(0.95f,0.75f,0.34f),true):WorldVfx.Crystal(v,dynamicRoot,"Blue experience crystal",p.position,.40f,new Color(.09f,.48f,1));drops[p.id]=go;
                }
                float pop=Mathf.Sin(Mathf.Clamp01(p.age/.5f)*Mathf.PI)*.85f;
                go.transform.position=Vector3.Lerp(go.transform.position,p.position+Vector3.up*(0.45f+pop+Mathf.Sin(state.elapsed*3+p.id)*0.10f),blend);
                go.transform.localScale=(p.kind==PickupKind.Gold?new Vector3(.29f,.045f,.29f):Vector3.one*.40f)*Mathf.Lerp(.1f,1,Mathf.Clamp01(p.age*5));
                go.transform.rotation=Quaternion.Euler(p.kind==PickupKind.Gold?90:0,state.elapsed*65+p.id,0);
            }CollectAbsent(drops,state);
        }
        void SyncLoot(WorldState state)
        {
            seen.Clear();foreach(var drop in state.loot)
            {
                seen.Add(drop.id);if(!loot.TryGetValue(drop.id,out var go))
                {
                    go=v.Group("Chest reward / "+drop.content,dynamicRoot).gameObject;
                    if(drop.kind==RewardKind.Weapon){var model=WeaponVisuals.Create(v,go.transform,(WeaponKind)drop.content);model.transform.localScale=Vector3.one*.65f;}
                    else
                    {
                        go.AddComponent<LootPresentation>().Initialize(drop);
                    }
                    Color color=drop.kind==RewardKind.Weapon?GameData.Weapons[drop.content].color:GameData.Items[drop.content].color;
                    if(drop.kind==RewardKind.Weapon)v.Ring("Reward halo",go.transform,.50f,color,.025f).transform.localPosition=Vector3.down*.38f;loot[drop.id]=go;
                }
                var presentation=go.GetComponent<LootPresentation>();if(presentation!=null){presentation.Present(drop,Camera);continue;}
                go.transform.position=drop.position+Vector3.up*(drop.collecting?0:Mathf.Sin(state.elapsed*2.2f+drop.id)*.055f);
                go.transform.localScale=Vector3.one*(drop.collecting?Mathf.Lerp(1,.2f,PresentationMotion.Ease(drop.collectTime/.35f)):Mathf.Lerp(.1f,1,PresentationMotion.Out(drop.age/.22f)));
                var pictureTransform=go.transform.Find("Item picture");if(pictureTransform!=null)pictureTransform.rotation=Camera.transform.rotation;
            }CollectAbsent(loot,state);
        }
        void SyncAuras(WorldState state)
        {
            seen.Clear();foreach(var p in state.players)
            {
                if(p.hero!=Hero.Lumen||p.downed||p.eliminated||Simulation.AuraRadius(p)<=0)continue;
                seen.Add(p.id);if(!auraRings.TryGetValue(p.id,out var go)){go=new GameObject("Lumen support light");go.transform.SetParent(dynamicRoot,false);go.AddComponent<SupportAuraPresentation>();auraRings[p.id]=go;}
                go.GetComponent<SupportAuraPresentation>().Present(p,state);
            }RemoveAbsent(auraRings);
        }
        void SyncPortal(WorldState state)
        {
            if(!state.portal.active){if(portalVisual!=null)portalVisual.SetActive(false);return;}
            if(portalVisual==null)
            {
                var root=v.Group("Passage to the next realm",dynamicRoot);portalVisual=root.gameObject;
                var color=CampaignData.Color(Mathf.Min(7,state.stage+1));
                var ring=v.Ring("Portal arch",root,1.8f,color,.095f);ring.transform.localPosition=Vector3.up*1.9f;ring.transform.localRotation=Quaternion.Euler(90,0,0);
                v.Ring("Portal ground seal",root,2.3f,color,.035f).transform.localPosition=Vector3.up*.06f;
                var surface=v.Shape("Rift surface",PrimitiveType.Quad,root,Vector3.up*1.9f,new Vector3(3.35f,3.35f,1),color);
                var material=new Material(Shader.Find("Era/Rift Portal"));material.SetColor("_Color",color);surface.GetComponent<Renderer>().sharedMaterial=material;
                surface.AddComponent<OwnedVisualResource>().material=material;
                for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;WorldVfx.Crystal(v,root,"Portal rune",new Vector3(Mathf.Cos(a)*1.9f,1.9f+Mathf.Sin(a)*1.9f,0),.16f,color);}
            }
            if(portalAge==0)PresentationFx.Emit(state.portal.position,CampaignData.Color(Mathf.Min(7,state.stage+1)),3,FeedbackKind.Summon);
            portalAge+=PresentationMotion.Delta;portalVisual.SetActive(true);portalVisual.transform.position=state.portal.position;portalVisual.transform.localScale=Vector3.one*PresentationMotion.Out(portalAge/.7f);
        }
        void SyncHazards(WorldState state)
        {
            seen.Clear();foreach(var h in state.hazards)
            {
                seen.Add(h.id);if(!hazardRings.TryGetValue(h.id,out var ring))
                {
                    ring=v.Ring("Boss attack / "+h.style,dynamicRoot,h.radius,CampaignData.Color(h.style),.07f);ring.transform.position=h.position+Vector3.up*.09f;
                    var line=ring.GetComponent<LineRenderer>();for(int i=0;i<line.positionCount;i++){var p=line.GetPosition(i);p.y=WorldMap.Height(p.x+h.position.x,p.z+h.position.z)-h.position.y;line.SetPosition(i,p);}
                    ring.AddComponent<HazardPresentation>();hazardRings[h.id]=ring;
                }
                ring.GetComponent<HazardPresentation>().Present(h);var lr=ring.GetComponent<LineRenderer>();lr.widthMultiplier=h.triggered?.20f:.06f+(.5f+.5f*Mathf.Sin(state.elapsed*12))*.025f;
                lr.enabled=!h.triggered;Color color=new Color(.95f,.28f,.12f,.62f);lr.startColor=lr.endColor=color;
            }RemoveAbsent(hazardRings);
        }
        void SyncEffects(WorldState state)
        {
            seen.Clear();foreach(var e in state.effects){seen.Add(e.id);if(effectIds.Add(e.id)){spellEffects.Play(e);}}
            effectIds.RemoveWhere(id=>!seen.Contains(id));
        }
        void SyncHits(WorldState state)
        {
            seen.Clear();foreach(var h in state.hits)
            {
                seen.Add(h.id);if(!hitIds.Add(h.id))continue;
                if(h.evaded){PresentationFx.EmitStyle(h.position,FeedbackStyle.Speed,.25f);SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Speed,h.position);continue;}
                if(h.absorbed>0&&actors.TryGetValue(h.target,out var shielded)){shielded.root.GetComponent<AbilityPresentation>()?.ShieldImpact();PresentationFx.EmitStyle(h.position,FeedbackStyle.Shield,.35f);SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Shield,h.position);}
                if(h.player&&h.absorbed>=h.damage&&h.damage>0)continue;
                Color color=h.player?new Color(.95f,.3f,.26f):h.critical?Color.white:GameData.Amber;
                if(actors.TryGetValue(h.target,out var actor))
                {
                    if(actor.creature!=null){var kind=actor.creature.kind;if(kind==EnemyKind.StoneGolem||kind==EnemyKind.Gargoyle)color=new Color(.65f,.66f,.58f);else if(kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime)color=GameData.Mint;else if(kind==EnemyKind.CrystalGolem)color=new Color(.55f,.65f,1);if(h.critical)actor.creature.FreezePose(.035f);}
                    else if(h.player)actor.hero?.skeletal?.FreezePose(.025f);
                }
                PresentationFx.Emit(h.position,color,h.critical?.25f:.20f,h.critical?FeedbackKind.Critical:FeedbackKind.Impact);
                SynthAudio.Instance?.PlaySemantic(h.critical?FeedbackStyle.Critical:FeedbackStyle.Impact,h.position);
            }
            hitIds.RemoveWhere(id=>!seen.Contains(id));
        }
        void CollectAbsent(Dictionary<int,GameObject> dict,WorldState state)
        {
            retiredIds.Clear();foreach(var pair in dict)if(!seen.Contains(pair.Key))
            {
                var target=state.players.Find(p=>!p.downed&&!p.eliminated&&(p.position-pair.Value.transform.position).sqrMagnitude<8*8);
                if(target!=null)PickupRetirement.Begin(pair.Value,target.id);else Destroy(pair.Value);
                retiredIds.Add(pair.Key);
            }
            foreach(int id in retiredIds)dict.Remove(id);
        }
        void SyncChests(WorldState state)
        {
            foreach(var shrine in state.shrines)
            {if(!shrines.TryGetValue(shrine.id,out var visual)){visual=ShrineVisual.Create(v,dynamicRoot,shrine);shrines[shrine.id]=visual;}visual.Present(shrine,state.elapsed);}
            foreach(var c in state.chests)
            {
                if(!chests.TryGetValue(c.id,out var chest))
                {
                    var root=ChestArt.Create(v,dynamicRoot,c,out var lid);
                    chest=new ChestVisual{root=root.gameObject,lid=lid,announced=c.opened};chests[c.id]=chest;
                }
                chest.openness=Mathf.MoveTowards(chest.openness,c.opened?1:0,PresentationMotion.Delta*1.8f);
                float opening=PresentationMotion.Out(Mathf.InverseLerp(.08f,.87f,chest.openness));float settle=Mathf.Sin(Mathf.InverseLerp(.65f,1,chest.openness)*Mathf.PI)*4;
                chest.lid.localRotation=Quaternion.Euler(-opening*105-settle,0,0);
                if(c.opened && !chest.announced){chest.announced=true;SynthAudio.Instance?.Play(8);PresentationFx.Emit(c.position+Vector3.up*.7f,GameData.RarityColors[Mathf.Clamp(c.rewardRarity,0,GameData.RarityColors.Length-1)],1.3f,FeedbackKind.Summon);}
            }
        }
        void RemoveAbsent(Dictionary<int,GameObject> dict)
        {var dead=new List<int>();foreach(int id in dict.Keys)if(!seen.Contains(id))dead.Add(id);foreach(int id in dead){Destroy(dict[id]);dict.Remove(id);}}
        public void ClearActors()
        {
            if(dynamicRoot!=null)foreach(Transform child in dynamicRoot)Destroy(child.gameObject);
            feedback?.Clear();spellEffects?.Clear();effectIds.Clear();hitIds.Clear();actors.Clear();bullets.Clear();drops.Clear();loot.Clear();chests.Clear();shrines.Clear();auraRings.Clear();hazardRings.Clear();itemFields.Clear();portalVisual=null;portalAge=0;encounters=null;lastCoins=0;lastLevel=1;shake=0;
        }
        void OnDestroy(){if(dynamicRoot!=null)Destroy(dynamicRoot.gameObject);if(previewRoot!=null)Destroy(previewRoot.gameObject);if(Camera!=null)Destroy(Camera.gameObject);v.Dispose();}
    }
}
