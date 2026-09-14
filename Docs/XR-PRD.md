# Tsugi XR — Product Requirements Document

(The mixed-reality edition of **Tsugi**. Like the phone game, the code keeps the working name TrainSudoku.)

Status: agreed 2026-09-11 by interview. Meta Quest first, Apple Vision Pro second. Revised the same day: sharing narrowed to the rules code and the levels. **Revised 2026-09-13 by the user: there is no second Unity project.** XR is developed in this project on the long-lived branch `feat/MetaXR`, which takes the phone's `main` in by merge (X2, section 10). The shared package and its milestone (XR1) are dropped.

This document **extends** `Docs/PRD.md`. It does not repeat what is unchanged:

- The rules (PRD 3).
- The level data and text format (PRD 9).
- The level editor (PRD 10).
- Best times, stars and auto-save (PRD 6).

It covers only what the XR edition changes or adds. Where the two disagree, this document wins for the XR build and `Docs/PRD.md` wins for the phone build.

`Docs/UIDesign.MD` governs the phone only. The XR edition keeps the same station-signage visual language, but implements it itself (section 8).

**The phone game remains in active development.** The two editions share **the rules code and the levels**, and nothing else. Everything else is re-implemented for XR. They live in one Unity project: the phone on `main`, XR on `feat/MetaXR`.

---

## 0. Decisions taken

Settled by interview on 11 Sep 2026. Closed. Do not re-open one without asking first.

### 0.1 Product and platform

| # | Decision | Consequence |
|---|---|---|
| X1 | Phone and XR are **two live products**. They share **the rules code (`Core`) and the level data** only | The phone keeps developing independently. A level or line authored once ships to both. Nothing XR-specific may enter the level data or the save format |
| X2 | **One Unity project, two branches** (revised 2026-09-13; section 10.1). The phone develops on `main`; XR develops on the long-lived fork `feat/MetaXR`, which takes `main` in by merge and never merges back | `main` never sees an XR package. XR adds files under its own paths and puts its settings on the Meta Quest build profile, so merges from `main` stay clean (10.2) |
| X3 | Both branches stay on **`6000.7.0a6`**, the newest 6.7 build as of 11 Sep 2026 (there is no 6.7 beta yet; the latest beta is 6.6). They move to later 6.7 builds **together** | `Core` must compile on both. Every editor change goes through the checklist in 10.7 |
| X4 | Quest target is **Quest 3 and 3S** | Colour passthrough and a depth sensor are assumed. Quest Pro only if it comes for free; Quest 2 is not supported |
| X13 | Vision Pro uses **Metal mode, Mixed immersion, Full Space** | Same rendering pipeline, post-processing and (expected) world-space UI Toolkit as Quest. The app does not share space with other apps. The studio holds a Pro licence |
| X23 | Test hardware on hand: **Quest 3, Quest 3S, Apple Vision Pro** | Every milestone, Vision Pro included, is verified on a real headset |

### 0.2 Gameplay and interaction

| # | Decision | Consequence |
|---|---|---|
| X5 | **Hands first; controllers also supported** | Everything is designed around a pinch-grab. Controller grip maps to grab, trigger to UI select |
| X6 | **Cell size about 6 cm, scalable with both hands from 4 to 9 cm** | A 6×6 board with its tunnel ring is ~48 cm across at 6 cm; an 8×8 one ~60 cm. *Revised 2026-09-14 after the XR7 headset check:* two-hand scaling moved into v1 on the two-hand handle (5.3), and the size is saved with the board's anchor. Below 4 cm a piece is too small to pinch reliably |
| X7 | The board **snaps to detected horizontal surfaces**; if none are found it **floats** at waist height | Moved at any time, mid-level included, by a handle. Its position **persists across sessions** through a saved anchor |
| X8 | The tray holds **six slots, one per key, orientation locked, unlimited supply** | A held piece stays aligned to the board's grid whatever the wrist does. Keys map 1:1 onto `Legality` |
| X9 | Drop rules: see section 4 | Ghost preview. An illegal release returns the piece to where it came from. Dropping on a player piece replaces it if legal. Clues update on release, not while hovering |
| X10 | Lifting a placed piece erases it; dropping it on another legal cell is a **move**. **Throw vs. release is purely visual** | Fixed pieces refuse the grab with a wobble and the "that's fixed" note. Removal is always allowed, as on the phone |
| X11 | The tray is **attached to the board** at the edge nearest the player, on the dominant-hand side | It moves to whichever edge the player walks to and never follows the head |
| X16 | Pieces can be grabbed **directly and at a distance** (hand ray on Quest, look-and-pinch on Vision Pro) | A piece held at a distance rides the board surface under the ray. The ghost and snap work the same either way. *Revised 2026-09-13 after the XR6 headset check:* distant grab is for tray pieces; pieces on the board are taken by a close pinch only (4.2) |
| X17 | The board **grows from its near edge**; clue numbers are **standing signs that turn to face the player** | The tray and handle never jump between levels, and the board can be walked all the way round |
| X19 | Throw and puff are **steam**: a burst and a whistle for a throw, a small puff for a release | One particle effect in two sizes. *Revised 2026-09-14 after the XR7 headset check:* a piece let go or thrown off the platform first falls into the room, bouncing off its detected surfaces for 3 s before it puffs; touching the board puffs it at once (4.4) |

