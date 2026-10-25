using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class EnvironmentDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report{public bool ok,transparentWater,depth,foliage,map,realmChange;public int streams,renderers;}
        readonly Report report=new Report();GameApp App=>GameApp.Instance;
        string folder;float started;int phase;Vector3 focus,offset;WorldProp tree;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-folder");folder=at>=0?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"..","Artifacts"));Directory.CreateDirectory(folder);
            started=Time.realtimeSinceStartup;I18n.Language=GameLanguage.Russian;App.SelectedHero=Hero.Ember;App.Solo();App.State.spawnTimer=100000;App.State.resourceNodes.Clear();
            App.LocalPlayer.invulnerability=10000;App.LocalPlayer.weapons=new[]{-1,-1};
            float z=-28;focus=new Vector3(WorldMap.RiverCenter(z,0),WorldMap.RiverWaterHeight(z,0),z);offset=new Vector3(9,6,-13);
            App.LocalPlayer.position=WorldMap.OnGround(focus.x-5,z-4);App.CameraYaw=-32;
        }
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name+"-v020.png"));
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;
            if(phase==0&&age>2)
            {
                var environment=FindAnyObjectByType<WorldEnvironment>();report.transparentWater=true;report.foliage=true;report.renderers=environment.GetComponentsInChildren<Renderer>().Length;
                foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                {
                    var material=renderer.sharedMaterial;
                    if(material.shader.name=="Era/Stream Water"){report.streams++;report.transparentWater&=material.shader.isSupported&&material.renderQueue>=3000&&material.GetTag("RenderType",false)=="Transparent";}
                    if(renderer.name.StartsWith("Living canopies")){var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;report.foliage&=material.shader.isSupported&&mesh.vertexCount>1000;}
                }
                report.depth=(App.View.Camera.depthTextureMode&DepthTextureMode.Depth)!=0;
                report.map=RealmCartography.UV(Vector3.zero)==Vector2.one*.5f&&RealmCartography.UV(Vector3.forward*100).y>.5f;
                Shot("clear-stream");phase++;
            }
            else if(phase==1&&age>3.5f){offset=new Vector3(6,10,-6);Shot("stream-bed");phase++;}
            else if(phase==2&&age>5)
            {
                tree=WorldMap.Props.Find(p=>p.kind==0&&p.size>1.3f&&p.size<1.8f&&Mathf.Abs(p.position.x)<160&&Mathf.Abs(p.position.z)<160);focus=tree.position+Vector3.up*5*tree.size;offset=new Vector3(9,1,-13)*tree.size;
                App.LocalPlayer.position=WorldMap.OnGround(tree.position.x-4,tree.position.z-3);Shot("branching-trees");phase++;
            }
            else if(phase==3&&age>6.5f){App.Inventory=true;Shot("square-realm-map");phase++;}
            else if(phase==4&&age>8)
            {
                App.Inventory=false;App.State.stage=3;App.State.stageSerial++;WorldMap.SetStage(3);phase++;
            }
            else if(phase==5&&age>10)
            {
                tree=WorldMap.Props.Find(p=>p.kind==0&&p.size>1.3f&&p.size<1.8f&&Mathf.Abs(p.position.x)<160&&Mathf.Abs(p.position.z)<160);focus=tree.position+Vector3.up*4.5f*tree.size;offset=new Vector3(9,2,-13)*tree.size;
                App.LocalPlayer.position=WorldMap.OnGround(tree.position.x-4,tree.position.z-3);report.realmChange=FindAnyObjectByType<WorldEnvironment>().stage==3;Shot("conifer-trees");phase++;
            }
            else if(phase==6&&age>11.5f){App.Inventory=true;Shot("square-map-realm3");phase++;}
            else if(phase==7&&age>13)
            {
                report.ok=report.transparentWater&&report.streams==32&&report.depth&&report.foliage&&report.map&&report.realmChange&&report.renderers<800;
                File.WriteAllText(Path.Combine(folder,"smoke-environment.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ENVIRONMENT "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
            }
            if(age>35)Application.Quit(3);
        }
        void LateUpdate(){var camera=App.View.Camera;camera.transform.position=focus+offset;camera.transform.LookAt(focus);camera.fieldOfView=55;}
    }
}
