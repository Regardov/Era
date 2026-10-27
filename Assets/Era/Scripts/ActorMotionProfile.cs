using UnityEngine;

namespace Era
{
    // Presentation data only: these profiles never change simulation speed, reach or timing.
    public enum ActorDeathStyle { Collapse, Shatter, Evaporate, Deflate, WingFall, Sovereign }

    public readonly struct ActorMotionProfile
    {
        public readonly float cadence,stride,bob,sway,breath,weight;
        public readonly ActorDeathStyle death;
        public ActorMotionProfile(float cadence,float stride,float bob,float sway,float breath,float weight,ActorDeathStyle death)
        {this.cadence=cadence;this.stride=stride;this.bob=bob;this.sway=sway;this.breath=breath;this.weight=weight;this.death=death;}
        public static ActorMotionProfile For(EnemyKind kind)
        {
            switch(kind)
            {
                case EnemyKind.Wisp:return new ActorMotionProfile(2.6f,20,.035f,3.2f,2.4f,.35f,ActorDeathStyle.Evaporate);
                case EnemyKind.Stalker:return new ActorMotionProfile(4.9f,33,.045f,1.8f,2.9f,.35f,ActorDeathStyle.Collapse);
                case EnemyKind.Warden:return new ActorMotionProfile(1.8f,21,.022f,1.1f,1.25f,1,ActorDeathStyle.Sovereign);
                case EnemyKind.StoneGolem:return new ActorMotionProfile(2,18,.035f,3.2f,.9f,1,ActorDeathStyle.Shatter);
                case EnemyKind.CrystalGolem:return new ActorMotionProfile(2.4f,21,.028f,2.4f,1.2f,.85f,ActorDeathStyle.Shatter);
                case EnemyKind.Ogre:case EnemyKind.Brute:return new ActorMotionProfile(2.2f,24,.035f,3.8f,1.5f,.95f,ActorDeathStyle.Collapse);
                case EnemyKind.OgreShaman:return new ActorMotionProfile(2.1f,19,.022f,2.4f,1.6f,.85f,ActorDeathStyle.Evaporate);
                case EnemyKind.Slime:return new ActorMotionProfile(5,0,.17f,0,2,.25f,ActorDeathStyle.Deflate);
                case EnemyKind.AcidSlime:return new ActorMotionProfile(4.5f,0,.12f,0,1.7f,.35f,ActorDeathStyle.Deflate);
                case EnemyKind.Bat:return new ActorMotionProfile(13,0,.035f,4.5f,3,.15f,ActorDeathStyle.WingFall);
                case EnemyKind.Gargoyle:return new ActorMotionProfile(7.2f,12,.05f,2.7f,1.5f,.7f,ActorDeathStyle.WingFall);
                case EnemyKind.Necromancer:return new ActorMotionProfile(2.3f,15,.018f,1.6f,1.4f,.35f,ActorDeathStyle.Evaporate);
                case EnemyKind.Revenant:return new ActorMotionProfile(2.9f,24,.027f,1.7f,1.3f,.8f,ActorDeathStyle.Collapse);
                case EnemyKind.SkeletonArcher:return new ActorMotionProfile(3.3f,28,.027f,2.2f,2,.3f,ActorDeathStyle.Shatter);
                default:return new ActorMotionProfile(3.1f,26,.03f,2.4f,1.8f,.35f,ActorDeathStyle.Shatter);
            }
        }
    }

    public static class ActorMotionMath
    {
        public static float Delta=>PresentationMotion.Delta;
        public static float Damp(float current,float target,float sharpness,float dt)=>Mathf.Lerp(current,target,1-Mathf.Exp(-Mathf.Max(0,sharpness)*Mathf.Max(0,dt)));
        public static float Ease(float value){value=Mathf.Clamp01(value);return value*value*(3-2*value);}
        public static float Anticipation(float remaining,float duration)=>remaining<=0?0:Ease(1-Mathf.Clamp01(remaining/Mathf.Max(.01f,duration)));
        // Resolve starts at the fully extended pose, then settles without a second damage cue.
        public static float Recovery(float remaining,float duration)=>Ease(Mathf.Clamp01(remaining/Mathf.Max(.01f,duration)));
        // Finish the visible stroke at the existing simulation contact time.
        // Previously the full anticipation pose jumped straight to recovery on that frame.
        public static void AttackPose(float remaining,float duration,float recovery,out float preparation,out float strike)
        {
            float progress=1-Mathf.Clamp01(remaining/Mathf.Max(.01f,duration));
            float active=remaining>0?Ease(Mathf.InverseLerp(.65f,1,progress)):0;
            preparation=Anticipation(remaining,duration)*(1-active);
            strike=remaining>0?active:recovery;
        }
        public static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);
    }
}
