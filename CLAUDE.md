# Stopklatka: instructions for Claude Code on the developer's PC

Stopklatka is a Superhot-style mixed reality game for Meta Quest 3, built with Unity. The player walks around the real school they scanned with Space Setup, holding Touch controllers. Time moves only when they move. Red crystal enemies appear inside the scanned rooms.

The owner (Pl0T3R) speaks Polish, so talk to them in Polish. Code, comments and commit messages stay in English.

Read `docs/PLAN.md` for the full design and its requirements:
- 120 Hz,
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

## Where to tune (most likely needed on the real device)

- `Scripts/Core/Tuning.cs`: time feel, bullet speeds, enemy speed, reaction time, wave sizes.
- `Scripts/Visuals/Factory.cs`:
  - `GunHoldPosition` / `GunHoldEuler`: if guns sit tilted in the hand, fix this first,
  - the katana and bottle hold poses.
- `Scripts/UI/Hud.cs` and `MenuTarget.cs`: text scale. TextMesh sizes are estimates.
- `Scripts/World/PassthroughStyle.cs`: how washed-out the real world looks. Keep it moderate for safety.
- `Editor/StopklatkaBuilder.cs`:
  - the dynamic resolution range (`quest3Min/MaxDynamicResolutionScale` = 1.0 / 1.205),
  - which scene labels become colliders (`EffectMesh.Labels`) and NavMesh obstacles.

## Known risks to watch

- **Dynamic resolution** depends on the OpenXR plugin version (see `OVRManager`). If `renderViewportScale` never changes, set `XRSettings.eyeTextureResolutionScale` directly in `PerformanceManager`.
- **Door frames:** wall colliders are whole boxes, so bullets also stop at doorways.
- **Boundary suppression** works only while the passthrough layer is active. `PassthroughStyle.Update` keeps it in sync.
- **Shader errors** in `Shaders/*.shader` (URP HLSL with Meta's occlusion include) show up as pink materials. Fix the shaders before anything else.
- **Safety:** enemies spawn only inside scanned rooms, at least 2.5 m away. Keep it that way. Don't add mechanics that need running or fast backing up.
