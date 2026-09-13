# XR agent notes (`feat/MetaXR`)

This branch is **Tsugi XR**, the Meta Quest edition. Read these notes alongside two other files:
- `Docs/XR-PRD.md` is the spec. Its section 10 says how the fork works.
- The root `CLAUDE.md` still holds everything about the phone side.

## The fork (XR-PRD 10.2)

- `feat/MetaXR` is a **long-lived fork**: it takes `main` in by merge and never merges back. The phone keeps developing on `main`.
- **XR adds files; it does not edit phone files.** XR-owned paths:
  - `Assets/01.Scripts/XR/` (with `Rules/` and `Editor/`)
  - `Assets/02.Graphics/XR/`
  - `Assets/03.Data/XR/`
  - `Assets/99.Test/XR/`
  - `Assets/Samples/`
  - `Assets/Scenes/XR.unity`
  - `Assets/XR/` (generated XR Plug-in Management and OpenXR settings)
  - `Assets/XRI/` and `Assets/CompositionLayers/` (settings the XRI and Composition Layers packages generate)
  - `Assets/Settings/Build Profiles/Meta Quest.asset`
- **Quest settings live on the Meta Quest build profile's overrides** (Player, Graphics, Quality, scene list), never on the global Android or iOS settings. The project-wide files XR touches are `Packages/manifest.json`, `packages-lock.json`, and the config-object lines that XR Management, OpenXR and AR Foundation append to `ProjectSettings/EditorBuildSettings.asset`.
- **Shared changes land on `main` first.** That covers `Core`, the level assets and the four data types that stay in `TrainSudoku.Game` (`LevelDefinition`, `LevelCollection`, `LineDefinition`, `NetworkDefinition`). XR references `TrainSudoku.Game` for those four only.
- A commit here needs the phone's EditMode suite green and `SampleScene` still playing in the Editor. Phone builds ship from `main`.
- There is **no Meta XR SDK** (XR-PRD X12). Never run `metavr`'s Unity setup tool on this project: it installs Meta XR Core and an `OVRCameraRig`.

## Pinned packages (XR-PRD 10.5)

| Package | Version |
|---|---|
| `com.unity.xr.openxr` | 1.18.0 |
| `com.unity.xr.meta-openxr` | 2.6.1 |
| `com.unity.xr.interaction.toolkit` | 3.7.0-pre.1 |
| `com.unity.xr.arfoundation` | 6.6.2 |
| `com.unity.xr.hands` | 1.10.0-pre.1 |

XR Management and Composition Layers come in as dependencies. If a pin breaks, fall back to the version the a6 editor bundles and record the change in XR-PRD 10.5.

## XR assemblies

| Assembly | Location | Holds |
|---|---|---|
| `TrainSudoku.XR.Rules` | `Assets/01.Scripts/XR/Rules/` | `PieceDrop` (XR-PRD 4.4): grabbing from the tray or a cell, the ghost tint, and every release outcome. It works on the shared `Board` through `TryPlace`, `TryErase` and `Legality`. `noEngineReferences`; references only Core |
| `TrainSudoku.XR` | `Assets/01.Scripts/XR/` | The XR runtime. References Core, `TrainSudoku.Game` (the four level data types only), XR.Rules and the XR packages. Holds: `Board/` (display, materials, assets, occlusion materials, forked mesh code); `Train/XRTrainRun`; `Room/` (placement, handle, permission, depth occlusion); `XRPalette`; and `XRBoardDemo` until XR7 |
| `TrainSudoku.XR.Editor` | `Assets/01.Scripts/XR/Editor/` | `XRFoundationSetup`, the Window > TrainSudoku > XR menu (Create Render Pipeline, Build XR Scene, Add Board Demo, Add Board Placement, Add Depth Occlusion); and `XRManifestPermissions` |
| `TrainSudoku.XR.Tests.EditMode` | `Assets/99.Test/XR/EditMode/` | `PieceDropTests`, with its own fixtures in `XRTestBoards`. It never uses the phone's test assembly, which would pull in Game and Editor |

