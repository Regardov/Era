using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class EncounterView
    {
        readonly VisualFactory factory;readonly Transform root;
        readonly Dictionary<int,GameObject> cards=new Dictionary<int,GameObject>();
        readonly Dictionary<int,LineRenderer> seals=new Dictionary<int,LineRenderer>();
        GameObject statue;
        static readonly Mesh[][] cardMeshes=new Mesh[2][];static readonly Material[][] cardMaterials=new Material[2][];
        public EncounterView(VisualFactory factory,Transform parent){this.factory=factory;root=factory.Group("Statues, elite trials and resource cards",parent);}
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
                factory.Ring("Summoning circle",group,2.1f,GameData.Amber,.055f).transform.localPosition=Vector3.up*.05f;
            }
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
                seal.sharedMaterial=factory.Material(shrine.completed?new Color(.32f,.91f,.58f):shrine.active?new Color(1,.16f,.1f):new Color(1,.62f,.2f),true);
            }
            foreach(var node in state.resourceNodes)
            {
                if(!cards.TryGetValue(node.id,out var card))
                {
                    int variant=node.kind==PickupKind.Gold?0:1;if(cardMeshes[variant]==null)BuildCard(variant);
                    var group=factory.Group(node.kind==PickupKind.Gold?"Golden mini graphics card":"Blue mini graphics card",root,node.position);card=group.gameObject;card.transform.localRotation=Quaternion.Euler(-8,node.id*47,-7);
                    ClickInteractable.Add(group,Vector3.up*.45f,new Vector3(1.3f,.9f,.5f),2.7f);
                    for(int i=0;i<cardMeshes[variant].Length;i++){var part=factory.Group("GPU casing / dual fans / power rails",group);part.gameObject.AddComponent<MeshFilter>().sharedMesh=cardMeshes[variant][i];part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=cardMaterials[variant][i];}cards[node.id]=card;
                }
                card.SetActive(!node.broken);
            }
        }
        static void BuildCard(int variant)
        {
            var v=new VisualFactory();var temp=v.Group("GPU mesh authoring",null);Color accent=variant==0?new Color(1,.68f,.12f):new Color(.12f,.65f,1),black=new Color(.055f,.075f,.095f),metal=new Color(.34f,.40f,.45f);
            v.Shape("Compact PCB",PrimitiveType.Cube,temp,new Vector3(0,.46f,0),new Vector3(1.2f,.68f,.16f),black);
            v.Shape("Power rail",PrimitiveType.Cube,temp,new Vector3(0,.83f,0),new Vector3(1.23f,.055f,.21f),accent,true);
            v.Shape("PCI connector",PrimitiveType.Cube,temp,new Vector3(0,.10f,0),new Vector3(.80f,.10f,.12f),accent);
            for(int side=-1;side<=1;side+=2)for(int fan=-1;fan<=1;fan+=2)
            {
                var outer=v.Shape("Fan housing",PrimitiveType.Cylinder,temp,new Vector3(fan*.30f,.48f,side*.10f),new Vector3(.52f,.025f,.52f),metal);outer.transform.localRotation=Quaternion.Euler(90,0,0);
                var inner=v.Shape("Fan recess",PrimitiveType.Cylinder,temp,new Vector3(fan*.30f,.48f,side*.13f),new Vector3(.43f,.028f,.43f),black);inner.transform.localRotation=Quaternion.Euler(90,0,0);
                for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;var blade=v.Shape("Fan blade",PrimitiveType.Cube,temp,new Vector3(fan*.30f+Mathf.Cos(a)*.125f,.48f+Mathf.Sin(a)*.125f,side*.167f),new Vector3(.12f,.065f,.023f),metal);blade.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+35);}
                var hub=v.Shape("Fan RGB hub",PrimitiveType.Cylinder,temp,new Vector3(fan*.30f,.48f,side*.18f),new Vector3(.10f,.017f,.10f),accent,true);hub.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            var groups=new Dictionary<Material,List<CombineInstance>>();foreach(var f in temp.GetComponentsInChildren<MeshFilter>()){var m=f.GetComponent<Renderer>().sharedMaterial;if(!groups.TryGetValue(m,out var list)){list=new List<CombineInstance>();groups[m]=list;}list.Add(new CombineInstance{mesh=f.sharedMesh,transform=temp.worldToLocalMatrix*f.transform.localToWorldMatrix});}
            var meshes=new List<Mesh>();var materials=new List<Material>();foreach(var pair in groups){var mesh=new Mesh{name="Instanced dual fan GPU"};mesh.CombineMeshes(pair.Value.ToArray());meshes.Add(mesh);materials.Add(pair.Key);}cardMeshes[variant]=meshes.ToArray();cardMaterials[variant]=materials.ToArray();Object.Destroy(temp.gameObject);
        }
    }
}
