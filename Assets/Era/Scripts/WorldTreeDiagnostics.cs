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
        [Serializable]sealed class CameraSample{public float requestedPitch,forwardY,groundClearance;public bool finite,characterCentred;public float focusViewportX,focusViewportY;}
        [Serializable]sealed class Report
        {
            public string version=Application.version;public bool ok,mouseLookClamps;public List<Sample> worlds=new List<Sample>();
            public List<CameraSample> camera=new List<CameraSample>();public float fpsWithTree,fpsWithoutTree;
            public string skyMethod="Replacement masks: foliage pixels over otherwise visible sky, upper screen half, real gameplay camera at 90m / pitch 8. Look-up captures use the ordinary follow camera.";
        }
        readonly Report report=new Report();GameApp App=>GameApp.Instance;
        readonly float[] pitches={-75,-45,0,7.9f,8.1f,65};
        string folder;int stage=1,phase,cameraIndex,settledFrames;float deadline,benchStarted;int benchFrames;WorldTreeView tree;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--capture-folder");folder=i>=0?args[i+1]:Path.Combine(Application.dataPath,"../Artifacts/WorldTree");Directory.CreateDirectory(folder);
            App.SelectedHero=Hero.Lumen;App.Solo();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;
            float yaw=App.CameraYaw;
            App.ApplyMouseLook(0,10000);bool up=App.CameraPitch==GameApp.MinCameraPitch;
            App.ApplyMouseLook(0,-10000);report.mouseLookClamps=up&&App.CameraPitch==GameApp.MaxCameraPitch;App.CameraYaw=yaw;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=150;SetStage(1);deadline=Time.realtimeSinceStartup+3;
        }
        void SetStage(int value)
        {
            settledFrames=0;
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
                dimensions=Mathf.Abs(b.max.y-tree.transform.position.y-WorldTreeLayout.Height)<.5f&&Mathf.Max(b.size.x,b.size.z)<WorldTreeLayout.CrownDiameter+1,
                oneInstance=FindObjectsByType<WorldTreeView>(FindObjectsSortMode.None).Length==1,
                lods=tree.Levels.GetLODs().Length==3,solidTrunk=WorldTreeLayout.ContainsTrunk(Vector3.zero,.45f)&&!WorldTreeLayout.ContainsTrunk(WorldMap.Resolve(Vector3.zero,Vector3.zero,.45f),.44f),
                clearSanctuary=WorldMap.ClearLocation(WorldMap.OnGround(0,-24),1),height=b.max.y-tree.transform.position.y,crown=Mathf.Max(b.size.x,b.size.z),opaque=true,leafShadowsOff=true};
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
            App.State.spawnTimer=100000;App.State.enemies.Clear();App.LocalPlayer.invulnerability=10000;
            if(phase==5||phase==6)benchFrames++;
            settledFrames++;
            if(Time.realtimeSinceStartup<deadline||settledFrames<90)return;
            if(phase==0)
            {
                report.worlds.Add(Measure());Shot("World-"+stage+"-gameplay");phase=1;deadline=Time.realtimeSinceStartup+.5f;
            }
            else if(phase==1){App.CameraPitch=-45;phase=2;deadline=Time.realtimeSinceStartup+1.5f;}
            else if(phase==2){Shot("World-"+stage+"-look-up");phase=3;deadline=Time.realtimeSinceStartup+.5f;}
            else if(phase==3)
            {
                if(stage<CampaignData.StageCount){stage++;SetStage(stage);phase=0;deadline=Time.realtimeSinceStartup+2;}
                else{SetStage(1);App.CameraPitch=pitches[0];phase=4;deadline=Time.realtimeSinceStartup+2;}
            }
            else if(phase==4)
            {
                var cam=App.View.Camera;var pos=cam.transform.position;
                var focus=cam.WorldToViewportPoint(App.View.CameraFocus);
                report.camera.Add(new CameraSample{characterCentred=focus.z>0&&Mathf.Abs(focus.x-.5f)<.015f&&Mathf.Abs(focus.y-.5f)<.015f,focusViewportX=focus.x,focusViewportY=focus.y,requestedPitch=pitches[cameraIndex],forwardY=cam.transform.forward.y,groundClearance=pos.y-WorldMap.Ground(pos),finite=ActorMotionMath.Finite(pos)&&ActorMotionMath.Finite(cam.transform.forward)});
                Shot("Camera-pitch-"+cameraIndex);
                if(++cameraIndex<pitches.Length){App.CameraPitch=pitches[cameraIndex];settledFrames=0;deadline=Time.realtimeSinceStartup+1.5f;}
                else{App.CameraPitch=8;phase=5;deadline=Time.realtimeSinceStartup+3;benchStarted=Time.realtimeSinceStartup;benchFrames=0;}
            }
            else if(phase==5)
            {
                report.fpsWithTree=benchFrames/(Time.realtimeSinceStartup-benchStarted);
                tree.gameObject.SetActive(false);phase=6;deadline=Time.realtimeSinceStartup+3;benchStarted=Time.realtimeSinceStartup;benchFrames=0;
            }
            else
            {
                report.fpsWithoutTree=benchFrames/(Time.realtimeSinceStartup-benchStarted);tree.gameObject.SetActive(true);
                report.ok=report.worlds.Count==7&&report.worlds.TrueForAll(s=>s.centred&&s.dimensions&&s.oneInstance&&s.lods&&s.solidTrunk&&s.clearSanctuary&&s.opaque&&s.leafShadowsOff&&s.skyPixels>0&&s.skyCovered<.65f)
                    &&report.mouseLookClamps&&report.camera.Count==pitches.Length&&report.camera.TrueForAll(s=>s.finite&&s.groundClearance>.6f&&s.characterCentred)
                    &&report.camera[0].forwardY>.85f&&report.camera[1].forwardY>.5f&&Mathf.Abs(report.camera[2].forwardY)<.1f&&report.camera[5].forwardY<-.8f
                    &&Mathf.Abs(report.camera[3].forwardY-report.camera[4].forwardY)<.02f;
                File.WriteAllText(Path.Combine(folder,"world-tree.json"),JsonUtility.ToJson(report,true));
                Debug.Log("ERA_WORLD_TREE_CHECK "+report.ok);Application.Quit(report.ok?0:2);enabled=false;
            }
        }
    }
}
