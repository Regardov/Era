using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Era
{
    [Serializable]
    public sealed class ArcherClipReference
    {
        public string name;
        public AnimationClip clip;
        public bool loop;
        public float duration;
    }

    // Presentation only. Authoritative projectiles and damage stay in Simulation.
    // The exported Generic skeleton includes the bow/string and clothing bones.
    [DefaultExecutionOrder(65)]
    [DisallowMultipleComponent]
    public sealed class ArcherAnimationDriver : MonoBehaviour
    {
        public const string ModelRootName = "ArcherVisual";
        public Animator animator;
        public Transform actorRoot, pelvis, spine, chest, head;
        public Transform leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot;
        public Transform weaponSocket, arrowNock, arrowSpawn;
        public Renderer[] bowRenderers = Array.Empty<Renderer>();
        public Renderer[] nockedArrowRenderers = Array.Empty<Renderer>();
        public Renderer[] modelRenderers = Array.Empty<Renderer>();
        public ArcherClipReference[] clips = Array.Empty<ArcherClipReference>();
        public float walkSpeed = .9f * (2.23f / 1.8f), runSpeed = 2.8f * (2.23f / 1.8f);
        public bool useDraftDeathAnimation;
        public UnityEvent onSpawnArrow = new UnityEvent();
        public UnityEvent onCastActivate = new UnityEvent();
        public UnityEvent onTeleportDisappear = new UnityEvent();
        public UnityEvent onTeleportComplete = new UnityEvent();

        readonly Dictionary<string, ArcherClipReference> lookup = new Dictionary<string, ArcherClipReference>();
        Transform[] bindTransforms;
        Vector3[] bindPositions, bindScales;
        Quaternion[] bindRotations;
        string upperState = "", fullState = "Locomotion";
        float upperRemaining, fullRemaining, smoothedSpeed, layerWeight, aimYaw, crouchBlend, shotInterval = .76f;
        float lastHp = -1, previousDash;
        int owner = -1, attackSerial = -1, castSerial = -1, releaseEventSerial = int.MinValue, castEventSerial = int.MinValue, chestStamp;
        bool bound, dead, manualAim, manualDraw, bowVisible = true, arrowVisible, vanished, sampled, standing;
        PlayerState presented;

        public IReadOnlyList<ArcherClipReference> Clips => clips;
        public bool IsDead => dead;
        public bool IsHoldingBow => upperState == "Archer_BowHold";
        public Vector3 LastArrowOrigin { get; private set; }
        public Vector3 LastArrowDirection { get; private set; }
        public string LastAnimationEvent { get; private set; }
        public int ArrowEventCount { get; private set; }
        public int CastEventCount { get; private set; }

        void Awake() { Bind(); Stand(); }
        void Bind()
        {
            if (bound) return;
            if (animator == null) animator = GetComponent<Animator>();
            if (actorRoot == null) actorRoot = GetComponentInParent<HeroMotion>()?.transform ?? transform;
            foreach (var item in clips) if (item != null && item.clip != null && !string.IsNullOrEmpty(item.name)) lookup[item.name] = item;
            bindTransforms = GetComponentsInChildren<Transform>(true);
            bindPositions = new Vector3[bindTransforms.Length]; bindRotations = new Quaternion[bindTransforms.Length]; bindScales = new Vector3[bindTransforms.Length];
            for (int i = 0; i < bindTransforms.Length; i++) { bindPositions[i] = bindTransforms[i].localPosition; bindRotations[i] = bindTransforms[i].localRotation; bindScales[i] = bindTransforms[i].localScale; }
            if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
            bound = true;
        }
        public Transform Bone(string name)
        { foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
        public AnimationClip Clip(string name) { Bind(); return lookup.TryGetValue(name, out var item) ? item.clip : null; }
        public float Duration(string name) { var clip = Clip(name); return clip != null ? Mathf.Max(.033f, clip.length) : .3f; }
        public void Stand()
        {
            Bind(); if (animator == null || dead) return;
            if (standing && !sampled) return;
            animator.enabled = true; animator.speed = 1; animator.SetFloat("Speed", 0); animator.SetFloat("LocomotionRate", 1); animator.SetFloat("UpperRate", 1);
            animator.SetLayerWeight(1, 0); animator.Play("Base Layer.Locomotion", 0, 0); fullState = "Locomotion"; fullRemaining = 0; upperState = ""; upperRemaining = 0; sampled = false;
            SetArrowVisible(false);standing = true;
        }
        public void Present(PlayerState p)
        {
            Bind(); if (p == null || animator == null || animator.runtimeAnimatorController == null) return;
            bool arriving = owner < 0;
            owner = p.id; presented = p; sampled = false;standing = false;
            var app = GameApp.Instance; bool paused = app != null && app.WorldPaused; animator.speed = paused ? 0 : 1; if (paused) return;
            animator.enabled = true;
            float dt = PresentationMotion.Delta;
            float speed = Vector3.ProjectOnPlane(p.velocity, Vector3.up).magnitude;
            smoothedSpeed = PresentationMotion.Damp(smoothedSpeed, p.grounded ? speed : 0, 12, dt);
            animator.SetFloat("Speed", smoothedSpeed);
            // Blend-tree thresholds already interpolate authored stride speeds.
            // Only rescale cadence outside the tree's walking/running range.
            float rate = speed < walkSpeed ? speed / Mathf.Max(.1f, walkSpeed) : speed > runSpeed ? speed / Mathf.Max(.1f, runSpeed) : 1;
            animator.SetFloat("LocomotionRate", speed > .15f ? Mathf.Clamp(rate, .25f, 3.2f) : 1);
            bool hasBow = p.lastWeapon == (int)WeaponKind.Bow || p.lastWeapon < 0 && Array.IndexOf(p.weapons, (int)WeaponKind.Bow) >= 0;
            SetBowVisible(hasBow);
            if (attackSerial < 0) { attackSerial = p.weaponCastSerial; castSerial = p.castSerial; chestStamp = ChestStamp(p.id); }
            if (arriving && !p.downed) PlayFull("Archer_TeleportIn", true);
            if (p.downed)
            {
                if (!dead) { dead = true; ClearUpper(true); if(useDraftDeathAnimation)PlayFull("Archer_Death", false);else{PlayFull("Archer_Hit",false);animator.Play("Base Layer.Archer_Hit",0,.067f/Duration("Archer_Hit"));animator.Update(0);} }
                if(!useDraftDeathAnimation)animator.speed=0;
                lastHp = p.hp; attackSerial = p.weaponCastSerial; castSerial = p.castSerial; return;
            }
            if (dead) { dead = false; vanished = false; SetModelVisible(true); PlayFull("Archer_TeleportIn", true); }
            if (lastHp >= 0 && p.hp < lastHp && fullState != "Archer_Dash" && fullState != "Archer_DodgeRoll") { ClearUpper(false); PlayFull("Archer_Hit", true); }
            lastHp = p.hp;
            if (p.dashTime > 0 && previousDash <= 0) { ClearUpper(false); PlayFull("Archer_Dash", true); }
            previousDash = p.dashTime;
            if (p.castSerial != castSerial) { ClearUpper(false); PlayFull("Archer_Cast", true); }
            castSerial = p.castSerial;
            int stamp = ChestStamp(p.id);
            if (stamp > chestStamp && fullRemaining <= 0) { ClearUpper(false); PlayFull("Archer_Interact", true); }
            chestStamp = stamp;
            if (fullRemaining > 0)
            {
                fullRemaining = Mathf.Max(0, fullRemaining - dt);
                if (fullRemaining <= 0 && fullState != "Archer_Death" && fullState != "Archer_TeleportOut") ReturnToLocomotion();
            }
            if (p.chargingShrine >= 0 && fullRemaining <= 0) PlayFull("Archer_Cast", true);

            float timer = BowTimer(p);
            float targetYaw = p.weaponAimYaw;
            bool target = hasBow && TargetInRange(p, out targetYaw);
            if (!target) targetYaw = p.weaponAimYaw;
            aimYaw = manualAim ? aimYaw : target ? targetYaw : p.weaponPoseTime > 0 ? p.weaponAimYaw : p.yaw;
            bool shot = p.weaponCastSerial != attackSerial && p.lastWeapon == (int)WeaponKind.Bow;
            attackSerial = p.weaponCastSerial;
            if (shot && !dead)
            {
                if (timer > 0) shotInterval = timer;
                // Auto-fire can occur during a cast/dash. Keep the full-body
                // action and its events; don't cancel it with a ranged pose.
                if (fullState == "Locomotion") BeginUpper("Archer_BowRelease", Mathf.Min(Duration("Archer_BowRelease"), Mathf.Clamp(shotInterval * .14f, .045f, .18f)));
                // t=0 is the baked release marker. Dedupe the imported AnimationEvent
                // against this immediate host-shot presentation; never create damage here.
                SpawnArrow();
            }
            if (fullRemaining <= 0 && fullState == "Locomotion")
            {
                bool preparing = hasBow && (manualAim || target && timer >= 0 && timer < .62f);
                if (upperState.Length == 0 && preparing) BeginUpper("Archer_Aim", manualAim ? Duration("Archer_Aim") : Mathf.Clamp(timer * .20f, .025f, .15f));
                if (upperState.Length > 0 && upperState != "Archer_BowHold")
                {
                    upperRemaining = Mathf.Max(0, upperRemaining - dt);
                    if (upperRemaining <= 0)
                    {
                        switch (upperState)
                        {
                            case "Archer_Aim": BeginUpper("Archer_BowDraw", manualAim ? Duration("Archer_BowDraw") : Mathf.Clamp(timer, .03f, .65f)); break;
                            case "Archer_BowDraw": BeginUpper("Archer_BowHold", Duration("Archer_BowHold")); break;
                            case "Archer_BowRelease": BeginUpper("Archer_ShootRecovery", Mathf.Min(Duration("Archer_ShootRecovery"), Mathf.Clamp(shotInterval * .14f, .045f, .18f))); break;
                            case "Archer_ShootRecovery": ClearUpper(false); break;
                        }
                    }
                }
                if (upperState == "Archer_BowHold" && !preparing && !manualDraw) ClearUpper(false);
                if (!hasBow) ClearUpper(false);
            }
            float weight = upperState.Length > 0 && fullRemaining <= 0 ? 1 : 0;
            layerWeight = PresentationMotion.Damp(layerWeight, weight, 24, dt); animator.SetLayerWeight(1, layerWeight);
            crouchBlend = PresentationMotion.Damp(crouchBlend, p.crouching && p.grounded ? 1 : 0, 14, dt);
        }
        float BowTimer(PlayerState p)
        { for (int i = 0; i < p.weapons.Length; i++) if (p.weapons[i] == (int)WeaponKind.Bow && i < p.weaponTimers.Length) return p.weaponTimers[i]; return -1; }
        bool TargetInRange(PlayerState p, out float yaw)
        {
            yaw = p.yaw; var state = GameApp.Instance?.State; if (state == null) return false;
            float range = Mathf.Max(1, GameData.Weapons[(int)WeaponKind.Bow].range + p.weaponRange - 15), nearest = range * range; bool found = false;
            foreach (var e in state.enemies) if (e.hp > 0)
            {
                var d = e.position - p.position; float sq = d.sqrMagnitude;
                if (sq < nearest) { nearest = sq; yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; found = true; }
            }
            return found;
        }
        int ChestStamp(int id)
        { int stamp = 0; var state = GameApp.Instance?.State; if (state != null) foreach (var c in state.chests) if (c.opened && c.owner == id) stamp = unchecked(stamp + c.id + 1); return stamp; }
        void BeginUpper(string name, float seconds)
        {
            if (Clip(name) == null || animator == null) return;
            upperState = name; upperRemaining = Mathf.Max(.033f, seconds); animator.SetFloat("UpperRate", Mathf.Clamp(Duration(name) / upperRemaining, .3f, 12));
            animator.CrossFadeInFixedTime("UpperBody." + name, .045f, 1, 0);
            SetArrowVisible(name == "Archer_Aim" || name == "Archer_BowDraw" || name == "Archer_BowHold");
        }
        void ClearUpper(bool immediate)
        {
            upperState = ""; upperRemaining = 0; SetArrowVisible(false);
            if (animator != null && immediate) { layerWeight = 0; animator.SetLayerWeight(1, 0); }
        }
        void PlayFull(string name, bool recover)
        {
            if (Clip(name) == null || animator == null) return;
            fullState = name; fullRemaining = recover ? Duration(name) : 0; ClearUpper(true); animator.CrossFadeInFixedTime("Base Layer." + name, .055f, 0, 0);
        }
        void ReturnToLocomotion() { fullState = "Locomotion"; fullRemaining = 0; animator.CrossFadeInFixedTime("Base Layer.Locomotion", .10f, 0, 0); }
        public void SetAiming(bool enabled, float worldYaw, bool holdDraw = true) { manualAim = enabled; manualDraw = enabled && holdDraw; aimYaw = worldYaw; if (!enabled) ClearUpper(false); }
        public void PlayDodgeRoll() { if (!dead) { Bind(); PlayFull("Archer_DodgeRoll", true); } }
        public void PlayInteraction() { if (!dead) { Bind(); PlayFull("Archer_Interact", true); } }
        public void PlayTeleportOut() { if (!dead) { Bind(); PlayFull("Archer_TeleportOut", false); } }
        public void PlayTeleportIn() { Bind(); dead = false; vanished = false; SetModelVisible(true); PlayFull("Archer_TeleportIn", true); }

        void LateUpdate()
        {
            if (sampled || presented == null || dead || GameApp.Instance?.WorldPaused == true || animator == null || !animator.enabled) return;
            if (layerWeight > .01f && actorRoot != null)
            {
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(actorRoot.eulerAngles.y, aimYaw), -65, 65) * layerWeight;
                if (spine != null) spine.rotation = Quaternion.AngleAxis(yaw * .35f, actorRoot.up) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(yaw * .65f, actorRoot.up) * chest.rotation;
            }
            // Maintain the game's existing crouch input with a floor-constrained
            // additive IK adjustment; authored clips remain the movement source.
            if (crouchBlend > .001f && fullState == "Locomotion") ApplyCrouch(crouchBlend);
        }
        public void ApplyCrouch(float blend)
        {
            if (pelvis != null && actorRoot != null && leftThigh != null && leftShin != null && leftFoot != null && rightThigh != null && rightShin != null && rightFoot != null)
            {
                var left = leftFoot.position; var right = rightFoot.position; var lr = leftFoot.rotation; var rr = rightFoot.rotation;
                pelvis.position += actorRoot.TransformVector(new Vector3(0, -.24f, -.08f) * Mathf.Clamp01(blend));
                ArmPoseIK.Solve(leftThigh, leftShin, leftFoot, left, leftThigh.position + actorRoot.forward);
                ArmPoseIK.Solve(rightThigh, rightShin, rightFoot, right, rightThigh.position + actorRoot.forward);
                leftFoot.rotation = lr; rightFoot.rotation = rr;
            }
        }
        void SetBowVisible(bool visible)
        { bowVisible = visible; foreach (var r in bowRenderers) if (r != null) r.enabled = visible && !vanished; if (!visible) SetArrowVisible(false); }
        void SetArrowVisible(bool visible)
        { arrowVisible = visible; foreach (var r in nockedArrowRenderers) if (r != null) r.enabled = visible && bowVisible && !vanished; }
        void SetModelVisible(bool visible)
        { foreach (var r in modelRenderers) if (r != null) r.enabled = visible; SetBowVisible(bowVisible); SetArrowVisible(arrowVisible); }

        // Imported animation event receivers are on the Animator GameObject.
        public void SpawnArrow()
        {
            LastAnimationEvent = "SpawnArrow"; SetArrowVisible(false);
            if (releaseEventSerial == attackSerial) return;
            releaseEventSerial = attackSerial; ArrowEventCount++;
            LastArrowOrigin = arrowSpawn != null ? arrowSpawn.position : transform.position;
            LastArrowDirection = arrowSpawn != null ? arrowSpawn.up : actorRoot != null ? Quaternion.Euler(0, aimYaw, 0) * Vector3.forward : transform.forward;
            onSpawnArrow?.Invoke();
        }
        public void CastActivate()
        {
            LastAnimationEvent = "CastActivate"; if (castEventSerial == castSerial) return; castEventSerial = castSerial; CastEventCount++; onCastActivate?.Invoke();
            var p = GameApp.Instance?.State?.Player(owner);
            if (p != null && actorRoot != null) PresentationFx.Emit(actorRoot.position + Vector3.up, GameData.HeroColor(p.hero), 1.1f, p.hero == Hero.Lumen ? FeedbackKind.Heal : p.hero == Hero.Echo ? FeedbackKind.Shield : FeedbackKind.Magic);
        }
        public void TeleportDisappear() { LastAnimationEvent = "TeleportDisappear"; vanished = true; SetModelVisible(false); onTeleportDisappear?.Invoke(); }
        public void TeleportComplete() { LastAnimationEvent = "TeleportComplete"; vanished = false; SetModelVisible(true); onTeleportComplete?.Invoke(); }

        public bool SampleClip(string name, float time)
        {
            Bind(); var clip = Clip(name); if (clip == null) return false;
            if (animator != null) animator.enabled = false;
            for (int i = 0; i < bindTransforms.Length; i++) if (bindTransforms[i] != null) { bindTransforms[i].localPosition = bindPositions[i]; bindTransforms[i].localRotation = bindRotations[i]; bindTransforms[i].localScale = bindScales[i]; }
            clip.SampleAnimation(gameObject, Mathf.Clamp(time, 0, clip.length)); sampled = true; vanished = false; SetModelVisible(true); SetBowVisible(true);
            SetArrowVisible(name == "Archer_Aim" || name == "Archer_BowDraw" || name == "Archer_BowHold"); return true;
        }
    }
}
