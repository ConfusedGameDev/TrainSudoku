# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

TrainSudoku: a portrait mobile puzzle game (mouse-compatible) built in Unity `6000.7.0a6` with URP and the new Input System. The agreed spec is `Docs/PRD.md` (rules, screens, data format, architecture, milestones M0–M10); read it before touching gameplay. `Plan.md` is the original brief and is superseded where they differ. The only scene is `Assets/Scenes/SampleScene.unity`.

Progress: every milestone M0 to M10 is done (Core rules, level editor, UI flow, 3D board, piece placement with bent track meshes, validator hookup, train run, JSON save with auto-save, level loader with a shipped level set).

The current work is **M11-M20**, the Station UI rebuild: uGUI screens out, UI Toolkit in, plus localisation, star ratings and a line/network progression model. `Docs/UIDesign.MD` is the work order for it and is authoritative for that work — its section 0 holds 21 closed decisions, and its section 11 the milestones and their progress table. **Do not `git commit` until a milestone's *Verified by human* box is ticked**; one commit per milestone, message `M<n>: <title>`. Assemblies:

| Assembly | Location | Notes |
|---|---|---|
| `TrainSudoku.Core` | `Assets/01.Scripts/Core/` | `noEngineReferences: true`. Board, legality, win check, path finder, solver (with node budget), `LevelText` parser/serialiser, `LevelAuthoring` edit helpers, `GameFlow` state machine, `PlayTimer`, `ProgressTracker`, `BoardLayout` (cell/tunnel/clue world positions), `CameraFit` (fit-to-board maths), `TrackCurve` (per-cell centre line: straight or quarter circle), `PlacementSession` (tap-to-place interaction on top of `Legality`), `TrackPath` (S-to-E route by arc length), `LevelProgress` (in-progress snapshot: player pieces plus elapsed time), `ISaveStore` with `InMemorySaveStore` and `FileSaveStore` (JSON via `SaveJson`) |
| `TrainSudoku.Game` | `Assets/01.Scripts/Game/` | References `UnityEngine.UI`, `Unity.InputSystem` and `Unity.Localization`. `GameManager` (scene entry point on the `Game` object in `SampleScene`), `Palette` (all colour and font tokens), `Motion` (the unscaled tween every screen animation goes through) and `SceneObjects` (`Destroy`/`Clear`) plus `UiShell`, `Signage`, the `Elements/` (roundel, LED strip, maps, icons) and one `UiScreen` subclass per screen under `Ui/`, `IBoardView` + `BoardView` (tiles with colliders, tunnels, clue labels, pieces, markers, long-press erase, platform decals) + `BoardCamera`, `TrackMeshBender`, `ProceduralTrackMesh`, `ProceduralBoardMesh`, `PopScale`, `TrackAssets` under `Board/`, `TrainRunner` + `TrainAssets` under `Train/`, `AudioCue` hooks under `Audio/`, `LevelDefinition` and `LevelCollection` ScriptableObjects |
| `TrainSudoku.Editor` | `Assets/01.Scripts/Editor/` | Editor only. `LevelEditorWindow` (Window > TrainSudoku > Level Editor, UI Toolkit), `LineMapEditorWindow` (Window > TrainSudoku > Line Map Editor: drag map nodes on the 45° grid in `MapGrid`, check them with `LineMapValidation`, preview through the runtime `LineMapElement`) and the `LevelDefinition` and `LineDefinition` inspector buttons |
| `TrainSudoku.Tests.EditMode` | `Assets/99.Test/EditMode/` | NUnit tests for Core, the asset round-trip and `LevelCollectionTests` (every shipped level well formed and, within a node budget, uniquely solvable) |

## Asset layout

Numbered top-level folders under `Assets/`; put new files in the matching one:

| Folder | Holds |
|---|---|
| `00.Plugins` | Third-party code and packages not managed by UPM |
| `01.Scripts` | Runtime C# (add `Editor/` subfolders for editor-only code) |
| `02.Graphics` | Sprites, materials, shaders; URP pipeline assets live in `RenderPipeline/` |
| `03.Data` | ScriptableObjects and data assets: level definitions, `Input/InputSystem_Actions.inputactions`, `Localization/` (settings, locales, the `UI` String Table and `Fonts` Asset Table), `Ui/` (`PanelSettings.asset` and the runtime theme), `AddressableAssetsData/` |
| `04.Prefabs` | Prefabs |
| `05.Audio` | Audio clips and mixers |
| `Resources`, `StreamingAssets` | Unity special folders (runtime-loaded content) |
| `99.Test` | EditMode and PlayMode test assemblies |
| `Scenes` | Scenes |
| `AddressableAssetsData` | **Not ours.** Two files holding Addressables' `DefaultObject.asset`, whose path is a hard-coded const in the package. The real settings live under `03.Data/` |

Empty folders hold a `.gitkeep` so git tracks them.

## Domain vocabulary (full rules in Docs/PRD.md section 3)

- **Board**: 6x6 grid. Each row and column carries a clue number 0–6 = how many track pieces it must contain.
- **Piece**: one per cell, exactly two connections. Track keys: `NS`, `EW`, `NW`, `NE`, `SW`, `SE`.
- **Start (S) / End (E)**: fixed per level, sit outside the grid. Fixed pieces (`K`) are pre-placed and unerasable.
- **Rule**: a piece adjacent to an existing track must connect to it.
- **Win**: continuous track from S to E and every row/column clue satisfied.
- **Level text format**: first line is column clues; each row is 6 cells then its row clue; `S`/`E` sit on the outer edge. Levels are authored in an editor window (`M1`) and saved as assets loaded by the Game Manager.

## Commands

Development happens on **macOS** with the Unity Editor at `/Applications/Unity/Hub/Editor/6000.7.0a6/Unity.app`,
and the Editor is normally **left open** (check `Temp/UnityLockfile`). That makes `-batchmode` unavailable — it
fails on the project lock — so the primary way to drive Unity is the **Unity MCP tools against the live Editor**:

- `Unity_RunCommand` compiles and runs a C# snippet in the Editor. The class must be named `CommandScript` and be
  `internal`. Two traps: the wrapper re-emits your code inside its own namespace, so **declare helper classes at
  top level, never nested inside `CommandScript`** (a nested one gets hoisted *and* duplicated), and fully qualify
  `UnityEditor.Compilation.CompilationPipeline` because the injected namespace shadows it. `System.Reflection` is
  blocked. Long-running work (a test run, a UPM request) must write its result to a file the shell then polls —
  the call returns before the work finishes.
- `Unity_GetConsoleLogs` with `logTypes: "Error"` is the compile check. Note the project's analyzer emits
  `UAL0010`/`UAL0013` static-cleanup warnings on every class with a static field; they are pre-existing noise.
- `Unity_Camera_Capture` does **not** work in play mode.

To run the EditMode suite, call `TestRunnerApi.Execute` from `Unity_RunCommand` with an `ICallbacks` sink that
writes the counts to a file, then poll that file. A full run is ~235 tests and takes about 20 seconds.

Batchmode still works if the Editor is closed:

```bash
unity="/Applications/Unity/Hub/Editor/6000.7.0a6/Unity.app/Contents/MacOS/Unity"

# Compile check (script errors land in the log, exit code non-zero on failure)
"$unity" -batchmode -nographics -quit -projectPath . -logFile -

# Run all EditMode tests (use PlayMode for the other platform)
"$unity" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -

# Run a single test / fixture
"$unity" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.ClassName.MethodName" -testResults TestResults.xml -logFile -
```

Tests need the Unity Test Framework (already a dependency): put them under `Assets/99.Test/EditMode` or `Assets/99.Test/PlayMode` with their own `.asmdef` referencing the game assembly. There is no build script yet; a build requires adding a static method and invoking it with `-executeMethod`.

## Conventions