### 0.3 Screens and flow

| # | Decision | Consequence |
|---|---|---|
| X14 | Level select, pause and arrival: see section 6 | The **network and line maps are drawn on the platform itself**. A standing **signboard** at the far edge carries the masthead, briefing and arrival. A **wrist menu** carries pause and settings. The Concourse screen is dropped in XR |
| X15 | v1 carries **every phone system**; the **tutorial is rewritten** for XR | Out of scope for v1: multiplayer/co-location, hints, a VR level editor |
| X18 | Pause **stops the clock, dims the board and locks the pieces** | Also triggered by taking the headset off and by the system menu |
| X20 | The XR signboard and menus follow the phone's **visual language** (colour tokens, fonts, roundel, LED strip), **re-implemented under XR's own paths** | XR may copy phone files under its `XR/` paths as a starting point. Copies are forks: never kept in sync, and XR code never references the phone's UI code |

### 0.4 Code and sharing

| # | Decision | Consequence |
|---|---|---|
| X12 | Tech: Unity's cross-platform stack — **OpenXR + OpenXR Meta + XR Interaction Toolkit + AR Foundation + XR Hands** — behind **our own grab interface** | Quest can later swap in Meta's hand grab without the rules or board code knowing. Vision Pro is a new platform, not a rewrite. Meta's own SDKs do not compile on 6.7 and are not used |
| X21 | **Nothing is packaged** (revised 2026-09-13). XR uses **`Core`, the four level data types and the level/line/network assets in place**; the data types stay in `TrainSudoku.Game` | XR references `TrainSudoku.Game` for `LevelDefinition`, `LevelCollection`, `LineDefinition` and `NetworkDefinition` only, never for its screens, board display or input. XR writes its own |
| X22 | XR-only rules (`PieceDrop`, the XR tutorial coach) live in an **engine-free assembly under `Assets/01.Scripts/XR/Rules/`**, built on `Core` and unit-tested beside it | `Core` only grows with code both products use. `main` never has to keep XR-only code green |
| X24 | **Localisation copy and re-skins are not shared.** XR has its own `XR` String Table beside the phone's `UI` table (same four locales, same Localization settings) and its own Japanese atlas bake. Art (train kit, track meshes, fonts, the tsugi mark) is **used in place**; only what XR re-skins is copied under `02.Graphics/XR/` | Station and line names travel with the level assets, so they arrive in XR anyway. The two re-skins are independent |
| X25 | **Shared changes land on `main` first.** `Core`, the level assets and the four data types change only on `main` and reach XR by merge | The phone's level editor, Line Map Editor and network tools are the only writers of level assets. A commit on `feat/MetaXR` needs **both** the phone's and XR's EditMode suites green |
| X26 | *Obsolete (2026-09-13).* There is no extraction, so XR1 is dropped. The 24-line network expansion it waited for is committed (`b09b359`) | — |

---

## 1. Vision

The same puzzle, set on the player's own table. The player puts a small model railway platform down on a real surface, picks a station from a map printed on it, and lays track with their hands. They pinch pieces from a tray at the board's edge and set them on the grid. Pieces they no longer want are lifted off and tossed away in a puff of steam. A finished route sends a miniature train out of one tunnel and into the other.

