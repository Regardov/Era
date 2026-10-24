using UnityEngine;
namespace Era
{
    public sealed class HazardPresentation:MonoBehaviour
    {
        LineRenderer progress;float duration;bool triggered;
        public void Present(HazardState h)
        {
            if(progress==null)
            {
                var go=new GameObject("Attack countdown / exact boundary");go.transform.SetParent(transform,false);progress=go.AddComponent<LineRenderer>();progress.sharedMaterial=PresentationFx.SharedMaterial;progress.useWorldSpace=false;progress.positionCount=48;progress.widthMultiplier=.11f;duration=Mathf.Max(.01f,h.delay);
            }
            float charge=1-Mathf.Clamp01(h.delay/duration);Color c=Color.Lerp(new Color(1,.4f,.15f,.55f),new Color(1,.8f,.65f,.9f),charge);progress.startColor=progress.endColor=c;
            progress.enabled=!h.triggered;
            for(int i=0;i<48;i++){float a=i/47f*Mathf.PI*2*charge;var p=new Vector3(Mathf.Cos(a)*h.radius,0,Mathf.Sin(a)*h.radius);p.y=WorldMap.Ground(h.position+p)-h.position.y+.025f;progress.SetPosition(i,p);}
            if(h.triggered&&!triggered){triggered=true;PresentationFx.Emit(h.position,CampaignData.Color(h.style),h.radius,FeedbackKind.Magic);}
        }
    }
}
