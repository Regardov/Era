using UnityEngine;

namespace Era
{
    public sealed class LivingGrass:MonoBehaviour
    {
        public Renderer[] patches;
        float cullTimer;
        void LateUpdate()
        {
            var app=GameApp.Instance;var state=app?.State;
            for(int i=0;i<GameData.MaxPlayers;i++)
            {
                var p=state?.Player(i);bool live=p!=null&&!p.eliminated;
                var point=live?p.position:new Vector3(10000,10000,10000);
                // The feet bend nearby blades even while standing still; jump height restores them.
                Shader.SetGlobalVector("_GrassBend"+i,new Vector4(point.x,point.y,point.z,live?1:0));
            }
            if((cullTimer-=Time.unscaledDeltaTime)>0)return;cullTimer=.3f;
            var camera=app?.View?.Camera;if(camera==null)return;
            foreach(var patch in patches)if(patch!=null)
                patch.enabled=(patch.bounds.ClosestPoint(camera.transform.position)-camera.transform.position).sqrMagnitude<85*85;
        }
    }
}
