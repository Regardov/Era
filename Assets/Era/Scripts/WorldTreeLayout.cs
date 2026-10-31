using UnityEngine;
namespace Era
{
    // Shared, deterministic placement/collision; never tied to client-only MeshColliders.
    public static class WorldTreeLayout
    {
        public const float Height=156, CrownDiameter=144, TrunkRadius=16.5f;
        static WorldTreeCollisionData collision;
        public static WorldTreeCollisionData Collision=>collision!=null?collision:(collision=Resources.Load<WorldTreeCollisionData>("Environment/YggdrasilCollision"));
        public static void InvalidateCollision(){collision=null;}
        public static float RootCompression(float radius)=>Mathf.Lerp(1,.20f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(TrunkRadius*.873f,TrunkRadius*1.636f,radius)));
        public static Vector3 Sanctuary=>new Vector3(0,0,-24);
        public static bool ContainsTrunk(Vector3 p,float clearance)=>Collision!=null?Collision.Contains(new Vector2(p.x,p.z),clearance):p.x*p.x+p.z*p.z<(TrunkRadius+clearance)*(TrunkRadius+clearance);
        public static Vector3 ResolveTrunk(Vector3 p,float clearance)
        {
            if(Collision!=null){var corrected=Collision.Resolve(new Vector2(p.x,p.z),clearance);p.x=corrected.x;p.z=corrected.y;return p;}
            float radius=TrunkRadius+clearance,sq=p.x*p.x+p.z*p.z;
            if(sq>=radius*radius)return p;
            var direction=sq>.000001f?new Vector2(p.x,p.z).normalized:Vector2.down;
            p.x=direction.x*radius;p.z=direction.y*radius;return p;
        }
    }
}
