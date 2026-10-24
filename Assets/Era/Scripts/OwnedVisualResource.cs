using UnityEngine;
namespace Era
{
    // Only resources generated for this object are owned; imported/shared assets are never destroyed.
    public sealed class OwnedVisualResource:MonoBehaviour
    {
        public Mesh mesh;public Material material;
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
