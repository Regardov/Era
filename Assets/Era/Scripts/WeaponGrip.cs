using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(80)]
    public sealed class WeaponGrip:MonoBehaviour
    {
        public Transform actor,hand;public int owner;public WeaponKind kind;
        void LateUpdate()
        {
            var p=GameApp.Instance?.State?.Player(owner);if(p==null||hand==null||GameApp.Instance.WorldPaused)return;
            float swing=p.lastWeapon==(int)kind?Mathf.Sin(Mathf.Clamp01(p.weaponPoseTime/.32f)*Mathf.PI):0;
            var aim=Quaternion.Euler(0,p.weaponPoseTime>0?p.weaponAimYaw:actor.eulerAngles.y,0);
            var rotation=aim*Quaternion.Euler(kind==WeaponKind.Axe?-105*swing:kind==WeaponKind.Spear?70*swing:kind==WeaponKind.LightningStaff?-25*swing:kind==WeaponKind.Bow?-4*swing:0,0,kind==WeaponKind.Bow?-5-4*swing:-12);
            var grip=kind==WeaponKind.Bow?new Vector3(.36f,.48f,0):new Vector3(0,.05f,0);
            transform.rotation=rotation;transform.position=hand.position-rotation*grip*.8f;
        }
    }
}
