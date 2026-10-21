using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Era.Editor
{
    // Workspace-only commands let an already open editor build without closing user scenes.
    [InitializeOnLoad]
    public static class EditorBridge
    {
        [Serializable]sealed class Request { public string id,command; }
        [Serializable]sealed class Reply { public string id,command,scene,message;public bool ok,playing,dirty,compiling; }
        static double nextPoll;static bool busy;
        static EditorBridge(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(busy||EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+.5;
            const string path="Artifacts/editor-request.json";if(!File.Exists(path)||EditorApplication.isCompiling)return;
            Request request;
            try{request=JsonUtility.FromJson<Request>(File.ReadAllText(path));}catch{return;}
            if(request==null||string.IsNullOrEmpty(request.id))return;
            if(request.command!="status"&&EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            busy=true;File.Delete(path);
            var scene=EditorSceneManager.GetActiveScene();var reply=new Reply{id=request.id,command=request.command,scene=scene.path,dirty=scene.isDirty,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling};
            try
            {
                switch(request.command)
                {
                    case "status":break;
                    case "build":EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();BuildTools.BuildWindows();break;
                    case "checks":SimulationChecks.Run();break;
                    default:throw new ArgumentException("Unknown workspace editor command");
                }
                reply.ok=true;reply.message="Complete";
            }
            catch(Exception e){reply.message=e.ToString();Debug.LogException(e);}
            finally{Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/editor-reply.json",JsonUtility.ToJson(reply,true));busy=false;}
        }
    }
}
