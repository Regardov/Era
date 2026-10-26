using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class EncounterView
    {
        readonly VisualFactory factory;readonly Transform root;
        readonly Dictionary<int,GameObject> leafPiles=new Dictionary<int,GameObject>();
        readonly Dictionary<int,LineRenderer> seals=new Dictionary<int,LineRenderer>();
        GameObject statue;
        LineRenderer statueSeal;float statueCharge;bool summoned;
        readonly Dictionary<int,int> nodeHits=new Dictionary<int,int>();
        readonly Dictionary<int,float> nodeRecoil=new Dictionary<int,float>(),nodeDeath=new Dictionary<int,float>();
        readonly Dictionary<int,Transform> nodeBodies=new Dictionary<int,Transform>();
        readonly HashSet<int> trialActive=new HashSet<int>(),trialComplete=new HashSet<int>();
        static readonly Mesh[] leafMeshes=new Mesh[3];static readonly Material[] leafMaterials=new Material[3];
        public EncounterView(VisualFactory factory,Transform parent){this.factory=factory;root=factory.Group("Statues, elite trials and autumn leaves",parent);}
        public void Present(WorldState state)
        {
            if(statue==null)
            {
                var group=factory.Group("Stone boss statue",root,state.bossStatuePosition);statue=group.gameObject;
                ClickInteractable.Add(group,Vector3.up*2,new Vector3(3.6f,4.5f,3.6f),4);
                factory.Shape("Octagonal plinth",PrimitiveType.Cylinder,group,Vector3.up*.26f,new Vector3(3.5f,.26f,3.5f),new Color(.37f,.40f,.40f));
                factory.Shape("Carved pedestal",PrimitiveType.Cube,group,Vector3.up*.78f,new Vector3(1.9f,.70f,1.9f),new Color(.48f,.50f,.47f));
                var sculpture=BossArt.Create(factory,group,state.stage);sculpture.transform.localPosition=Vector3.up*1.15f;sculpture.transform.localScale=Vector3.one*1.25f;
                var skeleton=sculpture.GetComponent<SkeletalActor>();if(skeleton!=null){skeleton.animationPlayer.Play("Idle");skeleton.animationPlayer.Sample();skeleton.animationPlayer.Stop();skeleton.animationPlayer.enabled=false;skeleton.enabled=false;}sculpture.enabled=false;
                var stone=factory.Material(new Color(.49f,.53f,.52f));stone.mainTexture=Resources.Load<Texture2D>("Textures/StoneDetail");
                foreach(var renderer in sculpture.GetComponentsInChildren<Renderer>()){var slots=new Material[renderer.sharedMaterials.Length];for(int i=0;i<slots.Length;i++)slots[i]=stone;renderer.sharedMaterials=slots;}
                statueSeal=factory.Ring("Summoning circle",group,2.1f,GameData.Amber,.055f).GetComponent<LineRenderer>();statueSeal.transform.localPosition=Vector3.up*.05f;summoned=state.bossSpawned;
            }
            if(state.bossSpawned&&!summoned){summoned=true;PresentationFx.Emit(state.bossStatuePosition,CampaignData.Color(state.stage),4,FeedbackKind.Summon);}
            statueCharge=PresentationMotion.Damp(statueCharge,state.bossSpawned?1:0,4,PresentationMotion.Delta);
            statueSeal.widthMultiplier=Mathf.Lerp(.055f,.025f,statueCharge);statueSeal.startColor=statueSeal.endColor=Color.Lerp(GameData.Amber,new Color(.28f,.34f,.34f),statueCharge);
            foreach(var shrine in state.eliteShrines)
            {
                if(!seals.TryGetValue(shrine.id,out var seal))
                {
                    var group=factory.Group("Elite boss shrine / five guardians",root,shrine.position);
                    ClickInteractable.Add(group,Vector3.up*.8f,new Vector3(4,2,4),3.8f);
                    factory.Shape("Trial altar",PrimitiveType.Cylinder,group,Vector3.up*.15f,new Vector3(4,.2f,4),new Color(.30f,.32f,.37f));
                    for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var pillar=factory.Shape("Guardian stone",PrimitiveType.Cube,group,new Vector3(Mathf.Cos(a)*1.35f,.8f,Mathf.Sin(a)*1.35f),new Vector3(.48f,1.35f,.48f),new Color(.41f,.43f,.49f));pillar.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);WorldVfx.Crystal(factory,pillar.transform,"Guardian ember",Vector3.up*.65f,.25f,new Color(1,.40f,.20f));}
                    seal=factory.Ring("Elite trial seal",group,2.15f,new Color(1,.42f,.16f),.07f).GetComponent<LineRenderer>();seal.transform.localPosition=Vector3.up*.05f;seals[shrine.id]=seal;
                }
                Color target=shrine.completed?new Color(.32f,.91f,.58f):shrine.active?new Color(1,.16f,.1f):new Color(1,.62f,.2f);
                seal.startColor=seal.endColor=Color.Lerp(seal.startColor,target,1-Mathf.Exp(-6*PresentationMotion.Delta));seal.widthMultiplier=.055f+(shrine.active&&!shrine.completed?(.5f+.5f*Mathf.Sin(state.elapsed*3))*.035f:0);
                if(shrine.active&&trialActive.Add(shrine.id))PresentationFx.Emit(shrine.position,target,2,FeedbackKind.Summon);
                if(shrine.completed&&trialComplete.Add(shrine.id))PresentationFx.Emit(shrine.position,target,2,FeedbackKind.Heal);
            }
            foreach(var node in state.resourceNodes)
            {
                if(!leafPiles.TryGetValue(node.id,out var pile))
                {
                    // Appearance depends only on visual identity, never the hidden reward.
                    int variant=node.id%3;if(leafMeshes[variant]==null)BuildLeaves(variant);
                    var group=factory.Group("Autumn leaf pile",root,node.position);pile=group.gameObject;pile.transform.localRotation=Quaternion.Euler(0,node.id*47,0);
                    ClickInteractable.Add(group,Vector3.up*.25f,new Vector3(1.8f,.65f,1.6f),2.7f);
                    var body=factory.Group("Resource visual / independent of click volume",group);nodeBodies[node.id]=body;
                    var part=factory.Group("Folded maple and birch leaves",body);part.gameObject.AddComponent<MeshFilter>().sharedMesh=leafMeshes[variant];part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=leafMaterials[variant];leafPiles[node.id]=pile;
                }
                if(!nodeHits.ContainsKey(node.id)){nodeHits[node.id]=node.hitSerial;nodeRecoil[node.id]=0;nodeDeath[node.id]=node.broken?1:0;}
                if(node.hitSerial!=nodeHits[node.id]){nodeHits[node.id]=node.hitSerial;nodeRecoil[node.id]=1;PresentationFx.Emit(node.position+Vector3.up*.25f,new Color(.82f,.43f,.16f),.7f,node.broken?FeedbackKind.Death:FeedbackKind.Impact);}
                nodeRecoil[node.id]=Mathf.Max(0,nodeRecoil[node.id]-PresentationMotion.Delta*4);
                float recoil=nodeRecoil[node.id];var visual=nodeBodies[node.id];visual.localRotation=Quaternion.Euler(Mathf.Sin(recoil*Mathf.PI*2)*recoil*7,0,0);
                if(node.broken)nodeDeath[node.id]=Mathf.Min(1,nodeDeath[node.id]+PresentationMotion.Delta*3);
                float collapse=PresentationMotion.Ease(nodeDeath[node.id]);visual.localScale=new Vector3(1+collapse*.5f,1-collapse,1+collapse*.5f);visual.localPosition=Vector3.down*collapse*.1f;pile.GetComponent<BoxCollider>().enabled=!node.broken;pile.SetActive(collapse<1);
            }
        }
        static void BuildLeaves(int variant)
        {
            var authored=SceneryBuilder.LeafPile(null,variant);leafMeshes[variant]=authored.GetComponent<MeshFilter>().sharedMesh;leafMaterials[variant]=authored.GetComponent<Renderer>().sharedMaterial;Object.Destroy(authored);
        }
    }
}