## 2. Platforms

| Priority | Platform | Notes |
|---|---|---|
| 1 | Meta Quest 3, Quest 3S | Android, OpenXR, colour passthrough. Hands and Touch controllers. Horizon Store. |
| 2 | Apple Vision Pro | visionOS, Metal mode, Mixed immersion, Full Space. Hands only (look-and-pinch and direct pinch). App Store. |

The phone game (iOS, Android) continues on `main` (X2). On `feat/MetaXR` it must still pass its tests and play in the Editor at every XR milestone.

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
- **Distant**: hand ray on Quest, controller ray, or look-and-pinch on Vision Pro, aimed at a tray piece. A piece held at a distance rides the platform surface under the ray.
- **Placed pieces are taken by a close pinch only.** Revised 2026-09-13 after the XR6 headset check: on the board the pieces sit a cell apart, and a ray aimed at one kept landing on a neighbour.
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
| Any held piece | Off the platform, hand speed below the throw threshold | **Falls into the room**, bouncing off its detected surfaces, and puffs 3 s later, or at once if it touches the board. Piece gone |
| Any held piece | Anywhere, hand speed above the throw threshold (tunable; starts at 1.2 m/s) | **Thrown.** It flies along the throw and falls into the room the same way, then bursts into steam with a whistle. Same effect as a puff |
| A board piece | Another empty cell where its key is legal | **Moved.** A remove-then-place, already half done by the lift |

- **An illegal drop never costs the player anything.** A mistake is never confused with "delete".
- **Throwing and releasing mean the same thing** (X10); only the effect differs.
- **A piece leaving play is a real body under gravity** (X19). It tumbles and bounces off the room's detected surfaces (tables, the floor) and puffs after 3 s. One that touches the board, tray or handle included, puffs at once, so nothing is ever left lying on the platform looking laid. Revised 2026-09-14 after the XR7 headset check; until then the throw ignored the room.
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
  - **Art** is the phone's train kit, used in place (X24).
- The whole board sits under one root scaled to the cell size: **0.06**, so one cell is 6 cm, unless the player has scaled it (4 to 9 cm, 5.3). No board code learns about metres, and `Core`'s layout maths is used unchanged.
- Tunnels, clue signs, the train and every other board mesh inherit the scale.

### 5.2 First placement

1. The app opens in passthrough. The first time, it asks for the headset's scene/spatial permission. If refused, placement falls back to floating.
2. A floating sign says "look at a table".
3. A ghost platform slides over detected horizontal surfaces, following the hand ray, controller ray or gaze. If no surface is detected (no room scan, permission refused), it floats at waist height in front of the player, and the sign suggests running the headset's space setup.
4. A pinch (or trigger) places it. The platform faces the player: its near edge is the one nearest them.
5. The placement is saved as an anchor. On later launches the board reappears where it was left. If the anchor cannot be found (a different room, anchor lost), step 2 runs again.

### 5.3 Moving and turning

