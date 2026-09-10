TRAINSUDOKU · UI WORK ORDER
v0.4 · 10 SEP 2026 · main @ 3947585
STATUS: M15 done · M16 next of M11–M20 · progress table in 11.0

# Station UI — implementation work order

Instructions for Claude Code working directly in `ConfusedGameDev/TrainSudoku`. This is not a proposal to be
scoped by a studio — there are no estimates here, only the exact files to touch, the symbols that connect to
what, the traps that will silently break the build, and an acceptance test for each step.

The visual authority is `Docs/Train Sudoku UI - Line 01 (standalone).html` ("Direction 2"), amended by every
decision in section 0. Where the mockup and section 0 disagree, **section 0 wins** — the mockup was a pitch,
and four of its assumptions have since been overturned.

---

## 0 · Decisions taken

Settled by interview on 10 Sep 2026. Closed. Do not re-open one without asking first.

| # | Decision | Consequence |
|---|---|---|
| D1 | Real product, shipping to the App Store | Store metadata, portrait lock and font budget are launch blockers, not polish |
| D2 | 9 levels ship; the data model must scale to more | Build `NetworkDefinition` now, populate it with one line |
| D3 | Direction 2 is the visual authority | …as amended by this table |
| D4 | Scope is front end **plus** system integration **plus** localisation | The v0.1 claim of "front end only" was wrong |
| D5 | **One language per build.** No bilingual stacked pairs | Kills the kana-above-roman signage that was Direction 2's signature device |
| D6 | Ship English, Japanese, Spanish, French via `com.unity.localization` | Package is **not installed**; step 0 installs it |
| D7 | Full UI Toolkit | See section 7.1 |
| D8 | The 3D board is out of scope | UI is a strip above and a strip below; see the banner |
| D9 | `SetHudInsets(top, bottom)` replaces `SetHudFraction` | One scalar cannot express two insets; see 5.3 |
| D10 | Network renders only lines that have content | v1 shows one line, not five with four locked |
| D11 | Paid once. No IAP, no ads, no analytics, in v1 | No shop screen, no consent flow, no restore-purchases path |
| D12 | Star thresholds hand-authored, seeded from real play | 18 numbers; not derived from solver node count |
| D13 | Japanese survives as **environment art only** | Mesh or texture: yes, in every locale. `VisualElement`: never |
| D14 | Station names are invented, untranslated proper nouns | Zero translation cost; `LevelDefinition.id` values do **not** change |
| D15 | Fonts swap per locale via a Localization Asset Table | en/es/fr carry no Japanese glyphs at all |
| D16 | Icons are `Painter2D` vector paths, not an SVG package | `com.unity.vectorgraphics` is not installed and is experimental |
| D17 | `Hard` (12×11) is replaced by a new verified finale | It has never been proven uniquely solvable |
| D18 | **8 cells is the hard cap** on shipped board width | 12 wide in portrait gives sub-11 mm tap targets |
| D19 | The network branches; `Interchange[]` stays in the schema | Branch→island later is an empty array; island→branch is a migration |
| D20 | Line gate is lenient: **1★ on every station** opens the next line | Unobservable in v1; tighten with telemetry before line 2 |
| D21 | Pause and Train Run are **derived**, not designed | The mockup never drew them; they recombine existing components |

### 0.1 What D5 costs, stated plainly

Every sign in the Direction 2 mockup is a stacked pair — `ろせんず` small above `LINE MAP` large below. That
two-line rhythm *is* the station-sign read. Single-line signage in Barlow Condensed is cleaner, honest and
properly localisable; it is also noticeably plainer. **Mock one screen single-line and look at it before
step 8, not after.** If it reads as generic, the recovery is typographic (weight, tracking, rule-under-header),
not a return to the pair.

---

## THE BOARD IS NOT PART OF THIS WORK

The playfield is an existing, finished 3D scene: `BoardView` builds tiles, tunnels, world-space clue labels and
bent track meshes as real GameObjects, and `BoardCamera` pitches a camera at 60° and frames them. It is rendered
by the camera, not by any canvas. Every screen in this spec is built **around** that 3D view, never over it and
never instead of it. The UI's job is the strip above the board, the strip below it, and the full-screen overlays
(menus, maps, pause, arrival). **No UI code may draw a tile, a track piece or a train.** Files under
`01.Scripts/Game/Board/` and `01.Scripts/Game/Train/` change in exactly the ways listed in section 5, and in no
others.

---

## 1 · What connects to what

The current UI is not a separate layer that can be swapped wholesale — `BoardView`, `BoardCamera` and
`TrainRunner` all reach into `UiBuilder` for colours, a font and scene helpers. **Read this table before
deleting anything.** Every row below was verified against the code at `3947585`.

| EXISTING SYMBOL | WHO DEPENDS ON IT | CONSEQUENCE |
|---|---|---|
| `UiBuilder.Background` | `BoardCamera.Attach` sets `camera.backgroundColor` from it | Delete `UiBuilder` naively and the 3D board loses its clear colour. Move the value to `Palette` (4.1) and repoint |
| `UiBuilder.Font`, `.Font.material` | `BoardView.Label` — the world-space `TextMesh` for clue numbers and the S/E tunnel letters | These are 3D objects, not UI. They must keep a `Font`. Give `BoardView` a serialized `Font` field or keep the accessor in `Palette` |
| `UiBuilder.Success`, `UiBuilder.TextColor` | `BoardView.ClueColor`, alongside its own private `ClueExceeded` | Clue colouring is board logic. Repoint at `Palette` |
| `UiBuilder.Clear(Transform)` | `BoardView.Clear`, `BoardView.RefreshSelection`, **and `TrainRunner`** | Generic scene helper, nothing to do with widgets. Keep it |
| `UiBuilder.Destroy(GameObject)` | `GameManager.ClearGenerated` | Keep |
| `UiBuilder.Rect`, `Stretch`, `CreateCanvas`, `EnsureEventSystem` | `GameManager.Generate`, `AddPanel<T>` | `CreateCanvas` goes; **`EnsureEventSystem` stays** — UI Toolkit still routes runtime pointer events through the EventSystem |
| `UiBuilder.Label/Button/Row/Column/ScrollList/…` | The six `PanelBase` subclasses only | Delete with the panels |
| `BoardCamera.hudFraction` (0.1) | Shifts the projection matrix down so the board centres below the HUD | Replaced by `SetHudInsets`. See 5.3 — **this is the item most likely to be discovered late, by eye** |
| `GameManager.IsGenerated` → `panels.Count != 6` | Guards the runtime fallback in `Awake` | A hard-coded 6 at `GameManager.cs:59`. Change it with the screen count or the scene silently regenerates its UI every launch |
| `BoardView.IsOverUi(touchId)` → `EventSystem.IsPointerOverGameObject` | `BoardView.cs:550`, called from the press handler at `:420` | The highest-risk line in the migration. See 4.4 |

### 1.1 The flow contract, unchanged

Everything in `01.Scripts/Core/` is UI-agnostic and stays. The new screens plug into exactly the same three
seams the current panels use:

- `GameManager.ApplyState(GameState)` loops the screen list, calls `SetVisible(IsVisibleIn(state))` then
  `Refresh(state)`. **Keep this loop verbatim.**
- `PanelBase.Bind(GameManager)` gives each screen `Game` and `Flow`; buttons call `Flow.*` methods and nothing
  else. `GameFlow.Require` throws on an illegal transition, so a wrong button is a crash, not a soft bug — good,
  keep it.
