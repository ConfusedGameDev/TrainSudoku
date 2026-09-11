# Tsugi — Product Requirements Document

(Shipped as **Tsugi**; the codebase, its namespaces and the repo keep the working name TrainSudoku.)

Status: agreed 2026-09-09. Supersedes `Plan.md` (kept as the original brief).

## 1. Vision

A portrait puzzle game in the spirit of sudoku. The player lays railway track on a small grid so that a train can travel from an entrance tunnel to an exit tunnel, while every row and column contains exactly the number of track pieces its clue demands. Winning plays a short animation of a train running the finished route.

## 2. Platforms

| Priority | Platform | Notes |
|---|---|---|
| 1 | PC (Windows) | Any window size; board fits the shorter axis, 9:16 is the design reference. Mouse input. |
| 1 | iOS | Portrait locked. Xcode project exported from Windows; device build and testing require a Mac. |
| 2 | Android | Later. Nothing in the design may block it. |

Rendering uses URP with the PC and Mobile render pipeline assets per platform.

## 3. Game rules

### 3.1 Board
- Rectangular grid, default 6x6. Width and height are stored per level; nothing may hardcode 6.
- Every row and every column carries a **clue**: an integer 0..N stating how many cells in that line must hold a track piece.
- The **entrance (S)** and **exit (E)** are tunnel mouths placed outside the grid on any of the four edges, each adjacent to exactly one perimeter cell. One S and one E per level.

### 3.2 Pieces
- A cell holds at most one piece. A piece has exactly two **connections**, each pointing to one of the four sides.
- The six **keys**: `NS`, `EW`, `NW`, `NE`, `SW`, `SE`.
- **Fixed pieces** are given by the level. They are locked: cannot be erased or replaced, and are visually distinct.
- **Player pieces** are placed by the player and can be erased.

### 3.3 Placement legality
For a cell being filled, each of its four directions is classified:

| Class | Condition | Effect |
|---|---|---|
| **Forced** | The neighbouring cell holds a piece with a connection pointing at this cell, or that side is the S/E tunnel | The new piece must include this direction |
| **Forbidden** | The neighbouring cell holds a piece that does not point at this cell, or the side is the board edge with no tunnel | The new piece may not include this direction |
| **Open** | The neighbouring cell is empty | May be chosen |

A piece is legal when both of its connections are forced or open and every forced direction is included. Two forced directions fully determine the piece. Three or more forced directions make the cell unfillable; the UI shows no options.

### 3.4 Win condition
All three must hold after a placement:
1. A continuous track exists from S to E.
2. Every row and column clue is satisfied exactly.
3. Every placed piece (fixed or player) has both connections matched by a neighbour piece or a tunnel. No open ends.

Closed loops that are not on the S→E path are legal if condition 3 holds; level designers decide whether to use them.

## 4. Player interaction

1. **Tap an empty cell**: it becomes selected, and its four **neighbours** are marked with how that side reads — green where the player may connect (3.3 open), red where they may not (3.3 forbidden, which includes a board edge with no tunnel), the line colour where they must (3.3 forced). A side that leaves the board has no neighbouring cell to mark, so it shows a marker at the cell's edge instead: that is where the S/E tunnel and a plain wall appear.
2. **Tap a green or line-coloured neighbour**: first connection chosen. It turns the selected cell's own yellow, the neighbours that can still partner it stay marked, and the rest turn red.
3. **Tap a second one**: piece is placed with a short scale-in animation. Board validation runs.
4. **Auto-place**: if exactly two directions are legal when the cell is selected, the piece is placed immediately with no marking step.
5. **Cancel**: tapping the selected cell again or anywhere outside the grid clears the selection. Tapping a red neighbour is not a connection, so it simply moves the selection there.
6. **Erase**: long press (0.5 s, with a fill-ring indicator) on a player piece removes it. Long press on a fixed piece does nothing beyond a small shake. Mouse uses the same hold on the left button.
7. Tapping a placed piece (short tap) does nothing — unless it is a marked neighbour of the selected cell, where it is a connection as in step 2.

Input is raycast against per-cell colliders through the Input System, so touch and mouse share one path.

