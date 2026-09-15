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
| `TrainSudoku.XR.Rules` | `Assets/01.Scripts/XR/Rules/` | `PieceDrop` (XR-PRD 4.4): grabbing from the tray or a cell, the ghost tint, and every release outcome. It works on the shared `Board` through `TryPlace`, `TryErase` and `Legality`. `TrayDock` (4.1: slot layout, docking edge, handedness, the 1.5 s dwell), `ThrowGesture` (hand speed over the last 0.1 s against 1.2 m/s) and `BoardPick` (the cell under a point, the hover band). `MapReveal` (which lines the network map shows, 6.2), `MapFit` (map space onto the platform card) and `MapStroke` (route, loop and stadium polylines); `QuickBoard`, the testing aid that lays all but a station's last few rails as fixed pieces; and, from XR8, `WristMenuPlan` (the wrist menu's entries per state and what its button does), `WatchCheck` (when the wrist button shows) and `XRPreferences` (the headset's settings over an `IPreferenceStore`). `noEngineReferences`; references only Core |
| `TrainSudoku.XR` | `Assets/01.Scripts/XR/` | The XR runtime. References Core, `TrainSudoku.Game` (the four level data types only), XR.Rules, the XR packages (XR Hands among them), the Input System and Localization. Holds: `Wrist/` (the wrist menu, `XRLocale` and `XRPlayerPrefsStore`, XR8); `Board/` (display, materials, assets, occlusion materials, forked mesh code); `Grab/` (the grab interface `IGrabInput` and its XRI implementation, the tray, `XRPieceHands`, flights and steam, and `XRTouchPoints`, the fingertips and pinch points read off the rig); `Train/XRTrainRun`; `Room/` (placement, handle, permission, depth occlusion); `Maps/` (the platform card, its maps and roundels); `Signboard/` (the world-space UI Toolkit signboard, `XRSignageAssets`, and the `XRSignageUi` blocks and `XRPanelTouch` fingertip presses it shares with the wrist menu); `XRPalette`; `XRGame`, the shell that owns the flow (XR7); and `XRStarSample` |
| `TrainSudoku.XR.Editor` | `Assets/01.Scripts/XR/Editor/` | `XRFoundationSetup`, the Window > TrainSudoku > XR menu (Create Render Pipeline, Build XR Scene, Add Game, Add Board Placement, Add Depth Occlusion, Add Tray and Grab, Add Flow); and `XRManifestPermissions` |
| `TrainSudoku.XR.Tests.EditMode` | `Assets/99.Test/XR/EditMode/` | `PieceDropTests`, `TrayDockTests`, `ThrowGestureTests`, `BoardPickTests`, `MapRevealTests`, `MapFitTests`, `MapStrokeTests`, `QuickBoardTests`, `WristMenuPlanTests`, `WatchCheckTests` and `XRPreferencesTests`, with their own fixtures in `XRTestBoards`. It never uses the phone's test assembly, which would pull in Game and Editor |

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
  - **Placement.** `Room/XRBoardPlacement` first tries to restore the saved anchor. The anchor's GUID lives in `PlayerPrefs` under `tsugi.xr.boardAnchor`, never in `save.json`. The cell size the handle last scaled the board to is saved beside it under `tsugi.xr.boardCell`, and clamped to `MinCellSize`–`MaxCellSize` (0.04–0.09 m) on restore.
    - Without one, it asks for spatial data and slides a ghost over detected horizontal planes where the right hand points (its ray pose; no ray is drawn since 2026-09-15), then the left hand, then the gaze. If there is no surface, the ghost floats at waist height.
    - A pinch or trigger places it.
  - **`BoardRoot`** is the board's parent:
    - Scaled 0.06, with its origin at the middle of the near edge, on the surface, and +Z pointing away from the player.
    - `XRBoardDisplay` lifts itself by the slab height and puts its near edge on that origin, so a bigger board grows away from the player.
  - **Handle.** `Room/XRBoardHandle` is an `XRSimpleInteractable` (an `XRGrabOnlyInteractable`, which no poke or gaze can select), not a grab interactable.
    - **It is an L-shaped rail** (since the second XR7 headset check) round the near corner opposite the tray: a leg 2 cells along the near edge, a quarter circle 0.45 cells out from the corner, and a leg 2 cells up the side, 0.25 cells above the surface. The XR6 knob, and the grown bar before it, gave too many false positives.
    - **It opens out when touched** (2026-09-14). Once lit, the near-edge leg runs on to the far corner, round it and 2 cells up the far side in 0.25 s. The result is a U with an end for each hand.
      - 0.8 s after nothing is on, near or hovering it, it folds back in 0.35 s. Hidden, it snaps folded.
      - The path is the whole U cut to a length (`TracePath`), and the grab volume follows it, rebuilt every frame while it moves.
      - On an 8x8 board the far leg's volume clears the tray's inner column by about 0.17 cells (1 cm). The tray side never had a rail before, so watch for a pinch at the tray catching it.
    - It follows the platform on show: `SetFootprint`, called from `XRGame.FitHandle` with `BoardLayout.HalfWidth` for a board and `XRPlatformMap.HalfWidth` for the map card.
    - The rail and its grab volume are tube meshes built along that path. The volume is a `MeshCollider` 0.25 cells fatter than the rail, taken by a close pinch alone (`XRDirectReach`).
    - **One hand only lights and opens it; two move it.** A single pinch lights it, and so does a fingertip or pinch point from `XRTouchPoints` within 2 cm of the grab volume (until it is 5 cm away). XRI's hover never came through for a hand merely resting on it.
    - With both hands on (one at each end), the board follows once they have moved it 3 cm or turned it 12 degrees; a pair that never leaves that dead zone moves and re-anchors nothing. Either hand letting go ends the move.
    - A grab interactable detaches the grabbed object from its parent for the length of the grab, which left the bar in the air while the board moved.
    - Its own code moves `BoardRoot` freely, height included, turning it about the grab point so the rail stays in the hands.
    - The hands' midpoint carries the board, and the line between them steers it. The XR6 one-hand wrist twist went with the knob.
    - **Spreading or closing the hands scales it** (since the XR7 check), about their midpoint. The new cell size is the start size times the current span over the start span, clamped to 4–9 cm. It is eased over about 0.08 s, because jitter in the span is magnified at the far edge of a big board. An 8% change in span counts as leaving the dead zone.
    - `XRBoardPlacement.MoveTo` takes the cell size. The root is unparented while it moves, so its local scale is its size in the room.
    - Everything under `BoardRoot` scales with it, the signboard included: the sign is sized for the standard 6 cm cell, not for whatever cell the board had when the sign was made.
    - On release, `XRBoardPlacement` settles the board onto a detected surface within 5 cm (`SettleReach`), or leaves it floating. It then re-anchors, saves the new anchor and erases the old one.
    - Surfaces stay enabled (unseen) after an anchor restore so this still works.
  - **Anchor order.** A new anchor is attached before the old one is removed, because removing an anchor destroys its GameObject and anything still parented to it.
  - **Shadow catcher.** `02.Graphics/XR/Shaders/XRShadowCatcher.shader` darkens the passthrough table where the main light is blocked. The display builds it 3 cells wider than the board and shows it only when the board sits on a surface.
  - **Occlusion.** `Room/XRDepthOcclusion` switches on AR Foundation's `AROcclusionManager` and `ARShaderOcclusion` once spatial data is granted. They give hard occlusion from environment depth.
    - It also publishes `_XRWorldToTrackables` and `_XROcclusionBias`.
    - Every board, piece, sign, text and train material is made from the `Occluded Lit` or `Occluded Text` template through `Board/XROcclusionMaterials`.
    - The shared clip lives in `Shaders/XROcclusion.hlsl`.
  - **Locomotion** in the XRI rig is switched off, because the world never moves the player (4.5, 8).
- **Tray and grab (XR6).**
  - **The grab interface** is `Grab/IGrabInput`: hands grab and release `GrabTarget`s (a tray key or a cell) and report a `GrabHold` each frame (the hand, and a ray for a distant hold). `XRPieceHands` turns those into `PieceDrop` calls and plays the result; nothing else changes the board in play. A later Meta hand grab or visionOS pointer is a second `IGrabInput`.
  - **The XRI implementation** (`XRIGrabInput`) adds an `XRGrabTargetInteractable` (an `XRSimpleInteractable`) to each target's collider. XRI's near-far interactors then choose what each hand takes, and arbitrate with the board handle. Since 2026-09-15 that is up close only: far casting is off (`XRTouchOnly`, below), so the distant hold in `GrabHold` is unused on Quest. Nothing XRI selects moves.
    - A target is hoverable and selectable only while it has something to give, so an empty cell is never a target. Pokes and gaze never grab.
    - **Trap:** XRI asks `IsSelectableBy` on every frame of a selection and drops the selection when it turns false. So a hand already selecting a target must always answer true (`IsSelected`), or lifting a piece (which empties the cell) would end the grab at once.
    - Both hands may select one tray slot at once (`InteractableSelectMode.Multiple`): supply is unlimited.
  - **Cells.** Each tile's cube collider is kept and stretched from the slab's foot to half a cell above its top: that is what a hand aims at to lift a piece.
  - **Pinch-only targets** (after the XR6 headset check). Board cells and the handle sit on Unity's built-in Ignore Raycast layer (`Grab/XRDirectReach`), and since 2026-09-15 the tray pieces do too.
    - The rig's far casters already leave that layer out: their mask is `0x80000021` (Default, UI and layer 31).
    - The controllers' near casters look at Default only, so `XRDirectReach` adds the layer to every near-far interactor's near caster.
    - Before this, a ray aimed at a far piece met a nearer cell's tall volume first.
  - **Trap:** `ProceduralBoardMesh.ChamferedTile` hands one cached mesh to every caller. Never destroy the display's `TileMesh`. The first XR6 build did, on every level change, and every board after the first drew without its slabs.
  - **The ghost is the whole contract of a release.** A piece lands on the cell its ghost is over. With no ghost (above the 10 cm hover band, or off the grid), letting go is letting go off the platform, and the piece falls into the room.
    - **Falling pieces** (since the XR7 headset check). A piece let go or thrown off the platform gets a `BoxCollider`, at least 0.15 cells thick so the flat track cannot slip through a surface, and a continuous-collision `Rigidbody` moving at the hand's release velocity (`XRPieceFlight.Fall`).
    - It bounces off the room's detected surfaces, whose colliders stay on after placement with only their renderers hidden. After 3 s it puffs, or bursts if it was thrown.
    - Anything under `BoardRoot` (tiles, tray, handle, sign) ends it at once.
    - A direct hold carries the piece 12 mm below the pinch. A distant hold rides it 0.4 cells above the platform where the ray meets it.
    - Its yaw is always the board's.
  - **The tray** (`XRTray`) stands just outside the platform, off the dominant-hand side of the edge the player is at, with its near row level with that edge. That keeps the whole near edge free for the handle.
    - It stands clear of an 8x8 board at the least, so on the edge the board was placed from it does not move between levels (X17).
    - Slots are 1.3 cells apart. It slides to a new edge 1.5 s after the player moves there, never while a piece is held.
  - **Feedback.** The ghost and the steam are on `Shaders/XROccludedFade.shader` (`XROccludedFade.mat` is the template, wired on `XRDepthOcclusion`), so real hands hide them like the board.
    - A fixed piece refuses a grab with the fork's `PieceView` shake, enlarged to 0.1 cells over 300 ms.
    - The handle hides while a piece is held; pieces cannot be grabbed while the handle carries the board.
    - Cues, the whistle and the "that's fixed" note come with XR9 and XR10.
- **Flow on the platform (XR7).** `XRGame` replaced the XR4-XR6 stand-in (`XRBoardDemo`, deleted).
  - **The shell.** It owns the shared `GameFlow`, unchanged, with a `FileSaveStore` on `save.json` in `Application.persistentDataPath` (the phone's format; on the Quest under `/sdcard/Android/data/com.GorillaGonzalez.Tsugi.XR/files/`), the level assets' star times and `AwardMissingStars`. It waits for the placed board, builds the display, the map, the signboard, the tray and the hands under `BoardRoot`, and goes straight to `Network`: XR never shows the Concourse (6.1).
  - **The platform card** (`Maps/XRPlatformMap`) replaces the board in the map states: 9.6 cells square (58 cm), a concrete base the slab height with paper on it, its near edge on the root's origin like the board's. Everything on it is rebuilt on each visit.
    - **Network:** the lines `MapReveal` returns (earned plus the first closed one), fitted by `MapFit` with the phone's y-down map space flipped so the screen's top is the table's far side. The stroke is 44 map units scaled by the fit, never under 0.1 cell. A raised roundel stands at each revealed line's last node: the line code on its colour, or a padlock on grey for the closed one.
    - **Line map:** the route (a stadium for Thornemoor), a 0.55-cell (3.3 cm) roundel per station, and the code and name printed flat beside it, on the side the phone's rule picks, reading from the near edge. Cleared stations are filled in the line colour with their stars printed under the name. A saved attempt shows a CONTINUE tag, and the next station to play gets a halo.
    - **Trap:** printed layers sit 0.3 mm apart, the first 0.3 mm above the paper. A stroke printed flush with the paper's top z-fought down to a few dashes.
  - **Roundels** (`XRMapRoundel`) are pressed by a fingertip only, since 2026-09-15. At XR7 they could also be selected by ray, pinch or grip, and pressed by the controller trigger through `uiPressInput`, all via an `XRSimpleInteractable` on a hit volume. That path went with the rays, and the interactable and hit volume with it. A 0.35 s cooldown keeps a fingertip seen coming down twice to one press. A closed roundel shakes.
    - **A fingertip presses it by touch.** `XRTouchPoints` reads each poke interactor's point, which on a hand is the OpenXR poke pose (a `TrackedPoseDriver` on the rig's hand Poke Interactor). A fingertip seen anywhere higher than 1.5 cm above the face is ready. Reaching 6 mm above a roundel, within 1.4 times its width, presses it, once, until it rises again; one that comes down off the roundels must lift before it can press.
    - The first rule armed a fingertip only inside the roundel's own 2 cm column, which missed fingers coming in at an angle: the second XR7 check found touch unreliable. The hand poke interactors are never switched off: the rig's `PokeGestureDetector` only toggles the near-far interactors' far casting.
    - XRI's own poke (an `XRPokeFilter` with `PokeAxis.NegativeY`) never pressed a roundel on the headset, and was removed at the XR7 check.
    - **Trap, if an interactable ever returns to a roundel:** the XR7 map released each roundel (`CancelInteractableHover` and `CancelInteractableSelection`) before it destroyed it. Otherwise XRI ends the hover a frame late, on a destroyed collider, and throws a `NullReferenceException` in `XRUIToolkitHandler.HasUIDocument`. That was logged on roundel presses that changed the view.
  - **The signboard** (`Signboard/XRSignboard`) is a world-space `UIDocument` 1260 x 720 px at 100 px to a unit, scaled to 42 x 24 cm (the generated collider measures exactly that). It stands on two posts 1.1 cells above the surface, 0.5 cells behind the far edge of whatever is on show, and turns about the vertical to face the head.
    - **It leans to the eyes** (since the XR7 headset check). The panel tilts back about its bottom edge by the head's elevation over the card's centre, clamped to 0–50 degrees; the posts stay upright. An upright card took rays at a glancing angle, and its bottom row was hard to hit.
    - **Ways back sat at the top right** at XR7: LINE MAP in play, and NETWORK on the line map, moved up at the XR7 check because a ray reaching for the bottom of the card skims low over the map and can catch a roundel first. XR8 moved both to the wrist menu.
    - **Buttons answer a fingertip too** (second XR7 check). The sign keeps each view's buttons with their actions and maps every fingertip into card space: the pivot is the bottom centre, 100 UI pixels to a unit, and the player's side is -z. A fingertip that has been 2 cm or more in front of the card presses the button under it on coming to 6 mm of the face; within 4 cm the button under it swells 6%.
    - The panel's `XRPokeFilter` was removed: XRI's poke never pressed a button.
    - **Views:** the masthead with the tsugi mark (network); the line badge, name and "n / 9 CLEARED" (line map); the station, clock and star targets, dimming as `StarTier` drops (play); stars, this run, best, NEW BEST and the verdict stamp, with NEXT STATION or TO THE NETWORK, RUN AGAIN and LINE MAP (arrival). The stamp (ON TIME, SLIGHT DELAY, DELAYED, in green, amber and red) is pressed on with the phone's timing: down from 2.4 times its size and 22 degrees off square, then a rattle to rest 4 degrees off. It is hidden for the train run.
    - **Copy** is English literals until XR10's `XR` String Table.
    - **Rebuilt on every show:** switching a `UIDocument` off throws away whatever code built into its root.
    - **XRI's UI Toolkit support needs two things in the scene.** Add Flow puts both on the `UI Input` object: a `PanelInputConfiguration` with redirection `Never` (no EventSystem), and an `XRUIToolkitManager`.
    - **The panel settings** (`03.Data/XR/Ui/XRSignboardPanel.asset`, on XR's own copy of the default theme) have `m_ColliderUpdateMode` 0, MatchBoundingBox, which gives a poke some depth. `ColliderUpdateMode` is internal to UI Toolkit, so it is written as the raw value.
    - At XR7 the panel carried the `XRSimpleInteractable` that XRI prescribes, and a ray pinch clicked a button (checked on the Quest 3 on 2026-09-14). Since 2026-09-15 no panel has an interactable, and XRI's UI interaction is off on every interactor (`XRTouchOnly`), so only fingertips press. The `UI Input` object's configuration and toolkit manager are still in the scene, unused.
  - **The ways back** were on the sign until XR8, which moved them to the wrist menu (below).
  - **Play.** The clock starts on the first grab (`Flow.BoardTouched` on a `Taken` or `Lifted`); a saved attempt is put back with `LevelProgress.ApplyTo` and waits idle at its time. Every board change is written through; the winning one records the star sample and completes the level.
  - **Train run.** It always runs into the exit tunnel before the arrival shows. The first XR7 build skipped it on a pinch, grip or trigger anywhere; on the headset a stray pinch after the winning drop cut it short, so the skip went, and `XRPinch` with it.
    - **A hand on the train** (2026-09-14). A fingertip or pinch point within 0.2 cells of a car's box counts as touching it. The box is measured from the car's renderers, without the destination plate, and a car inside a tunnel cannot be touched.
      - **Shake** (made bigger after the first headset check). The touched car shakes at full turbulence, and 70% of that passes down the couplings for each car either way. The shake is Perlin sway of 0.14 cells, a hop of 0.09 cells, 7° of pitch and 14° of roll at 11 Hz, fading with a 0.5 s time constant. A hurrying train keeps rattling at up to 40% of full turbulence until it is back to cruising.
      - **Push.** The train speeds up: 0.5 times cruising speed is added at first contact, then 1.5 times cruising per second while the hand stays, capped at 3 times cruising. It eases back with a 1.5 s time constant.
      - **Squash and stretch, cartoon fashion.**
        - Each car is a pivot on the rails carrying its model, so the stretch runs along the track whatever the model's yaw offset.
        - Stretch: a car grows 0.15 longer per unit of push, up to 1.3 times at top speed, and gets thinner as it lengthens so it keeps its volume.
        - The wave: each car follows the one ahead on a loose spring (stiffness 220, damping 9), and the locomotive follows the push. The stretch runs from front to back and wobbles as it settles.
        - Couplings: the gaps grow with the cars, so the train draws out rather than the cars overlapping. The run ends on the actual tail position.
        - A poked car squashes first and then springs out.
        - Cars rear nose-up as they stretch, 5° per unit a second up to 15°, and lift by whatever the tilt would sink into the rails.
        - The destination plate hangs off the `Cars` group rather than a pivot, so the stretch never shears its text.
      - **Steam.** The train has its own `XRSteam`. It puffs where the hand touched, and chuffs from the locomotive's roof once the push passes 0.2: every 0.35 s at top speed, more slowly below it.
      - It is still no skip: the run always ends in the exit tunnel.
  - **Star sample (XR-PRD 9).** `XRStarSample` still appends every solve by hand (grabs > 0) to `xr-star-sample.csv` beside the save. Quick-test solves are left out. Each row records the cell size it was played at (`cellCm`); a file from before scaling gains the column, with 6.0 on its old rows, the first time a row is added.
  - **Quick test.** `XRGame.quickTestRails` (3 in `XR.unity`) plays every station with all but its last that-many rails along the route already laid, as fixed pieces (`QuickBoard`, on the level's in-memory copy, so no level asset changes).
    - Quick solves count for progress on that device.
    - The solve runs when the station loads, so a big board may hitch.
    - **Set it to 0 before XR11.**
  - **Editor testing.** With no headset, `floatWithoutHeadset` floats the board in front of the camera; `XRBoardPlacement` still runs, and its yellow ghost tints the view. `unlockAllInEditor` opens everything, and the component's context menu has **Debug: Solve This Station**. The Editor's save is the phone's Editor save (`%USERPROFILE%/AppData/LocalLow/GorillaGonzalez/Tsugi/save.json`): back it up before playing `XR.unity` and put it back afterwards.
- **Wrist menu and pause (XR8).**
  - **Pause** (6.3, X18). `XRGame` pauses play on the wrist menu, on the left controller's menu button, on `OnApplicationFocus(false)` (the headset taken off, the system menu) and on `OnApplicationPause(true)` (suspended). Only play pauses; elsewhere a focus loss just saves. A return from a focus loss stays paused.
    - The flow stops the clock. `XRBoardDisplay.SetDimmed` darkens every lit material under the board, the tray's included, to 40% through a `MaterialPropertyBlock` on `_BaseColor`; clearing the block puts it back exactly. Clue numerals are text and stay bright.
    - Grabs were already off outside Play. The handle is made unavailable while paused.
    - The signboard's paused card shows the station, PAUSED and the stopped clock, and a RESUME button for a return from a focus loss, when the menu is closed.
  - **The wrist menu** is `Wrist/XRWristMenu`, created by `XRGame` at runtime, so there is no scene step. It shows and asks; `XRGame` decides through `WristMenuPlan`.
    - **The roundel** is a 3 cm world-space UI Toolkit document carrying the tsugi mark. It sits 3 cm out of the back of the non-dominant wrist and 1.5 cm towards the elbow, facing out of the wrist, upright to the viewer.
    - The wrist pose comes from the running `XRHandSubsystem`, transformed by the rig's camera offset (joint poses are in session space, where the head's pose driver also writes).
    - **Check on the headset:** it assumes OpenXR's joint axes (up out of the back of the hand, forward towards the fingertips), which the XR Hands docs do not state. If the roundel shows over the palm, flip `back` in `UpdateRoundel`.
    - It shows only while `WatchCheck` passes: the back of the wrist within 45 degrees of the eyes, hiding again past 65. Hand tracking only; with controllers the menu button opens the panel.
    - It is pressed by the other hand's fingertip (`XRPanelTouch`, 8 mm of slop, since it rides a moving wrist) and nothing else. The ray-and-pinch path went on 2026-09-15.
    - Turning the wrist must never press it against the other hand: a fingertip is ready only over the roundel, within 1.5 cm of its edge, and a roundel just turned into view takes no press for 0.3 s.
    - A 1 s cooldown makes one touch one press. At 0.35 s the first XR8 check saw double presses: a fingertip on a moving wrist drew back past the arming gap and came down again.
    - **The panel** is a 20 cm wide document opened 6 cm above the roundel, else above the left controller, else 45 cm ahead of the eyes. It turns to face the eyes and is then world-locked.
    - It closes on every state change (a pause opened from the wrist reopens it once the pause is in), on the button pressed again, or once the head is 1.2 m away.
    - **Toggle rules:** opening in play pauses. Closing never resumes; only a RESUME button does. The train run and the arrival hide it.
    - **The first XR8 headset check (2026-09-14)** found the game resuming wherever the player pressed, and sometimes with no press at all. That build logged no presses, so the cause is unconfirmed. Two changes followed:
      - Closing the menu used to resume; now it doesn't.
      - The wrist hand's own fingertips are ignored by the roundel and the panel (`XRPanelTouch.IgnoredHand`, fed from `TouchPoint.Handedness`).
    - **The second XR8 headset check (2026-09-15)** logged every press. What it found, and what changed:
      - **Every panel press went to the first button listed on its page**: SETTINGS on the network page, LEFT on the settings page, RESUME in the pause, LINE MAP on the sign's arrival card. `XRPanelTouch` compared the fingertip with `worldBound` as it came but measured its slop in UI pixels. The slop evidently covered the whole card, and the roundel answered a fingertip anywhere in its plane, which is why turning the wrist paused the game.
      - It now brings each button's `worldBound` into the root's own space (`root.WorldToLocal`), puts the fingertip there in UI pixels, and of the buttons within reach takes the nearest. The Editor bridge was stuck in Play mode at the time, so the proof was the third XR8 headset check (2026-09-15), which passed.
      - A fingertip is ready only over a panel (2 cm margin) and within 15 cm in front of it. A panel takes one press per 0.35 s.
      - **Choosing LEFT moved the roundel to the right wrist while the panel was open**, so the right fingertip that chose it was ignored from then on. The open panel now keeps ignoring the hand it opened over; the setting moves only the roundel.
      - **The roundel sometimes showed for a frame or two at a giant scale** behind the wrist as it came into view. Both documents now stay switched on and are hidden through their root's `style.visibility`, and the roundel is built once.
      - **A pinch on the handle's rail lifted the corner cell's piece** (`Refused (FixedPiece) at (5, 5)`). `XRIGrabInput.Reserved` now keeps board cells from a hand whose pinch point `XRBoardHandle.Claims`: inside the rail's grab volume, or within 0.2 cell of it. The tray is unaffected.
    - **Every press is logged:**
      - `[XR touch] <panel>: <button> by the <hand> fingertip`, where the panel is sign, wrist menu or wrist roundel
      - `[XR wrist] Pressed by …` (the roundel or the menu button, with its control path)
      - `[XR touch only] …`, once at start: how many interactors lost their rays
      - every flow change, as `[XR flow] A -> B`
    - **Entries:** the network has Settings and Close; the line map Network, Settings and Close; play and pause Resume, Retry, Line map and Settings.
    - **Settings:** hand (left or right), board height (lower or raise 1 cm), re-place board, language (cycles the project's locales), music and effects volume (0 to 10). The page is rebuilt on every press.
    - **The height nudge** is `XRBoardPlacement.Nudge`. It lifts the board off its anchor, stops at a detected surface below, and re-anchors 0.6 s after the last press. The handle takes over if it is grabbed meanwhile.
    - Re-place now also remembers that surfaces are allowed when the board had been restored from its anchor, so the ghost can find a table again.
    - **The menu button** is an `InputAction` on `<XRController>{LeftHand}/{MenuButton}`. In the Editor, Escape does the same.
  - **Settings storage.** `XRPreferences` (Rules) sits over `XRPlayerPrefsStore`, under `tsugi.xr.dominantHand`, `tsugi.xr.musicVolume`, `tsugi.xr.effectsVolume` and `tsugi.xr.locale`, beside the anchor and never in `save.json` (X1). It is read once and written through, because PlayerPrefs on Android is not free per frame. `XRGame.dominantHand` is only the default now.
    - **The volumes are stored, but nothing plays yet.** XR10 brings the audio and reads them.
    - **The language.** The shared Localization settings take the system's language and remember no choice, and changing them is a shared change (X25). So `XRLocale` applies XR's own stored choice once the board is placed. Until XR10's XR String Table only the settings row shows it.
  - **No rays** (2026-09-15, after the first XR8 headset check; X16 revised). The player said the ray selector interfered with pausing and with grabbing the handle, and asked for touch everywhere. `Grab/XRTouchOnly.Enforce()` runs at the top of `XRGame.Update` every frame, from launch:
    - It holds `enableFarCasting` and `enableUIInteraction` off on every near-far interactor, and `enableUIInteraction` off on every poke.
    - It keeps the ray line visuals and the controllers' teleport rays switched off.
    - It switches off the rig's `PokeGestureDetector`s (whose events turned far casting back on whenever a hand stopped pointing) and `ControllerInputActionManager`s (teleport mode). Those are sample scripts, so they are matched by type name.
    - Pieces (tray included) and the handle take a close pinch or grip. Every panel and roundel takes a fingertip, or a controller's tip.
    - The hand's ray pose still aims the placement ghost; nothing is drawn.
  - **Shared blocks.** `Signboard/XRSignageUi` (card, text, buttons, LED strip, badge, star tally) and `Signboard/XRPanelTouch` (fingertip presses; the document's pivot must be its bottom centre) came out of `XRSignboard`, which uses them unchanged.
  - **Editor testing.** With no headset, a lost focus only saves, since clicking another window would otherwise pause. `XRGame`'s context menu adds **Debug: Lose Focus** and **Debug: Press Wrist Menu**, and the panel opens ahead of the camera.
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
  - **The bridge runs a command again after the domain reload that entering Play causes** (2026-09-14). The second run finds Play already on. Make such commands idempotent (check `isPlaying` first), and read a warning from the second run as that, not as someone else pressing Play.
  - **Never edit a script while the Editor is in Play mode.** The compile waits for Play to end (`ScriptCompilationDuringPlay` 1), but `isCompiling` reads true meanwhile, so the bridge refuses every call, `ExitPlaymode` included, until someone presses Play in the Editor (2026-09-13).
  - `Unity_Camera_Capture` with no camera captures the Scene view, and that works in Play mode: aim it first with `SceneView.lastActiveSceneView.LookAtDirect`.
  - **An unfocused Editor never repaints the Scene view** (2026-09-14), so that capture comes back stale, sky only, whatever the aim. `ScreenCapture.CaptureScreenshot` writes nothing either, because the Game view does not render unfocused.
    - What works is a temporary `Camera` rendered into a `RenderTexture` and written out as a PNG from the bridge.
    - That render leaves out world-space UI Toolkit panels (the signboard, the wrist menu), so check those by their document contents instead.
  - Write `UnityEditor.Tools` in full in a bridge script, like `CompilationPipeline`: the injected namespace has its own `Tools`.
  - **In the Editor without a headset, the placement ghost lies exactly over the floating board** and tints it yellow. Hide `Placement Ghost` before judging colours: a dimmed board read as tan until it was hidden.

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
   - **Read the game's log by streaming it live, with plain `adb`**: `...AndroidPlayer\SDK\platform-tools\adb.exe logcat -v time -s Unity:V`, in a background loop that restarts when the cable drops.
     - The headset's main log buffer is 256 KiB, and the OS's anchor discovery (`SP:AF:AnchorFramework`, every ~70 ms while planes are on) fills it in seconds. A later `logcat -d` finds no `Unity` lines at all.
     - `metavr adb shell logcat` held its output back, so a stream through it caught nothing (2026-09-13).
   - In Git Bash, set `MSYS_NO_PATHCONV=1` before any `adb shell` command that names `/sdcard/...`, or the path is rewritten to `C:/Program Files/Git/sdcard/...`.
   - The headset leaves adb whenever it sleeps (taken off and set down), so a waiting loop that installs and launches once `adb devices` lists it again saves a round trip.
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
- `xrEndFrame failed with error code XR_ERROR_LAYER_LIMIT_EXCEEDED`, once, as a session starts (seen on every launch on 2026-09-14), followed by an OpenXR diagnostic report headed "Uncaught Exception". The game runs on. Not yet explained.

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
- It can leave four `preloadedAssets` on the Meta Quest profile's Player settings: the Localization settings, `XRGeneralSettingsPerBuildTarget`, the input actions and the OpenXR package settings. The packages inject them for the build and add them again at every build; the committed profile keeps `preloadedAssets: []`.
- Building from script with the Meta Quest profile inactive: the 2026-09-13 XR6 build made it the active profile first (`BuildProfile.SetActiveBuildProfile`), so the OpenXR validator read the Quest's values. Built in 6 min 38 s after the first IL2CPP build.
- The 2026-09-14 XR7 build, or making the profile active before it, also:
  - switched on the **Meta Quest Touch Pro Controller Profile** (`m_enabled` 0 to 1) in `Assets/XR/Settings/OpenXR Package Settings.asset`. **Decided at XR7: it stays on**, committed with the milestone. Touch Pro controllers pair with a Quest 3, and every Quest build re-enabled it anyway.
  - wrote an untracked `ProjectSettings/BuildProfileUtilityOpenXR.asset`, and `Assets/03.Data/AddressableAssetsData/link.xml` again.
  - built in 9 min 39 s; the APK is about 73 MB.
- **A queued build dies with the Editor.** A `delayCall` does not survive an Editor restart, so a build queued before one never starts. Write a "started" marker at the top of the queued method so a waiting loop can tell *not started* from *still building*.
- **A queued build can hang on a pending recompile** (2026-09-14). Making the profile active changes the define symbols, and an unfocused Editor only recompiles once it gets focus, which is also when the queued build runs.
  - The Editor then logged "Tundra requires additional run", never ran it, and sat on "Compiling Scripts" for 45 minutes with no compiler process alive. Only ending the Editor got it back.
  - Have the queued method re-queue itself (another `delayCall`) while `EditorApplication.isCompiling` or `isUpdating` is true. The next build, guarded that way, went through in 7 min 57 s.
- **Restoring the profile's `preloadedAssets` with `git restore` after a build leaves no profile active.** Make Meta Quest active again before the next build, and let any recompile finish before queueing it.
- **With no profile active, the Editor can switch Quest features off** (2026-09-14, the XR8 session). `Assets/XR/Settings/OpenXR Package Settings.asset` lost four Android features: Meta Quest Support and the Oculus Touch, Touch Plus and Touch Pro profiles.
  - A Quest build from that state would lack Meta Quest Support. Check the file before every commit and `git restore` it. A refresh then reads all four as on again.
- `%LOCALAPPDATA%\Unity\Editor\Editor.log` is shared by every Editor on the machine, so it can hold another project's log. Ask the bridge for `Application.dataPath` instead.

## Local files that stay out of commits

These are left over from before the fork, or generated:
- The CRLF-only diffs in 5 settings files.
- `Assets/Settings/Build Profiles/Android™.asset`.
- `Assets/03.Data/AddressableAssetsData/Android/` and `link.xml`.
- `user.keystore` (ignored by `*.keystore`).

Stage files by name; never `git add -A`.