- `GameManager.Update` calls `_playPanel.UpdateClock(Flow.Timer.Elapsed)` every frame in `Play`. The new play
  screen must expose the same method or the clock stops.

Therefore: **keep the `PanelBase` abstraction and its method names.** Re-implement its body against a
`UIDocument` instead of a `RectTransform`. `GameManager` then changes in four places instead of being rewritten.

---

## 2 · Screens

| SCREEN | GAMESTATE | BEHAVIOUR AND BOARD RELATIONSHIP |
|---|---|---|
| Concourse | `MainMenu` | Full-screen. Station sign, current-line progress strip, platform art, BOARD / LINE MAP, LED ticker. Board hidden (no level loaded) |
| Network | `Network` (new) | Full-screen map of every line **that has content** (D10). Open line in full colour, unlockable-but-unearned lines in a 25 % tint with a lock badge at the terminus, legend with unlock rules. Tap an open line → `LevelSelect` |
| Line map | `LevelSelect` | Full-screen. One line as a loop or route, one roundel per station, docked card for the selection. Replaces the scrolling button list in `LevelSelectPanel` |
| Play | `Play` | Transparent frame around the 3D board. Top: sign bar (pause, roundel, station name, LED clock) — **250 px**. Bottom: LED strip — **150 px**. Right: hint button. Everything between is untouched camera view |
| Signal stop | `Pause` | Dim scrim over the still-rendering board plus a signage card. `BoardView.SetInteractable(false)` already fires from `ApplyState` |
| Train run | `TrainRun` | LED strip only, full-screen tap-to-skip target. Unchanged logic |
| Arrival | `Win` | Arrival board over the solved 3D board. If the run completed the line, route to `Network` instead of the next station and animate the new line opening |

### 2.1 The two screens the mockup never drew (D21)

Direction 2 contains five artboards: Concourse, Network, Line Map, Play, Arrival. **Pause and Train Run do not
exist in it and no art is coming.** Do not stall at step 8 waiting for them. Derive both from components that
already exist by step 7:

- **Signal stop** = the Arrival card's chrome (dark signage panel, `--ink` ground, line-colour rule) over a
  `--ink` scrim at 65 % alpha, with three stacked buttons — RESUME / START OVER / BACK TO MAP — plus elapsed
  time and rails-laid read out in the same two-column form the Arrival screen uses for THIS RUN / BEST.
- **Train run** = the `LedStrip` element alone, bottom-anchored, over a full-screen transparent button that
  calls the existing skip path. Nothing else on screen.

---

## 3 · Localisation

D5, D6, D13, D14, D15. **This section is why there is a step 0.** v0.2 said every string should go through a
lookup "from the start" and then listed eleven steps, none of which set up localisation. Building the screens
first and localising second means re-opening all seven of them.

### 3.1 The rule

**One language per build, chosen by the player.** There are no stacked pairs anywhere in the UI. A sign is one
line of Barlow Condensed in the active locale.

The one exception, and it is a hard line a coder can follow without judgement:

> **If it is a `VisualElement`, it is one language. If it is a mesh or a texture in the 3D scene, it may be
> Japanese in every locale.**

That keeps the identity alive in the English, Spanish and French builds through platform decals, the train's
destination plate and the LED grille texture — scenery, not copy — at zero translation cost. Nobody expects
the paint on a platform to change language.

### 3.2 Locales and tables

- Locales: `en` (source), `ja`, `es`, `fr`.
- **One String Table** for UI copy. Keys are screen-scoped: `play.pause`, `arrival.on_time`, `network.opens_after`.
- **One Asset Table** carrying the font asset per locale (3.4).
- Station and line names are **not** in the tables — they are untranslated proper nouns held on the assets (D14).

### 3.3 Text length

Spanish and French signage runs 20–30 % longer than English: `NEXT STATION` → `PROCHAINE STATION`,
`LINE MAP` → `MAPA DE LÍNEA`. The signage band is a fixed 190 px with a 24 px floor and positive tracking, so
something has to give. **The rule is: tracking tightens first, then the font shrinks to the 24 px floor, then
the label wraps to two lines and the band grows.** Never ellipsise a sign — a truncated station name is worse
than a tall one. Every signage element gets `white-space: normal` and a `min-height`, not a fixed height.

### 3.4 Fonts (D15)

| LOCALE | FONT ASSETS | NOTE |
|---|---|---|
| en, es, fr | Barlow, Barlow Condensed | **Zero Japanese glyphs.** Latin Extended covers the accents |
| ja | Barlow, Barlow Condensed, Noto Sans JP | Static SDF atlas built from the *actual* ja String Table |

All three faces are SIL OFL, so commercial use is clear. Build the ja atlas from the translated strings once
copy is final and regenerate it whenever copy changes — a UI this small is a few hundred glyphs, not a full CJK
set. **Check the atlas size after every copy change**; fonts are the only real build-size risk in this project.

> v0.2's instruction to "subset the Japanese to an explicit character list … for the few dozen kana this UI
> uses" is void. It was written for a bilingual design that no longer exists, and it is wrong in both
> directions: en/es/fr need none, and ja needs far more than a few dozen.

### 3.5 Station names (D14)

The nine stations are currently named `SIMPLE`, `CORNER`, `FIRST STEPS`, `PLAN EXAMPLE`, `ZIGZAG`, `CROSSING`,
`SPIRAL`, `LONG HAUL`, `HARD` — developer names for puzzles, promoted to station names by the mockup. They read
as a list of difficulty adjectives, and translating them would mean four authored strings each for names that
are placeholders in the first place.

Rename them to invented place names, exactly as `Shinjuku` is never translated on any map in the world. The new
names live in `LevelDefinition.displayName` as untranslated strings.

**`LevelDefinition.id` values must not change.** They are the save-file identity (`GameManager.SaveFilePath`,
`save.json`), and the asset filenames should stay put too so git history stays legible.

---

## 4 · Files to create

### 4.1 `01.Scripts/Game/Ui/Palette.cs`

Static colour and font tokens, in the `Game` assembly so `BoardView` and `BoardCamera` can read it. Carries
`Paper`, `Ink`, `InkDim`, `Closed`, `Led`, `LedGround`, `Warn`, `Stop`, `Success`, `ClueExceeded`,
`BoardBackground`, plus `Font`. Every colour the board currently pulls from `UiBuilder` moves here **unchanged
in value** — the re-skin happens later, by editing this one file.

Line colour is **not** in `Palette`. It is a runtime value the shell publishes as the USS variable
`--line-current`, read from the active `LineDefinition` on every state change. Screens must never name a
specific line's colour. `NetworkMapElement` is the sole exception: it draws several lines at once and reads
each line's colour from its own `LineDefinition`.

### 4.2 `01.Scripts/Game/Ui/` — the UI Toolkit layer

