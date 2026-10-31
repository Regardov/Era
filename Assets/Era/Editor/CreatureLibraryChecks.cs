using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Era.Editor
{
    public static class CreatureLibraryChecks
    {
        static int assertions;
        static void Check(bool b,string m){assertions++;if(!b)throw new Exception("ERA creature library: "+m);}
        public static void PrepareAndRender(){AssetDatabase.Refresh();GolemImporter.Prepare();Run();Render();AssetDatabase.SaveAssets();}
        public static void Run()
        {
            assertions=0;
            foreach(var kind in new[]{EnemyKind.StoneGolem,EnemyKind.CrystalGolem,EnemyKind.Bat})
            {
                var original=Resources.Load<GameObject>("Creatures/"+kind);Check(original!=null,"prefab "+kind);var root=Object.Instantiate(original);
                try
                {
                    var filters=root.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
                    Check(triangles>=1500&&triangles<4500,"low poly budget "+kind);Check(root.GetComponent<ActorBodyCollider>()!=null,"existing body component "+kind);
                    foreach(var f in filters){var m=f.sharedMesh;Check(m.vertices.All(ActorMotionMath.Finite)&&m.normals.All(n=>n.sqrMagnitude>.98f&&n.sqrMagnitude<1.02f),"finite flat authored normals "+f.name);Check(m.colors.Length==m.vertexCount&&m.colors.All(c=>c.a>.99f),"opaque palette "+f.name);}
                    var golem=root.GetComponent<GolemMotion>();
                    if(golem!=null)
                    {
                        float minZ=100,maxZ=-100,minY=100,maxY=-100;
                        for(int frame=0;frame<64;frame++)
                        {
                            golem.Pose(-1,frame*Mathf.PI*2/64,1);var foot=golem.joints.First(t=>t.name=="Foot_L");minZ=Mathf.Min(minZ,foot.position.z);maxZ=Mathf.Max(maxZ,foot.position.z);minY=Mathf.Min(minY,foot.position.y);maxY=Mathf.Max(maxY,foot.position.y);
                            foreach(var filter in foot.GetComponentsInChildren<MeshFilter>())foreach(var p in filter.sharedMesh.vertices)Check(filter.transform.TransformPoint(p).y>=-.003f,"sole does not enter floor "+kind+" / "+frame);
                        }
                        Check(maxZ-minZ>.65f&&maxY-minY>.10f,"readable full stride "+kind);
                        foreach(float phase in new[]{0f,.3f,.65f,.85f,1f,1.6f,2f}){golem.Pose(phase,0,0);foreach(var t in golem.joints)Check(ActorMotionMath.Finite(t.position)&&t.position.magnitude<5,"safe attack pose "+kind);}
                    }
                    else
                    {
                        var bat=root.GetComponent<BatMotion>();Check(bat!=null&&root.GetComponent<CreatureMotion>().bat==bat&&bat.joints.Length==9,"bat joint controller");
                        float lo=100,hi=-100;
                        for(int frame=0;frame<48;frame++)
                        {
                            bat.Pose(frame*Mathf.PI*2/48,1,0);var tip=bat.joints.First(t=>t.name=="Tip_L");lo=Mathf.Min(lo,tip.position.y);hi=Mathf.Max(hi,tip.position.y);
                            foreach(var t in bat.joints)Check(ActorMotionMath.Finite(t.position)&&t.position.magnitude<3,"bat wing extremes");
                        }
                        Check(hi-lo>.6f,"large visible wing stroke");
                        foreach(float p in new[]{0f,.3f,.7f,1f}){bat.Pose(0,1,p);foreach(var t in bat.joints)Check(ActorMotionMath.Finite(t.position),"safe folded dive");}
                    }
                }
                finally{Object.DestroyImmediate(root);}
            }
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/creature-library-editor.json","{\"ok\":true,\"assertions\":"+assertions+"}");Debug.Log("ERA_CREATURE_LIBRARY_CHECKS_OK "+assertions);
        }
        public static void Render()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.61f,.66f);
            var light=new GameObject("Studio key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(35,150,0);
            var cam=new GameObject("Camera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.38f,.43f,.48f);Directory.CreateDirectory("Artifacts/Creatures-v030");
            foreach(var kind in new[]{EnemyKind.StoneGolem,EnemyKind.CrystalGolem,EnemyKind.Bat})
            {
                var root=Object.Instantiate(Resources.Load<GameObject>("Creatures/"+kind));bool flying=kind==EnemyKind.Bat;float center=flying?.5f:1.35f;cam.orthographicSize=flying?1.50f:1.7f;cam.transform.position=new Vector3(flying?2.5f:4,center+(flying?1.4f:1.0f),flying?4:6);cam.transform.LookAt(new Vector3(0,center,0));
                for(int i=0;i<3;i++)
                {
                    var golem=root.GetComponent<GolemMotion>();if(golem!=null)golem.Pose(-1,i*Mathf.PI*.5f,1);else root.GetComponent<BatMotion>().Pose(i*Mathf.PI*.5f,1,0);
                    var rt=new RenderTexture(800,900,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(800,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,800,900),0,0);tex.Apply();File.WriteAllBytes("Artifacts/Creatures-v030/"+kind+"-"+i+".png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
                }
                Object.DestroyImmediate(root);
            }
            EditorSceneManager.CloseScene(scene,true);
        }
    }
}
