using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    // Shared static ribbons, one small dynamic sparkle mesh, no lights or physics.
    [DefaultExecutionOrder(82)]
    public sealed class LumenAuraVisual:MonoBehaviour
    {
        public const int SparkCount=20;
        public float Radius{get;private set;}
        public float AnimationClock=>clock;
        public int DynamicVertices=>SparkCount*8;
        static Material material;
        static Mesh longArc,shortArc,fullRing;
        Transform[] ribbons;
        MeshRenderer[] renderers;
        Mesh stars;
        Vector3[] points=new Vector3[SparkCount*8];
        Color[] colors=new Color[SparkCount*8];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        PlayerState source;
        float clock,lastRadius=-1;
        public void Present(PlayerState player)
        {
            source=player;Radius=Simulation.AuraRadius(player);
            transform.position=GameApp.Instance?.View!=null?GameApp.Instance.View.PlayerVisualPosition(player):player.position;
            if(ribbons==null)Build();
        }
        static Mesh Arc(float fraction,float width,float lift)
        {
            const int segments=96;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++)
            {
                float t=(float)i/segments,a=t*Mathf.PI*2*fraction,spread=width*(fraction>=.999f?1:.12f+.88f*Mathf.Sin(t*Mathf.PI));
                for(int j=0;j<2;j++){float r=1+(j==0?-spread:spread);vertices[i*2+j]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(t*Mathf.PI)*lift,Mathf.Sin(a)*r);uv[i*2+j]=new Vector2(fraction>=.999f?.5f:t,j);colors[i*2+j]=Color.white;}
                if(i<segments){int v=i*2,k=i*6;triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;}
            }
            var mesh=new Mesh{name="Lumen shared luminous arc"};mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        MeshRenderer Child(string name,Mesh mesh,out Transform child)
        {
            child=new GameObject(name).transform;child.SetParent(transform,false);child.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return renderer;
        }
        void Build()
        {
            if(material==null)
            {
                material=new Material(Shader.Find("Era/Lumen Aura")){name="Lumen golden light / shared"};
                longArc=Arc(.76f,.060f,.022f);shortArc=Arc(.46f,.030f,.040f);fullRing=Arc(1,.008f,0);
            }
            ribbons=new Transform[7];renderers=new MeshRenderer[8];
            for(int i=0;i<ribbons.Length;i++)renderers[i]=Child(i==6?"Crown halo":"Rotating light ribbon "+i,i==5||i==6?fullRing:i==3||i==4?shortArc:longArc,out ribbons[i]);
            stars=new Mesh{name="Lumen bounded animated sparks"};stars.MarkDynamic();
            var uv=new Vector2[points.Length];var triangles=new int[SparkCount*12];
            for(int q=0;q<SparkCount*2;q++)
            {
                int v=q*4,k=q*6;uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(0,1);uv[v+2]=new Vector2(1,1);uv[v+3]=new Vector2(1,0);
                triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;triangles[k+3]=v;triangles[k+4]=v+2;triangles[k+5]=v+3;
            }
            stars.vertices=points;stars.uv=uv;stars.triangles=triangles;renderers[7]=Child("Orbiting golden starbursts",stars,out _);
        }
        void Quad(int at,Vector3 center,Vector3 along,Vector3 across,float alpha)
        {
            points[at]=center-along-across;points[at+1]=center-along+across;points[at+2]=center+along+across;points[at+3]=center+along-across;
            for(int j=0;j<4;j++)colors[at+j]=new Color(1,1,1,alpha);
        }
        void LateUpdate()
        {
            if(source==null||ribbons==null)return;
            var app=GameApp.Instance;transform.position=app?.View!=null?app.View.PlayerVisualPosition(source):source.position;
            clock+=PresentationMotion.Delta;float intensity=PresentationMotion.Intensity,surge=source.auraSurge>0?1:0;
            float strength=.35f+.65f*intensity;
            for(int i=0;i<renderers.Length;i++){properties.SetColor("_Tint",i==3||i==4?new Color(.9f,.86f,.70f,strength*.38f):new Color(2.3f,1.25f,.12f,strength));renderers[i].SetPropertyBlock(properties);}
            for(int i=0;i<5;i++)
            {
                float scale=Radius*(i==0?1:i==1?.82f:i==2?.62f:i==3?1.03f:.91f);
                ribbons[i].localScale=Vector3.one*scale;
                ribbons[i].localPosition=Vector3.up*(.18f+i*.09f);
                ribbons[i].localRotation=Quaternion.Euler(0,clock*(i%2==0?32:-23)+i*103,0);
            }
            // A small inner seal keeps the source of the large aura readable.
            ribbons[5].localScale=Vector3.one*.78f;ribbons[5].localPosition=Vector3.up*.14f;ribbons[5].localRotation=Quaternion.Euler(0,-clock*45,0);
            ribbons[6].localScale=Vector3.one*.39f;ribbons[6].localPosition=Vector3.up*2.48f;ribbons[6].localRotation=Quaternion.Euler(0,clock*25,0);
            var camera=app?.View?.Camera;Vector3 right=camera!=null?camera.transform.right:Vector3.right;
            for(int i=0;i<SparkCount;i++)
            {
                bool beam=i<7;float a=i*2.399963f+clock*(beam?.08f:.16f),r=Radius*(beam?.97f:.32f+.66f*Mathf.Repeat(i*.618034f,1));
                float phase=Mathf.Repeat(clock*(beam?.25f:.42f)+i*.618034f,1);
                float pulse=Mathf.Pow(Mathf.Sin(phase*Mathf.PI),beam?5:2),height=beam?1.15f+surge*.4f:.07f;
                var center=new Vector3(Mathf.Cos(a)*r,beam?.85f:phase*1.8f,Mathf.Sin(a)*r);
                float alpha=pulse*(beam?.78f:.5f)*strength;
                Quad(i*8,center,Vector3.up*height,right*(beam?.055f:.035f),alpha);
                Quad(i*8+4,center,right*(beam?.30f:.055f),Vector3.up*(beam?.035f:.025f),alpha);
            }
            stars.vertices=points;stars.colors=colors;
            if(!Mathf.Approximately(lastRadius,Radius)){stars.bounds=new Bounds(Vector3.up, new Vector3(Radius*2+3,6,Radius*2+3));lastRadius=Radius;}
        }
        void OnDestroy(){if(stars!=null)Destroy(stars);}
    }
}