| FILE | CONTENTS |
|---|---|
| `PanelBase.cs` (rewrite in place) | Same public surface — `Build()`, `Bind(GameManager)`, `IsVisibleIn`, `Refresh`, `SetVisible`, `IsBuilt`. Body wraps a `UIDocument`; `SetVisible` toggles `rootVisualElement.style.display` rather than the GameObject, so the panel keeps receiving `Refresh` |
| `UiShell.cs` | Owns the `PanelSettings`, applies `Screen.safeArea` as root padding, sets `--line-current`, runs the 260 ms transition |
| `Elements/StationRoundel.cs` | `VisualElement` subclass. `generateVisualContent` → `Painter2D` arc + stroke; properties `code`, `number`, `state` (cleared / current / closed) |
| `Elements/LineMapElement.cs` | Draws one `LineDefinition`: stroked polyline through `mapNodes`, dots at `stationNodeIndices`, interchange rings on top. Hit test = nearest station node within 60 px; raises `StationClicked(int)` |
| `Elements/NetworkMapElement.cs` | Same draw, every line **with content**, tint for locked ones, lock badge at each closed terminus. Raises `LineClicked(int)` |
| `Elements/LedStrip.cs` | Scrolling amber text with a tiling grille overlay. **`Announce(string localizationKey)`** — one argument (D5) |
| `Elements/Icons.cs` | ~14 `VisualElement` subclasses, each stroking 1–3 Lucide paths with `Painter2D` at 1.5 width, tinted by `--line-current` or `--ink` (D16, 7.2) |
| `Screens/*.cs` ×7 | One per row of section 2, each a `PanelBase` subclass with a UXML asset beside it |
| `Uss/tokens.uss`, `components.uss` | Section 6 tokens as USS variables; component rules |

Delete `MainMenuPanel`, `LevelSelectPanel`, `PlayPanel`, `PausePanel`, `TrainRunPanel`, `WinPanel` and the
widget half of `UiBuilder` **only after the replacements compile.**

### 4.3 Assets that must exist before anything renders

- `03.Data/Ui/PanelSettings.asset` — reference resolution 1080×1920, Match 0.5 (same as the current
  `CanvasScaler`), Scale With Screen Size.
- Font assets per 3.4, plus the Localization Asset Table that selects between them.
- The String Table with the `en` source strings.
- Verify `com.unity.modules.uielements` is in `Packages/manifest.json` (it is, at `1.0.0`).
- Add `com.unity.localization` (it is **not** present).
- Do **not** add `com.unity.vectorgraphics` (D16).

### 4.4 The pointer trap

`BoardView.Update` reads the pointer itself and calls `IsOverUi(touchId)` at `BoardView.cs:550`, which asks
`EventSystem.current.IsPointerOverGameObject()`. That returns true for uGUI graphics because of the
`GraphicRaycaster` the old canvas had. UI Toolkit participates through a `PanelRaycaster` that Unity adds per
runtime panel — **this must be verified on 6000.7 before the screens are built**, because if it does not hold,
every tap on the pause button also places a track piece.

Test first, in isolation: a scene with the board and one `UIDocument` with a button in the corner; tap the
button; assert no piece is placed. If it fails, change `IsOverUi` to also ask the panel directly:

```csharp
var panel = uiDocument.rootVisualElement.panel;
var local = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(pos.x, Screen.height - pos.y));
if (panel.Pick(local) != null) return true;
```

---

## 5 · Files to modify

### 5.1 `Game/GameManager.cs`

- `Generate()` — replace `UiBuilder.CreateCanvas` + six `AddPanel<T>` calls with one `UiShell` object and seven
  `UIDocument` children, each carrying the serialized `PanelSettings`. Keep `UiBuilder.EnsureEventSystem`,
  `BoardCamera.Attach`, `AudioCuePlayer.Create`, `BoardView.Create` and `TrainRunner.Create` exactly as they
  are, **in that order**.
- `IsGenerated` — `panels.Count != 6` becomes `!= 7` (line 59).
- `CurrentLevel` — resolve through the network:
  `Network.Line(Flow.CurrentLineIndex).Levels[Flow.CurrentStationIndex]`, with the flat levels list kept as the
  id source.
- Add a serialized `NetworkDefinition network` field alongside `levels`; `Awake` builds the flat id list from it
  so `GameFlow`'s constructor signature does not change.
- **Do not touch** `ApplyState`, `OnBoardInteracted`, `OnBoardCompleted`, `SaveProgress`, `OnApplicationPause`
  or the `Update` tick.

### 5.2 `Core/GameFlow.cs`

- Add `Network` to `GameState`.
- New transitions: `ShowNetwork()` allowed from `MainMenu`, `LevelSelect`, `Win`; `ShowLineMap(int lineIndex)`
  from `Network`; `ShowMainMenu` gains `Network` to its `Require` list.
- `CurrentLineIndex` / `CurrentStationIndex` alongside the existing `CurrentLevelIndex`, which **stays the flat
  index and stays the save-file identity**.
- `HasNextLevel` currently means "next in the flat list". Split into `HasNextStation` (same line) and
  `IsLineComplete`; `NextLevel()` routes to `Network` when the line is done.
- `IsLineUnlocked(int lineIndex)` delegating to `ProgressTracker`.

### 5.3 `Game/Board/BoardCamera.cs` — the inset fix (D9)

`hudFraction` reserves space along the **top edge only**. `Fit` computes `strip = 1f - hudFraction` and shifts
the projection with `shift.m13 = -hudFraction`. One scalar, one edge.

Section 2 now specifies **250 px on top and 150 px on the bottom**. No single value satisfies both: feed it
`250/1920 ≈ 0.130` and the board is centred in the 1670 px below the sign bar — its centre lands ~50 px low and
the fit assumes 1670 px of height when only 1520 px is free, so the board's bottom runs under the LED strip.
Feed it `0.026` to place the centre correctly and the fit thinks 97 % of the screen is available and the board
overflows both bars. The scalar conflates *how much room is free* with *where its centre is*, and two insets
need two numbers.

Replace it with:

```csharp
public void SetHudInsets(float top, float bottom)
```

Both are fractions of viewport height, each clamped to `[0, 0.4]` with `top + bottom` clamped below `0.9`.
In `Fit`:

```csharp
var strip = 1f - top - bottom;   // was: 1f - hudFraction
var vertical = strip * verticalFraction;
...
shift.m13 = bottom - top;        // was: -hudFraction
```

The clip-space centre of the free strip is `bottom - top`; the old behaviour is the special case `bottom = 0`,
so `SetHudFraction(x)` is exactly `SetHudInsets(x, 0)` and existing `CameraFit` tests are unaffected. For
250/150 at 1080×1920 this gives `strip = 0.792` (1520 px ✓) and `m13 = -0.052`, a 50 px downward shift.

The play screen calls it once in `Refresh` with its measured bar heights ÷ screen height. **Nothing else in this
file changes**: the pitch, the fit maths and `CameraFit` stay as they are.

### 5.4 `Game/Board/BoardView.cs` and `BoardMaterials.cs` — colours only

`BoardMaterials` values are re-tinted to the platform palette (concrete tiles, steel ballast, line-colour fixed
pieces, red exit portal). `BoardView` changes only where it names `UiBuilder`: the `Label` font and the two clue
colours in `ClueColor` become `Palette` lookups.

Optional and separable: a chamfered tile mesh generated once at startup instead of `PrimitiveType.Cube`, and a
swept-arch tunnel portal mesh to replace the placeholder box in `BuildTunnel`. Both are additive and **must not
alter `ContentBounds` behaviour**, or the camera fit shifts.

Environment art under D13 lands here: platform decals and the train destination plate may carry Japanese in
every locale.

### 5.5 `Core/` progression

| FILE | CHANGE |
|---|---|
| `ISaveStore.cs` | `bool TryGetStars(string levelId, out int stars)` and `void SetStars(string levelId, int stars)`. Implement in `InMemorySaveStore` in the same file |
| `SaveJson.cs` | Write `"version":2` with a `"stars"` map; read v1 and v2. A v1 file loads, awards stars from each level's thresholds, and is rewritten as v2. Malformed `stars` is skipped, never fatal — the same rule the existing `inProgress` parser follows |
| `FileSaveStore.cs` | Pass the new pair through; the temp-file swap and `.corrupt` handling are untouched |
| `ProgressTracker.cs` | `RecordCompletion` gains a `starTimes` parameter, computes stars, keeps the best. Add `GetStars(levelId)`, `StarsOnLine(ids)`, `IsLineUnlocked(prevLineIds, minStars)`. Station unlocking (`IsUnlocked`) is unchanged |
| `CompletionResult` | Add `Stars` and `IsNewBestStars` |

