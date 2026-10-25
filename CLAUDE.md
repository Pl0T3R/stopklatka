# Stopklatka: instructions for Claude Code on the developer's PC

Stopklatka is a Superhot-style mixed reality game for Meta Quest 3, built with Unity. The player walks around the real school or home they scanned with Space Setup, holding Touch controllers. Time moves only when they move. Red crystal enemies appear inside the scanned rooms.

The menu is a set of black cubes you grab with the grip, floating in an arc in front of you: START (the first level not cleared yet), POZIOM 1 / 2 / 3 (level select; cleared levels say ZALICZONY), STYL (passthrough look: NORMALNY / BIAŁY / SUPERHOT), TRYB (SZKOŁA / DOM) and WYJŚCIE (grab twice to quit). STYL, TRYB and the cleared levels are saved in `PlayerPrefs`. `menu.useCubes` off in GameConfig brings back the old crystals you shoot. Each of the 3 levels puts its own white props (GALERIA, ULICA, HOL) into the scanned rooms, only where they can't hide a real obstacle. Home mode (`Scripts/Core/GameMode.cs`) is for a whole house of about 150 m² with several rooms and maybe floors: closer spawns, fewer enemies at once, slower enemy bullets.

The owner (Pl0T3R) speaks Polish, so talk to them in Polish. Code, comments and commit messages stay in English.

