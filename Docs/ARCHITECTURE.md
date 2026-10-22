# v0.9 architecture

ChestState stores immutable baseCost and authoritative cost. After an atomic shared-wallet purchase, openedChests increases and every unopened chest is repriced using ceil(baseCost × (1 + openedChests / 10)). Failed purchases do not mutate prices; portal population uses the cumulative counter. The opened chest retains the amount actually paid. Protocol 8 carries these prices and PlayerState.currentAura. Aura rank minus one is its upgrade count, excluding initial acquisition; currentAura identifies the last selected buff, while every owned aura stays active.

ChestArt builds hollow beveled plank coffers with separate animated barrel lids, arched bands, rivets, side handles, feet, lining and an inset jewel. Six mesh renderers plus a treasure marker per chest share cached meshes/materials for each of two variants. WorldAssetLibrary uses PreferBinarySerialization for large generated meshes; scenes, prefabs and code remain text. Every generated file fits GitHub's per-file limit without Git LFS.

Only the host runs Simulation at 30 Hz. Clients send movement, action and choice intent. The host owns collision, combat, RNG, XP, offers, loot claims, auras and results. Snapshots go to all guests at 20 Hz.

## Session and transport

CoopTransport supports one host and two guests on TCP 7777. Protocol 8 uses length-prefixed UTF-8 JSON (512 KiB maximum). Each connection has its own background receive/send threads, bounded command queue and replaceable snapshot slot. Every event retains a unique connection ID. GameApp maps authenticated-by-connection hello messages to available player IDs 1 and 2, and sends a targeted welcome message. Input/ready/loadout always use that mapping, never an ID supplied by the client. A guest disconnect removes only that player. Malformed guest data cannot close the whole host session.

A lobby can start with 2 or 3 players. All present players must ready up; joining, leaving or changing loadout cancels readiness. After three seconds the host starts the run. Late joins are rejected. Solo can start immediately. Public matchmaking, reconnect, host migration, split screen and local movement prediction are not implemented.

## Weapons and progression

Every PlayerState has two weapon entries, two levels and two independent timers. GiveStarterWeapon assigns Bow to Ember, LightningStaff to Echo and Aura to Lumen. A new weapon cannot enter the second slot before world level 25, and a third weapon is rejected. Chests never grant weapons. Book of Knowledge upgrades an existing slot, up to level 5.

Shared XP increments pendingLevels. completedLevel identifies the next unconsumed level, preserving milestones when several levels are gained before selecting. The staged offer sequence is: second weapon at level 25, accessory, then Lumen aura. Choosing a weapon does not consume the accessory; choosing an accessory does not consume Lumen's aura. Only the final stage decrements pendingLevels and increments completedLevel. Three accessory offers each roll rarity. Lumen can choose all eight aura types until owning five, then only upgrade those five. Input keys 1–8 and mouse use the same validated host choice operation.

There are 21 attribute definitions and matching accessory names/icons. accessories stores 21 × 4 rarity counters; attributes stores total upgrades per stat. Base stats remain separate from temporary aura multipliers. Shield accessories grant consumable shield, just like other shield sources.

Solo UpgradePaused processes choices before its early return and freezes the complete world. Multiplayer state remains unpaused after guests leave. Networking and menu input use unscaled time.

## Auras and combat feedback

Simulation.Auras recomputes eight aura bonuses from live Lumen sources each tick. Sources need their Aura weapon and a live target within AuraRadius. Identical aura types use the strongest source. Max-HP contributions are removed before recalculating from base HP, avoiding compounding. Leaving range, becoming downed and source disconnects remove temporary bonuses; re-entry never heals. Convergence temporarily increases power and radius. Auras apply to damage, attack cadence, max HP, movement, damage mitigation, recharge, rarity weights and XP reward calculation.

DamageEnemy validates that the target is still alive and present, resolves critical/item modifiers, emits a HitState, increments hitSerial and resolves kill rewards exactly once. Necromancer spirits use the same damage path. Lifesteal uses actual health lost. HitState persists for 0.9 seconds, so lethal hits still display numbers. ArenaView uses hitSerial for hit animations and a temporary material-property flash. GameHud projects floating damage numbers and highlights criticals. Clients render the host's resolved values.

## Chest and floor loot authority

TryOpenChest atomically spends shared gold, opens one chest and rolls one rarity-matched item from 31 definitions. Loot appears at a resolved floor position immediately. owner = -1 means unclaimed. Pressing F within 2.5 m reserves the closest unclaimed item for any live player. Further claims fail. A 0.35-second collection animation ends with the host applying the item and a localized reward notice. Falling or disconnecting before collection releases the item. Items never auto-deliver to the opener and never expire while on the floor.

## Presentation and editable assets

