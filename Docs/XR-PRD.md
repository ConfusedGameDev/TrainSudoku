# Tsugi XR — Product Requirements Document

(The mixed-reality edition of **Tsugi**. Like the phone game, the code keeps the working name TrainSudoku.)

Status: agreed 2026-09-11 by interview. Meta Quest first, Apple Vision Pro second. Revised the same day: sharing narrowed to the rules code and the levels, and the XR edition moved into its own Unity project.

This document **extends** `Docs/PRD.md`. It does not repeat what is unchanged:

- The rules (PRD 3).
- The level data and text format (PRD 9).
- The level editor (PRD 10).
- Best times, stars and auto-save (PRD 6).

It covers only what the XR edition changes or adds. Where the two disagree, this document wins for the XR build and `Docs/PRD.md` wins for the phone build.

`Docs/UIDesign.MD` governs the phone only. The XR edition keeps the same station-signage visual language, but implements it itself (section 8).

**The phone game remains in active development.** The two editions share **the rules code and the levels**, and nothing else. Everything else is re-implemented for XR.

---

## 0. Decisions taken

Settled by interview on 11 Sep 2026. Closed. Do not re-open one without asking first.

### 0.1 Product and platform

| # | Decision | Consequence |
|---|---|---|
| X1 | Phone and XR are **two live products**. They share **the rules code (`Core`) and the level data** only | The phone keeps developing independently. A level or line authored once ships to both. Nothing XR-specific may enter the level data or the save format |
| X2 | **Two Unity projects in one repo, sharing one local package** (section 10.1). The phone project stays where it is; the XR project is `TsugiXR/` | The phone project never sees an XR package, and each product has its own settings. Two Editors can be open at once, so phone and XR work run in parallel |
| X3 | Both projects stay on **`6000.7.0a6`**, the newest 6.7 build as of 11 Sep 2026 (there is no 6.7 beta yet; the latest beta is 6.6). They move to later 6.7 builds **together** | The shared package must compile in both. Every editor change goes through the checklist in 10.7 |
| X4 | Quest target is **Quest 3 and 3S** | Colour passthrough and a depth sensor are assumed. Quest Pro only if it comes for free; Quest 2 is not supported |
| X13 | Vision Pro uses **Metal mode, Mixed immersion, Full Space** | Same rendering pipeline, post-processing and (expected) world-space UI Toolkit as Quest. The app does not share space with other apps. The studio holds a Pro licence |
| X23 | Test hardware on hand: **Quest 3, Quest 3S, Apple Vision Pro** | Every milestone, Vision Pro included, is verified on a real headset |

### 0.2 Gameplay and interaction

| # | Decision | Consequence |
|---|---|---|
| X5 | **Hands first; controllers also supported** | Everything is designed around a pinch-grab. Controller grip maps to grab, trigger to UI select |
| X6 | **Fixed cell size, about 6 cm** | A 6×6 board with its tunnel ring is ~48 cm across; an 8×8 one ~60 cm. Two-hand scaling of the board is later, not v1 |
| X7 | The board **snaps to detected horizontal surfaces**; if none are found it **floats** at waist height | Moved at any time, mid-level included, by a handle. Its position **persists across sessions** through a saved anchor |
| X8 | The tray holds **six slots, one per key, orientation locked, unlimited supply** | A held piece stays aligned to the board's grid whatever the wrist does. Keys map 1:1 onto `Legality` |
| X9 | Drop rules: see section 4 | Ghost preview. An illegal release returns the piece to where it came from. Dropping on a player piece replaces it if legal. Clues update on release, not while hovering |
| X10 | Lifting a placed piece erases it; dropping it on another legal cell is a **move**. **Throw vs. release is purely visual** | Fixed pieces refuse the grab with a wobble and the "that's fixed" note. Removal is always allowed, as on the phone |
| X11 | The tray is **attached to the board** at the edge nearest the player, on the dominant-hand side | It moves to whichever edge the player walks to and never follows the head |
| X16 | Pieces can be grabbed **directly and at a distance** (hand ray on Quest, look-and-pinch on Vision Pro) | A piece held at a distance rides the board surface under the ray. The ghost and snap work the same either way |
| X17 | The board **grows from its near edge**; clue numbers are **standing signs that turn to face the player** | The tray and handle never jump between levels, and the board can be walked all the way round |
| X19 | Throw and puff are **steam**: a burst and a whistle for a throw, a small puff for a release | One particle effect in two sizes. The room's shape is never used |

