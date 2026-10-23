using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public sealed class CreatureMotion : MonoBehaviour
    {
        public EnemyKind kind;
        public Transform body;
        public List<Transform> limbs=new List<Transform>();
        public Vector3 baseScale=Vector3.one;
        public Vector3 basePosition;
        public float hit;
        public SkeletalActor skeletal;
        public static CreatureMotion Create(VisualFactory v,Transform parent,EnemyKind kind)
        {
            if(Application.isPlaying)
            {var prefab=Resources.Load<GameObject>("Creatures/"+kind);if(prefab!=null)return Instantiate(prefab,parent,false).GetComponent<CreatureMotion>();}
            if(kind>=EnemyKind.Skeleton||kind==EnemyKind.Brute)return RoguelikeCreatureArt.Create(v,parent,kind);
            var root=v.Group(kind.ToString(),parent);var m=root.gameObject.AddComponent<CreatureMotion>();m.kind=kind;m.body=v.Group("Living body",root);
            Color teal=new Color(0.40f,0.64f,0.62f),purple=new Color(0.57f,0.44f,0.62f),cream=new Color(0.88f,0.85f,0.69f);
            if(kind==EnemyKind.Wisp)
            {
                v.Ellipsoid("Wisp bell",m.body,new Vector3(0,0.98f,0),new Vector3(0.95f,1.08f,0.90f),teal);
                v.Ellipsoid("Wisp cap",m.body,new Vector3(0,1.30f,0),new Vector3(1.08f,0.45f,1.02f),teal);
                for(int i=0;i<5;i++)
                {float a=i*Mathf.PI*2/5;var limb=v.Group("Trailing petal",m.body,new Vector3(Mathf.Cos(a)*0.28f,0.65f,Mathf.Sin(a)*0.28f));v.Bone("Petal",limb,new Vector3(0,-0.21f,0),new Vector3(0.17f,0.24f,0.17f),teal);m.limbs.Add(limb);}
                Eyes(v,m.body,0.99f,0.43f,0.15f);
            }
            else if(kind==EnemyKind.Stalker)
            {
                v.Ellipsoid("Fox body",m.body,new Vector3(0,0.68f,-0.12f),new Vector3(0.67f,0.70f,1.32f),purple);
                v.Ellipsoid("Fox head",m.body,new Vector3(0,1.0f,0.53f),new Vector3(0.76f,0.68f,0.76f),purple);
                v.Ellipsoid("Muzzle",m.body,new Vector3(0,0.90f,0.88f),new Vector3(0.42f,0.30f,0.49f),cream);
                for(int s=-1;s<=1;s+=2){v.Cone("Ear",m.body,new Vector3(s*0.25f,1.24f,0.5f),0.15f,0.42f,purple,9);}
                Eyes(v,m.body,1.05f,0.85f,0.18f);
                var tail=v.Bone("Plume tail",m.body,new Vector3(0,0.86f,-0.98f),new Vector3(0.35f,0.55f,0.35f),cream);tail.transform.localRotation=Quaternion.Euler(45,0,0);
                for(int i=0;i<4;i++){var leg=v.Group("Paw joint",m.body,new Vector3(i%2==0?-0.25f:0.25f,0.65f,i<2?0.35f:-0.55f));v.Bone("Leg",leg,new Vector3(0,-0.25f,0),new Vector3(0.17f,0.27f,0.17f),purple);v.Ellipsoid("Paw",leg,new Vector3(0,-0.50f,0.06f),new Vector3(0.23f,0.16f,0.32f),cream);m.limbs.Add(leg);}
            }
            else
            {
                bool boss=kind==EnemyKind.Warden;Color flesh=boss?new Color(0.48f,0.57f,0.43f):new Color(0.68f,0.64f,0.49f);
                v.Ellipsoid("Round torso",m.body,new Vector3(0,1.02f,0),new Vector3(1.26f,1.45f,0.95f),flesh);
                v.Ellipsoid("Head",m.body,new Vector3(0,1.67f,0.12f),new Vector3(1.0f,0.78f,0.85f),flesh);
                v.Ellipsoid("Mushroom cap",m.body,new Vector3(0,2.04f,0.03f),new Vector3(1.86f,0.67f,1.52f),boss?new Color(0.55f,0.44f,0.61f):new Color(0.76f,0.47f,0.38f));
                for(int i=0;i<5;i++)v.Ellipsoid("Cap spot",m.body,new Vector3(Mathf.Cos(i*2.4f)*0.51f,2.33f,Mathf.Sin(i*2.4f)*0.38f),new Vector3(0.19f,0.06f,0.16f),cream);
                Eyes(v,m.body,1.69f,0.53f,0.21f);
                for(int i=0;i<4;i++)
                {bool arm=i<2;var joint=v.Group(arm?"Arm joint":"Leg joint",m.body,new Vector3(i%2==0?-1:1,arm?1.48f:0.62f,0));joint.localPosition=new Vector3((i%2==0?-1:1)*(arm?0.67f:0.32f),arm?1.48f:0.62f,0);v.Bone("Organic limb",joint,new Vector3(0,-0.32f,0),new Vector3(arm?0.38f:0.39f,0.40f,0.38f),flesh);m.limbs.Add(joint);}
                if(boss)
                {
                    for(int s=-1;s<=1;s+=2)
                    {v.Branch(m.body,new Vector3(s*0.5f,2.1f,0),new Vector3(s*0.85f,3.0f,0),0.16f,cream);v.Branch(m.body,new Vector3(s*0.72f,2.6f,0),new Vector3(s*1.25f,2.9f,0.1f),0.11f,cream);}
                }
                m.body.localScale=Vector3.one*(boss?2.2f:1.25f);
            }
            m.baseScale=m.body.localScale;return m;
        }
        static void Eyes(VisualFactory v,Transform body,float y,float z,float spread)
        {
            for(int s=-1;s<=1;s+=2)
            {v.Ellipsoid("Eye socket",body,new Vector3(s*spread,y,z),new Vector3(0.16f,0.20f,0.06f),new Color(0.17f,0.23f,0.24f));v.Ellipsoid("Eye light",body,new Vector3(s*spread,y,z+0.035f),new Vector3(0.055f,0.085f,0.025f),new Color(0.98f,0.78f,0.56f),true);}
        }
        public void Pose(float time,bool frozen,float windup)
        {
            if(skeletal!=null)return;
            if(frozen){body.localRotation=Quaternion.Euler(0,0,4);return;}
            hit=Mathf.Max(0,hit-Time.unscaledDeltaTime*6);
            float phase=time*(kind==EnemyKind.Stalker?14:5)+GetInstanceID()%15;
            body.localPosition=Vector3.up*(kind==EnemyKind.Wisp?0.16f+Mathf.Sin(phase)*0.13f:Mathf.Abs(Mathf.Sin(phase))*0.055f);
            body.localRotation=Quaternion.Euler(windup>0?-18:-hit*12,0,Mathf.Sin(phase)*2);
            body.localScale=baseScale*(1+hit*.035f);
            for(int i=0;i<limbs.Count;i++)limbs[i].localRotation=Quaternion.Euler(Mathf.Sin(phase+i*Mathf.PI)* (kind==EnemyKind.Wisp?12:27),0,kind==EnemyKind.Wisp?Mathf.Cos(phase+i)*10:0);
        }
        public void CombatPose(EnemyState enemy,float time)
        {
            if(skeletal!=null)return;
            if(enemy.frozen>0||enemy.spawnGrace>0)return;
            hit=Mathf.Max(0,hit-Time.unscaledDeltaTime*6);
            float phase=time*(EnemyCatalog.Flying(kind)?13:kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime?5:6)+enemy.id;
            float charge=enemy.meleeWindup>0?1-enemy.meleeWindup/Mathf.Max(.01f,enemy.attackDuration):0;
            float strike=enemy.recovery>0?Mathf.Clamp01(enemy.recovery/.42f):0;
            bool gel=kind==EnemyKind.Slime||kind==EnemyKind.AcidSlime;
            body.localPosition=basePosition+Vector3.up*(gel?Mathf.Max(0,Mathf.Sin(phase))*.18f:Mathf.Abs(Mathf.Sin(phase))*.05f);
            body.localScale=Vector3.Scale(baseScale,gel?new Vector3(1-Mathf.Sin(phase)*.13f,1+Mathf.Sin(phase)*.2f,1-Mathf.Sin(phase)*.13f):new Vector3(1+hit*.03f,1-charge*.08f,1+hit*.03f));
            body.localRotation=Quaternion.Euler(-charge*22+strike*26-hit*12,enemy.attackStyle==0?charge*-25+strike*35:0,Mathf.Sin(phase)*2);
            foreach(var limb in limbs)
            {
                bool wing=limb.name.StartsWith("Wing"),arm=limb.name.StartsWith("Arm"),left=limb.name.EndsWith("-1");float side=left?-1:1;
                limb.localRotation=wing?Quaternion.Euler(0,0,side*(Mathf.Sin(phase)*35-charge*30)):arm?Quaternion.Euler(enemy.meleeWindup>0?-50-charge*85:enemy.recovery>0?strike*75:Mathf.Sin(phase+(left?0:Mathf.PI))*18,charge*side*12,side*charge*18):Quaternion.Euler(Mathf.Sin(phase+(left?Mathf.PI:0))*22,0,0);
            }
        }
    }
}
