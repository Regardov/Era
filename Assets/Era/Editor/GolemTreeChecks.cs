using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class GolemTreeChecks
    {
        static int assertions;
        static void Check(bool value,string message){assertions++;if(!value)throw new Exception("ERA golem/tree: "+message);}
        public static void ProjectChecks()
        {
            EditorSceneManager.OpenScene("Assets/Era/Scenes/Rift.unity");SimulationChecks.Run();WorldTreeChecks.Run();Run();
        }
        public static void PrepareAndCheck()
        {
            AssetDatabase.Refresh();GolemImporter.Prepare();WorldTreeImporter.Prepare();Run();RenderGolem();AssetDatabase.SaveAssets();
        }
        public static void Run()
        {
            assertions=0;var collision=WorldTreeLayout.Collision;
            Check(collision!=null&&collision.hulls.Length>=10,"measured collision asset");
            int outside=0;
            foreach(var h in collision.hulls)
            {
                var center=h.points.Aggregate(Vector2.zero,(a,b)=>a+b)/h.points.Length;
                foreach(var v in h.points)
                {
                    var point=Vector2.Lerp(v,center,.05f);var p=new Vector3(point.x,0,point.y);
                    if(point.magnitude>16.8f&&WorldTreeLayout.ContainsTrunk(p,.4f))outside++;
                }
            }
            Check(outside>0,"solid roots outside the old circular collider are covered");
            int previous=WorldMap.Stage;
            for(int stage=1;stage<=7;stage++)
            {
                WorldMap.SetStage(stage);
                for(int x=-55;x<=55;x+=3)for(int z=-55;z<=55;z+=3)
                {
                    var p=WorldMap.OnGround(x,z);if(!WorldTreeLayout.ContainsTrunk(p,.4f))continue;
                    var q=WorldTreeLayout.ResolveTrunk(p,.4f);Check(!WorldTreeLayout.ContainsTrunk(q,.399f),"valid outside recovery "+stage+" / "+x+" / "+z);
                }
                var state=new WorldState();
                for(int a=0;a<16;a++)
                {
                    var d=Quaternion.Euler(0,a*22.5f,0)*Vector3.forward;var start=WorldMap.OnGround(d.x*60,d.z*60);
                    var stop=ActorBodies.Move(state,start,-d*120,new ActorCapsule(.4f,2.23f,true));
                    Check(!WorldTreeLayout.ContainsTrunk(stop,.399f)&&Vector3.Dot(stop,d)>0,"swept run/dash/knockback cannot cross tree "+stage+" / "+a);
                }
                Check(WorldMap.ClearLocation(WorldMap.OnGround(0,-24),1),"sanctuary remains accessible "+stage);
            }
            WorldMap.SetStage(previous);
            var source=Resources.Load<GameObject>("Creatures/StoneGolem");Check(source!=null,"existing enemy prefab retained");var clone=Object.Instantiate(source);
            try
            {
                var golem=clone.GetComponent<GolemMotion>();Check(golem!=null&&golem.sourceTriangles==2880&&golem.joints.Length==15,"actual library model and articulated joints");
                Check(clone.GetComponent<CreatureMotion>().golem==golem&&clone.GetComponent<ActorBodyCollider>()!=null,"existing gameplay and collider components retained");
                Check(clone.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)==2880,"complete original geometry");
                foreach(float phase in new[]{-1f,0,.25f,.65f,.85f,1,1.25f,1.7f,2})
                {
                    golem.Pose(phase,0,0);
                    foreach(var joint in golem.joints)Check(ActorMotionMath.Finite(joint.position)&&joint.position.magnitude<5,"finite connected pose "+phase+" / "+joint.name);
                    foreach(string foot in new[]{"Foot_L","Foot_R"})Check(Vector3.Distance(golem.joints.First(t=>t.name==foot).position,new Vector3(foot=="Foot_L"?-.35f:.35f,.27f,-.025f))<.003f,"planted foot during slam "+phase);
                }
                golem.Pose(.65f,0,0);float raised=golem.joints.First(t=>t.name=="Hand_R").position.y;golem.Pose(1,0,0);float contact=golem.joints.First(t=>t.name=="Hand_R").position.y;
                Check(raised>2.5f&&contact<.65f&&raised-contact>1.9f,"readable overhead raise and ground strike");
            }
            finally{Object.DestroyImmediate(clone);}
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/golem-tree-editor.json","{\"ok\":true,\"assertions\":"+assertions+",\"collisionPieces\":"+collision.hulls.Length+",\"rootSamplesBeyondOldRadius\":"+outside+"}");
            Debug.Log("ERA_GOLEM_TREE_CHECKS_OK "+assertions);
        }
        public static void RenderGolem()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var golem=Object.Instantiate(Resources.Load<GameObject>("Creatures/StoneGolem"));
            var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.transform.rotation=Quaternion.Euler(35,-30,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.67f,.70f);key.transform.rotation=Quaternion.Euler(35,150,0);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.53f,.57f);camera.orthographic=true;camera.orthographicSize=1.8f;camera.transform.position=new Vector3(4,2.7f,6);camera.transform.LookAt(new Vector3(0,1.4f,.3f));
            Directory.CreateDirectory("Artifacts/Golem-v0292");var rt=new RenderTexture(800,900,24);camera.targetTexture=rt;
            try{foreach(var pair in new[]{(-1f,"Idle"),(.65f,"Windup"),(1f,"Contact"),(1.6f,"Recovery")})
            {golem.GetComponent<GolemMotion>().Pose(pair.Item1,0,0);camera.Render();RenderTexture.active=rt;var tex=new Texture2D(800,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,800,900),0,0);tex.Apply();File.WriteAllBytes("Artifacts/Golem-v0292/"+pair.Item2+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}}
            finally{RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