### 0.3 Screens and flow

| # | Decision | Consequence |
|---|---|---|
| X14 | Level select, pause and arrival: see section 6 | The **network and line maps are drawn on the platform itself**. A standing **signboard** at the far edge carries the masthead, briefing and arrival. A **wrist menu** carries pause and settings. The Concourse screen is dropped in XR |
| X15 | v1 carries **every phone system**; the **tutorial is rewritten** for XR | Out of scope for v1: multiplayer/co-location, hints, a VR level editor |
| X18 | Pause **stops the clock, dims the board and locks the pieces** | Also triggered by taking the headset off and by the system menu |
| X20 | The XR signboard and menus follow the phone's **visual language** (colour tokens, fonts, roundel, LED strip), **re-implemented in the XR project** | The XR project may copy phone files as a starting point. Copies are forks: never kept in sync, never referenced across projects |

### 0.4 Code and sharing

| # | Decision | Consequence |
|---|---|---|
| X12 | Tech: Unity's cross-platform stack — **OpenXR + OpenXR Meta + XR Interaction Toolkit + AR Foundation + XR Hands** — behind **our own grab interface** | Quest can later swap in Meta's hand grab without the rules or board code knowing. Vision Pro is a new platform, not a rewrite. Meta's own SDKs do not compile on 6.7 and are not used |
| X21 | The shared package holds **`Core`, the level data types, the level/line/network assets and the tests that need only those** | The phone's `TrainSudoku.Game` stays whole and keeps its screens, board display and input. XR writes its own |
| X22 | XR-only rules (`PieceDrop`, the XR tutorial coach) live in an **engine-free assembly in the XR project**, built on the shared `Core` and unit-tested there | The shared package only grows with code both products use. The phone never has to keep XR-only code green |
| X24 | **Localisation and art are not shared.** XR has its own String Table (same four locales) and its own Japanese atlas bake. Art (train kit, track meshes, fonts, the tsugi mark) is **copied once** into the XR project | Station and line names travel with the level assets, so they arrive in XR anyway. The two re-skins are independent |
| X25 | **The phone edits the shared package, XR reads it.** Every shared change must pass **both** projects' test suites before a commit | The phone project's level editor, Line Map Editor and network tools are the only writers of level assets. Shared code may be edited from either project under the both-suites rule |
| X26 | The extraction is the **first XR milestone**, and it starts only once the in-flight 24-line network expansion is committed | Moving the files the expansion is still writing would tangle two pieces of work in one diff |

---

## 1. Vision

The same puzzle, set on the player's own table. The player puts a small model railway platform down on a real surface, picks a station from a map printed on it, and lays track with their hands. They pinch pieces from a tray at the board's edge and set them on the grid. Pieces they no longer want are lifted off and tossed away in a puff of steam. A finished route sends a miniature train out of one tunnel and into the other.

## 2. Platforms

| Priority | Platform | Notes |
|---|---|---|
| 1 | Meta Quest 3, Quest 3S | Android, OpenXR, colour passthrough. Hands and Touch controllers. Horizon Store. |
| 2 | Apple Vision Pro | visionOS, Metal mode, Mixed immersion, Full Space. Hands only (look-and-pinch and direct pinch). App Store. |

The phone game (iOS, Android) continues in its own project (X2). At every XR milestone that touches the shared package, it must still build and pass its tests.

**Vision Pro comes second, but nothing built for Quest may block it.** In practice:

- Interaction goes only through the grab interface (section 10.4).
- No Meta-only API is used.
- Every UI is world-space UI Toolkit.
- Distant grab works with look-and-pinch.
- The wrist menu avoids the palm (section 6.4).

## 3. Game rules

Unchanged: PRD 3, including placement legality, the adjacency rule and the win condition, all enforced by the shared `Core`. The XR edition adds no rule. It changes how a piece reaches a cell (section 4), not what may go there.

