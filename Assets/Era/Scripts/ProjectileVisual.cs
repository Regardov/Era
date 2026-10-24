using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public sealed class ProjectileVisual:MonoBehaviour
    {
        static readonly List<ProjectileVisual> retiring=new List<ProjectileVisual>();
        Transform model;TrailRenderer trail;float age,remaining,retireAge;bool returning,retired;int contacts;Color color;Vector3 direction;
        public bool LikelyImpactOnRemoval=>remaining>.12f&&!returning;
        public static int RetiringCount=>retiring.Count;
        public static ProjectileVisual Create(VisualFactory v,Transform parent,ProjectileState b,WorldState state)
        {
            var root=v.Group("Projectile presentation / "+b.id,parent);var visual=root.gameObject.AddComponent<ProjectileVisual>();
            visual.model=(b.hostile?v.Facet("Hostile core",root,Vector3.zero,new Vector3(.18f,.18f,.35f),new Color(1,.22f,.08f)):WeaponVisuals.Arrow(v,root)).transform;
            visual.color=b.hostile?new Color(1,.25f,.1f):GameData.Weapons[(int)b.weapon].color;visual.trail=visual.model.GetComponent<TrailRenderer>();
            root.position=b.position;visual.direction=b.direction;visual.contacts=b.hitTargets.Count;
            PresentationFx.Emit(b.position,visual.color,.25f,FeedbackKind.Impact,b.direction);return visual;
        }
        public void Sync(ProjectileState b,float blend,float dt)
        {
            if(retired)return;age+=dt;remaining=b.life;returning=b.returning;direction=b.direction;
            // Keep the visual core on the authoritative flight path, including bounces and return arcs.
            transform.position=Vector3.Lerp(transform.position,b.position,blend);
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-40*dt));
            model.localPosition=Vector3.up*Mathf.Sin(age*18)*.018f*PresentationMotion.Intensity;
            model.localScale=Vector3.one*Mathf.Lerp(.55f,1,PresentationMotion.Out(age/.075f));
            if(trail!=null){trail.emitting=dt>0;trail.time=returning?.18f:.12f;trail.widthMultiplier=.5f+.5f*PresentationMotion.Intensity;}
            if(b.hitTargets.Count>contacts){contacts=b.hitTargets.Count;PresentationFx.Emit(b.position,color,.45f,FeedbackKind.Impact,b.direction);}
        }
        public void Retire(bool impact)
        {
            if(retired)return;retired=true;
            while(retiring.Count>=32){var old=retiring[0];retiring.RemoveAt(0);if(old!=null)Destroy(old.gameObject);}retiring.Add(this);
            if(impact)PresentationFx.Emit(transform.position,color,.55f,FeedbackKind.Impact,direction);
            if(trail!=null)trail.emitting=false;
        }
        void Update(){if(!retired)return;retireAge+=PresentationMotion.Delta;model.localScale=Vector3.one*(1-PresentationMotion.Ease(retireAge/.14f));if(retireAge>=.18f)Destroy(gameObject);}
        void OnDestroy(){retiring.Remove(this);}
    }
}
