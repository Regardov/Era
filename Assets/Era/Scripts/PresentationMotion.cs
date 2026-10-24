using UnityEngine;
namespace Era
{
    // A separate presentation clock: hit-stop never pauses combat or networking.
    public static class PresentationMotion
    {
        static float intensity=-1;
        public static float Delta=>GameApp.Instance?.WorldPaused==true?0:Mathf.Max(0,Time.unscaledDeltaTime);
        public static float Intensity {get {if(intensity<0)intensity=Mathf.Clamp01(PlayerPrefs.GetFloat("era-vfx-intensity",.75f));return intensity;} set {intensity=Mathf.Clamp01(value);PlayerPrefs.SetFloat("era-vfx-intensity",intensity);}}
        public static float Damp(float value,float target,float speed,float dt)=>Mathf.Lerp(value,target,1-Mathf.Exp(-Mathf.Max(0,speed)*Mathf.Max(0,dt)));
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        public static float Out(float t)=>1-Mathf.Pow(1-Mathf.Clamp01(t),3);
    }
}