The **entrance and exit cells never start empty**: both are forced towards their tunnel, so each ships with a fixed piece taken from the level's own solution (baked by the editor's Bake Tunnel Pieces, PRD 9.1). A puzzle therefore opens anchored at both ends.

## 5. Screens and flow

Single scene, driven by a state machine with one uGUI panel per state.

```
Main Menu -> Level Select -> Play <-> Pause
                               |
                               v
                          Train run -> Win screen -> Next / Retry / Menu
```

- **Main Menu**: Play, Quit (PC only).
- **Level Select**: ordered list from the `LevelCollection`. Level N is unlocked when level N-1 has a saved best time. Locked entries are greyed with a lock icon. Completed levels show their best time and can be replayed. An editor-only "unlock all" toggle exists for testing.
- **Play**: board, clues, timer, pause button.
- **Pause**: Resume, Retry, Exit. Retry clears player pieces and resets the timer. Exit returns to Level Select. Timer stops while paused.
- **Train run**: plays automatically on win. A tap skips to the Win screen.
- **Win screen**: this time, best time, "New best" flag. Buttons: Next (disabled on the last level), Retry, Menu (to Level Select).

## 6. Timer and high score

- Timer starts on the first tap on the board, not on load. Pauses in the pause menu. Stops on win. Retry resets it.
- On win, if no best time exists or the new time is lower, it is saved as the best time for that level.
- Best times and unlock state live in one JSON save file under `Application.persistentDataPath`, accessed through an `ISaveStore` interface with an in-memory implementation for tests.
- Level identity is a string `id` field on the level asset, so renaming files never loses progress.
- Auto-save: leaving a level unfinished (pause and exit, backgrounding the app, quitting) keeps its player pieces and elapsed time in the same save file, per level. Selecting the level again continues from that state, with the clock waiting for the first tap. Retry and winning discard it. Level Select marks such levels "Continue".

## 7. Presentation

- **3D top-down**. Perspective camera pitched 55–65°, fit-to-width computed from board size so any grid fills the screen.
- **Cell size** is 1 world unit. All art is authored to that scale.
- **Art kit**: the train model pack in `Assets/02.Graphics/Train Assets/` (single `colormap.png` texture). Cell size must match the kit's straight segment length; verify in Unity, expected 1 unit.
- **Track meshes** are produced by bending the kit's `spline-track.fbx` (or `spline-segment.fbx`) along a per-cell spline with a custom mesh deformer on top of the Unity Splines package. One asset serves all six keys. If bend quality disappoints, the fallback is the kit's `railroad-straight.fbx` and `railroad-curve.fbx` rotated per key; nothing else changes.
- **Per-cell spline geometry**: knots at side midpoints so neighbours join with matching tangents. Straights are a line through the centre. Curves are a quarter circle of radius 0.5 centred on the cell corner.
- **Train**: `train-locomotive-a.fbx` plus two carriages from the kit, chosen per level later if desired.
- **Tunnels** sit one cell outside the perimeter facing the adjacent cell. The kit has no tunnel model, so a primitive placeholder under `Assets/02.Graphics/Placeholders/` is used until one is sourced.
- **Board tiles** and markers are primitives from the same placeholder folder.

## 8. Train run

- On win, one path spline is built by concatenating the per-cell splines from S to E, extended one cell into each tunnel so the train fully disappears.
- Locomotive plus two wagons, each on its own `SplineAnimate` with a distance offset. Speed about 0.4 s per cell. Static camera.

## 9. Level data

### 9.1 Assets
- `LevelDefinition` (ScriptableObject) in `Assets/03.Data/Levels/`: `id`, `displayName`, `width`, `height`, `columnClues[]`, `rowClues[]`, `fixedPieces[] {x, y, key}`, `entrance {side, index}`, `exit {side, index}`.
- `LevelCollection` (ScriptableObject): ordered list of `LevelDefinition`. The Game Manager loads from it.
- The runtime never parses text; text is an authoring format only.

### 9.2 Text format
Whitespace-separated tokens. Alignment is cosmetic.

```
name: First Steps
  . . . S . .        <- optional top edge line: . / S / E per column
  2 3 1 4 2 1        <- column clues, W integers
. . . . . . . 3      <- row line: [S|E] W cells clue [S|E]
. NE . . . . 2
. . . . SW . 4 E
. . . . . . 1
. . . . . . 2
. . . . . . 1
  . . . . . .        <- optional bottom edge line
```

