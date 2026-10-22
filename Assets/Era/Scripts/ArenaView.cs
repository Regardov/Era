using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class ArenaView : MonoBehaviour
    {
        public Camera Camera {get;private set;}
        public HeroMotion Preview {get;private set;}
        readonly VisualFactory v=new VisualFactory();
        EncounterView encounters;
        readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
        readonly Dictionary<int,GameObject> bullets=new Dictionary<int,GameObject>(),drops=new Dictionary<int,GameObject>(),effects=new Dictionary<int,GameObject>(),loot=new Dictionary<int,GameObject>();
        readonly Dictionary<int,ChestVisual> chests=new Dictionary<int,ChestVisual>();
        readonly Dictionary<int,ShrineVisual> shrines=new Dictionary<int,ShrineVisual>();
        readonly Dictionary<int,GameObject> auraRings=new Dictionary<int,GameObject>();
        readonly Dictionary<int,GameObject> hazardRings=new Dictionary<int,GameObject>();
        WorldEnvironment environment;GameObject portalVisual;int presentedStage=-1,presentedSerial=-1;
        readonly MaterialPropertyBlock hitBlock=new MaterialPropertyBlock();
        readonly HashSet<int> seen=new HashSet<int>();
        Transform dynamicRoot,previewRoot;
        float shake,footstep;
        int lastCoins,lastLevel=1;
        class Actor {public GameObject root,weapon;public HeroMotion hero;public CreatureMotion creature;public Transform telegraph;public float hp=-1,hitFlash;public int jump,land,cast,weaponKind=-1,weaponSerial,hitSerial;public Hero kind;public Renderer[] renderers;public Color[] colors;}
        class ChestVisual {public GameObject root;public Transform lid;public float openness;public bool announced;}
        void Awake()
        {
            if(FindAnyObjectByType<WorldEnvironment>()==null)new GameObject("World / Stillwater Reach").AddComponent<WorldEnvironment>().Build();
            WorldEnvironment.ApplyAtmosphere();environment=FindAnyObjectByType<WorldEnvironment>();if(environment!=null)SurfaceDetail.Apply(environment.transform);
            var cam=new GameObject("Player camera");Camera=cam.AddComponent<Camera>();cam.tag="MainCamera";cam.AddComponent<AudioListener>();
            Camera.fieldOfView=60;Camera.nearClipPlane=0.13f;Camera.farClipPlane=360;
            Camera.backgroundColor=new Color(0.64f,0.77f,0.78f);Camera.clearFlags=CameraClearFlags.Skybox;Camera.allowHDR=true;
            cam.AddComponent<StillwaterLens>();
            dynamicRoot=new GameObject("Runtime / Heroes, creatures & loot").transform;
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
            var state=app.State;float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*20);
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
                actor.root.SetActive(!p.eliminated);
                actor.root.transform.position=Vector3.Lerp(actor.root.transform.position,p.position,blend);
                actor.root.transform.rotation=Quaternion.Slerp(actor.root.transform.rotation,Quaternion.Euler(0,p.yaw,0),blend);
                if(actor.hero.skeletal!=null)actor.hero.skeletal.Hero(p);else actor.hero.Pose(p.velocity,p.grounded,p.verticalSpeed,Mathf.Max(p.castTime,p.weaponPoseTime*2),p.downed,p.dashTime>0);
                if(actor.weaponKind!=p.lastWeapon&&p.lastWeapon>=0)
                {
                    if(actor.weapon!=null){actor.weapon.SetActive(false);Destroy(actor.weapon);}var wand=actor.hero.rightElbow.Find("Wand");if(wand!=null)wand.gameObject.SetActive(false);
                    actor.weapon=WeaponVisuals.Create(v,actor.hero.rightElbow,(WeaponKind)p.lastWeapon);actor.weapon.transform.localPosition=actor.hero.skeletal!=null?Vector3.zero:new Vector3(0,-.30f,.07f);if(actor.hero.skeletal!=null){actor.weapon.transform.localRotation=Quaternion.Euler(90,0,0);actor.weapon.transform.localScale=Vector3.one*.8f;}actor.weaponKind=p.lastWeapon;
                    if(actor.hero.skeletal!=null){var grip=actor.weapon.AddComponent<WeaponGrip>();grip.actor=actor.root.transform;grip.hand=actor.hero.rightElbow;grip.owner=p.id;grip.kind=(WeaponKind)p.lastWeapon;}
                }
                if(p.weaponCastSerial!=actor.weaponSerial){if(p.id==app.LocalId)SynthAudio.Instance?.Play(p.lastWeapon==(int)WeaponKind.Bow?0:1);actor.weaponSerial=p.weaponCastSerial;}
                if(p.id==app.LocalId)
                {
                    if(p.jumpSerial!=actor.jump)SynthAudio.Instance?.Play(4);
                    if(p.landSerial!=actor.land){SynthAudio.Instance?.Play(5);shake=0.08f;}
                    if(p.castSerial!=actor.cast)SynthAudio.Instance?.Play(p.castKind==1?3:1);
                    if(p.hp<actor.hp){shake=0.2f;SynthAudio.Instance?.Play(11);}
                    if(!app.WorldPaused && p.grounded && p.velocity.sqrMagnitude>4 && (footstep-=Time.unscaledDeltaTime)<=0)
                    {footstep=0.29f;SynthAudio.Instance?.Footstep();}
                }
                actor.jump=p.jumpSerial;actor.land=p.landSerial;actor.cast=p.castSerial;actor.hp=p.hp;
            }
            foreach(var e in state.enemies)
            {
                seen.Add(e.id);
                if(!actors.TryGetValue(e.id,out var actor))
                {var creature=e.kind==EnemyKind.Warden?BossArt.Create(v,dynamicRoot,Mathf.Max(1,e.bossStage)):CreatureMotion.Create(v,dynamicRoot,e.kind);actor=new Actor{root=creature.gameObject,creature=creature};actor.renderers=actor.root.GetComponentsInChildren<Renderer>();actor.colors=new Color[actor.renderers.Length];for(int r=0;r<actor.renderers.Length;r++){var mat=actor.renderers[r].sharedMaterial;actor.colors[r]=mat!=null&&mat.HasProperty("_Color")?mat.color:Color.white;}actors[e.id]=actor;actor.root.transform.position=e.position;var target=state.players.Find(p=>!p.downed&&!p.eliminated);if(target!=null){var facing=target.position-e.position;facing.y=0;if(facing.sqrMagnitude>.01f)actor.root.transform.rotation=Quaternion.LookRotation(facing);}}
                var direction=e.position-actor.root.transform.position;direction.y=0;
                if(e.eliteShrine>=0&&actor.root.transform.Find("Elite guardian halo")==null){actor.root.transform.localScale*=1.2f;v.Ring("Elite guardian halo",actor.root.transform,.9f,new Color(1,.25f,.12f),.065f).transform.localPosition=Vector3.up*.06f;}
                if(direction.sqrMagnitude>0.0004f)actor.root.transform.rotation=Quaternion.Slerp(actor.root.transform.rotation,Quaternion.LookRotation(direction),blend);
                actor.root.transform.position=Vector3.Lerp(actor.root.transform.position,e.position,blend);
                if(e.hitSerial!=actor.hitSerial){actor.creature.hit=1;actor.hitFlash=.18f;actor.creature.skeletal?.Hit();SynthAudio.Instance?.Play(2);}actor.hp=e.hp;actor.hitSerial=e.hitSerial;
                if(!app.WorldPaused)actor.hitFlash=Mathf.Max(0,actor.hitFlash-Time.unscaledDeltaTime);
                for(int r=0;r<actor.renderers.Length;r++){hitBlock.Clear();hitBlock.SetColor("_Color",Color.Lerp(actor.colors[r],Color.white,Mathf.Clamp01(actor.hitFlash/.18f)*.85f));actor.renderers[r].SetPropertyBlock(hitBlock);}
                if(actor.telegraph==null&&e.spawnGrace>0){actor.telegraph=v.Ring("Enemy arrival warning",dynamicRoot,.75f,new Color(.91f,.32f,.15f),.045f).transform;actor.telegraph.position=e.position+Vector3.up*.06f;}
                if(actor.telegraph!=null&&e.spawnGrace<=0){Destroy(actor.telegraph.gameObject);actor.telegraph=null;}
                if(!app.WorldPaused){if(actor.creature.skeletal!=null)actor.creature.skeletal.Enemy(direction.magnitude>.005f?e.speed:0,e.frozen>0||e.spawnGrace>0,Mathf.Max(e.windup,e.meleeWindup),e.attackTimer);else actor.creature.Pose(state.elapsed,e.frozen>0||e.spawnGrace>0,Mathf.Max(e.windup,e.meleeWindup));}
            }
            var dead=new List<int>();foreach(var pair in actors)if(!seen.Contains(pair.Key))dead.Add(pair.Key);
            foreach(int id in dead)
            {var a=actors[id];if(a.telegraph!=null)Destroy(a.telegraph.gameObject);if(a.creature?.skeletal!=null)a.creature.skeletal.Die();else Destroy(a.root);actors.Remove(id);}
            SyncAuras(state);SyncPortal(state);SyncHazards(state);SyncProjectiles(state,blend);SyncPickups(state,blend);SyncLoot(state);SyncEffects(state);SyncChests(state);
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
            var pivot=target+Vector3.up*(p.crouching?1.12f:1.65f);var rotation=Quaternion.Euler(app.CameraPitch,app.CameraYaw,0);
            var desired=pivot+rotation*new Vector3(0,0.25f,-8.7f);
            Vector3 ray=(desired-pivot).normalized;float distance=Vector3.Distance(pivot,desired),safe=distance;
            foreach(var prop in WorldMap.Props)
            {
                var center=prop.position+Vector3.up*(prop.kind==0?4.2f:0.7f)*prop.size;
                safe=CameraClearance(pivot,ray,safe,center,(prop.kind==0?2.3f:0.95f)*prop.size);
            }
            foreach(var enemy in app.State.enemies)
                safe=CameraClearance(pivot,ray,safe,enemy.position+Vector3.up*(enemy.kind==EnemyKind.Warden?2.8f:enemy.kind==EnemyKind.Brute?1.5f:0.9f),enemy.kind==EnemyKind.Warden?2.2f:enemy.kind==EnemyKind.Brute?1.15f:0.5f);
            desired=pivot+ray*Mathf.Clamp(safe,2.4f,distance);
            for(int i=1;i<=12;i++){var sample=Vector3.Lerp(pivot,desired,i/12f);float floor=WorldMap.Ground(sample)+0.65f;if(sample.y<floor)desired.y+=floor-sample.y;}
            Camera.transform.position=Vector3.Lerp(Camera.transform.position,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            Camera.transform.LookAt(pivot+rotation*Vector3.forward*1.2f);
            if(shake>0){shake=Mathf.Max(0,shake-Time.unscaledDeltaTime);Camera.transform.position+=Random.insideUnitSphere*shake*0.11f;}
        }
        static float CameraClearance(Vector3 pivot,Vector3 direction,float distance,Vector3 center,float radius)
        {
            var delta=center-pivot;float along=Vector3.Dot(delta,direction);
            if(along<1.8f||along>distance+radius)return distance;
            float side=delta.sqrMagnitude-along*along;if(side>=radius*radius)return distance;
            return Mathf.Min(distance,Mathf.Max(2.4f,along-Mathf.Sqrt(radius*radius-side)-0.35f));
        }
        void SyncProjectiles(WorldState state,float blend)
        {
            seen.Clear();foreach(var b in state.projectiles)
            {
                seen.Add(b.id);if(!bullets.TryGetValue(b.id,out var go))
                {go=WeaponVisuals.Arrow(v,dynamicRoot);go.transform.position=b.position;bullets[b.id]=go;}
                go.transform.position=Vector3.Lerp(go.transform.position,b.position,blend);if(b.direction.sqrMagnitude>0)go.transform.rotation=Quaternion.LookRotation(b.direction);
            }RemoveAbsent(bullets);
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
            }RemoveAbsent(drops);
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
                        var picture=new GameObject("Item picture");picture.transform.SetParent(go.transform,false);
                        var renderer=picture.AddComponent<SpriteRenderer>();renderer.sprite=IconLibrary.ItemSprite(drop.content);renderer.color=Color.white;
                    }
                    v.Ring("Reward halo",go.transform,.50f,GameData.Items[drop.content].color,.025f).transform.localPosition=Vector3.down*.38f;loot[drop.id]=go;
                }
                go.transform.position=drop.position;go.transform.localScale=Vector3.one*(drop.collecting?Mathf.Lerp(1,.2f,drop.collectTime/.35f):1);
                var pictureTransform=go.transform.Find("Item picture");if(pictureTransform!=null)pictureTransform.rotation=Camera.transform.rotation;
            }RemoveAbsent(loot);
        }
        void SyncAuras(WorldState state)
        {
            seen.Clear();foreach(var p in state.players)
            {
                if(p.hero!=Hero.Lumen||p.downed||p.eliminated)continue;
                for(int a=0;a<p.auraLevels.Length;a++)if(p.auraLevels[a]>0)
                {
                    int key=p.id*16+a;seen.Add(key);
                    if(!auraRings.TryGetValue(key,out var ring)){ring=v.Ring("Lumen / "+GameData.AuraName(a),dynamicRoot,1,GameData.AuraColors[a],.025f);auraRings[key]=ring;}
                    ring.transform.position=new Vector3(p.position.x,WorldMap.Ground(p.position)+.12f+a*.015f,p.position.z);
                    ring.transform.localScale=Vector3.one*(Simulation.AuraRadius(p)-a*.13f);
                }
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
                for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;WorldVfx.Crystal(v,root,"Portal rune",new Vector3(Mathf.Cos(a)*1.9f,1.9f+Mathf.Sin(a)*1.9f,0),.16f,color);}
            }
            portalVisual.SetActive(true);portalVisual.transform.position=state.portal.position;
        }
        void SyncHazards(WorldState state)
        {
            seen.Clear();foreach(var h in state.hazards)
            {
                seen.Add(h.id);if(!hazardRings.TryGetValue(h.id,out var ring))
                {
                    ring=v.Ring("Boss attack / "+h.style,dynamicRoot,h.radius,CampaignData.Color(h.style),.07f);ring.transform.position=h.position+Vector3.up*.09f;
                    var line=ring.GetComponent<LineRenderer>();for(int i=0;i<line.positionCount;i++){var p=line.GetPosition(i);p.y=WorldMap.Height(p.x+h.position.x,p.z+h.position.z)-h.position.y;line.SetPosition(i,p);}
                    hazardRings[h.id]=ring;
                }
                var lr=ring.GetComponent<LineRenderer>();lr.widthMultiplier=h.triggered?.20f:.06f+(.5f+.5f*Mathf.Sin(state.elapsed*12))*.025f;
                Color color=h.triggered?CampaignData.Color(h.style):Color.Lerp(new Color(.95f,.2f,.12f),CampaignData.Color(h.style),.35f);lr.startColor=lr.endColor=color;
            }RemoveAbsent(hazardRings);
        }
        void SyncEffects(WorldState state)
        {
            seen.Clear();foreach(var e in state.effects)
            {
                seen.Add(e.id);if(!effects.TryGetValue(e.id,out var go))
                {
                    Color c=e.kind>=9&&e.kind<=11?GameData.Weapons[(int)e.weapon].color:e.kind==12?new Color(.3f,.66f,1):e.kind==1||e.kind==3?GameData.Mint:e.kind==5?new Color(.91f,.43f,.44f):GameData.Amber;
                    if(e.kind>=9&&e.kind<=11)
                    {go=v.Group("Weapon strike",dynamicRoot).gameObject;var line=go.AddComponent<LineRenderer>();line.sharedMaterial=v.Material(c,true);line.useWorldSpace=true;line.numCapVertices=3;line.numCornerVertices=3;}
                    else{go=v.Ring("Spell ripple",dynamicRoot,1,c,.10f);go.transform.position=e.position+Vector3.up*.09f;}
                    effects[e.id]=go;if(e.kind==2)SynthAudio.Instance?.Play(6);if(e.kind==7)SynthAudio.Instance?.Play(8);
                }
                float age=Mathf.Clamp01(1-e.life/(e.kind>=9&&e.kind<=11?.4f:.55f));var lr=go.GetComponent<LineRenderer>();
                if(e.kind>=9&&e.kind<=11)
                {
                    Vector3 forward=(e.end-e.position).normalized,side=Vector3.Cross(Vector3.up,forward).normalized;
                    if(e.kind==9)
                    {lr.positionCount=17;for(int i=0;i<17;i++){float a=Mathf.Lerp(-1.4f,1.4f,i/16f)+Mathf.Lerp(-.35f,.35f,age);lr.SetPosition(i,e.position+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*e.radius*Mathf.Lerp(.5f,1,age));}}
                    else if(e.kind==10)
                    {lr.positionCount=12;for(int i=0;i<12;i++){float t=i/11f;lr.SetPosition(i,Vector3.Lerp(e.position,e.end,t)+side*((i==0||i==11)?0:Mathf.Sin(i*13.8f+e.id)*.38f));}}
                    else{lr.positionCount=2;lr.SetPosition(0,Vector3.Lerp(e.position,e.end,age*.7f));lr.SetPosition(1,Vector3.Lerp(e.position,e.end,Mathf.Min(1,age*3+.25f)));}
                    lr.widthMultiplier=Mathf.Max(.008f,(1-age)*(e.kind==9?.27f:e.kind==10?.12f:.22f));
                }
                else{go.transform.localScale=Vector3.one*Mathf.Lerp(.2f,e.radius,Mathf.Sqrt(age));lr.widthMultiplier=Mathf.Max(.005f,(1-age)*.14f);}
            }RemoveAbsent(effects);
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
                chest.openness=Mathf.MoveTowards(chest.openness,c.opened?1:0,GameApp.Instance.WorldPaused?0:Time.unscaledDeltaTime*2.3f);
                chest.lid.localRotation=Quaternion.Euler(-chest.openness*105,0,0);
                if(c.opened && !chest.announced){chest.announced=true;SynthAudio.Instance?.Play(8);}
            }
        }
        void RemoveAbsent(Dictionary<int,GameObject> dict)
        {var dead=new List<int>();foreach(int id in dict.Keys)if(!seen.Contains(id))dead.Add(id);foreach(int id in dead){Destroy(dict[id]);dict.Remove(id);}}
        public void ClearActors()
        {
            if(dynamicRoot!=null)foreach(Transform child in dynamicRoot)Destroy(child.gameObject);
            actors.Clear();bullets.Clear();drops.Clear();loot.Clear();effects.Clear();chests.Clear();shrines.Clear();auraRings.Clear();hazardRings.Clear();portalVisual=null;encounters=null;lastCoins=0;lastLevel=1;
        }
    }
}
