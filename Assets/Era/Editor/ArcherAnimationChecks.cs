using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Era.Editor
{
    public static class ArcherAnimationChecks
    {
        static int assertions;
        static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception("ERA archer check failed: " + message); }
        [MenuItem("ERA/Archer/Import and validate")]
        public static void ImportAndValidate() { ArcherAnimationImporter.Prepare(); Run(); }
        public static void DumpMaterials()
        {
            var driver=Resources.Load<GameObject>("Characters/Ember").GetComponent<HeroMotion>().archer;
            foreach(var material in driver.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                Debug.Log("ERA_ARCHER_MATERIAL "+material.name+" shader="+material.shader.name+" color="+material.color.ToString("F8")+" gamma="+material.color.gamma.ToString("F8"));
        }
        [MenuItem("ERA/Archer/Validate imported animation system")]
        public static void Run()
        {
            assertions = 0; RunPrefab(Resources.Load<GameObject>("Characters/Ember"));
            Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/archer-checks.json", "{\"ok\":true,\"assertions\":" + assertions + "}");
            Debug.Log("ERA_ARCHER_CHECKS_OK " + assertions + " assertions");
        }
        public static int RunPrefab(GameObject source)
        {
            int before = assertions;
            Check(source != null, "Ember resource prefab exists");
            var hero = source.GetComponent<HeroMotion>(); var authored = source.GetComponent<AuthoredCharacter>(); var driver = hero != null ? hero.archer : null;
            Check(hero != null && hero.Hero == Hero.Ember && driver != null, "existing Ember identity and HeroMotion bind the Generic visual");
            Check(source.GetComponent<ActorBodyCollider>() != null, "original root gameplay collider retained");
            Check(AssetDatabase.AssetPathToGUID(ArcherAnimationImporter.PrefabPath) == "82a05cda970775d40a5c472775efb80e", "existing Ember prefab GUID preserved");
            Check(authored != null && authored.version == 20 && authored.sourceHash?.Length == 64 && authored.triangles > 0, "FBX provenance and measured triangle count");
            Check(source.GetComponent<SkeletalActor>() == null && source.GetComponent<HumanRigShape>() == null && source.GetComponent<CostumeMotion>() == null && source.GetComponent<Animation>() == null, "legacy pose writers removed from Generic actor");
            Check(driver.animator != null && driver.animator.avatar != null && driver.animator.avatar.isValid && !driver.animator.avatar.isHuman, "valid Generic avatar");
            Check(!driver.animator.applyRootMotion && driver.animator.cullingMode == AnimatorCullingMode.AlwaysAnimate, "simulation remains the only actor movement source");
            Check(Mathf.Abs(driver.transform.localScale.x - ArcherAnimationImporter.VisualScale) < .0001f, "visual has explicit human-scale conversion");
            Check(driver.weaponSocket != null && driver.arrowNock != null && driver.arrowSpawn != null && hero.rightElbow == driver.Bone("Hand.R"), "projectile sockets and independent right-hand weapon grip rebound");
            foreach (string bone in new[] { "Root", "Pelvis", "Spine", "Chest", "Neck", "Head", "UpperArm.L", "Forearm.L", "Hand.L", "UpperArm.R", "Forearm.R", "Hand.R", "Thigh.L", "Shin.L", "Foot.L", "Toe.L", "Thigh.R", "Shin.R", "Foot.R", "Toe.R", "BowRoot" }) Check(driver.Bone(bone) != null, "shared animation bone: " + bone);
            var controller = driver.animator.runtimeAnimatorController as AnimatorController;
            Check(controller != null && controller.layers.Length == 2, "locomotion plus masked upper-body controller");
            foreach (string param in new[] { "Speed", "LocomotionRate", "UpperRate" }) Check(controller.parameters.Any(p => p.name == param), "controller parameter: " + param);
            var mask = controller.layers[1].avatarMask;
            Check(mask != null && mask.transformCount > 20, "upper-body transform mask exists");
            bool spineActive = false;
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                if (path.EndsWith("Spine", StringComparison.Ordinal)) spineActive |= mask.GetTransformActive(i);
                if (path.Contains("Thigh.") || path.Contains("Shin.") || path.Contains("Foot.")) Check(!mask.GetTransformActive(i), "upper layer cannot overwrite locomotion legs: " + path);
            }
            Check(spineActive, "upper layer animates the spine");
            var data = ArcherAnimationImporter.ReadManifest();
            Check(driver.clips.Length == 16, "sixteen standalone clips assigned");
            foreach (var clipData in data.clips)
            {
                var clip = driver.Clip(clipData.name); Check(clip != null && !clip.legacy && Mathf.Abs(clip.frameRate - 30) < .1f && clip.length > .06f, "nonempty 30fps Generic clip: " + clipData.name);
                Check(File.ReadLines(AssetDatabase.GetAssetPath(clip)).Take(6).Any(line => line == "AnimationClip:"), "serialized runtime AnimationClip class, never editor-only PreviewAnimationClip: " + clipData.name);
                Check(clip.isLooping == clipData.loop, "loop flag: " + clipData.name);
                if (clipData.duration > 0) Check(Mathf.Abs(clip.length - clipData.duration) <= .04f, "clip duration matches export manifest: " + clipData.name);
                Check(AnimationUtility.GetCurveBindings(clip).Length > 15, "clip contains actual baked skeleton curves: " + clipData.name);
                foreach (var marker in clipData.events ?? Array.Empty<ArcherAnimationImporter.EventData>())
                {
                    var e = clip.events.FirstOrDefault(x => x.functionName == marker.name);
                    Check(e != null && Mathf.Abs(e.time - ArcherAnimationImporter.EventTime(marker, data.fps)) < .035f, "event timing in seconds: " + clipData.name + " / " + marker.name);
                }
            }
            Check(driver.Clip("Archer_BowRelease").events.Any(e => e.functionName == "SpawnArrow" && e.time < .001f), "projectile release marker matches authoritative shot at t=0");
            Check(driver.Clip("Archer_Cast").events.Any(e => e.functionName == "CastActivate"), "cast effect marker");
            Check(driver.Clip("Archer_TeleportOut").events.Any(e => e.functionName == "TeleportDisappear") && driver.Clip("Archer_TeleportIn").events.Any(e => e.functionName == "TeleportComplete"), "teleport visibility markers");
            Check(driver.bowRenderers.Length > 0 && driver.nockedArrowRenderers.Length > 0, "bow and nocked arrow have independent visibility");
            var skins = driver.GetComponentsInChildren<SkinnedMeshRenderer>(true); Check(skins.Length > 0, "skinned authored geometry exists");
            foreach(var material in skins.SelectMany(s=>s.sharedMaterials).Distinct())
            {
                var entry=data.blenderPaletteLinear?.FirstOrDefault(p=>p.name==material.name);
                Check(entry!=null&&AssetDatabase.GetAssetPath(material).StartsWith(ArcherAnimationImporter.Folder+"/Materials/",StringComparison.Ordinal),"explicit source-palette material asset: "+material.name);
                var expected=ArcherAnimationImporter.PaletteColor(entry);Check(Vector4.Distance(material.color,expected)<.0001f,"linear Blender palette converted to sRGB exactly once: "+material.name);
            }
            int triangles = 0;
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh; Check(mesh != null && mesh.vertexCount > 0 && mesh.normals.Length == mesh.vertexCount, "authored normals retained: " + skin.name);
                Check(skin.bones.Length == mesh.bindposes.Length && skin.bones.All(b => b != null), "valid bind poses and bone transforms: " + skin.name);
                Check(mesh.boneWeights.All(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .002f && w.boneIndex0 < skin.bones.Length && w.boneIndex1 < skin.bones.Length && w.boneIndex2 < skin.bones.Length && w.boneIndex3 < skin.bones.Length), "normalized skin weights: " + skin.name);
                Check(skin.sharedMaterials.Length == mesh.subMeshCount && skin.sharedMaterials.All(m => m != null && m.shader != null), "all authored material slots assigned: " + skin.name);
                triangles += mesh.triangles.Length / 3;
            }
            Check(triangles == authored.triangles, "polygon provenance matches actual render meshes");
            var instance = Object.Instantiate(source); var baked = new Mesh();
            try
            {
                var actor = instance.GetComponent<HeroMotion>().archer;
                foreach (string name in ArcherAnimationImporter.ClipNames)
                {
                    float duration = actor.Duration(name);
                    foreach (float fraction in new[] { 0f, .25f, .5f, .75f, .999f })
                    {
                        Check(actor.SampleClip(name, duration * fraction), "sample exported clip: " + name);
                        Check((actor.transform.localScale - Vector3.one * ArcherAnimationImporter.VisualScale).sqrMagnitude < .00001f, "clip cannot overwrite the visual scale: " + name);
                        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            skin.BakeMesh(baked);
                            Check(baked.vertexCount == skin.sharedMesh.vertexCount && baked.vertices.All(v => Finite(v) && instance.transform.InverseTransformPoint(skin.transform.TransformPoint(v)).magnitude < 8), "deformation stays finite and bounded in actor space: " + name + " / " + skin.name);
                        }
                    }
                }
                actor.SampleClip("Archer_Run", .04f); var left = actor.leftThigh.localRotation; actor.SampleClip("Archer_Run", actor.Duration("Archer_Run") * .45f);
                Check(Quaternion.Angle(left, actor.leftThigh.localRotation) > 10, "running clip articulates actual thigh joint");
                actor.animator.enabled = true; actor.animator.Rebind(); actor.animator.SetFloat("Speed", actor.runSpeed); actor.animator.SetFloat("LocomotionRate", 1); actor.animator.SetFloat("UpperRate", 1);
                actor.animator.Play("Base Layer.Locomotion", 0, .25f); actor.animator.SetLayerWeight(1, 0); actor.animator.Update(0);
                var runThigh = actor.leftThigh.localRotation; var runHand = actor.Bone("Hand.R").position;
                actor.animator.Play("UpperBody.Archer_BowHold", 1, .25f); actor.animator.SetLayerWeight(1, 1); actor.animator.Update(0);
                Check(Quaternion.Angle(runThigh, actor.leftThigh.localRotation) < .1f, "real Animator upper mask preserves running legs");
                Check(Vector3.Distance(runHand, actor.Bone("Hand.R").position) > .1f, "real Animator upper mask actually poses the draw hand");
                actor.SampleClip("Archer_BowRelease", 0);
                var muzzle = instance.transform.InverseTransformPoint(actor.arrowSpawn.position);
                Debug.Log("ERA_ARCHER_MUZZLE " + muzzle.ToString("F8") + " direction=" + actor.arrowSpawn.up.ToString("F8") + " expected=" + ArcherShotProfile.OriginOffset.ToString("F8"));
                Check(Vector3.Distance(muzzle, ArcherShotProfile.OriginOffset) < .035f, "authoritative projectile origin matches the baked release socket");
                Check(Vector3.Dot(actor.arrowSpawn.up, instance.transform.forward) > .995f, "exported socket +Y points along the firing axis");
                actor.SampleClip("Archer_Idle", 0); var idleFoot = new[] { actor.leftFoot.position, actor.rightFoot.position };
                actor.ApplyCrouch(1);
                Check(Vector3.Distance(idleFoot[0], actor.leftFoot.position) < .025f && Vector3.Distance(idleFoot[1], actor.rightFoot.position) < .025f, "existing crouch input preserves planted ankles");
                int events = actor.ArrowEventCount; actor.SpawnArrow(); actor.SpawnArrow(); Check(actor.ArrowEventCount == events + 1, "duplicate SpawnArrow callbacks do not duplicate visual release");
                actor.TeleportDisappear(); Check(actor.modelRenderers.All(r => !r.enabled), "teleport event hides visual only"); actor.TeleportComplete(); Check(actor.modelRenderers.Any(r => r.enabled), "teleport completion restores visual");
                actor.animator.enabled=true;actor.Stand();
                var shotState=new PlayerState{id=0,hp=100,maxHp=100,grounded=true,lastWeapon=(int)WeaponKind.Bow,weapons=new[]{(int)WeaponKind.Bow,-1},weaponTimers=new[]{.1f,0},weaponCastSerial=10};
                actor.Present(shotState);actor.Stand();shotState.castSerial++;shotState.weaponCastSerial++;actor.Present(shotState);
                actor.animator.SetLayerWeight(1,1);actor.animator.Update(0);
                Check(actor.animator.GetCurrentAnimatorStateInfo(1).IsName("UpperBody.Archer_BowRelease")||actor.animator.GetNextAnimatorStateInfo(1).IsName("UpperBody.Archer_BowRelease"),"cast plus auto-fire still requests the bow release on the upper layer");
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(instance); }
            return assertions - before;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
