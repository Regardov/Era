using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    public sealed class WorldEnvironment : MonoBehaviour
    {
        public const int CurrentVersion=5;
        public int bakedVersion;
        public string mapName="Stillwater Reach / 420 m across / flat arena";
        public void Build()
        {
            bakedVersion=CurrentVersion;
            var v=new VisualFactory();
            var terrain=v.Group("01 / Flat meadows",transform);
            BuildGround(terrain);
            var grove=v.Group("02 / Lavender & willow groves",transform);
            var stone=v.Group("03 / Mossy boulders",transform);
            foreach(var prop in WorldMap.Props)
            {
                var root=v.Group(prop.kind==0?"Willow":"Moss rock",prop.kind==0?grove:stone,prop.position);
                root.localScale=Vector3.one*prop.size;root.localRotation=Quaternion.Euler(0,prop.angle,0);
                int seed=Mathf.RoundToInt(prop.position.x*31+prop.position.z*17+prop.angle*11);
                if(prop.kind==0)Tree(v,root,prop.position.x< -45,seed);
                else Rock(v,root,seed);
            }
            var flowers=v.Group("04 / Wildflowers & ferns",transform);var random=new System.Random(1921);
            for(int i=0;i<760;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=8+Mathf.Sqrt((float)random.NextDouble())*195;
                var p=WorldMap.OnGround(Mathf.Sin(a)*r,Mathf.Cos(a)*r);
                var clump=v.Group("Wildflower patch",flowers,p);
                Color bloom=i%3==0?new Color(0.82f,0.72f,0.87f):i%3==1?new Color(0.95f,0.82f,0.58f):new Color(0.82f,0.87f,0.70f);
                for(int j=0;j<3;j++)
                {
                    var stem=new Vector3((j-1)*0.23f,0.25f+j*0.08f,Mathf.Sin(j)*0.2f);
                    v.Bone("Stem",clump,stem/2,new Vector3(0.035f,stem.y/2,0.035f),new Color(0.36f,0.51f,0.36f));
                    v.Ellipsoid("Bloom",clump,stem,new Vector3(0.19f,0.13f,0.19f),bloom);
                }
                if(i%2==0)
                {
                    v.Branch(clump,new Vector3(-.08f,.02f,.04f),new Vector3(-.38f,.28f,.16f),.025f,new Color(.29f,.48f,.31f));
                    v.Branch(clump,new Vector3(.08f,.02f,-.04f),new Vector3(.37f,.25f,-.18f),.025f,new Color(.34f,.54f,.34f));
                }
            }
            var sanctuary=v.Group("05 / Waking sanctuary",transform);
            Landmark(v,sanctuary,WorldMap.OnGround(0,0),0);
            Landmark(v,sanctuary,WorldMap.OnGround(WorldMap.Landmarks[1].x,WorldMap.Landmarks[1].z),1);
            Landmark(v,sanctuary,WorldMap.OnGround(WorldMap.Landmarks[2].x,WorldMap.Landmarks[2].z),2);
            Landmark(v,sanctuary,WorldMap.OnGround(WorldMap.Landmarks[3].x,WorldMap.Landmarks[3].z),3);
            var boundary=v.Group("06 / Misty mountain ring",transform);
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40;float r=224+(i%3)*10;
                var p=WorldMap.OnGround(Mathf.Cos(a)*r,Mathf.Sin(a)*r)-Vector3.up*5;
                v.Cone("Distant peak",boundary,p,13+i%4,18+i%6*4,new Color(0.47f,0.59f,0.57f),7);
            }
            var ring=v.Ring("Soft world boundary",boundary,GameData.ArenaRadius,new Color(0.77f,0.83f,0.70f),0.12f).GetComponent<LineRenderer>();
            for(int i=0;i<ring.positionCount;i++){var p=ring.GetPosition(i);p.y=WorldMap.Ground(p)+0.15f;ring.SetPosition(i,p);}
            var lighting=v.Group("07 / Morning light",transform);
            var sun=new GameObject("Morning sun").AddComponent<Light>();sun.transform.SetParent(lighting,false);sun.type=LightType.Directional;
            sun.color=new Color(1,0.91f,0.79f);sun.intensity=1.04f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(46,-32,0);
            var fill=new GameObject("Sky fill").AddComponent<Light>();fill.transform.SetParent(lighting,false);fill.type=LightType.Directional;
            fill.color=new Color(0.71f,0.79f,0.94f);fill.intensity=0.28f;fill.transform.rotation=Quaternion.Euler(28,140,0);
            ApplyAtmosphere();
        }
        public static void ApplyAtmosphere()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.48f,0.58f,0.68f);
            RenderSettings.ambientEquatorColor=new Color(0.30f,0.39f,0.37f);
            RenderSettings.ambientGroundColor=new Color(0.18f,0.24f,0.20f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(0.71f,0.80f,0.78f);RenderSettings.fogDensity=0.0042f;
        }
        void BuildGround(Transform parent)
        {
            var mat=new Material(Shader.Find("Era/Low Poly")){name="Stillwater meadow"};
            const int chunks=11,side=16;const float size=40,step=size/side,extent=chunks*size*.5f;
            for(int cx=0;cx<chunks;cx++)for(int cz=0;cz<chunks;cz++)
            {
                var verts=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
                for(int x=0;x<side;x++)for(int z=0;z<side;z++)
                {
                    float wx=-extent+cx*size+x*step,wz=-extent+cz*size+z*step;
                    var a=WorldMap.OnGround(wx,wz);var b=WorldMap.OnGround(wx+step,wz);var c=WorldMap.OnGround(wx,wz+step);var d=WorldMap.OnGround(wx+step,wz+step);
                    Triangle(verts,indices,colors,a,c,b);Triangle(verts,indices,colors,b,c,d);
                }
                var mesh=new Mesh{name="Meadow chunk "+cx+" "+cz};mesh.SetVertices(verts);mesh.SetTriangles(indices,0);mesh.SetColors(colors);mesh.RecalculateNormals();
                var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            }
        }
        static void Triangle(List<Vector3> vertices,List<int> tris,List<Color> colors,Vector3 a,Vector3 b,Vector3 c)
        {
            int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);tris.Add(i);tris.Add(i+1);tris.Add(i+2);
            Vector3 p=(a+b+c)/3;float noise=Mathf.PerlinNoise(p.x*0.09f+100,p.z*0.09f+100);
            Color color=Color.Lerp(new Color(0.35f,0.51f,0.39f),new Color(0.61f,0.71f,0.48f),noise);
            if(p.x< -45)color=Color.Lerp(color,new Color(0.53f,0.59f,0.57f),0.35f);
            if(p.z< -50)color=Color.Lerp(color,new Color(0.72f,0.63f,0.43f),0.36f);
            colors.Add(color);colors.Add(color);colors.Add(color);
        }
        static void Tree(VisualFactory v,Transform root,bool lavender,int seed)
        {
            Color trunk=new Color(0.31f,0.25f,0.21f);var random=new System.Random(seed);
            v.Shape("Trunk",PrimitiveType.Cylinder,root,new Vector3(0,1.65f,0),new Vector3(.58f,1.65f,.58f),trunk);
            v.Branch(root,new Vector3(0,1.25f,0),new Vector3(-1.25f,3.25f,.38f),.22f,trunk);
            v.Branch(root,new Vector3(.05f,1.75f,0),new Vector3(1.35f,3.65f,-.25f),.21f,trunk);
            v.Branch(root,new Vector3(0,2.25f,.05f),new Vector3(-.15f,4.35f,1.05f),.18f,trunk);
            v.Branch(root,new Vector3(-.05f,2.6f,0),new Vector3(.38f,4.6f,-1.05f),.16f,trunk);
            Color leaf=lavender?new Color(.52f,.43f,.63f):new Color(.27f,.52f,.32f);
            Vector3[] positions={new Vector3(-1.45f,3.75f,.48f),new Vector3(1.38f,4.06f,-.30f),new Vector3(-.18f,4.82f,.82f),new Vector3(.45f,4.92f,-.94f),new Vector3(.05f,5.25f,.02f)};
            Vector3[] sizes={new Vector3(2.2f,1.45f,1.75f),new Vector3(2.25f,1.55f,1.82f),new Vector3(1.95f,1.38f,1.65f),new Vector3(1.82f,1.28f,1.58f),new Vector3(2.15f,1.48f,1.85f)};
            for(int i=0;i<positions.Length;i++)
            {
                float tint=(float)random.NextDouble()*.16f-.05f;Color color=tint>=0?Color.Lerp(leaf,Color.white,tint):Color.Lerp(leaf,Color.black,-tint);
                var cluster=v.Rock("Canopy leaves",root,positions[i],sizes[i],color,seed+i*37);cluster.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,(float)random.NextDouble()*8-4);
            }
        }
        static void Rock(VisualFactory v,Transform root,int seed)
        {
            Color stone=new Color(.39f,.44f,.42f);var main=v.Rock("Weathered rock",root,new Vector3(0,.48f,0),new Vector3(1.75f,1.45f,1.45f),stone,seed);
            main.transform.localRotation=Quaternion.Euler(8,17,7);
            var side=v.Rock("Broken rock",root,new Vector3(.92f,.20f,-.38f),new Vector3(.78f,.66f,.72f),Color.Lerp(stone,Color.white,.07f),seed+19);
            side.transform.localRotation=Quaternion.Euler(-6,42,12);
            var moss=v.Rock("Moss patch",root,new Vector3(-.18f,1.13f,.03f),new Vector3(1.18f,.13f,.92f),new Color(.35f,.55f,.31f),seed+43);
            moss.transform.localRotation=Quaternion.Euler(1,13,-4);
        }
        static void Landmark(VisualFactory v,Transform parent,Vector3 p,int type)
        {
            var root=v.Group(type==0?"Waking gate":type==1?"Moonwell":type==2?"Fallen garden":"Amber beacon",parent,p);
            var stone=new Color(0.63f,0.65f,0.57f);
            v.Shape("Sanctuary floor",PrimitiveType.Cylinder,root,new Vector3(0,-0.12f,0),new Vector3(10,0.13f,10),stone);
            if(type==0)
            {
                var r=v.Ring("Waking arch",root,2.8f,stone,0.38f);r.transform.localPosition=new Vector3(0,2.8f,3);r.transform.localRotation=Quaternion.Euler(90,0,0);
                r=v.Ring("Gate light",root,2.47f,GameData.Amber,0.09f);r.transform.localPosition=new Vector3(0,2.8f,3);r.transform.localRotation=Quaternion.Euler(90,0,0);
                v.Ring("Inlaid runes",root,4.6f,GameData.Mint,0.055f).transform.localPosition=Vector3.up*0.06f;
            }
            else if(type==1)
            {
                v.Shape("Well",PrimitiveType.Cylinder,root,new Vector3(0,0.25f,0),new Vector3(5,0.25f,5),stone);
                v.Shape("Still water",PrimitiveType.Cylinder,root,new Vector3(0,0.52f,0),new Vector3(4.2f,0.025f,4.2f),new Color(0.49f,0.76f,0.78f));
                v.Ellipsoid("Moon pearl",root,new Vector3(0,2.8f,0),Vector3.one*1.2f,new Color(0.79f,0.76f,0.91f),true);
            }
            else
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;var pos=new Vector3(Mathf.Cos(a)*4,1.5f,Mathf.Sin(a)*4);
                    var column=v.Shape("Weathered column",PrimitiveType.Cylinder,root,pos,new Vector3(0.9f,1.5f+i%2,0.9f),stone);
                    if(type==2)column.transform.localRotation=Quaternion.Euler(0,0,i%2==0?12:-7);
                }
                v.Ellipsoid("Beacon seed",root,new Vector3(0,3,0),new Vector3(1,1.8f,1),GameData.Amber,true);
            }
        }
    }
}