- A **handle** moves the board, and stays in the hand while it does.
  - It is a **rounded L-shaped rail** wrapped round the platform's near corner opposite the tray (near-left for a right-handed player): a leg along the near edge, a quarter circle round the corner, and a leg up the side, each leg about 12 cm. It follows the corner of whatever is on show, a board or the map card. It is taken by a close pinch only: never a ray, a poke or the gaze.
  - **Touched, it opens out** along the near edge to the far corner, round it and up the far side, so there is an end of it for each hand. Left alone for a moment, it folds back to its corner. Added 2026-09-14.
  - **It takes both hands to move the board**, one at each end. One hand, pinching it or just near it (a fingertip or the pinch point within 2 cm), only turns it yellow, so the player sees it is there to take. Revised 2026-09-14 after the XR7 headset checks: a corner knob taken by one pinch kept moving the board by accident.
  - The board only follows once the hands have moved the rail about 3 cm or turned it about 12 degrees, so a stray pair of pinches does nothing.
  - Revised 2026-09-13 after the XR6 headset check: the full-length bar at the near edge kept catching rays and pinches aimed at the board, and moved it mid-level.
  - Grab and drag to move the board in any direction, height included.
  - Let go within 5 cm of a detected surface and the board settles onto it, shadows included. Anywhere else it floats where it was left.
  - To turn it about the vertical, steer with the two hands: the line between them turns the board.
  - **Spread or close the hands to scale the board**, about the point between them, from 4 to 9 cm a cell. Everything on the platform scales with it, the signboard included, and the size is saved with the anchor. Added 2026-09-14 after the XR7 headset check.
  - Revised 2026-09-13 after the XR5 headset check, which asked for height and a reliable turn.
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
- **Occlusion.** Real hands, arms and furniture in front of the board hide it, using the headset's environment depth:
  - The pieces are the Meta Quest: Occlusion feature and AR Foundation's `AROcclusionManager` and `ARShaderOcclusion`.
  - Only materials on an occlusion-aware shader are hidden, so every board, piece, sign, text and train material uses one.
  - Hand removal stays off, so the depth map covers the player's real hands too.
  - It needs the same spatial-data permission as surface placement (5.2).
  - Added 2026-09-13 after the XR4 headset check showed the board drawn over the player's body.

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
| Train run | The train runs the finished route in miniature, always into the exit tunnel before the arrival shows. There is no skip (revised 2026-09-14 after the XR7 headset check: a stray pinch after the winning drop cut it short). A hand on the running train shakes it and hurries it on for a moment, cartoon fashion, before it eases back to speed: the cars squash and stretch in a wave from the locomotive back, the train draws out, and it puffs steam (added 2026-09-14, made livelier after the first headset check) | — | — |
| Arrival (`Win`) | The solved board stays | Time, best, new-best flag, stars, and the verdict stamped on as on the phone: ON TIME, SLIGHT DELAY or DELAYED. Buttons: Next, Retry, Map | — |

- **Selecting on the platform.** Roundels are chosen by **poking with a finger or by ray/look-and-pinch**, the same two ways as grabbing (X16). A fingertip coming down onto a roundel's face presses it. It is read from the hand's poke pose rather than through XRI's poke, which did not press on the headset (revised 2026-09-14 after the XR7 headset check).
- **Signboard buttons** work by poke or ray. A poke is a fingertip pressed onto the button, read from the hand as on the roundels. The card leans back about its bottom edge to face the player's eyes, up to 50 degrees, as well as turning to them. Seen from above across the platform, an upright card took rays at a glancing angle (revised 2026-09-14 after the XR7 headset check).
- **Until the wrist menu (XR7 interim).** XR8 brings the wrist menu that carries Back to map and Back to network. Until then the signboard carries both: a LINE MAP button in Play, which pauses and saves on the way out, and a NETWORK button on the line map.
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
- **Level constraints.** The phone's `LevelCollectionTests` keep holding the tutorial level to constraints that also suit XR: solvable by propagation alone, both rail kinds on the guided path, and exactly one tutorial station.

---

## 8. Presentation

- **Visual language**: the same station signage as the phone. The colour tokens (`Paper`, `Ink`, `Led`, and the rest from `Docs/UIDesign.MD` section 6), the Barlow and Noto faces, roundels and the LED strip. The line colour tints the active line's markings.
- **Re-implemented under XR's own paths.** It gets its own palette file as its single re-skin point, and its own style sheets and elements. Phone files may be copied as a starting point (X20).
- **Board art**: the phone's train kit and track meshes, used in place, bent along `Core`'s `TrackCurve`, at the 0.06 board scale.
- **Signage**: the signboard, wrist menu and placement sign are **world-space UI Toolkit panels**.
- **Train run**: the train follows `Core`'s `TrackPath` by arc length and hides inside the tunnels, as on the phone.
- **Steam effects**: one particle system in two sizes (puff and burst) plus a whistle. Placement has a scale-in; a return plays a quick arc back to the origin.
- **Rendering on Quest**: XR's own render pipeline asset (`02.Graphics/XR/XR_RPAsset`), assigned through the Meta Quest build profile.
  - HDR off, post-processing off, 4× MSAA, Forward renderer.
  - Target 90 Hz on Quest 3, never below 72 Hz on Quest 3S.
  - Refresh rate and foveation are set per device.
- **Rendering on Vision Pro**: URP in Metal mode with foveated rendering. Post-processing only if it holds frame rate.
- **Comfort**: the world never moves the player. Nothing is attached to the head except, briefly, the first-launch "look at a table" sign.

