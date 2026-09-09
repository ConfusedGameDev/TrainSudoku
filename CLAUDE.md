# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

TrainSudoku: a portrait mobile puzzle game (mouse-compatible) built in Unity `6000.7.0a3` with URP and the new Input System. The agreed spec is `Docs/PRD.md` (rules, screens, data format, architecture, milestones M0–M10); read it before touching gameplay. `Plan.md` is the original brief and is superseded where they differ. The only scene is `Assets/Scenes/SampleScene.unity`.

Progress: every milestone M0 to M10 is done (Core rules, level editor, UI flow, 3D board, piece placement with bent track meshes, validator hookup, train run, JSON save with auto-save, level loader with a shipped level set). Next steps are content and polish. Assemblies:

| Assembly | Location | Notes |
|---|---|---|
| `TrainSudoku.Core` | `Assets/01.Scripts/Core/` | `noEngineReferences: true`. Board, legality, win check, path finder, solver (with node budget), `LevelText` parser/serialiser, `LevelAuthoring` edit helpers, `GameFlow` state machine, `PlayTimer`, `ProgressTracker`, `BoardLayout` (cell/tunnel/clue world positions), `CameraFit` (fit-to-board maths), `TrackCurve` (per-cell centre line: straight or quarter circle), `PlacementSession` (tap-to-place interaction on top of `Legality`), `TrackPath` (S-to-E route by arc length), `LevelProgress` (in-progress snapshot: player pieces plus elapsed time), `ISaveStore` with `InMemorySaveStore` and `FileSaveStore` (JSON via `SaveJson`) |
| `TrainSudoku.Game` | `Assets/01.Scripts/Game/` | References `UnityEngine.UI` and `Unity.InputSystem`. `GameManager` (scene entry point on the `Game` object in `SampleScene`), `UiBuilder` and one `PanelBase` subclass per state under `Ui/`, `IBoardView` + `BoardView` (tiles with colliders, tunnels, clue labels, pieces, markers, long-press erase) + `BoardCamera`, `TrackMeshBender`, `ProceduralTrackMesh`, `TrackAssets` under `Board/`, `TrainRunner` + `TrainAssets` under `Train/`, `AudioCue` hooks under `Audio/`, `LevelDefinition` and `LevelCollection` ScriptableObjects |
| `TrainSudoku.Editor` | `Assets/01.Scripts/Editor/` | Editor only. `LevelEditorWindow` (Window > TrainSudoku > Level Editor, UI Toolkit) and the `LevelDefinition` inspector button |
| `TrainSudoku.Tests.EditMode` | `Assets/99.Test/EditMode/` | NUnit tests for Core, the asset round-trip and `LevelCollectionTests` (every shipped level well formed and, within a node budget, uniquely solvable) |

## Asset layout

Numbered top-level folders under `Assets/`; put new files in the matching one:

| Folder | Holds |
|---|---|
| `00.Plugins` | Third-party code and packages not managed by UPM |
| `01.Scripts` | Runtime C# (add `Editor/` subfolders for editor-only code) |
| `02.Graphics` | Sprites, materials, shaders; URP pipeline assets live in `RenderPipeline/` |
| `03.Data` | ScriptableObjects and data assets: level definitions, `Input/InputSystem_Actions.inputactions` |
| `04.Prefabs` | Prefabs |
| `05.Audio` | Audio clips and mixers |
| `Resources`, `StreamingAssets` | Unity special folders (runtime-loaded content) |
| `99.Test` | EditMode and PlayMode test assemblies |
| `Scenes` | Scenes |

Empty folders hold a `.gitkeep` so git tracks them.

## Domain vocabulary (full rules in Docs/PRD.md section 3)

- **Board**: 6x6 grid. Each row and column carries a clue number 0–6 = how many track pieces it must contain.
- **Piece**: one per cell, exactly two connections. Track keys: `NS`, `EW`, `NW`, `NE`, `SW`, `SE`.
- **Start (S) / End (E)**: fixed per level, sit outside the grid. Fixed pieces (`K`) are pre-placed and unerasable.
- **Rule**: a piece adjacent to an existing track must connect to it.
- **Win**: continuous track from S to E and every row/column clue satisfied.
- **Level text format**: first line is column clues; each row is 6 cells then its row clue; `S`/`E` sit on the outer edge. Levels are authored in an editor window (`M1`) and saved as assets loaded by the Game Manager.

