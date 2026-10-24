using UnityEngine;
namespace Era
{
    // The hand anchors the visual; aim and follow-through never affect hitboxes.
    [DefaultExecutionOrder(80)]
    public sealed class WeaponGrip : MonoBehaviour
    {
        public Transform actor, hand;
        public int owner;
        public WeaponKind kind;
        WeaponPresentation presentation;
        bool initialized;
        void LateUpdate()
        {
            var app = GameApp.Instance;
            var player = app?.State?.Player(owner);
            if (player == null || actor == null || hand == null || app.WorldPaused) return;
            if (presentation == null) presentation = GetComponent<WeaponPresentation>();
            if (presentation != null) presentation.BindOwner(owner);
            var sample = presentation != null ? presentation.Sample : default;
            float aimYaw = player.weaponPoseTime > 0 ? player.weaponAimYaw : actor.eulerAngles.y;
            if (presentation != null && presentation.Anticipation > .02f) aimYaw = presentation.AimYaw;
            var aim = Quaternion.Euler(0, aimYaw, 0);
            var target = aim * Quaternion.Euler(sample.euler + new Vector3(0, kind==WeaponKind.Bow?-90:0, kind == WeaponKind.Bow ? -5 : -12));
            float dt = PresentationMotion.Delta;
            float follow = initialized ? 1 - Mathf.Exp(-Mathf.Lerp(22, 64, sample.strike) * dt) : 1;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, follow);
            var grip = kind == WeaponKind.Bow ? new Vector3(.36f, .48f, 0) : new Vector3(0, .05f, 0);
            transform.position = hand.position - transform.rotation * Vector3.Scale(grip, transform.localScale) + aim * sample.offset;
            initialized = true;
        }
    }
}
