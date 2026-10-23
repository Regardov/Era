using UnityEngine;
namespace Era
{
    public static class WeaponVisuals
    {
        public static GameObject Create(VisualFactory v,Transform parent,WeaponKind kind)
        {
            var root=v.Group(kind.ToString(),parent);Color wood=new Color(.34f,.25f,.21f),metal=new Color(.79f,.84f,.80f),gold=new Color(.87f,.68f,.36f);
            if(kind==WeaponKind.Aura)
            {
                WorldVfx.Crystal(v,root,"Aura focus",new Vector3(0,.35f,0),.43f,GameData.HeroColor(Hero.Lumen));
                v.Ring("Conduit orbit",root,.36f,gold,.035f).transform.localPosition=Vector3.up*.35f;
            }
            else if(kind==WeaponKind.Bow)
            {
                var points=new Vector3[17];for(int i=0;i<points.Length;i++){float a=Mathf.Lerp(-Mathf.PI/2,Mathf.PI/2,i/16f);points[i]=new Vector3(Mathf.Cos(a)*.36f,Mathf.Sin(a)*.72f+.48f,0);if(i>0)v.Branch(root,points[i-1],points[i],.035f,wood);}
                v.Branch(root,points[0],points[16],.008f,metal);v.Branch(root,new Vector3(.36f,.31f,0),new Vector3(.36f,.65f,0),.05f,gold);
                v.Branch(root,new Vector3(-.15f,.48f,0),new Vector3(.75f,.48f,0),.014f,metal);
            }
            else
            {
                float length=kind==WeaponKind.Spear?2.25f:kind==WeaponKind.LightningStaff?1.65f:1.12f;
                v.Shape("Wooden haft",PrimitiveType.Cylinder,root,new Vector3(0,length/2-.35f,0),new Vector3(.065f,length/2,.065f),wood);
                for(int i=0;i<4;i++)v.Shape("Grip wrap",PrimitiveType.Cylinder,root,new Vector3(0,-.05f+i*.06f,0),new Vector3(.08f,.014f,.08f),gold);
                if(kind==WeaponKind.Axe)
                {
                    var blade=v.Cone("Crescent blade",root,new Vector3(.20f,.58f,0),.38f,.10f,metal,12);blade.transform.localRotation=Quaternion.Euler(90,0,0);blade.transform.localScale=new Vector3(1,1,.95f);
                    v.Ellipsoid("Blade edge",root,new Vector3(.39f,.58f,0),new Vector3(.17f,.65f,.075f),metal);
                    v.Ellipsoid("Rivet",root,new Vector3(.02f,.69f,0),Vector3.one*.14f,gold);
                }
                else if(kind==WeaponKind.Sword)
                {
                    v.Shape("Crossguard",PrimitiveType.Cube,root,new Vector3(0,.2f,0),new Vector3(.58f,.075f,.12f),gold);
                    v.Shape("Forged blade",PrimitiveType.Cube,root,new Vector3(0,.7f,0),new Vector3(.14f,.95f,.055f),metal);
                    v.Cone("Sword point",root,new Vector3(0,1.17f,0),.1f,.24f,metal,4);
                    v.Shape("Fuller",PrimitiveType.Cube,root,new Vector3(0,.72f,.031f),new Vector3(.026f,.8f,.01f),gold);
                }
                else if(kind==WeaponKind.Spear)
                {v.Cone("Leaf spearhead",root,new Vector3(0,1.88f,0),.16f,.59f,metal,6);v.Ellipsoid("Spear collar",root,new Vector3(0,1.88f,0),new Vector3(.13f,.20f,.13f),gold);}
                else
                {
                    v.Branch(root,new Vector3(0,1.15f,0),new Vector3(-.23f,1.55f,0),.045f,gold);v.Branch(root,new Vector3(0,1.15f,0),new Vector3(.23f,1.55f,0),.045f,gold);
                    v.Cone("Storm crystal",root,new Vector3(0,1.28f,0),.16f,.39f,new Color(.68f,.61f,.9f),6);v.Ellipsoid("Charged heart",root,new Vector3(0,1.44f,0),Vector3.one*.15f,GameData.Mint,true);
                }
            }
            return root.gameObject;
        }
        public static GameObject Arrow(VisualFactory v,Transform parent)
        {
            var root=v.Group("Wildwood arrow",parent);v.Branch(root,new Vector3(0,0,-.45f),new Vector3(0,0,.35f),.022f,new Color(.51f,.35f,.23f));
            var tip=v.Cone("Arrowhead",root,new Vector3(0,0,.32f),.075f,.23f,new Color(.85f,.9f,.85f),4);tip.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<3;i++){var feather=v.Ellipsoid("Feather",root,new Vector3(0,0,-.35f),new Vector3(.17f,.025f,.24f),GameData.Mint);feather.transform.localRotation=Quaternion.Euler(0,0,i*120);}
            v.Ellipsoid("Bright arrow core",root,new Vector3(0,0,.2f),Vector3.one*.17f,new Color(1,.90f,.35f),true);
            var trail=root.gameObject.AddComponent<TrailRenderer>();trail.time=.25f;trail.minVertexDistance=.06f;trail.startWidth=.16f;trail.endWidth=.018f;trail.startColor=new Color(1,.78f,.14f,.95f);trail.endColor=new Color(1,.32f,.05f,0);trail.sharedMaterial=new Material(Shader.Find("Sprites/Default"));trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
            return root.gameObject;
        }
    }
}
