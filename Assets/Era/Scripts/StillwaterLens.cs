using UnityEngine;
namespace Era
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class StillwaterLens:MonoBehaviour
    {
        Material material;
        void OnEnable(){
            var shader=Shader.Find("Hidden/Era/Stillwater Lens");
            if(shader==null||!shader.isSupported||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){enabled=false;return;}
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            GetComponent<Camera>().depthTextureMode|=DepthTextureMode.DepthNormals;
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination){
            if(material==null){Graphics.Blit(source,destination);return;}
            int w=Mathf.Max(1,source.width/4),h=Mathf.Max(1,source.height/4);
            var a=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.DefaultHDR);
            var b=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.DefaultHDR);
            a.filterMode=b.filterMode=FilterMode.Bilinear;
            try {
                Graphics.Blit(source,a,material,0);
                material.SetVector("_BlurDirection",new Vector2(1,0));Graphics.Blit(a,b,material,1);
                material.SetVector("_BlurDirection",new Vector2(0,1));Graphics.Blit(b,a,material,1);
                material.SetTexture("_BloomTex",a);material.SetFloat("_BloomStrength",.22f);material.SetFloat("_OcclusionStrength",.68f);
                Graphics.Blit(source,destination,material,2);
            }
            finally {RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        void OnDisable(){if(material!=null){Destroy(material);material=null;}}
    }
}
