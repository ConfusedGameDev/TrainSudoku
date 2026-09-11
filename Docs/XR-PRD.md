# Tsugi XR — Product Requirements Document

(The mixed-reality edition of **Tsugi**. Like the phone game, the code keeps the working name TrainSudoku.)

Status: agreed 2026-09-11 by interview. Meta Quest first, Apple Vision Pro second.

This document **extends** `Docs/PRD.md`. It does not repeat what is unchanged:

- The rules (PRD 3).
- The level data and text format (PRD 9).
- The level editor (PRD 10).
- Best times, stars and auto-save (PRD 6).

It covers only what the XR edition changes or adds. Where the two disagree, this document wins for the XR build and `Docs/PRD.md` wins for the phone build. `Docs/UIDesign.MD` still governs the station-signage look (colour and font palette, style sheets, String Table); section 8 here says which parts of it the XR edition reuses.

---

## 0. Decisions taken

Settled by interview on 11 Sep 2026. Closed. Do not re-open one without asking first.

| # | Decision | Consequence |
|---|---|---|
| X1 | Phone and XR are **two live products** sharing rules, levels, localisation and progression data | A line authored once ships to both. Nothing XR-specific may enter `Core` level data or the save format |
| X2 | **One Unity project**, staying on the **6.7** line | XR is added to this project: no second project, no shared package. Meta's own SDKs (Interaction SDK, MRUK, Meta XR Core) do not compile on 6.7 yet and are not used (X12) |
| X3 | Stay on **`6000.7.0a6`**, which is the newest 6.7 build as of 11 Sep 2026 (there is no 6.7 beta yet; the latest beta is 6.6). Move to each later 6.7 build as it lands | Every editor change goes through the upgrade checklist in 10.6 before XR work continues on it |
| X4 | Quest target is **Quest 3 and 3S** | Colour passthrough and a depth sensor are assumed. Quest Pro only if it comes for free; Quest 2 is not supported |
| X5 | **Hands first; controllers also supported** | Everything is designed around a pinch-grab. Controller grip maps to grab, trigger to UI select |
| X6 | **Fixed cell size, about 6 cm** | A 6×6 board with its tunnel ring is ~48 cm across; an 8×8 one ~60 cm. Two-hand scaling of the board is later, not v1 |
| X7 | The board **snaps to detected horizontal surfaces**; if none are found it **floats** at waist height | Moved at any time, mid-level included, by a handle. Its position **persists across sessions** through a saved anchor |
| X8 | The tray holds **six slots, one per key, orientation locked, unlimited supply** | A held piece stays aligned to the board's grid whatever the wrist does. Keys map 1:1 onto `Legality` |
| X9 | Drop rules: see section 4 | Ghost preview. An illegal release returns the piece to where it came from. Dropping on a player piece replaces it if legal. Clues update on release, not while hovering |
| X10 | Lifting a placed piece erases it; dropping it on another legal cell is a **move**. **Throw vs. release is purely visual** | Fixed pieces refuse the grab with a wobble and the "that's fixed" note. Removal is always allowed, as on the phone |
| X11 | The tray is **attached to the board** at the edge nearest the player, on the dominant-hand side | It moves to whichever edge the player walks to and never follows the head |
| X12 | Tech: Unity's cross-platform stack — **OpenXR + OpenXR Meta + XR Interaction Toolkit + AR Foundation + XR Hands** — behind **our own grab interface** | Quest can later swap in Meta's hand grab without the rules or board code knowing. Vision Pro is a new platform, not a rewrite |
| X13 | Vision Pro uses **Metal mode, Mixed immersion, Full Space** | Same rendering pipeline, post-processing and (expected) world-space UI Toolkit as Quest. The app does not share space with other apps. The studio holds a Pro licence |
| X14 | Level select, pause and arrival: see section 6 | The **network and line maps are drawn on the platform itself**. A standing **signboard** at the far edge carries the masthead, briefing and arrival. A **wrist menu** carries pause and settings. The Concourse screen is dropped in XR |
| X15 | v1 carries **every phone system**; the **tutorial is rewritten** for XR | Out of scope for v1: multiplayer/co-location, hints, a VR level editor |
| X16 | Pieces can be grabbed **directly and at a distance** (hand ray on Quest, look-and-pinch on Vision Pro) | A piece held at a distance rides the board surface under the ray. The ghost and snap work the same either way |
| X17 | The board **grows from its near edge**; clue numbers are **standing signs that turn to face the player** | The tray and handle never jump between levels, and the board can be walked all the way round |
| X18 | Pause **stops the clock, dims the board and locks the pieces** | Also triggered by taking the headset off and by the system menu |
| X19 | Throw and puff are **steam**: a burst and a whistle for a throw, a small puff for a release | One particle effect in two sizes. The room's shape is never used |
| X20 | The signboard is **new layouts built from the phone's parts** | Reuses the palette, style sheets, String Table and elements (roundel, LED strip, icons), not the 1080×1920 screens |
| X21 | **Split `TrainSudoku.Game`** into a shared `Presentation` assembly, a `Phone` assembly and an `XR` assembly | The phone build carries no XR code; the XR build carries no touch screens. The split is its own milestone (XR1) with no XR packages |
| X22 | The drop rules live in **Core** as `PieceDrop`, next to `PlacementSession`, unit-tested | The XR input layer only reports grabs, hovers and releases |
| X23 | Test hardware on hand: **Quest 3, Quest 3S, Apple Vision Pro** | Every milestone, Vision Pro included, is verified on a real headset |