- **Every `PieceDrop` call returns a `DropResult`:**
  - `Outcome`: `Taken`, `Lifted`, `Placed`, `Moved`, `Replaced`, `Returned`, `Puffed`, `Thrown` or `Refused`.
  - `IllegalDrop`, for the coach: a `Returned`, or a `Puffed` whose way back was closed.
  - `BoardChanged`, for the validator.
  - `RefuseReason`. `FixedPiece` is the case that plays the wobble and the "that's fixed" note.
- **`Hover` never changes the board**, and `GhostAgreesWithTheReleaseOnEveryCellAndKey` holds the ghost tint to the real release outcome.
- **The board (XR4).**
  - **What it draws.** `XRBoardDisplay` builds tiles, tunnels, clue signs, pieces, platform-edge decals and the validator colouring from Core's `BoardLayout`, one unit per cell.
  - **Scale.** The parent is scaled 0.06, so a cell is 6 cm. A 6x6 board comes out 0.52 x 0.46 m and an 8x8 0.64 x 0.58 m, tunnels and clue signs included.
  - **No input.** Whatever changes `Board` calls `Sync(animate)`, which brings the pieces on show into line and recolours the clues.
  - **Clue signs** are the phone's chip stood on a pole, and turn about the vertical to face `Camera.main` every frame. The destination plate on the locomotive does the same.
  - **Forked mesh code.** `XR/Board/` holds forks of the phone's mesh code (`TrackMeshBender`, `TrackMeshResampler`, `TrackMeshProfile`, `ProceduralTrackMesh`, `ProceduralBoardMesh`, `PieceView`, `PopScale`). Each is marked with a one-line fork note at the top and is never synced with the phone's copy.
  - **`XRBoardAssets`** was seeded field by field from the phone's `TrackAssets` and `TrainAssets`, so it has the same kit and tuning.
