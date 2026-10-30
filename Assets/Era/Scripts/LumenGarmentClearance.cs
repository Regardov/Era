using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    // One shared cross-section warp keeps front, side and lining overlaps together.
    [DefaultExecutionOrder(95)]
    public sealed class LumenGarmentClearance:MonoBehaviour
    {
        sealed class Panel
        {
            public SkinnedMeshRenderer skin;public Mesh original,mesh;public Transform[] originalBones;public Transform originalRoot;
            public Vector3[] source,rest,output,posed;public int[] triangles,representative,row;
        }
        struct Capsule{public Vector3 a,b;public float radius;}
        sealed class Row{public float y,left,right,front,back,newLeft,newRight,newFront,newBack;}
        public LumenAnimationDriver actor;public bool auditCollisions;
        Panel[] panels;Row[] rows;Transform pelvis;Matrix4x4 pelvisRestInverse;
        Capsule[] caps=new Capsule[6],localCaps=new Capsule[6];
        Transform[] legBones;
        public float MaxPenetration{get;private set;}public float MaxCorrection{get;private set;}public int CorrectedVertices{get;private set;}
        public string WorstPanel{get;private set;}="";public bool finite{get;private set;}=true;
        void Start(){Bind();}
        void Bind()
        {
            if(panels!=null)return;if(actor==null)actor=GetComponent<LumenAnimationDriver>();
            pelvis=actor.Bone("Pelvis");pelvisRestInverse=(actor.actorRoot.worldToLocalMatrix*pelvis.localToWorldMatrix).inverse;
            legBones=new[]{actor.Bone("Thigh.L"),actor.Bone("Shin.L"),actor.Bone("Foot.L"),actor.Bone("Toe.L"),actor.Bone("Thigh.R"),actor.Bone("Shin.R"),actor.Bone("Foot.R"),actor.Bone("Toe.R")};
            panels=GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name=="AW_OuterCoat"||s.name=="AW_InnerCoat"||s.name=="AW_RearRobeInlay").Select(s=>
            {
                var mesh=Instantiate(s.sharedMesh);mesh.name=s.sharedMesh.name+" / continuous robe instance";mesh.MarkDynamic();
                var p=new Panel{skin=s,original=s.sharedMesh,mesh=mesh,originalBones=s.bones,originalRoot=s.rootBone,source=mesh.vertices,output=mesh.vertices,posed=mesh.vertices,triangles=mesh.triangles};
                p.rest=new Vector3[p.source.Length];p.representative=new int[p.source.Length];p.row=new int[p.source.Length];
                var unique=new Dictionary<Vector3,int>();
                for(int i=0;i<p.source.Length;i++)
                {
                    p.rest[i]=actor.actorRoot.InverseTransformPoint(s.transform.TransformPoint(p.source[i]));
                    var key=p.source[i];if(unique.TryGetValue(key,out int index))p.representative[i]=index;else{p.representative[i]=i;unique[key]=i;}
                }
                // Vertices are already posed by this component. A single root
                // binding avoids inverse-skinning them only to skin them again.
                mesh.bindposes=new[]{Matrix4x4.identity};mesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},p.source.Length).ToArray();
                s.sharedMesh=mesh;s.bones=new[]{actor.actorRoot};s.rootBone=actor.actorRoot;return p;
            }).ToArray();
            var outer=panels.First(p=>p.skin.name=="AW_OuterCoat").rest;
            var levels=panels.SelectMany(p=>p.rest).Select(v=>Mathf.Round(v.y*10000)/10000).Distinct().OrderBy(y=>y).ToArray();
            rows=levels.Select(y=>
            {
                var band=outer.Where(v=>Mathf.Abs(v.y-y)<.065f).ToArray();if(band.Length<4)band=outer.OrderBy(v=>Mathf.Abs(v.y-y)).Take(30).ToArray();
                return new Row{y=y,left=band.Max(v=>v.x),right=band.Min(v=>v.x),front=band.Max(v=>v.z),back=band.Min(v=>v.z)};
            }).ToArray();
            var lookup=levels.Select((y,i)=>(y,i)).ToDictionary(p=>p.y,p=>p.i);
            foreach(var p in panels)for(int i=0;i<p.rest.Length;i++)p.row[i]=lookup[Mathf.Round(p.rest[i].y*10000)/10000];
        }
        static Vector3 Nearest(Capsule c,Vector3 p){var d=c.b-c.a;return c.a+d*Mathf.Clamp01(Vector3.Dot(p-c.a,d)/Mathf.Max(.000001f,d.sqrMagnitude));}
        static bool Covers(Capsule c,float y,float r)=>y>=Mathf.Min(c.a.y,c.b.y)-r&&y<=Mathf.Max(c.a.y,c.b.y)+r;
        static float Bound(Capsule c,float y,int axis,float sign,float radius)
        {
            float dy=c.b.y-c.a.y,da=c.b[axis]-c.a[axis],t=Mathf.Abs(dy)<.00001f?(sign*c.b[axis]>sign*c.a[axis]?1:0):Mathf.Clamp01((y-c.a.y+radius*sign*da*Mathf.Sign(dy)/Mathf.Sqrt(dy*dy+da*da))/dy);
            float gap=Mathf.Abs(y-Mathf.Lerp(c.a.y,c.b.y,t));return Mathf.Lerp(c.a[axis],c.b[axis],t)+sign*Mathf.Sqrt(Mathf.Max(0,radius*radius-gap*gap));
        }
        void LateUpdate()
        {
            Bind();if(actor==null||GameApp.Instance?.WorldPaused==true)return;MaxPenetration=MaxCorrection=0;CorrectedVertices=0;finite=true;WorstPanel="";
            var inv=actor.actorRoot.worldToLocalMatrix;var delta=inv*pelvis.localToWorldMatrix*pelvisRestInverse;var unpose=delta.inverse;int index=0;
            for(int side=0;side<2;side++)
            {
                int bone=side*4;var thigh=inv.MultiplyPoint3x4(legBones[bone].position);var shin=inv.MultiplyPoint3x4(legBones[bone+1].position);var foot=inv.MultiplyPoint3x4(legBones[bone+2].position);var toe=inv.MultiplyPoint3x4(legBones[bone+3].position);
                caps[index++]=new Capsule{a=thigh,b=shin,radius=.080f};caps[index++]=new Capsule{a=shin,b=foot,radius=.084f};caps[index++]=new Capsule{a=foot,b=toe,radius=.070f};
            }
            for(int i=0;i<caps.Length;i++)localCaps[i]=new Capsule{a=unpose.MultiplyPoint3x4(caps[i].a),b=unpose.MultiplyPoint3x4(caps[i].b),radius=caps[i].radius};
            foreach(var row in rows)
            {
                row.newLeft=row.left;row.newRight=row.right;row.newFront=row.front;row.newBack=row.back;
                foreach(var c in localCaps)
                {
                    float radius=c.radius+.065f;if(!Covers(c,row.y,radius))continue;
                    row.newLeft=Mathf.Max(row.newLeft,Bound(c,row.y,0,1,radius));row.newRight=Mathf.Min(row.newRight,Bound(c,row.y,0,-1,radius));
                    row.newFront=Mathf.Max(row.newFront,Bound(c,row.y,2,1,radius));row.newBack=Mathf.Min(row.newBack,Bound(c,row.y,2,-1,radius));
                }
            }
            foreach(var p in panels)
            {
                for(int i=0;i<p.source.Length;i++)
                {
                    if(p.representative[i]!=i){p.output[i]=p.output[p.representative[i]];p.posed[i]=p.posed[p.representative[i]];continue;}
                    var rest=p.rest[i];var row=rows[p.row[i]];
                    var q=new Vector3(Mathf.LerpUnclamped(row.newRight,row.newLeft,(rest.x-row.right)/Mathf.Max(.01f,row.left-row.right)),rest.y,Mathf.LerpUnclamped(row.newBack,row.newFront,(rest.z-row.back)/Mathf.Max(.01f,row.front-row.back)));
                    for(int iteration=0;iteration<2;iteration++)foreach(var c in localCaps)
                    {
                        var center=Nearest(c,q);var d=q-center;float length=d.magnitude,radius=c.radius+.035f;
                        if(length<radius){if(length<.0001f)d=new Vector3(rest.x,0,rest.z).normalized;else d/=length;q=center+d*radius;}
                    }
                    var final=delta.MultiplyPoint3x4(q);MaxCorrection=Mathf.Max(MaxCorrection,Vector3.Distance(final,delta.MultiplyPoint3x4(rest)));if((q-rest).sqrMagnitude>.000001f)CorrectedVertices++;
                    p.output[i]=p.posed[i]=final;finite&=ActorMotionMath.Finite(p.output[i])&&p.output[i].magnitude<8;
                    if(auditCollisions)foreach(var c in caps)MaxPenetration=Mathf.Max(MaxPenetration,c.radius-Vector3.Distance(p.posed[i],Nearest(c,p.posed[i])));
                }
                if(auditCollisions)for(int t=0;t<p.triangles.Length;t+=3)
                {
                    var point=(p.posed[p.triangles[t]]+p.posed[p.triangles[t+1]]+p.posed[p.triangles[t+2]])/3;
                    foreach(var c in caps){float amount=c.radius-Vector3.Distance(point,Nearest(c,point));if(amount>MaxPenetration){MaxPenetration=amount;WorstPanel=p.skin.name+" triangle "+t/3;}}
                }
                p.mesh.vertices=p.output;p.mesh.RecalculateNormals();p.mesh.RecalculateBounds();
            }
        }
        void OnDestroy(){if(panels==null)return;foreach(var p in panels){if(p.skin!=null){p.skin.sharedMesh=p.original;p.skin.bones=p.originalBones;p.skin.rootBone=p.originalRoot;}Destroy(p.mesh);}}
    }
}
