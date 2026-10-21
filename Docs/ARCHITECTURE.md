# v0.5 architecture

## Editable world and assets

`Rift.unity` contains the baked Stillwater Reach world and a GameApp entry object. `WorldEnvironment.Build` authors 64 terrain chunks, groves, rocks, flowers, four landmarks and lighting. BuildTools saves generated meshes/materials into a WorldAssetLibrary in `Assets/Era/Generated`; normal builds keep the baked scene if its version matches. Explicitly rebuilding the world replaces the scene, so preserve manual scene work separately.

`WorldMap` defines the host/client-shared height function and deterministic obstacle footprints. The play area is a 150 m radius circle. Terrain is a triangular approximation of that smooth height function; trees and boulders resolve planar collision in the simulation. The chase camera follows ground and moves inward to avoid trees and large creatures.

Hero and creature prefabs are saved under `Resources/Characters` and `Resources/Creatures`. Runtime code loads those prefabs, preserving artist changes. Missing assets are authored once by BuildTools; ordinary builds do not overwrite existing character prefabs. `VisualFactory` supplies fallback procedural geometry and authoring primitives.

`HeroMotion` and `CreatureMotion` retain the original procedural fallback, while the shipped prefabs now use textured KayKit skinned models and 20 native skeletal animation clips each. `SkeletalActor` blends locomotion and upper-body attacks, samples an idle pose at spawn, adds a crouch overlay, and handles landing, hit and death animation. WeaponGrip runs after skeletal poses, keeping the weapon grip at the animated hand. Corpses finish a death clip before sinking away. Enemy roles keep their gameplay stats while using skeleton minion, warrior, rogue and mage models.

## Authoritative game state

Only the host runs `Simulation`, at 30 fixed ticks per second. It owns damage, movement, terrain/obstacle collision, jump physics, cooldowns, enemy AI, currency, chests, experience, upgrade offers, revives and run results. Clients send input intent rather than damage or positions. Invalid movement numbers are sanitized and direction magnitude is limited.

Jumping has a 0.14 s input buffer, 0.12 s coyote window, variable hold height, stronger falling gravity and a ground height query. Jump and landing serials reproduce animation/audio once on each client. Casting has a separate serial, pose timer and ability kind.

`WorldState.coins` is the shared team wallet. XP and gold are distinct pickup kinds. XP magnets only remove experience drops. Chests are created once in the simulation, with deterministic positions. Interaction finds an unopened chest within range, checks and deducts the team balance, marks the chest opened and creates a reserved LootState atomically in the single host thread. The reward then pops up, hovers and flies to its owner before equipping. A second interaction cannot pay for the same chest again. Moon chests grant two copies and extra shared XP.

The presentation never awards money or items. Its lid animation reads `ChestState.opened`; pickups disappear when removed by the host. HUD statistics read shared and individual counters directly from snapshots.

## Lobby and session flow

Main menu, connection screen, character selection and lobby are distinct UI states. Solo launches only after confirming the selected character. Online creation/join also follows character selection.

The `ready` message updates only the sending player's readiness on the host. Both players must be present and ready for a three-second countdown. Unready resets the countdown, disconnect resets readiness, and loadout edits are denied while ready or after starting. The host starts automatically when the countdown reaches zero. No manual host start button bypasses readiness.

## Transport

`CoopTransport` is a two-player, dependency-free, host-authoritative TCP transport for LAN/VPN, port 7777. Protocol **4** adds crouch, air jumps, blessing arrays, shrine channel state and queued reward IDs; it is incompatible with previous builds. Each frame has a four-byte network-order length and UTF-8 JSON body, with a 512 KiB limit. Socket I/O uses background threads; all Unity API and JSON state handling run on the main thread.

Snapshots are sent 20 times per second. A single replaceable pending snapshot avoids accumulating stale state; commands use a separate bounded queue. TCP has ordered delivery and `NoDelay`; this is suitable for a prototype and can stall on packet loss. There is smoothing but no local input prediction or rewind. Joining a running session, reconnect, host migration, Relay, public matchmaking, authentication and split screen are outside the current implementation.

## Audio, UI and diagnostics

`MusicComposer` writes an original 16-bar stereo score into a real WAV resource, imported with Vorbis compression. `SynthAudio` loops the score, controls music/effects independently and generates short event sounds. No third-party audio is embedded. Audio assets do not depend on a network connection.

