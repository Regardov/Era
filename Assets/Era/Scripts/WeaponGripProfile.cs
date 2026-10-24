using UnityEngine;
namespace Era
{
    [CreateAssetMenu(menuName="ERA/Weapon grip profile")]
    public sealed class WeaponGripProfile:ScriptableObject
    {
        public WeaponKind kind;public bool twoHanded,primaryLeft;
        public Vector3 primaryPoint,secondaryPoint,holdFromChest=new Vector3(.12f,-.16f,.18f),aimEuler;
        [Range(0,1)]public float fingerClosure=.2f;
    }
    public static class ArmPoseIK
    {
        public static void Solve(Transform upper,Transform elbow,Transform wrist,Vector3 target,Vector3 pole)
        {
            if(upper==null||elbow==null||wrist==null)return;
            Vector3 origin=upper.position,delta=target-origin;float a=Vector3.Distance(origin,elbow.position),b=Vector3.Distance(elbow.position,wrist.position);
            if(a<.001f||b<.001f||delta.sqrMagnitude<.000001f)return;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);Vector3 axis=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(pole-origin,axis).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(Vector3.down,axis).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);Vector3 joint=origin+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along)),goal=origin+axis*distance;
            upper.rotation=Quaternion.FromToRotation(elbow.position-origin,joint-origin)*upper.rotation;
            elbow.rotation=Quaternion.FromToRotation(wrist.position-elbow.position,goal-elbow.position)*elbow.rotation;
        }
    }
}