## 9. Systems

| System | XR behaviour |
|---|---|
| Progression (lines, network reveal, unlocks) | From the shared `NetworkDefinition`, `NetworkLayout` and `GameFlow`, unchanged |
| Stars | Same `starTimes` thresholds per level, through the shared `ProgressTracker`. **Open item:** grab-and-drop may be slower or faster than tapping. XR6 measures a sample on device and may introduce a single XR scaling factor, held in XR code. The level assets are never forked. *XR6 sample, 2026-09-13 (Quest 3, one player, Ashgate):* 291 s by hand on the first try and 95 s on the second, against 180 s for three stars and 300 s for two. Too small to set a factor, so none is introduced yet; the stand-in keeps appending to `xr-star-sample.csv` |
| Timer | Shared `PlayTimer`; starts on the first grab (section 4.2) |
| Save / Continue | Shared `SaveJson`, `FileSaveStore` and `LevelProgress`: the same `save.json` format, local to each device. **No sync between phone and headset**, but the shared format keeps that possible later |
| Localisation | **XR's own** `XR` String Table in the project's existing `com.unity.localization` setup, same four locales (X24). Station and line names come from the shared assets. XR gets its own Japanese atlas bake covering its table plus those names |
| Audio | XR's own cue set, **spatialised**: piece cues from the piece, board cues from the board, UI cues from the signboard or wrist |
| Haptics | Controller haptics from XR's own cue-to-feel table. Bare hands feel nothing, so **nothing may depend on haptics alone** |

---

## 10. Technical architecture

### 10.1 Branch and folder layout (X2)

```
TrainSudoku/                        one Unity project
├── Assets/
│   ├── 01.Scripts/Core/            TrainSudoku.Core: shared, changed on main only
│   ├── 01.Scripts/Game/            TrainSudoku.Game: the phone; XR uses only its four level data types
│   ├── 01.Scripts/XR/              TrainSudoku.XR            (XR only)
│   ├── 01.Scripts/XR/Rules/        TrainSudoku.XR.Rules      (XR only, noEngineReferences)
│   ├── 01.Scripts/XR/Editor/       TrainSudoku.XR.Editor     (XR only)
│   ├── 02.Graphics/XR/             XR render pipeline and re-skinned art
│   ├── 03.Data/Levels/             level, line and network assets: shared, written on main only
│   ├── 03.Data/XR/                 XR data (cue tables, the XR String Table, ...)
│   ├── 99.Test/XR/EditMode/        TrainSudoku.XR.Tests.EditMode
│   ├── Samples/                    XRI and XR Hands samples (XR only)
│   ├── Scenes/XR.unity             the XR scene; SampleScene stays the phone's
│   ├── Settings/Build Profiles/    Meta Quest.asset (XR only) beside the phone's profiles
│   ├── XR/                         XR Plug-in Management and OpenXR settings (generated, XR only)
│   └── XRI/, CompositionLayers/    settings the XRI and Composition Layers packages generate (XR only)
└── Docs/                           PRD.md, UIDesign.MD, XR-PRD.md, XR-Agent.md
```

- `main` is the phone. `feat/MetaXR` is the XR edition: `main` plus the XR packages, the XR paths above and the Meta Quest build profile.
- The branch is **long-lived** and never merges back into `main`. It takes `main` in by merge (10.6).

### 10.2 Sharing by merge (X21, X25)

1. **XR adds files; it does not edit phone files.** Everything XR owns sits under the XR paths in 10.1.
2. **Quest settings ride on the Meta Quest build profile's overrides** (Player, Graphics, Quality, scene list), never on the global Android or iOS settings the phone owns. The project-wide files XR touches are `Packages/manifest.json`, `packages-lock.json`, and the config-object lines that XR Management, OpenXR and AR Foundation append to `ProjectSettings/EditorBuildSettings.asset`, plus at most one appended quality level if an override cannot hold the render pipeline.
3. **Shared changes land on `main` first.** That covers `Core`, the level assets and the four data types (`LevelDefinition`, `LevelCollection`, `LineDefinition`, `NetworkDefinition`). If XR needs one (making `TutorialCoach.TryFindMistake` public, say), it is made and committed on `main`, then merged in.
4. **XR references `TrainSudoku.Game` only for those four data types.** Moving them into their own `TrainSudoku.Data` assembly is an optional refactor, done on `main` if ever.
5. **The phone keeps working on the branch.** Its EditMode suite stays green and `SampleScene` plays in the Editor. Phone builds ship from `main`.
6. **Project infrastructure is reused, not copied.** `AddressablesBuildGuard` already covers every build, and `com.unity.localization` is already set up; XR adds its own table (section 9).

