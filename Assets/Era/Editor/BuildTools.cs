using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Era.Editor
{
    [InitializeOnLoad]
    public static class BuildTools
    {
        const string ScenePath = "Assets/Era/Scenes/Rift.unity";
        static BuildTools() { EditorApplication.delayCall += EnsureScene; }

        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
            PrepareProject();
        }
        [MenuItem("ERA/Prepare project")]
        public static void PrepareProject()
        {
            Directory.CreateDirectory("Assets/Era/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("ERA · Press Play to enter the rift");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Era Studio";
            PlayerSettings.productName = "ERA - Rift";
            PlayerSettings.bundleVersion = "0.19.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.stripEngineCode = false;
            // Runtime-generated materials still need explicit shader references in the player build.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[] { "Standard", "Era/Character Surface", "Sprites/Default", "Era/Rift Portal", "Era/Actor Satin", "Era/Faceted Realm", "Era/Low Poly", "Era/Foliage", "Era/Realm Surface", "Era/Realm Terrain", "Era/Realm Leaves", "Era/Meadow Grass", "Era/Painted Sky", "Hidden/Era/Stillwater Lens", "Hidden/Era/Capture" })
            {
                var shader = Shader.Find(shaderName); bool found = false;
                if (shader == null) throw new Exception("Missing shader: " + shaderName);
                for (int i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (!found) { included.InsertArrayElementAtIndex(included.arraySize); included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            var waterShader=Shader.Find("Era/Stream Water");bool waterIncluded=false;
            for(int i=0;i<included.arraySize;i++)if(included.GetArrayElementAtIndex(i).objectReferenceValue==waterShader)waterIncluded=true;
            if(!waterIncluded){included.InsertArrayElementAtIndex(included.arraySize);included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue=waterShader;graphics.ApplyModifiedPropertiesWithoutUndo();}
            var stillWater=Shader.Find("Era/Still Water");bool stillIncluded=false;
            if(waterShader==null||stillWater==null)throw new Exception("Missing transparent water shader");
            for(int i=0;i<included.arraySize;i++)if(included.GetArrayElementAtIndex(i).objectReferenceValue==stillWater)stillIncluded=true;
            if(!stillIncluded){included.InsertArrayElementAtIndex(included.arraySize);included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue=stillWater;graphics.ApplyModifiedPropertiesWithoutUndo();}
            IconBuilder.Prepare();PrepareItemArtwork();SurfaceTextureBuilder.Prepare();KayKitImporter.Prepare();PrepareMusic();
            PrepareCharacterPrefabs();ActorArtBuilder.Prepare();FacetedArtBuilder.Prepare();WeaponAssetBuilder.Prepare();RoguelikeArtBuilder.Prepare();BlenderCharacterImporter.Prepare();CharacterBodyBuilder.Prepare();
            if(UnityEngine.Object.FindAnyObjectByType<WorldEnvironment>()==null)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            var world=UnityEngine.Object.FindAnyObjectByType<WorldEnvironment>();
            if(world==null || world.bakedVersion!=WorldEnvironment.CurrentVersion)BakeWorld();
            AssetDatabase.SaveAssets();
            // Generated mesh libraries are binary; source scenes, prefabs and scripts stay reviewable text.
            var compactLibraries=Array.FindAll(Directory.GetFiles("Assets/Era/Generated","*.asset"),p=>new FileInfo(p).Length>50_000_000);
            if(compactLibraries.Length>0)AssetDatabase.ForceReserializeAssets(compactLibraries);
        }
        static void PrepareItemArtwork()
        {
            for(int sheet=1;sheet<=4;sheet++)
            {
                string path="Assets/Era/Resources/UI/item-sheet-"+sheet+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.GUI;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
        }
        static void PrepareMusic()
        {
            foreach(string path in new[]{"Assets/Era/Resources/Audio/Stillwater-at-Dawn.wav","Assets/Era/Resources/Audio/Storm-of-the-Seven.wav"})
            {
                if(!File.Exists(path)){if(path.Contains("Storm-of-the-Seven"))MusicComposer.WriteBattleWave(path);else MusicComposer.WriteWave(path);}
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                if(importer!=null)
                {
                    var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;
                    importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
                }
            }
        }
        [MenuItem("ERA/Create missing character prefabs")]
        public static void PrepareCharacterPrefabs()
        {
            string[] names={"Characters/Ember","Characters/Echo","Characters/Lumen","Creatures/Wisp","Creatures/Brute","Creatures/Stalker","Creatures/Warden"};
            bool needed=false;foreach(string name in names)if(!File.Exists("Assets/Era/Resources/"+name+".prefab"))needed=true;
            if(!needed)return;
            Directory.CreateDirectory("Assets/Era/Resources/Characters");Directory.CreateDirectory("Assets/Era/Resources/Creatures");Directory.CreateDirectory("Assets/Era/Generated");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,AssetDatabase.GenerateUniqueAssetPath("Assets/Era/Generated/CharacterVisuals.asset"));
            var temp=new GameObject("Prefab authoring");var visual=new VisualFactory();
            try
            {
                for(int i=0;i<names.Length;i++)
                {
                    string path="Assets/Era/Resources/"+names[i]+".prefab";if(File.Exists(path))continue;
                    var root=i<3?HeroMotion.Create(visual,temp.transform,(Hero)i,((Hero)i).ToString()).gameObject:CreatureMotion.Create(visual,temp.transform,(EnemyKind)(i-3)).gameObject;
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))AssetDatabase.AddObjectToAsset(filter.sharedMesh,library);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())foreach(var mat in renderer.sharedMaterials)if(mat!=null&&!AssetDatabase.Contains(mat))AssetDatabase.AddObjectToAsset(mat,library);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                AssetDatabase.SaveAssets();Debug.Log("ERA_PREFABS_READY 3 heroes / 4 creatures");
            }
            finally{UnityEngine.Object.DestroyImmediate(temp);}
        }
        [MenuItem("ERA/Rebuild Stillwater map")]
        public static void BakeWorld()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var env=new GameObject("World / Stillwater Reach").AddComponent<WorldEnvironment>();env.Build();
            Directory.CreateDirectory("Assets/Era/Generated");
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Era/Generated/StillwaterWorld.asset");
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,path);
            long chunkBytes=0;int chunk=0;var meshLibrary=library;
            foreach(var filter in env.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;if(mesh==null||AssetDatabase.Contains(mesh))continue;
                long bytes=mesh.vertexCount*72L+mesh.GetIndexCount(0)*4L;
                if(chunkBytes+bytes>24_000_000&&chunkBytes>0){meshLibrary=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(meshLibrary,AssetDatabase.GenerateUniqueAssetPath("Assets/Era/Generated/Realm10-chunk-"+(++chunk)+".asset"));chunkBytes=0;}
                AssetDatabase.AddObjectToAsset(mesh,meshLibrary);chunkBytes+=bytes;
            }
            foreach(var renderer in env.GetComponentsInChildren<Renderer>())
            {
                foreach(var material in renderer.sharedMaterials)if(material!=null&&!AssetDatabase.Contains(material))AssetDatabase.AddObjectToAsset(material,library);
                renderer.gameObject.isStatic=true;
            }
            if(RenderSettings.skybox!=null&&!AssetDatabase.Contains(RenderSettings.skybox))AssetDatabase.AddObjectToAsset(RenderSettings.skybox,library);
            new GameObject("Game systems / Press Play").AddComponent<GameApp>();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,0),Quaternion.Euler(32,35,0),30);
            Debug.Log("ERA_WORLD_BAKED "+env.GetComponentsInChildren<Renderer>().Length+" chunked renderers / sculpted hills / reactive grass");
        }
        [MenuItem("ERA/Build Windows")]
        public static void BuildWindows()
        {
            PrepareProject();
            SimulationChecks.Run();
            string output="Builds/Windows";var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--era-output");if(at>=0&&at+1<args.Length)output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = Path.Combine(output,"Era.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result + " / " + report.summary.totalErrors);
            Debug.Log("ERA_BUILD_OK " + report.summary.totalSize + " bytes");
        }
        [MenuItem("ERA/Open game scene")]
        public static void OpenScene() { PrepareProject(); EditorSceneManager.OpenScene(ScenePath); }
    }
}