---

## 4. Player interaction

### 4.1 The tray

- Six slots, one per key: `NS`, `EW`, `NW`, `NE`, `SW`, `SE`. Each slot shows the real track piece on a small concrete tile, at board scale.
- **Supply is unlimited.** Taking a piece leaves another in the slot straight away.
- **Orientation is locked to the board's grid.** A slot's piece, and a piece being held, always shows its key relative to the board's north, whatever the hand does. The piece may follow the hand's position and tilt slightly for feel, but its yaw snaps to the grid.
- **Where it sits.** A 2×3 block docked at the near edge of the board, on the dominant-hand side (a setting, default right). It moves to the edge nearest the player once they have stood closer to a different edge for about 1.5 s, and never while a piece is held. It moves with the board.
- **Keys stay relative to the board.** When the player walks to the east edge, `NS` reads as running left to right. That is correct: keys belong to the board, not to the viewer.

### 4.2 Grabbing

- **Direct**: pinch (or controller grip) on a tray piece or a placed player piece.
- **Distant**: hand ray on Quest, controller ray, or look-and-pinch on Vision Pro, aimed at a tray piece or placed piece. A piece held at a distance rides the platform surface under the ray.
- Each hand holds at most one piece, and both hands may hold at once.
- **Fixed pieces (`K`) refuse the grab.** They wobble, play the "that's fixed" note, and stay put.
- **Lifting a placed piece erases it at once.** The cell empties, the validator runs, and clues update. The piece is now held like any tray piece and remembers the cell it came from.
- **The clock starts on the first grab** of any piece, which replaces the phone's "first tap" (PRD 6). A restored in-progress snapshot waits for the first grab the same way.

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
- The phone's tap flow (`PlacementSession`: neighbour marks, auto-place, long-press erase) is not used in XR.

### 4.5 Controllers

| Input | Action |
|---|---|
| Grip | Grab and release a piece; grab the board handle |
| Trigger | Select UI (signboard, map roundels, wrist menu) by ray |
| Menu button | Opens the wrist menu |
| Thumbstick | Unused in v1 |

---

## 5. The board in the room

### 5.1 Scale and construction

- The XR edition builds its own board.
  - **Positions** come from the shared `Core`, at **1 unit per cell**: cell, tunnel and clue positions from `BoardLayout`, per-piece centre lines from `TrackCurve`, and the S-to-E route from `TrackPath`.
  - **Art** is the copied train kit (X24).
- The whole board sits under one root scaled to **0.06**, so one cell is 6 cm. No board code learns about metres, and `Core`'s layout maths is used unchanged.
- Tunnels, clue signs, the train and every other board mesh inherit the scale.

### 5.2 First placement

1. The app opens in passthrough. The first time, it asks for the headset's scene/spatial permission. If refused, placement falls back to floating.
2. A floating sign says "look at a table".
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