### 10.3 XR assemblies

| Assembly | Location | Depends on | Contents |
|---|---|---|---|
| `TrainSudoku.XR.Rules` | `Assets/01.Scripts/XR/Rules/` | Core only; `noEngineReferences: true` | `PieceDrop` (section 4.4), the XR tutorial coach (section 7), tray-docking and throw-threshold maths that need no engine |
| `TrainSudoku.XR` | `Assets/01.Scripts/XR/` | Core, `TrainSudoku.Game` (the four level data types only), XR.Rules, XR packages, Localization | XR shell and game manager, XR start-up, board placement and anchor, handle, board display (tiles, tunnels, clue signs, pieces, track bending), tray, the grab interface and its XRI implementation, train run, platform maps, signboard, wrist menu, steam effects, audio, haptics, palette |
| `TrainSudoku.XR.Editor` | `Assets/01.Scripts/XR/Editor/` | as needed | `XRFoundationSetup` (the XR render pipeline and scene, from Window > TrainSudoku > XR), later XR tools and the Japanese atlas bake for the XR table. The project's existing `AddressablesBuildGuard` already covers XR builds |
| `TrainSudoku.XR.Tests.EditMode` | `Assets/99.Test/XR/EditMode/` | XR.Rules, Core, `TrainSudoku.Game` | `PieceDropTests`, XR tutorial coach tests |

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

- **Quest packages** (on `feat/MetaXR` only): XR Plug-in Management, OpenXR, OpenXR Meta (`com.unity.xr.meta-openxr`), XR Interaction Toolkit, AR Foundation, XR Hands. URP, the Input System and `com.unity.localization` are already in the project.
- **Vision Pro packages** (XR12): visionOS XR (`com.unity.xr.visionos`), plus PolySpatial's spatial pointer input if Metal mode needs it for look-and-pinch.
- **Versions** are pinned exactly. As of 13 Sep 2026 each pin below exists in the Unity registry, and they agree with each other (OpenXR Meta 2.6.1 requires OpenXR 1.18.0, AR Foundation 6.6.x and Composition Layers 2.4.0):

  | Package | Version |
  |---|---|
  | OpenXR | 1.18.0 |
  | OpenXR Meta | 2.6.1 |
  | XRI | 3.7.0-pre.1 |
  | AR Foundation | 6.6.2 |
  | XR Hands | 1.10.0-pre.1 |
  | visionOS XR | 3.2.2 |

  The a6 editor bundles older pre-releases of these: OpenXR 1.18.0-pre.2, OpenXR Meta 2.6.0-pre.1, XRI 3.6.0, AR Foundation 6.6.0-pre.2 and XR Hands 1.9.0. If a pin fails to resolve or compile, fall back to the bundled version and record it here.
- **Not used**: Meta XR Core / All-in-One / Interaction SDK / MRUK (X12), and so not `metavr`'s Unity setup tool either, which installs Meta XR Core and an `OVRCameraRig`.
- **One XR scene**, `Assets/Scenes/XR.unity`. `SampleScene` stays the phone's.
- **The Meta Quest build profile** (`Assets/Settings/Build Profiles/Meta Quest.asset`, Android, Vulkan, ARM64, IL2CPP) carries every Quest setting as an override:
  - **Player**: bundle id, product name `Tsugi XR`, Vulkan only, IL2CPP, ARM64 only, ASTC, minimum API 32, **target API 34**. The Horizon Store requires API 34 of every app created since 1 Mar 2026.
  - **Graphics and Quality**: `02.Graphics/XR/XR_RPAsset`.
  - **Scene list**: `XR.unity` only.
  - Vision Pro gets a visionOS profile (XR12).