## Commands

Unity is at `C:\Program Files\Unity\Hub\Editor\6000.7.0a3\Editor\Unity.exe`. Close the Unity Editor before running any batchmode command, or it fails on the project lock.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.7.0a3\Editor\Unity.exe"

# Compile check (script errors land in the log, exit code non-zero on failure)
& $unity -batchmode -nographics -quit -projectPath . -logFile -

# Run all EditMode tests (use PlayMode for the other platform)
& $unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -

# Run a single test / fixture
& $unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.ClassName.MethodName" -testResults TestResults.xml -logFile -
```

Tests need the Unity Test Framework (already a dependency): put them under `Assets/99.Test/EditMode` or `Assets/99.Test/PlayMode` with their own `.asmdef` referencing the game assembly. There is no build script yet; a build requires adding a static method and invoking it with `-executeMethod`.

When the Editor is open (check `Temp/UnityLockfile`), batchmode is unavailable. Two fallbacks work from the shell: run the Core tests with a throwaway `dotnet test` project that compiles `Assets/01.Scripts/Core/**` plus the Core-only test files against NUnit 3, and compile-check Game and Editor code with a `netstandard2.1` project that references `C:\Program Files\Unity\Hub\Editor\6000.7.0a3\Editor\Data\Managed\UnityEngine\*.dll`. The open Editor also recompiles on focus; `Library/ScriptAssemblies/TrainSudoku.*.dll` timestamps show whether it succeeded. Tests that need `UnityEngine` still have to run in the Editor's Test Runner.

## Conventions

- Editor-only code lives in `Assets/01.Scripts/Editor/` under `TrainSudoku.Editor`; runtime code in `Game`; anything that can be plain C# goes in `Core` so it stays unit-testable without Unity.
- Board rules (placement legality, connection resolution, win check, solver) stay free of `UnityEngine`. The `Core` asmdef enforces this with `noEngineReferences`.
- Progress lives in `save.json` under `Application.persistentDataPath` (`GameManager.SaveFilePath`), written through on every change by `FileSaveStore` with a temp-file swap; a corrupt file is set aside as `.corrupt`. The format is `{"version":1,"bestTimes":{"level-id":seconds},"inProgress":{"level-id":{"elapsed":seconds,"pieces":[{"x":1,"y":0,"key":"NE"}]}}}`, read and written by the hand-rolled `SaveJson` so Core needs no UnityEngine; a malformed `inProgress` entry is skipped, never fatal. The Game inspector has "Delete save file" for testing.
- Auto-save: an unfinished level is snapshotted per level id by `GameFlow.SaveProgress` after every `BoardView.BoardChanged`, on entering Pause and in `OnApplicationQuit`; `OnApplicationPause(true)` calls `PauseGame` so backgrounding saves too. `StartLevel` and `NextLevel` pass the snapshot through `LevelStarted(index, progress)` to `IBoardView.Load`, which applies it with `Board.SetUnchecked` (replaying `TryPlace` in raster order can refuse a legal board) and restores the clock idle at the saved time, waiting for the first tap. `Retry` and `CompleteLevel` clear the snapshot. Level Select shows "Continue" for such levels.
- Level assets are saved by the editor window into `Assets/03.Data/Levels/`; the level `id` string is the save-file identity and must not change after release. `LevelCollection.asset` in that folder is what the `GameManager` loads; it is ordered by difficulty (solver node count is the rough proxy): simple, Corner, First, PlanExample, Zigzag, Crossing, Spiral, LongHaul (8x8), Hard (12x11). All but Hard are solver-verified unique; Hard is too large for the backtracking solver to finish (billions of nodes without a result), so its solvability is unproven.
- The uGUI screens are built in code by `UiBuilder` (legacy `Text`, portrait reference 1080x1920, canvas scaler match 0.5) and baked into the scene: select the `Game` object and click **Generate Scene Objects** (also in the component context menu) after changing any `BuildWidgets` code, then save the scene. At runtime `GameManager.Awake` only generates when the scene has nothing generated. Each panel splits into `BuildWidgets` (creates widgets into `[SerializeField]` fields; runs in the Editor) and `Wire` (attaches click handlers; runs at runtime, because lambdas are not serialized). Every button plays an `AudioCue`; the cue-to-clip mapping is `Assets/03.Data/Audio/UiAudioCues.asset`.
- Never let a fixed-height element in a `VerticalLayoutGroup` report a flexible height: `UiBuilder.Row`, `Label` and `Button` set `flexibleHeight = 0` because a layout group with `childForceExpand` otherwise reports flexible 1 and competes with spacers.
- `GameFlow` in Core owns all screen transitions and throws on invalid ones; panels only call it, never each other. The board talks to the flow through `IBoardView` (`Interacted`, `Completed`).
- World layout: one cell is one unit, the board is centred on the origin, X grows east and Z grows north, so row 0 is the far row. Positions come from `BoardLayout`, never from ad-hoc arithmetic. The board is built from primitives at load time with materials cloned from the pipeline default; there are no board prefabs or material assets yet.
- Track geometry is analytic, not the Splines package: `TrackCurve` in Core gives position and tangent for each key (straights through the centre, quarter circles of radius 0.5 around the shared corner), and `TrackMeshBender` maps a straight segment mesh (local Z along the track, one cell long) onto it, cached per key. The segment comes from `Assets/03.Data/Board/TrackAssets.asset`; while its mesh is empty `ProceduralTrackMesh` supplies rails and sleepers. The kit's `spline-track.fbx`, `spline-segment.fbx` and `railroad-straight.fbx` are Read/Write enabled for this. `TrackPath` concatenates the `TrackCurve`s from S to E with a one-cell straight into each tunnel; `TrainRunner` samples it by arc length (2.5 cells per second, cars 0.9 apart, hidden inside the tunnels) and raises `Finished`, which `GameManager` turns into `FinishTrainRun`. Models come from `Assets/03.Data/Board/TrainAssets.asset` with boxy placeholders while empty.
- Interaction (PRD section 4) is decided in Core by `PlacementSession`; `BoardView` only renders markers and pieces from its state and reads the pointer (mouse or primary touch) itself. Auto-place happens when one key is legal on selection and also when the first chosen side leaves a single legal partner.
- The main camera carries a `BoardCamera` (added at runtime) that solves `CameraFit` for a world-space box and re-fits when the aspect, projection mode or field of view changes. `BoardView.ContentBounds` supplies the box: the grid plus the tunnel ring and `BoardLayout.Height` as a minimum (so the train and markers always fit), grown by the renderer bounds of everything built (tunnel mouths and letters, clue labels); the camera aims at the centre of that box, so one-sided clue labels do not push the board off centre. It handles both projections: perspective moves the camera back along the pitched view direction, orthographic grows the orthographic size (the baked scene camera is orthographic). It builds the projection matrix itself and shifts it down by `hudFraction` so the board is centred in the strip below the HUD; `Fit(aspect)` is public so tools can check other aspects. The Play HUD is transparent apart from a small `||` pause button, the level name and the clock along the top edge. Every placement and erase runs `WinChecker` in `BoardView.Validate`: satisfied clues turn green, over-filled lines red, and a winning board raises `Completed` so `GameFlow.CompleteLevel` fires. Loading never announces a win. The pause menu keeps an editor-only "Debug: win" button for testing the screens.
- Input goes through `Assets/03.Data/Input/InputSystem_Actions.inputactions` (maps `Player` and `UI`); the legacy Input Manager is disabled in ProjectSettings.
- `*.csproj`, `*.slnx`, `Library/`, `Logs/`, `UserSettings/` are gitignored and regenerated by Unity. Always commit `.meta` files alongside new assets.
- `ProjectSettings.asset` currently has `defaultScreenOrientation: 4` (auto-rotate); the spec calls for portrait, so lock it when building for mobile.
