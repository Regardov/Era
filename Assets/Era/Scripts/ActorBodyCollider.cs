using UnityEngine;
namespace Era
{
    // Kinematic query bodies mirror host capsules. They never apply a second set of forces.
    [DisallowMultipleComponent]
    public sealed class ActorBodyCollider:MonoBehaviour
    {
        public const int PlayerLayer=8,EnemyLayer=9;
        CapsuleCollider body;
        public static ActorBodyCollider Attach(GameObject root)
        {
            var c=root.GetComponent<ActorBodyCollider>();if(c==null)c=root.AddComponent<ActorBodyCollider>();
            c.body=root.GetComponent<CapsuleCollider>();if(c.body==null)c.body=root.AddComponent<CapsuleCollider>();
            var rb=root.GetComponent<Rigidbody>();if(rb==null)rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.None;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            return c;
        }
        public void Present(PlayerState p){gameObject.layer=PlayerLayer;Set(ActorBodies.Player(p),!p.downed&&!p.eliminated);}
        public void Present(EnemyState e){gameObject.layer=EnemyLayer;Set(ActorBodies.Enemy(e),e.hp>0);}
        void Set(ActorCapsule shape,bool live)
        {
            if(body==null)return;float scale=Mathf.Max(.001f,transform.lossyScale.x);body.isTrigger=false;body.direction=1;body.radius=shape.radius/scale;body.height=shape.height/scale;body.center=Vector3.up*shape.height/(2*scale);body.enabled=live;
        }
        public void Retire(){if(body!=null)body.enabled=false;}
    }
}
