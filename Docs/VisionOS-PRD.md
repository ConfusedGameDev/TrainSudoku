# Tsugi Vision — Product Requirements Document (supplement)

(The Apple Vision Pro edition of **Tsugi**. Like the phone game and the Quest edition, the code keeps
the working name TrainSudoku.)

Status: opened 2026-09-21. This is the **visionOS half of XR12 and XR13**, developed on its own
long-lived branch `feat/VisionOS`, forked from `feat/MetaXR` at `0a533bf` (XR8).

This document is a **supplement**, not a replacement. `Docs/XR-PRD.md` remains the spec for
everything the two headset editions share, and it already settled the decisions that matter here:

- **X13 / XR-PRD 2** — Vision Pro runs **Metal mode, Mixed immersion, Full Space**. The same URP
  pipeline, the same post-processing rules and the same **world-space UI Toolkit** as Quest.
- **X12** — everything rides Unity's cross-platform stack behind *our own* grab interface
  (XR-PRD 10.4), so "Vision Pro is a new platform, not a rewrite". No Meta SDK is used anywhere.
- **X23** — an Apple Vision Pro is on hand. Every milestone is verified on the real headset.
- **XR-PRD 12** — **RealityKit mode and the Shared Space are out of scope.** PolySpatial is therefore
  never installed, and none of its limits (ShaderGraph-only materials, no UI Toolkit, no
  post-processing) apply to us.

Read alongside:

| File | Governs |
|---|---|
| `Docs/XR-PRD.md` | rules, content, interaction, flow, presentation, the grab interface, the fork rules. **Never edited on this branch** (see V-c) |
| `Docs/XR-Agent.md` | how the Quest edition is built and driven. **Never edited on this branch** |
| `Docs/VisionOS-Agent.md` | how *this* edition is built, driven and deployed, plus every device-check finding |
| `Docs/PRD.md`, `Docs/UIDesign.MD` | the phone. Unchanged by anything here |

Where this document and `Docs/XR-PRD.md` disagree, this one wins for the visionOS build and XR-PRD
wins for the Quest build.

---

## 0. Decisions taken

Opened 2026-09-21 by interview. Closed. Do not re-open one without asking first. These are numbered
`V-` so they never collide with XR-PRD's `X-` decisions, which all still apply.

| # | Decision | Consequence |
|---|---|---|
| V-a | The branch is **`feat/VisionOS`**, forked from `feat/MetaXR` @ `0a533bf` | The Quest edition at XR8 is the starting point, so all of XR2–XR8 is inherited rather than rebuilt |
| V-b | It is a **sibling long-lived fork**: it takes `feat/MetaXR` in by merge (which itself takes `main` in), and **never merges back** | The same discipline one level deeper. The visionOS package, loader and build profile never reach the Quest edition, so XR9–XR11 proceed undisturbed |
| V-c | **`Docs/XR-PRD.md` and `Docs/XR-Agent.md` are never edited on this branch** | `feat/MetaXR` keeps editing both as XR9–XR11 land. Editing them here would conflict on every merge. visionOS decisions and progress live in this file |
| V-d | Milestones are **`V1`…`V5`**, commit message `V<n>: <title>` | `XR<n>` commits keep arriving by merge, so a separate series can never be confused with them |
| V-e | Only **`com.unity.xr.visionos` 3.2.2** is added. **No PolySpatial** | Metal mode needs nothing else, and the PolySpatial meta-package would pull in the RealityKit path XR-PRD 12 rules out |
| V-f | XR12 is sliced into **V1 foundation → V2 hands and grab → V3 the room → V4 full playthrough**. XR13 becomes **V5** | XR-PRD 13 lists world-space UI Toolkit and XRI direct grab on Vision Pro as unverified. V1 settles both on device before anything is built on them |
| V-g | Bundle id **`com.GorillaGonzalez.Tsugi.VisionOS`**, product name **`Tsugi Vision`** | Its own App Store product, as Quest got `…Tsugi.XR`. **No universal purchase** with the iPhone app. This closes the question XR-PRD 10.5 deferred to XR12. It is also the identity of `save.json`'s container and of the `tsugi.xr.*` `PlayerPrefs`, so it cannot change after release |
| V-h | Enabling changes inside `TrainSudoku.XR` land as **small seam commits on `feat/MetaXR`** first, then merge in | The same shape as XR-PRD 10.2 rule 3 ("made and committed on `main`, then merged in"). Each must leave the Quest build working. None is a milestone commit |
| V-i | **`TrainSudoku.XR` is shared, not forked.** `TrainSudoku.VisionOS` references it and adds only what differs | X20's "copies are forks" governs phone→XR. Quest→visionOS is the opposite: sharing the 9 671 lines is the whole point of X12. A fork here would double every future fix |