- `name:` line is optional.
- Edge lines are optional and recognised because they contain no digits.
- Row lines may start with `S`/`E` (left edge) or end with one (right edge).
- A cell is `.` (empty) or a key literal (`NS`, `EW`, `NW`, `NE`, `SW`, `SE`) for a fixed piece.
- Width and height are inferred from the token counts.

## 10. Level editor (Unity Editor window)

Built with UI Toolkit, editor assembly only.

- Grid view to set width/height, place or clear fixed pieces by key, set clues, place S and E on the perimeter.
- **Import from text** / **Export to text** using the format above.
- **Author by solution**: draw a complete track, the editor derives all clues, then the designer removes pieces to make the puzzle.
- **Validation**, live as you edit: clues consistent with fixed pieces, S and E present, and a solver result of `0`, `1`, or `2+` solutions. Zero shows a red banner. More than one warns but saving is allowed.
- Saves to a `LevelDefinition` asset and can add it to a `LevelCollection`.

## 11. Technical architecture

| Assembly | Location | Depends on | Contents |
|---|---|---|---|
| `TrainSudoku.Core` | `Assets/01.Scripts/Core/` | nothing from `UnityEngine` | Board, Piece, Key, Direction, placement legality, win check, path finder, solver, text parser/serialiser, `ISaveStore` |
| `TrainSudoku.Game` | `Assets/01.Scripts/Game/` | Core, Unity, Splines, uGUI | Game Manager, state machine, board view, input, spline builder, mesh deformer, train, UI panels, JSON save store |
| `TrainSudoku.Editor` | `Assets/01.Scripts/Editor/` | Core, Game | Level editor window |
| `TrainSudoku.Tests.EditMode` | `Assets/99.Test/EditMode/` | Core | Rule, solver, parser tests |
| `TrainSudoku.Tests.PlayMode` | `Assets/99.Test/PlayMode/` | Core, Game | Only if a feature cannot be tested in EditMode |

- Packages to add: `com.unity.splines`.
- Input through `Assets/03.Data/Input/InputSystem_Actions.inputactions`; the legacy Input Manager stays disabled.
- Audio: a UI sound pack exists in `Assets/05.Audio/UI/`. An `AudioCue` hook fires on place, erase, error, win, and train start, and on UI button presses. UI cues are wired from the pack in M2; game and train sounds are a later content pass.

## 12. Milestones

Each milestone ends with a commit on `main`.

| # | Milestone | Done when |
|---|---|---|
| M0 | Core rules model | Core assembly with EditMode tests covering legality classes, win check, parser round-trip, and solver on the Plan.md example. No `UnityEngine` reference. |
| M1 | Level editor | Window imports/exports text, validates, shows solution count, saves `LevelDefinition`. |
| M2 | User interface | All panels and the state machine navigate end to end with a stub board. |
| M3 | Player field | Board, clues, tunnels, and cell colliders render from a `LevelDefinition` at any size. Camera fits. |
| M4 | Pieces and splines | Selecting and placing produces a bent track mesh from the per-cell spline. Erase works. |
| M7 | Validation hookup | Every placement runs the Core validator; clues colour when satisfied; win state fires. |
| M8 | Train run | Path spline built from the solved board; locomotive and wagons run S to E; skip works. |
| M9 | High score | JSON save store, best time, new-best flag, level unlocking. |
| M10 | Level loader | `LevelCollection`, Level Select populated, five hand-authored levels verified unique. |

M5 and M6 from the original brief (merge and split splines) are folded into M8: the path spline is built once at win time, so per-piece splines never need merging or splitting.

## 13. Out of scope for this version

Game and train sound effects, music, Android release, localisation, hints, undo, procedural level generation, analytics, monetisation.

## 14. Risks

- **Unity 6000.7.0a3 is an alpha.** Package versions (Splines, Input System) may need pinning; if the editor proves unstable, move to the latest 6000.x LTS installed on the machine.
- **Mesh bending quality** on curves. Mitigated by the fallback to pre-modelled pieces.
- **iOS builds need a Mac.** Export is set up from day one; device testing waits for hardware.
