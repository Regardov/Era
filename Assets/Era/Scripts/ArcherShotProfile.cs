using UnityEngine;

namespace Era
{
    // Baked Archer_BowRelease, frame 1 (t=0), then 1.8m -> 2.23m scale.
    // This deterministic profile belongs to Simulation; no view/socket state
    // is sent to the host or used to decide projectile damage.
    public static class ArcherShotProfile
    {
        public static readonly Vector3 OriginOffset = new Vector3(.15996370f, 1.87504756f, .88690402f);
        public static Vector3 Origin(Vector3 actorPosition, float aimYaw) => actorPosition + Quaternion.Euler(0, aimYaw, 0) * OriginOffset;
    }
}
