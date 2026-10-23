using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class CharacterDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report{public bool ok,skins,clips,crouch;public int models;}
        static readonly string[] Names={"Wayfarer","DuskMage","Stalker","Wisp","Ember","Lumen","Echo"};
        GameApp App=>GameApp.Instance;GameObject[] models=new GameObject[7];string folder;float started,next;int phase;FrameCapture capture;Font font;
        readonly Report report=new Report{skins=true,clips=true};
        void Start()
        {
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            App.Solo();App.State.spawnTimer=100000;App.State.resourceNodes.Clear();App.LocalPlayer.invulnerability=10000;App.LocalPlayer.position=WorldMap.OnGround(0,-150);App.LocalPlayer.weapons=new[]{-1,-1};
            var environment=FindAnyObjectByType<WorldEnvironment>();environment.gameObject.SetActive(false);
            var light=new GameObject("Character studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.95f;light.transform.rotation=Quaternion.Euler(32,-30,0);light.shadows=LightShadows.Soft;
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.60f,.64f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Character studio floor";floor.transform.position=new Vector3(0,-.07f,0);floor.transform.localScale=new Vector3(16,.1f,12);floor.GetComponent<Renderer>().material.color=new Color(.76f,.77f,.73f);
            for(int i=0;i<7;i++)
            {
                string path=(i==2||i==3?"Creatures/":i<2?"Characters/Library/":"Characters/")+Names[i];models[i]=Instantiate(Resources.Load<GameObject>(path));
                var skeleton=models[i].GetComponent<SkeletalActor>();skeleton.animationPlayer.enabled=false;skeleton.enabled=false;models[i].GetComponent<HumanRigShape>().enabled=false;
                report.skins&=models[i].GetComponent<AuthoredCharacter>()?.model==Names[i]&&models[i].GetComponentsInChildren<SkinnedMeshRenderer>().Length==1;
                report.clips&=skeleton.animationPlayer.GetClipCount()>=15;models[i].SetActive(false);
            }
            font=Font.CreateDynamicFontFromOSFont("Arial",16);capture=new FrameCapture(font);started=Time.realtimeSinceStartup;next=started+1.5f;
        }
        void Pose(GameObject model,string clip,float time,bool crouch=false)
        {
            var shape=model.GetComponent<HumanRigShape>();var skeleton=model.GetComponent<SkeletalActor>();
            for(int i=0;i<shape.bones.Length;i++)shape.bones[i].localPosition=shape.originalLocalPositions[i];
            skeleton.animationPlayer.GetClip(clip).SampleAnimation(model,time);
            if(crouch){skeleton.hips.localPosition+=Vector3.down*.22f;skeleton.spine.localRotation*=Quaternion.Euler(16,0,0);skeleton.leftThigh.localRotation*=Quaternion.Euler(-65,0,0);skeleton.rightThigh.localRotation*=Quaternion.Euler(-65,0,0);skeleton.leftKnee.localRotation*=Quaternion.Euler(130,0,0);skeleton.rightKnee.localRotation*=Quaternion.Euler(130,0,0);}
            shape.ApplyPose();
            shape.PlantFeet(crouch?1:0);
        }
        void LateUpdate()
        {
            var camera=App.View.Camera;camera.GetComponent<StillwaterLens>().enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.88f,.89f,.86f);camera.orthographic=true;
            if(Time.realtimeSinceStartup<next)return;
            foreach(var model in models)model.SetActive(false);
            if(phase<7)
            {
                var model=models[phase];model.SetActive(true);model.transform.position=Vector3.zero;model.transform.rotation=Quaternion.Euler(0,-18,0);Pose(model,"Idle",.1f);
                camera.transform.position=new Vector3(0,1.5f,5);camera.transform.LookAt(Vector3.up*1.25f);camera.orthographicSize=1.45f;
                capture.Save(Path.Combine(folder,"character-"+Names[phase]+"-v016.png"),camera,()=>{});report.models++;
            }
            else if(phase==7||phase==8)
            {
                for(int i=0;i<3;i++){var model=models[4+i];model.SetActive(true);model.transform.position=Vector3.right*(i-1)*1.5f;model.transform.rotation=Quaternion.identity;Pose(model,phase==7?"Running_A":"Idle",.18f,phase==8);}
                camera.transform.position=new Vector3(0,1.5f,6);camera.transform.LookAt(Vector3.up*1.15f);camera.orthographicSize=1.5f;
                capture.Save(Path.Combine(folder,phase==7?"characters-running-v016.png":"characters-crouching-v016.png"),camera,()=>{});
                if(phase==8)report.crouch=models[4].GetComponent<SkeletalActor>().leftKnee.localRotation.eulerAngles.x>80;
            }
            else
            {
                report.ok=report.models==7&&report.skins&&report.clips&&report.crouch;File.WriteAllText(Path.Combine(folder,"smoke-characters.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_CHARACTERS "+JsonUtility.ToJson(report));Application.Quit(report.ok?0:2);enabled=false;return;
            }
            phase++;next=Time.realtimeSinceStartup+.7f;
            if(Time.realtimeSinceStartup-started>35)Application.Quit(3);
        }
        void OnDestroy(){capture?.Dispose();if(font!=null)Destroy(font);}
    }
}
