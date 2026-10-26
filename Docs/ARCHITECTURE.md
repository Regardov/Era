# v0.14 architecture

## Roguelike items and enemy roles

ItemCatalog contains 129 localized definitions with compact IDs after removal of Vampire Fang; protocol 13 separates them from older clients. IconLibrary maps the first 30 remaining items to their original atlas cells, skipping the removed picture. Serialized rarity values remain compatible with previous code, while RarityOrder maps them to the ascending 1..5 ranks used in the supplied logarithmic luck formula. One probability function supplies both rolls and HUD odds. Accessory rolls exclude the mythic tier and renormalize the four stat tiers; the uncommon/rare stat values now follow rarity strength.

Simulation.Items owns pickup, volley, hit, kill, hurt, dodge, dash, landing and ability hooks. Persistent item timers/counters belong to each player. Afflictions retain source-player ownership; independent layers of burn, poison, bleed, chill and shock are simulated by the host. Item-generated secondary damage cannot recursively generate on-hit effects. Queued echoes, moving spirits, mines, vortices, roots and turrets are explicit ItemFieldState entities. Stage travel clears fields and weapon queues but retains earned legendary progression.

All healing goes through Heal, including regeneration, lifesteal, shrines and ability heals; Inversion Heart can redirect it into shield. Revives and full realm-travel restoration explicitly restore life. Damage/debt settlement share a single lethal-damage resolver. Safety limits bound projectiles per volley, fields per player, enemies, allied summons and recurring proc chains.

Simulation.Weapons keeps the complete requested projectile count even when fewer targets exist. A per-volley target history prioritizes distinct enemies. Bow projectiles steer and retarget; each base weapon strike damages one target. Ricochet Seed alone adds distinct targets for arrows and staff beams, at 65% damage per bounce, capped at four. Returning arrows cannot damage. Sword/spear/axe queues retain remaining shots, aim history and the requested geometric interval; sub-frame intervals consume all due shots. Host snapshots include pending queues and delayed effects.

EnemyCatalog defines role-specific health, speed, reach, radius and flight. Simulation.Enemies handles aiming, windup, locked attack direction, recovery, melee hitboxes, summoning, slime division and flying dives. CreatureMotion animates the same windup/recovery state presented by attack warnings. RoguelikeArtBuilder saves original procedural models as editable prefabs and combines static surfaces per material and moving joint. FacetedArtBuilder preserves the three hero silhouettes with more surface geometry and costume/face/hand detail.

GameHud retains the map and stats in Tab, adds a five-page full item catalogue in Tab and the main menu, an owned-only filter, and defers all tooltips until all underlying panels are drawn. Owned-item pages remove the old ten-item display limit.

Simulation.Encounters owns continuous nearby spawning, minute hordes, a ten-minute countdown, the one-shot boss statue, four elite trials and 72 breakable autumn leaf piles. Enemies carry a wave ID and optional eliteShrine ID. Only the five deaths belonging to an active trial complete it; a completed trial creates one free chest. The boss summon refuses while an elite trial is active, preventing threat cleanup from completing trials for free. Leaf damage is authoritative; the host rolls gold or XP with equal odds only on destruction, once per pile. Visual palette/mesh and HUD labels do not read the loot kind. Protocol 15 carries all encounter state and the short knockback vector/timer.

WorldMap has radius 300 m, 240 starting chests and 350 props. Movement clamps six meters inside the arena radius, before the continuous visible stone wall. Generated geometry is saved in several mesh libraries with a conservative 24 MB estimated budget per chunk. FacetedArtBuilder restores the untouched pre-smoothing rigs, constructs original flat-shaded costumes from a per-face vertex-color palette, and saves one skinned mesh per new character with updated bind poses. The ranger, monk, aura keeper, wolf and jester use the supplied visual references; CC0 animations remain intact. HumanRigShape retargets local bind lengths after SkeletalActor and before WeaponGrip; hips retain animation deltas while the neck and head stay attached. Neck skin blends chest/head bones; faceted hair caps and tapered locks are authored into each hero mesh. The supplied item-atlas.png is imported at 1536×1024 without NPOT resizing; the same precise regions feed HUD icons and floor sprites. Menu and battle audio crossfade between separate original scores.