**Line unlock state is derived, never stored** — recomputing from stars each launch means a rule change ships
without a save migration. The v1 rule is `minStars = 1` (D20).

### 5.6 New data assets

| ASSET | FIELDS |
|---|---|
| `LineDefinition` (SO) | `id`, `code` ("TS"), `displayName` *(untranslated proper noun)*, `color`, `LevelCollection levels` (reuse the existing asset type as-is), `UnlockRule unlockRule`, `mapShape`, `Vector2[] mapNodes`, `int[] stationNodeIndices` |
| `NetworkDefinition` (SO) | `LineDefinition[] lines`, `Interchange[] interchanges` (`lineA`, `nodeA`, `lineB`, `nodeB`) — kept per D19 — map bounds |
| `LevelDefinition` (extend) | `float[] starTimes` length 2 — seconds for 3★ then 2★. Add a serialized field and a `StarTimes` accessor; **`SetFrom` must not clear it**. `displayName` already exists and becomes the station's proper noun (D14). **No kana field** (D5) |

There is **no `kanaName` field** on either asset. v0.1 and v0.2 both listed one; D5 removed the need.

The shipped `LevelCollection.asset` becomes the `levels` of a single `LineDefinition` with `code = "TS"`. Level
ids do not change, so existing saves survive.

---

## 6 · Tokens

| TOKEN | VALUE | USE |
|---|---|---|
| `--paper` | `#F4F5F2` | Screen ground |
| `--ink` / `--ink-dim` | `#1F2321` / `#5A615D` | Text, signage panels, 4 px object borders / secondary text |
| `--closed` | `#9AA3A0`, `#B9C0BC` | Locked stations and lines |
| `--led` on `--led-ground` | `#FFB020` on `#0D0F0E` | Ticker, clock |
| `--warn` / `--stop` | `#F2C230` / `#D0342C` | Platform edge / exit portal, erase ring, error |
| `--line-current` | runtime | TS `#9ACD32` · MR `#2F6FB5` · KB `#E8702A` · SG `#C2379A` · NT `#5A4B9C` |

**Type.** Barlow Condensed SemiBold for all signage, letter-spacing 0.04–0.10 em, tighter as size grows. Barlow
Regular/Medium for body copy and list metadata. Minimum 24 px at the 1080×1920 reference. Roundel and clock
numerals are tabular — the clock must not reflow as digits change. Signage elements wrap rather than truncate
(3.3).

> v0.2's rule "Noto Sans JP for the kana line at 0.45–0.55× the roman size" is void. There is no kana line.
> Noto Sans JP appears only in the `ja` build, at full signage size, as the primary face.

**Geometry.** Square corners except the roundel (a perfect circle) and the board's tile chamfer. 4 px `--ink`
borders. Map strokes 26–30 px active / 22 px inactive; station dots r9 stroke 6; interchange rings r17 stroke 7.
Screen margin 48 px; signage band 190 px minimum with a 12 px line-colour rule beneath.

---

## 7 · Asset production

**Generate it in Unity. No exported-sprite pipeline.** The identity is parametric: a roundel is a ring plus a
line of tabular text plus a state; a map is a polyline with dots on it. As PNGs, every new line, rename, colour
tune and device scale factor becomes an export chore, and the map stops being authorable data. Two things come
from outside, and only two: **fonts and two or three tiny tiling textures.**

| ASSET CLASS | HOW | NOTES |
|---|---|---|
| Roundels, rings, dots, halos | Procedural · `Painter2D` | One element class; resolution independent and themable |
| Line and network maps | Procedural · `Painter2D` from `NetworkDefinition` | The reason for choosing UI Toolkit at all |
| Panels, bars, buttons, chips, cards | Procedural · USS | Square borders and flat fills are native. No sprites |
| LED strip | Hybrid | USS text plus one 8×8 tiling PNG grille. UI Toolkit takes no per-element custom shaders, so do not plan one |
| Icons | Procedural · `Painter2D` paths | See 7.2 |
| Fonts | External → imported | Barlow, Barlow Condensed, Noto Sans JP (all SIL OFL). Static SDF atlases, per locale (3.4) |
| Board tiles, portals | Procedural · existing primitives | Section 5.4. Colours first; meshes only after the screens work |
| Track pieces | Already procedural | `TrackMeshBender` over `TrackCurve`. Untouched |
| Train and liveries | Kit model + material variants | The stainless-and-green EMU is a texture swap on the kit locomotive, so a livery reward costs a material, not a model |
| Confetti, steam | Procedural · Shuriken | Colours from `--line-current` |

### 7.1 Why UI Toolkit and not uGUI (D7)

Two of the three headline screens are vector diagrams generated from data. uGUI can only draw those through a
custom `MaskableGraphic` that writes its own mesh, or through pre-generated sprites; UI Toolkit draws a stroked,
joined polyline in a few lines of `Painter2D` and gives USS variables for the line-colour swap. The cost is that
runtime custom shaders are effectively unavailable — which only affects the LED grille, already solved with a
tiling texture. `Core`, `GameFlow`, `ProgressTracker`, `AudioCuePlayer` and the whole 3D board are unaffected
either way.

### 7.2 Icons (D16)

v0.2 wanted Lucide SVGs through `com.unity.vectorgraphics`. **That package is not in the manifest** — only
`com.unity.modules.vectorgraphics`, which is the runtime module, not the SVG importer — and it is experimental.

Skip it. The ~14 icons this UI uses are 1–3 `<path>` elements each at a 1.5 stroke (the concourse arrow is
literally two paths). Paste the path data into small `VisualElement` classes in `Elements/Icons.cs` and stroke
them with `Painter2D`. Resolution independent, tintable from `--line-current`, no preview-package risk, no atlas
to rebuild. v0.2's "do not hand-draw them in `Painter2D`" was advice for icons with real detail; these have none.

### 7.3 The one editor tool worth building

A Line Map Editor window beside `LevelEditorWindow`: drag nodes on a 45°-snapped grid, assign stations to nodes,
set colour and code, preview the runtime draw at device aspect, and validate (every station on a node, no
overlapping strokes, each interchange shared by exactly two lines). Without it the maps are hand-typed
coordinates — which is how these mockups were made, and does not survive five lines.

---

## 8 · Content: the level lineup

The line's stations are its levels, so the lineup is now a UI concern as well as a gameplay one.

**`Hard` (12×11) must be replaced before launch (D17).** It has never been proven uniquely solvable — the
backtracking solver runs billions of nodes without a result, and `LevelCollectionTests` verifies every shipped
level *except* that one. It is currently the **terminus** of the line, so a paid game would end on a puzzle that
might have zero solutions or many.

It is also 12 columns wide in portrait. `BoardCamera` will dutifully fit it, giving roughly 84 px tiles before
margins — around 11 mm on a phone, at the floor for a target that must accept both a tap and a long-press-to-erase.

- Author a new verified 6×6 or 7×7 finale and give it a new `id`.
- **8 cells is the hard cap on shipped board width (D18).** `LongHaul` at 8×8 is already at the practical limit.
  Add a validation rule to `LevelCollectionTests` so nothing wider than 8 can ship again.

