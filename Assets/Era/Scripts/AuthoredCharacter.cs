using UnityEngine;
namespace Era
{
    // Trace the in-game surface to the editable Blender source and its named detail groups.
    public sealed class AuthoredCharacter:MonoBehaviour
    {
        public string model,sourceHash;
        public int version=16,triangles;
        public string[] details;
    }
}
