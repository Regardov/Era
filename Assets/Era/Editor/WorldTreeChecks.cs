using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Era.Editor
{
    public static class WorldTreeChecks
    {
        [Serializable]sealed class PackedReport{public string version="0.30.0";public bool noAuthoringLibrary;public int assetCount;public string[] sourceAssets;}
        public static void Run()
        {
            var prefab=Resources.Load<GameObject>("Environment/Yggdrasil");
            if(prefab==null||prefab.GetComponent<LODGroup>().GetLODs().Length!=3)throw new Exception("World tree prefab/LOD missing");
            if(GameApp.MinCameraPitch>-70||GameApp.MaxCameraPitch!=65)throw new Exception("Camera cannot look up");
            int previous=WorldMap.Stage;
            for(int stage=1;stage<=CampaignData.StageCount;stage++)
            {
                WorldMap.SetStage(stage);
                if(WorldMap.ClearLocation(Vector3.zero,.45f))throw new Exception("Tree trunk accepts spawn");
                var resolved=WorldMap.Resolve(Vector3.zero,Vector3.zero,.45f);
                if(WorldTreeLayout.ContainsTrunk(resolved,.44f))throw new Exception("Tree collision unresolved");
                var spawn=WorldMap.RandomClearLocation(new System.Random(71+stage),5,Vector3.zero,30);
                if(WorldTreeLayout.ContainsTrunk(spawn,5))throw new Exception("World tree blocks player spawn");
                if(!WorldMap.ClearLocation(WorldMap.OnGround(0,-24),1))throw new Exception("Sanctuary access blocked");
            }
            WorldMap.SetStage(previous);Debug.Log("ERA_WORLD_TREE_EDITOR_OK seven realms, solid trunk, safe spawn, sanctuary and LOD");
        }
        public static void RecordBuild(BuildReport report)
        {
            var sources=report.packedAssets.SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p).ToArray();
            if(sources.Length==0)throw new Exception("Unity returned no packed asset details for this incremental build. Repeat Tools/Build.ps1 -CleanCache before packaging.");
            var forbidden=sources.Where(p=>p.Contains("ArtSource/")||p.Contains("CharacterFactory")||p.Contains("ERA_CharacterLibrary")||p.EndsWith(".blend")||p.EndsWith(".py")).ToArray();
            if(forbidden.Length>0)throw new Exception("Authoring library included in Player: "+string.Join(",",forbidden));
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/player-content-v029.json",JsonUtility.ToJson(new PackedReport{noAuthoringLibrary=true,assetCount=sources.Length,sourceAssets=sources},true));
        }
    }
}
