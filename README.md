# ERA: Seven Realms — v0.10

В v0.10 добавлены конечные волны с передышкой, каменная статуя для призыва босса, четыре испытания по пять элитных стражей с бесплатным сундуком, 72 видеокарты с золотом/опытом, карта диаметром 600 м с 60 сундуками и стеной по границе. Опыт с обычных врагов увеличен примерно втрое. Удары вызывают короткое скольжение, снаряды получили яркие следы, во время боя играет отдельная музыка. Модели героев имеют сглаженную геометрию и взрослые пропорции. Картинки предметов взяты из исходного присланного атласа. В выборе героя информация об оружии/пассивке расположена справа, кнопки — слева; полноэкранный режим включается в настройках.

## Скачать готовую игру для Windows

**[Последний релиз → Assets → Era-Windows.zip](https://github.com/Regardov/Era/releases/latest)**. Распакуйте весь архив и запустите **Era.exe**. Устанавливать Unity не нужно.

Игра доступна всем: регистрация в GitHub и приглашение не нужны. **[Пошаговая инструкция для скачивания и кооператива](Docs/DOWNLOAD-RU.md)**. Кнопка **Code → Download ZIP** скачивает исходники, а готовая игра находится в **Releases**.

В v0.10 каждое открытие сундука добавляет **10% его базовой цены** к ценам оставшихся сундуков команды. Пример для обычного сундука: **25 → 28 → 30 → 33** золота; округление вверх. Счётчик общий для команды и сохраняется между мирами. Новые модели имеют планки, скобы, ручки, обивку и открывающуюся крышку. У Люмена в HUD, Tab и выборе аур показаны **ранг** и **число улучшений**; получение новой ауры не считается улучшением.

![Новое меню, сглаженные герои и описание оружия/пассивки](Docs/Images/hero-lumen-v010.png)

Unity **2023.1.22f1**, Windows. A third-person survivor for **1–3 players** with **seven realms**, seven different bosses, party portals, sculpted terrain, detailed bark/stone materials, reactive grass and the aura support hero **Lumen**. Russian / English interface, original music and sound effects.

## Open in Unity

Open this folder in Unity Hub using **Add → Add project from disk**, then use **2023.1.22f1**. Open **`Assets/Era/Scenes/Rift.unity`** and press **Play**. You can also run `Tools/Open-In-Hub.ps1`, `Tools/Open-Unity.ps1`, or choose **ERA → Open game scene** in the editor.

The **600 m wide first realm with hills is saved in the scene**, visible and editable before Play. The hierarchy separates terrain, chunked bark/foliage/rocks, reactive grass, landmarks and light. **ERA → Rebuild Stillwater map** regenerates it and replaces scene contents; preserve manual scene edits separately. Later realms are generated on entry from the shared stage ID. Only the current realm is active; generated meshes/materials are released when it is replaced.

The game creates its cameras, animated heroes, creatures, chests, HUD and audio systems when entering Play mode. Editable, runtime-used hero prefabs are in **`Assets/Era/Resources/Characters`**; creature prefabs are in **`Assets/Era/Resources/Creatures`**. Their joints and materials can be inspected in Prefab mode. No paid assets or third-party packages are required.

## Play without the editor

Run **`Builds/Windows/Era.exe`**. Choose **Play solo**, pick **Ember**, **Echo** or **Lumen**, choose a relic, enter a name and select **Begin the journey**.

To share the game, send **`Releases/Era-Windows.zip`** or the **whole `Builds/Windows` folder**. The executable needs its accompanying data folder and DLLs.

## Co-op for 2–3 players

1. Run the same v0.10 game version on all computers in the same **LAN or VPN**.
2. Choose **Play co-op · 2–3 players**. One player creates a lobby.
3. One or two friends choose **Join a lobby**, enter the host IP, choose heroes and connect.
4. Wait for the whole party, then **every connected player presses `I'm ready`**. A three-second countdown begins. A party of two can also start.
5. Selecting **Not ready yet** cancels the countdown. Character changes are available while unready; a player disconnecting also cancels the countdown.

The port is **TCP 7777**. Allow the game on your private network if Windows Firewall asks. Use `127.0.0.1` for copies on the same PC; use the host's actual LAN/VPN IP for different computers. There is no split screen, Steam lobby, Relay, host migration or automatic NAT traversal. Joining a run already in progress is not supported. Protocol 9 is incompatible with older builds.

## Controls

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Space | Jump; hold for a higher arc, release for a short hop |
| Shift | Dash with brief invulnerability |
| Hold Ctrl / C | Crouch and move quietly at 43% speed |
| Q / E | Character abilities |
| R | Use the selected active relic |
| F | Enter a nearby party portal, pick up a floor item, or open a chest |
| Hold F | Revive a nearby downed companion (2.5 s), or channel a nearby shrine (5 s) |
| 1–3 / mouse | Choose an accessory; second weapon choices appear at level 25 |
| 1–8 / mouse | Lumen's aura choices (only owned auras after five types) |
| Hold Alt | Show cursor |
| Tab | Map, chest items, rarity odds and all attributes |
| Esc | Pause / settings menu |
| Alt+F4 | Exit |

There are exactly **two weapon slots**: the hero's starting weapon and a second weapon chosen at **level 25**. Equipped attack weapons fire automatically; Lumen's Aura Conduit continuously buffs the party. Escape pauses solo play. Every solo level-up pauses until all its choices are made. Co-op never pauses for upgrades, including after a disconnect. Tab keeps the world moving. Switch RU / EN in the menu or Settings.

## The world and progression

Explore seven large rolling realms. Height, obstacles, chest/shrine positions and spawn ground all use the same deterministic stage-specific terrain function on host and guests. The arrival clearing is flat; hills and ridges surround it. Trees have tapered branching trunks, roots and layered crowns; rocks use fractured 512px albedo/normal maps and moss on top. Grass covers the realm in chunked meshes and bends away from the feet of all three players, then rises when they move or jump away. Distant grass is culled at 85 m. Press Tab for the map, treasure, enemies and party.

There are **60 chests**, **30 starting shared gold**, and shared XP. The **level and XP bar are top-center**, above the two weapon slots. Health and both teammates appear on the left. Every successful hit displays damage; critical hits use larger gold numbers. Enemies flash and react to hits. Tab shows the map, items, accessory stacks, rarity odds and all **21 stats**.

Press **F** near a chest. Ordinary chests cost **25–45 gold**; **Moon chests cost 65** and have better rarity odds. A chest can only be opened once. Its actual item immediately appears on the floor and remains there until **any living player approaches within 2.5 m and presses F**. The first claim wins; a collector falling or disconnecting releases the item. Chests contain **31 items** in four rarity tiers and never unlock extra weapons. Luck improves rarity rolls.

Every level offers three **accessories** from a pool covering **21 stats**: damage, attack speed, critical chance/damage, max HP, healing, regeneration, shield, armor, lifesteal, movement, evasion, recharge speed, range, area, projectile count/speed, pickup range, luck, XP and gold gain. Examples: **Precision Ring**, **Life Amulet**, **Merchant's Purse**. Rarity sets the bonus: legendary Life Amulet gives +40 max HP; epic Precision Ring gives +15% crit chance. Accessories stack for the run. The level-25 weapon choice is additional to that level's accessory and remains queued correctly after multi-level XP gains.

Waves have a finite spawn quota. Clear every wave enemy to begin an eight-second break, then face a stronger wave. The realm boss appears only when a living player presses **F at the stone boss statue**; elapsed time never summons it automatically. Enemy HP/damage and boss strength increase with realm number and party size. Attacks have visible warnings; jump or leave the danger zone before impact.

| Realm | Boss | Signature attack |
|---|---|---|
| Mistveil Meadows | Grove Warden | Delayed ground slam |
| Amberwood | Antlered Executioner | A line of fiery impacts |
| Azure Highlands | Glacial Oracle | Frost nova and aimed ice zones |
| Cinder Crags | Forge Colossus | Cross-shaped eruptions |
| Twilight Mire | Mire Matriarch | Persistent poisonous pools |
| Stormpass Citadel | Storm Paladin | Sequential lightning strikes |
| Heart of the Rift | Rift Sovereign | Radial ruptures and summoned stalkers |

After bosses 1–6 die, the remaining enemies disappear and a **portal opens at the boss's position**. Any living player within 3.5 m can press **F** to move the **whole party** into the next realm. Gear, accessories, aura ranks, XP, gold and queued level choices are preserved. Everyone arrives together, healed and revived, with brief protection. Treasure, shrines, enemies and the stage timer reset. **Only the seventh boss completes the run.**

If a companion falls, approach within 3.5 m and hold F for 2.5 seconds. They have 25 seconds before bleeding out. Reviving restores half health and gives three seconds of protection. Everyone downed means defeat. A disconnected guest leaves the remaining party able to continue; losing the host returns guests to the menu. Enemy health and spawn counts scale with party size.

## Weapons, heroes, chest items and relics

**Weapons**: Crescent Axe cleaves an arc; Lightning Staff chains between enemies; Wildwood Bow fires arrows; Dawn Spear pierces in a line. Ember starts with a bow, Echo with a staff, Lumen with an Aura Conduit. At level 25 choose one different attack weapon. Book of Knowledge upgrades an equipped weapon up to level 5.

**Ember**: 100 health, Sunburst (Q: 65 base damage in 7 m, 8 s cooldown), Overdrive (E: double fire rate and +30% movement for 6 s, 18 s cooldown).

**Echo**: 115 health, 90% base damage, Frost Bloom (Q: 35 base damage and 3 s freeze in 8 m, 10 s cooldown), Resonance (E: restore 35 health and give 25 shield to both allies within 12 m, 16 s cooldown).

**Lumen**: 110 HP, an animated ivory-and-gold mage with a jeweled halo. Aura radius is **12 / 1.3 ≈ 9.23 m**, exactly 1.3 times smaller than v0.7. The conduit deals **10 base damage per second** to enemies in range in four pulses, scaling with damage/crit/item modifiers. Starts with **Fortune Aura**. Every level grants an accessory and **one choice: upgrade an owned aura OR add a new buff aura**. Keep up to **five different types**. Repeated choices add 20% of base buff per rank; after five types only those five can be upgraded. Auras affect the bearer and living allies, scale with range/area/conduit level, disappear outside range or when Lumen falls/disconnects, and use the strongest overlapping source.

| Aura | Rank 1 bonus |
|---|---|
| Fortune | +15% epic/legendary rarity weights |
| Might | +12% damage |
| Haste | +12% attack speed |
| Vitality | +15% maximum HP; does not heal on re-entry |
| Wind | +10% movement speed |
| Protection | 10% incoming damage reduction |
| Quickening | +12% ability recharge speed |
| Wisdom | +15% XP gained from kills |

Lumen's **Q** deals 45 damage in 8 m and heals allies for 20 (8 s cooldown). **E** increases buff strength by 50% and radius by about 4.6 m for 6 s (18 s cooldown).

**R relics**: Storm Compass (90 base damage in 9 m / 24 s), Hourglass (4 s freeze in 12 m and refresh dash / 30 s), Home Shard (heal 50 and gather all XP / 25 s).

**Thirty-one stacking chest items** cover stats, regeneration, lifesteal, projectiles and special powers. Every item/accessory uses an individually authored, centered transparent **128×128 icon**, shared by floor loot, cards, rewards and inventory. Projectile count is capped at twelve.

## Blessing shrines and crouch

Twelve ancient shrines are spread across the map, in blue, amber and violet variations. Stand within 3 m and **hold F for five uninterrupted seconds**. Release F, leave the radius or become downed to reset the charge. One player channels a shrine at a time; the shrine is spent after granting one random personal blessing. Charging never pauses the world in co-op.

Eight possible blessings: **Skybound** (+1 air jump, up to 3), **Windstride** (+15% movement), **Frenzy** (+20% attack speed), **Might** (+25% damage), **Vitality** (+30 max HP and heal 50), **Stoneguard** (+50 shield), **Quickening** (+15% cooldown recovery) and **Star Magnet** (+3 m pickup radius). Stat blessings last for the run; shield is consumed by damage and decays as normal. Each shrine shows channel progress; a completed shrine produces a visual and sound cue.

Hold **Ctrl or C** to crouch. Movement slows to 43% and the pose and camera blend down; jumping exits crouch. Extra air jumps recharge when landing and are triggered by pressing Space again while airborne. Crouching does not grant invulnerability or a stealth AI mechanic.

## Animation and audio

All three heroes and enemies use textured, skinned **KayKit Adventurers / Skeletons 1.0** models by Kay Lousberg (CC0). Lumen has a new ivory/gold palette and halo; seven bosses have different bases, horns, crowns, armor, crystals or appendages. Each prefab retains skeletal clips. `SkeletalActor` blends jump launch, rising tuck, descending balance and a soft landing compression, with a subtle squash/stretch overlay; it respects solo pause. `WeaponGrip` aligns hand weapons. Defeated enemies play their death clip. Cloth/skin uses a soft rim shader. Foliage, grass, shrine seals and portal surfaces animate separately.

Original GLB files, license texts, native meshes/clips and painted texture atlases are in `Assets/Era/Art/KayKit`. See `Docs/THIRD-PARTY.md`. `KayKitImporter` performs an offline conversion into native Unity assets; no external runtime importer is needed. Subtle seamless ground, stone and wood detail textures are authored in `SurfaceTextureBuilder`; materials and foliage wind are applied without replacing the baked world scene.

The original **Stillwater at Dawn** score is composed in code: 16 bars, 88 BPM, stereo pads, plucked bells, bass and soft percussion. The actual WAV asset is in **`Assets/Era/Resources/Audio/Stillwater-at-Dawn.wav`**. `MusicComposer.cs` contains the score and synthesizer. `SynthAudio.cs` supplies footsteps, jumps, landings, spells, shots, pickups, chest opening, level-ups, hurt and menu cues. Music and effects have independent sliders.

## Build and verification

```powershell
.\Tools\Build.ps1
.\Tools\Test-Update.ps1
.\Tools\Test-Chests.ps1
.\Tools\Test-Campaign.ps1
.\Tools\Test-Art.ps1
.\Tools\Test-Solo.ps1
.\Tools\Package.ps1
```

`Build.ps1` accepts `-Unity 'D:\Unity\2023.1.22f1\Editor\Unity.exe'`. It prepares assets and runs simulation, aura/range/removal, queued level-25 choices, accessory, loot-claim, weapon-limit, localization and three-party TCP checks before building. The exact assertion count is recorded in `Artifacts/editor-checks.json`.

If Unity is already open, use `.\Tools\Build-Isolated.ps1` to build a source snapshot while retaining the open editor.

`Test-Art.ps1` checks shrines, extra jump, crouch, models and reward descriptions. `Test-Solo.ps1` checks pause/resume, accessories and manual floor pickup. The optional `Test-Coop.ps1 -ThreeProcesses` launches **three actual game processes** and defaults to headless mode; `-Graphics` explicitly enables rendered instances. Without `-ThreeProcesses` it exits before launching games. Test mode is limited to 20 FPS and a reduced quality preset. It checks player identities, readiness/start, movement, abilities, upgrades without pause, allied loot pickup, five Lumen auras and second weapons after level 25. Reports are in `Artifacts/`. On limited hardware, rely on the build's in-process simulation/TCP checks and test co-op on separate computers.

`Build.ps1` uses two worker threads, at most two CPU cores and a lower process priority. It detects a Unity shutdown stall only after the log confirms a successfully saved build and shutdown.

`Test-Chests.ps1` launches one game at 800×450 / 20 FPS on two CPU cores. It checks rising team prices, both chest models, the lid animation, aura ranks/upgrade counts, the aura-choice screen, inventory and snapshot serialization. It saves four screenshots and `Artifacts/smoke-chests.json`. Generated world mesh libraries are stored as compact binary assets; code, prefabs and scenes use text serialization. No Git LFS setup is required.

`Test-Update.ps1` checks the v0.10 encounter flow in one game process: finite waves, the statue, five elite guards, a free chest opened by an ally, both GPU drops, short knockback, battle music, smooth heroes and the stone perimeter. It also captures the character menu and fullscreen controls. Latest validation: [478 assertions and both single-process scenarios](Docs/VALIDATION-v0.10.md).

`Test-Campaign.ps1` uses **one game process** at 800×450, 20 FPS and two CPU cores. It exercises seven realms and six portals with three authoritative player states, preserves builds/revives allies, checks the skeletal Lumen, grass interaction uniforms and shaders, and captures every biome/boss/portal. It does not create three game instances. Useful arguments: `--solo`, `--host`, `--join`, `--capture-lumen`, `--capture-auras`, `--capture-milestone`, `--smoke-campaign`. Automated runs do not change preferences. Separate-computer LAN/firewall/input testing is still manual.

## Source layout

- `WorldEnvironment.cs`, `WorldMap.cs`: baked map, regions, shared height and obstacles.
- `Simulation.cs`, `Simulation.Combat.cs`, `Simulation.Auras.cs`, `Simulation.Campaign.cs`, `Simulation.Shrines.cs`, `GameData.cs`, `CampaignData.cs`: rules and serializable state.
- `GameApp.cs`, `CoopTransport.cs`: menu/session flow, ready handshake and network.
- `SkeletalActor.cs`, `HeroMotion.cs`, `CreatureMotion.cs`, `WeaponGrip.cs`, `ArenaView.cs`: models, animation and presentation.
- `SceneryBuilder.cs`, `LivingGrass.cs`, realm shaders: chunked sculpted world, textured trees/rocks, reactive meadow.
- `ActorArtBuilder.cs`, `BossArt.cs`: refined hero assets and seven boss identities.
- `HumanArtBuilder.cs`, `HumanRigShape.cs`: smooth adult proportions, skin weights, bind poses and clip retargeting.
- `Simulation.Encounters.cs`, `EncounterView.cs`: finite waves, boss statue, four elite trials and resource graphics cards.
- `Assets/Era/Art/KayKit`: source models, painted atlases, native skeletal clips and licenses.
- `GameHud.cs`, `I18n.cs`, `IconLibrary.cs`, `FrameCapture.cs`: bilingual HUD, menus and offscreen capture.
- `MusicComposer.cs`, `SynthAudio.cs`: original soundtrack and effects.
- `Assets/Era/Generated/`: saved map meshes/materials. `Assets/Era/Editor/`: bake/build/check tools.

This is a prototype: seven realms/bosses, three heroes, four attack weapon types plus an aura conduit, 31 chest items, 21 accessories/stats, eight auras, three relics and eight blessings. Online matchmaking, gamepads and persistent meta-progression are not implemented. All heroes use CC0 skeletal clips with authored overlays and original decoration. The host remains authoritative; TCP snapshots suit LAN/VPN, with no local prediction for high latency.