- **Clue signs.** Clue numbers are small standing signs that turn about the vertical to face the player's head, so they read from any edge. They keep the phone's colour states: satisfied green, overfilled red.
- **Tunnel letters.** The S and E letters on the tunnels turn to face the player the same way.
- **Environment art.** The Japanese environment art (platform decals, the locomotive's destination plate) is art and does not turn.

### 5.6 Sitting on the table

- An invisible shadow-catching surface under the platform lets the board, pieces and train cast shadows onto the real table.
- There is one directional light, chosen for a readable shadow, not estimated from the room.

---

## 6. Screens and flow

### 6.1 States

- **Same states and rules.** The shared `GameFlow` is used unchanged: it still owns every transition and still throws on an invalid one.
- **Concourse skipped.** XR starts the flow and moves straight to `Network`. The Concourse (`MainMenu`) is never shown, and XR never calls a transition into it.
- **Board placement is not a `GameFlow` state.** It is a precondition held by the XR shell. The flow starts once the board is placed, and moving the board later never touches the flow.

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
- **Tutorial briefing.** The three-card rules briefing shows on the signboard, once per session, on the tutorial station with no rails laid (section 7).

### 6.3 Pause

- **What it does.** The clock stops, the board dims, and pieces and the handle cannot be grabbed (X18).
- **What triggers it.**
  - The wrist menu.
  - The controller menu button.
  - The app losing focus: headset off, the system menu or passthrough settings opened.
  - The app being suspended.
- **Saving.** Pausing snapshots progress through the shared `GameFlow.SaveProgress`. Returning from a focus loss comes back to the Pause state, not straight into play.

### 6.4 The wrist menu

- **Where it lives.** A small button on the **back of the non-dominant wrist**, where a watch would be. Poke it or look-and-pinch it to open a compact panel above the wrist.
- **Why not the palm.** Both headsets use the palm for system gestures: palm-up and pinch opens the Quest system menu, and looking at the palm on visionOS opens the home and control shortcuts. So the menu stays off the palm.
- **Contents.** Resume, Retry, Back to map, and Settings. Settings holds dominant hand, Re-place board, height nudge, language, and music and effects volume.

---

## 7. Tutorial (the first station of the first line)

- **Same idea as the phone.** A walkthrough over the solved path: the target is the first empty cell in S-to-E order, and which lesson a step teaches is read off the board.
- **Its own coach.** The phone's `TutorialCoach` (shared `Core`) is built around the tap flow: its guide kinds and actions are select, side, hold. So XR gets **its own coach** in the XR rules assembly (X22). It reuses the shared `Solver`, `PathFinder` and `Legality`, and the mistake-finding logic. If `TutorialCoach.TryFindMistake` must become public to be reused, that is a shared change under X25.
- **Guide.**
  - For the **first three rails** the guide highlights **both the tray slot and the target cell**. After that it highlights **the cell only**, and the player finds the key.
  - The guide stays **locked** until the erase lesson is done. Grabbing a piece is always allowed, but a drop anywhere other than the target returns the piece with a note rather than the error cue.
- **Staged mistakes.** Both stay, rewritten for XR:
  1. **The adjacency rule.** The coach asks for a piece that is illegal at the target. The ghost goes red, and on release the piece flies back to the tray.
  2. **Erasing.** The coach points at an overfill cell derived from the board, as on the phone. The player lays the piece, watches the clue sign turn red, and is then shown how to lift the piece and throw or release it.
- **Callout.** The tutorial callout is a world-space sign beside the target.
- **Level constraints.** The phone's `LevelCollectionTests`, which move into the shared package, keep holding the tutorial level to constraints that also suit XR: solvable by propagation alone, both rail kinds on the guided path, and exactly one tutorial station.

---

## 8. Presentation

- **Visual language**: the same station signage as the phone. The colour tokens (`Paper`, `Ink`, `Led`, and the rest from `Docs/UIDesign.MD` section 6), the Barlow and Noto faces, roundels and the LED strip. The line colour tints the active line's markings.
- **Re-implemented in the XR project.** It gets its own palette file as its single re-skin point, and its own style sheets and elements. Phone files may be copied as a starting point (X20).
- **Board art**: the copied train kit and track meshes, bent along `Core`'s `TrackCurve`, at the 0.06 board scale.
- **Signage**: the signboard, wrist menu and placement sign are **world-space UI Toolkit panels**.
- **Train run**: the train follows `Core`'s `TrackPath` by arc length and hides inside the tunnels, as on the phone.
- **Steam effects**: one particle system in two sizes (puff and burst) plus a whistle. Placement has a scale-in; a return plays a quick arc back to the origin.
- **Rendering on Quest**: the XR project's own render pipeline asset.
  - HDR off, post-processing off, 4× MSAA, Forward renderer.
  - Target 90 Hz on Quest 3, never below 72 Hz on Quest 3S.
  - Refresh rate and foveation are set per device.
- **Rendering on Vision Pro**: URP in Metal mode with foveated rendering. Post-processing only if it holds frame rate.
- **Comfort**: the world never moves the player. Nothing is attached to the head except, briefly, the first-launch "look at a table" sign.

## 9. Systems

| System | XR behaviour |
|---|---|
| Progression (lines, network reveal, unlocks) | From the shared `NetworkDefinition`, `NetworkLayout` and `GameFlow`, unchanged |
| Stars | Same `starTimes` thresholds per level, through the shared `ProgressTracker`. **Open item:** grab-and-drop may be slower or faster than tapping. XR6 measures a sample on device and may introduce a single XR scaling factor, held in the XR project. The level assets are never forked |
| Timer | Shared `PlayTimer`; starts on the first grab (section 4.2) |
| Save / Continue | Shared `SaveJson`, `FileSaveStore` and `LevelProgress`: the same `save.json` format, local to each device. **No sync between phone and headset**, but the shared format keeps that possible later |
| Localisation | **XR's own** `com.unity.localization` setup, String Table and four locales (X24). Station and line names come from the shared assets. XR gets its own Japanese atlas bake covering its table plus those names |
| Audio | XR's own cue set, **spatialised**: piece cues from the piece, board cues from the board, UI cues from the signboard or wrist |
| Haptics | Controller haptics from XR's own cue-to-feel table. Bare hands feel nothing, so **nothing may depend on haptics alone** |

---

## 10. Technical architecture

### 10.1 Repository layout (X2)

```
TrainSudoku/                                    repo root = phone project (unchanged location)
├── Assets/ Packages/ ProjectSettings/          phone project
├── Docs/                                       PRD.md, UIDesign.MD, XR-PRD.md
├── Shared/
│   └── com.gorillagonzalez.tsugi.shared/       the shared local package
│       ├── package.json
│       ├── Runtime/Core/                       TrainSudoku.Core      (moved from Assets/01.Scripts/Core/)
│       ├── Runtime/Data/                       TrainSudoku.Data      (the level data types)
│       ├── Levels/                             level, line and network assets (moved from Assets/03.Data/Levels/)
│       └── Tests/EditMode/                     TrainSudoku.Shared.Tests
└── TsugiXR/                                    XR project
    ├── Assets/ Packages/ ProjectSettings/
    └── CLAUDE.md                               the XR project's own agent guide
```

- **How the projects use it.** Both manifests reference the package by path: `"com.gorillagonzalez.tsugi.shared": "file:../Shared/com.gorillagonzalez.tsugi.shared"` from the phone, `file:../../Shared/…` from XR. Both also list it under `testables`, so each project's Test Runner runs the shared tests.
- **The phone project nests the XR project.** It never imports `TsugiXR/`, because Unity only reads `Assets/` and `Packages/`. Its `.gitignore` rules must also cover `TsugiXR/Library/`, `TsugiXR/Logs/`, `TsugiXR/UserSettings/` and the generated IDE files there.

### 10.2 What moves into the shared package (X21)

- **`TrainSudoku.Core`**: all of it, unchanged, still `noEngineReferences: true`.
  - That includes the phone-only parts (`PlacementSession`, `CameraFit`, `TutorialCoach`). They are plain C#, they cost XR nothing, and splitting them out would churn the phone for no gain.
- **`TrainSudoku.Data`**, a new assembly referencing only Core and UnityEngine.
  - It holds `LevelDefinition`, `LevelCollection`, `LineDefinition` and `NetworkDefinition`, moved out of `TrainSudoku.Game`.
  - They reference nothing in Game today: only Core types and a line `Color`.
  - **Their `.cs.meta` GUIDs are kept**, so the level and line assets keep their script links.
  - They move to the namespace `TrainSudoku.Data`; if any asset fails to load after the move, the old namespace is kept instead.
- **The assets**: every level, line and network asset now under `Assets/03.Data/Levels/` (the 24-line set: 216 stations plus the unused `Hard`), meta GUIDs kept.
- **The tests that need only Core and Data**: the Core rule, solver, parser, save and flow tests, plus the asset tests (`LevelCollectionTests`, `LevelDefinitionTests`).
  - Tests that touch the phone's `Game` or `Editor` assemblies stay in the phone project: `BoardCameraTests`, `TrackMeshBenderTests`, `LineMapValidationTests`, `MapGridTests`, `NetworkMapLayoutTests`, `StringTableTests`, and any others that fail to compile against Core and Data alone.
- **Phone-side changes that go with it.**
  - `TrainSudoku.Game`, `TrainSudoku.Editor` and the phone test assembly reference `TrainSudoku.Data`.
  - These editor tools hard-code `Assets/03.Data/Levels`: `LevelEditorWindow`, `LineMapEditorWindow`, `TunnelPieceBaker`, `NetworkGenerator`, `NetworkExpansion`. They are repointed at `Packages/com.gorillagonzalez.tsugi.shared/Levels`, which a local `file:` package allows writing to.
  - The phone's `CLAUDE.md` is updated for the new locations.
- **Not moved**: everything else in the phone project, which stays exactly as it is.

### 10.3 XR project assemblies

| Assembly | Location | Depends on | Contents |
|---|---|---|---|
| `TrainSudoku.XR.Rules` | `TsugiXR/Assets/01.Scripts/Rules/` | shared Core only; `noEngineReferences: true` | `PieceDrop` (section 4.4), the XR tutorial coach (section 7), tray-docking and throw-threshold maths that need no engine |
| `TrainSudoku.XR` | `TsugiXR/Assets/01.Scripts/Game/` | shared Core and Data, XR.Rules, XR packages, Localization | XR shell and game manager, XR start-up, board placement and anchor, handle, board display (tiles, tunnels, clue signs, pieces, track bending), tray, the grab interface and its XRI implementation, train run, platform maps, signboard, wrist menu, steam effects, audio, haptics, palette |
| `TrainSudoku.XR.Editor` | `TsugiXR/Assets/01.Scripts/Editor/` | as needed | A copy of `AddressablesBuildGuard` (localisation brings Addressables, and the Packed Mode trap is the editor's, not the project's), the Japanese atlas bake |
| `TrainSudoku.XR.Tests.EditMode` | `TsugiXR/Assets/99.Test/EditMode/` | XR.Rules, shared Core and Data | `PieceDropTests`, XR tutorial coach tests |

The XR project follows the phone's numbered asset folders (`00.Plugins` … `99.Test`).

### 10.4 The grab interface (X12)

The XR input layer turns hardware events into a small set of calls on `PieceDrop`, and nothing else touches the board.

| Event | Meaning |
|---|---|
| `GrabFromTray(hand, key)` | A hand took a piece from a tray slot |
| `GrabFromCell(hand, x, y)` | A hand lifted a placed piece. Refused for a fixed piece |
| `Hover(hand, cell or none)` | The held piece is in the hover band over a cell, or not |
| `Release(hand, cell or none, thrown)` | Let go over a cell or off the platform, with the throw flag computed from hand speed |

- **`PieceDrop` works on the shared `Board`**, through `TryPlace`, `TryErase` and `Legality`. It returns an outcome from section 4.4's table (Placed, Returned, Replaced, Moved, Puffed, Thrown, Refused). The board display plays it and the tutorial coach reads it.
- **`PieceDrop` owns the edge cases.** The move rollback, "re-place at origin if still legal, else puff", replace, and two hands at once all live in `XR.Rules` and are unit-tested.
- **Implementations**: XRI direct and near-far interactors with XR Hands on Quest; XRI plus the visionOS spatial pointer on Vision Pro. Meta's hand grab can later be a second Quest implementation.
- The platform maps, signboard and wrist menu use XRI's UI interaction (poke and ray) against world-space UI Toolkit panels.

### 10.5 Packages, scenes and builds

- **Quest packages**: XR Plug-in Management, OpenXR, OpenXR Meta (`com.unity.xr.meta-openxr`), XR Interaction Toolkit, AR Foundation, XR Hands. Also URP, the Input System and `com.unity.localization`.
- **Vision Pro packages** (XR12): visionOS XR (`com.unity.xr.visionos`), plus PolySpatial's spatial pointer input if Metal mode needs it for look-and-pinch.
- **Versions** are pinned exactly to what the 6.7 manual lists for this editor. As of 11 Sep 2026:

  | Package | Version |
  |---|---|
  | OpenXR | 1.18.0 |
  | OpenXR Meta | 2.6.1 (marked pre-release) |
  | XRI | 3.7.0-pre.1 (the only XRI listed for 6.7) |
  | AR Foundation | 6.6.2 |
  | XR Hands | 1.10.0-pre.1 |
  | visionOS XR | 3.2.2 |

- **Not used**: Meta XR Core / All-in-One / Interaction SDK / MRUK. They do not compile on 6.7 (X12); revisit when they do.
- **One scene**, `TsugiXR/Assets/Scenes/XR.unity`.
- **Build profiles.** Quest uses Unity's **Meta Quest** build profile (Android, Vulkan, ARM64, IL2CPP); Vision Pro gets a visionOS profile. They are different build targets, so XR Plug-in Management's per-target settings separate them naturally.
- **The phone project installs no XR package**, so it needs no build guard. That problem existed only in the one-project plan.
- **Bundle ids**: Quest `com.GorillaGonzalez.Tsugi.XR`. Whether Vision Pro shares the iPhone app's id (universal purchase) is decided at XR12.
- Addressables stays on Packed Mode in the XR project too (10.3).

### 10.6 Working with the shared package (X25)

- **Levels are written only from the phone project.** The XR project reads the package and never creates or modifies a level, line or network asset.
- **Shared code may be edited from either project.** A change is committed only when **both** projects' EditMode suites are green, since each includes the shared tests through `testables`.
- **A shared change travels with the milestone that needs it.** It is committed together with that milestone's changes in whichever project.
- **Two Editors open.** With both projects open, an edit in one is picked up by the other when it regains focus. Do not edit the same shared file from both Editors at once.
- **Save format.** `SaveJson` lives in the shared Core, so a save-format change is a shared change and must keep both products' existing saves loading.

### 10.7 Editor upgrades

Both projects move to a newer 6.7 build together (X3). Back both up first: Unity gives no guarantee that an alpha project upgrades cleanly.

1. Both EditMode suites pass.
2. An iOS phone build installs and plays.
3. `AddressablesBuildGuard` still holds in both projects after a domain reload and a build.
4. The phone's Japanese font atlas rebakes correctly (`m_LineHeight / m_PointSize` ≈ 1.45), and so does XR's from XR10 on.
5. From XR2 on: a Quest build still starts passthrough with hands.

### 10.8 Testing

- **Automated.** `PieceDrop`, the XR coach and the shared Core are EditMode-tested.
- **In the Editor.** Iteration uses the XR Interaction Simulator (from the XRI samples, with a simulated-hands mode) and AR Foundation's XR Simulation (planes, anchors and ray casts, but no hands). Whether either works on the macOS Editor is unverified. They speed things up but do not verify a milestone.
- **On device.** A milestone is verified by a human on a Quest 3 and a Quest 3S (and a Vision Pro for XR12 onward).

---

## 11. Milestones

Same rules as the phone work: **no `git commit` until a milestone's *Verified by human* box is ticked**; one commit per milestone, message `XR<n>: <title>`. Any milestone that touches the shared package also requires the phone's full suite to pass and the phone to play unchanged.

| # | Milestone | Done when |
|---|---|---|
| XR1 | Shared package | Starts after the network expansion is committed (X26). `Shared/com.gorillagonzalez.tsugi.shared/` holds Core, Data, the level assets and the shared tests (10.2). The phone references it and runs its tests, and its level tools write into it. Level assets load unchanged. The phone's full suite passes, the phone plays identically on device, and the phone's `CLAUDE.md` is updated |
| XR2 | XR project | `TsugiXR/` on `6000.7.0a6` references the shared package and passes its tests. XR packages are installed at pinned versions, the Quest build profile is set up, and the Addressables guard is copied. A Quest 3 build shows passthrough, tracked hands and controllers. XR `CLAUDE.md` written, `.gitignore` covers the nested project |
| XR3 | `PieceDrop` | `XR.Rules` with EditMode tests covering every row of section 4.4, the move rollback, replacing a piece, fixed-piece refusal, two hands and lift-equals-erase |
| XR4 | Board display | A shared level loads into an XR-built board at 0.06 scale: tiles, tunnels, clue signs facing the player, fixed pieces, bent track from `TrackCurve`, validator colouring, train run along `TrackPath`. Placed in front of the player for now |
| XR5 | Board in the room | Surface-snapped placement, float fallback, handle move and turn, anchor persistence across launches, growth from the near edge, shadow catcher |
| XR6 | Tray and grab | Six-slot tray with docking, handedness and moving between edges. Direct and distant grab, ghost, every release outcome, steam puff and throw, timer from the first grab. A level is playable start to finish. Star timing sampled (section 9) |
| XR7 | Flow on the platform | Network and line maps on the platform, roundel selection by poke and ray, signboard (masthead, station, arrival), train run skip, save and continue, stars |
| XR8 | Wrist menu and pause | Back-of-wrist menu, pause semantics including focus loss and headset removal, all settings |
| XR9 | XR tutorial | The XR coach and tutorial per section 7; the shared `LevelCollectionTests` still green |
| XR10 | Sound, haptics, language | XR String Table in all four locales, Japanese atlas bake checked on the signboard, spatialised cues, controller haptics |
| XR11 | Quest release readiness | 90 Hz on Quest 3, never below 72 on 3S, Meta's store requirements (VRCs) pass, store icons and metadata, Horizon Store build uploaded to a test channel |
| XR12 | Vision Pro port | visionOS profile in Metal mode, Mixed immersion. Grab by look-and-pinch and direct pinch through the grab interface; surface placement and anchors; world-space UI Toolkit verified. Full playthrough of a line on device |
| XR13 | Vision Pro release readiness | Frame rate, App Store requirements, bundle id decision, TestFlight build |

### 11.0 Progress

| # | Milestone | Verified by human |
|---|---|---|
| XR1 | Shared package | ☐ |
| XR2 | XR project | ☐ |
| XR3 | `PieceDrop` | ☐ |
| XR4 | Board display | ☐ |
| XR5 | Board in the room | ☐ |
| XR6 | Tray and grab | ☐ |
| XR7 | Flow on the platform | ☐ |
| XR8 | Wrist menu and pause | ☐ |
| XR9 | XR tutorial | ☐ |
| XR10 | Sound, haptics, language | ☐ |
| XR11 | Quest release readiness | ☐ |
| XR12 | Vision Pro port | ☐ |
| XR13 | Vision Pro release readiness | ☐ |

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
- Sharing anything beyond Core and the level data (X1).

## 13. Risks

- **6.7 is an alpha, and several of its XR packages are pre-release** (XRI 3.7.0-pre.1, XR Hands 1.10.0-pre.1, OpenXR Meta 2.6.1). A later alpha may break a package, and a package update may break us. Mitigation: exact version pins, XR2 is early and small and verified before any gameplay depends on it, and every editor change runs the 10.7 checklist.
- **The extraction disturbs a live phone project.** Moving Core, the data types and every level asset is a large diff in a product under active development. Mitigation: it waits for the network expansion to land (X26), keeps every meta GUID, is its own milestone with no XR code, and is verified by the phone's full suite and a device playthrough.
- **Shared-code drift.** Two teams (or two sessions) changing Core can break each other. Mitigation: the both-suites rule (X25), and XR-only rules kept out of the package (X22).
- **Meta SDKs unavailable on 6.7.** We lose Meta's better hand grab and instant placement. Mitigation: the grab interface (10.4) lets either be added later without touching the rules or board.
- **Hand-tracking precision at 6 cm pieces.** Grabs at this size can miss. Mitigation: the tray spaces its slots generously, distant grab is always available, the ghost confirms the target before release, and XR6 tunes the hover band and snap on device.
- **Direct grab on Vision Pro is not documented for XRI.** Expect custom work in XR12 behind the same interface. Look-and-pinch is the fallback that is documented.
- **World-space UI Toolkit on Vision Pro Metal mode is unverified.** XR12 checks it first. If it fails, the signboard and wrist menu need a uGUI fallback on Vision Pro only.
- **Quest performance.** The board is many small meshes (bent track per piece, tiles, decor). Mitigation: XR's own render pipeline asset, and profiling in XR6, not XR11.
- **Editor upgrade side effects.** `JaFontAtlasBuilder` writes internal FontAsset fields by name, and `AddressablesBuildGuard` works around an a6 behaviour. Both are on the 10.7 checklist, for both projects.
- **Unity MCP drives one Editor.** The agent tooling in `CLAUDE.md` assumes one open Editor. XR2's `CLAUDE.md` records how the XR Editor is driven. Batchmode against `TsugiXR/` works whenever that Editor is closed.