Rift.unity contains the baked 420 m sculpted first-realm map, groves, rocks, flowers, landmarks and lighting. WorldMap supplies shared height/collision queries. Normal builds preserve the current baked world; ERA/Rebuild Stillwater map intentionally replaces it.

Resources/Characters and Resources/Creatures contain editable runtime prefabs. Ember, Echo and enemies use CC0 KayKit skinned models and native clips; Lumen now uses a refined skeletal mage with an ivory/gold palette and jeweled halo. HeroMotion and CreatureMotion retain procedural fallback rigs. SkeletalActor drives clips; WeaponGrip aligns hand weapons. ArenaView creates aura rings, item pictures, floor halos, camera smoothing and hit feedback.

IconBuilder generates individually centered transparent 128×128 PNGs under Resources/Icons. GameData.ItemIcons, AccessoryIcons and AuraIcons map every definition to a complete texture; no atlas UV cropping is used. Item pictures, cards, inventory and notifications share these icons. The old supplied atlas files remain available as source assets but are not used by the HUD.

GameHud uses 1600×900 reference coordinates. Level/XP are top-center above two weapon slots; both teammates appear on the left, abilities at the bottom. Tab shows the map, items and 21 attribute/accessory tiles. I18n keeps language local. RewardNotice contains structured IDs. MusicComposer and SynthAudio provide original music and effects.

## Validation

Build.ps1 prepares assets, executes SimulationChecks and builds Windows. editor-checks.json records the exact count. Checks cover queued milestones, all accessory stacks, aura limits/range/removal, HP non-compounding, lethal damage feedback, chest claims, three-player limits, serialization and two simultaneous TCP guests with targeted/broadcast messages.

Test-Coop.ps1 requires the explicit -ThreeProcesses switch before launching host plus two guests; it defaults to headless mode and tests readiness, abilities, upgrades, loot, auras, shrines and progression. Test-Solo.ps1 checks pause/resume and manual loot pickup. Test-Art.ps1 checks models, shrines and animation. Diagnostic game sessions are capped at 20 FPS and reduced quality; Build.ps1 limits Unity to two CPU cores and lower priority. FrameCapture renders actual camera/HUD screenshots from hidden windows. Test modes do not save preferences. LAN/firewall behavior on separate computers still needs manual testing.

## Seven-realm campaign

WorldState carries stage (1–7), stageSerial, stageElapsed, portal and hazards. Total elapsed time, player level, gold and builds span the whole campaign. Each stage spawns its boss at 150 seconds. Normal enemies scale with stage and party size; each boss has its own prefab/identity and stronger HP/damage. BossAttack schedules delayed hazards with stage-specific patterns, radius, damage and lifetime; pools tick over time. Host snapshots serialize all warnings and impacts.

Defeating bosses 1–6 clears threats and opens a portal at their resolved ground position. A live player within 3.5 m presses F to atomically advance the entire party. TryUsePortal increments stageSerial, preserves builds/queued offers, revives/heals all players, resets motion and regenerates treasure/shrines. Only stage 7 sets Victory. GameApp sets the received WorldMap stage before client presentation. ArenaView detects stage changes, clears old actors/loot, replaces the environment and rebuilds the new realm. No seven-scene preload is required.

WorldMap.Height is deterministic per stage, blends hills/ridges with flat arrival/landmark clearings, and supplies terrain vertices, collision height, pickups, actors and portals. SceneryBuilder emits shared chunk meshes for terrain, tapered trees/roots, layered crowns, weathered rocks and full-map grass. LivingGrass culls patches beyond 85 m and supplies three player-foot uniforms. MeadowGrass bends blade tips away from nearby feet while pinning roots; height attenuates bending during jumps. Shared 512px surface/normal maps provide bark grooves, stone fractures and leaf grain. Runtime-generated realm meshes/materials are released on replacement.

ActorArtBuilder saves three skeletal heroes and seven decorated boss prefabs. Actor Satin preserves painted atlases with a soft rim and Lumen palette remap. SkeletalActor overlays jump launch/tuck/descent, landing compression, balance and subtle squash/stretch on the real clips. Animation pauses with solo gameplay.

Lumen's complete aura radius is divided by 1.3. TickAuraDamage accumulates fixed-step time and emits four 2.5-base-damage pulses per second in range; modifiers use the shared DamageEnemy path. Each level still offers an accessory and one aura decision: improve an owned rank or add a buff type, capped at five types.

Test-Campaign.ps1 launches ONE reduced-quality game process (800x450, 20 FPS, one job worker, two CPU cores), exercises three player states through seven bosses and six portals, and captures realm/portal/jump frames. It complements in-process simulation and lightweight TCP checks; separate-PC network testing remains manual.
