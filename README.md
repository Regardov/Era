# ERA: Stillwater Reach — v0.5

Unity **2023.1.22f1**, Windows. A third-person survivor for **one or two players** with a Russian / English interface, a fantasy game HUD, textured KayKit skeletal actors, original music and sound effects.

## Open in Unity

Open this folder in Unity Hub using **Add → Add project from disk**, then use **2023.1.22f1**. Open **`Assets/Era/Scenes/Rift.unity`** and press **Play**. You can also run `Tools/Open-In-Hub.ps1`, `Tools/Open-Unity.ps1`, or choose **ERA → Open game scene** in the editor.

The **420 m wide, flat Stillwater Reach map is saved in the scene**, visible and editable before entering Play mode. Its hierarchy separates terrain, groves, boulders, wildflowers, landmarks and lighting. **ERA → Rebuild Stillwater map** regenerates the map from `WorldEnvironment.cs` / `WorldMap.cs` and replaces the current scene contents; save custom scene edits separately before using that command. Normal builds retain a current-version baked map.

The game creates its cameras, animated heroes, creatures, chests, HUD and audio systems when entering Play mode. Editable, runtime-used hero prefabs are in **`Assets/Era/Resources/Characters`**; creature prefabs are in **`Assets/Era/Resources/Creatures`**. Their joints and materials can be inspected in Prefab mode. No paid assets or third-party packages are required.

## Play without the editor

Run **`Builds/Windows/Era.exe`**. Choose **Play solo**, pick **Ember** or **Echo**, choose a relic, enter a name and select **Begin the journey**.

To share the game, send **`Releases/Era-Windows.zip`** or the **whole `Builds/Windows` folder**. The executable needs its accompanying data folder and DLLs.

## Two-player co-op

1. Run the same game version on both computers in the same **LAN or VPN**.
2. Choose **Play with a companion**. One player opens **Create a lobby**, enters a name, chooses a hero and creates the lobby.
3. The companion opens **Join a lobby**, enters the host IP and a name, then chooses a hero and connects.
4. **Both players press `I'm ready`.** A three-second countdown begins, then the game starts automatically.
5. Selecting **Not ready yet** cancels the countdown. Character changes are available while unready; a player disconnecting also cancels the countdown.

The port is **TCP 7777**. Allow the game on your private network if Windows Firewall asks. Use `127.0.0.1` only for two copies on the same PC; use the host's actual LAN/VPN IP for different computers. There is no split screen, Steam lobby, Relay, host migration or automatic NAT traversal. Joining a run already in progress is not supported.

## Controls

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Space | Jump; hold for a higher arc, release for a short hop |
| Shift | Dash with brief invulnerability |
| Hold Ctrl / C | Crouch and move quietly at 43% speed |
| Q / E | Character abilities |
| R | Use the selected active relic |
| F | Open a nearby chest |
| Hold F | Revive a nearby downed companion (2.5 s), or channel a nearby shrine (5 s) |
| 1 / 2 / 3 | Pick a level-up memory |
| Hold Alt | Show cursor |
| Tab | Map and basic run statistics |
| Esc | Pause / settings menu |
| Alt+F4 | Exit |

Four weapon slots attack automatically. Escape pauses solo play. Every solo level-up pauses the simulation until you choose a memory; queued levels each require a choice. Co-op never pauses for level-ups, including after a companion disconnects. The Tab map keeps the world moving. Switch RU / EN in the menu or Settings; the choice is saved locally and can differ between players.

## The world and progression

Explore **Moss Light Meadows**, **Lavender Grove**, **the Fallen Gardens** and **Amberwilds** across a large flat arena with groves, a moonwell, ruined columns and a waking gate. Trees and large rocks have shared host-side collision. Press Tab for the map, treasure, enemies and both players.

There are **30 chests**. The team begins with **30 shared gold**. Defeated creatures drop gold coins and blue faceted experience crystals. Gold is collected into the team wallet; experience levels up both players. The fantasy HUD uses clean dark frames and gold accents. Four weapon slots sit at the top. Red health appears in the upper-left and above heroes; XP is a blue bar. Abilities use small Q/E/R/Shift icons with cooldown numbers. Personal blessings appear under health. Acquired weapons, memories and blessings show an icon, name and short effect description in a queued toast. The map and basic run statistics are available with Tab; chest count appears in run results.