ChestState stores immutable baseCost and authoritative cost. After an atomic shared-wallet purchase, openedChests and chestsOpenedInStage increase and every unopened chest is repriced using recursive ceil(previousPrice × 5 / 3). Failed purchases do not mutate prices; portal travel resets only the realm statistics while openedChests continues to drive cumulative prices. The opened chest retains the amount actually paid. Protocol 15 carries these prices and PlayerState.currentAura. Aura rank minus one is its upgrade count, excluding initial acquisition; currentAura identifies the last selected buff, while every owned aura stays active.

ChestArt builds hollow beveled plank coffers with separate animated barrel lids, arched bands, rivets, side handles, feet, lining and an inset jewel. Six mesh renderers plus a treasure marker per chest share cached meshes/materials for each of two variants. WorldAssetLibrary uses PreferBinarySerialization for large generated meshes; scenes, prefabs and code remain text. Every generated file fits GitHub's per-file limit without Git LFS.

Only the host runs Simulation at 30 Hz. Clients send movement, action and choice intent. The host owns collision, combat, RNG, XP, offers, loot claims, auras and results. Snapshots go to all guests at 20 Hz.

## Session and transport

CoopTransport supports one host and two guests on TCP 7777. Protocol 15 uses length-prefixed UTF-8 JSON (1 MiB maximum). Each connection has its own background receive/send threads, bounded command queue and replaceable snapshot slot. Every event retains a unique connection ID. GameApp maps authenticated-by-connection hello messages to available player IDs 1 and 2, and sends a targeted welcome message. Input/ready/loadout always use that mapping, never an ID supplied by the client. A guest disconnect removes only that player. Malformed guest data cannot close the whole host session.

A lobby can start with 2 or 3 players. All present players must ready up; joining, leaving or changing loadout cancels readiness. After three seconds the host starts the run. Late joins are rejected. Solo can start immediately. Public matchmaking, reconnect, host migration, split screen and local movement prediction are not implemented.

## Weapons and progression

Every PlayerState has two weapon entries, two levels and two independent timers. GiveStarterWeapon assigns Bow to Ember, LightningStaff to Echo and Aura to Lumen. A new weapon cannot enter the second slot before world level 25, and a third weapon is rejected. Chests never grant weapons. Book of Knowledge upgrades an existing slot, up to level 5.

Shared XP increments pendingLevels. completedLevel identifies the next unconsumed level, preserving milestones when several levels are gained before selecting. The staged offer sequence is: second weapon at level 25, accessory, then Lumen aura. Choosing a weapon does not consume the accessory; choosing an accessory does not consume Lumen's aura. Only the final stage decrements pendingLevels and increments completedLevel. Three accessory offers each roll rarity. Lumen sees three distinct random aura cards, drawn from all eight types until owning five, then only from those five owned upgrades. Input keys 1–8 and mouse use the same validated host choice operation.

There are 21 attribute definitions and matching accessory names/icons. accessories stores 21 × 4 rarity counters; attributes stores total upgrades per stat. Base stats remain separate from temporary aura multipliers. Shield accessories grant consumable shield, just like other shield sources.

Solo UpgradePaused processes choices before its early return and freezes the complete world. Multiplayer state remains unpaused after guests leave. Networking and menu input use unscaled time.

## Auras and combat feedback

Simulation.Auras recomputes eight aura bonuses from live Lumen sources each tick. Sources need their Aura weapon and a live target within AuraRadius. Identical aura types use the strongest source. Max-HP contributions are removed before recalculating from base HP, avoiding compounding. Leaving range, becoming downed and source disconnects remove temporary bonuses; re-entry never heals. Convergence temporarily increases power and radius. Auras apply to damage, attack cadence, max HP, movement, damage mitigation, recharge, rarity weights and XP reward calculation.

