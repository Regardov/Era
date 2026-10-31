using UnityEngine;
namespace Era
{
    public sealed class BatMotion:MonoBehaviour
    {
        public Transform[] joints;
        public string sourceHash;
        public int sourceTriangles;
        Vector3[] positions;Quaternion[] rotations;
        float cycle,moving,age,lastWindup=-1,lastRecovery=-1;int serial=-1;
        public float FlightBlend=>moving;
        public float WingAngle{get;private set;}
        public float DiveBlend{get;private set;}
        Transform Joint(string name){foreach(var t in joints)if(t.name==name)return t;return null;}
        void Bind(){if(positions!=null)return;positions=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];for(int i=0;i<joints.Length;i++){positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;}}
        public void Present(EnemyState enemy,float speed,float dt,float hit)
        {
            moving=ActorMotionMath.Damp(moving,Mathf.Clamp01(speed/EnemyCatalog.Speed(EnemyKind.Bat)),8,dt);
            cycle+=dt*Mathf.Lerp(14,19,moving);
            if(serial!=enemy.attackSerial||lastWindup!=enemy.meleeWindup||lastRecovery!=enemy.recovery)age=0;else age+=dt;
            serial=enemy.attackSerial;lastWindup=enemy.meleeWindup;lastRecovery=enemy.recovery;
            float wind=enemy.meleeWindup>0?1-Mathf.Max(.0001f,enemy.meleeWindup-age)/Mathf.Max(.01f,enemy.attackDuration):0;
            float dive=enemy.meleeWindup>0?ActorMotionMath.Ease(Mathf.InverseLerp(.28f,1,wind)):enemy.recovery>0?ActorMotionMath.Ease(Mathf.Clamp01((enemy.recovery-age)/.42f)):0;
            Pose(cycle,moving,dive,hit);
        }
        public void Pose(float phase,float flight,float dive,float hit=0)
        {
            Bind();DiveBlend=dive;for(int i=0;i<joints.Length;i++){joints[i].localPosition=positions[i];joints[i].localRotation=rotations[i];}
            var body=Joint("Body");body.localPosition+=Vector3.up*Mathf.Sin(phase)*.018f;body.localRotation*=Quaternion.Euler(flight*10+dive*36+hit*10,0,Mathf.Sin(phase*.37f)*2);
            Joint("Head").localRotation*=Quaternion.Euler(-flight*5-dive*16,Mathf.Sin(phase*.21f)*2,0);
            WingAngle=Mathf.Lerp(Mathf.Sin(phase)*(42+flight*9),25,dive*.80f);
            for(int i=0;i<2;i++)
            {
                string side=i==0?"L":"R";float sign=i==0?-1:1;
                Joint("Wing_"+side).localRotation*=Quaternion.Euler(25,sign*dive*28,sign*WingAngle);
                // Keep the closed membrane continuous across its wrist seam.
                Joint("Foot_"+side).localRotation*=Quaternion.Euler(18+flight*14-dive*22,0,sign*5);
            }
            Joint("Tail").localRotation*=Quaternion.Euler(Mathf.Sin(phase-.8f)*9+dive*12,0,0);
        }
    }
}