Press **F** near a chest. Ordinary chests cost **25–45 gold** and give one weapon or memory to the opener. **Moon chests cost 65**, give two copies of a weapon or memory and 10 shared experience. A chest can only be opened and charged once, even if both players interact together. Chest loot rises in an arc, spins, then flies to the opener before equipping. Missing weapon types are prioritized until all four slots are occupied. Duplicate weapons improve the existing slot up to level 5, then convert excess copies into gold. Level-ups offer a personal choice of three memories, each with an original icon. Gold and run progression reset with each new journey.

Every 30 seconds a stronger wave arrives. Wisps hover and sway, stalkers run on four paws, and mushroom brutes lumber after you. At **five minutes**, the antlered Stillwater Warden appears. Leave its marked attack circle before the strike. Defeat the Warden to win.

If your companion falls, approach within 3.5 m and hold F for 2.5 seconds. They have 25 seconds before bleeding out. Reviving restores half health and gives three seconds of protection. Both players downed means defeat. A disconnected client leaves the host able to continue alone; losing the host returns the client to the menu.

## Weapons, heroes, memories and relics

**Weapons**: Crescent Axe cleaves an arc; Lightning Staff chains between up to four enemies; Wildwood Bow fires fast arrows at distant targets; Dawn Spear pierces enemies along a line. All equipped weapons attack independently. Ember starts with a bow, Echo with a lightning staff. Find the remaining three types in chests.

**Ember**: 100 health, Sunburst (Q: 65 base damage in 7 m, 8 s cooldown), Overdrive (E: double fire rate and +30% movement for 6 s, 18 s cooldown).

**Echo**: 115 health, 90% base damage, Frost Bloom (Q: 35 base damage and 3 s freeze in 8 m, 10 s cooldown), Resonance (E: restore 35 health and give 25 shield to both allies within 12 m, 16 s cooldown).

**R relics**: Storm Compass (90 base damage in 9 m / 24 s), Hourglass (4 s freeze in 12 m and refresh dash / 30 s), Home Shard (heal 50 and gather all XP / 25 s).

**Eight stacking memories**: Cinder Heart, Dawn Shard, Copper Coil, Wind Bell, Moon Lantern, Quartz Lens, Living Amber and Satellite Seed. Their upgrade cards show the exact effect before selection. Bolts are capped at five per volley. Experience magnets gather experience only, leaving gold as gold.

## Blessing shrines and crouch

Twelve ancient shrines are spread across the map, in blue, amber and violet variations. Stand within 3 m and **hold F for five uninterrupted seconds**. Release F, leave the radius or become downed to reset the charge. One player channels a shrine at a time; the shrine is spent after granting one random personal blessing. Charging never pauses the world in co-op.

Eight possible blessings: **Skybound** (+1 air jump, up to 3), **Windstride** (+15% movement), **Frenzy** (+20% attack speed), **Might** (+25% damage), **Vitality** (+30 max HP and heal 50), **Stoneguard** (+50 shield), **Quickening** (+15% cooldown recovery) and **Star Magnet** (+3 m pickup radius). Stat blessings last for the run; shield is consumed by damage and decays as normal. Each shrine shows channel progress; a completed shrine produces a visual and sound cue.

Hold **Ctrl or C** to crouch. Movement slows to 43% and the pose and camera blend down; jumping exits crouch. Extra air jumps recharge when landing and are triggered by pressing Space again while airborne. Crouching does not grant invulnerability or a stealth AI mechanic.

## Animation and audio

Heroes and enemies now use textured, skinned **KayKit Adventurers / Skeletons 1.0** models by Kay Lousberg, licensed CC0. Each native Unity prefab includes 20 skeletal clips for idle, walk/run, jump, landing, attacks, casting, hit reaction and death. `SkeletalActor` selects and blends clips, adds a blended crouch pose, and pauses animation with the solo simulation. `WeaponGrip` keeps equipped weapons aligned with animated hands. Defeated creatures play a death clip before sinking away. Foliage sways and shrine seals, loot and crystals animate separately.

