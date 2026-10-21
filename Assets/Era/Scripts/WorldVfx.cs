using UnityEngine;
namespace Era
{
    public static class WorldVfx
    {
        static Mesh crystalMesh;
        public static GameObject Crystal(VisualFactory v,Transform parent,string name,Vector3 position,float size,Color color)
        {
            if(crystalMesh==null)
            {
                crystalMesh=new Mesh{name="Faceted experience crystal"};var vertices=new Vector3[48];var indices=new int[48];int at=0;
                for(int side=0;side<8;side++)
                {
                    float a=side*Mathf.PI/4,b=(side+1)*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a)*.34f,0,Mathf.Sin(a)*.34f);var q=new Vector3(Mathf.Cos(b)*.34f,0,Mathf.Sin(b)*.34f);
                    vertices[at]=p;vertices[at+1]=Vector3.up*.8f;vertices[at+2]=q;vertices[at+3]=q;vertices[at+4]=Vector3.down*.45f;vertices[at+5]=p;at+=6;
                }
                for(int i=0;i<indices.Length;i++)indices[i]=i;crystalMesh.vertices=vertices;crystalMesh.triangles=indices;crystalMesh.RecalculateNormals();crystalMesh.RecalculateBounds();
            }
            var root=v.Group(name,parent,position).gameObject;root.AddComponent<MeshFilter>().sharedMesh=crystalMesh;root.AddComponent<MeshRenderer>().sharedMaterial=v.Material(color,true);root.transform.localScale=Vector3.one*size;return root;
        }
    }
    public sealed class ShrineVisual:MonoBehaviour
    {
        public Transform crystal,inner,outer;public LineRenderer circle;public Light lightSource;public bool announced;public Color color;
        public static ShrineVisual Create(VisualFactory v,Transform parent,ShrineState s)
        {
            var root=v.Group("Ancient blessing shrine / "+s.style,parent,s.position);var result=root.gameObject.AddComponent<ShrineVisual>();result.color=s.style==0?new Color(.2f,.65f,1):s.style==1?new Color(.87f,.58f,.18f):new Color(.63f,.39f,.91f);
            Color stone=new Color(.29f,.34f,.35f),metal=new Color(.63f,.51f,.30f);
            v.Shape("Carved stone foundation",PrimitiveType.Cylinder,root,new Vector3(0,.10f,0),new Vector3(2.7f,.15f,2.7f),stone);
            v.Shape("Gold inlay",PrimitiveType.Cylinder,root,new Vector3(0,.28f,0),new Vector3(2.03f,.04f,2.03f),metal);
            v.Cone("Tapered plinth",root,new Vector3(0,.29f,0),.86f,1.28f,stone,8);
            v.Shape("Offering bowl",PrimitiveType.Cylinder,root,new Vector3(0,1.2f,0),new Vector3(1.35f,.1f,1.35f),stone);
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;var p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v.Branch(root,p*.83f+Vector3.up*.4f,p*1.04f+Vector3.up*1.8f,.14f,stone);WorldVfx.Crystal(v,root,"Rune shard",p*1.04f+Vector3.up*1.88f,.24f,result.color);
            }
            result.crystal=WorldVfx.Crystal(v,root,"Floating blessing core",new Vector3(0,2,0),.75f,result.color).transform;
            result.inner=v.Ring("Orbiting runic seal",root,.69f,result.color,.045f).transform;result.inner.localPosition=Vector3.up*2;result.inner.localRotation=Quaternion.Euler(65,0,0);
            result.outer=v.Ring("Outer seal",root,.85f,metal,.035f).transform;result.outer.localPosition=Vector3.up*2;result.outer.localRotation=Quaternion.Euler(25,0,70);
            result.circle=v.Ring("Channel progress circle",root,1.6f,result.color,.06f).GetComponent<LineRenderer>();result.circle.transform.localPosition=Vector3.up*.045f;
            var lamp=new GameObject("Shrine glow");lamp.transform.SetParent(root,false);lamp.transform.localPosition=Vector3.up*2;result.lightSource=lamp.AddComponent<Light>();result.lightSource.color=result.color;result.lightSource.range=6;result.lightSource.intensity=.8f;
            result.announced=s.spent;SurfaceDetail.Apply(root);return result;
        }
        public void Present(ShrineState state,float time)
        {
            float progress=state.charge/5;crystal.localPosition=Vector3.up*(2+Mathf.Sin(time*1.6f+state.id)*.12f+progress*.35f);crystal.localRotation=Quaternion.Euler(0,time*(35+progress*110),Mathf.Sin(time)*8);
            crystal.localScale=Vector3.one*(state.spent?.28f:.75f+progress*.2f);inner.localRotation=Quaternion.Euler(65,time*48,0);outer.localRotation=Quaternion.Euler(time*19,0,70);
            inner.gameObject.SetActive(!state.spent);outer.gameObject.SetActive(!state.spent);lightSource.intensity=state.spent?.12f:.8f+progress*2+Mathf.Sin(time*3)*.1f;
            circle.startColor=circle.endColor=state.spent?new Color(.2f,.25f,.26f):Color.Lerp(color*.5f,Color.white,progress);circle.widthMultiplier=.04f+progress*.09f;
            if(state.spent&&!announced){announced=true;SynthAudio.Instance?.Play(13);}
        }
    }
}
