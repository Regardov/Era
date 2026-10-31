using UnityEngine;
namespace Era
{
    // Rigid articulated stones: no rubber deformation and no changes to authoritative damage timing.
    public sealed class GolemMotion:MonoBehaviour
    {
        public Transform[] joints;
        public string sourceHash;
        public int sourceTriangles;
        Vector3[] positions;Quaternion[] rotations;
        static readonly string[] Sides={"L","R"};
        float cycle,blend,lastRemaining=-1,lastRecovery=-1,sampleAge;int lastSerial=-1;
        public float AttackPhase{get;private set;}
        Transform Joint(string name){foreach(var t in joints)if(t.name==name)return t;return null;}
        void Bind()
        {
            if(positions!=null)return;positions=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];
            for(int i=0;i<joints.Length;i++){positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;}
        }
        static float E(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        public void Present(EnemyState enemy,float speed,float dt,float hit)
        {
            blend=ActorMotionMath.Damp(blend,enemy.meleeWindup>0||enemy.recovery>0?0:Mathf.Clamp01(speed/EnemyCatalog.Speed(enemy.kind)),8,dt);
            cycle+=dt*Mathf.Lerp(.4f,3.2f,blend);
            // Advance between authoritative samples at render FPS; do not hold each 30 Hz pose.
            if(lastSerial!=enemy.attackSerial||lastRemaining!=enemy.meleeWindup||lastRecovery!=enemy.recovery)sampleAge=0;else sampleAge+=dt;
            lastSerial=enemy.attackSerial;lastRemaining=enemy.meleeWindup;lastRecovery=enemy.recovery;
            float remaining=Mathf.Max(.0001f,enemy.meleeWindup-sampleAge),recovery=Mathf.Max(0,enemy.recovery-sampleAge);
            float phase=enemy.meleeWindup>0?1-remaining/Mathf.Max(.01f,enemy.attackDuration):enemy.recovery>0?1+(1-recovery/.8f):-1;
            Pose(phase,cycle,blend,hit);
        }
        public void Pose(float phase,float gait,float moving,float hit=0)
        {
            Bind();AttackPhase=phase;
            for(int i=0;i<joints.Length;i++){joints[i].localPosition=positions[i];joints[i].localRotation=rotations[i];}
            float attack=phase>=0?1:0,progress=Mathf.Clamp01(phase),recovery=phase>1?1-E(Mathf.InverseLerp(1.10f,2,phase)):1;
            float lift=E(progress/.65f),stroke=E(Mathf.InverseLerp(.67f,1,progress));
            float bend=Mathf.Lerp(-12*lift,58,stroke)*recovery*attack;
            float drop=Mathf.Lerp(.035f*lift,.30f,stroke)*recovery*attack;
            var hips=Joint("Hips");var torso=Joint("Torso");var head=Joint("Head");
            hips.localPosition+=new Vector3(Mathf.Sin(gait)*.018f*moving,-drop+Mathf.Abs(Mathf.Sin(gait))*.018f*moving,0);
            torso.localRotation*=Quaternion.Euler(bend+hit*5,Mathf.Sin(gait)*2.5f*moving,Mathf.Sin(gait)*1.3f*moving);
            head.localRotation*=Quaternion.Euler(-bend*.45f,0,0);
            foreach(string side in Sides)
            {
                float sign=side=="L"?-1:1,step=Mathf.Sin(gait+(side=="L"?Mathf.PI:0));
                var thigh=Joint("Thigh_"+side);var shin=Joint("Shin_"+side);var foot=Joint("Foot_"+side);
                Vector3 footTarget=new Vector3(sign*.35f,.27f+Mathf.Max(0,step)*.075f*moving,-.025f+step*.17f*moving);
                Solve(thigh,shin,foot,transform.TransformPoint(footTarget),transform.TransformPoint(new Vector3(sign*.4f,.68f,.65f)));
                foot.rotation=transform.rotation;
                var arm=Joint("UpperArm_"+side);var elbow=Joint("Forearm_"+side);var hand=Joint("Hand_"+side);
                Vector3 rest=new Vector3(sign*.86f,1.04f,.105f-step*.12f*moving);
                Vector3 raised=new Vector3(sign*.50f,2.85f,.08f);
                Vector3 chamber=Vector3.Lerp(rest,new Vector3(sign*.64f,1.60f,.62f),E(progress/.25f));
                Vector3 target=Vector3.Lerp(chamber,raised,E(Mathf.InverseLerp(.22f,.65f,progress)));
                target=Vector3.Lerp(target,new Vector3(sign*.62f,.34f,1.16f),stroke);
                target=Vector3.Lerp(rest,target,attack*recovery);
                Solve(arm,elbow,hand,transform.TransformPoint(target),transform.TransformPoint(new Vector3(sign*1.4f,1.65f,.12f)));
                hand.rotation=transform.rotation*Quaternion.Euler(stroke*recovery*attack*65,0,-sign*lift*recovery*attack*7);
            }
        }
        static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            var a=upper.position;var b=lower.position;var c=end.position;float ab=Vector3.Distance(a,b),bc=Vector3.Distance(b,c);
            var d=target-a;float distance=Mathf.Clamp(d.magnitude,Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);var forward=d.sqrMagnitude>.00001f?d.normalized:Vector3.down;
            var bend=Vector3.ProjectOnPlane(pole-a,forward).normalized;if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(Vector3.forward,forward).normalized;
            float along=(ab*ab+distance*distance-bc*bc)/(2*distance);
            var knee=a+forward*along+bend*Mathf.Sqrt(Mathf.Max(0,ab*ab-along*along));
            upper.rotation=Quaternion.FromToRotation(b-a,knee-a)*upper.rotation;
            var wanted=a+forward*distance;lower.rotation=Quaternion.FromToRotation(end.position-lower.position,wanted-lower.position)*lower.rotation;
        }
    }
}