Star thresholds (D12): play each of the nine, read your times out of `save.json` (`bestTimes` already records
them), and set 3★ ≈ your time × 1.3 and 2★ ≈ × 2.2. You are faster than a new player, so your raw time is a
reasonable 3★ target. Do **not** derive them from solver node count: that measures search-tree size, which is
how hard the puzzle is for a backtracker, not how long a person takes — and `Hard` has no computable value at all.

---

## 9 · Motion

All unscaled time, so it survives the pause. Nothing blocks input for more than 200 ms.

| MOMENT | DURATION | BEHAVIOUR |
|---|---|---|
| Piece snap-in | 220 ms | Overshoot to 1.15, settle. Already stubbed as `PieceView.PlayScaleIn` |
| Marker hop | 900 ms loop | Forced side hops 6 px higher, in `--line-current` |
| Erase hold ring | 500 ms | Linear fill, matching `BoardView.HoldDuration`. Fixed piece: 180 ms shake via `PieceView.PlayShake` |
| Clue satisfied | 160 ms | Token pops to 1.2; LED strip prints the line |
| Screen change | 260 ms | Incoming slides up from beneath the LED strip, outgoing drops 40 px and fades |
| Arrival | 1.4 s | Stamp 1.6 → 0.96 → 1.0 over 380 ms, then stars 120 ms apart |
| Line opening | 1.8 s | On `Network`: new line draws from its interchange outward (dash offset), lock badge falls away |

---

## 10 · Audio, platform, commerce

- `AudioCue` gains `MapOpen`, `StationSelect`, `LineUnlocked`, `StarAwarded`; wire clips in
  `03.Data/Audio/UiAudioCues.asset`. `AudioCuePlayer.Play` is already global and needs no change.
- `ProjectSettings.asset` has `defaultScreenOrientation: 4` (auto-rotate). **Lock to portrait** (D1).
- Safe area is applied once by `UiShell` as root padding; the LED strip may extend into the home-indicator area
  but its text may not.
- Design survives 9:16 through 9:21. Maps scale to fit their container's shorter axis at fixed aspect and never
  stretch.
- **Paid once. No IAP, no ads, no analytics, in v1 (D11).** No shop screen, no restore-purchases path, no
  consent flow, and the privacy manifest is the trivial one. Liveries are pure rewards. Revisit for v2, when
  there are five lines to sell.

---

## 11 · Milestones

M0–M10 are complete (see `CLAUDE.md`). This work is **M11–M20**. Each milestone compiles and runs before the
next begins.

> ### The verification gate
>
> **Do not `git commit` or `git push` until a milestone's *Verified by human* box is ticked.** Most of this work
> — layout, camera framing, how the board sits in its frame, whether the signage reads right — can only be
> judged by eye in the Editor. A clean compile and a green test run are not evidence a milestone is done.
>
> The loop is: finish the tasks → tick them → run the *Done when* checks → tick them → say the milestone is
> ready for review → **stop**. The human plays it, ticks *Verified*, and only then does anything get committed.
> One commit per milestone, message `M<n>: <title>`.

### 11.0 Progress

| MILESTONE | TITLE | VERIFIED |
|---|---|---|
| M11 | Foundations | ☑ |
| M12 | Shell and tokens | ☑ |
| M13 | Core: save v2, network, flow | ☑ |
| M14 | Content pass | ☑ |
| M15 | Elements, and the look test | ☑ |
| M16 | The seven screens | ☐ |
| M17 | Board framing | ☐ |
| M18 | Line Map Editor | ☐ |
| M19 | Motion and board re-skin | ☐ |
| M20 | Translation pass | ☐ |

---

### M11 · Foundations

**Goal:** de-risk everything that could invalidate the plan, and change nothing a player would see.
**Touches:** `Packages/manifest.json`, `Ui/UiBuilder.cs`, `Ui/Palette.cs`, `Board/BoardCamera.cs`,
`Board/BoardView.cs`, `Train/TrainRunner.cs`.

- [x] Add `com.unity.localization` to the manifest
- [x] Create locales `en` (source), `ja`, `es`, `fr`
- [x] Create the UI String Table and the font Asset Table (3.2)
- [x] Create `Palette.cs`; move every colour `BoardCamera`/`BoardView` reads from `UiBuilder`, unchanged in value (4.1)
- [x] Repoint `BoardCamera.Attach`, `BoardView.Label`, `BoardView.ClueColor`, and the `UiBuilder.Clear` callers in `BoardView` and `TrainRunner` (section 1)
- [x] Pointer-trap spike: board + one `UIDocument` button in a scratch scene (4.4)

**Done when**

- [x] Project compiles; the existing uGUI game runs and plays exactly as before
- [x] The board renders identically — clear colour, clue label font, clue colours all unchanged
- [x] Switching locale at runtime changes a test label's language
- [x] Tapping the `UIDocument` button over the board places **no** piece — or, if it does, `IsOverUi` is patched per 4.4 and then it doesn't
- [x] **Verified by human** ☑ &nbsp;&nbsp; date: 10 Sep 2026

> If the pointer spike fails and the 4.4 patch doesn't fix it, stop and re-plan. Everything downstream assumes
> UI can consume a tap.

#### M11 notes — what the milestone found

- **The editor moved from `6000.7.0a3` to `6000.7.0a6`.** `com.unity.localization` ships code guarded by
  `#if UNITY_6000_7_OR_NEWER` that calls the group-aware `EditorGUIUtility.TrTextContent*` overloads. Those land
  in a6 ("Editor: Added group aware localization APIs"), so on a3 the package's editor assembly did not compile
  at all. a6 also bundles the package as **BuiltIn at 1.5.12** — the manifest pin is 1.5.12 to match, and that is
  the only version the editor offers. Addressables came along at 4.0.1.
- **4.4's pointer trap does not bite.** In play mode a `PanelRaycaster` is created alongside the old
  `GraphicRaycaster`, so UI Toolkit is registered with the EventSystem and `BoardView.IsOverUi` works unchanged.
  **No patch to `IsOverUi` was needed** — leave it alone at M16.
- **`Palette` carries `ClueText`**, a name not in 4.1's list. 4.1 says board colours move "unchanged in value",
  but section 6's `--ink` is near-black while the clue text is near-white; both cannot be `Ink`. `ClueText` holds
  the old `UiBuilder.TextColor`; `Ink` holds the section 6 value. Re-skinning at M19 is one line.
- **`SceneObjects.cs` holds `Destroy`/`Clear`**, with `UiBuilder` forwarding, so section 1's "keep it" and M11's
  "repoint the callers" are both satisfied and M16 can delete the widget half safely.