`GameHud` is a bilingual Russian / English IMGUI interface in 1600x900 reference coordinates, scaled to fit the window. Dark beveled frames, gold borders and light text are drawn in code, using nine-slice rendering shared with FrameCapture. Weapons sit at the top, ability cooldown icons at the bottom, and blue XP progress below red HP. `FrameCapture` renders the real camera and the same HUD drawing commands into a texture, allowing screenshots from hidden test windows; the text rasterization in diagnostic captures can differ slightly from IMGUI's own font rendering.

`SimulationChecks` runs 155 assertions before building. `RuntimeDiagnostics` is enabled only with explicit diagnostic command-line flags. The two-process smoke test exercises readiness/start, jump, abilities, chest purchase, gold, XP, upgrades and music inclusion. It persists reports and captures under `Artifacts`, and does not save player preferences. Actual LAN/firewall behavior still requires two target computers.

## Level choices, weapons and icons

Simulation.UpgradePaused is derived from a live player's pendingLevels in a solo WorldState. Choices are processed before the early return; all elapsed time, cooldowns, AI, spawns, pickups and loot remain frozen until the last pending choice is consumed. GameApp releases the cursor and zeroes Time.timeScale during solo pause. Presentation animation uses simulation time and respects WorldPaused. WorldState.multiplayer is set when hosting and is retained if the companion disconnects, so online level choices never pause play. Network pumping always uses unscaled time.

PlayerState carries exactly four weapon kinds, levels and independent timers. Simulation.Combat implements arc cleaves, chained lightning, swept arrow collision and piercing line thrusts. Chests prioritize missing types; duplicates improve existing slots up to level 5. LootState includes owner, content, kind, age and collection progress so clients reproduce the same drop. An eliminated or disconnected owner's loot transfers to a live teammate. Presentation does not award loot.

I18n keeps the local language outside authoritative game state, persists era.language, and resolves both fixed UI strings and game definitions. Reward notices contain structured IDs rather than localized sentences so players can use different languages. IconBuilder authors 25 original 128px transparent PNGs under Resources/Icons. They are regular editable assets, shared by menus, upgrade cards, reward notices and the compact ability/weapon HUD.

Build-Isolated.ps1 validates a source snapshot under Artifacts/Validation-v03 without closing the user's interactive editor; normal builds can use the ERA menu. Test-Solo.ps1 verifies actual frame-time pause/resume, both languages, loot motion and four chest-sourced slots. Test-Coop.ps1 exercises simultaneous English/Russian clients and verifies simulation time advances while upgrades remain pending.
## Shrine authority and extra jumps

Simulation.Shrines populates twelve deterministic shrine locations. Holding F is a continuous intent (InputFrame.revive), also used for teammate revival. A shrine checks a live player within 3 m, reserves one owner and accumulates five seconds. Releasing the intent, moving out of range or becoming downed resets ownership/progress. The character turns toward the shrine while channeling. Completion atomically marks the shrine spent, rolls a valid blessing, applies it to that player and emits a structured RewardNotice. Shared snapshots carry all progress and effects; clients never grant buffs locally. Solo level pauses freeze shrine time too.

Eight blessings apply run-lifetime stat changes or consumable shield. Skybound grants an extra airborne jump, capped at three; ground contact replenishes the jump budget. Crouch is held input, makes ground movement 43% speed, lowers the camera and blends a crouch pose over locomotion. It does not add stealth AI or damage immunity.

GameHud queues personal RewardNotice IDs, so rapid chest/shrine rewards each get an icon, localized name and effect description. Weapon slots are top-center; abilities remain compact. Tab opens a large map with health, level progress, time, kills, gold and chest count. Experience pickups render as shared faceted blue crystal meshes. Shrine cores, orbital seals, charge glow, lids, loot arcs, foliage wind and death presentation obey solo pause.

KayKitImporter is an editor-only, offline GLB conversion for the six retained CC0 source models. It resolves meshes, UVs, texture atlases, bind poses, bone weights and animation curves into native Unity assets. Hidden handheld accessories remain hidden while authored headgear/cloaks stay visible. Licensing and author credits are in Docs/THIRD-PARTY.md and copied into releases. SurfaceTextureBuilder generates subtle seamless detail textures, while SurfaceDetail applies them without overwriting the baked map scene.
