# visionOS agent notes (`feat/VisionOS`)

This branch is **Tsugi Vision**, the Apple Vision Pro edition. Read these notes alongside:

- `Docs/VisionOS-PRD.md` — the spec supplement. Its section 0 holds the `V-` decisions, section 2 the
  fork rules, section 4 the milestones.
- `Docs/XR-PRD.md` — the shared XR spec. Everything about rules, content, interaction and flow.
  **Never edited on this branch** (V-c).
- `Docs/XR-Agent.md` — the Quest edition's operational notes. Most of its accumulated traps apply
  here too, because the runtime code is the same code. **Never edited on this branch.**
- the root `CLAUDE.md` — the phone side.

## The fork, in one screen

- `feat/VisionOS` forked from `feat/MetaXR` @ `0a533bf` (XR8). It is a **sibling long-lived fork**:
  it takes `feat/MetaXR` in by merge (which takes `main` in), and **never merges back** (V-b).
- **visionOS adds files; it does not edit Quest files or phone files.** Owned paths and the licensed
  shared exceptions are listed in `Docs/VisionOS-PRD.md` section 2.
- **`TrainSudoku.XR` is shared, not forked** (V-i). `TrainSudoku.VisionOS` references it and adds only
  what differs. When a file under `Assets/01.Scripts/XR/` has to become platform-agnostic, that is a
  **seam commit on `feat/MetaXR`** first, then merged in (V-h) — never an edit here.
- One commit per milestone, `V<n>: <title>`, and **not until the *Verified by human* box is ticked**.
- **Stage files by name; never `git add -A`.**

## Toolchain (macOS)

Unlike the Quest work, which was done on Windows, this edition is developed on **macOS** — visionOS
requires an Apple Silicon Mac.

| Thing | Where / state (checked 2026-09-21) |
|---|---|
| Unity | `/Applications/Unity/Hub/Editor/6000.7.0a6/Unity.app/Contents/MacOS/Unity` |
| visionOS Build Support | **installed**: `…/6000.7.0a6/PlaybackEngines/VisionOSPlayer/` (il2cpp variation only) |
| Xcode | **26.6** at `/Applications/Xcode.app`, with `XROS.platform` and `XRSimulator.platform` |
| visionOS SDK | `xros` and `xrsimulator` both **26.5** |
| Simulator | `Apple Vision Pro` on visionOS 26.5, udid `7A474A3D-1EFF-4652-9787-87480501B295` |
| Physical Vision Pro | **paired** 2026-09-22. `Apple Vision Pro`, `RealityDevice14,1`, identifier `4A3B54DD-4754-598E-8EC5-0246CC4DCF71`, hostname `Apple-Vision-Pro-2.coredevice.local` |
| Signing | One identity: `Apple Development: jorgepedreror1@hotmail.com (T66UQNEA39)`, **team `5F72Z7PR8L`** ("Jorge Pedrero"). Note `appleDeveloperTeamID` in `ProjectSettings.asset` is **empty** and `appleEnableAutomaticSigning` is **0** — that file is shared with the phone, so pass signing to `xcodebuild` instead of editing it: `DEVELOPMENT_TEAM=5F72Z7PR8L CODE_SIGN_STYLE=Automatic` |

**Trap: `xcode-select` must point at Xcode, not the Command Line Tools.** It was at
`/Library/Developer/CommandLineTools` on 2026-09-21, which makes `xcrun --sdk xros …` fail with
"SDK \"xros\" cannot be located" and gives a build no usable SDK. The visionOS package's own
requirements page names this as a prerequisite. Fix, in a **real terminal** (it needs a TTY for the
password; the `!` runner in Claude Code has none):

```bash
sudo xcode-select -s /Applications/Xcode.app
xcrun --sdk xros --show-sdk-version     # should print a version, not an error
```

### Driving the Editor

The same situation as the phone work, and `CLAUDE.md`'s "Commands" section applies unchanged: the
Editor is normally **left open** (check `Temp/UnityLockfile`), which blocks `-batchmode` on the
project lock, so the Unity MCP bridge against the live Editor is the primary way in. Every trap
`Docs/XR-Agent.md` records about the bridge still holds — `CommandScript` must be `internal`, helper
classes must be declared at top level, `UnityEditor.Compilation.CompilationPipeline` and
`UnityEditor.Tools` must be fully qualified, `System.Reflection` is blocked, and long-running work
must write a file that a shell then polls.

**This Mac's `Library/` had never seen the XR packages** — it was last built on `main`. The first open
of an XR branch here reimports the project and resolves five XR packages from scratch, which takes a
long time. Do branch switches and merges with the **Editor closed**, then reopen.

