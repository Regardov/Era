using System;
using System.IO;
using UnityEngine;
namespace Era
{
    [DefaultExecutionOrder(90)]
    public sealed class EnvironmentDiagnostics:MonoBehaviour
    {
        [Serializable]sealed class Report{public bool ok,transparentWater,depth,foliage,transparentFoliage,map,realmChange,cottages,leafPiles,threeAuras;public int streams,renderers,buildings;}
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
        void Shot(string name)=>App.GetComponent<GameHud>().RequestCapture(Path.Combine(folder,name+"-v0221.png"));
        void Update()
        {
            float age=Time.realtimeSinceStartup-started;
            if(phase==0&&age>2)
            {
                var environment=FindAnyObjectByType<WorldEnvironment>();report.transparentWater=true;report.foliage=true;report.transparentFoliage=true;report.renderers=environment.GetComponentsInChildren<Renderer>().Length;
                foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                {
                    var material=renderer.sharedMaterial;
                    if(material.shader.name=="Era/Stream Water"){report.streams++;report.transparentWater&=material.shader.isSupported&&material.renderQueue>=3000&&material.GetTag("RenderType",false)=="Transparent";}
                    if(renderer.name.StartsWith("Living canopies")){var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;report.foliage&=material.shader.isSupported&&mesh.vertexCount>1000;report.transparentFoliage&=material.renderQueue>=3000&&material.GetTag("RenderType",false)=="Transparent"&&material.GetFloat("_Opacity")>.3f&&material.GetFloat("_Opacity")<.8f&&Array.TrueForAll(mesh.uv,q=>q.x>-.5f);}
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
                App.Inventory=false;App.State.stage=1;App.State.stageSerial++;WorldMap.SetStage(1);var building=WorldMap.Buildings.Find(b=>!b.ruined);
                report.buildings=WorldMap.Buildings.Count;report.cottages=report.buildings>=16;
                focus=building.position+Vector3.up*2;offset=Quaternion.Euler(0,building.angle,0)*new Vector3(10,5,-13);
                var front=building.position+Quaternion.Euler(0,building.angle,0)*new Vector3(0,0,-5);
                App.LocalPlayer.position=WorldMap.OnGround(front.x,front.z-3);App.State.resourceNodes.Clear();
                App.State.resourceNodes.Add(new ResourceNodeState{id=App.State.nextId++,position=WorldMap.OnGround(front.x,front.z)});phase++;
            }
            else if(phase==8&&age>16){Shot("autumn-cottage");phase++;}
            else if(phase==9&&age>18)
            {
                var pile=GameObject.Find("Autumn leaf pile");var mesh=pile?.GetComponentInChildren<MeshFilter>().sharedMesh;
                report.leafPiles=mesh!=null&&mesh.vertexCount>600&&mesh.colors.Length==mesh.vertexCount&&pile.GetComponentInChildren<Renderer>().sharedMaterial.shader.isSupported;
                focus=App.State.resourceNodes[0].position+Vector3.up*.22f;offset=new Vector3(1.8f,1.7f,-2.6f);Shot("autumn-leaf-pile");phase++;
            }
            else if(phase==10&&age>20)
            {
                App.SelectedHero=Hero.Lumen;App.Solo();App.State.spawnTimer=100000;App.LocalPlayer.invulnerability=10000;
                App.Simulation.GrantExperience(GameData.ExperienceRequired(1));App.Simulation.ChooseItem(App.LocalPlayer,0);
                report.threeAuras=App.LocalPlayer.offerKind==RewardKind.Aura&&App.LocalPlayer.offers.Length==3;
                focus=App.LocalPlayer.position+Vector3.up;offset=new Vector3(10,8,-13);phase++;
            }
            else if(phase==11&&age>22){Shot("three-aura-choices");phase++;}
            else if(phase==12&&age>23.5f)
            {
                report.ok=report.transparentWater&&report.streams==32&&report.depth&&report.foliage&&report.transparentFoliage&&report.map&&report.realmChange&&report.renderers<800&&report.cottages&&report.leafPiles&&report.threeAuras;
                File.WriteAllText(Path.Combine(folder,"smoke-environment.json"),JsonUtility.ToJson(report,true));Debug.Log("ERA_ENVIRONMENT "+JsonUtility.ToJson(report));enabled=false;Application.Quit(report.ok?0:2);
            }
            if(age>48)Application.Quit(3);
        }
        void LateUpdate(){var camera=App.View.Camera;camera.transform.position=focus+offset;camera.transform.LookAt(focus);camera.fieldOfView=55;}
    }
}