- **`PanelSettings.asset` was created early** (M12's first checkbox) because a runtime `UIDocument` cannot exist
  without one and the spike needs it. 1080x1920, Match 0.5, at `03.Data/Ui/`.
- **`Assets/AddressableAssetsData/` sits at the Assets root** and holds two files. Its path is a hard-coded const
  in Addressables; the real settings were placed under `03.Data/AddressableAssetsData/`. The generated
  `Assets/UI Toolkit/` theme folder *was* relocated, to `03.Data/Ui/`.
- **The throwaway spike is gone.** `PointerSpike.cs` and `Spike_M11.unity` were deleted at sign-off, as planned.

---

### M12 · Shell and tokens

**Goal:** an empty UI Toolkit shell that renders correctly on a real device and themes itself from data.
**Touches:** `03.Data/Ui/PanelSettings.asset`, font assets, `Ui/UiShell.cs`, `Ui/PanelBase.cs`, `Uss/tokens.uss`.

- [x] `PanelSettings.asset` — 1080×1920, Match 0.5, Scale With Screen Size *(created early, at M11)*
- [x] Import Barlow and Barlow Condensed; build SDF atlases
- [x] Import Noto Sans JP; wire the per-locale font Asset Table (3.4)
- [x] `tokens.uss` — every token in section 6 as a USS variable
- [x] `UiShell.cs` — owns `PanelSettings`, applies `Screen.safeArea` as root padding, publishes `--line-current` *(see notes: published by tint class, not by USS variable)*
- [x] Rewrite `PanelBase` against `UIDocument`, same public surface (4.2) *(as `UiScreen`, alongside `PanelBase` — see notes)*

**Done when**

- [x] The shell renders at 1080×1920 with correct safe-area inset on a device or a notched simulator
- [x] Setting `--line-current` at runtime changes a test element's colour
- [x] Switching to `ja` swaps the font asset; switching back restores it
- [x] **Verified by human** ☑ &nbsp;&nbsp; date: 10 Sep 2026

#### M12 notes — two places the plan met the engine

- **`--line-current` cannot be a USS variable.** UI Toolkit can only *read* custom properties from C#
  (`CustomStyleProperty<T>` + `customStyleResolved`); the setter, `SetCustomProperty`, is internal to the
  UIElements module — verified by compiling against it on 6000.7.0a6. The contract in 4.1 is kept by a different
  mechanism: a screen tags an element `line-bg`, `line-fg` or `line-border` and `UiShell.ApplyLineColour` tints
  it. Screens still never name a line's colour and the swap is still one call. `tokens.uss` keeps a
  `--line-current` fallback so a screen opened outside the shell still renders.
  **Ordering matters:** the tint pass only finds elements that already exist, so `UiScreen.Build` registers with
  the shell *before* building the tree (for the token sheet) and re-tints *after* it. Getting this backwards
  silently leaves the first line colour unapplied — the elements render black.
- **`PanelBase` is not rewritten in place; `UiScreen` lands beside it.** M14 requires a *playable* game to read
  real times out of `save.json` for the star thresholds, and the six uGUI panels are `PanelBase` subclasses.
  Rewriting the base at M12 would leave the game uncompilable from M12 until the screens land at M16. `UiScreen`
  carries the same public surface, so `GameManager`'s state loop is unchanged; at M16 the seven screens, the
  `List<PanelBase>`, the six old panels, `PanelBase` and the widget half of `UiBuilder` all go together — which
  is what 4.2 asks for anyway ("delete the old panels only after the replacements compile").
- **Font assets are Dynamic, not baked static atlases.** All four are SDF with on-demand glyph rasterisation, so
  no atlas has to be rebuilt while copy is still moving. M20 already owns the static bake ("Rebuild the `ja` SDF
  atlas from the actual translated strings"), and that is where the build-size measurement belongs. Note the
  source `NotoSansJP-Variable.ttf` is 9.1 MB — the largest single asset in the project.
- **Table keys for fonts:** `font.signage`, `font.body`, `font.numerals`. Numerals stay Barlow in *every* locale,
  including `ja` — the roundel and clock are tabular figures (section 6) and Noto's are not the ones the layout
  was drawn against. That is why 3.4 lists Barlow in the `ja` row.
- **The throwaway spike is gone.** `ShellSpike.cs` and `Spike_M12.unity` were deleted at sign-off, as planned.

---

### M13 · Core: save v2, network, flow

**Goal:** every `Core` change in one pass, all of it test-verifiable, none of it visible yet.
**Touches:** `Core/SaveJson.cs`, `Core/ISaveStore.cs`, `Core/FileSaveStore.cs`, `Core/ProgressTracker.cs`,
`Core/GameFlow.cs`, `Game/LevelDefinition.cs`, new `LineDefinition`/`NetworkDefinition`.

- [x] `ISaveStore` gains `TryGetStars` / `SetStars`; implement in `InMemorySaveStore` (5.5)
- [x] `SaveJson` writes `"version":2` with a `"stars"` map; reads v1 and v2; malformed `stars` skipped, never fatal
- [x] `FileSaveStore` passes the pair through; temp-file swap and `.corrupt` handling untouched
- [x] `ProgressTracker.RecordCompletion` takes `starTimes`; add `GetStars`, `StarsOnLine`, `IsLineUnlocked(prev, minStars)` with `minStars = 1` (D20)
- [x] `CompletionResult` gains `Stars`, `IsNewBestStars`
- [x] `LevelDefinition` gains `float[] starTimes` (length 2) and a `StarTimes` accessor; **`SetFrom` must not clear it**
- [x] Create `LineDefinition` and `NetworkDefinition` SOs (5.6); keep `Interchange[]` (D19)
- [x] Wrap the shipped `LevelCollection.asset` as the `TS` line inside a one-entry `NetworkDefinition`
- [x] `GameFlow`: add `Network` state, `ShowNetwork()`, `ShowLineMap(int)`, `CurrentLineIndex`/`CurrentStationIndex`, split `HasNextLevel` into `HasNextStation` + `IsLineComplete`, `IsLineUnlocked` (5.2)

**Done when**

- [x] New tests pass: a v1 file migrates and awards correct stars; a v2 file round-trips; a malformed `stars` block is skipped
- [x] Existing `SaveJsonTests`, `FileSaveStoreTests` and `LevelCollectionTests` still pass *(three shape assertions updated to v2 — see notes)*
- [x] New `LevelCollectionTests` assert every station well formed, every id unique across the network, **and no board wider than 8** (D18)
- [x] New `GameFlowTests` cover each added transition, that illegal ones still throw, and that `NextLevel` at the end of a line routes to `Network`
- [x] `CurrentLevelIndex` is still the flat index and still the save-file identity
- [x] **Verified by human** ☑ &nbsp;&nbsp; date: 10 Sep 2026

#### M13 notes

- **`SaveJson` cannot award the stars itself** — it has no idea what a level's thresholds are. So the reader just
  accepts a v1 file (no `stars`) and the writer always emits v2; the awarding is
  `ProgressTracker.AwardMissingStars(levelId, starTimes)`, driven for the whole list by `GameFlow.AwardMissingStars()`.
  It is idempotent and never overwrites a rating that already exists, so a slow best time cannot demote a star the
  player earned. **M16 must call it once at startup** — nothing calls it yet.
- **Two new seams on `GameFlow`, both because Core cannot see a ScriptableObject.** `StarTimesForLevel` is a
  `Func<int, IReadOnlyList<double>>` the Unity layer sets from the level assets; unset, every completion is worth the
  floor of one star. And the constructor takes an optional `NetworkLayout` — null means one line holding every level,
  which is exactly the old behaviour, so the existing call in `GameManager` and every old test still work untouched.
- **New Core type: `NetworkLayout`** (`Core/NetworkLayout.cs`), the flat-list-to-lines mapping. Its whole job is the
  promise that **appending a line cannot renumber the ones before it**, which is what keeps the flat index the
  save-file identity. `NetworkDefinition.ToLayout()` builds it, skipping content-free lines.
- **`Hard` was removed from the shipped collection here, not at M14.** D18's width rule is only worth having if it is
  enforced, and leaving `Hard` in would have meant shipping M13 with a knowingly red test. The line is temporarily
  eight stations; **M14 still owns authoring the verified replacement finale**. `Hard.asset` is untouched on disk.
- **Three existing `SaveJsonTests` asserted the literal v1 JSON** and were updated to v2. One `GameFlowTests` case,
  `NextIsRefusedOnTheLastLevel`, asserted the old throw at the end of the list; it is now
  `NextAtTheEndOfTheLineGoesToTheNetwork`, which is the behaviour 5.2 asks for.
- **Nothing is visible yet and the running game is unchanged.** `GameManager` still builds `GameFlow` with the
  default single-line layout and no star provider, exactly as before. `Network` is unreachable in the current UI
  because `WinPanel` disables its Next button when `HasNextLevel` is false — worth remembering at M16, when wiring
  the network is what makes that route live.
- 275 EditMode tests pass, up from 235.

---

### M14 · Content pass

**Goal:** the nine stations become a line worth putting on a map, and the finale becomes one we can prove.
**Touches:** `03.Data/Levels/*.asset`, `LevelEditorWindow`.

- [x] Choose nine invented proper-noun station names (3.5)
- [x] Set them as `displayName` on each level; **`id` values and asset filenames unchanged**
- [x] Author a new verified 6×6 or 7×7 finale with a new `id` (section 8)
- [x] Remove `Hard` (12×11) from the shipped collection (D17) *(done at M13, so the width test is live rather than red-in-waiting; the asset is still on disk)*
- [ ] Play all nine; read times from `save.json` — **deferred by agreement**, see notes
- [~] Author 18 star times: 3★ ≈ your time × 1.3, 2★ ≈ × 2.2 (D12) — **placeholders in place**, see notes

**Done when**

- [x] The new finale is solver-verified uniquely solvable within the node budget
- [x] No shipped level exceeds 8 cells wide; the M13 test proves it
- [x] Nine proper-noun names read as a transit line, not a difficulty ladder
- [x] Every level has two star times; a fresh playthrough awards plausible stars
- [x] Existing `save.json` still resolves every level id
- [x] **Verified by human** ☑ &nbsp;&nbsp; date: 10 Sep 2026

#### M14 notes

**The line, in order.** Ids and asset filenames are untouched — they are the save-file identity.

| # | id | station | board |
|---|---|---|---|
| 1 | `simple` | Ashgate | 6×6 |
| 2 | `corner` | Kettleburn | 6×6 |
| 3 | `1` | Merrow Cross | 6×6 |
| 4 | `plan-example` | Halden | 6×6 |
| 5 | `zigzag` | Wrenfield | 6×6 |
| 6 | `crossing` | Sable Junction | 6×6 |
| 7 | `spiral` | Cinder Halt | 6×6 |
| 8 | `long-haul` | Thorne Park | 8×8 |
| 9 | `ravensmoor` | **Ravensmoor** | 7×7 |

- **The finale is 7×7, `id` `ravensmoor`, 33 track pieces, 7 of them pre-placed. The solver proves exactly one
  solution in 180,918 nodes** — comfortably inside `LevelCollectionTests`' five-million budget, which is the whole
  point of replacing `Hard`.
- **Clues alone could not produce a provable 7×7.** The first search tried 135 boards with no pre-placed pieces:
  108 exhausted a three-million-node budget *without finding a single solution*, and 27 found two. That is exactly
  the wall `Hard` hits. Pre-placed pieces are the lever — they prune the tree until uniqueness is provable, and they
  are also what gives the player a foothold. The generator now reveals progressively more of its own route until the
  board is provable, and keeps the sparsest one that works. For Ravensmoor that was 7 of 33.
- **`Assets/01.Scripts/Editor/FinaleGenerator.cs` was kept**, not deleted, because it is the provenance of
  `Ravensmoor.asset`: the search is deterministic, so `Search(7, 7, 29, 60, 20260910, 2000000, 300000, null)`
  reproduces that exact board. It is not wired to a menu item and nothing in the shipped game calls it.
- **Star times are placeholders and are the one thing here still owed real play.** D12 wants them seeded from real
  times; playing nine levels was deferred by agreement. The stand-in is track-piece count — the only honest proxy
  available without playing, since the player must place every piece — at **3 s per piece for three stars and 6 s
  for two**. That gives Ashgate 18 s / 36 s and Ravensmoor 99 s / 198 s. Tune from `save.json` later; the shape of
  the data is what matters now, and `LevelCollectionTests` already enforces that both exist and are ordered.
- **`GameManager` gained two lines**, earlier than 5.1 puts it, because without them a playthrough awards one star
  regardless and M14's own *Done when* could not be checked: `Flow.StarTimesForLevel` is pointed at the level assets
  and `Flow.AwardMissingStars()` runs once at startup. This also discharges the M13 note that nothing called the
  migration yet. Neither touches `ApplyState`, `Generate` or the `Update` tick.
- **Verified by a scripted playthrough:** Ashgate in 10 s → 3★, Kettleburn in 40 s → 2★, Merrow Cross in 200 s →
  1★, six stars on the line, and `save.json` on disk is version 2 carrying a `stars` map. The existing save
  migrated in place: its one best time survived and gained its rating.
- 278 EditMode tests pass.
- **Window > TrainSudoku > Save File** was added during review: the save lives under
  `Application.persistentDataPath`, which nobody can be expected to find. *Show Progress in Console* prints a
  table of station names, stars and best times, and flags any orphaned id in the save that no shipped level
  claims - the early warning if a level id is ever changed by accident.

---

### M15 · Elements, and the look test

**Goal:** the drawn pieces exist and are proven at every aspect — and we find out whether single-line signage
carries the identity **before** building six more screens on top of it.
**Touches:** `Ui/Elements/*.cs`.

- [x] `StationRoundel` — `Painter2D` arc + stroke, states cleared / current / closed
- [x] `LineMapElement` — polyline through `mapNodes`, dots, hit test to nearest node within 60 px, `StationClicked(int)`
- [x] `NetworkMapElement` — all content-bearing lines, tint + lock badge for locked, `LineClicked(int)` (D10)
- [x] `LedStrip` — scrolling amber text, tiling grille, `Announce(string localizationKey)` (D5)
- [x] `Icons.cs` — ~14 Lucide paths stroked with `Painter2D` (7.2)
- [x] **Look test:** mock one screen (Line map) at final type, in all four locales

**Done when**

- [ ] A scratch scene draws the TS line from its asset at 9:16, 9:19.5 and 9:21 with no clipping — **needs your eye**
- [ ] Clicking a station raises the right index; clicking a line raises the right line
- [x] No signage is clipped or ellipsised in `es` or `fr` (3.3)
- [ ] **The look test is judged by eye and passes** — see 0.1. If it reads as generic, fix it typographically here, not by reinstating the bilingual pair
- [x] **Verified by human** ☑ &nbsp;&nbsp; date: 10 Sep 2026

#### M15 notes

- **The icon paths are the mockup's own.** Rather than guessing at a Lucide set, I harvested the 22 inline SVGs from
  `Train Sudoku UI - Line 01 (standalone).html` — nine distinct icons, at a 24 box and 1.5 stroke — so the shipped
  icon and the pitched icon are the same shape. The five the screens need and the mockup never drew (pause, close,
  clock, retry) are added in the same idiom.
- **`VectorPath.cs` replays SVG path data into `Painter2D`.** 7.2 says to paste the path data in; that is only
  honest if the `d` attribute is kept verbatim rather than hand-translated into painter calls, so there is a small
  parser for `M L H V C S Q T A Z` with the standard endpoint-to-centre arc conversion. Every arc in this set is
  circular, but ellipses and x-rotation are handled so a future icon cannot silently come out wrong.
- **The TS line's `mapNodes` are hand-typed**, nine nodes on a 45-degree snap grid, which is exactly what 7.3 says
  they will be until M18's editor exists. A check in the authoring command asserts every segment is horizontal,
  vertical or exactly diagonal; all nine pass. M18 re-authors them with the tool and the runtime draw should not move.
- **The LED grille is an 8x8 PNG**, 80 bytes, point-filtered and set to repeat: transparent where a diode glows,
  55% black in the gutter. 7.1 rules out a runtime custom shader, and this is the one place the design reaches for
  a bitmap on purpose.
- **`LedStrip` runs on `Time.realtimeSinceStartup`**, not `Time.deltaTime`, so the strip keeps moving while the game
  is paused (section 9's "all unscaled time").
- **What I could not do: see it.** The Game view was not rendering frames — the editor's own AI package logged
  *"may be due to network issues or editor focus"* at the same moment — so both a screenshot and a render-to-texture
  came back black. I verified layout and colour numerically instead (map fills 1196x1068, nine nodes, TS #9ACD32,
  every element present) and confirmed **no label overflows its container in any of the four locales**. The look
  test itself is 0.1's question and was always yours to answer.
- **The look-test spike is deliberately still in the tree.** `LookTest.cs` and `Spike_M15.unity` were kept past
  sign-off rather than deleted, because 0.1's question is worth re-asking as the real screens land. **M16 removes
  them** once the Line map screen replaces the mock.
- 278 EditMode tests pass; the new elements are drawing code with no test coverage, which is what the look test is for.

> This is the milestone most worth stopping at. Six screens built on signage that doesn't read is the expensive
> version of this mistake.

---

### M16 · The seven screens

**Goal:** the whole game navigable end to end in the new UI, with the old one deleted.
**Touches:** `Ui/Screens/*.cs`, `Uss/components.uss`, `Game/GameManager.cs`.

- [ ] Concourse, Network, Line map, Play, Signal stop, Train run, Arrival (section 2)
- [ ] Signal stop and Train run derived from existing components, not designed (2.1)
- [ ] `GameManager.Generate` — one `UiShell` + seven `UIDocument` children; keep `EnsureEventSystem`, `BoardCamera.Attach`, `AudioCuePlayer.Create`, `BoardView.Create`, `TrainRunner.Create` in that order (5.1)
- [ ] `GameManager.IsGenerated` — `panels.Count != 6` → `!= 7` (`GameManager.cs:59`)
- [ ] `CurrentLevel` resolves through the network; serialized `NetworkDefinition` field added
- [ ] `AudioCue` gains `MapOpen`, `StationSelect`, `LineUnlocked`, `StarAwarded`; clips wired in `UiAudioCues.asset`
- [ ] Delete the six old panels and the widget half of `UiBuilder`

**Done when**

- [ ] Menu → Network → Line map → Play → Pause → Win navigates end to end on the real 3D board
- [ ] `UpdateClock` still ticks every frame in `Play`
- [ ] `ApplyState`, `OnBoardInteracted`, `OnBoardCompleted`, `SaveProgress`, `OnApplicationPause` and the `Update` tick are untouched
- [ ] Auto-save still round-trips: quit mid-level, relaunch, Continue restores pieces and clock
- [ ] Every button plays its `AudioCue`
- [ ] **Verified by human** ☐ &nbsp;&nbsp; date: ______

---

### M17 · Board framing

**Goal:** the board sits correctly between the two bars, on every phone.
**Touches:** `Board/BoardCamera.cs`, the Play screen.

- [ ] Replace `SetHudFraction` with `SetHudInsets(float top, float bottom)` (5.3)
- [ ] `strip = 1f - top - bottom`; `shift.m13 = bottom - top`; clamp each to `[0, 0.4]` and the sum below `0.9`
- [ ] Play screen calls it once in `Refresh` with measured bar heights ÷ screen height
- [ ] Add a `CameraFit` test asserting `SetHudInsets(x, 0)` equals the old `SetHudFraction(x)`

**Done when**

- [ ] The board is optically centred between the 250 px sign bar and the 150 px LED strip at 9:16, 9:19.5 and 9:21
- [ ] No board overlaps either bar at any aspect, including the 8×8 level
- [ ] Existing `CameraFit` tests still pass
- [ ] **Verified by human** ☐ &nbsp;&nbsp; date: ______

---

### M18 · Line Map Editor

**Goal:** maps become authored data instead of hand-typed coordinates.
**Touches:** `01.Scripts/Editor/`.

- [ ] Editor window beside `LevelEditorWindow`: drag nodes on a 45° snap grid
- [ ] Assign stations to nodes; set line colour and code
- [ ] Preview the runtime `Painter2D` draw at device aspect
- [ ] Validate: every station on a node, no overlapping strokes, each interchange shared by exactly two lines

**Done when**

- [ ] The TS line's `mapNodes` are re-authored with the tool and the runtime draw is unchanged
- [ ] Validation catches a deliberately broken line
- [ ] **Verified by human** ☐ &nbsp;&nbsp; date: ______

---

### M19 · Motion and board re-skin

**Goal:** the game feels like the mockup, not just looks like its wireframe.
**Touches:** `Uss/`, `Ui/Screens/*.cs`, `Board/BoardMaterials.cs`, `Board/BoardView.cs`.

- [ ] Every row of the section 9 motion table, on unscaled time
- [ ] `BoardMaterials` re-tinted to the platform palette (5.4)
- [ ] Japanese environment art: platform decals, train destination plate (D13)
- [ ] *Optional:* chamfered tile mesh replacing `PrimitiveType.Cube`
- [ ] *Optional:* swept-arch tunnel portal replacing the placeholder box

**Done when**

- [ ] Every motion in section 9 is present and nothing blocks input longer than 200 ms
- [ ] Motion still runs while paused (unscaled time)
- [ ] `ContentBounds` is unchanged within 0.01 after any mesh work, so the camera fit doesn't shift
- [ ] No Japanese appears in any `VisualElement` in `en`/`es`/`fr` (D13)
- [ ] **Verified by human** ☐ &nbsp;&nbsp; date: ______

---

### M20 · Translation pass

**Goal:** four locales that all ship.
**Touches:** the String Table, the `ja` font atlas, `ProjectSettings.asset`.

- [ ] Final `en` copy locked
- [ ] `ja`, `es`, `fr` translations authored into the String Table
- [ ] Rebuild the `ja` SDF atlas from the actual translated strings (3.4)
- [ ] Lock `defaultScreenOrientation` to portrait (currently `4`, auto-rotate)
- [ ] Record iOS build size before and after the font work

**Done when**

- [ ] All four locales render all seven screens with no clipped, wrapped-badly or ellipsised signage
- [ ] `en`/`es`/`fr` builds contain no Japanese glyphs in the UI atlas
- [ ] The `ja` atlas covers every glyph in the `ja` table — no missing-glyph boxes anywhere
- [ ] The build is portrait-locked on device
- [ ] Build size is recorded and acceptable
- [ ] **Verified by human** ☐ &nbsp;&nbsp; date: ______

---

## 12 · Deferred to v2

Not open questions — decided, and decided as "later".

- **Lines 2–5** (MR, KB, SG, NT) and the content behind them. The schema supports them from M13; the
  `NetworkDefinition` ships with one entry (D10).
- **Tightening the line gate** from 1★-all to 2★-all or an aggregate threshold, once there is telemetry (D20).
- **Livery economy** — how many at launch and whether they appear on the map and arrival screen. Liveries are
  rewards, never products (D11).
- **A station-PA voice** — recorded chimes and a two-tone departure jingle would carry the identity further than
  any visual. Costed separately. Text-to-speech announcements are out of scope permanently.
- **Monetisation of any kind.** Revisit only when five lines exist.