**An Editor left open across a branch switch does not notice.** An unfocused Editor never refreshes,
so it can sit for days with one branch's state in memory and another's on disk. Quit it before
switching; the first thing it would otherwise do on regaining focus is a full reimport, at the worst
possible moment.

### Trap: `.claude/skills` breaks `git merge` in this checkout

Every entry in `.claude/skills/` is a **symlink** into `../../.agents/skills/`, while git still tracks
the real files that used to be there. Git therefore reports ~100 deletions and cannot process those
paths at all:

```
error: '.claude/skills/ask-matt/PHASE-BOUNDARIES.md' is beyond a symbolic link
fatal: Unable to process path …
Cannot save the current worktree state
fatal: stash failed
```

This kills `git merge` **before it merges anything**, and `--no-autostash` and
`-c merge.autoStash=false` do not help — it is not autostash. It is unrelated to this project's
code; nothing under `Assets/`, `Packages/` or `ProjectSettings/` is involved. Until the stale entries
are untracked (`git rm -r --cached .claude/skills`, which deletes nothing on disk), a merge needs
those symlinks moved aside first.

`git merge-tree --write-tree --name-only <ours> <theirs>` is read-only and works regardless, so use it
to check a merge for conflicts before attempting it.

## Traps found while building V1

**`-batchmode -quit` exits 0 with script errors.** `CLAUDE.md` says "script errors land in the log,
exit code non-zero on failure". On this editor that is **not true for compile errors**: a run with 21
`error CS…` lines still exited 0, and `Library/ScriptAssemblies/` simply lacked the assembly that
failed. So **never trust the exit code as a compile check.** Grep the log and check the DLL:

```bash
grep -E "error CS[0-9]+" <log> | sort -u        # the errors themselves
ls Library/ScriptAssemblies/TrainSudoku.VisionOS*.dll   # absent means it did not build
```

**Our namespace shadows the platform API.** The visionOS entry point is a *class* named `VisionOS`
inside the namespace `UnityEngine.XR.VisionOS`, and our own namespace is `TrainSudoku.VisionOS`. So a
bare `VisionOS.QueryAuthorizationStatus(…)` binds to *our* namespace and fails with

```
error CS0234: The type or namespace name 'QueryAuthorizationStatus' does not exist
in the namespace 'TrainSudoku.VisionOS' (are you missing an assembly reference?)
```

which reads like a missing reference rather than the name collision it is. Use the alias
`using VisionOSApi = UnityEngine.XR.VisionOS.VisionOS;`. Types such as `VisionOSAuthorizationType` are
unaffected — only the bare identifier `VisionOS` collides. This is the same class of trap as
`CLAUDE.md`'s rule about fully qualifying `CompilationPipeline` and `Tools` in a bridge snippet.

**`CreateDefaultManagerSettingsForBuildTarget` overwrites.** Its own documentation says it "**will
overwrite** any current settings for that build target", so calling it unconditionally empties the
loader list on every re-run. Call it only when `ManagerSettingsForBuildTarget(group)` is null.
`GetOrCreate()` is `internal`, so reach the asset through
`EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget …)`.
`XRPackageMetadataStore.AssignLoader` wants the loader's **full** type name.

**`Unity.XR.VisionOS` is `includePlatforms: ["Editor", "VisionOS"]`.** So `TrainSudoku.VisionOS` and
`TrainSudoku.VisionOS.Editor` carry the same constraint, which is a feature: **nothing in this
edition's assemblies can compile into a Quest or phone build, so it cannot break one.**

**The world-sensing component installs itself** (`[RuntimeInitializeOnLoadMethod]` under
`#if UNITY_VISIONOS && !UNITY_EDITOR`), rather than being placed in the scene. V1 runs the Quest's own
`Assets/Scenes/XR.unity`, which this branch keeps byte-identical, and a component added in the Editor
would be one careless save away from breaking that. For a deliberate Editor run,
**Window > TrainSudoku > VisionOS > Add World Sensing to Open Scene** adds it without saving.