- **The board in the room (XR5).**
  - **Placement.** `Room/XRBoardPlacement` first tries to restore the saved anchor. The anchor's GUID lives in `PlayerPrefs` under `tsugi.xr.boardAnchor`, never in `save.json`.
    - Without one, it asks for spatial data and slides a ghost over detected horizontal planes under the right-hand ray (then the left hand's, then the gaze). If there is no surface, the ghost floats at waist height.
    - A pinch or trigger places it.
  - **`BoardRoot`** is the board's parent:
    - Scaled 0.06, with its origin at the middle of the near edge, on the surface, and +Z pointing away from the player.
    - `XRBoardDisplay` lifts itself by the slab height and puts its near edge on that origin, so a bigger board grows away from the player.
  - **Handle.** `Room/XRBoardHandle` is an `XRSimpleInteractable`, not a grab interactable.
    - A grab interactable detaches the grabbed object from its parent for the length of the grab, which left the bar in the air while the board moved.
    - Its own code moves `BoardRoot` freely, height included, turning it about the grab point so the bar stays in the hand.
    - One hand: wrist twist (roll about the hand's pointing axis) turns the board, at 1.5 degrees per degree (`TwistGain`).
    - Two hands on the bar: their midpoint carries the board, and the line between them steers it.
    - On release, `XRBoardPlacement` settles the board onto a detected surface within 5 cm (`SettleReach`), or leaves it floating. It then re-anchors, saves the new anchor and erases the old one.
    - Surfaces stay enabled (unseen) after an anchor restore so this still works.
  - **Anchor order.** A new anchor is attached before the old one is removed, because removing an anchor destroys its GameObject and anything still parented to it.
  - **Shadow catcher.** `02.Graphics/XR/Shaders/XRShadowCatcher.shader` darkens the passthrough table where the main light is blocked. The display builds it 3 cells wider than the board and shows it only when the board sits on a surface.
  - **Occlusion.** `Room/XRDepthOcclusion` switches on AR Foundation's `AROcclusionManager` and `ARShaderOcclusion` once spatial data is granted. They give hard occlusion from environment depth.
    - It also publishes `_XRWorldToTrackables` and `_XROcclusionBias`.
    - Every board, piece, sign, text and train material is made from the `Occluded Lit` or `Occluded Text` template through `Board/XROcclusionMaterials`.
    - The shared clip lives in `Shaders/XROcclusion.hlsl`.
  - **Locomotion** in the XRI rig is switched off, because the world never moves the player (4.5, 8).
- **The XR4 demo.** `XRBoardDemo` places the board 0.6 m ahead of the head and 0.5 m below it, facing the gaze, one second after start.
  - It then plays every station in network order: the level, then its solution laid along the route, then the train, then the next station.
  - It solves on a background thread, so the frame rate holds.
  - The XR flow replaces it at XR7.
- **The rules tests also run outside Unity.** A scratch `net10.0` NUnit project that compiles `Core/**`, `XR/Rules/**` and `99.Test/XR/EditMode/**` runs them with `dotnet test` in well under a second. That also proves the assembly stays engine-free.

## Driving the Editor

- **Windows paths.**
  - Unity: `C:\Program Files\Unity\Hub\Editor\6000.7.0a6\Editor\Unity.exe`.
  - The Android module's SDK, NDK and JDK: `Editor\Data\PlaybackEngines\AndroidPlayer\`.
  - `aapt`: `...\AndroidPlayer\SDK\build-tools\<version>\aapt.exe`.
- **The Editor is usually open.** That blocks batchmode, which fails on the project lock.
  - The `unity-mcp` bridge drives whichever Editor is open, so probe `Application.dataPath` first.
  - Without the bridge, compile-check with dotnet against Unity's DLLs, have the user run the Test Runner, and read `%LOCALAPPDATA%\Unity\Editor\Editor.log` for compile errors.
- **Switching between `main` and this branch in one checkout reimports everything.** For parallel phone work, use a second git worktree.
- **`metavr`** lives at `~/.metavr/bin/metavr`. `adb` comes with Meta Quest Developer Hub.
- **`Unity_RunCommand` refuses `AssetDatabase.DeleteAsset`** as a "user interaction", so delete untracked files from the shell.
  - The Test Runner does work through the bridge: `TestRunnerApi.Execute` with an `ICallbacks` sink that writes its counts to a file, which a background shell loop then waits on. The full EditMode suite ran 390 tests in about 13 s on 2026-09-13.
  - Entering and leaving Play mode work too (`EditorApplication.EnterPlaymode` / `ExitPlaymode`), one call each, because entering Play reloads the domain.

## XR2 Editor setup

**Done from the agent through Unity MCP on 2026-09-13.** Every step can be repeated.

1. **Packages** resolved at the pins above.
2. **Samples** imported with `UnityEditor.PackageManager.UI.Sample.FindByPackage(...)` then `Import()`:
   - XR Interaction Toolkit: **Starter Assets** and **Hands Interaction Demo**.
   - XR Hands: **HandVisualizer**.
3. **Pipeline and scene:** **Window > TrainSudoku > XR > Create Render Pipeline**, then **Build XR Scene** (`XRFoundationSetup`).
   - The pipeline is `02.Graphics/XR/XR_RPAsset` plus `XR_Renderer`: a copy of `Mobile_RPAsset` with Forward, no renderer features, post-processing off, HDR off and 4x MSAA.
   - `Scenes/XR.unity` holds:
     - An AR Session.
     - The `XR Origin Hands (XR Rig)` sample rig. It is a variant of the Starter Assets rig, which carries the Input Action Manager.
     - A passthrough camera: clear colour alpha 0, with an AR Camera Manager.
     - A light, and a 6 cm cube at (0, 0.9, 0.6).
4. **XR Plug-in Management:** the OpenXR loader is on Android only. Standalone and iOS have no XR manager, so Play on `SampleScene` never starts XR.
   - Call `XRGeneralSettingsPerBuildTarget.CreateDefaultManagerSettingsForBuildTarget` **before** `XRPackageMetadataStore.AssignLoader`. Otherwise the manager is null and `AssignLoader` throws.
5. **OpenXR on Android:**
   - Features: Meta Quest Support, Meta Quest Session, Meta Quest Camera (Passthrough), Hand Tracking Subsystem, Meta Hand Tracking Aim.
   - Interaction profiles: Oculus Touch, Meta Quest Touch Plus, Hand Interaction.
   - Target devices: Quest 3 and Quest 3S only. **Quest 3 is listed under its codename `eureka`** (Quest Pro is `cambria`), so a check for `quest3` misses it.
   - Latency Optimization: Prioritize Input Polling.

**The Meta Quest build profile** (2026-09-13):

6. **Create it in the window:** **File > Build Profiles > Add Build Profile > Meta Quest**, installing no partner packages.
   - `BuildProfile.CreateBuildProfile` with the Meta Quest platform id (`80657fe557de4d17822398b3a01b8c9e`) writes an asset whose Android platform settings stay empty. Use the window.
   - The scene list is set from script (`overrideGlobalScenes`, `scenes`) to `Assets/Scenes/XR.unity` only.
7. **Player settings live on the profile's `Player Settings` sub-asset** (`BuildProfilePlayerSettings`).
   - It is created with Meta Quest defaults: Landscape Left, Vulkan only, IL2CPP, ARM64, min API 29, target API 32.
   - The XR values were written through a `SerializedObject` on that sub-asset: `productName` `Tsugi XR`, the Android `applicationIdentifier` `com.GorillaGonzalez.Tsugi.XR`, `AndroidMinSdkVersion` 32, `AndroidTargetSdkVersion` 34.
   - **Trap:** Edit > Project Settings > Player edits whatever the *active* profile resolves to. With the phone's `Android™` profile active, it edits the phone's global `ProjectSettings.asset`. That happened once (product name `TsugiXR`, landscape, API 32/34, Vulkan only) and was put back to the committed values.
   - Make the Meta Quest profile active before touching XR Player settings.
8. **Render pipeline.** Creating the profile appended a `Meta Quest (Build Profile)` quality level to `QualitySettings.asset` and made it the profile's default. Its `customRenderPipeline` now points at `XR_RPAsset`; it had pointed at URP's built-in default asset.
9. **The OpenXR validator reads the active profile's values.**
   - With Meta Quest active, the only issue left is a warning about the SSAO feature on the phone's renderer.
   - With `Android™` active, it shows two errors (Landscape Left, minimum API 32) that do not apply to the Quest profile.
   - Validation errors **fail the build** (`OpenXRProjectValidationRulesSetup.cs`, `throw new BuildFailedException`), so build from the Meta Quest profile.
   - Global `ProjectSettings.asset` now differs from `main` only by the input define symbols XRI adds (`USE_INPUT_SYSTEM_POSE_CONTROL;USE_STICK_CONTROL_THUMBSTICKS`).

## Building and running on a Quest

1. **Connect the headset.** Put it in developer mode, connect it over USB and accept USB debugging. `metavr device list` should show it. The first test headset is a Quest 3 (`eureka`), serial `2G0YC5ZF7P0259`, Horizon OS on Android 14.
2. **Build from script.** Queue `BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile = <Meta Quest profile>, locationPathName = "builds/TsugiXR.apk" })` on `EditorApplication.delayCall`, and have it write its `BuildReport` summary to a file that a background shell loop waits on.
   - **The queued call does not run until the Unity window has focus.**
   - A first IL2CPP build took 12 min 54 s. The APK is about 64 MB, in `builds/`, which is gitignored.
3. **Check the APK** with `aapt` from `...\AndroidPlayer\SDK\build-tools\36.0.0\aapt.exe`: `dump badging`, `dump permissions`, and `dump xmltree <apk> AndroidManifest.xml`. The first build showed:
   - Package `com.GorillaGonzalez.Tsugi.XR`, label `Tsugi XR`, min SDK 32, target SDK 34, `arm64-v8a`.
   - The `com.oculus.intent.category.VR` launcher category, and `com.oculus.vr.focusaware`.
   - Features: `com.oculus.feature.PASSTHROUGH` required, `oculus.software.handtracking` optional.
   - Permissions: `INTERNET`, `com.oculus.permission.HAND_TRACKING` and the two OpenXR permissions. Nothing prohibited.
4. **Install and launch** with `metavr app install builds/TsugiXR.apk`, then `metavr app launch com.GorillaGonzalez.Tsugi.XR`. Check that `adb shell dumpsys activity activities` shows the Unity activity as `topResumedActivity`.
   - The `Unity`-tagged logcat holds the OpenXR diagnostic report. The session should reach `XR_SESSION_STATE_FOCUSED`.

**Package workaround: `USE_SCENE` in the manifest.**
- **The bug.** OpenXR Meta 2.6.1 requests its permissions through XR Management's manifest `OverrideElements`. That matches existing elements by path alone.
  - Its three `uses-permission` requests (`IMPORT_EXPORT_IOT_MAP_DATA`, `USE_SCENE`, `USE_ANCHOR_API`) all land on one node, so only `USE_ANCHOR_API` survives.
  - Without `USE_SCENE` declared, the runtime request is refused outright and plane detection never starts.
- **The workaround.** `XR/Editor/XRManifestPermissions` is an `IPostGenerateGradleAndroidProject` hook. It adds `USE_SCENE` to `unityLibrary/src/main/AndroidManifest.xml` whenever the OpenXR Planes feature is on for Android.
- **Check every Quest APK** with `aapt dump permissions`.
- **Remove the hook** once the package merges its permissions correctly.

**Harmless log lines on Quest 3 (runtime 207):**
- `xrSetHandTrackingFrequencyHintMETA ... XR_ERROR_FUNCTION_UNSUPPORTED` and `Failed to look up xrDiscoverSpacesMETA`.
- Requested extensions the runtime lacks: `XR_FB_scene_capture` and `XR_OCULUS_android_initialize_loader`.
- `TryGetFrame returned false because camera image support is not enabled`. That is AR Foundation asking for passthrough camera images, which the game does not use. The passthrough itself still shows.
- `Entitlement for packageName=... not found`. Expected for a sideloaded build.

**Store items for XR11**, found in the first APK:
- `installLocation` is `preferExternal` (inherited). Meta requires `auto`.
- There is no `android.hardware.vr.headtracking` `uses-feature`. Meta requires it for immersive apps.
- `com.oculus.supportedDevices` is `eureka|quest3s`: OpenXR writes Quest 3 by codename. Meta documents `quest3`, so confirm the store accepts the codename.
- There is no `excludeFromRecents` on the launch activity.
- An entitlement check will need Meta's Platform SDK. Check that it installs without Meta XR Core.

**A Quest build also rewrites some phone assets.** Restore them with `git restore` before committing:
- It clears the dynamic atlases of the three Barlow SDF fonts in `02.Graphics/Fonts/SDF/` to 1x1.
- It rewrites two shader-prefiltering fields in `Mobile_RPAsset`.
- The Editor re-saves `SampleScene` with `GameManager`'s empty `music` and `musicPlayer` fields.

## Local files that stay out of commits

These are left over from before the fork, or generated:
- The CRLF-only diffs in 5 settings files.
- `Assets/Settings/Build Profiles/Android™.asset`.
- `Assets/03.Data/AddressableAssetsData/Android/` and `link.xml`.
- `user.keystore` (ignored by `*.keystore`).

Stage files by name; never `git add -A`.
