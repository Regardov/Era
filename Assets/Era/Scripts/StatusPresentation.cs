using UnityEngine;
namespace Era
{
    public sealed class StatusPresentation:MonoBehaviour
    {
        int previousMask,cursor;float next;GameObject ice;float iceBlend;GameObject burn;float burnBlend,clock;
        static readonly FeedbackStyle[] Styles={FeedbackStyle.Fire,FeedbackStyle.Frost,FeedbackStyle.Poison,FeedbackStyle.Blood,FeedbackStyle.Lightning,FeedbackStyle.Mark,FeedbackStyle.Root};
        public int ActiveMask{get;private set;}
        public void Present(EnemyState enemy)
        {
            int mask=enemy.frozen>0?2:0;
            foreach(var a in enemy.ailments)if(a.time>0&&a.owner>=0)
            {int bit=a.kind==Ailment.Burn?0:a.kind==Ailment.Chill?(a.visual==FeedbackStyle.Root?6:1):a.kind==Ailment.Poison?2:a.kind==Ailment.Bleed?3:a.kind==Ailment.Shock?4:a.kind==Ailment.Mark?5:-1;if(bit>=0)mask|=1<<bit;}
            ActiveMask=mask;
            float dt=PresentationMotion.Delta;next-=dt;clock+=dt;var shape=ActorBodies.Enemy(enemy);
            var camera=GameApp.Instance?.View?.Camera;bool near=camera==null||(transform.position-camera.transform.position).sqrMagnitude<40*40;
            if(dt>0&&near&&mask!=0)
            {
                for(int i=0;i<Styles.Length;i++)if((mask&(1<<i))!=0&&(previousMask&(1<<i))==0)SynthAudio.Instance?.PlaySemantic(Styles[i],transform.position);
                if(next<=0)
                {
                    next=.48f+(enemy.id%7)*.037f;
                    for(int n=0;n<Styles.Length;n++){int bit=(cursor+n)%Styles.Length;if((mask&(1<<bit))==0)continue;cursor=(bit+1)%Styles.Length;
                        var point=transform.position+Vector3.up*(bit==5?shape.height+.20f:bit==6?.12f:shape.height*.4f);
                        PresentationFx.EmitStyle(point,Styles[bit],bit==6?shape.radius:Mathf.Min(.55f,shape.radius*.7f));break;}
                }
            }
            previousMask=mask;
            burnBlend=PresentationMotion.Damp(burnBlend,(mask&1)!=0&&near?1:0,15,dt);
            if(burnBlend>.01f&&burn==null){burn=FeedbackGeometry.MeshObject("Active burning flames",transform,FeedbackGeometry.FlameCluster,FeedbackGeometry.Fire);var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(1,1,1,.7f));burn.GetComponent<Renderer>().SetPropertyBlock(block);}
            if(burn!=null){burn.SetActive(burnBlend>.01f&&PresentationMotion.Intensity>0);burn.transform.localPosition=Vector3.up*shape.height*.18f;burn.transform.localScale=new Vector3(Mathf.Min(1.4f,shape.radius*1.8f),shape.height*.60f,Mathf.Min(1.4f,shape.radius*1.8f))*burnBlend*(.95f+Mathf.Sin(clock*11+enemy.id)*.055f);burn.transform.localRotation=Quaternion.Euler(0,enemy.id*47,0);}
            iceBlend=PresentationMotion.Damp(iceBlend,enemy.frozen>0&&near?1:0,12,dt);
            if(iceBlend>.01f&&ice==null)
            {
                var pieces=new CombineInstance[5];var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.48f,.83f,1,.58f));
                for(int i=0;i<pieces.Length;i++){float a=i*2.399963f;pieces[i]=new CombineInstance{mesh=FeedbackGeometry.Shard,transform=Matrix4x4.TRS(new Vector3(Mathf.Cos(a)*shape.radius*1.05f,shape.height*(.18f+i*.13f),Mathf.Sin(a)*shape.radius*1.05f),Quaternion.Euler(i*17,i*71,i*29),new Vector3(.75f,1.1f,.60f))};}
                var mesh=new Mesh{name="Combined frost crust"};mesh.CombineMeshes(pieces);ice=FeedbackGeometry.MeshObject("Frost crust",transform,mesh,FeedbackGeometry.Solid);ice.AddComponent<OwnedVisualResource>().mesh=mesh;ice.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            if(ice!=null){ice.SetActive(iceBlend>.01f&&PresentationMotion.Intensity>0);ice.transform.localScale=Vector3.one*iceBlend;}
        }
    }
}
