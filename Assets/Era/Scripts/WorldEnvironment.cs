using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    public sealed class WorldEnvironment : MonoBehaviour
    {
        public const int CurrentVersion=26;
        public int bakedVersion,stage=1;
        public string mapName="Seven Realms / streams, rolling hills, old trees and living meadow";
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
                // Keep the complete top above terrain, including the lowest tessellated edge.
                float top=.04f;
                for(int i=0;i<32;i++){float a=i*Mathf.PI/16;top=Mathf.Max(top,WorldMap.Height(point.x+Mathf.Cos(a)*3.5f,point.z+Mathf.Sin(a)*3.5f)-root.position.y+.04f);}
                v.Shape("Weathered stone dais",PrimitiveType.Cylinder,root,Vector3.up*(top-.12f),new Vector3(7,.12f,7),stone);
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;var pos=new Vector3(Mathf.Cos(a)*3,1.1f,Mathf.Sin(a)*3);
                    var pillar=v.Rock("Ancient standing stone",root,pos,new Vector3(.55f,2.1f,.7f),stone,stage*53+i);
                    pillar.transform.localRotation=Quaternion.Euler(i*3,40*i,6-i*6);
                    v.Ellipsoid("Inlaid rune",root,pos+Vector3.up*.8f,Vector3.one*.12f,CampaignData.Color(stage),true);
                }
            }
            SurfaceDetail.Apply(landmarks);
            BuildSettlements(v);
            var walls=v.Group("07 / Continuous stone perimeter",transform);float wallRadius=GameData.ArenaRadius-2;
            for(int i=0;i<96;i++)
            {
                float angle=i*360f/96,rad=angle*Mathf.Deg2Rad;var point=WorldMap.OnGround(Mathf.Sin(rad)*wallRadius,Mathf.Cos(rad)*wallRadius);
                var block=v.Shape("Stone boundary wall",PrimitiveType.Cube,walls,point+Vector3.up*1.5f,new Vector3(2*wallRadius*Mathf.Tan(Mathf.PI/96)+.3f,10,2.5f),new Color(.43f,.45f,.44f));block.transform.rotation=Quaternion.Euler(0,angle,0);
                var cap=v.Shape("Stone wall coping",PrimitiveType.Cube,walls,point+Vector3.up*6.6f,new Vector3(2*wallRadius*Mathf.Tan(Mathf.PI/96)+.4f,.35f,2.9f),new Color(.55f,.56f,.52f));cap.transform.rotation=block.transform.rotation;
            }
            SurfaceDetail.Apply(walls);
            CombineGeometry(walls,"Stone perimeter / combined blocks");
            var lighting=v.Group("06 / Realm lighting",transform);
            var sun=new GameObject("Realm sun").AddComponent<Light>();sun.transform.SetParent(lighting,false);sun.type=LightType.Directional;
            sun.color=stage==3?new Color(.81f,.91f,1):stage>=5?new Color(.82f,.77f,1):new Color(1,.88f,.70f);
            sun.intensity=stage>=5?.9f:1.12f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(43,-32+stage*13,0);
            var fill=new GameObject("Sky bounce").AddComponent<Light>();fill.transform.SetParent(lighting,false);fill.type=LightType.Directional;
            fill.color=new Color(.56f,.69f,.86f);fill.intensity=.16f;fill.transform.rotation=Quaternion.Euler(28,140,0);
            ApplyAtmosphere();
        }
        void BuildSettlements(VisualFactory v)
        {
            var settlements=v.Group("08 / Autumn cottages and roofless ruins",transform);
            Color plaster=new Color(.76f,.68f,.49f),wood=new Color(.30f,.22f,.15f),stone=new Color(.46f,.47f,.41f),roof=new Color(.47f,.22f,.14f),glass=new Color(.28f,.39f,.39f);
            int index=0;
            foreach(var building in WorldMap.Buildings)
            {
                var root=v.Group(building.ruined?"Old stone ruin":"Timber cottage",settlements,building.position);root.localRotation=Quaternion.Euler(0,building.angle,0);root.localScale=Vector3.one*building.size;
                v.Shape("Rough stone foundation",PrimitiveType.Cube,root,new Vector3(0,-.75f,0),new Vector3(6.6f,1.9f,5.2f),stone);
                if(building.ruined)
                {
                    for(int side=-1;side<=1;side+=2)for(int j=0;j<6;j++)
                    {
                        float height=1.0f+(j%3)*.45f;v.Shape("Broken masonry",PrimitiveType.Cube,root,new Vector3(-2.7f+j*1.06f,height*.5f+.15f,side*2.28f),new Vector3(1.0f,height,.52f),stone);
                    }
                    for(int j=0;j<4;j++)v.Rock("Fallen lintel",root,new Vector3(-2+j*1.3f,.35f,(j%2-.5f)*2.5f),new Vector3(1.1f,.65f,.65f),stone,index*7+j);
                }
                else
                {
                    v.Shape("Limewashed walls",PrimitiveType.Cube,root,new Vector3(0,1.8f,0),new Vector3(6,3.3f,4.6f),plaster);
                    for(int side=-1;side<=1;side+=2)
                    {
                        for(int corner=-1;corner<=1;corner+=2)v.Shape("Timber corner post",PrimitiveType.Cube,root,new Vector3(corner*2.94f,1.85f,side*2.32f),new Vector3(.22f,3.5f,.22f),wood);
                        v.Shape("Horizontal oak beam",PrimitiveType.Cube,root,new Vector3(0,3.3f,side*2.35f),new Vector3(6.2f,.22f,.20f),wood);
                        var slope=v.Shape("Overhanging tiled roof",PrimitiveType.Cube,root,new Vector3(side*1.65f,4.08f,0),new Vector3(3.95f,.22f,5.45f),roof);slope.transform.localRotation=Quaternion.Euler(0,0,-side*27);
                        for(int row=0;row<7;row++)
                        {
                            float x=side*(.08f+row*.51f),y=4.92f-Mathf.Abs(x)*.51f;
                            var tile=v.Shape("Terracotta roof course",PrimitiveType.Cube,root,new Vector3(x,y,0),new Vector3(.09f,.10f,5.5f),roof*.84f);tile.transform.localRotation=Quaternion.Euler(0,0,-side*27);
                        }
                        for(int window=-1;window<=1;window+=2)
                        {
                            v.Shape("Window recess",PrimitiveType.Cube,root,new Vector3(window*1.75f,2.0f,side*2.33f),new Vector3(.95f,1.04f,.10f),wood);
                            v.Shape("Dull window glass",PrimitiveType.Cube,root,new Vector3(window*1.75f,2.0f,side*2.40f),new Vector3(.74f,.82f,.05f),glass);
                            v.Shape("Window mullion",PrimitiveType.Cube,root,new Vector3(window*1.75f,2.0f,side*2.44f),new Vector3(.07f,.88f,.07f),wood);
                            v.Shape("Window sill",PrimitiveType.Cube,root,new Vector3(window*1.75f,1.46f,side*2.45f),new Vector3(1.14f,.14f,.30f),wood);
                        }
                    }
                    // The small gable closes the roof ends; all cottages have solid footprints.
                    for(int layer=0;layer<7;layer++){float h=layer*.21f;v.Shape("Timber gable",PrimitiveType.Cube,root,new Vector3(0,3.55f+h,0),new Vector3(5.8f-layer*.78f,.23f,4.58f),wood);}
                    v.Shape("Oak door",PrimitiveType.Cube,root,new Vector3(0,1.22f,-2.36f),new Vector3(1.1f,2.18f,.15f),wood);
                    for(int board=0;board<4;board++)v.Shape("Door plank",PrimitiveType.Cube,root,new Vector3(-.43f+board*.28f,1.22f,-2.46f),new Vector3(.035f,2.1f,.035f),roof);
                    v.Shape("Door step",PrimitiveType.Cube,root,new Vector3(0,.16f,-2.53f),new Vector3(1.45f,.30f,.14f),stone);
                    v.Shape("Stone chimney",PrimitiveType.Cube,root,new Vector3(1.9f,4.50f,.85f),new Vector3(.70f,2.0f,.78f),stone);
                    v.Shape("Chimney cap",PrimitiveType.Cube,root,new Vector3(1.9f,5.5f,.85f),new Vector3(.9f,.16f,.98f),stone);
                }
                index++;
            }
            SurfaceDetail.Apply(settlements);CombineGeometry(settlements,"Cottages and ruins / combined surfaces");
        }
        static void CombineGeometry(Transform walls,string name)
        {
            var groups=new System.Collections.Generic.Dictionary<Material,System.Collections.Generic.List<CombineInstance>>();var original=walls.GetComponentsInChildren<MeshFilter>();
            foreach(var filter in original){var material=filter.GetComponent<Renderer>().sharedMaterial;if(!groups.TryGetValue(material,out var list)){list=new System.Collections.Generic.List<CombineInstance>();groups[material]=list;}list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=walls.worldToLocalMatrix*filter.transform.localToWorldMatrix});}
            foreach(var pair in groups){var part=new GameObject(name);part.transform.SetParent(walls,false);var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;}
            foreach(var filter in original){if(Application.isPlaying){filter.gameObject.SetActive(false);Destroy(filter.gameObject);}else DestroyImmediate(filter.gameObject);}
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
