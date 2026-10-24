using System;
using UnityEngine;
namespace Era.Editor
{
    public static class PresentationChecks
    {
        public static int Run()
        {
            int count=0;void Check(bool ok,string label){count++;if(!ok)throw new Exception("Animation check: "+label);}
            float Sample(int fps){float value=0;for(int i=0;i<fps;i++)value=PresentationMotion.Damp(value,1,8,1f/fps);return value;}
            Check(Mathf.Abs(Sample(30)-Sample(144))<.00001f,"exponential smoothing independent of frame rate");
            Check(PresentationMotion.Damp(.3f,1,20,0)==.3f,"paused smoothing stable");
            Check(ActorMotionMath.Anticipation(.8f,.8f)==0&&ActorMotionMath.Anticipation(.01f,.8f)>.99f,"windup advances toward release");
            Check(ActorMotionMath.Recovery(.4f,.4f)==1&&ActorMotionMath.Recovery(0,.4f)==0,"recovery settles");
            foreach(EnemyKind kind in Enum.GetValues(typeof(EnemyKind))){var p=ActorMotionProfile.For(kind);Check(p.cadence>0&&p.weight>0&&!float.IsNaN(p.bob),"motion profile "+kind);}
            Check(ActorMotionProfile.For(EnemyKind.Slime).death!=ActorMotionProfile.For(EnemyKind.Skeleton).death,"distinct creature deaths");
            var hud=new HudMotion();for(int i=0;i<2000;i++)hud.Value("transient/"+i,1);Check(hud.Count==HudMotion.Capacity,"HUD bounded");hud.Tick(4);Check(hud.Count==0,"HUD old channels retired");
            var health=new HudHealthMeter();health.Set(1);health.Set(.4f);health.Tick(.1f);Check(health.Trail>health.Value&&health.Value>.4f,"health damage trail");health.Tick(2);Check(Mathf.Abs(health.Value-.4f)<.001f&&health.Trail<.401f,"health trail settles");
            Check(HudMotion.Envelope(0,1)==0&&HudMotion.Envelope(1,1)==0&&HudMotion.Envelope(.5f,1)>.99f,"UI entry exit envelope");
            Check(PresentationFx.Capacity*28<=1344&&EffectPresentation.Capacity<=96,"bounded particle and contour budget");
            return count;
        }
    }
}