**Metal app mode needs minimum visionOS 2.0, and Unity defaults to 1.0.** The package's own Project
Validation carries the rule ("Metal, RealityKit, and Hybrid apps require minimum visionOS version
2.0"), so `VisionOSFoundationSetup` sets
`PlayerSettings.VisionOS.targetOSVersionString = "2.0"`. Worth running that validation page whenever
anything behaves oddly — it knows more than the build log does.

The Xcode project is not a reliable way to check this: the post-processor raises
`XROS_DEPLOYMENT_TARGET` to 2.0 on some targets while leaving 1.0 on others, so `grep` finds both
values either way. Check `VisionOSTargetOSVersionString` in `ProjectSettings/ProjectSettings.asset`.

**Unresolved: the app does not render in the visionOS simulator.** Every frame throws

```
NotImplementedException: Unsupported XR layout: 1 render passes
  at UnityEngine.Experimental.Rendering.XRSystem.CreateDefaultLayout (…)
  at UnityEngine.Rendering.Universal.UniversalRenderPipeline.RenderCameraStack (…)
```

What is established:

- The display provider **starts** (`[XR] VisionOSDisplayProvider::Initialize`, `[XR] Display Start`) and
  the Display, Input and Meshing subsystems all load.
- URP's `XRSystem.CreateDefaultLayout` (in `com.unity.render-pipelines.core/Runtime/XR/XRSystem.cs`)
  sets a layout for **1 pass with 2 views**, **2 passes with 1 view each**, or **2 passes with 2 views
  each**. It has **no case for 1 pass with 1 view**, and throws on anything else. So the provider is
  reporting a **monoscopic** layout.
- `VisionOS.IsImmersiveSpaceReady()` is `false`, and nothing in 36 000 lines of console output ever
  says an immersive space opened — consistent with rendering into a flat window.
- **Raising the minimum OS to 2.0 did not fix it.** That was the first hypothesis and it was wrong;
  the setting is kept anyway because validation requires it.
- The package CHANGELOG says the plug-in "will automatically switch between single-pass and multi-pass
  rendering depending on whether the app was built for the visionOS simulator or a device", so **the
  simulator and the device take different rendering paths** and a simulator result does not transfer to
  the device either way.

Not chased further because **the simulator is not what a milestone is verified on** (VisionOS-PRD 4).
The two live hypotheses, for whoever picks this up: the simulator does not support Compositor Services
immersive rendering at all, or the immersive space needs opening explicitly there. Check it on the
device first — if the device renders, this is a simulator-only limitation and the simulator is then
only good for checking `Info.plist` and that the app launches.

**`-buildTarget VisionOS` is mandatory, and leaving it out fails silently.** The visionOS package's
build post-processor — `VisionOSBuildProcessor.PostProcessor`, the thing that writes the immersive-space
scene manifest and the two ARKit usage descriptions into `Info.plist` — lives in a file wrapped
entirely in `#if UNITY_VISIONOS`. That define comes from the Editor's **active build target**, not from
what `BuildPipeline.BuildPlayer` is asked to build. So a batchmode run without `-buildTarget VisionOS`
compiles its editor scripts with the class **absent**, and the post-processor cannot run.

The build still **succeeds**, and it still produces a real visionOS Xcode project (`SDKROOT = xros`,
`SUPPORTED_PLATFORMS = xrsimulator`, the visionOS plugin present), which is what makes this so easy to
miss. What you get is the plain iOS trampoline `Info.plist`:

| Symptom in `Builds-VisionOS*/Info.plist` | What it means |
|---|---|
| `UIApplicationSceneManifest` holds `UIWindowSceneSessionRoleApplication` / `UnityScene` | no immersive space — the app opens as a flat window |
| no `UIApplicationPreferredDefaultSceneSessionRole`, no `UISceneInitialImmersionStyle` | Mixed immersion never applied |
| no `NSHandsTrackingUsageDescription` / `NSWorldSensingUsageDescription` | **visionOS refuses hand tracking and plane detection without prompting** |
| `LSRequiresIPhoneOS = true`, `XROS_DEPLOYMENT_TARGET = 1.0` | the post-processor never touched it |

So **check those keys on every export**, and treat their absence as a failed build however green the
log is:

```bash
/usr/libexec/PlistBuddy -c "Print" Builds-VisionOS-Simulator/Info.plist \
  | grep -E "ImmersionStyle|PreferredDefaultSceneSessionRole|UsageDescription"
```

Note that `-buildTarget` also **changes the Editor's active build target persistently**, so the next
Editor open comes up on visionOS. That is reversible and expected, but it is why the Quest's own
`Docs/XR-Agent.md` insists on making the right build profile active before building.

**A profile-less visionOS build would have shipped the phone game.** The global Build Settings scene
list holds `SampleScene.unity` alone, because the Quest's scene list rides on the Meta Quest build
profile as an override so the phone's stays untouched. A visionOS build inheriting that list would
produce the *phone* game wrapped in an immersive space — and succeed while doing it, with nothing in
the log to say so. `VisionOSBuildScript.Scenes()` therefore names `Assets/Scenes/XR.unity` outright
and never reads `EditorBuildSettings.scenes`. Editing the global list instead would break the phone.

**Unity rewrites the three Barlow SDF atlases to 1×1 when it closes**, exactly as `Docs/XR-Agent.md`
records for a Quest build. It happened here simply from the Editor quitting, and it blocked a merge
(`git merge` refuses to overwrite locally modified files). `git restore` them; never commit them.

## Packages

One pin added to `Packages/manifest.json`, on top of the five `Docs/XR-Agent.md` lists:

| Package | Version |
|---|---|
| `com.unity.xr.visionos` | 3.2.2 |

The version `Docs/XR-PRD.md` 10.5 already named. Checked against the registry on 2026-09-21: it
requires Unity `6000.3.0f1`+ (we are on `6000.7.0a6`), `com.unity.xr.arfoundation` ≥ `6.3.4` (we pin
6.6.2), `com.unity.xr.management` ≥ `4.5.0` and `com.unity.xr.core-utils` ≥ `2.3.0` (both resolve to
the editor's builtin 6.0.0). **It has no PolySpatial dependency.**

**No PolySpatial** (V-e). `com.unity.polyspatial.visionos` is the *meta*-package for RealityKit app
mode, which XR-PRD 12 rules out. Installing it would drag in PolySpatial's constraints — ShaderGraph
materials only, no ShaderLab, no post-processing, and **no UI Toolkit** — and every one of those would
break code the Quest edition already ships. `VisionOSSpatialPointerDevice`, which is what look-and-pinch
needs, is in `com.unity.xr.visionos` itself.

## XR Plug-in Management

- The visionOS loader is assigned to the **visionOS build target only**. Android keeps OpenXR; iOS and
  Standalone keep no XR manager, so pressing Play on `SampleScene` still never starts XR.
- **Call `XRGeneralSettingsPerBuildTarget.CreateDefaultManagerSettingsForBuildTarget` before
  `XRPackageMetadataStore.AssignLoader`** — the same trap `Docs/XR-Agent.md` records for the Android
  setup. Without it the manager is null and `AssignLoader` throws.
- This appends one `buildTarget` entry to `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`, the one
  licensed edit to a Quest-owned file (see `Docs/VisionOS-PRD.md` section 2). A merge conflict there
  is a trivial keep-both.

## visionOS settings

Written through `UnityEditor.XR.VisionOS.VisionOSSettings` (`GetOrCreateSettings()` / `currentSettings`):

| Setting | Value | Why |
|---|---|---|
| `appMode` | `AppMode.Metal` | "Metal Rendering with Compositor Services". X13 |
| `metalImmersionStyle` | `Mixed` | Passthrough. X13. Needs the camera background to be a solid colour with **alpha 0**, the same trick `XR.unity` already uses for Quest |
| `upperLimbVisibility` | visible | The OS draws the player's real arms over the content. This is what replaces `XRDepthOcclusion` on Vision Pro — there is no occlusion provider at all |
| `foveatedRendering` | true | URP only. XR-PRD 8 |
| `handsTrackingUsageDescription` | non-empty | Goes into `Info.plist`. **Empty means ARKit hand tracking is refused** |
| `worldSensingUsageDescription` | non-empty | Goes into `Info.plist`. **Empty means plane detection is refused** |

Project Validation (Project Settings > XR Plug-in Management > Project Validation, visionOS tab) also
wants: the loader enabled, an `ARSession` in the scene beside the `TrackedPoseDriver`, and **depth
texture on in URP with copy-depth mode `AfterOpaques`** — the only mode Metal app mode supports. That
last one is why visionOS gets its own render pipeline asset rather than sharing the Quest's.

## Building

`Assets/01.Scripts/VisionOS/Editor/VisionOSBuildScript.cs`, modelled on the phone's
`Assets/01.Scripts/Editor/BuildScript.cs`, which this branch has by merge from `main`. Its two
inherited lessons:

- **The export is a Replace, never an Append.** An Append preserves by-hand Xcode edits, which is how
  the phone once shipped a build that could not launch. Anything the build needs must be expressed in
  the script or in Player Settings, not in the Xcode project.
- **Read settings back, never echo the intention.** A log line that states what was asked for cannot
  report a setting that declined to change, and that is exactly what hid a silent failure on iOS.

Plus the Quest lessons from `Docs/XR-Agent.md`:

- A queued build (`EditorApplication.delayCall`) **does not start until the Unity window has focus**,
  and **dies with the Editor**. Write a "started" marker at the top of the queued method so a waiting
  loop can tell *not started* from *still building*.
- **A queued build can hang on a pending recompile.** Have the queued method re-queue itself while
  `EditorApplication.isCompiling || isUpdating`.
- Write the outcome to `BuildResult.txt`, because the build outlives the call that started it.
- `Builds-VisionOS/` (device) and `Builds-VisionOS-Simulator/` are separate folders, because the
  export is a Replace and sharing one would destroy the other.

### Running it

**Simulator first.** It costs nothing and proves the export, the `Info.plist` keys and that the
immersive space opens. It has no hands, so it proves nothing about interaction.

**Export from Unity.** `-buildTarget VisionOS` is not optional — see the trap above.

```bash
unity="/Applications/Unity/Hub/Editor/6000.7.0a6/Unity.app/Contents/MacOS/Unity"
"$unity" -batchmode -nographics -quit -projectPath . -buildTarget VisionOS \
  -executeMethod TrainSudoku.VisionOS.Editor.VisionOSBuildScript.BuildVisionOS -simulator \
  -logFile build.log
cat BuildResult.txt        # result=Succeeded, and check sdk= and scene=
```

**Then compile and run it.** The project exports **targets, not schemes** (`xcodebuild -list` shows a
`Unity-VisionOS` target and only a `GameAssembly` scheme), so build the target:

```bash
cd Builds-VisionOS-Simulator
xcodebuild -project Unity-VisionOS.xcodeproj -target Unity-VisionOS \
  -configuration Release -sdk xrsimulator CODE_SIGNING_ALLOWED=NO \
  CONFIGURATION_BUILD_DIR=$PWD/out build

xcrun simctl boot 7A474A3D-1EFF-4652-9787-87480501B295   # the Apple Vision Pro sim
xcrun simctl install booted out/Tsugi.app
xcrun simctl launch --console booted com.GorillaGonzalez.Tsugi.VisionOS
```

**`-derivedDataPath` does not work with `-target`**: it fails with "The flag -scheme,
-testProductsPath, or -xctestrun is required when specifying -derivedDataPath". Use
`CONFIGURATION_BUILD_DIR` instead. And **`xcodebuild` also returned exit 0 on that failure**, so check
the log tail for `BUILD SUCCEEDED` and check the `.app` exists, exactly as with Unity's batchmode.

**On device**, the headset must also be **registered with the developer team**, which pairing in Xcode
does *not* do. The first device build (2026-09-22) compiled and signed fine and then failed to install:

```
Failed to install embedded profile for com.GorillaGonzalez.Tsugi.VisionOS : 0xe8008012
(This provisioning profile cannot be installed on this device.)
```

The team profile (`iOS Team Provisioning Profile: *`) listed only the iPhone
(`00008140-0011319A1A40801C`), not the headset (UDID `00008112-000A40A00C41A01E` — note that is the
hardware UDID from `devicectl device info details`, **not** the CoreDevice identifier `devicectl list`
prints). A `-target` build names no destination, so automatic signing has no device to add. Build the
**`Unity-VisionOS` scheme** (it exists, though a truncated `xcodebuild -list` can hide it) against the
headset with device registration on — the same thing Xcode's Run button does:

```bash
cd Builds-VisionOS
xcodebuild -project Unity-VisionOS.xcodeproj -scheme Unity-VisionOS -configuration Release \
  -destination 'id=00008112-000A40A00C41A01E' \
  DEVELOPMENT_TEAM=5F72Z7PR8L CODE_SIGN_STYLE=Automatic -derivedDataPath build \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
security cms -D -i build/Build/Products/Release-xros/Tsugi.app/embedded.mobileprovision \
  | grep -A4 ProvisionedDevices        # the headset's UDID must be listed
```

Then install and launch:

```bash
xcrun devicectl list devices                       # find the Vision Pro's identifier
xcrun devicectl device install app --device <id> <path to built .app>
xcrun devicectl device process launch --device <id> --console com.GorillaGonzalez.Tsugi.VisionOS
```

The game's own log lines to look for, all of which the inherited code already emits:

- `[XR touch] <panel>: <button> by the <hand> fingertip` — the panel is sign, wrist menu or wrist
  roundel. **These are the proof that fingertip presses work.**
- `[XR wrist] Pressed by …`
- `[XR touch only] …`, once at start: how many interactors lost their rays
- `[XR flow] A -> B`, every flow change
- `[AVP] …` — this edition's own world-sensing and authorization steps

## Device-check findings

One entry per headset check, newest last. The point of this section is that a "no" recorded here is
worth as much as a fix.

### 2026-09-21 — V1, visionOS 26.5 simulator (not the headset)

The simulator is not what a milestone is verified on, but it settled several things cheaply.

| | Result |
|---|---|
| Export | **Works.** A genuine visionOS Xcode project: `SDKROOT = xros`, `SUPPORTED_PLATFORMS = xrsimulator`, the visionOS plugin present under `Libraries/com.unity.xr.visionos`. Unity export 18 s incremental, Xcode compile ~6 min for 772 IL2CPP files |
| `Info.plist` | **Correct**, once `-buildTarget VisionOS` was passed: `UIApplicationPreferredDefaultSceneSessionRole = CPSceneSessionRoleImmersiveSpaceApplication`, `UISceneInitialImmersionStyle = UIImmersionStyleMixed` (X13), `NSHandsTrackingUsageDescription`, `NSWorldSensingUsageDescription` |
| App launches | **Yes.** The visionOS XR loader initialises and the Display, Input and Meshing subsystems load |
| Self-installing component | **Works.** `[AVP] World sensing starting. simulator=True` — the `RuntimeInitializeOnLoadMethod` put it in the running scene with no scene asset change |
| **Finding 3 — the `XRScenePermission` workaround** | **Works.** `[AVP] Plane detection enabled on 1 manager(s)`, then `WorldSensing is Allowed` and `HandTracking is Allowed`. So enabling `ARPlaneManager` from the visionOS side is enough to get ARKit authorization. **Whether the board then uses the planes could not be seen here** — the device run showed it does not (see the next section) |
| Rendering | **Fails** — `Unsupported XR layout: 1 render passes`, every frame. See the trap above. Unresolved, and deliberately left for the device |
| Findings 1 and 2 (UI Toolkit signboard, `<XRHandDevice>` poses) | **Not answerable here.** Nothing renders, and the simulator has no hands |

Oddity worth remembering: `QueryAuthorizationStatus` returned `Allowed` for both types while the
`AuthorizationChanged` event then reported `NotDetermined` for both. The two disagree in the simulator,
which is why `VisionOSWorldSensing` both polls and subscribes rather than trusting either alone.

### 2026-09-22 — V1, first run on the Apple Vision Pro (RealityDevice14,1)

| | Result |
|---|---|
| Install | Needed the headset **registered with the team** first (see "Running it") |
| Launch | Needs the headset worn and unlocked; `devicectl … launch` waits silently after "Acquired usage assertion" until it is |
| `Unsupported XR layout` | **Zero** on the device. So the simulator failure was simulator-only, as suspected |
| **Finding 3** | **Half right — the seam IS needed.** Authorization works on hardware (real prompts, both `NotDetermined` → `Allowed`) and planes are detected (`[AVP] First surfaces detected: 6`). **But the board ignores them.** `XRBoardPlacement` asks `XRScenePermission` for itself (`_surfacesAllowed = await RequestPermissionAsync()`, line 122), which still answers refused off Android, and only tries surfaces when that was yes (line 247). The log says so plainly: `[XR placement] Spatial data refused; locating.` — and the ghost floats with the head. So enabling `ARPlaneManager` from outside is **not** enough, and the `IScenePermission` seam goes on `feat/MetaXR` in V2 (V-h). An earlier note here, written after the simulator run, claimed the opposite; it was premature |
| Exceptions | None |
| **What the player saw** | **Blinking green and pink pixels, nothing else.** Fixed below; after the fix the yellow placement ghost rendered and followed the head |
| **Finding 2 — `<XRHandDevice>` poses** | **No. V1's central hypothesis was wrong.** The rig's 3D hands tracked the player's real hands, so **joints work**, but a pinch did nothing. The rig's `Pinch Position`, `Poke Position`, `Aim Position` and `Select` actions all read XR Hands' *common gestures* (`<XRHandDevice>/pinchPosition`, `/pokePosition`, `/aimPosition`, `/pinchTouched`, `/pinchValue`), and **the visionOS provider supplies no common gestures at all** — no occurrence of `CommonHandGestures`, `aimPose` or `pinchValue` anywhere in `com.unity.xr.visionos/Runtime`. So those controls exist and read zero. The missing aim pose is also why the ghost followed the head: `XRBoardPlacement` aims with the right hand's ray, then the left's, then the gaze |

**The fix for finding 2 — `VisionOSHandInput`** (`Assets/01.Scripts/VisionOS/`, self-installing like
`VisionOSWorldSensing`). XR Hands only fills those controls through internal setters, so they cannot be
fed from outside. But nothing in the game reads them directly: everything goes through the rig's XRI
interactors, so it feeds those instead, and edits no Quest file:

- **Pinch and poke poses** — switches off the `TrackedPoseDriver`s bound to `Pinch Position` and
  `Poke Position` (found by action name, not object name) and drives their transforms from the
  **thumb-tip, index-tip and index-proximal joints**, through the rig's camera offset.
- **Select** — through `XRInputButtonReader.bypass`, XRI's own injection point: every read of a
  near-far interactor's select input is answered from the joint distance. On below 1.5 cm, off above
  3 cm; the gap stops a pinch held at the threshold from chattering, which a grab would read as
  drop-and-regrab.
- **Aim is deliberately not supplied.** The Quest has no rays (X16), and without an aim pose placement
  uses the gaze — the headset's own look-and-pinch.

It logs under `[AVP hands]`: what it wired, each hand tracked or lost, and each pinch on and off.

### 2026-09-22 — V1, first full playthrough on the headset

With `VisionOSHandInput` in, **a whole station was played**: `MainMenu → Network → LevelSelect → Play →
TrainRun → Win`, 40 pinches, zero exceptions. Getting from the network map into a station means the
**map roundels took fingertip presses**, so the poke pose works too. The player asked for three fixes:

| Report | Cause | Fix |
|---|---|---|
| "Remove the hand mesh" | The rig carries skinned hand meshes (Quest and AndroidXR versions of each hand, plus XR Hands' `Hand Visualizer`) drawn over the player's real hands, which the OS already composites (`upperLimbVisibility`) | `VisionOSHandInput.HideRigVisuals` disables every renderer under the XR Origin **except the `TrackablesParent` subtree**, every frame (the `Hand Visualizer` instantiates its meshes only once a hand is first tracked). **The exception is the whole game**: AR Foundation puts every anchor under `XROrigin.TrackablesParent`, and the board is parented to its anchor. The first version swept the whole origin on the false belief that nothing of the game lived there, and the next headset run showed the ghost and then nothing — placing the board moved it under the origin and hid it (the hidden count climbed 188 → 214 as the board was built). Hands, controllers and pinch visuals are all under the camera offset |
| "Remove the transparent white meshes" | Two sources: the rig's `PinchPointStabilized` blob between thumb and index (`FresnelHighlight`, grey at 33% alpha — taken by the same sweep), and **the detected planes** (`XRDetectedPlane.mat`, white at 12%), which `XRBoardPlacement.ShowPlanes` shows while choosing a surface | `VisionOSWorldSensing.HidePlanes` disables plane renderers in `LateUpdate`, after `ShowPlanes` re-enables them in `Update`. Colliders kept: placement and falling pieces use them. visionOS never shows its scanned surfaces, and the ghost settling on a table is feedback enough |
| "The win UI is unstable, it shakes and disappears at times" | **Hypothesis, not yet confirmed**: Metal mode reprojects each frame per pixel from the depth buffer, and a world-space UI Toolkit panel is transparent and writes no depth. Card pixels over the board reproject at the board's depth, those over nothing at the far plane — so one flat card moves by different amounts as the head moves, and in Mixed immersion the far-plane parts can come through as passthrough. The Quest's OpenXR depth submission is off (`m_depthSubmissionMode: 0`), so its timewarp never depended on depth, which is why the same card was steady there | First attempt, `VisionOSPanelBacking`: an ordinary opaque plate (the posts' steel) exactly the card's shape, 1 mm behind it. It **stopped the vanishing** — the card was "always present" on the next run — but the card was **grey in both eyes**: the plate won over the card everywhere although the card is opaque paper and the plate was behind it. (Tested one eye at a time, which ruled out the other live theory, that UI Toolkit renders into the left eye only.) UI Toolkit's shaders are compiled into the editor, so whether it draws before the opaque pass or puts its geometry behind its transform cannot be read. **Replaced by `VisionOSPanelDepth`**, which depends on neither: a depth-only proxy (`VisionOSDepthProxy.shader`: `ColorMask 0`, `ZWrite On`, stereo-instanced, queued `Transparent+100`) drawn **after** the UI, 1 cm behind the card, so it fills in the depth the UI never wrote and has no colour to cover it with. Material at `02.Graphics/VisionOS/Resources/VisionOSDepthProxy.mat`, created by `VisionOSFoundationSetup`. Logs `[AVP panel] Depth proxy added`. If the card shakes again with that line present, the next suspect is the sign billboarding in `LateUpdate` to a head pose the renderer then moves on from |

**Cause of the pixels (first diagnosis):** the build was rendering with the **phone's
`Mobile_RPAsset`**, not `VisionOS_RPAsset`. The per-platform quality table had no visionOS entry, so
visionOS fell to level 0, `Mobile` — no depth texture (`m_RequireDepthTexture: 0`), HDR on, render scale
0.8. visionOS reprojects every frame from the submitted depth, and the package's own validation requires
a depth texture on every camera. The tell in the log was `PostProcess:.ctor(PostProcessData)` —
post-processing being constructed at all, which `VisionOS_RPAsset`'s renderer never would. The
pipeline asset was correct but unreachable: the only thing that pointed at it was the build profile,
which does not exist yet. `VisionOSFoundationSetup.CreatePipeline` now appends a `visionOS` quality
level (excluded from Standalone, WebGL, Android, iPhone and tvOS) and makes it visionOS's default, and
reads the result back through `QualitySettings.GetRenderPipelineAssetsForPlatform` — the call the
package's validator uses.

### 2026-09-23 — V1, world-space UI Toolkit, and the two white rectangles

**Finding 1 is answered, and the answer is no.** `Docs/XR-PRD.md` section 13 left "world-space UI
Toolkit on Vision Pro Metal mode" unverified. A screenshot settled it: the signboard's **posts stood
on the platform while its card followed the head**. The posts are ordinary meshes, the card is a
world-space `UIDocument` — so world-space UI Toolkit is *not* rendered in world space here.

That one fact explains, in order, every earlier symptom and both fixes built on top of them:

| Earlier report | What it actually was |
|---|---|
| "The win UI shakes and disappears at times" | A head-locked overlay inside a reprojected frame |
| The card went "grey in both eyes" (`VisionOSPanelBacking`) | An opaque plate at the sign's *true* position while the card stayed stuck to the view |
| No UI at all (`VisionOSPanelDepth`) | That same plate, made invisible |

Two fixes were built on the wrong diagnosis before the screenshot showed the posts and the card in
different places. **Neither survives** — `VisionOSPanelBacking` and `VisionOSPanelDepth` are gone,
replaced by `VisionOSPanelSurface`: each world-space document gets its own copy of its `PanelSettings`
pointed at a `RenderTexture`, and a quad at the document's own transform shows that texture. The panel
becomes an ordinary mesh — stereo-correct, depth-writing, reprojected like everything else — so the
shimmer question is settled by the same change, with no depth proxy. Nothing above it changes:
`XRPanelTouch`'s fingertip mapping and the Quest's own view-building are untouched (VisionOS-PRD 2).

**Then the surfaces came out white — both of them.** The headset run showed a solid white card over
the board and a second white rectangle floating near the player. Two separate causes, both now fixed:

| Report | Cause | Fix |
|---|---|---|
| "The panel is still white" | The quad's material was given its texture through **`Material.mainTexture`**, which writes whichever property carries the `[MainTexture]` attribute and **falls back to `_MainTex`** when none does. `VisionOSPanelSurface.shader` declared plain `_BaseMap`, so the write landed nowhere and `_BaseMap` kept its `"white"` default — and because that default is **opaque**, the alpha clip discarded nothing. A fully white card is the exact signature of an unbound `_BaseMap`; an empty *panel* would have been invisible instead, every pixel failing the clip | The texture is bound **by name** (`Shader.PropertyToID("_BaseMap")`), and the shader now carries `[MainTexture]` as well, the way every URP shader tags `_BaseMap`. Either route works; the code depends on neither |
| "A flying white quad to my left — is it the pause button?" | **Yes, it was the wrist roundel.** The two panels hide themselves differently: `XRSignboard.Hide` deactivates its GameObject, which takes the quad (a child of the document transform) with it, but `XRWristMenu.SetVisible` sets `root.style.visibility` and **deliberately leaves the GameObject on** — a document switched off loses what was built into it (XR8). The quad is an ordinary `MeshRenderer` and knew nothing about that, so a roundel the game believed hidden kept drawing | `VisionOSPanelSurface.FollowVisibility` reads each root's resolved visibility and display each `LateUpdate` and switches its renderer to match. Clearing the panel to transparent would probably have covered it, but that leans on UI Toolkit still clearing a panel it draws nothing into — and a stale texture would freeze a card in the room |

Also changed while in there: the panel `RenderTexture` is created with **24 bits of depth, not 0**,
because that is what carries the stencil and **UI Toolkit clips with the stencil buffer**. With none,
anything the card masks — a rounded corner, an `overflow: hidden` group — stops being cut.

**Next:** run the device build and check the card actually reads. The open questions after that are
whether the surface is steady under reprojection (which is what the whole change was for) and whether
`XRPanelTouch` still lands its presses now that the panel renders through a texture — it works off
layout coordinates and the document transform rather than panel picking, so it should be untouched,
but it has not been pressed on the headset since.