DamageEnemy validates that the target is still alive and present, resolves critical/item modifiers, emits a HitState, increments hitSerial and resolves kill rewards exactly once. Necromancer spirits use the same damage path. Weapon-hit lifesteal rolls a chance independently of damage: one base HP through 100%, then +1 per 50 excess percentage points (upward tiers). Secondary damage does not proc it. Evasion gains apply to remaining probability and cap at 95%; evaded HitState entries display a localized notice. HitState persists for 0.9 seconds, so lethal hits still display numbers. ArenaView uses hitSerial for hit animations and a temporary material-property flash. GameHud projects floating damage numbers and highlights criticals. Clients render the host's resolved values.

## Chest and floor loot authority

TryOpenChest atomically spends shared gold, opens one chest and rolls one rarity-matched item from 129 definitions. Loot appears at a resolved floor position immediately. owner = -1 means unclaimed. Pressing F within 2.5 m reserves the closest unclaimed item for any live player. Further claims fail. A 0.35-second collection animation ends with the host applying the item and a localized reward notice. Falling or disconnecting before collection releases the item. Items never auto-deliver to the opener and never expire while on the floor.

## Presentation and editable assets

Rift.unity contains the baked 600 m sculpted first-realm map, groves, rocks, flowers, landmarks and lighting. WorldMap supplies shared height/collision queries. Normal builds preserve the current baked world; ERA/Rebuild Stillwater map intentionally replaces it.

Resources/Characters and Resources/Creatures contain editable runtime prefabs. Heroes use original faceted skinned geometry on CC0 KayKit rigs and clips. Lumen has ivory hair, a beard and layered robes; new creatures use original jointed geometry. HeroMotion and CreatureMotion retain procedural fallback rigs. SkeletalActor drives clips; WeaponGrip aligns hand weapons. ArenaView creates aura rings, item pictures, floor halos, camera smoothing and hit feedback.

The 30 remaining original item pictures retain their supplied atlas UVs. New item definitions use complete centered transparent icons from Resources/Icons. IconLibrary handles both paths for HUD and floor sprites; sprite caches cover all 129 IDs.

GameHud uses 1600×900 reference coordinates. Level/XP are top-center above two weapon slots; both teammates appear on the left, abilities at the bottom. Tab shows the map, items and 21 attribute/accessory tiles. I18n keeps language local. RewardNotice contains structured IDs. MusicCatalog maps the menu and seven realms to eight streamed scores; SynthAudio crossfades two music sources and supplies synthesized effects. Editable scores and offline music rendering live in ArtSource/Music.

## Validation

Build.ps1 prepares assets, executes SimulationChecks and builds Windows. editor-checks.json records the exact count. Checks cover queued milestones, all accessory stacks, aura limits/range/removal, HP non-compounding, lethal damage feedback, chest claims, three-player limits, serialization and two simultaneous TCP guests with targeted/broadcast messages.

Test-Coop.ps1 requires the explicit -ThreeProcesses switch before launching host plus two guests; it defaults to headless mode and tests readiness, abilities, upgrades, loot, auras, shrines and progression. Test-Solo.ps1 checks pause/resume and manual loot pickup. Test-Balance.ps1 captures the main-menu catalogue, standing/crouching heroes, shield/evade notice and foreground stat tooltip. Test-Art.ps1 checks models, shrines and animation. Diagnostic game sessions are capped at 20 FPS and reduced quality; Build.ps1 limits Unity to two CPU cores and lower priority. FrameCapture renders actual camera/HUD screenshots from hidden windows. Test modes do not save preferences. LAN/firewall behavior on separate computers still needs manual testing.

## Seven-realm campaign

WorldState carries stage (1–7), stageSerial, stageElapsed, portal and hazards. Total elapsed time, player level, gold and builds span the whole campaign. Each stage exposes a stone statue; only a validated nearby interaction summons its boss. There is no automatic boss timer. Normal enemies scale with stage and party size; each boss has its own prefab/identity and stronger HP/damage. BossAttack schedules delayed hazards with stage-specific patterns, radius, damage and lifetime; pools tick over time. Host snapshots serialize all warnings and impacts.