---

## 1. What visionOS gives us, and what it does not

Established 2026-09-21 against the Unity registry and the AR Foundation 6.6 / visionOS 3.2 docs,
before any code was written.

| Capability | visionOS, Metal app mode |
|---|---|
| Session, device tracking, **planes**, **anchors**, ray casts, meshing, image tracking | **Supported.** `XRSessionSubsystem`, `XRPlaneSubsystem`, `XRAnchorSubsystem`, `XRMeshSubsystem`, … So `XRBoardPlacement`'s plane-and-anchor flow has a provider |
| Hand tracking | **Supported** through `XRHandSubsystem`, the same package `XRWristMenu` already reads |
| Look-and-pinch | **`VisionOSSpatialPointerDevice`**, an Input System device. Usable on its own in Metal mode; the RealityKit `SpatialPointerDevice` is only needed when mixing Metal with RealityKit content, which we never do |
| Passthrough | `metalImmersionStyle` = **Mixed**, camera background a solid colour with **alpha 0** — the same clear-colour trick `XR.unity` already uses for Quest passthrough |
| `ARCameraManager` / camera images | **Not supported.** `XR.unity` carries an AR Camera Manager; harmless, since the game never asks for camera images (the Quest build already logs that it asked and did not get them) |
| **Occlusion / environment depth** | **No provider at all.** The AR Foundation occlusion feature table has no visionOS row. `XRDepthOcclusion` cannot run |
| Real hands drawn over the content | Handled by **the OS**, through `VisionOSSettings.upperLimbVisibility`. This is precisely the problem `XRDepthOcclusion` was added to solve at XR5, so Vision Pro needs no depth clip — see V3 |
| Controllers | **None.** Hands only, which X5 and X16 already assume |
| Foveated rendering | Supported with URP, and asked for by XR-PRD 8 |

### 1.1 Consequences for the inherited code

Surveyed 2026-09-21 across all 53 files and 9 671 lines of `Assets/01.Scripts/XR/`.

**`TrainSudoku.XR` never references OpenXR.** Its asmdef lists only AR Foundation, AR Subsystems,
Core Utils, XRI, XR Hands, the Input System and Localization. Every OpenXR and Meta call in the fork
lives in `TrainSudoku.XR.Editor`. At runtime there is exactly **one** Quest-specific file and **one**
`UNITY_ANDROID` block.

| | |
|---|---|
| ✅ | **The grab and touch layer is XRI-only.** `IGrabInput` is the designed seam and names this port in its own comment. `XRPieceHands` imports no XRI or AR type at all |
| ✅ | **The rig's poke and pinch poses already bind a platform-neutral device.** `XRI Default Input Actions` binds `Poke Position` to a `Vector3Fallback` over `<XRHandDevice>{LeftHand}/pokePosition`, `<HandInteraction>…` and `<HandInteractionPoses>…`. **`XRHandDevice` is XR Hands' own synthesised device, not OpenXR's hand-interaction profile.** So the fallback should resolve on visionOS, and if it does, `XRTouchPoints`, the signboard's fingertip presses, the map roundels and the wrist roundel work unchanged. **This is V1's central hypothesis** |
| ✅ | **The occlusion clip compiles out for free.** `XROcclusion.hlsl` is keyword-gated on `XR_HARD_OCCLUSION`/`XR_SOFT_OCCLUSION` with an empty `XRClipOccluded` in the `#else`, and `XROcclusionMaterials.Configure(null, null, null)` returns the source material untouched. Not wiring `ARShaderOcclusion` is the entire change; **no C# edit** |
| ✅ | `XRGame`'s only platform-specific lines are five `#if UNITY_EDITOR` blocks. No binding paths, no `Application.platform`, no Android paths |
| ✅ | `XRLocale` and `XRPlayerPrefsStore` are portable, and `XRPreferences` already sits behind `IPreferenceStore` |
| ✅ | `XRBoardPlacement` does **not** use `ARRaycastManager` — it raycasts physics against the plane prefab's colliders and reads `ARPlane.alignment`. The anchor API it uses (`TryLoadAnchorAsync`, `TrySaveAnchorAsync`, `descriptor.supportsSaveAnchor`) is cross-platform |
| ⚠️ | **`Room/XRScenePermission.cs` is the one blocking problem.** A `static class` with no interface, hard-coded to `com.oculus.permission.USE_SCENE` behind `#if UNITY_ANDROID && !UNITY_EDITOR`, **returning *refused* on every other platform**. `XRBoardPlacement` only ever does `if (XRScenePermission.IsGranted && planes != null) planes.enabled = true`, and `XRDepthOcclusion` waits on the same flag. Left alone, plane detection never starts on Vision Pro and the board floats at waist height forever |
| ⚠️ | `XRWristMenu`'s menu button binds `<XRController>{LeftHand}/{MenuButton}` — dead but harmless, since the roundel's fingertip path is separate. Its documented OpenXR joint-axis assumption ("up is out of the back of the hand") should hold because XR Hands normalises axes across providers, but it wants checking on the headset |
| ⚠️ | `XRFoundationSetup` hard-codes `BuildTargetGroup.Android` and `Assets/Scenes/XR.unity`, so it cannot build a visionOS scene. This is the second reason V1 reuses `XR.unity` rather than authoring one |

