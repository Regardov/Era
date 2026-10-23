using UnityEngine;

namespace Era
{
    // Original flat-shaded silhouettes, authored as joints so the attack windup can be read at range.
    public static class RoguelikeCreatureArt
    {
        static readonly Color Bone=new Color(.77f,.74f,.58f),Dark=new Color(.08f,.10f,.12f),Iron=new Color(.25f,.29f,.32f),Leather=new Color(.29f,.13f,.07f),Glow=new Color(.4f,.92f,.75f);
        public static CreatureMotion Create(VisualFactory v,Transform parent,EnemyKind kind)
        {
            var root=v.Group(EnemyCatalog.Name(kind),parent);var m=root.gameObject.AddComponent<CreatureMotion>();m.kind=kind;m.body=v.Group("Articulated body",root);
            if(kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime)Slime(v,m,kind==EnemyKind.AcidSlime);
            else if(kind==EnemyKind.StoneGolem||kind==EnemyKind.CrystalGolem)Golem(v,m,kind==EnemyKind.CrystalGolem);
            else if(kind==EnemyKind.Ogre||kind==EnemyKind.Brute||kind==EnemyKind.OgreShaman)Ogre(v,m,kind==EnemyKind.OgreShaman);
            else if(kind==EnemyKind.Bat)Bat(v,m);
            else if(kind==EnemyKind.Gargoyle){Golem(v,m,false);Wings(v,m,new Color(.23f,.26f,.29f),1.3f);m.body.localScale=Vector3.one*.72f;}
            else Undead(v,m,kind);
            m.baseScale=m.body.localScale;m.basePosition=m.body.localPosition;return m;
        }
        static Transform Joint(VisualFactory v,CreatureMotion m,string name,Vector3 position)
        {var t=v.Group(name,m.body,position);m.limbs.Add(t);return t;}
        static void Segment(VisualFactory v,Transform parent,string name,Vector3 a,Vector3 b,float width,Color color)
        {
            var t=v.Group(name,parent,(a+b)*.5f);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            v.Facet(name+" volume",t,Vector3.zero,new Vector3(width,(b-a).magnitude*.56f,width*.8f),color,12,6);
        }
        static void Eyes(VisualFactory v,Transform head,float y,float z,float spread,Color light)
        {for(int side=-1;side<=1;side+=2){v.Facet("Deep eye socket",head,new Vector3(side*spread,y,z),new Vector3(.09f,.075f,.04f),Dark,10,5);v.Facet("Eye ember",head,new Vector3(side*spread,y,z+.032f),new Vector3(.025f,.03f,.018f),light,8,4);}}
        static void Undead(VisualFactory v,CreatureMotion m,EnemyKind kind)
        {
            bool revenant=kind==EnemyKind.Revenant,mage=kind==EnemyKind.Necromancer,archer=kind==EnemyKind.SkeletonArcher;
            Color bone=revenant?new Color(.36f,.46f,.39f):Bone;
            v.Facet("Pelvis",m.body,new Vector3(0,.85f,0),new Vector3(.26f,.17f,.18f),bone);
            Segment(v,m.body,"Vertebral column",new Vector3(0,.9f,-.05f),new Vector3(0,1.48f,-.05f),.07f,bone);
            for(int row=0;row<5;row++)
            {
                float y=1.14f+row*.075f,w=.22f+Mathf.Sin(row*.7f)*.07f;
                for(int side=-1;side<=1;side+=2){Segment(v,m.body,"Rib back",new Vector3(0,y,-.06f),new Vector3(side*w,y+.02f,.04f),.025f,bone);Segment(v,m.body,"Rib front",new Vector3(side*w,y+.02f,.04f),new Vector3(side*.065f,y-.025f,.15f),.024f,bone);}
            }
            var head=v.Group("Skull joint",m.body,new Vector3(0,1.74f,0));v.Facet("Cranium",head,Vector3.zero,new Vector3(.21f,.235f,.19f),bone,16,9);v.Facet("Jaw",head,new Vector3(0,-.17f,.045f),new Vector3(.16f,.075f,.14f),bone);
            Eyes(v,head,.01f,.167f,.09f,mage?new Color(.8f,.32f,1):Glow);
            v.Cone("Nasal cavity",head,new Vector3(0,-.10f,.18f),.035f,.06f,Dark,3);
            for(int n=0;n<7;n++)v.Shape("Individual tooth",PrimitiveType.Cube,head,new Vector3((n-3)*.029f,-.13f,.173f),new Vector3(.018f,.035f,.025f),Bone);
            for(int side=-1;side<=1;side+=2)
            {
                var arm=Joint(v,m,"Arm / "+side,new Vector3(side*.35f,1.48f,0));Segment(v,arm,"Humerus",Vector3.zero,new Vector3(side*.06f,-.36f,.02f),.065f,bone);v.Facet("Elbow",arm,new Vector3(side*.06f,-.36f,.02f),Vector3.one*.07f,bone);
                for(int n=-1;n<=1;n+=2)Segment(v,arm,"Forearm bone",new Vector3(side*.06f+n*.025f,-.36f,.02f),new Vector3(side*.11f+n*.025f,-.67f,.08f),.028f,bone);
                for(int n=0;n<4;n++)Segment(v,arm,"Finger",new Vector3(side*.11f+(n-1.5f)*.025f,-.66f,.08f),new Vector3(side*.11f+(n-1.5f)*.03f,-.79f,.12f),.013f,bone);
                var leg=Joint(v,m,"Leg / "+side,new Vector3(side*.18f,.83f,0));Segment(v,leg,"Femur",Vector3.zero,new Vector3(0,-.37f,.04f),.075f,bone);v.Facet("Knee",leg,new Vector3(0,-.37f,.055f),Vector3.one*.07f,bone);Segment(v,leg,"Tibia",new Vector3(0,-.38f,.02f),new Vector3(0,-.72f,0),.055f,bone);v.Facet("Foot bones",leg,new Vector3(0,-.77f,.09f),new Vector3(.08f,.045f,.16f),bone);
                if(revenant){v.Facet("Shoulder plate",arm,new Vector3(0,-.02f,0),new Vector3(.21f,.14f,.22f),Iron);v.Facet("Knee plate",leg,new Vector3(0,-.35f,.10f),new Vector3(.12f,.12f,.06f),Iron);}
            }
            if(revenant){v.Facet("Armored breastplate",m.body,new Vector3(0,1.28f,.1f),new Vector3(.34f,.33f,.19f),Iron);v.Shape("Tattered tabard",PrimitiveType.Cube,m.body,new Vector3(0,.85f,.23f),new Vector3(.28f,.58f,.035f),Leather);WeaponVisuals.Create(v,m.limbs[2],WeaponKind.Sword).transform.localPosition=new Vector3(.1f,-.7f,.1f);}
            if(archer){var bow=WeaponVisuals.Create(v,m.limbs[0],WeaponKind.Bow);bow.transform.localPosition=new Vector3(-.08f,-.56f,.17f);bow.transform.localScale=Vector3.one*.7f;v.Facet("Leather quiver",m.body,new Vector3(.2f,1.21f,-.25f),new Vector3(.11f,.4f,.12f),Leather);}
            if(mage)
            {
                v.Cone("Layered necromancer robe",m.body,new Vector3(0,.12f,0),.53f,1.3f,new Color(.17f,.10f,.24f),16);
                v.Cone("Ragged cowl",head,new Vector3(0,.12f,-.04f),.3f,.4f,new Color(.17f,.10f,.24f),12);
                var staff=WeaponVisuals.Create(v,m.limbs[2],WeaponKind.LightningStaff);staff.transform.localPosition=new Vector3(.1f,-.68f,.1f);staff.transform.localScale=Vector3.one*.85f;
                for(int n=0;n<4;n++)v.Facet("Soul charm",m.body,new Vector3(Mathf.Cos(n*1.6f)*.28f,1.42f,Mathf.Sin(n*1.6f)*.2f),Vector3.one*.055f,Glow);
            }
        }
        static void Slime(VisualFactory v,CreatureMotion m,bool acid)
        {
            Color gel=acid?new Color(.58f,.69f,.13f):new Color(.17f,.56f,.47f);
            v.Facet("Faceted gel dome",m.body,new Vector3(0,.55f,0),new Vector3(.8f,.65f,.72f),gel,20,12);
            for(int n=0;n<7;n++){float a=n*Mathf.PI*2/7;v.Facet("Gel lobe",m.body,new Vector3(Mathf.Cos(a)*.53f,.2f,Mathf.Sin(a)*.5f),new Vector3(.32f,.22f,.29f),gel*.9f,12,6);}
            v.Facet("Suspended nucleus",m.body,new Vector3(0,.65f,.53f),new Vector3(.24f,.21f,.12f),acid?Leather:Glow,12,7);Eyes(v,m.body,.77f,.59f,.23f,Glow);
            for(int n=0;n<9;n++)v.Facet("Visible gel bubble",m.body,new Vector3(Mathf.Sin(n*2.4f)*.51f,.4f+(n%3)*.19f,Mathf.Cos(n*2.4f)*.49f),Vector3.one*(.045f+(n%2)*.035f),gel*1.2f,8,4);
            if(acid)for(int n=0;n<5;n++)v.Cone("Acid crystal",m.body,new Vector3(Mathf.Sin(n*2.4f)*.48f,.92f,Mathf.Cos(n*2.4f)*.35f),.09f,.32f,gel*.65f,5);
        }
        static void Golem(VisualFactory v,CreatureMotion m,bool crystal)
        {
            Color stone=crystal?new Color(.32f,.44f,.60f):new Color(.38f,.40f,.35f),core=crystal?new Color(.42f,.76f,1):Glow;
            v.Facet("Fractured chest",m.body,new Vector3(0,1.3f,0),new Vector3(.62f,.65f,.4f),stone,14,8);v.Facet("Hip boulder",m.body,new Vector3(0,.7f,0),new Vector3(.4f,.26f,.3f),stone*.8f);
            v.Facet("Runic head",m.body,new Vector3(0,2.05f,0),new Vector3(.34f,.4f,.3f),stone*1.15f);Eyes(v,m.body,2.13f,.26f,.15f,core);
            v.Facet("Exposed crystal heart",m.body,new Vector3(0,1.44f,.38f),new Vector3(.16f,.24f,.09f),core,8,5);
            for(int side=-1;side<=1;side+=2)
            {
                var arm=Joint(v,m,"Arm / "+side,new Vector3(side*.65f,1.65f,0));v.Facet("Shoulder boulder",arm,Vector3.zero,new Vector3(.35f,.32f,.36f),stone);
                v.Facet("Upper arm stone",arm,new Vector3(side*.1f,-.35f,0),new Vector3(.27f,.38f,.28f),stone*.9f);v.Facet("Huge fist",arm,new Vector3(side*.14f,-.8f,.03f),new Vector3(.34f,.38f,.32f),stone);
                for(int n=0;n<3;n++)v.Facet("Knuckle",arm,new Vector3(side*.14f+(n-1)*.14f,-.82f,.31f),new Vector3(.09f,.14f,.08f),stone*.75f);
                var leg=Joint(v,m,"Leg / "+side,new Vector3(side*.3f,.73f,0));v.Facet("Rock thigh",leg,new Vector3(0,-.18f,0),new Vector3(.23f,.27f,.24f),stone);v.Facet("Rock boot",leg,new Vector3(0,-.52f,.1f),new Vector3(.26f,.24f,.37f),stone*.85f);
                for(int n=0;n<3;n++)v.Cone("Shoulder shard",arm,new Vector3(side*.1f,.20f,n*.17f-.17f),.12f,.43f+(n%2)*.2f,crystal?core:stone,5);
            }
            for(int n=0;n<7;n++){var p=new Vector3(Mathf.Sin(n*2.4f)*.5f,.95f+(n%4)*.17f,.36f);v.Facet("Runic seam",m.body,p,new Vector3(.03f,.08f,.014f),core,6,3);}
        }
        static void Ogre(VisualFactory v,CreatureMotion m,bool shaman)
        {
            Color skin=shaman?new Color(.40f,.46f,.30f):new Color(.49f,.44f,.29f);
            v.Facet("Heavy belly",m.body,new Vector3(0,1.12f,0),new Vector3(.69f,.79f,.48f),skin,18,12);v.Facet("Chest",m.body,new Vector3(0,1.65f,-.03f),new Vector3(.72f,.37f,.41f),skin*.9f);
            var head=v.Group("Heavy head",m.body,new Vector3(0,2.06f,.07f));v.Facet("Angular face",head,Vector3.zero,new Vector3(.37f,.38f,.31f),skin,16,10);v.Facet("Heavy jaw",head,new Vector3(0,-.17f,.16f),new Vector3(.33f,.19f,.24f),skin*.85f);Eyes(v,head,.07f,.276f,.17f,new Color(1,.50f,.18f));
            for(int side=-1;side<=1;side+=2)
            {
                v.Cone("Ivory tusk",head,new Vector3(side*.21f,-.18f,.33f),.07f,.27f,Bone,7);v.Cone("Pointed ear",head,new Vector3(side*.34f,.02f,0),.12f,.29f,skin,7).transform.localRotation=Quaternion.Euler(0,0,-side*65);
                var arm=Joint(v,m,"Arm / "+side,new Vector3(side*.72f,1.72f,0));v.Facet("Upper muscle",arm,new Vector3(side*.12f,-.28f,0),new Vector3(.28f,.4f,.27f),skin);v.Facet("Forearm muscle",arm,new Vector3(side*.2f,-.68f,.06f),new Vector3(.26f,.34f,.25f),skin);v.Facet("Fist",arm,new Vector3(side*.23f,-.94f,.1f),new Vector3(.25f,.24f,.23f),skin*.9f);
                for(int n=0;n<4;n++)v.Facet("Finger knuckle",arm,new Vector3(side*.23f+(n-1.5f)*.09f,-1.02f,.28f),new Vector3(.065f,.1f,.06f),skin*.75f,8,5);
                var leg=Joint(v,m,"Leg / "+side,new Vector3(side*.32f,.72f,0));v.Facet("Thick thigh",leg,new Vector3(0,-.16f,0),new Vector3(.26f,.3f,.28f),skin);v.Facet("Shin",leg,new Vector3(0,-.43f,0),new Vector3(.20f,.27f,.2f),skin);v.Facet("Leather boot",leg,new Vector3(0,-.62f,.15f),new Vector3(.25f,.17f,.36f),Leather);
            }
            v.Facet("Hide belt",m.body,new Vector3(0,.89f,.025f),new Vector3(.69f,.16f,.49f),Leather);v.Shape("Belt buckle",PrimitiveType.Cube,m.body,new Vector3(0,.9f,.52f),new Vector3(.23f,.19f,.045f),Iron);
            for(int n=0;n<7;n++){float a=n*Mathf.PI*2/7;v.Facet("Hide skirt panel",m.body,new Vector3(Mathf.Cos(a)*.46f,.64f,Mathf.Sin(a)*.35f),new Vector3(.21f,.25f,.12f),Leather*(n%2==0?1:.8f),8,4);}
            if(shaman){var staff=WeaponVisuals.Create(v,m.limbs[2],WeaponKind.LightningStaff);staff.transform.localPosition=new Vector3(.2f,-1,.1f);v.Cone("Bone ritual crown",head,new Vector3(0,.25f,0),.39f,.45f,Bone,9);}
            else{var club=v.Group("Crusher club",m.limbs[2],new Vector3(.23f,-.94f,.16f));Segment(v,club,"Club haft",Vector3.zero,Vector3.up*1.1f,.08f,Leather);v.Facet("Stone club head",club,Vector3.up*1.12f,new Vector3(.35f,.45f,.28f),Iron,12,7);}
        }
        static void Wings(VisualFactory v,CreatureMotion m,Color color,float scale)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var wing=Joint(v,m,"Wing / "+side,new Vector3(side*.22f,1.4f,-.1f));
                var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
                Vector3 center=new Vector3(side*.65f,-.05f,-.12f)*scale;
                for(int n=0;n<12;n++)
                {
                    float t=n/11f;Vector3 a=new Vector3(side*(.2f+t*1.5f),Mathf.Sin(t*Mathf.PI)*.5f,-t*.35f)*scale,b=new Vector3(side*(.2f+t*1.5f),-.65f+Mathf.Sin(t*Mathf.PI*3)*.12f,-t*.45f)*scale;
                    Segment(v,wing,"Wing finger",center,a,.022f,color*.75f);
                    if(n<11){int at=vertices.Count;float next=(n+1)/11f;var c=new Vector3(side*(.2f+next*1.5f),-.65f+Mathf.Sin(next*Mathf.PI*3)*.12f,-next*.45f)*scale;vertices.AddRange(new[]{center,b,c,c,b,center});triangles.AddRange(new[]{at,at+1,at+2,at+3,at+4,at+5});}
                }
                var mesh=new Mesh{name="Faceted wing membrane"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();var go=new GameObject("Wing membrane");go.transform.SetParent(wing,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=v.Material(color);
            }
        }
        static void Bat(VisualFactory v,CreatureMotion m)
        {
            Color fur=new Color(.25f,.16f,.22f);v.Facet("Bat torso",m.body,new Vector3(0,1.3f,0),new Vector3(.25f,.38f,.22f),fur);v.Facet("Bat head",m.body,new Vector3(0,1.68f,.04f),new Vector3(.26f,.23f,.22f),fur);Eyes(v,m.body,1.73f,.23f,.11f,new Color(1,.21f,.12f));
            for(int side=-1;side<=1;side+=2){v.Cone("Long bat ear",m.body,new Vector3(side*.16f,1.81f,0),.11f,.4f,fur,8);v.Cone("Bat fang",m.body,new Vector3(side*.07f,1.51f,.23f),.028f,.11f,Bone,5).transform.localRotation=Quaternion.Euler(180,0,0);}
            Wings(v,m,new Color(.30f,.11f,.18f),.85f);m.body.localPosition=Vector3.down*1.2f;
        }
    }
}
