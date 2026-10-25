# Stopklatka: instructions for Claude Code on the developer's PC

Stopklatka is a Superhot-style mixed reality game for Meta Quest 3, built with Unity. The player walks around the real school or home they scanned with Space Setup, holding Touch controllers. Time moves only when they move. Red crystal enemies appear inside the scanned rooms.

The menu has three crystals: START, STYL (passthrough look: NORMALNY / BIAŁY / SUPERHOT) and TRYB (SZKOŁA / DOM). Both choices are saved in `PlayerPrefs`. Home mode (`Scripts/Core/GameMode.cs`) is for a whole house of about 150 m² with several rooms and maybe floors: closer spawns, fewer enemies at once, slower enemy bullets.

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
- [ ] Throwables: bottle (white glass), mug and ashtray (black, break into dark shards), block and ball (black, bounce). All stun.
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
- [ ] Enemies have a soft dark shadow on the floor. It isn't hidden by the real floor (that's what `_OcclusionBias` 0.08 is for) and is hidden behind real walls.
- [ ] Bullet streaks fade toward the tail. Enemy streaks are red and up to 1.6 m long, player streaks dark and up to 0.8 m.
- [ ] Kills: a bright flash at the hit, then two shades of red shards.
- [ ] Getting hit shows a red vignette at the edges of the view, not a flat red quad.
- [ ] HUD and menu labels are bold with a dark drop shadow, readable over a white room. "STOP." / "KLATKA." pop in.
- [ ] STYL cycles NORMALNY → BIAŁY → SUPERHOT. Pausing (leaving the scan) always shows plain passthrough.
- [ ] TRYB switches SZKOŁA ↔ DOM and the menu subtitle shows the mode. Both survive an app restart.
- [ ] Home mode with several rooms: bullets and enemies' line of sight pass through doorways (MRUK cuts `DOOR_FRAME` holes in the wall colliders). Enemies only spawn on the floor you're on.

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
- [ ] Afterwards: **Stopklatka → Build Settings: Game Scene**.

## Where to tune (most likely needed on the real device)

- `Resources/GameConfig.asset` (created by Build Game, edit it in the Inspector, also in Play mode):
  - `time`: time feel (minimum scale, speeds for full time, head turn weight, response curve, smoothing, action pulse),
  - `player`: grab radius, throws, catching (`catchMinSpeed`, `catchRadiusPerSpeed`, `catchRadiusBonusMax`), the fist (`fistEnabled`, `punchKillSpeed`, `punchStunSpeed`, `punchRadius`),
  - `combat`: player bullet speed, shooting bullets down (`bulletInterceptRadius`), katana bullet cuts (`katanaCutRadius`), thrown objects (`killOnThrow`, stun/kill/break speeds, `thrownBladeKills`),
  - `weapons`: ammo, cooldowns and spread of the pistol, shotgun and SMG (rounds, rounds per burst, interval between rounds),
  - `haptics`: strength, length and frequency of every vibration (shots, empty click ticks, grab, catch, punch, cut, block, getting hit),
  - `enemies`: reaction times, how many are armed,
  - `school` / `home`: per-mode numbers (spawn distances, enemies at once and per wave, enemy speed and bullet speed, how often enemies spawn in your room). The factory defaults are in `Scripts/Core/GameMode.cs`,
  - `performance`: refresh rate (90 or 120), foveation,
  - `debug`: the wrist time gauge and the FPS readout at start.
- `Scripts/Visuals/Factory.cs`:
  - `GunHoldPosition` / `GunHoldEuler`: if guns sit tilted in the hand, fix this first,
  - the katana and throwable hold poses (`CreateKatana`, `CreateThrowable`),
  - `FistPosition` / `FistEuler`: where the black fist sits on the controller (right hand; the left is mirrored). Thumb on top by default.
- `Scripts/UI/Hud.cs` and `MenuTarget.cs`: text scale. TextMesh sizes are estimates.
- `Scripts/World/PassthroughStyle.cs`: the BIAŁY (`SetBrightnessContrastSaturation`) and SUPERHOT (gradient color map) looks. Keep them moderate for safety.
- `Editor/StopklatkaBuilder.cs` → `CreateMaterials`: all colors (lit and shadow tone, rim, highlight, gloss). Re-run Stopklatka → Build Game after changing them.
- `Shaders/Crystal.shader`: light direction (`_LightDir`), the two-tone step (`smoothstep(0.3, 0.7, ...)`) and the darkening near the floor (`_FloorShade`).
- `Editor/StopklatkaBuilder.cs`:
  - the dynamic resolution range (`quest3Min/MaxDynamicResolutionScale` = 1.0 / 1.205),
  - which scene labels become colliders (`EffectMesh.Labels`) and NavMesh obstacles.

## Known risks to watch

- **GameConfig:** if `Resources/GameConfig.asset` is missing, the log says so and code uses the defaults from `GameConfig.cs`. Stopklatka → 4. Create Game Config makes it. Fields added later keep their code defaults in an existing asset.
- **Dynamic resolution** depends on the OpenXR plugin version (see `OVRManager`). If `renderViewportScale` never changes, set `XRSettings.eyeTextureResolutionScale` directly in `PerformanceManager`.
- **Door frames:** `EffectMesh.CutHoles = DOOR_FRAME` cuts doorways out of the wall meshes, and the wall colliders are built from those meshes. If bullets still stop in doorways, check in the Scene view that the generated wall `MeshCollider`s have the holes.
- **Floors:** `RoomService.UpdateFloor` takes the floor height from `MRUK.GetCurrentRoom()` every frame. Stairs are outside every room, so the game pauses there on purpose.
- **Reveal effect:** during spawn-in the enemy parts use the `EnemyReveal` material (keyword `_REVEAL`) plus a `MaterialPropertyBlock`, which takes them out of the SRP batcher for 0.9 s. If the keyword variant is stripped from the build, enemies appear without the effect; keep the material in `Resources/Materials`.
- **Boundary suppression** works only while the passthrough layer is active. `PassthroughStyle.Update` keeps it in sync.
- **Shader errors** in `Shaders/*.shader` (URP HLSL with Meta's occlusion include) show up as pink materials. Fix the shaders before anything else.
- **Fist and hand poses** (`Factory.FistPosition/FistEuler`, hold poses) are guesses made without a headset. If the fist doesn't sit over the real hand, or punches register too easily while walking (hand speed includes walking), tune the pose and `player.punchKillSpeed` / `punchStunSpeed` first.
- **Haptics:** Touch Plus vibrations shorter than about 20 ms may be too weak to feel; if the empty-gun double tick is not noticeable, raise `haptics.emptyClick.seconds` or `amplitude`.
- **Safety:** enemies spawn only inside scanned rooms, at least 2.5 m away at school and 1.8 m at home (1.2 m at the very least in tiny rooms). Keep it that way. Don't add mechanics that need running or fast backing up.