---

## 2. Fork rules for `feat/VisionOS`

The shape of `Docs/XR-Agent.md`'s rules, one level deeper. **visionOS adds files; it does not edit
Quest files or phone files.** visionOS-owned paths:

- `Assets/01.Scripts/VisionOS/` (with `Editor/`, and `Rules/` only if engine-free maths appears)
- `Assets/02.Graphics/VisionOS/`, `Assets/03.Data/VisionOS/`, `Assets/99.Test/VisionOS/`
- `Assets/Settings/Build Profiles/visionOS.asset`
- `Assets/Scenes/VisionOS.unity` — **only if a milestone proves one is needed.** V1 reuses `XR.unity`
- `Docs/VisionOS-PRD.md`, `Docs/VisionOS-Agent.md`

The shared project-wide files it may touch, and no others:

| File | What visionOS adds |
|---|---|
| `Packages/manifest.json`, `Packages/packages-lock.json` | the one pin, `com.unity.xr.visionos` |
| `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` | one appended `buildTarget` entry with the visionOS loader, beside the existing Android one. **The one licensed exception to "does not edit Quest files"** — there is only one such asset per project, and the append is analogous to the appended quality level XR-PRD 10.2 already permits. A merge conflict here is a trivial keep-both |
| `Assets/XR/Loaders/VisionOSLoader.asset` | **new file**, created by `XRPackageMetadataStore.AssignLoader`, beside the Quest's `OpenXRLoader.asset`. A new file never conflicts on merge |
| `Assets/XR/Settings/VisionOSSettings.asset` | **new file**, the Metal/Mixed player settings (app mode, immersion style, upper-limb visibility, foveation, the two usage descriptions) |
| `Assets/XR/Settings/OpenXR Package Settings.asset` | the OpenXR package appends a `VisionOS` block of its own, with every feature **disabled**, simply because the visionOS platform now exists. Verified purely additive: no Quest feature was switched off. Reverting it is futile — the package rewrites it on the next Editor open — so it is kept |
| `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset` | whatever config-object lines and define symbols the package appends |
| `.gitignore` | `Builds-VisionOS*/` |
| `CLAUDE.md` | one line naming this branch and its two docs |

**Never touched on this branch:** anything under `Assets/01.Scripts/XR/`, `Assets/02.Graphics/XR/`,
`Assets/Scenes/XR.unity`, `Assets/Settings/Build Profiles/Meta Quest.asset`, `Docs/XR-PRD.md`,
`Docs/XR-Agent.md`, and every phone path.

Both suites stay green, and `SampleScene` still plays in the Editor, at every milestone — the same
rule XR-PRD 10.6 holds the Quest branch to.

---

## 3. Presentation

Per XR-PRD 8, with these visionOS differences:

- **Its own render pipeline asset**, `Assets/02.Graphics/VisionOS/VisionOS_RPAsset.asset` plus
  `VisionOS_Renderer.asset`, copied from the Quest's `XR_RPAsset` rather than editing it. The Quest
  asset is Quest-owned, sits at `m_RenderScale: 0.8`, and has no depth texture.
- **Render scale 1.0** with **foveated rendering** on, instead of the Quest's 0.8.
- **Depth texture on, copy-depth mode `AfterOpaques`** — the only mode Metal app mode supports, and a
  Project Validation requirement of the visionOS package.
- Single-pass instanced rendering is the Metal default and stays on.
- HDR off, post-processing off, Forward, 4× MSAA, as XR-PRD 8 asks.

---

## 4. Milestones