Defeating bosses 1–6 clears threats and opens a portal at their resolved ground position. A live player within 3.5 m presses F to atomically advance the entire party. TryUsePortal increments stageSerial, preserves builds/queued offers, revives/heals all players, resets motion and regenerates treasure/shrines. Only stage 7 sets Victory. GameApp sets the received WorldMap stage before client presentation. ArenaView detects stage changes, clears old actors/loot, replaces the environment and rebuilds the new realm. No seven-scene preload is required.

WorldMap.Height is deterministic per stage, blends hills/ridges with flat arrival/landmark clearings, and supplies terrain vertices, collision height, pickups, actors and portals. SceneryBuilder emits shared chunk meshes for terrain, tapered trees/roots, layered crowns, weathered rocks and full-map grass. LivingGrass culls patches beyond 85 m and supplies three player-foot uniforms. MeadowGrass bends blade tips away from nearby feet while pinning roots; height attenuates bending during jumps. Shared 512px surface/normal maps provide bark grooves, stone fractures and leaf grain. Runtime-generated realm meshes/materials are released on replacement.

ActorArtBuilder saves three skeletal heroes and seven decorated boss prefabs. Actor Satin preserves painted atlases with a soft rim and Lumen palette remap. SkeletalActor overlays jump launch/tuck/descent, landing compression, balance and subtle squash/stretch on the real clips. Animation pauses with solo gameplay.

Lumen's complete aura radius is divided by 1.3. TickAuraDamage accumulates fixed-step time and emits four 2.5-base-damage pulses per second in range; modifiers use the shared DamageEnemy path. Each level still offers an accessory and one aura decision: improve an owned rank or add a buff type, capped at five types.

Test-Campaign.ps1 launches ONE reduced-quality game process (800x450, 20 FPS, one job worker, two CPU cores), exercises three player states through seven bosses and six portals, and captures realm/portal/jump frames. It complements in-process simulation and lightweight TCP checks; separate-PC network testing remains manual.

HudTextLayout measures glyphs and wraps words once in the fixed 1600 x 900 design canvas. GameHud renders the resulting lines without IMGUI auto-wrap; FrameCapture consumes the same layout. Long tokens wrap by character, explicit paragraph breaks survive, and font size adjusts to available height. Screen scaling cannot choose different line breaks.

Enemy HP and boss HP use 1.5^(stage-1), with existing wave and party multipliers. Character levels do not affect this multiplier. EnemyCatalog.EffectImmune marks bosses: Afflict and FreezeEnemy reject statuses, TickAfflictions clears stale data, pull fields skip bosses and hit presentation skips stagger clips. Direct damage still resolves normally. GrantGold credits the shared wallet and emits the normal item pickup hook without a floor entity. Gold leaf rewards and enemy deaths use that path; legacy gold pickups are converted on the next simulation tick from any distance. XP retains ground collection. The HUD has no crosshair; boss health uses a numerical bar and immunity label.

## Passive progression and autumn scenery (v0.22)

GrantExperience increments PlayerState.levelUps once per shared level, including downed allies and queued levels. Ember gains .03 critChance immediately. Echo converts levelUps * 10 percentage points into guaranteed lightning transitions plus one Bernoulli remainder; Ricochet Seed adds its existing guaranteed transitions. Chains never revisit a target and retain 65% damage per hop. Lumen multiplies aura radius by (1 + .03 * levelUps), separately from areaSize, leaving ability radii unchanged. Consuming upgrade cards does not award a second passive gain. Protocol 15 snapshots carry levelUps.

WorldMap.Buildings is a deterministic per-realm list of cottages and roofless ruins. ClearLocation and Resolve share each oriented solid foundation footprint, keeping spawns and actors outside it. Scenery props avoid a buffer around buildings; streams and sanctuaries stay clear. WorldEnvironment batches the architecture by material; RealmCartography paints building footprints. SceneryBuilder generates folded maple silhouettes, birch leaves and vertex-colored canopies; EncounterView shares three leaf-pile meshes selected by visual identity only.
