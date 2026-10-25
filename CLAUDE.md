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
   - Check that the only scene in Build Settings is `Assets/Stopklatka/Scenes/Stopklatka.unity`.
3. **Compile to zero errors and zero warnings.** Fix what breaks while keeping the design in `docs/PLAN.md`. Re-run Stopklatka → Build Game after changing the builder.
4. **Build and run on the headset.**
   - Start with `metavr device list`.
   - Use File → Build And Run, or build an APK and `metavr app install`.
   - Read logs filtered by `[Stopklatka]`.
   - The player's first launch asks for spatial data permission.
5. **Verify** with the checklist below, then **commit and push** to `origin main` with a short English message.

## Verification checklist (from the plan)

- [ ] No exceptions in the logs. The log shows `display 120 Hz` and the eye texture size.
- [ ] With 8 enemies on screen, OVR Metrics or `metavr perf` shows a stable 120 fps and p95 frame time under 8.3 ms. The resolution drops only when needed.
- [ ] Time nearly stops when standing still and speeds up with head or hand movement.
- [ ] All four weapons work:
  - pistol: 4 shots,
  - shotgun: 2 shots, spread,
  - katana: cuts enemies and blocks bullets,
  - bottle: breaks and stuns.
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

## Where to tune (most likely needed on the real device)

- `Scripts/Core/Tuning.cs`: time feel, player bullet speed, reaction time curves.
- `Scripts/Core/GameMode.cs`: per-mode numbers (school / home): spawn distances, enemies at once and per wave, enemy speed and bullet speed, how often enemies spawn in your room.
- `Scripts/Visuals/Factory.cs`:
  - `GunHoldPosition` / `GunHoldEuler`: if guns sit tilted in the hand, fix this first,
  - the katana and bottle hold poses.
- `Scripts/UI/Hud.cs` and `MenuTarget.cs`: text scale. TextMesh sizes are estimates.
- `Scripts/World/PassthroughStyle.cs`: the BIAŁY (`SetBrightnessContrastSaturation`) and SUPERHOT (gradient color map) looks. Keep them moderate for safety.
- `Editor/StopklatkaBuilder.cs` → `CreateMaterials`: all colors (lit and shadow tone, rim, highlight, gloss). Re-run Stopklatka → Build Game after changing them.
- `Shaders/Crystal.shader`: light direction (`_LightDir`), the two-tone step (`smoothstep(0.3, 0.7, ...)`) and the darkening near the floor (`_FloorShade`).
- `Editor/StopklatkaBuilder.cs`:
  - the dynamic resolution range (`quest3Min/MaxDynamicResolutionScale` = 1.0 / 1.205),
  - which scene labels become colliders (`EffectMesh.Labels`) and NavMesh obstacles.

## Known risks to watch

- **Dynamic resolution** depends on the OpenXR plugin version (see `OVRManager`). If `renderViewportScale` never changes, set `XRSettings.eyeTextureResolutionScale` directly in `PerformanceManager`.
- **Door frames:** `EffectMesh.CutHoles = DOOR_FRAME` cuts doorways out of the wall meshes, and the wall colliders are built from those meshes. If bullets still stop in doorways, check in the Scene view that the generated wall `MeshCollider`s have the holes.
- **Floors:** `RoomService.UpdateFloor` takes the floor height from `MRUK.GetCurrentRoom()` every frame. Stairs are outside every room, so the game pauses there on purpose.
- **Reveal effect:** during spawn-in the enemy parts use the `EnemyReveal` material (keyword `_REVEAL`) plus a `MaterialPropertyBlock`, which takes them out of the SRP batcher for 0.9 s. If the keyword variant is stripped from the build, enemies appear without the effect; keep the material in `Resources/Materials`.
- **Boundary suppression** works only while the passthrough layer is active. `PassthroughStyle.Update` keeps it in sync.
- **Shader errors** in `Shaders/*.shader` (URP HLSL with Meta's occlusion include) show up as pink materials. Fix the shaders before anything else.
- **Safety:** enemies spawn only inside scanned rooms, at least 2.5 m away at school and 1.8 m at home (1.2 m at the very least in tiny rooms). Keep it that way. Don't add mechanics that need running or fast backing up.
