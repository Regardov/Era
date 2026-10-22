using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    public sealed class WorldEnvironment : MonoBehaviour
    {
        public const int CurrentVersion=9;
        public int bakedVersion,stage=1;
        public string mapName="Seven Realms / sculpted terrain / reactive grass";
        bool runtimeGenerated;
        static Material skyMaterial;
        public void Build()
        {
            bakedVersion=CurrentVersion;stage=WorldMap.Stage;runtimeGenerated=Application.isPlaying;
            SceneryBuilder.Build(transform,stage);
            var v=new VisualFactory();var landmarks=v.Group("05 / Ancient landmarks",transform);
            foreach(var point in WorldMap.Landmarks)
            {
                var root=v.Group("Waystone sanctuary",landmarks,WorldMap.OnGround(point.x,point.z));
                var stone=new Color(.38f,.43f,.43f);
                v.Shape("Weathered stone dais",PrimitiveType.Cylinder,root,Vector3.down*.12f,new Vector3(7,.12f,7),stone);
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;var pos=new Vector3(Mathf.Cos(a)*3,1.1f,Mathf.Sin(a)*3);
                    var pillar=v.Rock("Ancient standing stone",root,pos,new Vector3(.55f,2.1f,.7f),stone,stage*53+i);
                    pillar.transform.localRotation=Quaternion.Euler(i*3,40*i,6-i*6);
                    v.Ellipsoid("Inlaid rune",root,pos+Vector3.up*.8f,Vector3.one*.12f,CampaignData.Color(stage),true);
                }
            }
            SurfaceDetail.Apply(landmarks);
            var lighting=v.Group("06 / Realm lighting",transform);
            var sun=new GameObject("Realm sun").AddComponent<Light>();sun.transform.SetParent(lighting,false);sun.type=LightType.Directional;
            sun.color=stage==3?new Color(.81f,.91f,1):stage>=5?new Color(.82f,.77f,1):new Color(1,.88f,.70f);
            sun.intensity=stage>=5?.9f:1.12f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(43,-32+stage*13,0);
            var fill=new GameObject("Sky bounce").AddComponent<Light>();fill.transform.SetParent(lighting,false);fill.type=LightType.Directional;
            fill.color=new Color(.56f,.69f,.86f);fill.intensity=.16f;fill.transform.rotation=Quaternion.Euler(28,140,0);
            ApplyAtmosphere();
        }
        public static void ApplyAtmosphere()
        {
            int s=WorldMap.Stage;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=s>=5?new Color(.30f,.35f,.49f):new Color(.49f,.60f,.69f);
            RenderSettings.ambientEquatorColor=new Color(.31f,.37f,.34f);RenderSettings.ambientGroundColor=new Color(.16f,.20f,.18f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=s>=5?new Color(.36f,.38f,.49f):s==3?new Color(.63f,.77f,.84f):new Color(.69f,.77f,.70f);
            RenderSettings.fogDensity=.0035f;
            var shader=Shader.Find("Era/Painted Sky");
            if(shader!=null)
            {
                if(skyMaterial==null)skyMaterial=new Material(shader){name="Realm sky"};var sky=skyMaterial;sky.SetColor("_Zenith",s>=5?new Color(.14f,.18f,.33f):s==3?new Color(.25f,.48f,.64f):new Color(.27f,.49f,.66f));
                sky.SetColor("_Horizon",RenderSettings.fogColor);RenderSettings.skybox=sky;
            }
        }
        void OnDestroy()
        {
            if(!runtimeGenerated)return;
            var meshes=new System.Collections.Generic.HashSet<Mesh>();var materials=new System.Collections.Generic.HashSet<Material>();
            foreach(var f in GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null&&f.sharedMesh.name!="Sphere"&&f.sharedMesh.name!="Cylinder"&&f.sharedMesh.name!="Cube"&&f.sharedMesh.name!="Capsule"&&f.sharedMesh.name!="Quad")meshes.Add(f.sharedMesh);
            foreach(var r in GetComponentsInChildren<Renderer>(true))foreach(var material in r.sharedMaterials)if(material!=null)materials.Add(material);
            foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);
        }
    }
}
