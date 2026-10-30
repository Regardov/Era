using UnityEngine;
namespace Era
{
    // A lightweight held sigil. It follows the support palm, never the back.
    [DefaultExecutionOrder(85)]
    public sealed class LumenRuneFocus:MonoBehaviour
    {
        public LumenAnimationDriver actor;
        public float PalmError{get;private set;}
        Transform support;LineRenderer[] lines;float clock;
        static Material material;
        public static GameObject Create(LumenAnimationDriver mage,Transform parent,int owner)
        {
            var root=new GameObject("Lumen / held amber rune");root.transform.SetParent(parent,false);
            var rune=root.AddComponent<LumenRuneFocus>();rune.actor=mage;
            root.AddComponent<WeaponPresentation>().kind=WeaponKind.Aura;root.GetComponent<WeaponPresentation>().BindOwner(owner);
            return root;
        }
        void Start()
        {
            support=actor.Bone("Hand.R").Find("ERA palm socket");
            if(material==null){material=new Material(Shader.Find("Sprites/Default")){name="Lumen amber rune / unlit"};material.color=Color.white;}
            void Line(string name,Vector3[] points,float width,bool loop)
            {
                var child=new GameObject(name);child.transform.SetParent(transform,false);var line=child.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.loop=loop;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=width;line.numCapVertices=0;
                line.startColor=line.endColor=new Color(1,.57f,.09f,1);line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            }
            Vector3[] Diamond(float x,float y,float radius)=>new[]{new Vector3(x,y+radius,0),new Vector3(x+radius*.66f,y,0),new Vector3(x,y-radius,0),new Vector3(x-radius*.66f,y,0)};
            Line("Outer diamond",Diamond(0,0,.175f),.008f,true);Line("Inner diamond",Diamond(0,0,.105f),.005f,true);
            var circle=new Vector3[16];for(int i=0;i<circle.Length;i++){float a=i*Mathf.PI*2/circle.Length;circle[i]=new Vector3(Mathf.Sin(a)*.16f,Mathf.Cos(a)*.13f,0);}
            Line("Broken ancient ring",circle,.004f,true);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Line("Floating rune facet "+i,Diamond(Mathf.Sin(a)*.225f,Mathf.Cos(a)*.225f,.022f),.005f,true);}
            Line("Axis upper",new[]{new Vector3(0,.18f,0),new Vector3(0,.22f,0)},.006f,false);
            Line("Axis lower",new[]{new Vector3(0,-.18f,0),new Vector3(0,-.22f,0)},.006f,false);
            lines=GetComponentsInChildren<LineRenderer>();
        }
        void LateUpdate()
        {
            if(support==null)return;
            if(GameApp.Instance?.WorldPaused!=true)clock+=PresentationMotion.Delta;
            var root=actor.actorRoot;var target=support.position+root.up*.15f+root.forward*.055f;
            transform.SetPositionAndRotation(target,root.rotation);PalmError=Vector3.Distance(transform.position,target);
            float pulse=1+Mathf.Sin(clock*2.2f)*.025f;transform.localScale=Vector3.one*pulse;
            foreach(var line in lines)line.enabled=!actor.IsDead;
        }
    }
}
