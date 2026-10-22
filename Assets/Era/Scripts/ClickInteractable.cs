using UnityEngine;
namespace Era
{
    public sealed class ClickInteractable:MonoBehaviour
    {
        public float radius=4;
        public static void Add(Transform target,Vector3 center,Vector3 size,float radius)
        {
            var marker=target.gameObject.AddComponent<ClickInteractable>();marker.radius=radius;var collider=target.gameObject.AddComponent<BoxCollider>();collider.center=center;collider.size=size;
        }
    }
}