The current work follows `GAME_SPEC.md` (the owner's spec, in Polish), `PLAN.md` (how it maps onto this MR game, stage by stage) and `STYLE_GUIDE.md` (the visual target). Read them before changing gameplay or visuals. The owner's reference screenshots are in `References/` on their PC only; they are not in git.

Read `docs/PLAN.md` for the original design and its requirements:
- 90 Hz by default, 120 Hz as a config option (owner's decision of 2026-10-25, see `PLAN.md`; the original plan said 120 Hz),
- about 2024 px per eye,
- controllers only,
- pistol, shotgun, katana and throwables,
- if performance runs short, frame rate wins over resolution.

## Current state

Everything in `Assets/Stopklatka/` was written in a cloud session that had no Unity editor. It was checked against:
- the Meta XR Core SDK 207.0.1 and MR Utility Kit 207.0.1 package sources,
- the Unity 6000.0.85f1 managed assemblies (see `docs/PLAN.md`, "Verification done in the cloud").

It has **never been opened in Unity or run on a headset**. Expect to fix compile errors and to tune things on the device. The Unity project scaffold (`Packages/`, `ProjectSettings/`) does not exist yet. You create it in step 1.

## Tools

- Use the **metavr MCP server** (configured in `.mcp.json`) for:
  - Unity project setup (`metavr_unity_setup`),
  - devices (`device`), apps (`app`), logs, performance (`run` with `perf` / `ovrmetrics`),
  - Meta docs (`vr_docs_search`).
- Before changing any Meta or MRUK API call, check the real package source in `Library/PackageCache/com.meta.xr.sdk.core@*/` and `Library/PackageCache/com.meta.xr.mrutilitykit@*/`, or use `vr_docs_search`. Don't guess API names.
- Requirements:
  - Unity 6 LTS. MRUK 207 needs 6000.0.66f2 or newer.
  - The Android Build Support module, including OpenJDK, SDK and NDK.

## Steps

1. **Create the Unity project in this repo folder.** Call `metavr_unity_setup(action="inject", project_path="<absolute path of this repo>")`, then open it with `metavr unity projects open <path>`.
   - Simplified VR Setup runs on first open and takes 5–10 minutes. Wait for `SETUP COMPLETE`.
   - In the "Optional Meta XR SDKs" window, add **MR Utility Kit**. It pulls in `com.unity.ai.navigation`.
   - If the setup tool refuses because `Assets/` already exists, create the project in a temporary folder, then copy `Assets/Stopklatka`, `CLAUDE.md`, `docs/` and `.mcp.json` into it.
2. **Build the game content.** In Unity, run the menu **Stopklatka → Build Game (settings + materials + scene)**. Then run **Meta → Tools → Project Setup Tool → Fix All** for Android.
   - Build Game also imports **TMP Essential Resources** if they're missing (**Stopklatka → 6. Import TextMeshPro Essentials**). The import finishes after the menu command returns; check that `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` exists, else use Window → TextMeshPro → Import TMP Essential Resources.
   - Check that OpenXR has the Meta Quest feature group enabled and that the render mode is **Multi-view**.
   - Check that the only scene in Build Settings is `Assets/Stopklatka/Scenes/Stopklatka.unity`. **Stopklatka → Build Settings: Game Scene** sets it. **Stopklatka → Build Time Test Scene** switches the build to the time test scene; switch back before building the game.
3. **Compile to zero errors and zero warnings.** Fix what breaks while keeping the design in `docs/PLAN.md`. Re-run Stopklatka → Build Game after changing the builder.
4. **Build and run on the headset.**
   - Start with `metavr device list`.
   - Use File → Build And Run, or build an APK and `metavr app install`.
   - Read logs filtered by `[Stopklatka]`.
   - The player's first launch asks for spatial data permission.
5. **Verify** with the checklist below, then **commit and push** to `origin main` with a short English message.

## Verification checklist (from the plan)

- [ ] No exceptions in the logs. The log shows `display 90 Hz` (120 Hz when `performance.targetRefreshRate` in GameConfig is 120), `physics step 1/90 s` and the eye texture size.
- [ ] With 8 enemies on screen, OVR Metrics or `metavr perf` shows a stable 90 fps and p95 frame time under 11.1 ms (with the 120 Hz option: 120 fps, 8.3 ms). The resolution drops only when needed.
- [ ] Time nearly stops when standing still and speeds up with head or hand movement (details in "Time test scene" below).
- [ ] All the weapons work:
  - pistol: 4 shots,
  - shotgun: 2 shots, spread,
  - SMG: 3-round bursts, 5 bursts (15 rounds), small spread, one time pulse per burst,
  - katana: cuts enemies, blocks bullets and cuts bullets in flight with a swing,
  - bottle: breaks and stuns.
- [ ] An empty gun clicks on every trigger pull: a click sound plus two short sharp haptic ticks, clearly different from the single strong buzz of a shot.
- [ ] Thrown items with `combat.killOnThrow` off (default): a fast throw staggers the enemy and knocks its gun out, it doesn't kill. A thrown katana still kills (`thrownBladeKills`). With `killOnThrow` on: above `throwKillSpeed` it kills, bottles, mugs and ashtrays always kill.
- [ ] Throwables: bottle (dark smoky glass, dark glass shards), mug and ashtray (black, break into dark shards), block and ball (black, bounce). All stun.
- [ ] Fist: an empty hand shows a black faceted fist over the controller, hidden while holding something. A punch faster than about 2 m/s shatters the enemy (punch sound, strong haptic). A slower push (1 to 2 m/s) only staggers it and knocks its gun out. Slower contact does nothing.
- [ ] Shooting bullets down: your bullet passing within about 5 cm of an enemy bullet destroys both with a small flash and a high "clink".
- [ ] Catching: hold the grip with an empty hand and a flying item (a thrown bottle, a gun dropped by a dying or stunned enemy) is caught when it reaches the hand, with a stronger haptic. Pressing the grip just as it passes also works.
- [ ] Haptics on shot, grab, catch, punch, katana cut, blocked bullet, and on both hands when you're hit (also in the time test scene).
- [ ] Without a scan, thrown items land on an invisible floor at your floor height (log: `fallback floor at y = ...`).
- [ ] Throws fly where the hand was moving.
- [ ] Bullets stop at real walls. Enemies walk around desks.
- [ ] Walking out of the scanned area pauses the game with plain passthrough. Walking back resumes it.
- [ ] The boundary doesn't pop up in the corridor.
- [ ] Without a scan or with the permission denied, the game still runs around the player and shows a message.
- [ ] No pink materials: every shader in `Shaders/` compiles for Android/Vulkan with multiview.
- [ ] Enemies form from the floor up with a glowing edge, over a red ring spreading on the floor. Their gun pops in when the edge reaches the hand. After 0.9 s they use the shared `EnemyCrystal` material again (check in the Frame Debugger that enemies batch).
- [ ] Enemies have a soft cool grey-blue shadow on the floor (about `#B9C6CC` over a white floor, never black). It isn't hidden by the real floor (that's what `_OcclusionBias` 0.08 is for) and is hidden behind real walls.
- [ ] Bullet streaks are about 1 cm thick along their whole length, solid for the front 60% and fading to clear at the tail. Enemy streaks are bright red (`#FF1E1E`) and up to 3.5 m long (`visuals.enemyStreakLength`), yours blue-black and up to 1 m.
- [ ] Enemy look (STYLE_GUIDE 6): about 1.8 m, broad pointed shoulders, narrow waist, long legs, a long narrow faceless head, block hands. 11 parts plus the floor shadow: about 12 draw calls per enemy (13 with a gun), and they batch (Frame Debugger).
- [ ] Kills (any: bullet, throw with `killOnThrow`, thrown katana): a short yellow flash at the hit, the hit part and parts close to it burst into 30 to 60 small red shards (tetrahedra and flakes, two reds, some glowing yellow), the other limbs and the torso fall as big chunks with a flat yellow-orange cap where they broke, roll to a stop lying flat, and shrink away after about 4 s of game time. In slow time everything hangs in the air.
- [ ] Katana: the enemy is cut in half along the swing. Each half has a flat glowing cross-section at the cut, the upper half flies off along the swing, the legs mostly stay, a fan of shards sprays from the cut line, and after about 1.3 s of game time both halves burst into shards. A stab (no sideways swing) shatters instead.
- [ ] Fist kill: glowing cracks spread from the knuckles over the hit part for about 0.25 s of game time while the enemy trembles, then it shatters. Bullets pass through it while it cracks.
- [ ] Armed enemies: the gun arm rises slowly while they take aim, then a tell before every shot (they stop walking, a small white glint at the muzzle and a click), then the shot. Every shot can be dodged with a step or a lean.
- [ ] SMG enemies (from level 2, and from wave 4 in endless mode) fire 3-round bursts, all at where your head was when the burst started, with a little spread.
- [ ] Unarmed enemies walk up (never run), pull the arm back visibly, then punch.
- [ ] Your bullet passing close to an enemy, or a neighbour dying, makes it raise its arms before its face for about 0.7 s and lose its aim.
- [ ] Spawns: often just inside a scanned door frame on your floor (as if walking in through the door), otherwise sometimes outside your view. Never closer than 2.5 m at school and 1.8 m at home (1.2 m in tiny rooms), always inside a scanned room.
- [ ] Levels (`levels.useLevels` on, default): START shows "POZIOM 1" and "GALERIA", then waves ("FALA 2/3", "FALA 3/3"). Clearing the last wave: "STOP." / "KLATKA." twice, "POZIOM 1 ZALICZONY", then level 2 (ULICA), level 3 (HOL). After level 3: "KONIEC", then the menu with "Wszystkie poziomy zaliczone" (or which levels are still to clear, if you started from level 2 or 3).
- [ ] Menu (stage 6): black faceted cubes (13 cm, START 16 cm) at chest height within arm's reach, each with a white bold label: upper row POZIOM 1 / 2 / 3 (with the level name), lower row STYL, START, TRYB and WYJŚCIE further right. If you stand close to a wall, the arc turns toward free space (never inside a wall or cupboard).
  - Grip on a cube: it breaks into black shards with a sound and a grab haptic, and does its thing. A fast punch, a bullet or a katana cut work too. Touching it without the grip does nothing.
  - STYL and TRYB come back at once with the new value. POZIOM n starts that level.
  - START starts the first level not cleared yet (its label says which). Cleared levels show "ZALICZONY" on their cube and survive an app restart. When all are cleared, START starts level 1.
  - WYJŚCIE: the first grab turns it into "WYJŚCIE? CHWYĆ JESZCZE RAZ" for 4 s (`menu.quitConfirmSeconds`); a second grab in that time quits the app, otherwise it goes back to "WYJŚCIE".
  - `menu.useCubes` off: the old menu (red START crystal, white STYL / TRYB crystals, a hovering pistol to shoot them).
- [ ] Level props (stage 6): when a level starts, white props rise from the floor with a glowing edge (about 1.2 s); when it's cleared they sink back into the floor during "POZIOM n ZALICZONY". The log shows `Props GALERIA: n on furniture, m on walls (scan on your floor: ...)`.
  - GALERIA: white blocks exactly over scanned furniture (STORAGE, TABLE, OTHER, COUCH, BED) with small white statues of the enemy figure on top (three poses), half columns and tall soft white "window light" panels on free walls, with a pale light rectangle on the floor below.
  - ULICA: white cars over couches, beds and big furniture (always smaller than the furniture), white blocks over the rest, lamp posts and bar rails flat against walls, a facade trim high on a few walls.
  - HOL: a reception counter over the largest table, white blocks over other furniture, half columns, and elevator frames around scanned door frames. Enemies mostly walk out of those doors (`props.elevatorDoorSpawnChance` 0.9).
  - Bullets stop at props (except the light panels), enemies can't see through them, thrown items bounce off. Pickups never land inside a statue.
  - With `props.enabled` off, no scan, or an empty room, the level plays the same without props.
- [ ] Prop safety (walk the level and check each rule):
  - Every free-standing prop covers only a real piece of furniture: walk around each one and make sure no part sticks out past the real furniture. Things on or under the furniture may be hidden; anything sticking out in front of a prop still shows (Depth API).
  - Wall props stick out at most 18 cm (`props.maxWallDepth`), never cover a door frame, window or other scanned wall item (20 cm margin), never sit where furniture stands against the wall, and nothing ever goes across or above a door opening; elevator frames stand beside the opening and the doorway stays clear.
  - Nothing new in open floor space (the floor light is a flat, see-through decal without a collider) and nothing within 1.5 m of where you stood when the level started.
  - Nothing on stairs or on another storey. Nothing taller than 2.3 m above your floor.
  - Walking out of the scan (pause) hides every prop together with the game. The menu has no props.
- [ ] Haze: far props (from about 3 m, strongest at 15 m) take on a light cyan tint (`#DDEFF4`, `props.haze*`); enemies, weapons and your hands never do.
- [ ] Death in a level: one hit gives the red vignette and "TRAFIONY", the world almost stops, the view doesn't move, and about 1.5 s later the level restarts from its first wave (enemies, bullets and debris cleared, new pickups).
- [ ] With `levels.useLevels` off: the old endless waves (FALA 1, 2, 3...) as before, now with SMG enemies too.
- [ ] Leaving the scan during a level pauses it (the restart and wave timers wait too).
- [ ] Getting hit shows a red vignette at the edges of the view, not a flat red quad.
- [ ] Colors match `STYLE_GUIDE.md` (stage 5): enemies `#FF2B1C` lit / `#9E0A08` shadow with a pink-red rim; weapons, throwables and the fist blue-black (`#1E3242` / `#0B141A`) with a navy-teal sheen on the edges; the katana blade black with a steel-grey edge; muzzle flashes white; yellow-orange only where enemies break.
- [ ] Hatching: up close (under about 3 m) enemies show thin darker diagonal stripes (45°, about 60 per metre, `#B80E0A` at 25%). Further away the stripes fade out smoothly, no shimmering or moiré when you move your head. Weapons have none.
- [ ] STYL SUPERHOT maps the room onto the white palette (cool grey shadows to white), and the edges of real furniture, steps and door frames are still easy to see.
- [ ] Sound (stage 5): the log shows `Audio: mixer StopklatkaMixer, world pitch in the mixer`. Shots, shatters and thuds drop in pitch when time slows (down to about 35%), UI chimes never do. Standing still in a fight there is a low hum; moving fades it out and brings in a quiet airy rush. Both are silent in the menu and while paused. Without the mixer (see below) the pitch still follows time, set per sound.
- [ ] HUD and menu labels are bold with a dark drop shadow, readable over a white room. "STOP." / "KLATKA." pop in.
- [ ] Texts (stage 6): the log shows `Texts: TextMeshPro (LiberationSans SDF)`. Polish letters (ą ć ę ł ń ó ś ź ż) show in every text, white or red with a soft dark shadow and a thin dark outline. Titles are not glued to your face: a new title appears once in front of you, on the real wall ahead if it is 1.5 to 6 m away, else 2.5 m ahead, a little above eye level, and stays there (it only turns to face you). Look away for more than about 1.2 s and it moves in front of you again. The FPS readout (debug) still follows your head.
- [ ] Text fallback: with `ui.useTextMeshPro` off (or the TMP font missing) the log shows `Texts: legacy TextMesh` and every text still shows, with the old drop shadow.
- [ ] STYL cycles NORMALNY → BIAŁY → SUPERHOT. Pausing (leaving the scan) always shows plain passthrough.
- [ ] TRYB switches SZKOŁA ↔ DOM and the menu subtitle shows the mode. Both survive an app restart.
- [ ] Home mode with several rooms: bullets and enemies' line of sight pass through doorways (MRUK cuts `DOOR_FRAME` holes in the wall colliders). Enemies only spawn on the floor you're on.

### Editor check without a headset scan (stage 6)

Play the game scene in the Editor (Meta XR Simulator, or Quest Link without a scan).
- [ ] The log shows `Editor: no scan from the device, loaded the MRUK sample room LivingRoom00`, then the menu cubes appear in front of the camera.
- [ ] Start each level from its cube and look at the props in the Scene view (they're named Plinth, Statue, Car, Counter, HalfColumn, WindowLight, FloorLight, LampPost, Rail, Trim, ElevatorFrame). Turn on the MRUK scene debugger or the EffectMesh visuals to compare them with the room's anchors: every Plinth/Car/Counter/Statue inside a furniture box, wall props flat on walls and clear of door and window anchors.
- [ ] Try other rooms through `GameConfig.debug.editorSampleRoom` (e.g. `.../Prefabs/Office/Office03.prefab`, `.../Other/OtherRoom07_Cluttered.prefab`, `.../Other/OtherRoom04_empty.prefab`: no props, the level still plays).
- [ ] In a build, the log never mentions the sample room.

### Time test scene (stage 2)

Run **Stopklatka → Build Time Test Scene**, then Build And Run. It needs no scan, so stand with a few metres of free space ahead. The boundary stays on in this scene.
- [ ] The wrist gauge (above the left controller) shows the time scale: about `x0.03` when standing still. The pendulum and the turret's bullet nearly freeze.
- [ ] A step or a quick hand movement brings the gauge to `x1.00` within about 0.1 s. After stopping it falls back within about 0.3 s, without flicker.
- [ ] Turning the head alone speeds time up about a third as much as moving (weight `headTurnWeight` 0.3).
- [ ] A shot, a grab or a fast throw jumps the gauge to `x1.00` for 0.2 s (the white tick shows the target).
- [ ] A bottle thrown while time is slow flies smoothly, without stepping (physics step scales with time).
- [ ] A turret bullet can be dodged in slow time. A hit shows the red vignette and `TRAFIONY (n)`, nothing restarts.
- [ ] The enemy on the right freezes and moves with time like the pendulum. Killed, it comes back after 3 s.
- [ ] The left menu button puts everything in front of you again. A left thumbstick click shows FPS, motion, target and counters.
- [ ] Recentering (hold the Meta button) doesn't make time jump.
- [ ] Stage 3 (weapons):
  - Floating in an arc in front of you: katana, shotgun, SMG, pistol, bottle, mug, ashtray, block.
  - The **X button** lobs a pistol in an arc toward your right hand, in game time. Hold the right grip and let it fly into the hand: it's caught. Standing still, it nearly freezes mid-air. The counter shows `rzuty X`.
  - Shoot the turret's bullet with the pistol or SMG: both bullets vanish with a flash and a clink.
  - Swing the katana through a nearly frozen turret bullet: it's cut (sparks, deflect sound, haptic).
  - Empty a gun, keep pulling: click plus double haptic tick on every pull. Throw the empty gun at the enemy: it staggers and drops its pistol (catch the pistol in the air).
  - Punch the enemy fast: it shatters. Push it slowly: nothing, or a stagger above 1 m/s.
  - A turret hit vibrates both controllers.
- [ ] Stage 4 (enemies and death):
  - The **Y button** cycles the enemy's weapon (title shows it): PISTOLET → PISTOLET MASZYNOWY → STRZELBA → BEZ BRONI, and the enemy comes back at once with it. A left thumbstick click also shows `zabici` (kills).
  - Pistol and shotgun: the arm rises, then the tell (stop, glint, click), then the shot. SMG: same tell, then a 3-round burst at where your head was; step aside during the tell and all three miss.
  - Unarmed: it walks up, pulls the arm back, punches; a hit counts `TRAFIONY (n)`.
  - Shoot it: yellow flash, shards from the hit, limbs fall as chunks with glowing caps and lie on the floor.
  - Cut it with the katana (diagonal, horizontal, vertical swings): two halves with glowing faces, then shards.
  - Punch it fast: glowing cracks over the hit part, then the shatter.
  - Shoot past it without hitting: it guards for a moment.
  - Stand still right after a kill: shards and chunks hang in the air.
- [ ] Stage 5 (look and sound):
  - Up close the enemy shows the diagonal hatching; step back and it fades out without shimmering.
  - The turret's streak is a long (up to 3.5 m) thin red line, solid near the bullet and fading at the tail.
  - Standing still: a low hum, and the turret shot sounds slow and deep. Move: the hum fades, a quiet airy rush comes in, shots sound normal.
  - The bottle is dark glass; the katana blade is black with a light edge.
- [ ] Afterwards: **Stopklatka → Build Settings: Game Scene**.

## Where to tune (most likely needed on the real device)

- `Resources/GameConfig.asset` (created by Build Game, edit it in the Inspector, also in Play mode):
  - `time`: time feel (minimum scale, speeds for full time, head turn weight, response curve, smoothing, action pulse),
  - `player`: grab radius, throws, catching (`catchMinSpeed`, `catchRadiusPerSpeed`, `catchRadiusBonusMax`), the fist (`fistEnabled`, `punchKillSpeed`, `punchStunSpeed`, `punchRadius`),
  - `combat`: player bullet speed, shooting bullets down (`bulletInterceptRadius`), katana bullet cuts (`katanaCutRadius`), thrown objects (`killOnThrow`, stun/kill/break speeds, `thrownBladeKills`),
  - `weapons`: ammo, cooldowns and spread of the pistol, shotgun and SMG (rounds, rounds per burst, interval between rounds),
  - `haptics`: strength, length and frequency of every vibration (shots, empty click ticks, grab, catch, punch, cut, block, getting hit),
  - `enemies`: reaction times, how many are armed (endless waves), SMG share, the aim tell (`aimTellSeconds`, `aimLockAngle`, `aimInaccuracy`), enemy SMG bursts (`smgBurstRounds`, `smgBurstInterval`, `smgBurstSpread`), the punch wind-up (`punchWindupSeconds`, `punchStrikeSeconds`, `punchRecoverSeconds`), the guard (`defensivePose`, `guardSeconds`, `guardBulletDistance`, `guardNeighbourDistance`), spawn points (`doorSpawnChance`, `doorSpawnOffset`, `outOfViewChance`, `outOfViewAngle`),
  - `shatter`: shard count, sizes, colors and lifetimes, which parts turn to dust (`pulverizeRadius`), big chunks (`chunkLifetime`, `chunkPush`, `maxPieces`), the yellow flash, the katana cut (`katanaCutsInHalf`, `cutHalfSeconds`, speeds, shard counts), the punch cracks (`punchCracks`, `crackSeconds`, `crackCount`, `crackLength`),
  - `levels`: `useLevels` (off = endless waves), `restartDelay` (1.5 s), intro, wave gap, spawn interval, chant repeats, pickups, and the 3 levels with their waves (enemies, alive at once, armed / shotgun / SMG shares, difficulty 1..9),
  - `school` / `home`: per-mode numbers (spawn distances, enemies at once and per wave, enemy speed and bullet speed, how often enemies spawn in your room). The factory defaults are in `Scripts/Core/GameMode.cs`,
  - `performance`: refresh rate (90 or 120), foveation,
  - `debug`: the wrist time gauge, the FPS readout at start, and `editorSampleRoom` (the MRUK sample room loaded in the Editor when there is no scan; empty = off),
  - `props` (stage 6): on/off, how many (`maxProps` 14, `maxFurnitureProps` 6, `maxWallProps` 8), the safety distances (`minPlayerDistance` 1.5 m, `wallMargin` 0.2 m, `maxWallDepth` 0.18 m, `floorTolerance`, `maxHeight` 2.3 m), shapes (statue height, window width, floor light depth, wall spacing), the rise and sink times, HOL's door spawn chance and the haze (color, start, end, strength),
  - `menu` (stage 6): cubes or crystals, cube sizes, the arc (radius, row heights, angles), extra grab radius, quit confirm time,
  - `ui` (stage 6): TMP on/off, the TMP font and material, shadow color, title / subtitle / label sizes, where titles appear (wall distances, default distance, height, keep and re-anchor angles),
  - `levels.list[n].theme` (GALERIA / ULICA / HOL / None / Auto) and `doorSpawnChance` per level.
- `Scripts/Visuals/Factory.cs`:
  - `GunHoldPosition` / `GunHoldEuler`: if guns sit tilted in the hand, fix this first,
  - the katana and throwable hold poses (`CreateKatana`, `CreateThrowable`),
  - `FistPosition` / `FistEuler`: where the black fist sits on the controller (right hand; the left is mirrored). Thumb on top by default.
- `Scripts/Enemies/EnemyBody.cs`: body proportions (`EnsureMeshes`, rings are height, x radius, z radius, z offset), the guard and punch wind-up poses (`GuardUpperR/ForeR/...`, `WindupUpperR/ForeR`, directions in the chest's frame), the cut-face sizes (`CrossSection`). These are guesses made without a headset.
- `Scripts/UI/Hud.cs` and `MenuTarget.cs`: text scale. TextMesh sizes are estimates. Sizes now come from GameConfig `ui` (font height per metre of distance for titles, metres for cube labels); `WorldText` converts them (TMP: font size 1 = 0.1 m; legacy: about 12 units per line). The TMP drop shadow and outline values are in `WorldText.Init`.
- `Scripts/World/PassthroughStyle.cs`: the BIAŁY (`SetBrightnessContrastSaturation`) and SUPERHOT (gradient color map) looks. Keep them moderate for safety.
- `Editor/StopklatkaBuilder.cs` → `CreateMaterials`: all colors as hex values from `STYLE_GUIDE.md` (lit and shadow tone, rim, highlight color and strength, gloss, hatching). Re-run Stopklatka → Build Game after changing them. `Mats.CreateFallback` has the same colors for a project without the material assets.
- `Shaders/Crystal.shader`: light direction (`_LightDir`), the two-tone step (`smoothstep(0.3, 0.7, ...)`), the darkening near the floor (`_FloorShade`) and the hatching (`_HatchStrength`, `_HatchDensity`, `_HatchColor`; the line width 0.14 and the distance fade `1.6 - aa * 2.5` in the fragment shader).
- `GameConfig` → `visuals` (streak lengths) and `sound` (mixer on/off, pitch at slow and full time, hum and ambient volumes, response, ambient dB).
- `Scripts/World/LevelProps.cs`: what each theme puts where (`BuildGaleria`, `BuildUlica`, `BuildHol`: which furniture becomes a car or a counter, statue rules, how wall props alternate). `Scripts/World/PropPlanner.cs`: the safety geometry (which anchors block a wall, 0.45 m for furniture in front of a wall, 0.6 m storey tolerance). `Scripts/Visuals/PropKit.cs`: the prop shapes (plinth, counter, car, half column, lamp post, rail, trim, elevator frame, light panels). `Enemies/EnemyBody.cs` → `StatueMesh`: the three statue poses.
- `Editor/StopklatkaBuilder.cs`:
  - the dynamic resolution range (`quest3Min/MaxDynamicResolutionScale` = 1.0 / 1.205),
  - which scene labels become colliders (`EffectMesh.Labels`) and NavMesh obstacles.

## Known risks to watch

- **GameConfig:** if `Resources/GameConfig.asset` is missing, the log says so and code uses the defaults from `GameConfig.cs`. Stopklatka → 4. Create Game Config makes it. Fields added later keep their code defaults in an existing asset.
- **Dynamic resolution** depends on the OpenXR plugin version (see `OVRManager`). If `renderViewportScale` never changes, set `XRSettings.eyeTextureResolutionScale` directly in `PerformanceManager`.
- **Door frames:** `EffectMesh.CutHoles = DOOR_FRAME` cuts doorways out of the wall meshes, and the wall colliders are built from those meshes. If bullets still stop in doorways, check in the Scene view that the generated wall `MeshCollider`s have the holes.
- **Floors:** `RoomService.UpdateFloor` takes the floor height from `MRUK.GetCurrentRoom()` every frame (its first `FloorAnchors` entry; the single `FloorAnchor` property is obsolete in MRUK 207 and would warn). Stairs are outside every room, so the game pauses there on purpose.
- **Reveal effect:** during spawn-in the 11 enemy parts use the `EnemyReveal` material (keyword `_REVEAL`) plus a `MaterialPropertyBlock`, which takes them out of the SRP batcher for 0.9 s. If the keyword variant is stripped from the build, enemies appear without the effect; keep the material in `Resources/Materials`. An enemy killed while forming finishes forming first, so its pieces are whole.
- **Death effects** (`Enemies/Shatter.cs`, `Debris.cs`, `Cracks.cs`): big chunks are copies of the dead enemy's part meshes in pooled debris (`EnemyCrystal`, batched), and the glowing caps, cut faces, cracks and the kill flash use the new `FractureGlow` material (`Stopklatka/Transparent`, white) with the colors in the meshes' vertex colors (linear values of `#FFD84D` / `#FF9A1F`). If the glow looks washed out or too dark, check the color space (must be Linear) before touching the colors. Glowing shards use the new `ShardGlow` material (`Stopklatka/Crystal`). Chunks move on their own (no Rigidbody): they bounce on the enemy's floor height but pass through real walls; they only fly about a metre. A kill spawns up to about 20 chunk and cap pieces and 60 shards; the pools are created while the scan loads (`Shatter.Prewarm`). Watch `metavr perf` when 3 or more enemies die at once.
- **Katana cut plane** = blade direction × swing direction. Each body part goes whole to one side, so the cut always falls between parts (neck, waist, hips, shoulders, knees); the glowing face is placed where that half's parts end. Very vertical cuts get a tall face down the body. If faces float away from the halves, tune `EnemyBody.CrossSection` or switch the cut off (`shatter.katanaCutsInHalf`).
- **Door spawns** use `MRUKRoom.Anchors` with the `DOOR_FRAME` label. Both sides of each frame are tried and only a side inside a scanned room, on your floor, at the allowed distance and connected by the NavMesh is used, so the frame's facing direction doesn't matter. Without door frames in the scan, spawns fall back to random floor spots.
- **Audio mixer:** Unity has no public API to build an AudioMixer, so **Stopklatka → 5. Create Audio Mixer** (also run by Build Game) calls the editor's internal `AudioMixerController` by reflection, the same steps as Unity's Audio Mixer window (checked against the 6000.0.85 editor DLL). It makes `Resources/Audio/StopklatkaMixer.mixer` with groups World, UI and Ambient under Master and exposes `WorldPitch` (World pitch, 1.0 = 100%) and `AmbientVolume` (Ambient volume, dB). If it fails, the log says why and prints these manual steps; the game meanwhile sets the pitch per sound:
  1. Project window: right-click `Assets/Stopklatka/Resources/Audio` → Create → Audio Mixer, name it `StopklatkaMixer`.
  2. Window → Audio → Audio Mixer. Next to Groups click + three times (all under Master): `World`, `UI`, `Ambient`.
  3. Select the World group. In the Inspector, right-click Pitch → Expose "Pitch (of World)" to script.
  4. Select the Ambient group. Right-click Volume → Expose "Volume (of Ambient)" to script.
  5. In the Audio Mixer window, top right: Exposed Parameters. Rename them `WorldPitch` and `AmbientVolume`.
  6. Save (Ctrl+S). `SoundBank` finds the mixer and the groups by name.
- **Hatching** is computed in `Crystal.shader` from object-space position with `fwidth` antialiasing (one extra interpolator, no new keyword or variant). If it shimmers on the headset, lower `_HatchStrength` or raise the fade (`1.6 - aa * 2.5`); if the shader fails to compile (pink), check this block first.
- **Boundary suppression** works only while the passthrough layer is active. `PassthroughStyle.Update` keeps it in sync.
- **Shader errors** in `Shaders/*.shader` (URP HLSL with Meta's occlusion include) show up as pink materials. Fix the shaders before anything else.
- **Fist and hand poses** (`Factory.FistPosition/FistEuler`, hold poses) are guesses made without a headset. If the fist doesn't sit over the real hand, or punches register too easily while walking (hand speed includes walking), tune the pose and `player.punchKillSpeed` / `punchStunSpeed` first.
- **Haptics:** Touch Plus vibrations shorter than about 20 ms may be too weak to feel; if the empty-gun double tick is not noticeable, raise `haptics.emptyClick.seconds` or `amplitude`.
- **TextMeshPro (stage 6):** in Unity 6 TMP is part of `com.unity.ugui` 2.0 (namespace `TMPro`); the project must have that package (every Unity 6 template does). The code was checked against the `com.unity.textmeshpro` 3.2.0-pre.15 sources, which uGUI 2.0's TMP is built from; `TextMeshPro`, `TMP_FontAsset`, `FontStyles`, `TextAlignmentOptions`, `TextWrappingModes` and `TextOverflowModes` are the only TMP types used. Texts load `Resources/Fonts & Materials/LiberationSans SDF` and copy `LiberationSans SDF - Drop Shadow` (underlay + outline on, so those shader variants ship); its dynamic fallback asset adds Polish letters at runtime. If the font is missing, everything falls back to the legacy TextMesh. If text is invisible or pink, check that the Essentials were imported into `Assets/TextMesh Pro`.
- **Prop placement (stage 6)** uses `MRUKRoom.Anchors`, `FloorAnchors`, `IsPositionInRoom`, `IsPositionInSceneVolume` and `MRUKAnchor.PlaneRect` / `VolumeBounds` (MRUK 207 sources). Box axes are read from the anchor transforms (the axis closest to vertical is the height), and a wall's inside is the side where a point 30 cm away is in its room, so no assumption about MRUK's local axes. If props float, sink or face the wrong way, check this first in the Scene view with the MRUK scene debugger. Props are not part of the NavMesh: enemies walk by the scanned furniture they cover anyway; wall props are at most 18 cm deep.
- **Prop reveal and haze:** props rise with copies of the `PropWhite` / `StatueWhite` materials with the `_REVEAL` keyword (the variant ships because `EnemyReveal` uses it) and go back to the plain copies afterwards. The haze is computed in `Crystal.shader` from the distance to the eye (`_FogColor`, `_FogStart`, `_FogEnd`, `_FogStrength`, material values, no keyword); every other material has `_FogStrength` 0. If the shader fails to compile (pink), check this block.
- **Editor sample rooms:** in the Editor only, when the device gives no rooms, `RoomService` loads `GameConfig.debug.editorSampleRoom` (default MRUK's `LivingRoom00` prefab) with `MRUK.LoadSceneFromPrefab`. It's behind `#if UNITY_EDITOR` and uses `AssetDatabase`, so builds never contain or load it. The player starts at the room's origin, which may be close to furniture: some props may be skipped for being within 1.5 m.
- **Safety:** enemies spawn only inside scanned rooms, at least 2.5 m away at school and 1.8 m at home (1.2 m at the very least in tiny rooms). Keep it that way. Don't add mechanics that need running or fast backing up.