- Editor-only code lives in `Assets/01.Scripts/Editor/` under `TrainSudoku.Editor`; runtime code in `Game`; anything that can be plain C# goes in `Core` so it stays unit-testable without Unity.
- Board rules (placement legality, connection resolution, win check, solver) stay free of `UnityEngine`. The `Core` asmdef enforces this with `noEngineReferences`.
- Every colour and font comes from `Ui/Palette.cs`; a re-skin is an edit to that one file. It holds two disjoint sets that must not be mixed: the station-signage tokens from `Docs/UIDesign.MD` section 6 (`Paper`, `Ink`, `InkDim`, `Closed`, `Led`, `LedGround`, `Warn`, `Stop`), and the platform — the board's own values, re-tinted to the same palette at M19 (`BoardBackground` ballast, `Concrete`/`ConcreteAlt` slabs, `Steel` rails, `Success`, `ClueExceeded`, `ClueText`, `Font`). `BoardMaterials` names nothing itself; it reads those tokens, and `BoardMaterials.SetLineColour` (called from `GameManager.ApplyState` beside `UiShell.SetLineColour`) tints the two things that carry the line: fixed pieces and the forced marker. **The line colour is deliberately absent** — it is a runtime value published as the USS variable `--line-current` from the active line, never a constant. Scene teardown (`Destroy`, `Clear`) lives in `Ui/SceneObjects.cs`. Nothing under `Board/` or `Train/` may reference `UiBuilder`: that class is the uGUI widget factory and is deleted at M16.
- Localisation is `com.unity.localization`, bundled **BuiltIn at 1.5.12** with the a6 editor — the only version it offers, so the manifest pin must match. Locales are `en` (source), `ja`, `es`, `fr`; UI copy goes in the one `UI` String Table under `03.Data/Localization/`, keyed by screen (`play.pause`, `arrival.on_time`). One language per build, no bilingual pairs. Station and line names are untranslated proper nouns held on the assets, not in the table. Prefer `StringDatabase.GetLocalizedString`; the async overload returns an Addressables `AsyncOperationHandle`, which would drag `Unity.ResourceManager` into the Game assembly's references.
- Progress lives in `save.json` under `Application.persistentDataPath` (`GameManager.SaveFilePath`), written through on every change by `FileSaveStore` with a temp-file swap; a corrupt file is set aside as `.corrupt`. The format is `{"version":1,"bestTimes":{"level-id":seconds},"inProgress":{"level-id":{"elapsed":seconds,"pieces":[{"x":1,"y":0,"key":"NE"}]}}}`, read and written by the hand-rolled `SaveJson` so Core needs no UnityEngine; a malformed `inProgress` entry is skipped, never fatal. The Game inspector has "Delete save file" for testing.
- Auto-save: an unfinished level is snapshotted per level id by `GameFlow.SaveProgress` after every `BoardView.BoardChanged`, on entering Pause and in `OnApplicationQuit`; `OnApplicationPause(true)` calls `PauseGame` so backgrounding saves too. `StartLevel` and `NextLevel` pass the snapshot through `LevelStarted(index, progress)` to `IBoardView.Load`, which applies it with `Board.SetUnchecked` (replaying `TryPlace` in raster order can refuse a legal board) and restores the clock idle at the saved time, waiting for the first tap. `Retry` and `CompleteLevel` clear the snapshot. Level Select shows "Continue" for such levels.
- Level assets are saved by the editor window into `Assets/03.Data/Levels/`; the level `id` string is the save-file identity and must not change after release. `LevelCollection.asset` in that folder is what the `GameManager` loads; it is ordered by difficulty (solver node count is the rough proxy): simple, Corner, First, PlanExample, Zigzag, Crossing, Spiral, LongHaul (8x8), Hard (12x11). All but Hard are solver-verified unique; Hard is too large for the backtracking solver to finish (billions of nodes without a result), so its solvability is unproven.
- The uGUI screens are built in code by `UiBuilder` (legacy `Text`, portrait reference 1080x1920, canvas scaler match 0.5) and baked into the scene: select the `Game` object and click **Generate Scene Objects** (also in the component context menu) after changing any `BuildWidgets` code, then save the scene. At runtime `GameManager.Awake` only generates when the scene has nothing generated. Each panel splits into `BuildWidgets` (creates widgets into `[SerializeField]` fields; runs in the Editor) and `Wire` (attaches click handlers; runs at runtime, because lambdas are not serialized). Every button plays an `AudioCue`; the cue-to-clip mapping is `Assets/03.Data/Audio/UiAudioCues.asset`.
- Never let a fixed-height element in a `VerticalLayoutGroup` report a flexible height: `UiBuilder.Row`, `Label` and `Button` set `flexibleHeight = 0` because a layout group with `childForceExpand` otherwise reports flexible 1 and competes with spacers.
- `GameFlow` in Core owns all screen transitions and throws on invalid ones; panels only call it, never each other. The board talks to the flow through `IBoardView` (`Interacted`, `Completed`).
- World layout: one cell is one unit, the board is centred on the origin, X grows east and Z grows north, so row 0 is the far row. Positions come from `BoardLayout`, never from ad-hoc arithmetic. The board is built from primitives at load time with materials cloned from the pipeline default; there are no board prefabs or material assets yet.
- Board geometry beyond the track is generated too, in `ProceduralBoardMesh`: the chamfered platform slab, the swept-arch tunnel portal and the erase ring. **Every mesh there spans the same extents as the primitive it replaces**, so `ContentBounds` cannot shift and M17's camera fit stays put — check it before changing one. Generated meshes must wind their triangles the way the built-in primitives do (`cross(p1 - p0, p2 - p0)` points out of the front face); getting it backwards culls the face silently, which is what had happened to `ProceduralTrackMesh` until M19.
- Motion (`Docs/UIDesign.MD` section 9) is **always on unscaled time**, so it survives the pause. Screens go through `Ui/Motion.cs`, which drives a 0-to-1 value from `Time.realtimeSinceStartup`; world-space objects use coroutines on `Time.unscaledDeltaTime` (`PieceView`, `PopScale`, `BoardMarker`). `UiScreen.SetVisible` runs the 260 ms screen change; `PlayScreen` opts out through `Animates` because it is the board's frame and its layout is what the camera insets are measured from.
- **A `UIDocument` root is `PickingMode.Ignore`, and the board depends on it.** That is what lets a tap fall through the full-screen transparent Play screen to the 3D board: `BoardView.BeginPress` asks `EventSystem.IsPointerOverGameObject` first, so a root that picks answers yes everywhere and silently swallows every placement — no error, just a board that will not take a piece. Never assign `pickingMode` on a screen root; `UiScreen` remembers each root's own value and puts that back. To stop a screen taking taps, disable the subtree (`SetEnabled(false)`), which is what the exit transition does.
- Track geometry is analytic, not the Splines package: `TrackCurve` in Core gives position and tangent for each key (straights through the centre, quarter circles of radius 0.5 around the shared corner), and `TrackMeshBender` maps a straight segment mesh (local Z along the track, one cell long) onto it, cached per key and dropped by `BoardView.Clear`. The Splines package would not help: it ships no mesh deformer, only `SplineExtrude`/`SplineInstantiate`/`SplineAnimate`, which generate geometry rather than bend art.
- Bending is a per-vertex warp, so **curve smoothness comes entirely from the source's rings along Z**. The kit rails carry only three, which bend into a two-facet polyline; `TrackMeshResampler` therefore slices the source into `curveSlices` bands first (16 by default, straights are never cut). Along-track lengths scale by the Jacobian `k = (Length / depth) * (1 + curvature * x)`, so normals divide `n.z` by `k` and tangents multiply by it. `k` hits zero at `|x| = 0.5`: art that wide folds its inner edge onto the arc centre. Sleepers are therefore bent across their own stretch of the curve only while `sleeperWidthScale` keeps them clear of that limit, and are dropped in rigidly otherwise.
- The two kit meshes play different roles: `spline-track.fbx` is the **rails only**, `spline-segment.fbx` is **one cross tie** repeated along the piece. `TrackAssets.sleepersPerCell` sets how many ties a full one-cell straight gets (6 by default); with `evenSleeperSpacing` on, a curve gets `round(sleepersPerCell * curve.Length)` so the spacing stays even across straights (length 1.0) and curves (π/4), and with it off every piece gets the same count and curves pack them tighter. Because the tiles are bent to join into a deck, a count below about 6 leaves visible seams on curves — lower it deliberately for a ladder of separate ties. Both meshes, plus `railroad-straight.fbx`, are Read/Write enabled; most of the kit is not, and a non-readable mesh is ignored with a warning.
- `widthScale` narrows the whole track and `sleeperWidthScale` narrows the ties on top of that. **0.7 is the value that matters**: the kit tie is a full cell wide, so at 1.0 it spans radius 0.000–1.008 on a curve and swallows the arc centre, while at 0.7 it spans 0.150–0.859 and lines up exactly with the rails (0.150–0.850). `TrackPath` concatenates the `TrackCurve`s from S to E with a one-cell straight into each tunnel; `TrainRunner` samples it by arc length (2.5 cells per second, cars 0.9 apart, hidden inside the tunnels) and raises `Finished`, which `GameManager` turns into `FinishTrainRun`. Models come from `Assets/03.Data/Board/TrainAssets.asset` with boxy placeholders while empty.
- Interaction (PRD section 4) is decided in Core by `PlacementSession`; `BoardView` only renders markers and pieces from its state and reads the pointer (mouse or primary touch) itself. Auto-place happens when one key is legal on selection and also when the first chosen side leaves a single legal partner.
- The main camera carries a `BoardCamera` (added at runtime) that solves `CameraFit` for a world-space box and re-fits when the aspect, projection mode or field of view changes. `BoardView.ContentBounds` supplies the box: the grid plus the tunnel ring and `BoardLayout.Height` as a minimum (so the train and markers always fit), grown by the renderer bounds of everything built (tunnel mouths and letters, clue labels); the camera aims at the centre of that box, so one-sided clue labels do not push the board off centre. It handles both projections: perspective moves the camera back along the pitched view direction, orthographic grows the orthographic size (the baked scene camera is orthographic). It builds the projection matrix itself and shifts it down by `hudFraction` so the board is centred in the strip below the HUD; `Fit(aspect)` is public so tools can check other aspects. The Play HUD is transparent apart from a small `||` pause button, the level name and the clock along the top edge. Every placement and erase runs `WinChecker` in `BoardView.Validate`: satisfied clues turn green, over-filled lines red, and a winning board raises `Completed` so `GameFlow.CompleteLevel` fires. Loading never announces a win. The pause menu keeps an editor-only "Debug: win" button for testing the screens.
- Japanese survives as environment art only (D13): the platform-edge decals in `BoardView.BuildDecor` and the locomotive's destination plate in `TrainRunner`. Both are world-space `TextMesh`, so they need a legacy `Font` — `TrackAssets.signageFont` and `TrainAssets.signageFont`, pointing at `NotoSansJP-Variable.ttf`. Clear either field and that art is skipped rather than drawn as missing-glyph boxes. **No Japanese may reach a `VisualElement` outside the `ja` string table.**
- Input goes through `Assets/03.Data/Input/InputSystem_Actions.inputactions` (maps `Player` and `UI`); the legacy Input Manager is disabled in ProjectSettings.
- `*.csproj`, `*.slnx`, `Library/`, `Logs/`, `UserSettings/` are gitignored and regenerated by Unity. Always commit `.meta` files alongside new assets.
- `ProjectSettings.asset` currently has `defaultScreenOrientation: 4` (auto-rotate); the spec calls for portrait, so lock it when building for mobile.