- **XR Plug-in Management** enables OpenXR on the Quest target only. iOS and Standalone stay off, so pressing Play on the phone's scene in the Editor never starts XR.
- **Bundle ids**: Quest `com.GorillaGonzalez.Tsugi.XR`. Whether Vision Pro shares the iPhone app's id (universal purchase) is decided at XR12.
- Addressables stays on Packed Mode; the project's `AddressablesBuildGuard` sees to it for every build.

### 10.6 Working across the two branches

- **Merging `main` in.** At the start of each XR milestone, and whenever XR needs a phone-side change, merge `main` into `feat/MetaXR`.
  - Conflicts should arise only in `Packages/manifest.json`, `packages-lock.json` and settings assets. Keep both sides and let Unity re-resolve.
  - A conflict anywhere else means a rule in 10.2 was broken.
- **Levels are written only on `main`**, with the phone's tools. XR never creates or modifies a level, line or network asset.
- **Both suites run on the branch.** A commit on `feat/MetaXR` needs the phone's EditMode suite and XR's to be green.
- **Switching branches in one checkout reimports.** For phone and XR work at the same time, give one of them a second git worktree, with its own `Library/`. The Unity MCP bridge drives whichever Editor is open.
- **Save format.** `SaveJson` lives in `Core`, so a save-format change is made on `main` and must keep both products' existing saves loading.

### 10.7 Editor upgrades

Both branches move to a newer 6.7 build together (X3). Back up first: Unity gives no guarantee that an alpha project upgrades cleanly.

1. The phone's EditMode suite passes on both branches, and XR's passes on `feat/MetaXR`.
2. An iOS phone build from `main` installs and plays.
3. `AddressablesBuildGuard` still holds after a domain reload and a build, on both branches.
4. The phone's Japanese font atlas rebakes correctly (`m_LineHeight / m_PointSize` ≈ 1.45), and so does XR's from XR10 on.
5. From XR2 on: a Quest build still starts passthrough with hands.

### 10.8 Testing

- **Automated.** `PieceDrop`, the XR coach and the shared Core are EditMode-tested.
- **In the Editor.** Iteration uses the XR Interaction Simulator (from the XRI samples, with a simulated-hands mode) and AR Foundation's XR Simulation (planes, anchors and ray casts, but no hands). Whether either works on this Editor (Windows or macOS) is unverified. They speed things up but do not verify a milestone.
- **On device.** A milestone is verified by a human on a Quest 3 and a Quest 3S (and a Vision Pro for XR12 onward).

---

## 11. Milestones

Same rules as the phone work: **no `git commit` until a milestone's *Verified by human* box is ticked**; one commit per milestone, message `XR<n>: <title>`, on `feat/MetaXR`. Every milestone also requires the phone's full suite to pass and `SampleScene` to play on the branch.

| # | Milestone | Done when |
|---|---|---|
| XR1 | ~~Shared package~~ | **Dropped 2026-09-13** with the X2 revision: one project, nothing to extract |
| XR2 | XR foundation | On `feat/MetaXR`: XR packages at the pinned versions; the Meta Quest build profile with its Player, Graphics/Quality and scene-list overrides; XR Plug-in Management with OpenXR on the Quest target only; `Scenes/XR.unity` with an AR Session and the XRI hands-and-controllers rig. The phone's full suite stays green and `SampleScene` still plays. A Quest 3 build shows passthrough, tracked hands and controllers, and its APK targets API 34 and declares `quest3\|quest3s`. `Docs/XR-Agent.md` written, `.gitignore` covers keystores |
| XR3 | `PieceDrop` | `XR.Rules` with EditMode tests covering every row of section 4.4, the move rollback, replacing a piece, fixed-piece refusal, two hands and lift-equals-erase |
| XR4 | Board display | A shared level loads into an XR-built board at 0.06 scale: tiles, tunnels, clue signs facing the player, fixed pieces, bent track from `TrackCurve`, validator colouring, train run along `TrackPath`. Placed in front of the player for now |
| XR5 | Board in the room | Surface-snapped placement, float fallback, handle move and turn, anchor persistence across launches, growth from the near edge, shadow catcher, environment depth occlusion of the board and train by the player's body and the room (5.6) |
| XR6 | Tray and grab | Six-slot tray with docking, handedness and moving between edges. Direct and distant grab, ghost, every release outcome, steam puff and throw, timer from the first grab. A level is playable start to finish. Star timing sampled (section 9) |
| XR7 | Flow on the platform | Network and line maps on the platform, roundel selection by poke and ray, signboard (masthead, station, arrival with its verdict stamp), the train run into the exit tunnel, save and continue, stars |
| XR8 | Wrist menu and pause | Back-of-wrist menu, pause semantics including focus loss and headset removal, all settings |
| XR9 | XR tutorial | The XR coach and tutorial per section 7; the phone's `LevelCollectionTests` still green |
| XR10 | Sound, haptics, language | XR String Table in all four locales, Japanese atlas bake checked on the signboard, spatialised cues, controller haptics |
| XR11 | Quest release readiness | 90 Hz on Quest 3, never below 72 on 3S, Meta's store requirements (VRCs) pass, store icons and metadata, Horizon Store build uploaded to a test channel |
| XR12 | Vision Pro port | visionOS profile in Metal mode, Mixed immersion. Grab by look-and-pinch and direct pinch through the grab interface; surface placement and anchors; world-space UI Toolkit verified. Full playthrough of a line on device |
| XR13 | Vision Pro release readiness | Frame rate, App Store requirements, bundle id decision, TestFlight build |

