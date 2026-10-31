using UnityEngine;
namespace Era
{
    // Shared, deterministic placement/collision; never tied to client-only MeshColliders.
    public static class WorldTreeLayout
    {
        public const float Height=156, CrownDiameter=144, TrunkRadius=16.5f;
        public static Vector3 Sanctuary=>new Vector3(0,0,-24);
        public static bool ContainsTrunk(Vector3 p,float clearance)=>p.x*p.x+p.z*p.z<(TrunkRadius+clearance)*(TrunkRadius+clearance);
        public static Vector3 ResolveTrunk(Vector3 p,float clearance)
        {
            float radius=TrunkRadius+clearance,sq=p.x*p.x+p.z*p.z;
            if(sq>=radius*radius)return p;
            var direction=sq>.000001f?new Vector2(p.x,p.z).normalized:Vector2.down;
            p.x=direction.x*radius;p.z=direction.y*radius;return p;
        }
    }
}