---

## 1. Vision

The same puzzle, set on the player's own table. The player puts a small model railway platform down on a real surface, picks a station from a map printed on it, and lays track with their hands. They pinch pieces from a tray at the board's edge and set them on the grid. Pieces they no longer want are lifted off and tossed away in a puff of steam. A finished route sends a miniature train out of one tunnel and into the other.

## 2. Platforms

| Priority | Platform | Notes |
|---|---|---|
| 1 | Meta Quest 3, Quest 3S | Android, OpenXR, colour passthrough. Hands and Touch controllers. Horizon Store. |
| 2 | Apple Vision Pro | visionOS, Metal mode, Mixed immersion, Full Space. Hands only (look-and-pinch and direct pinch). App Store. |
| — | Phone (iOS, Android) | Unchanged, from the same project (X2). Must keep building and passing its tests at every XR milestone. |

**Vision Pro comes second, but nothing built for Quest may block it.** In practice:

- Interaction goes only through the grab interface (section 10.3).
- No Meta-only API is used.
- Every UI is world-space UI Toolkit.
- Distant grab works with look-and-pinch.
- The wrist menu avoids the palm (section 6.4).

## 3. Game rules

Unchanged: PRD 3, including placement legality, the adjacency rule and the win condition. The XR edition adds no rule. It changes how a piece reaches a cell (section 4), not what may go there.

---

## 4. Player interaction

### 4.1 The tray

- Six slots, one per key: `NS`, `EW`, `NW`, `NE`, `SW`, `SE`. Each slot shows the real bent track mesh on a small concrete tile, at board scale.
- **Supply is unlimited.** Taking a piece leaves another in the slot straight away.
- **Orientation is locked to the board's grid.** A slot's piece, and a piece being held, always shows its key relative to the board's north, whatever the hand does. The piece may follow the hand's position and tilt slightly for feel, but its yaw snaps to the grid.
- **Where it sits.** A 2×3 block docked at the near edge of the board, on the dominant-hand side (a setting, default right). It moves to the edge nearest the player once they have stood closer to a different edge for about 1.5 s, and never while a piece is held. It moves with the board.
- **Keys stay relative to the board.** When the player walks to the east edge, `NS` reads as running left to right. That is correct: keys belong to the board, not to the viewer.

### 4.2 Grabbing