### 11.0 Progress

| # | Milestone | Verified by human |
|---|---|---|
| XR1 | ~~Shared package~~ (dropped) | — |
| XR2 | XR foundation | ☑ |
| XR3 | `PieceDrop` | ☑ |
| XR4 | Board display | ☑ |
| XR5 | Board in the room | ☑ |
| XR6 | Tray and grab | ☑ |
| XR7 | Flow on the platform | ☑ |
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
- ~~Two-hand board scaling.~~ Moved into v1 on 2026-09-14 (X6, 5.3).
- Pieces landing on real surfaces (room-mesh physics).
- Meta's own SDKs (until they support 6.7).
- Vision Pro RealityKit mode and the Shared Space.
- Quest 2 and Quest Pro.
- An "explain why it's red" overlay on the ghost.
- Sharing anything beyond Core and the level data (X1).

## 13. Risks

- **6.7 is an alpha, and several of its XR packages are pre-release** (XRI 3.7.0-pre.1, XR Hands 1.10.0-pre.1, OpenXR Meta 2.6.1). A later alpha may break a package, and a package update may break us. Mitigation: exact version pins, XR2 is early and small and verified before any gameplay depends on it, and every editor change runs the 10.7 checklist.
- **Merge drift.** `feat/MetaXR` lives apart from `main` for months while the phone keeps changing. Mitigation: the fork rules (10.2) keep XR's changes in its own paths and on the build profile, and `main` is merged in at the start of every milestone so conflicts stay small.
- **Shared-code drift.** Two teams (or two sessions) changing Core can break each other. Mitigation: shared changes land on `main` first (X25), both suites run on the branch, and XR-only rules stay in `XR.Rules` (X22).
- **Meta SDKs unavailable on 6.7.** We lose Meta's better hand grab and instant placement. Mitigation: the grab interface (10.4) lets either be added later without touching the rules or board.
- **Hand-tracking precision at 6 cm pieces.** Grabs at this size can miss. Mitigation: the tray spaces its slots generously, distant grab is always available, the ghost confirms the target before release, and XR6 tunes the hover band and snap on device.
- **Direct grab on Vision Pro is not documented for XRI.** Expect custom work in XR12 behind the same interface. Look-and-pinch is the fallback that is documented.
- **World-space UI Toolkit on Vision Pro Metal mode is unverified.** XR12 checks it first. If it fails, the signboard and wrist menu need a uGUI fallback on Vision Pro only.
- **Quest performance.** The board is many small meshes (bent track per piece, tiles, decor). Mitigation: XR's own render pipeline asset, and profiling in XR6, not XR11.
- **Editor upgrade side effects.** `JaFontAtlasBuilder` writes internal FontAsset fields by name, and `AddressablesBuildGuard` works around an a6 behaviour. Both are on the 10.7 checklist, for both branches.
- **Unity MCP drives one Editor.** It attaches to whichever Editor is open, and switching branches in one checkout forces a reimport. Phone and XR work at the same time needs a second git worktree (10.6); `Docs/XR-Agent.md` records how the Editor is driven.