Same rules as the phone and Quest work: **no `git commit` until a milestone's *Verified by human* box
is ticked**; one commit per milestone, message `V<n>: <title>`, on `feat/VisionOS`. Every milestone
also requires the phone's full suite and the XR rules suite to pass, and `SampleScene` to play.

| # | Milestone | Done when |
|---|---|---|
| V1 | visionOS foundation | The package, the loader, the Metal/Mixed settings, the render pipeline asset, the build profile and a build script exist, and a build launches on a real Vision Pro in an immersive passthrough space with hands tracked and the board rendered at 6 cm a cell. **Plus the three findings below written down** |
| V2 | Hands and grab | Scoped by V1's findings. At most: a `VisionOSGrabInput` behind the existing `IGrabInput` seam, look-and-pinch through `VisionOSSpatialPointerDevice`, and a uGUI fallback for the signboard and wrist menu if UI Toolkit did not render. At least: the `IScenePermission` seam (on `feat/MetaXR` first, per V-h) and tuning |
| V3 | The room | Anchor save, load, erase and persistence across launches on ARKit. **`XRDepthOcclusion` dropped on visionOS** (no provider; the OS draws the limbs) while the shadow catcher stays. The handle's move, the two-hand scale, and tray docking |
| V4 | Full playthrough | The rest of XR12: a whole line played on the device, save and continue, stars, the train run |
| V5 | Release readiness | XR13: frame rate against the Vision Pro's refresh, App Store requirements, the App Store Connect record under `com.GorillaGonzalez.Tsugi.VisionOS`, a TestFlight build |

### 4.1 V1's three findings

V1 exists as much to answer these as to build anything. A "no" is a result, not a failure, provided
it is written into `Docs/VisionOS-Agent.md`.

1. **Does the world-space UI Toolkit signboard render and read?** XR-PRD 13's open risk. A "no"
   decides whether the signboard and wrist menu need a uGUI fallback on Vision Pro only, which would
   be the largest single piece of V2.
2. **Do `<XRHandDevice>` poke and pinch poses resolve?** If they do, most of V2 is already working
   and V2 becomes look-and-pinch plus tuning.
3. **Does the board land on a real table?** i.e. whether enabling `ARPlaneManager` from the visionOS
   side was enough, or whether `XRScenePermission` needs the `IScenePermission` seam.

### 4.2 Progress

| # | Milestone | Verified by human |
|---|---|---|
| V1 | visionOS foundation | ☐ |
| V2 | Hands and grab | ☐ |
| V3 | The room | ☐ |
| V4 | Full playthrough | ☐ |
| V5 | Release readiness | ☐ |

---

## 5. Out of scope

Everything in XR-PRD 12, and in addition:

- **RealityKit app mode, the Shared Space, and PolySpatial.** Already XR-PRD 12; restated because it
  is the single biggest architectural fork in the road and the reason UI Toolkit and our URP shaders
  survive at all.
- **Windowed and Hybrid app modes.**
- **Universal purchase with the iPhone app** (V-g).
- **Environment-depth occlusion** (V3: there is no provider, and the OS makes it unnecessary).
- **Sharing progress between the phone, the Quest and the Vision Pro.**

## 6. Risks

- **The editor is an alpha and the visionOS package targets a release.** `com.unity.xr.visionos`
  3.2.2 declares Unity `6000.3.0f1` as its minimum; we are on `6000.7.0a6`, which satisfies it but is
  four minors ahead of anything the package was tested against. Mitigation: V1 is small, early, and
  verified on device before any gameplay depends on it.
- **World-space UI Toolkit on Metal mode is unverified** (XR-PRD 13). Mitigation: it is V1's first
  finding, checked before V2 is scoped.
- **Direct grab on Vision Pro is not documented for XRI** (XR-PRD 13). Mitigation: the grab interface
  (XR-PRD 10.4) already isolates it, and look-and-pinch through `VisionOSSpatialPointerDevice` is the
  documented fallback.
- **Two-level merge drift.** This branch is two forks away from `main`. Mitigation: the fork rules in
  section 2, V-c keeping the Quest docs untouched, and V-h putting seams on the parent branch instead
  of here.
- **Anchor persistence differs on ARKit.** `descriptor.supportsSaveAnchor` may answer differently
  from Quest's, and the board's saved position is the feature a returning player notices first.
  Mitigation: V3 owns it, and `XRBoardPlacement` already asks the descriptor rather than assuming.
- **No controllers means no fallback input.** On Quest, a controller rescued a failed hand track.
  Mitigation: V2 must make look-and-pinch work at 6 cm, and the handle can scale a board to 9 cm.
