using System;
using System.Collections.Generic;
using System.IO;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Level editor (PRD section 10). The level being edited is a Core <see cref="LevelData"/>; every change is
    /// serialised to text on <see cref="UndoState"/> so the Editor's Undo stack and domain reloads both see it.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private enum Tool { NS, EW, NW, NE, SW, SE, Erase, Entrance, Exit }

        private const int MinSize = 2;
        private const int MaxSize = 16;
        private const long QuickSolveBudget = 200_000;
        private const long DeepSolveBudget = 20_000_000;
        private const string DefaultLevelFolder = "Assets/03.Data/Levels";
        private const string DefaultLevelText = "  0 0 0 0 0 0\nS . . . . . . 0\n. . . . . . 0\n. . . . . . 0\n. . . . . . 0\n. . . . . . 0\n. . . . . . 0 E\n";

        private const float CellSize = 44f;
        private const float EdgeSize = 24f;
        private const float ClueSize = 44f;
        private const float Gap = 2f;

        [SerializeField] private string _levelText;
        [SerializeField] private string _levelId = "";
        [SerializeField] private LevelDefinition _asset;
        [SerializeField] private LevelCollection _collection;
        [SerializeField] private UndoState _undoState;

        private LevelData _level;
        private Tool _tool = Tool.NS;
        private SolveResult _solveResult;
        private bool _solveIsDeep;

        // Board widgets, rebuilt when the size changes and refreshed otherwise.
        private VisualElement _boardRoot;
        private int _builtWidth = -1;
        private int _builtHeight = -1;
        private CellView[,] _cells;
        private IntegerField[] _columnClueFields;
        private IntegerField[] _rowClueFields;
        private readonly Dictionary<Tunnel, Button> _edgeButtons = new Dictionary<Tunnel, Button>();
        private readonly Dictionary<Tool, Button> _toolButtons = new Dictionary<Tool, Button>();

        // Side panel widgets.
        private TextField _nameField;
        private TextField _idField;
        private IntegerField _widthField;
        private IntegerField _heightField;
        private Label _trackLabel;
        private HelpBox _problemsBox;
        private VisualElement _solveBanner;
        private Label _solveLabel;
        private Button _deepSolveButton;
        private TextField _textField;
        private HelpBox _textErrorBox;
        private ObjectField _assetField;
        private Button _saveButton;
        private Button _loadButton;
        private ObjectField _collectionField;
        private Button _addToCollectionButton;
        private IVisualElementScheduledItem _solveJob;

        [MenuItem("Window/TrainSudoku/Level Editor")]
        public static LevelEditorWindow Open()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(720, 480);
            return window;
        }

        public static void Open(LevelDefinition asset)
        {
            var window = Open();
            if (asset == null) return;
            window._asset = asset;
            window.LoadFromAsset();
            if (window._assetField != null) window._assetField.SetValueWithoutNotify(asset);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _solveJob?.Pause();
        }

        private void OnDestroy()
        {
            if (_undoState != null) DestroyImmediate(_undoState);
        }

        private void CreateGUI()
        {
            RestoreLevel();

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            var boardScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            boardScroll.style.flexGrow = 1;
            boardScroll.contentContainer.style.paddingTop = 8;
            boardScroll.contentContainer.style.paddingLeft = 8;
            boardScroll.contentContainer.style.paddingRight = 8;
            boardScroll.contentContainer.style.paddingBottom = 8;
            boardScroll.Add(BuildToolbar());
            _boardRoot = new VisualElement();
            _boardRoot.style.marginTop = 8;
            boardScroll.Add(_boardRoot);
            var hint = new Label("Left click a cell paints the selected piece (click again to remove). Right click erases. " +
                                 "Click an edge to move the entrance there; shift-click or the E tool moves the exit.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 8;
            hint.style.opacity = 0.7f;
            hint.style.maxWidth = 460;
            boardScroll.Add(hint);
            root.Add(boardScroll);

            var side = new ScrollView(ScrollViewMode.Vertical);
            side.style.width = 320;
            side.style.minWidth = 320;
            side.style.borderLeftWidth = 1;
            side.style.borderLeftColor = new Color(0, 0, 0, 0.35f);
            side.contentContainer.style.paddingTop = 6;
            side.contentContainer.style.paddingLeft = 8;
            side.contentContainer.style.paddingRight = 8;
            side.contentContainer.style.paddingBottom = 8;
            side.Add(BuildLevelSection());
            side.Add(BuildValidationSection());
            side.Add(BuildSolutionSection());
            side.Add(BuildTextSection());
            side.Add(BuildAssetSection());
            root.Add(side);

            _solveJob = root.schedule.Execute(() => RunSolver(QuickSolveBudget));
            _solveJob.Pause();

            Refresh();
        }

        // ------------------------------------------------------------------ state

        /// <summary>Holds the level text so that Undo.RecordObject and domain reloads work on plain Unity serialisation.</summary>
        private sealed class UndoState : ScriptableObject
        {
            public string levelText;
            public string levelId;
        }

        private void RestoreLevel()
        {
            if (_undoState == null)
            {
                _undoState = CreateInstance<UndoState>();
                _undoState.hideFlags = HideFlags.HideAndDontSave;
                _undoState.levelText = _levelText;
                _undoState.levelId = _levelId;
            }

            _level = TryParse(_undoState.levelText) ?? TryParse(_levelText) ?? LevelText.Parse(DefaultLevelText);
            _levelId = _undoState.levelId ?? _levelId ?? "";
            _levelText = LevelText.Serialize(_level);
            _undoState.levelText = _levelText;
            _undoState.levelId = _levelId;
        }

        private static LevelData TryParse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                return LevelText.Parse(text);
            }
            catch (LevelTextException)
            {
                return null;
            }
        }

        /// <summary>Call after mutating <see cref="_level"/> or <see cref="_levelId"/>: records undo, stores the text and refreshes the UI.</summary>
        private void Commit(string undoName)
        {
            Undo.RecordObject(_undoState, undoName);
            _levelText = LevelText.Serialize(_level);
            _undoState.levelText = _levelText;
            _undoState.levelId = _levelId;
            Refresh();
        }

        private void ReplaceLevel(LevelData level, string undoName)
        {
            _level = level;
            Commit(undoName);
        }

        private void OnUndoRedo()
        {
            if (_undoState == null || _boardRoot == null) return;
            if (_undoState.levelText == _levelText && _undoState.levelId == _levelId) return;

            var restored = TryParse(_undoState.levelText);
            if (restored == null) return;
            _level = restored;
            _levelText = _undoState.levelText;
            _levelId = _undoState.levelId ?? "";
            Refresh();
        }

        // ------------------------------------------------------------------ toolbar and board

        private VisualElement BuildToolbar()
        {
            var bar = Row();
            bar.style.flexWrap = Wrap.Wrap;
            foreach (Tool tool in Enum.GetValues(typeof(Tool)))
            {
                var captured = tool;
                var button = new Button(() => SelectTool(captured)) { text = ToolLabel(tool), tooltip = ToolTooltip(tool) };
                button.style.minWidth = 44;
                button.style.height = 26;
                _toolButtons[tool] = button;
                bar.Add(button);
            }

            return bar;
        }

        private static string ToolLabel(Tool tool)
        {
            switch (tool)
            {
                case Tool.Erase: return "Erase";
                case Tool.Entrance: return "S";
                case Tool.Exit: return "E";
                default: return tool.ToString();
            }
        }

        private static string ToolTooltip(Tool tool)
        {
            switch (tool)
            {
                case Tool.Erase: return "Remove the fixed piece from a cell";
                case Tool.Entrance: return "Click an edge to place the entrance tunnel";
                case Tool.Exit: return "Click an edge to place the exit tunnel";
                default: return $"Fixed piece connecting {tool}";
            }
        }

        private void SelectTool(Tool tool)
        {
            _tool = tool;
            foreach (var pair in _toolButtons)
            {
                var selected = pair.Key == tool;
                pair.Value.style.backgroundColor = selected
                    ? new StyleColor(new Color(0.24f, 0.48f, 0.90f))
                    : new StyleColor(StyleKeyword.Null);
                pair.Value.style.color = selected ? new StyleColor(Color.white) : new StyleColor(StyleKeyword.Null);
            }
        }

        private void RebuildBoard()
        {
            _boardRoot.Clear();
            _edgeButtons.Clear();
            var w = _level.Width;
            var h = _level.Height;
            _builtWidth = w;
            _builtHeight = h;
            _cells = new CellView[w, h];
            _columnClueFields = new IntegerField[w];
            _rowClueFields = new IntegerField[h];

            // Top edge.
            var top = Row();
            top.Add(Spacer(EdgeSize));
            for (var x = 0; x < w; x++) top.Add(EdgeButton(new Tunnel(Direction.North, x), CellSize, EdgeSize));
            top.Add(Spacer(ClueSize));
            top.Add(Spacer(EdgeSize));
            _boardRoot.Add(top);

            // Column clues.
            var clues = Row();
            clues.Add(Spacer(EdgeSize));
            for (var x = 0; x < w; x++)
            {
                var column = x;
                _columnClueFields[x] = ClueField(v => SetColumnClue(column, v));
                clues.Add(_columnClueFields[x]);
            }

            clues.Add(Spacer(ClueSize));
            clues.Add(Spacer(EdgeSize));
            _boardRoot.Add(clues);

            // Cell rows.
            for (var y = 0; y < h; y++)
            {
                var row = Row();
                row.Add(EdgeButton(new Tunnel(Direction.West, y), EdgeSize, CellSize));
                for (var x = 0; x < w; x++)
                {
                    var cell = new CellView(x, y);
                    cell.style.width = CellSize;
                    cell.style.height = CellSize;
                    cell.style.marginRight = Gap;
                    cell.style.marginBottom = Gap;
                    var cx = x;
                    var cy = y;
                    cell.RegisterCallback<ClickEvent>(evt => OnCellClicked(cx, cy));
                    cell.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button == 1) EraseCell(cx, cy);
                    });
                    _cells[x, y] = cell;
                    row.Add(cell);
                }

                var rowIndex = y;
                _rowClueFields[y] = ClueField(v => SetRowClue(rowIndex, v));
                row.Add(_rowClueFields[y]);
                row.Add(EdgeButton(new Tunnel(Direction.East, y), EdgeSize, CellSize));
                _boardRoot.Add(row);
            }

            // Bottom edge.
            var bottom = Row();
            bottom.Add(Spacer(EdgeSize));
            for (var x = 0; x < w; x++) bottom.Add(EdgeButton(new Tunnel(Direction.South, x), CellSize, EdgeSize));
            bottom.Add(Spacer(ClueSize));
            bottom.Add(Spacer(EdgeSize));
            _boardRoot.Add(bottom);
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static VisualElement Spacer(float width)
        {
            var spacer = new VisualElement();
            spacer.style.width = width;
            spacer.style.marginRight = Gap;
            spacer.style.flexShrink = 0;
            return spacer;
        }

        private Button EdgeButton(Tunnel tunnel, float width, float height)
        {
            var button = new Button();
            button.style.width = width;
            button.style.height = height;
            button.style.marginLeft = 0;
            button.style.marginRight = Gap;
            button.style.marginTop = 0;
            button.style.marginBottom = Gap;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.RegisterCallback<ClickEvent>(evt => OnEdgeClicked(tunnel, evt.shiftKey));
            _edgeButtons[tunnel] = button;
            return button;
        }

        private static IntegerField ClueField(Action<int> onChanged)
        {
            var field = new IntegerField { isDelayed = true };
            field.style.width = ClueSize;
            field.style.height = CellSize;
            field.style.marginLeft = 0;
            field.style.marginRight = Gap;
            field.style.marginTop = 0;
            field.style.marginBottom = Gap;
            var input = field.Q(TextField.textInputUssName);
            if (input != null) input.style.unityTextAlign = TextAnchor.MiddleCenter;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private void RefreshBoard()
        {
            if (_builtWidth != _level.Width || _builtHeight != _level.Height) RebuildBoard();

            var board = new Board(_level);
            var result = WinChecker.Evaluate(board);
            var onPath = new HashSet<(int, int)>(result.Path);

            for (var y = 0; y < _level.Height; y++)
            for (var x = 0; x < _level.Width; x++)
            {
                var cell = _cells[x, y];
                cell.Key = _level.TryGetFixedPiece(x, y, out var piece) ? piece.Key : (PieceKey?)null;
                cell.OnPath = onPath.Contains((x, y));
                cell.tooltip = cell.Key.HasValue ? $"({x},{y}) {cell.Key}" : $"({x},{y})";
                cell.MarkDirtyRepaint();
            }

            for (var x = 0; x < _level.Width; x++)
                RefreshClue(_columnClueFields[x], _level.ColumnClues[x], board.ColumnCount(x), _level.Height);
            for (var y = 0; y < _level.Height; y++)
                RefreshClue(_rowClueFields[y], _level.RowClues[y], board.RowCount(y), _level.Width);

            foreach (var pair in _edgeButtons)
            {
                var isEntrance = pair.Key == _level.Entrance;
                var isExit = pair.Key == _level.Exit;
                pair.Value.text = isEntrance ? "S" : isExit ? "E" : "";
                pair.Value.tooltip = isEntrance ? "Entrance" : isExit ? "Exit" : $"{pair.Key.Side} edge {pair.Key.Index}";
                pair.Value.style.backgroundColor = isEntrance
                    ? new StyleColor(new Color(0.20f, 0.60f, 0.30f))
                    : isExit
                        ? new StyleColor(new Color(0.80f, 0.40f, 0.20f))
                        : new StyleColor(StyleKeyword.Null);
            }

            _trackLabel.text = TrackStatus(result, board.PieceCount);
        }

        private static void RefreshClue(IntegerField field, int clue, int fixedCount, int max)
        {
            field.SetValueWithoutNotify(clue);
            Color color;
            if (clue < 0 || clue > max || fixedCount > clue) color = new Color(0.95f, 0.35f, 0.35f);
            else if (fixedCount == clue && clue > 0) color = new Color(0.35f, 0.80f, 0.45f);
            else color = new Color(0, 0, 0, 0);
            var input = field.Q(TextField.textInputUssName);
            if (input == null) return;
            input.style.color = color.a > 0 ? new StyleColor(color) : new StyleColor(StyleKeyword.Null);
            input.style.unityFontStyleAndWeight = color.a > 0 ? FontStyle.Bold : FontStyle.Normal;
        }

        private static string TrackStatus(WinResult result, int pieceCount)
        {
            if (pieceCount == 0) return "No fixed pieces.";
            var track = result.PathConnected
                ? $"Track runs S to E through {result.Path.Count} cells."
                : $"Track from S covers {result.Path.Count} cells and does not reach E.";
            var ends = result.NoOpenEnds ? "No open ends." : "Some pieces have open ends.";
            var clues = result.CluesSatisfied ? "Clues match the pieces." : "Clues differ from the piece counts.";
            return $"{track} {ends} {clues}";
        }

        // ------------------------------------------------------------------ board interaction

        private void OnCellClicked(int x, int y)
        {
            switch (_tool)
            {
                case Tool.Erase:
                    EraseCell(x, y);
                    return;
                case Tool.Entrance:
                case Tool.Exit:
                    return;
            }

            var key = (PieceKey)Enum.Parse(typeof(PieceKey), _tool.ToString());
            var current = _level.TryGetFixedPiece(x, y, out var existing) ? existing.Key : (PieceKey?)null;
            LevelAuthoring.SetFixedPiece(_level, x, y, current == key ? (PieceKey?)null : key);
            Commit(current == key ? "Remove fixed piece" : "Place fixed piece");
        }

        private void EraseCell(int x, int y)
        {
            if (!_level.TryGetFixedPiece(x, y, out _)) return;
            LevelAuthoring.SetFixedPiece(_level, x, y, null);
            Commit("Remove fixed piece");
        }

        private void OnEdgeClicked(Tunnel tunnel, bool shift)
        {
            var moveExit = _tool == Tool.Exit || shift;
            if (moveExit)
            {
                if (_level.Exit == tunnel) return;
                if (_level.Entrance == tunnel) _level.Entrance = _level.Exit;
                _level.Exit = tunnel;
                Commit("Move exit");
            }
            else
            {
                if (_level.Entrance == tunnel) return;
                if (_level.Exit == tunnel) _level.Exit = _level.Entrance;
                _level.Entrance = tunnel;
                Commit("Move entrance");
            }
        }

        private void SetColumnClue(int x, int value)
        {
            if (_level.ColumnClues[x] == value) return;
            _level.ColumnClues[x] = value;
            Commit("Set column clue");
        }

        private void SetRowClue(int y, int value)
        {
            if (_level.RowClues[y] == value) return;
            _level.RowClues[y] = value;
            Commit("Set row clue");
        }

        // ------------------------------------------------------------------ side panel: level

        private VisualElement BuildLevelSection()
        {
            var section = Section("Level");

            _nameField = new TextField("Name") { isDelayed = true };
            _nameField.RegisterValueChangedCallback(evt =>
            {
                var oldSuggested = LevelAuthoring.SuggestId(_level.Name);
                _level.Name = evt.newValue.Trim();
                if (string.IsNullOrEmpty(_levelId) || _levelId == oldSuggested) _levelId = LevelAuthoring.SuggestId(_level.Name);
                Commit("Rename level");
            });
            section.Add(_nameField);

            _idField = new TextField("Id") { isDelayed = true, tooltip = "Stable identity for the save file. Never change it after release." };
            _idField.RegisterValueChangedCallback(evt =>
            {
                _levelId = evt.newValue.Trim();
                Commit("Change level id");
            });
            section.Add(_idField);

            var size = Row();
            _widthField = new IntegerField("Width") { isDelayed = true };
            _widthField.style.flexGrow = 1;
            _widthField.labelElement.style.minWidth = 44;
            _widthField.RegisterValueChangedCallback(evt => Resize(evt.newValue, _level.Height));
            _heightField = new IntegerField("Height") { isDelayed = true };
            _heightField.style.flexGrow = 1;
            _heightField.labelElement.style.minWidth = 48;
            _heightField.RegisterValueChangedCallback(evt => Resize(_level.Width, evt.newValue));
            size.Add(_widthField);
            size.Add(_heightField);
            section.Add(size);

            var buttons = Row();
            buttons.Add(new Button(NewLevel) { text = "New level" });
            buttons.Add(new Button(() =>
            {
                _level.FixedPieces.Clear();
                Commit("Clear pieces");
            }) { text = "Clear pieces" });
            buttons.Add(new Button(() =>
            {
                Array.Clear(_level.ColumnClues, 0, _level.Width);
                Array.Clear(_level.RowClues, 0, _level.Height);
                Commit("Clear clues");
            }) { text = "Clear clues" });
            section.Add(buttons);

            return section;
        }

        private void NewLevel()
        {
            if (!EditorUtility.DisplayDialog("New level", "Replace the current level with an empty 6x6 board?", "New level", "Cancel")) return;
            _asset = null;
            _assetField?.SetValueWithoutNotify(null);
            _levelId = "";
            ReplaceLevel(LevelText.Parse(DefaultLevelText), "New level");
        }

        private void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            if (width == _level.Width && height == _level.Height)
            {
                _widthField.SetValueWithoutNotify(width);
                _heightField.SetValueWithoutNotify(height);
                return;
            }

            ReplaceLevel(LevelAuthoring.Resize(_level, width, height), "Resize level");
        }

        // ------------------------------------------------------------------ side panel: validation

        private VisualElement BuildValidationSection()
        {
            var section = Section("Validation");

            _solveBanner = new VisualElement();
            _solveBanner.style.paddingTop = 6;
            _solveBanner.style.paddingBottom = 6;
            _solveBanner.style.paddingLeft = 8;
            _solveBanner.style.paddingRight = 8;
            _solveBanner.style.borderTopLeftRadius = 4;
            _solveBanner.style.borderTopRightRadius = 4;
            _solveBanner.style.borderBottomLeftRadius = 4;
            _solveBanner.style.borderBottomRightRadius = 4;
            _solveBanner.style.marginBottom = 4;
            _solveLabel = new Label();
            _solveLabel.style.whiteSpace = WhiteSpace.Normal;
            _solveLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _solveBanner.Add(_solveLabel);
            section.Add(_solveBanner);

            _deepSolveButton = new Button(() => RunSolver(DeepSolveBudget)) { text = "Search longer" };
            section.Add(_deepSolveButton);

            _problemsBox = new HelpBox("", HelpBoxMessageType.Error);
            section.Add(_problemsBox);

            _trackLabel = new Label();
            _trackLabel.style.whiteSpace = WhiteSpace.Normal;
            _trackLabel.style.marginTop = 4;
            section.Add(_trackLabel);

            return section;
        }

        private void RefreshValidation()
        {
            var problems = _level.Validate();
            if (string.IsNullOrEmpty(_levelId))
            {
                var list = new List<string>(problems) { "The level has no id; the save file needs one." };
                problems = list;
            }

            _problemsBox.style.display = problems.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _problemsBox.text = string.Join("\n", problems);

            _solveResult = null;
            _solveIsDeep = false;
            ShowSolveBanner("Solving...", new Color(0.35f, 0.35f, 0.35f));
            _deepSolveButton.SetEnabled(false);
            _solveJob?.ExecuteLater(120);
        }

        private void RunSolver(long budget)
        {
            _solveIsDeep = budget >= DeepSolveBudget;
            _solveResult = Solver.Solve(_level, Solver.DefaultLimit, budget);
            var r = _solveResult;

            if (r.Exhausted)
            {
                var found = r.Count == 0 ? "no solution found yet" : r.Count == 1 ? "one solution found so far" : "several solutions found";
                ShowSolveBanner($"Search unfinished after {r.Nodes:N0} nodes: {found}.", new Color(0.45f, 0.45f, 0.45f));
                _deepSolveButton.SetEnabled(!_solveIsDeep);
                return;
            }

            _deepSolveButton.SetEnabled(false);
            if (r.Count == 0) ShowSolveBanner("No solution. The puzzle cannot be completed.", new Color(0.75f, 0.20f, 0.20f));
            else if (r.Count == 1) ShowSolveBanner("Exactly one solution.", new Color(0.20f, 0.55f, 0.30f));
            else ShowSolveBanner("2+ solutions. The puzzle is ambiguous; saving is allowed.", new Color(0.75f, 0.55f, 0.15f));
        }

        private void ShowSolveBanner(string text, Color background)
        {
            _solveLabel.text = text;
            _solveBanner.style.backgroundColor = background;
            _solveLabel.style.color = Color.white;
        }

        // ------------------------------------------------------------------ side panel: author by solution

        private VisualElement BuildSolutionSection()
        {
            var section = Section("Author by solution");
            var info = new Label("Draw the full track with the piece tools, derive the clues, then remove pieces until the puzzle is interesting but still has one solution.");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.opacity = 0.8f;
            info.style.marginBottom = 4;
            section.Add(info);

            var buttons = Row();
            buttons.Add(new Button(() =>
            {
                LevelAuthoring.DeriveClues(_level);
                Commit("Derive clues");
            }) { text = "Derive clues from pieces", tooltip = "Set every clue to the number of fixed pieces in that line" });
            buttons.Add(new Button(FillFromSolver) { text = "Fill from solver", tooltip = "Make every piece of the first solution fixed" });
            section.Add(buttons);
            return section;
        }

        private void FillFromSolver()
        {
            var result = Solver.Solve(_level, 1, DeepSolveBudget);
            if (result.First == null)
            {
                EditorUtility.DisplayDialog("Fill from solver",
                    result.Exhausted ? "The search ran out of budget before finding a solution." : "This level has no solution.", "OK");
                return;
            }

            LevelAuthoring.SetFixedPiecesFrom(_level, result.First);
            Commit("Fill from solver");
        }

        // ------------------------------------------------------------------ side panel: text

        private VisualElement BuildTextSection()
        {
            var section = Section("Text");
            _textField = new TextField { multiline = true };
            _textField.style.minHeight = 150;
            _textField.style.whiteSpace = WhiteSpace.Pre;
            var input = _textField.Q(TextField.textInputUssName);
            if (input != null)
            {
                var font = MonospaceFont();
                if (font != null) input.style.unityFontDefinition = FontDefinition.FromFont(font);
                input.style.whiteSpace = WhiteSpace.Pre;
            }

            section.Add(_textField);

            _textErrorBox = new HelpBox("", HelpBoxMessageType.Error);
            _textErrorBox.style.display = DisplayStyle.None;
            section.Add(_textErrorBox);

            var buttons = Row();
            buttons.Add(new Button(ImportText) { text = "Import from text" });
            buttons.Add(new Button(() =>
            {
                _textField.value = LevelText.Serialize(_level);
                _textErrorBox.style.display = DisplayStyle.None;
            }) { text = "Export to text" });
            buttons.Add(new Button(() => GUIUtility.systemCopyBuffer = LevelText.Serialize(_level)) { text = "Copy" });
            section.Add(buttons);
            return section;
        }

        private static Font MonospaceFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, 12);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ImportText()
        {
            try
            {
                var level = LevelText.Parse(_textField.value ?? "");
                _textErrorBox.style.display = DisplayStyle.None;
                if (!string.IsNullOrEmpty(level.Name) && (string.IsNullOrEmpty(_levelId) || _levelId == LevelAuthoring.SuggestId(_level.Name)))
                    _levelId = LevelAuthoring.SuggestId(level.Name);
                ReplaceLevel(level, "Import level text");
            }
            catch (LevelTextException e)
            {
                _textErrorBox.text = e.Message;
                _textErrorBox.style.display = DisplayStyle.Flex;
            }
        }

        // ------------------------------------------------------------------ side panel: assets

        private VisualElement BuildAssetSection()
        {
            var section = Section("Asset");

            _assetField = new ObjectField("Level asset") { objectType = typeof(LevelDefinition), allowSceneObjects = false };
            _assetField.RegisterValueChangedCallback(evt =>
            {
                _asset = evt.newValue as LevelDefinition;
                RefreshAssetButtons();
            });
            section.Add(_assetField);

            var levelButtons = Row();
            _loadButton = new Button(LoadFromAsset) { text = "Load" , tooltip = "Replace the current level with the asset's content" };
            _saveButton = new Button(() => SaveToAsset(_asset)) { text = "Save", tooltip = "Overwrite the asset with the current level" };
            levelButtons.Add(_loadButton);
            levelButtons.Add(_saveButton);
            levelButtons.Add(new Button(SaveAsNewAsset) { text = "Save as new..." });
            section.Add(levelButtons);

            _collectionField = new ObjectField("Collection") { objectType = typeof(LevelCollection), allowSceneObjects = false };
            _collectionField.RegisterValueChangedCallback(evt =>
            {
                _collection = evt.newValue as LevelCollection;
                RefreshAssetButtons();
            });
            _collectionField.style.marginTop = 6;
            section.Add(_collectionField);

            _addToCollectionButton = new Button(AddToCollection) { text = "Add level asset to collection" };
            section.Add(_addToCollectionButton);

            return section;
        }

        private void RefreshAssetButtons()
        {
            _assetField.SetValueWithoutNotify(_asset);
            _collectionField.SetValueWithoutNotify(_collection);
            _loadButton.SetEnabled(_asset != null);
            _saveButton.SetEnabled(_asset != null);
            _addToCollectionButton.SetEnabled(_asset != null && _collection != null && !_collection.Contains(_asset));
        }

        private void LoadFromAsset()
        {
            if (_asset == null) return;
            _levelId = _asset.Id ?? "";
            var loaded = _asset.ToLevelData();
            // A hand-edited asset may hold pieces or tunnels outside the board; Resize to the same size drops and clamps them.
            _level = LevelAuthoring.Resize(loaded, loaded.Width, loaded.Height);
            if (_boardRoot != null)
            {
                Commit("Load level asset");
                return;
            }

            // CreateGUI has not run yet; leave the text where RestoreLevel will find it.
            _levelText = LevelText.Serialize(_level);
            if (_undoState != null)
            {
                _undoState.levelText = _levelText;
                _undoState.levelId = _levelId;
            }
        }

        private bool ConfirmSave()
        {
            var problems = new List<string>(_level.Validate());
            if (string.IsNullOrEmpty(_levelId)) problems.Add("The level has no id.");
            if (_solveResult != null && !_solveResult.Exhausted && _solveResult.Count == 0) problems.Add("The solver found no solution.");
            if (problems.Count == 0) return true;
            return EditorUtility.DisplayDialog("Level has problems", string.Join("\n", problems) + "\n\nSave anyway?", "Save anyway", "Cancel");
        }

        private void SaveToAsset(LevelDefinition asset)
        {
            if (asset == null || !ConfirmSave()) return;
            Undo.RecordObject(asset, "Save level");
            asset.SetFrom(_level, _levelId);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            _asset = asset;
            RefreshAssetButtons();
        }

        private void SaveAsNewAsset()
        {
            if (!ConfirmSave()) return;
            EnsureFolder(DefaultLevelFolder);
            var fileName = string.IsNullOrEmpty(_level.Name) ? "Level" : _level.Name;
            var path = EditorUtility.SaveFilePanelInProject("Save level asset", fileName, "asset", "Choose where to save the level", DefaultLevelFolder);
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<LevelDefinition>();
            asset.SetFrom(_level, _levelId);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _asset = asset;
            RefreshAssetButtons();
            EditorGUIUtility.PingObject(asset);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private void AddToCollection()
        {
            if (_asset == null || _collection == null) return;
            Undo.RecordObject(_collection, "Add level to collection");
            if (_collection.Add(_asset))
            {
                EditorUtility.SetDirty(_collection);
                AssetDatabase.SaveAssetIfDirty(_collection);
            }

            RefreshAssetButtons();
        }

        // ------------------------------------------------------------------ refresh

        private void Refresh()
        {
            if (_boardRoot == null) return;
            _nameField.SetValueWithoutNotify(_level.Name);
            _idField.SetValueWithoutNotify(_levelId);
            _widthField.SetValueWithoutNotify(_level.Width);
            _heightField.SetValueWithoutNotify(_level.Height);
            SelectTool(_tool);
            RefreshBoard();
            RefreshValidation();
            RefreshAssetButtons();
            if (string.IsNullOrEmpty(_textField.value)) _textField.SetValueWithoutNotify(_levelText);
        }

        private static Foldout Section(string title)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.style.marginBottom = 6;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
            return foldout;
        }

        // ------------------------------------------------------------------ cell drawing

        /// <summary>One board cell. Draws the fixed piece's two connections from the side midpoints; curves are quarter circles.</summary>
        private sealed class CellView : VisualElement
        {
            private static readonly Color EmptyBackground = new Color(0.82f, 0.82f, 0.82f);
            private static readonly Color PieceColor = new Color(0.16f, 0.16f, 0.18f);
            private static readonly Color PathColor = new Color(0.12f, 0.60f, 0.25f);

            public int X { get; }
            public int Y { get; }
            public PieceKey? Key { get; set; }
            public bool OnPath { get; set; }

            public CellView(int x, int y)
            {
                X = x;
                Y = y;
                style.backgroundColor = EmptyBackground;
                style.borderTopLeftRadius = 3;
                style.borderTopRightRadius = 3;
                style.borderBottomLeftRadius = 3;
                style.borderBottomRightRadius = 3;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext ctx)
            {
                if (!Key.HasValue) return;

                var rect = contentRect;
                var w = rect.width;
                var h = rect.height;
                var center = new Vector2(w / 2f, h / 2f);
                var p = ctx.painter2D;
                p.strokeColor = OnPath ? PathColor : PieceColor;
                p.lineWidth = Mathf.Max(3f, w * 0.16f);
                p.lineCap = LineCap.Round;
                p.BeginPath();

                var (a, b) = PieceKeys.Connections(Key.Value);
                if (a == b.Opposite())
                {
                    p.MoveTo(Midpoint(a, w, h));
                    p.LineTo(Midpoint(b, w, h));
                }
                else
                {
                    var corner = new Vector2(
                        a == Direction.East || b == Direction.East ? w : 0f,
                        a == Direction.South || b == Direction.South ? h : 0f);
                    var angleA = AngleFrom(corner, Midpoint(a, w, h));
                    var angleB = AngleFrom(corner, Midpoint(b, w, h));
                    var start = Mathf.Min(angleA, angleB);
                    var end = Mathf.Max(angleA, angleB);
                    if (end - start > 180f)
                    {
                        var swap = start + 360f;
                        start = end;
                        end = swap;
                    }

                    p.Arc(corner, w / 2f, Angle.Degrees(start), Angle.Degrees(end));
                }

                p.Stroke();

                // A dot in the centre marks the piece as fixed, so it reads at a glance even with the track colour.
                p.fillColor = p.strokeColor;
                p.BeginPath();
                p.Arc(center, p.lineWidth * 0.45f, Angle.Degrees(0), Angle.Degrees(360));
                p.Fill();
            }

            private static Vector2 Midpoint(Direction side, float w, float h)
            {
                switch (side)
                {
                    case Direction.North: return new Vector2(w / 2f, 0f);
                    case Direction.East: return new Vector2(w, h / 2f);
                    case Direction.South: return new Vector2(w / 2f, h);
                    default: return new Vector2(0f, h / 2f);
                }
            }

            private static float AngleFrom(Vector2 from, Vector2 to)
            {
                var d = to - from;
                return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
        }
    }
}
