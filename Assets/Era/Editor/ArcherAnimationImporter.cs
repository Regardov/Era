using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Era.Editor
{
    // Explicit opt-in importer: it never changes the legacy heroes unless both
    // authored files exist. Ember's resource path and prefab GUID remain stable.
    public static class ArcherAnimationImporter
    {
        public const string Folder = "Assets/Era/Art/Characters/Archer";
        public const string FbxPath = Folder + "/Archer_Rigged.fbx";
        public const string ManifestPath = Folder + "/Archer_AnimationManifest.json";
        public const string PrefabPath = "Assets/Era/Resources/Characters/Ember.prefab";
        public const string ControllerPath = Folder + "/Archer.controller";
        public const string MaskPath = Folder + "/Archer_UpperBody.mask";
        public const float VisualScale = 2.23f / 1.8f;
        const string Marker = "Assets/Era/Generated/ArcherAnimation-v1.txt";
        public static readonly string[] ClipNames = { "Archer_Idle", "Archer_Walk", "Archer_Run", "Archer_Aim", "Archer_BowDraw", "Archer_BowHold", "Archer_BowRelease", "Archer_ShootRecovery", "Archer_DodgeRoll", "Archer_Dash", "Archer_Hit", "Archer_Death", "Archer_Cast", "Archer_Interact", "Archer_TeleportOut", "Archer_TeleportIn" };
        [Serializable] public sealed class EventData { public string name; public float time = -1; public int frame = -1; }
        [Serializable] public sealed class ClipData { public string name; public float duration; public bool loop; public int frameStart, frameEnd; public EventData[] events; }
        [Serializable] public sealed class PaletteData { public string name; public float[] rgba; }
        [Serializable] public sealed class Manifest { public int fps = 30; public float walkSpeed = .9f, runSpeed = 2.8f; public ClipData[] clips; public PaletteData[] blenderPaletteLinear; }
        static string Hash(string path) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        public static Manifest ReadManifest()
        {
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (data == null || data.fps != 30 || data.clips == null) throw new Exception("Archer manifest must define 30 fps clips");
            foreach (string name in ClipNames) if (data.clips.Count(c => c.name == name) != 1) throw new Exception("Missing or duplicate archer clip: " + name);
            return data;
        }
        public static void PrepareIfAvailable() { if (File.Exists(FbxPath) && File.Exists(ManifestPath)) Prepare(); }
        [MenuItem("ERA/Archer/Import rig and animations")]
        public static void Prepare()
        {
            if (!File.Exists(FbxPath) || !File.Exists(ManifestPath)) throw new FileNotFoundException("Archer FBX and manifest are required");
            var manifest = ReadManifest();
            string signature = "archer-generic-v3 / " + Hash(FbxPath) + " / " + Hash(ManifestPath);
            var current = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (File.Exists(Marker) && File.ReadAllText(Marker).Trim() == signature && current != null && current.GetComponent<HeroMotion>()?.archer != null && AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            if (current == null || string.IsNullOrEmpty(guid)) throw new Exception("Existing Ember prefab/GUID is required");
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) throw new Exception("FBX ModelImporter unavailable");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "Root";
            importer.importAnimation = true; importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // Material/rig settings must be committed before Unity's native
            // clip serializer builds masks for a newly created Generic Avatar.
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var bakedTakes = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().ToArray();
            foreach (var name in ClipNames) if (!bakedTakes.Any(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal))) throw new Exception("FBX take not found: " + name);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || avatar.isHuman) throw new Exception("A valid Generic archer Avatar is required");
            // Validate the incoming hierarchy before touching shared controller,
            // mask or the existing resource prefab.
            var incoming = model.GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { "Root", "Pelvis", "Spine", "Chest", "Neck", "Head", "UpperArm.L", "Forearm.L", "Hand.L", "UpperArm.R", "Forearm.R", "Hand.R", "Thigh.L", "Shin.L", "Foot.L", "Toe.L", "Thigh.R", "Shin.R", "Foot.R", "Toe.R", "BowRoot", "WeaponSocket", "ArrowNock", "ArrowSpawn" })
                if (incoming.Count(t => t.name == name) != 1) throw new Exception("Missing or ambiguous archer bone/socket: " + name);
            var incomingRenderers = model.GetComponentsInChildren<Renderer>(true);
            if (!incomingRenderers.Any(r => IsBow(r.name)) || !incomingRenderers.Any(r => IsNockedArrow(r.name))) throw new Exception("Separate bow and NockedArrow renderers are required");
            if (incomingRenderers.Any(r => !(r is SkinnedMeshRenderer))) throw new Exception("All archer render meshes must be skinned before prefab migration");
            var materials = MakeMaterials(manifest, incomingRenderers);
            // Unity 2023.1's native SetClipAnimations throws on this FBX even
            // for freshly constructed descriptors. Copy the real baked takes
            // to stable .anim assets, retaining all curves and adding events in
            // seconds through AnimationUtility (no resampling/retargeting).
            Directory.CreateDirectory(Folder + "/Animations"); AssetDatabase.Refresh();
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var data in manifest.clips)
            {
                var sourceClip = bakedTakes.First(c => c.name == data.name || c.name.EndsWith("|" + data.name, StringComparison.Ordinal));
                string path = Folder + "/Animations/" + data.name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = Object.Instantiate(sourceClip); clip.name = data.name; AssetDatabase.CreateAsset(clip, path); }
                else { EditorUtility.CopySerialized(sourceClip, clip); clip.name = data.name; }
                clip.legacy = false;
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = data.loop; settings.loopBlend = data.loop;
                settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
                settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, (data.events ?? Array.Empty<EventData>()).Select(e => new AnimationEvent { functionName = e.name, time = Mathf.Clamp(EventTime(e, manifest.fps), 0, clip.length), messageOptions = SendMessageOptions.RequireReceiver }).ToArray());
                EditorUtility.SetDirty(clip); clips.Add(data.name, clip);
            }
            var mask = MakeMask(model);
            var controller = MakeController(clips, mask, manifest);
            if (current.GetComponent<HeroMotion>()?.archer == null)
            {
                Directory.CreateDirectory(Folder + "/Source"); AssetDatabase.Refresh();
                string backup = Folder + "/Source/Ember_PreArcher.prefab";
                if (!File.Exists(backup) && !AssetDatabase.CopyAsset(PrefabPath, backup)) throw new Exception("Cannot preserve legacy Ember prefab");
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var hero = root.GetComponent<HeroMotion>();
                if (hero == null || root.GetComponent<ActorBodyCollider>() == null) throw new Exception("Ember gameplay components missing; refusing destructive migration");
                // Only visual branches are replaced; root gameplay scripts/colliders survive.
                var visualRoots = new HashSet<GameObject>();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { var t = renderer.transform; while (t.parent != null && t.parent != root.transform) t = t.parent; if (t != root.transform) visualRoots.Add(t.gameObject); }
                if (hero.rig != null) { var t = hero.rig; while (t.parent != null && t.parent != root.transform) t = t.parent; if (t != root.transform) visualRoots.Add(t.gameObject); }
                foreach (var old in root.GetComponentsInChildren<ArcherAnimationDriver>(true)) if (old.gameObject != root) visualRoots.Add(old.gameObject);
                foreach (var old in visualRoots) if (old != null) Object.DestroyImmediate(old);
                foreach (var old in root.GetComponents<Animation>()) Object.DestroyImmediate(old);
                foreach (var old in root.GetComponents<SkeletalActor>()) Object.DestroyImmediate(old);
                foreach (var old in root.GetComponents<HumanRigShape>()) Object.DestroyImmediate(old);
                foreach (var old in root.GetComponents<CostumeMotion>()) Object.DestroyImmediate(old);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = ArcherAnimationDriver.ModelRootName; visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one * VisualScale;
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                var driver = visual.GetComponent<ArcherAnimationDriver>() ?? visual.AddComponent<ArcherAnimationDriver>();
                Transform Bone(string name) => visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) ?? throw new Exception("Missing archer bone/socket: " + name);
                driver.animator = animator; driver.actorRoot = root.transform; driver.pelvis = Bone("Pelvis"); driver.spine = Bone("Spine"); driver.chest = Bone("Chest"); driver.head = Bone("Head");
                driver.leftThigh = Bone("Thigh.L"); driver.rightThigh = Bone("Thigh.R"); driver.leftShin = Bone("Shin.L"); driver.rightShin = Bone("Shin.R"); driver.leftFoot = Bone("Foot.L"); driver.rightFoot = Bone("Foot.R");
                driver.weaponSocket = Bone("WeaponSocket"); driver.arrowNock = Bone("ArrowNock"); driver.arrowSpawn = Bone("ArrowSpawn");
                driver.walkSpeed = manifest.walkSpeed * VisualScale; driver.runSpeed = manifest.runSpeed * VisualScale;
                driver.clips = manifest.clips.Select(c => new ArcherClipReference { name = c.name, clip = clips[c.name], loop = c.loop, duration = clips[c.name].length }).ToArray();
                driver.modelRenderers = visual.GetComponentsInChildren<Renderer>(true);
                foreach(var renderer in driver.modelRenderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>materials[material.name]).ToArray();
                driver.bowRenderers = driver.modelRenderers.Where(r => IsBow(r.name)).ToArray();
                driver.nockedArrowRenderers = driver.modelRenderers.Where(r => IsNockedArrow(r.name)).ToArray();
                if (driver.bowRenderers.Length == 0) throw new Exception("No separately controllable bow mesh found");
                foreach (var r in driver.nockedArrowRenderers) r.enabled = false;
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { skin.updateWhenOffscreen = true; skin.localBounds = new Bounds(Vector3.up * .8f, new Vector3(5, 5, 5)); }
                hero.BindArcher(driver);
                var metadata = root.GetComponent<AuthoredCharacter>() ?? root.AddComponent<AuthoredCharacter>();
                metadata.model = "Ember"; metadata.version = 20; metadata.sourceHash = Hash(FbxPath); metadata.details = driver.modelRenderers.Select(r => r.name).ToArray();
                metadata.triangles = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh.triangles.Length / 3) + visual.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh == null ? 0 : f.sharedMesh.triangles.Length / 3);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (AssetDatabase.AssetPathToGUID(PrefabPath) != guid) throw new Exception("Ember GUID changed unexpectedly");
            Directory.CreateDirectory(Path.GetDirectoryName(Marker)); File.WriteAllText(Marker, signature + "\n");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("ERA_ARCHER_IMPORTED sixteen Generic clips; Ember root and GUID preserved");
        }
        public static float EventTime(EventData e, int fps) => e.time >= 0 ? e.time : Mathf.Max(0, e.frame - 1) / (float)fps;
        public static Color PaletteColor(PaletteData entry)
        {
            if(entry.rgba==null||entry.rgba.Length!=4)throw new Exception("Invalid source linear RGBA: "+entry.name);
            return new Color(entry.rgba[0],entry.rgba[1],entry.rgba[2],entry.rgba[3]).gamma;
        }
        static Dictionary<string,Material> MakeMaterials(Manifest manifest,Renderer[] renderers)
        {
            if(manifest.blenderPaletteLinear==null||manifest.blenderPaletteLinear.Length==0)throw new Exception("Fixed blenderPaletteLinear is required; imported FBX colors must not be gamma-converted repeatedly");
            var source=manifest.blenderPaletteLinear.ToDictionary(p=>p.name);
            var names=renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m.name).Distinct().ToArray();
            foreach(string name in names)if(!source.ContainsKey(name))throw new Exception("Source palette has no material: "+name);
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();var materials=new Dictionary<string,Material>();
            foreach(string name in names)
            {
                string filename=string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));string path=Folder+"/Materials/"+filename+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(Shader.Find("Standard")){name=name};AssetDatabase.CreateAsset(material,path);}
                material.shader=Shader.Find("Standard");material.color=PaletteColor(source[name]);material.SetFloat("_Glossiness",0);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);materials.Add(name,material);
            }
            return materials;
        }
        static bool IsNockedArrow(string name) => name.IndexOf("NockedArrow", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Arrow_Nocked", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool IsBow(string name) => name.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("String", StringComparison.OrdinalIgnoreCase) >= 0;
        static AvatarMask MakeMask(GameObject model)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, MaskPath); }
            mask.name = "Archer upper body (Generic transforms)";
            var transforms = model.GetComponentsInChildren<Transform>(true); mask.transformCount = transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                // Paths are relative to the Animator, independent of the FBX
                // root object's name or the scene hierarchy used for import.
                string path = AnimationUtility.CalculateTransformPath(transforms[i], model.transform);
                mask.SetTransformPath(i, path); string[] parts = path.Split('/');
                bool active = parts.Contains("Spine") || parts.Contains("BowRoot") || parts.Contains("WeaponSocket") || parts.Contains("ArrowNock") || parts.Contains("ArrowSpawn");
                mask.SetTransformActive(i, active);
            }
            EditorUtility.SetDirty(mask); return mask;
        }
        static AnimatorController MakeController(Dictionary<string, AnimationClip> clips, AvatarMask mask, Manifest manifest)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var layer in controller.layers) if (layer.stateMachine != null) Object.DestroyImmediate(layer.stateMachine, true);
            controller.layers = Array.Empty<AnimatorControllerLayer>(); controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float); controller.AddParameter("LocomotionRate", AnimatorControllerParameterType.Float); controller.AddParameter("UpperRate", AnimatorControllerParameterType.Float);
            var lower = new AnimatorStateMachine { name = "Base Layer" }; AssetDatabase.AddObjectToAsset(lower, controller);
            var upper = new AnimatorStateMachine { name = "UpperBody" }; AssetDatabase.AddObjectToAsset(upper, controller);
            controller.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", stateMachine = lower, defaultWeight = 1 }, new AnimatorControllerLayer { name = "UpperBody", stateMachine = upper, defaultWeight = 0, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override } };
            var tree = new BlendTree { name = "Archer ground speed", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false }; AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clips["Archer_Idle"], 0); tree.AddChild(clips["Archer_Walk"], manifest.walkSpeed * VisualScale); tree.AddChild(clips["Archer_Run"], manifest.runSpeed * VisualScale);
            var locomotion = lower.AddState("Locomotion", new Vector3(200, 40)); locomotion.motion = tree; locomotion.speedParameter = "LocomotionRate"; locomotion.speedParameterActive = true; lower.defaultState = locomotion;
            var empty = upper.AddState("Empty", new Vector3(200, 40)); upper.defaultState = empty;
            int baseIndex = 0, upperIndex = 0;
            foreach (string name in ClipNames.Skip(3))
            {
                bool bow = name == "Archer_Aim" || name.StartsWith("Archer_Bow", StringComparison.Ordinal) || name == "Archer_ShootRecovery";
                var state = (bow ? upper : lower).AddState(name, new Vector3(bow ? 420 : 200, 130 + 70 * (bow ? upperIndex++ : baseIndex++))); state.motion = clips[name];
                if (bow) { state.speedParameter = "UpperRate"; state.speedParameterActive = true; }
            }
            EditorUtility.SetDirty(controller); return controller;
        }
    }
}