- **Direct**: pinch (or controller grip) on a tray piece or a placed player piece.
- **Distant**: hand ray on Quest, controller ray, or look-and-pinch on Vision Pro, aimed at a tray piece or placed piece. A piece held at a distance rides the platform surface under the ray.
- Each hand holds at most one piece, and both hands may hold at once.
- **Fixed pieces (`K`) refuse the grab.** They wobble, play the "that's fixed" note (the phone's `EraseRefused` feedback), and stay put.
- **Lifting a placed piece erases it at once.** The cell empties, the validator runs, and clues update. The piece is now held like any tray piece and remembers the cell it came from.
- **The clock starts on the first grab** of any piece, which replaces the phone's "first tap" (PRD 6). A snapshot restored by auto-save waits for the first grab the same way.

### 4.3 Hover and ghost

- While a held piece is inside a hover band above the platform (tunable; starts at 10 cm), a **ghost** of it snaps to the cell under the piece's centre.
- The ghost is tinted **green** if releasing there is a legal drop (section 4.4) and **red** if not. That tint is the only feedback while hovering.
- **Clues do not update while hovering.** A live "row 3 would go red" preview would give too much away on a sudoku-style puzzle.
- Outside the hover band, no ghost is shown.

### 4.4 Releasing

One table covers every outcome. "Origin" means the tray for a tray piece, or the cell it was lifted from for a board piece.

| Released | Over | Result |
|---|---|---|
| Any held piece | An empty cell where its key is legal | **Placed.** Scale-in animation, validator runs, clues update, place cue |
| Any held piece | An empty cell where its key is illegal | **Flies back to its origin.** Error cue. A board piece returning to its cell is re-placed if still legal there, otherwise it puffs |
| Any held piece | A player piece, and its key is legal once that piece is gone | **Replaced.** The old piece puffs, the new one is placed |
| Any held piece | A player piece, and its key is still illegal once that piece is gone | Flies back to its origin. The old piece stays |
| Any held piece | A fixed piece | Flies back to its origin |
| Any held piece | Off the platform, hand speed below the throw threshold | **Small steam puff**, piece gone |
| Any held piece | Anywhere, hand speed above the throw threshold (tunable; starts at 1.2 m/s) | **Thrown.** It arcs along the throw, then bursts into steam with a whistle. Same effect as a puff |
| A board piece | Another empty cell where its key is legal | **Moved.** A remove-then-place, already half done by the lift |

- **An illegal drop never costs the player anything.** A mistake is never confused with "delete".
- **Throwing and releasing mean the same thing** (X10); only the effect differs.
- The throw arc uses gravity and ignores the room: pieces never land on real surfaces (X19).
- The phone's tap flow does not exist in XR: no neighbour marks, no auto-place, no long-press erase.

### 4.5 Controllers

| Input | Action |
|---|---|
| Grip | Grab and release a piece; grab the board handle |
| Trigger | Select UI (signboard, map roundels, wrist menu) by ray |
| Menu button | Opens the wrist menu |
| Thumbstick | Unused in v1 |

---

## 5. The board in the room

### 5.1 Scale

- The board is the phone's board, built by the same code at the same **1 unit per cell** (`BoardLayout`). The whole board sits under one root scaled to **0.06**, so one cell is 6 cm.
- No board-building code learns about metres.
- Tunnels, clue signs, the train and every other board mesh inherit the scale.

### 5.2 First placement

1. The app opens in passthrough. The first time, it asks for the headset's scene/spatial permission. If refused, placement falls back to floating.
2. A floating sign says "look at a table" (String Table key).
3. A ghost platform slides over detected horizontal surfaces, following the hand ray, controller ray or gaze. If no surface is detected (no room scan, permission refused), it floats at waist height in front of the player, and the sign suggests running the headset's space setup.
4. A pinch (or trigger) places it. The platform faces the player: its near edge is the one nearest them.
5. The placement is saved as an anchor. On later launches the board reappears where it was left. If the anchor cannot be found (a different room, anchor lost), step 2 runs again.

### 5.3 Moving and turning

- A **handle bar** at the board's near edge moves it. Grab and drag to move it along the surface it snapped to, or through the air if it was floating; twist the hand to turn it about the vertical.
- The handle is **hidden while any piece is held**, so it can't be caught by accident mid-drop.
- Moving is allowed at any time, mid-level included. The anchor is re-saved on release.
- **Re-place board** in settings runs section 5.2 again.
- The **height nudge** in settings raises or lowers a floating board.

### 5.4 Board size and growth

- The platform changes size between levels (6×6, 7×7, 8×8 in the shipped set). It **grows and shrinks from its near edge** (X17): the near edge, the tray and the handle stay where they are, and the board extends away from the player.
- In the Network and line-map states (section 6) the platform shows the map at a fixed footprint of about 60 cm, the size of an 8×8 board.

### 5.5 Seen from any side

- **Clue signs.** Clue numbers are small standing signs that turn about the vertical to face the player's head, so they read from any edge. They keep the phone's colour states: satisfied green (`Success`), overfilled red (`ClueExceeded`).
- **Tunnel letters.** The S and E letters on the tunnels turn to face the player the same way.
- **Environment art.** The Japanese platform decals (D13) are art and do not turn.

### 5.6 Sitting on the table

- An invisible shadow-catching surface under the platform lets the board, pieces and train cast shadows onto the real table.
- There is one directional light, chosen for a readable shadow, not estimated from the room.

---

## 6. Screens and flow

### 6.1 States

- **Same states and rules.** `GameFlow` is unchanged: it still owns every transition and still throws on an invalid one.
- **Concourse skipped.** XR starts the flow and moves straight to `Network`. The Concourse (`MainMenu`) is never shown, and XR never calls a transition into it.
- **Board placement is not a `GameFlow` state.** It is a precondition held by the XR shell, like the safe area on the phone. The flow starts once the board is placed, and moving the board later never touches the flow.

```
[place board] -> Network -> Line map (LevelSelect) -> Play <-> Pause
                                                       |
                                                       v
                                              Train run -> Arrival (Win) -> Next / Retry / Map
```

### 6.2 Where each state shows

| State | Platform surface | Signboard (far edge) | Wrist menu |
|---|---|---|---|
| Network | The network map, printed flat, lines as coloured track, raised line roundels. Revealed by the same progress rules as the phone overworld | Masthead: `TSUGI` / `NEXT STATION` with the app mark | Settings |
| Line map (`LevelSelect`) | The line's station map on the 45° grid from `LineDefinition.mapNodes`. Raised station roundels at least 3 cm across; closed stations look closed, and in-progress stations say "Continue" | Line name and code, LED strip in the line colour | Settings, Back to network |
| Play | The board, clue signs, tunnels and tray | Station name, clock, stars to beat | Resume/Retry/Map/Settings (opens as Pause) |
| Pause | The board, dimmed; pieces cannot be grabbed | Shows "Paused" | Resume, Retry, Back to map, Settings |
| Train run | The train runs the finished route in miniature. A pinch or trigger anywhere not on UI skips it | — | — |
| Arrival (`Win`) | The solved board stays | Time, best, new-best flag, stars. Buttons: Next, Retry, Map | — |

- **Selecting on the platform.** Roundels are chosen by **poking with a finger or by ray/look-and-pinch**, the same two ways as grabbing (X16).
- **Signboard buttons** work by poke or ray.
- **Tutorial briefing.** The phone's three-card rules briefing shows on the signboard, once per session, on the tutorial station with no rails laid (section 7).

### 6.3 Pause

- **What it does.** The clock stops, the board dims, and pieces and the handle cannot be grabbed (X18).
- **What triggers it.**
  - The wrist menu.
  - The controller menu button.
  - The app losing focus: headset off, the system menu or passthrough settings opened.
  - The app being suspended.
- **Saving.** Pausing snapshots progress as the phone does (`GameFlow.SaveProgress`). Returning from a focus loss comes back to the Pause state, not straight into play.

### 6.4 The wrist menu

- **Where it lives.** A small button on the **back of the non-dominant wrist**, where a watch would be. Poke it or look-and-pinch it to open a compact panel above the wrist.
- **Why not the palm.** Both headsets use the palm for system gestures: palm-up and pinch opens the Quest system menu, and looking at the palm on visionOS opens the home and control shortcuts. So the menu stays off the palm.
- **Contents.** Resume, Retry, Back to map, and Settings. Settings holds dominant hand, Re-place board, height nudge, language, and music and effects volume.

---

## 7. Tutorial (the first station of the first line)

- **Same mechanism.** `TutorialCoach` stays the mechanism: a walkthrough over the solved path. The target is the first empty cell in S-to-E order, and which lesson a step teaches is read off the board. Only the prompts and highlights change, which means **new `BoardAction` values** for grab, hover, drop, return, lift and throw, and **new copy keys**.
- **Guide.**
  - For the **first three rails** the guide highlights **both the tray slot and the target cell**. After that it highlights **the cell only**, and the player finds the key.
  - The guide stays **locked** until the erase lesson is done. Grabbing a piece is always allowed, but a drop anywhere other than the target returns the piece with a note rather than the error cue.
- **Staged mistakes.** Both stay, rewritten for XR:
  1. **The adjacency rule.** The coach asks for a piece that is illegal at the target. The ghost goes red, and on release the piece flies back to the tray.
  2. **Erasing.** The coach points at the overfill cell derived by `TryFindMistake`. The player lays the piece, watches the clue sign turn red, and is then shown how to lift the piece and throw or release it.
- **Callout.** The tutorial callout is a world-space sign beside the target. It no longer maps world to screen (`PlayScreen.PointTutorialAt` is phone-only).
- **Level constraints.** `LevelCollectionTests` keeps holding the tutorial level to its current constraints, which still suit XR: solvable by propagation alone, both rail kinds on the guided path, and exactly one tutorial station.

---

## 8. Presentation

- **Board art** is unchanged: the same meshes and materials, the same track bending, the same palette tokens (`Palette.cs` stays the single re-skin point). The line colour still tints `TileForced`.
- **Signage.** The signboard, wrist menu and placement sign are **world-space UI Toolkit panels**. They reuse the style sheets, the palette tokens, the `UI` String Table, the `Fonts` Asset Table and the shared elements (roundel, LED strip, icons). The layouts are new (X20). The line colour is still published as `--line-current`.
- **Train run**: `TrainRunner` unchanged, at board scale. Same tunnel reveal and hiding.
- **Steam effects**: one particle system in two sizes (puff and burst) plus a whistle. Placement keeps its scale-in; a return plays a quick arc back to the origin.
- **Rendering on Quest**: a dedicated render pipeline asset.
  - HDR off, post-processing off, 4× MSAA, Forward renderer.
  - Target 90 Hz on Quest 3, never below 72 Hz on Quest 3S.
  - Refresh rate and foveation are set per device.
- **Rendering on Vision Pro**: URP in Metal mode with foveated rendering. Post-processing only if it holds frame rate.
- **Comfort**: the world never moves the player. Nothing is attached to the head except, briefly, the first-launch "look at a table" sign.

## 9. Systems carried over

| System | XR behaviour |
|---|---|
| Progression (lines, network reveal, unlocks) | Unchanged, from `NetworkDefinition` and `NetworkLayout` |
| Stars | Same `starTimes` thresholds per level. **Open item:** grab-and-drop may be slower or faster than tapping. XR5 measures a sample on device and may introduce a single XR scaling factor. The level assets are never forked |
| Timer | Unchanged, except that it starts on the first grab (section 4.2) |
| Save / Continue | Same `save.json` format and `FileSaveStore`, local to each device. **No sync between phone and headset** |
| Localisation | Same `UI` String Table and four locales. XR copy adds keys under an `xr.` prefix. The Japanese font atlas bake (`JaFontAtlasBuilder`) covers the new keys |
| Audio | The same `AudioCue` vocabulary, **spatialised**: piece cues from the piece, board cues from the board, UI cues from the signboard or wrist |
| Haptics | Controller haptics driven by the same cue-to-feel table (`Haptics.cs` gains an XR back end). Bare hands feel nothing, so, as now, **nothing may depend on haptics alone** |

---

## 10. Technical architecture

### 10.1 Project and packages

- One project on Unity **6.7** (X2), currently `6000.7.0a6` (X3), moved to later 6.7 builds as they ship.
- **Quest packages** (XR3): XR Plug-in Management, OpenXR, OpenXR Meta (`com.unity.xr.meta-openxr`), XR Interaction Toolkit, AR Foundation, XR Hands.
- **Vision Pro packages** (XR11): visionOS XR (`com.unity.xr.visionos`), plus PolySpatial's spatial pointer input if Metal mode needs it for look-and-pinch.
- **Versions** are pinned to what the 6.7 manual lists for this editor. As of 11 Sep 2026:

  | Package | Version |
  |---|---|
  | OpenXR | 1.18.0 |
  | OpenXR Meta | 2.6.1 (marked pre-release) |
  | XRI | 3.7.0-pre.1 (the only XRI listed for 6.7) |
  | AR Foundation | 6.6.2 |
  | XR Hands | 1.10.0-pre.1 |
  | visionOS XR | 3.2.2 |

  Several are pre-release, so XR3 pins exact versions in the manifest.
- **Not used**: Meta XR Core / All-in-One / Interaction SDK / MRUK. They do not compile on 6.7 (X12); revisit when they do.

### 10.2 Assemblies (X21)

| Assembly | Location | Depends on | Contents |
|---|---|---|---|
| `TrainSudoku.Core` | `Assets/01.Scripts/Core/` | nothing from `UnityEngine` | As today, plus `PieceDrop` (X22) |
| `TrainSudoku.Presentation` | `Assets/01.Scripts/Presentation/` | Core, Unity, Localization | The level data types (`LevelDefinition`, `LevelCollection`, `LineDefinition`, `NetworkDefinition`, **`.meta` GUIDs kept**). Also `Palette`, `Motion`, `SceneObjects`, the board display (tiles, tunnels, clues, pieces, decor, validator hookup) split out of `BoardView`, `TrackMeshBender`, the procedural meshes, `PieceView`, `PopScale`, `TrackAssets`, `TrainRunner`, `TrainAssets`, `AudioCue` and the shared UI Toolkit elements |
| `TrainSudoku.Phone` | `Assets/01.Scripts/Phone/` | Core, Presentation, Input System, uGUI | `GameManager` (phone), `UiShell`, the `UiScreen` screens, touch/mouse board input (tap flow, long-press erase), `BoardCamera`, iOS `Haptics` |
| `TrainSudoku.XR` | `Assets/01.Scripts/XR/` | Core, Presentation, XR packages | XR shell and `GameManager` (XR), board placement and anchor, handle, tray, grab interface and its XRI implementation, clue-sign billboarding, signboard, wrist menu, steam effects, XR haptics. **The only assembly that references an XR package** |
| `TrainSudoku.Editor` | `Assets/01.Scripts/Editor/` | as today | Unchanged |
| `TrainSudoku.Tests.EditMode` | `Assets/99.Test/EditMode/` | Core (and Presentation for the asset tests) | Existing suite plus `PieceDropTests` |

- **The data types keep their meta GUIDs.** Each level asset records its type's assembly in `m_EditorClassIdentifier`, so moving the types means the 216 shipped level assets and the 24 line assets must still load. The asset round-trip tests prove it.
- **The `BoardView` split.** Today its tap and long-press input (the `PointerSample`, press, hover and marker code) is interleaved with rendering and feedback. XR1 separates them: the board display exposes spawn, remove, refresh, pulse and tutorial-guide calls, and each platform drives it. `IBoardView` stays the boundary to the flow.

### 10.3 The grab interface (X12)

The XR input layer turns hardware events into a small set of calls on `PieceDrop`, and nothing else touches the board.

| Event | Meaning |
|---|---|
| `GrabFromTray(hand, key)` | A hand took a piece from a tray slot |
| `GrabFromCell(hand, x, y)` | A hand lifted a placed piece. Refused for a fixed piece |
| `Hover(hand, cell or none)` | The held piece is in the hover band over a cell, or not |
| `Release(hand, cell or none, thrown)` | Let go over a cell or off the platform, with the throw flag computed from hand speed |

- **`PieceDrop` returns an outcome** from section 4.4's table (Placed, Returned, Replaced, Moved, Puffed, Thrown, Refused). The presentation layer plays it, and `BoardView`'s `Acted` event carries it to the tutorial coach.
- **`PieceDrop` owns the edge cases.** The move rollback, "re-place at origin if still legal, else puff", replace, and two hands at once all live in Core and are unit-tested.
- **Implementations**: XRI direct and near-far interactors with XR Hands on Quest; XRI plus the visionOS spatial pointer on Vision Pro. Meta's hand grab can later be a second Quest implementation.
- The platform map and roundels, the signboard and the wrist menu use XRI's UI interaction (poke and ray) against world-space UI Toolkit panels.

### 10.4 Scenes and builds

- `Assets/Scenes/SampleScene.unity` stays the phone scene, unchanged. The XR edition has its own scene, `Assets/Scenes/XR.unity`.
- **Build profiles**:

  | Profile | Target | XR |
  |---|---|---|
  | Phone iOS | iOS | none |
  | Phone Android | Android | none |
  | Quest | Android, using Unity's **Meta Quest** build profile (Vulkan, ARM64, IL2CPP) | OpenXR |
  | Vision Pro | visionOS | visionOS XR |

  Each profile carries its own scene list, scripting defines, Player settings (bundle id, icons) and Quality/Graphics settings (render pipeline asset). XR3 confirms that the bundle id and icons really are overridable per profile.
- **The phone builds must not start XR or request XR permissions.** Build profiles **cannot** hold per-profile XR settings. XR Plug-in Management keeps one loader list per build target, so the phone Android and Quest profiles share it. Reading the package source (not documented behaviour), switching to the Quest profile turns the OpenXR loader on and switching back does not turn it off, so a phone APK could silently ship with OpenXR. Two guards, both in XR3:
  1. **An XR build guard** in `TrainSudoku.Editor`, next to `AddressablesBuildGuard`. It is a `BuildPlayerProcessor` (it must run before the loader list is serialised into the build) that assigns the OpenXR loader for Android when the Quest profile is building, and removes it for the phone profile (`XRPackageMetadataStore.AssignLoader` / `RemoveLoader`). It fails the phone build if OpenXR is still active.
  2. **"Initialize XR on Startup" off.** The XR shell starts XR itself (`XRGeneralSettings.Instance.Manager.InitializeLoader()` then `StartSubsystems()`), so even a leaked loader never starts on the phone.

  XR3 verifies the result on the Android phone APK: no `openxr_loader` library, no XR manifest entries or permissions, and no XR start-up.
- **Bundle ids**: Quest `com.GorillaGonzalez.Tsugi.XR`. Whether Vision Pro shares the iPhone app's id (universal purchase) is decided at XR11.
- Addressables stays on Packed Mode (`AddressablesBuildGuard`), and the XR build profiles go through the same guard.

### 10.5 Testing

- **Automated.** `PieceDrop` and everything else in Core is EditMode-tested. The phone suite must stay green at every milestone.
- **In the Editor.** Iteration uses the XR Interaction Simulator (from the XRI samples, with a simulated-hands mode) and AR Foundation's XR Simulation (planes, anchors and ray casts, but no hands). Whether either works on the macOS Editor is unverified. They speed things up but do not verify a milestone.
- **On device.** A milestone is verified by a human on a Quest 3 and a Quest 3S (and a Vision Pro for XR11 onward).

### 10.6 Editor upgrades

Every move to a newer 6.7 build (X3) goes through this checklist before XR work continues on it. Back the project up first: Unity gives no guarantee that an alpha project upgrades cleanly.

1. Full EditMode suite passes.
2. An iOS phone build installs and plays.
3. `AddressablesBuildGuard` still holds after a domain reload and a build.
4. The Japanese font atlas rebakes correctly (`m_LineHeight / m_PointSize` ≈ 1.45; `JaFontAtlasBuilder` writes internal FontAsset fields by name).
5. From XR3 on: a Quest build still starts passthrough, and the XR build guard (10.4) still keeps OpenXR out of the phone APK.

---

## 11. Milestones

Same rules as the phone work: **no `git commit` until a milestone's *Verified by human* box is ticked**; one commit per milestone, message `XR<n>: <title>`. Every milestone also requires the phone game to still pass its EditMode suite and play unchanged.

| # | Milestone | Done when |
|---|---|---|
| XR1 | Assembly split | `Presentation` and `Phone` assemblies exist; `BoardView` split into display and phone input. Level assets load unchanged, the suite passes, the phone plays identically on device. No XR packages yet |
| XR2 | `PieceDrop` | Core type with EditMode tests covering every row of section 4.4, the move rollback, replacing a piece, fixed-piece refusal, two hands and lift-equals-erase |
| XR3 | XR foundation | XR packages installed at pinned versions; the four build profiles exist; the XR build guard and manual XR start-up are in place (10.4). A Quest build shows passthrough, tracked hands and controllers in `XR.unity`. The Android phone APK is verified free of the OpenXR loader, XR manifest entries, XR permissions and XR start-up, including after switching profiles back and forth |
| XR4 | Board in the room | Surface-snapped placement, float fallback, handle move and turn, anchor persistence across launches, 0.06 scale, growth from the near edge, shadow catcher, clue signs facing the player |
| XR5 | Tray and grab | Six-slot tray with docking, handedness and moving between edges. Direct and distant grab, ghost, every release outcome, steam puff and throw, validator hookup, timer from the first grab. A level is playable start to finish. Star timing sampled (section 9) |
| XR6 | Flow on the platform | Network and line maps on the platform, roundel selection by poke and ray, signboard (masthead, station, arrival), miniature train run and skip, save and continue, stars |
| XR7 | Wrist menu and pause | Back-of-wrist menu, pause semantics including focus loss and headset removal, all settings |
| XR8 | XR tutorial | Tutorial station rewritten per section 7; `LevelCollectionTests` still green |
| XR9 | Sound, haptics, language | Spatialised cues, controller haptics, `xr.` copy in all four locales, Japanese atlas rebaked and checked on the signboard |
| XR10 | Quest release readiness | 90 Hz on Quest 3, never below 72 on 3S, Meta's store requirements (VRCs) pass, store icons and metadata, Horizon Store build uploaded to a test channel |
| XR11 | Vision Pro port | visionOS profile in Metal mode, Mixed immersion. Grab by look-and-pinch and direct pinch through the grab interface; surface placement and anchors; world-space UI Toolkit verified. Full playthrough of a line on device |
| XR12 | Vision Pro release readiness | Frame rate, App Store requirements, bundle id decision, TestFlight build |

### 11.0 Progress

| # | Milestone | Verified by human |
|---|---|---|
| XR1 | Assembly split | ☐ |
| XR2 | `PieceDrop` | ☐ |
| XR3 | XR foundation | ☐ |
| XR4 | Board in the room | ☐ |
| XR5 | Tray and grab | ☐ |
| XR6 | Flow on the platform | ☐ |
| XR7 | Wrist menu and pause | ☐ |
| XR8 | XR tutorial | ☐ |
| XR9 | Sound, haptics, language | ☐ |
| XR10 | Quest release readiness | ☐ |
| XR11 | Vision Pro port | ☐ |
| XR12 | Vision Pro release readiness | ☐ |

## 12. Out of scope for v1

- Multiplayer and co-located play.
- Hints.
- A level editor in XR.
- Syncing progress between phone and headset.
- Undo.
- Two-hand board scaling.
- Pieces landing on real surfaces (room-mesh physics).
- Meta's own SDKs (until they support 6.7).
- Vision Pro RealityKit mode and the Shared Space.
- Quest 2 and Quest Pro.
- An "explain why it's red" overlay on the ghost.

## 13. Risks

- **6.7 is an alpha, and several of its XR packages are pre-release** (XRI 3.7.0-pre.1, XR Hands 1.10.0-pre.1, OpenXR Meta 2.6.1). A later alpha may break a package, and a package update may break us. Mitigation: exact version pins, XR3 is early and small and verified before any gameplay depends on it, and every editor change runs the 10.6 checklist.
- **Meta SDKs unavailable on 6.7.** We lose Meta's better hand grab and instant placement. Mitigation: the grab interface (10.3) lets either be added later without touching the rules or board.
- **Hand-tracking precision at 6 cm pieces.** Grabs at this size can miss. Mitigation: the tray spaces its slots generously, distant grab is always available, the ghost confirms the target before release, and XR5 tunes the hover band and snap on device.
- **Direct grab on Vision Pro is not documented for XRI.** Expect custom work in XR11 behind the same interface. Look-and-pinch is the fallback that is documented.
- **World-space UI Toolkit on Vision Pro Metal mode is unverified.** XR11 checks it first. If it fails, the signboard and wrist menu need a uGUI fallback on Vision Pro only.
- **One project, two Android products.** Build profiles cannot separate XR settings, and switching profiles can leave OpenXR on for the phone. Mitigation: the XR build guard plus manual XR start-up (10.4), verified on the APK at XR3 and at every editor upgrade.
- **Quest performance.** The board is many small meshes (bent track per piece, tiles, decor) and URP settings made for phone. Mitigation: a dedicated Quest render pipeline asset, and profiling in XR5, not XR10.
- **Editor upgrade side effects.** `JaFontAtlasBuilder` writes internal FontAsset fields by name, and `AddressablesBuildGuard` works around an a6 behaviour. Both are on the 10.6 checklist.
