using UnityEngine;

namespace Era
{
    public static class BossArt
    {
        public static CreatureMotion Create(VisualFactory v,Transform parent,int stage)
        {
            var prefab=Resources.Load<GameObject>("Bosses/Stage"+stage);
            if(prefab!=null)return Object.Instantiate(prefab,parent,false).GetComponent<CreatureMotion>();
            var actor=CreatureMotion.Create(v,parent,EnemyKind.Warden);Decorate(v,actor.gameObject,stage);return actor;
        }
        public static void Decorate(VisualFactory v,GameObject actor,int stage)
        {
            Color accent=CampaignData.Color(stage),gold=new Color(.70f,.55f,.27f),dark=new Color(.18f,.22f,.25f);
            var skeletal=actor.GetComponent<SkeletalActor>();var head=skeletal?.Bone("head")??actor.transform;var spine=skeletal?.spine??actor.transform;
            var mantle=v.Group("Boss identity / "+stage,spine);
            if(stage==1||stage==2)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var from=new Vector3(side*.21f,.35f,0);var end=new Vector3(side*.64f,1.1f,-.08f);
                    v.Branch(head,from,end,.085f,gold);v.Branch(head,Vector3.Lerp(from,end,.58f),end+new Vector3(side*.3f,-.08f,.17f),.05f,gold);
                    v.Branch(head,Vector3.Lerp(from,end,.42f),end+new Vector3(-side*.16f,.19f,-.15f),.045f,gold);
                    v.Rock("Moss pauldrons",mantle,new Vector3(side*.44f,.35f,0),new Vector3(.34f,.25f,.35f),accent*.68f,53+side);
                }
            }
            else if(stage==3)
            {
                for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;var shard=v.Cone("Glacial crown",head,new Vector3(Mathf.Cos(a)*.3f,.28f,Mathf.Sin(a)*.3f),.08f,.43f+(i%2)*.18f,accent,5);shard.transform.localRotation=Quaternion.Euler(Mathf.Sin(a)*20,0,-Mathf.Cos(a)*20);}
                for(int i=0;i<3;i++)WorldVfx.Crystal(v,mantle,"Ice satellite",new Vector3((i-1)*.65f,.6f,-.52f),.35f,accent);
            }
            else if(stage==4)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var armor=v.Shape("Forge armor",PrimitiveType.Cube,mantle,new Vector3(side*.48f,.3f,0),new Vector3(.47f,.39f,.5f),dark);armor.transform.localRotation=Quaternion.Euler(0,0,side*18);
                    v.Cone("Obsidian shoulder spike",mantle,new Vector3(side*.58f,.45f,0),.14f,.55f,dark,5);
                }
                v.Ellipsoid("Furnace heart",mantle,new Vector3(0,.18f,-.29f),new Vector3(.36f,.43f,.17f),accent,true);
            }
            else if(stage==5)
            {
                for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
                {
                    var start=new Vector3(side*.22f,.22f,-.06f);var elbow=new Vector3(side*(.65f+i*.07f),.45f-i*.18f,-.24f-i*.12f);var end=elbow+new Vector3(side*.2f,-.65f,-.22f);
                    v.Branch(mantle,start,elbow,.055f,dark);v.Branch(mantle,elbow,end,.036f,dark);
                }
                for(int i=0;i<5;i++)v.Cone("Thorn tiara",head,new Vector3((i-2)*.13f,.3f,0),.06f,.28f+(.2f-Mathf.Abs(i-2)*.07f),accent,5);
            }
            else if(stage==6)
            {
                for(int side=-1;side<=1;side+=2)
                {v.Rock("Stormsteel pauldrons",mantle,new Vector3(side*.43f,.33f,0),new Vector3(.38f,.28f,.38f),new Color(.62f,.70f,.79f),61+side);v.Cone("Storm fin",head,new Vector3(side*.25f,.25f,0),.06f,.65f,accent,5);}
                var hand=skeletal?.rightHand??mantle;var spear=WeaponVisuals.Create(v,hand,WeaponKind.Spear);spear.transform.localRotation=Quaternion.Euler(90,0,0);spear.transform.localScale=Vector3.one*.9f;
            }
            else
            {
                var seal=v.Ring("Rift sovereign seal",mantle,.88f,accent,.025f);seal.transform.localPosition=new Vector3(0,.64f,.35f);seal.transform.localRotation=Quaternion.Euler(90,0,0);
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;WorldVfx.Crystal(v,mantle,"Rift prism",new Vector3(Mathf.Cos(a)*.9f,.64f+Mathf.Sin(a)*.9f,.35f),.3f,accent);}
                for(int side=-1;side<=1;side+=2){v.Cone("Sovereign horn",head,new Vector3(side*.25f,.3f,0),.12f,.7f,gold,6);}
            }
        }
    }
}
