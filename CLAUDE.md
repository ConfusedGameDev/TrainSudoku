# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

TrainSudoku: a portrait mobile puzzle game (mouse-compatible) built in Unity `6000.7.0a3` with URP and the new Input System. The agreed spec is `Docs/PRD.md` (rules, screens, data format, architecture, milestones M0–M10); read it before touching gameplay. `Plan.md` is the original brief and is superseded where they differ. The only scene is `Assets/Scenes/SampleScene.unity`.

Progress: M0 (Core rules), M1 (level editor) and M2 (UI flow with a stub board) are done. Assemblies:

| Assembly | Location | Notes |
|---|---|---|
| `TrainSudoku.Core` | `Assets/01.Scripts/Core/` | `noEngineReferences: true`. Board, legality, win check, path finder, solver (with node budget), `LevelText` parser/serialiser, `LevelAuthoring` edit helpers, `GameFlow` state machine, `PlayTimer`, `ProgressTracker`, `ISaveStore` |
| `TrainSudoku.Game` | `Assets/01.Scripts/Game/` | References `UnityEngine.UI` and `Unity.InputSystem`. `GameManager` (scene entry point on the `Game` object in `SampleScene`), `UiBuilder` and one `PanelBase` subclass per state under `Ui/`, `IBoardView` + `StubBoardView` under `Board/`, `AudioCue` hooks under `Audio/`, `LevelDefinition` and `LevelCollection` ScriptableObjects |
| `TrainSudoku.Editor` | `Assets/01.Scripts/Editor/` | Editor only. `LevelEditorWindow` (Window > TrainSudoku > Level Editor, UI Toolkit) and the `LevelDefinition` inspector button |
| `TrainSudoku.Tests.EditMode` | `Assets/99.Test/EditMode/` | NUnit tests for Core and the asset round-trip |

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
- Level assets are saved by the editor window into `Assets/03.Data/Levels/`; the level `id` string is the save-file identity and must not change after release. `LevelCollection.asset` in that folder is what the `GameManager` loads.
- The uGUI screens are built in code by `UiBuilder` (legacy `Text`, portrait reference 1080x1920, canvas scaler match 0.5) rather than authored as prefabs, so screens can be added and reviewed without opening the Editor. Every button plays an `AudioCue`; the cue-to-clip mapping is `Assets/03.Data/Audio/UiAudioCues.asset`.
- `GameFlow` in Core owns all screen transitions and throws on invalid ones; panels only call it, never each other. The board talks to the flow through `IBoardView` (`Interacted`, `Completed`).
- Input goes through `Assets/03.Data/Input/InputSystem_Actions.inputactions` (maps `Player` and `UI`); the legacy Input Manager is disabled in ProjectSettings.
- `*.csproj`, `*.slnx`, `Library/`, `Logs/`, `UserSettings/` are gitignored and regenerated by Unity. Always commit `.meta` files alongside new assets.
- `ProjectSettings.asset` currently has `defaultScreenOrientation: 4` (auto-rotate); the spec calls for portrait, so lock it when building for mobile.