Original GLB files, license texts, native meshes/clips and painted texture atlases are in `Assets/Era/Art/KayKit`. See `Docs/THIRD-PARTY.md`. `KayKitImporter` performs an offline conversion into native Unity assets; no external runtime importer is needed. Subtle seamless ground, stone and wood detail textures are authored in `SurfaceTextureBuilder`; materials and foliage wind are applied without replacing the baked world scene.

The original **Stillwater at Dawn** score is composed in code: 16 bars, 88 BPM, stereo pads, plucked bells, bass and soft percussion. The actual WAV asset is in **`Assets/Era/Resources/Audio/Stillwater-at-Dawn.wav`**. `MusicComposer.cs` contains the score and synthesizer. `SynthAudio.cs` supplies footsteps, jumps, landings, spells, shots, pickups, chest opening, level-ups, hurt and menu cues. Music and effects have independent sliders.

## Build and verification

```powershell
.\Tools\Build.ps1
.\Tools\Test-Art.ps1
.\Tools\Test-Solo.ps1
.\Tools\Test-Coop.ps1
.\Tools\Package.ps1
```

`Build.ps1` accepts `-Unity 'D:\Unity\2023.1.22f1\Editor\Unity.exe'`. It prepares map/audio assets and runs **155 simulation, shrine, jump/crouch, weapons, localization, skeletal-asset and TCP checks** before building. `Test-Coop.ps1` runs two actual game processes: both ready up, wait for automatic start, jump, cast Q/E/R, open a chest, collect gold, gain XP and choose an upgrade. Add `-Headless` to skip graphics. Reports and screenshots are in `Artifacts/`.

If Unity is already open, use `.\Tools\Build-Isolated.ps1` to build a source snapshot while retaining the open editor.

`Test-Art.ps1` checks the full five-second shrine channel, random buff, extra air jump, crouch, models, reward descriptions and blue crystals. `Test-Solo.ps1` checks solo pause/resume and four chest-sourced weapons. `Test-Coop.ps1` checks shrine/buff and crouch replication between two actual game processes, plus readiness, movement, abilities, progression and chest loot. Captures and JSON reports are saved under Artifacts.

Useful player arguments: `--solo`, `--host`, `--join`, `--capture-menu`, `--capture-character`, `--capture-tour`. Automated capture/test runs do not change your saved preferences. Keyboard, LAN routing and firewall behavior should also be checked on the two actual target computers; the automated co-op test uses loopback on one PC.

## Source layout

- `WorldEnvironment.cs`, `WorldMap.cs`: baked map, regions, shared height and obstacles.
- `Simulation.cs`, `Simulation.Combat.cs`, `Simulation.Shrines.cs`, `GameData.cs`: authoritative game rules and serializable state.
- `GameApp.cs`, `CoopTransport.cs`: menu/session flow, ready handshake and network.
- `SkeletalActor.cs`, `HeroMotion.cs`, `CreatureMotion.cs`, `WeaponGrip.cs`, `ArenaView.cs`: models, animation and presentation.
- `WorldVfx.cs`, `SurfaceDetail.cs`: blue crystals, animated shrines and surface materials.
- `Assets/Era/Art/KayKit`: source models, painted atlases, native skeletal clips and licenses.
- `GameHud.cs`, `I18n.cs`, `IconLibrary.cs`, `FrameCapture.cs`: bilingual HUD, menus and offscreen capture.
- `MusicComposer.cs`, `SynthAudio.cs`: original soundtrack and effects.
- `Assets/Era/Generated/`: saved map meshes/materials. `Assets/Era/Editor/`: bake/build/check tools.

This is still a prototype: two heroes, four weapon types, eight memories, three relics, twelve shrines and eight blessings. Online matchmaking, gamepads and persistent meta-progression are not implemented. Characters use CC0 skeletal animation clips, with code-driven state selection and a procedural crouch overlay. The host remains authoritative; TCP snapshots suit LAN/VPN, with no local movement prediction for high-latency internet play.
