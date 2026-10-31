using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(210)]
    public sealed class WorldTreeDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Sample{public int stage;public bool centred,dimensions,oneInstance,lods,solidTrunk,clearSanctuary,opaque,leafShadowsOff;public float height,crown,skyCovered;public int skyPixels,leafOnSkyPixels;}
        [Serializable]sealed class Report{public string version="0.29.0";public bool ok;public List<Sample> worlds=new List<Sample>();public float fpsWithTree,fpsWithoutTree;public string skyMethod="Replacement masks: foliage pixels over otherwise visible sky, upper screen half, real gameplay camera at 90m / pitch 8";}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;
        string folder;int stage=1,phase;float deadline,benchStarted;int benchFrames;WorldTreeView tree;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:Path.Combine(Application.dataPath,"../Artifacts/WorldTree");Directory.CreateDirectory(folder);
            App.SelectedHero=Hero.Lumen;App.Solo();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=150;SetStage(1);deadline=Time.realtimeSinceStartup+3;
        }
        void SetStage(int value)
        {
            App.State.stage=value;App.State.stageSerial++;App.State.stageElapsed=0;App.State.enemies.Clear();App.State.spawnTimer=100000;
            WorldMap.SetStage(value);App.LocalPlayer.position=WorldMap.OnGround(0,-90);App.LocalPlayer.velocity=Vector3.zero;
            App.CameraYaw=0;App.CameraPitch=8;
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name+".png"));
        Texture2D Mask(int layers)
        {
            var cam=App.View.Camera;var flags=cam.clearFlags;var color=cam.backgroundColor;int mask=cam.cullingMask;var target=cam.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(256,144,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            try
            {
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.cullingMask=layers;cam.targetTexture=rt;
                cam.RenderWithShader(Shader.Find("Hidden/Era/WorldTreeMask"),"");RenderTexture.active=rt;
                var tex=new Texture2D(256,144,TextureFormat.RGB24,false,true);tex.ReadPixels(new Rect(0,0,256,144),0,0);tex.Apply();return tex;
            }
            finally{cam.clearFlags=flags;cam.backgroundColor=color;cam.cullingMask=mask;cam.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        Sample Measure()
        {
            tree=FindAnyObjectByType<WorldTreeView>();var b=tree.ModelBounds;
            var result=new Sample{stage=stage,centred=new Vector2(tree.transform.position.x,tree.transform.position.z).magnitude<.01f,
                dimensions=Mathf.Abs(b.max.y-WorldTreeLayout.Height)<.5f&&Mathf.Max(b.size.x,b.size.z)<WorldTreeLayout.CrownDiameter+1,
                oneInstance=FindObjectsByType<WorldTreeView>(FindObjectsSortMode.None).Length==1,
                lods=tree.Levels.GetLODs().Length==3,solidTrunk=WorldTreeLayout.ContainsTrunk(Vector3.zero,.45f)&&!WorldTreeLayout.ContainsTrunk(WorldMap.Resolve(Vector3.zero,Vector3.zero,.45f),.44f),
                clearSanctuary=WorldMap.ClearLocation(WorldMap.OnGround(0,-24),1),height=b.max.y,crown=Mathf.Max(b.size.x,b.size.z),opaque=true,leafShadowsOff=true};
            foreach(var renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var m in renderer.sharedMaterials)result.opaque&=m.renderQueue<2500&&m.color.a>.99f;
                if(renderer.gameObject.layer==WorldTreeView.FoliageLayer)result.leafShadowsOff&=renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var scene=Mask(~((1<<WorldTreeView.WoodLayer)|(1<<WorldTreeView.FoliageLayer)));
            var leaves=Mask(1<<WorldTreeView.FoliageLayer);var a=scene.GetPixels32();var c=leaves.GetPixels32();
            for(int y=72;y<144;y++)for(int x=0;x<256;x++)
            {int n=y*256+x;if(a[n].r<32){result.skyPixels++;if(c[n].r>128)result.leafOnSkyPixels++;}}
            result.skyCovered=result.skyPixels>0?(float)result.leafOnSkyPixels/result.skyPixels:1;
            Destroy(scene);Destroy(leaves);return result;
        }
        void LateUpdate()
        {
            if(App?.LocalPlayer==null)return;
            // GameHud captures on a later render callback: hold the diagnostic overview
            // across those frames instead of letting normal follow overwrite it immediately.
            if(phase==2)
            {
                var overview=App.View.Camera;overview.transform.position=new Vector3(65,43,-82);
                overview.transform.LookAt(new Vector3(0,23,0));
            }
            App.State.spawnTimer=100000;App.State.enemies.Clear();App.LocalPlayer.invulnerability=10000;
            if(phase>=4)benchFrames++;
            if(Time.realtimeSinceStartup<deadline)return;
            if(phase==0)
            {
                report.worlds.Add(Measure());Shot("World-"+stage+"-gameplay");phase=1;deadline=Time.realtimeSinceStartup+.4f;
            }
            else if(phase==1)
            {
                var cam=App.View.Camera;cam.transform.position=new Vector3(65,43,-82);cam.transform.LookAt(new Vector3(0,23,0));
                Shot("World-"+stage+"-overview");phase=2;deadline=Time.realtimeSinceStartup+.4f;
            }
            else if(phase==2)
            {
                if(stage<CampaignData.StageCount){stage++;SetStage(stage);phase=0;deadline=Time.realtimeSinceStartup+2;}
                else{SetStage(1);phase=3;deadline=Time.realtimeSinceStartup+3;benchStarted=Time.realtimeSinceStartup;benchFrames=0;}
            }
            else if(phase==3)
            {
                phase=4;deadline=Time.realtimeSinceStartup+3;benchStarted=Time.realtimeSinceStartup;benchFrames=0;
            }
            else if(phase==4)
            {
                report.fpsWithTree=benchFrames/(Time.realtimeSinceStartup-benchStarted);
                tree.gameObject.SetActive(false);phase=5;deadline=Time.realtimeSinceStartup+3;benchStarted=Time.realtimeSinceStartup;benchFrames=0;
            }
            else
            {
                report.fpsWithoutTree=benchFrames/(Time.realtimeSinceStartup-benchStarted);tree.gameObject.SetActive(true);
                report.ok=report.worlds.Count==7&&report.worlds.TrueForAll(s=>s.centred&&s.dimensions&&s.oneInstance&&s.lods&&s.solidTrunk&&s.clearSanctuary&&s.opaque&&s.leafShadowsOff&&s.skyPixels>0&&s.skyCovered<.65f);
                File.WriteAllText(Path.Combine(folder,"world-tree.json"),JsonUtility.ToJson(report,true));
                Debug.Log("ERA_WORLD_TREE_CHECK "+report.ok);Application.Quit(report.ok?0:2);enabled=false;
            }
        }
    }
}
