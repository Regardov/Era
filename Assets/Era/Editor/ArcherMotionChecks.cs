using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Era.Editor
{
    public static class ArcherMotionChecks
    {
        [Serializable] public sealed class Range { public string bone; public float run,runHold; }
        [Serializable] public sealed class AuditReport { public Range[] rotations; public string[] upperMask; }
        [Serializable] public sealed class MotionReport { public bool ok;public int assertions;public float maxStanceResidualMeters,springFrameRateError; }
        static int assertions;
        static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Archer motion check: "+message);}
        [MenuItem("ERA/Archer/Validate motion system")]
        public static void Run()
        {
            assertions=0;var report=RunPrefab(Resources.Load<GameObject>("Characters/Ember"));
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/archer-motion-checks.json",JsonUtility.ToJson(report,true));
            Debug.Log("ERA_ARCHER_MOTION_OK "+JsonUtility.ToJson(report));
            Audit();
        }
        public static MotionReport RunPrefab(GameObject source)
        {
            int before=assertions;var report=new MotionReport();
            var root=Object.Instantiate(source);
            try
            {
                var a=root.GetComponent<HeroMotion>().archer;
                a.Clip("Archer_Run");
                Check(a.Motion!=null,"post-Animator composer exists");
                var controller=(UnityEditor.Animations.AnimatorController)a.animator.runtimeAnimatorController;
                var tree=controller.layers[0].stateMachine.defaultState.motion as UnityEditor.Animations.BlendTree;
                Check(tree!=null&&tree.blendType==UnityEditor.Animations.BlendTreeType.FreeformDirectional2D&&tree.children.Length==9,"nine-motion 2D directional locomotion");
                foreach(string gait in new[]{"Walk","Run"})foreach(string side in new[]{"","Left","Right","Back"})
                {
                    string name="Archer_"+gait+side;var clip=a.Clip(name);Check(clip!=null&&clip.isLooping,"loop exists: "+name);
                    a.SampleClip(name,0);var first=a.leftFoot.position;var rotation=a.leftFoot.localRotation;
                    a.SampleClip(name,clip.length);Check(Vector3.Distance(first,a.leftFoot.position)<.003f&&Quaternion.Angle(rotation,a.leftFoot.localRotation)<.1f,"loop closes: "+name);
                    a.SampleClip(name,.01f);var start=a.leftFoot.position;
                    a.SampleClip(name,.10f);var delta=Vector3.ProjectOnPlane(a.leftFoot.position-start,Vector3.up);
                    Vector3 direction=side=="Left"?Vector3.left:side=="Right"?Vector3.right:side=="Back"?Vector3.back:Vector3.forward;
                    Check(Vector3.Dot(delta,direction)<-.025f,"stance travels opposite actual movement: "+name);
                    float speed=(gait=="Walk"?a.walkSpeed:a.runSpeed)*(side=="Back"?(gait=="Walk"?a.walkBackScale:a.runBackScale):side==""?1:(gait=="Walk"?a.walkSideScale:a.runSideScale));
                    float residual=(delta+direction*speed*.09f).magnitude;
                    report.maxStanceResidualMeters=Mathf.Max(report.maxStanceResidualMeters,residual);
                    Check(residual<.065f,"stance support matches authored stride speed within heel/toe roll allowance: "+name+" residual="+residual);
                }
                var samples=new Vector3[3];int index=0;
                foreach(int fps in new[]{15,30,60})
                {
                    var spring=new ArcherMotionRig.Spring();float dt=1f/fps;
                    for(int i=0;i<fps*2;i++)
                    {
                        var target=i<fps*2/5?new Vector3(6,2,-4):Vector3.zero;
                        var value=spring.Step(target,2.5f,dt,12);
                        Check(ActorMotionMath.Finite(value)&&value.magnitude<=12.001f,"bounded spring at "+fps+" FPS");
                        if(i==fps/3)samples[index]=value;
                    }
                    Check(spring.value.magnitude<.001f&&spring.velocity.magnitude<.01f,"spring settles after stop: "+fps);
                    index++;
                }
                // Constant target at identical elapsed times is analytically invariant.
                Vector3? reference=null;
                foreach(int fps in new[]{15,30,60})
                {
                    var spring=new ArcherMotionRig.Spring();for(int i=0;i<fps;i++)spring.Step(new Vector3(6,2,-4),2.5f,1f/fps,12);
                    if(reference.HasValue)report.springFrameRateError=Mathf.Max(report.springFrameRateError,Vector3.Distance(reference.Value,spring.value));else reference=spring.value;
                }
                Check(report.springFrameRateError<.0001f,"spring frame-rate equivalence");
                report.ok=true;report.assertions=assertions-before;return report;
            }
            finally {Object.DestroyImmediate(root);}
        }
        [MenuItem("ERA/Archer/Audit run and shooting layers")]
        public static void Audit()
        {
            var root=Object.Instantiate(Resources.Load<GameObject>("Characters/Ember"));
            try
            {
                var a=root.GetComponent<HeroMotion>().archer;
                var names=new[]{"Pelvis","Spine","Chest","Clavicle.L","Clavicle.R","Head","Cape1.C","Cape2.C","QuiverAttach"};
                var values=names.Select(n=>new Range{bone=n}).ToArray();
                for(int hold=0;hold<2;hold++)
                {
                    var first=new Quaternion[names.Length];
                    for(int frame=0;frame<=24;frame++)
                    {
                        a.animator.enabled=true;a.animator.Rebind();a.animator.SetFloat("Speed",a.runSpeed);a.animator.SetFloat("LocomotionRate",1);
                        if(a.animator.parameters.Any(p=>p.name=="MoveZ"))a.animator.SetFloat("MoveZ",a.runSpeed);
                        a.animator.Play("Base Layer.Locomotion",0,frame/24f);a.animator.Play("UpperBody.Archer_BowHold",1,.25f);
                        a.animator.SetLayerWeight(1,hold);a.animator.Update(0);
                        for(int i=0;i<names.Length;i++)
                        {
                            var q=a.Bone(names[i]).localRotation;if(frame==0)first[i]=q;
                            if(hold==0)values[i].run=Mathf.Max(values[i].run,Quaternion.Angle(first[i],q));
                            else values[i].runHold=Mathf.Max(values[i].runHold,Quaternion.Angle(first[i],q));
                        }
                    }
                }
                var controller=(UnityEditor.Animations.AnimatorController)a.animator.runtimeAnimatorController;var mask=controller.layers[1].avatarMask;
                var report=new AuditReport{rotations=values,upperMask=Enumerable.Range(0,mask.transformCount).Where(i=>mask.GetTransformActive(i)).Select(mask.GetTransformPath).ToArray()};
                Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/archer-layer-audit.json",JsonUtility.ToJson(report,true));
                Debug.Log("ERA_ARCHER_LAYER_AUDIT "+JsonUtility.ToJson(report));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
