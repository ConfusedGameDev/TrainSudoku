using System;
using System.Collections.Generic;
using System.Linq;
using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Line Map Editor (work order 7.3): drag a line's map nodes on a 45-degree grid, put its stations on them, set
    /// its colour and code, watch the runtime draw at device aspect, and validate the result.
    /// </summary>
    /// <remarks>
    /// Without this the maps are hand-typed coordinates in a <c>.asset</c> file, which is how the mockups were made
    /// and does not survive five lines.
    ///
    /// Edits are buffered in this window's own serialised fields — that is what makes Undo and domain reloads work —
    /// and reach the asset only on Save, through a <see cref="SerializedObject"/> so the asset's own Undo entry is
    /// recorded for free and <c>Game</c> needs no new API.
    /// </remarks>
    public sealed class LineMapEditorWindow : EditorWindow
    {
        private const string DefaultNetworkPath = "Assets/03.Data/Levels/Network.asset";

        /// <summary>Portrait aspects worth checking the draw against, as width:height.</summary>
        private static readonly (string Label, float Aspect)[] Aspects =
        {
            ("9:16", 9f / 16f),
            ("9:19.5 (notch)", 9f / 19.5f),
            ("9:21", 9f / 21f),
            ("3:4 (tablet)", 3f / 4f),
        };

        [SerializeField] private LineDefinition _asset;
        [SerializeField] private NetworkDefinition _network;
        [SerializeField] private Vector2[] _nodes = Array.Empty<Vector2>();
        [SerializeField] private int[] _stations = Array.Empty<int>();
        [SerializeField] private MapShape _shape = MapShape.Route;
        [SerializeField] private string _code = "";
        [SerializeField] private string _displayName = "";
        [SerializeField] private Color _color = new Color32(0x9A, 0xCD, 0x32, 0xFF);
        [SerializeField] private float _grid = 50f;
        [SerializeField] private bool _snap = true;
        [SerializeField] private bool _dirty;
        [SerializeField] private int _selected = -1;
        [SerializeField] private int _armed = -1;
        [SerializeField] private int _aspect = 1;

        private MapCanvas _canvas;
        private LineDefinition _preview;
        private LineMapElement _previewMap;
        private VisualElement _previewFrame;
        private ObjectField _assetField;
        private ObjectField _networkField;
        private TextField _codeField;
        private TextField _nameField;
        private ColorField _colorField;
        private EnumField _shapeField;
        private FloatField _gridField;
        private Toggle _snapToggle;
        private Label _nodeCount;
        private VisualElement _stationList;
        private int _builtStations = -1;
        private HelpBox _problems;
        private HelpBox _networkProblems;
        private Button _saveButton;
        private Button _revertButton;
        private Label _hint;

        [MenuItem("Window/TrainSudoku/Line Map Editor")]
        public static LineMapEditorWindow Open()
        {
            var window = GetWindow<LineMapEditorWindow>("Line Map");
            window.minSize = new Vector2(880, 520);
            return window;
        }

        public static void Open(LineDefinition line)
        {
            var window = Open();
            if (line == null) return;
            window._asset = line;
            window.LoadFromAsset();
            window.RefreshAll();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            if (_network == null) _network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(DefaultNetworkPath);
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnDestroy()
        {
            if (_preview != null) DestroyImmediate(_preview);
        }

        private void OnUndoRedo()
        {
            _builtStations = -1;   // the arrays may have changed length under us
            RefreshAll();
        }

        // ------------------------------------------------------------------ construction

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            var left = new VisualElement { style = { flexGrow = 1f, flexDirection = FlexDirection.Column } };
            left.Add(BuildToolbar());
            _canvas = new MapCanvas(this) { style = { flexGrow = 1f, marginTop = 4, marginLeft = 4, marginRight = 4 } };
            left.Add(_canvas);
            _hint = new Label { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.7f, marginLeft = 6, marginTop = 4, marginBottom = 6 } };
            left.Add(_hint);
            root.Add(left);

            var side = new ScrollView(ScrollViewMode.Vertical);
            side.style.width = 340;
            side.style.minWidth = 340;
            side.style.borderLeftWidth = 1;
            side.style.borderLeftColor = new Color(0f, 0f, 0f, 0.35f);
            side.contentContainer.style.paddingTop = 6;
            side.contentContainer.style.paddingLeft = 8;
            side.contentContainer.style.paddingRight = 8;
            side.contentContainer.style.paddingBottom = 8;
            side.Add(BuildLineSection());
            side.Add(BuildStationSection());
            side.Add(BuildValidationSection());
            side.Add(BuildPreviewSection());
            root.Add(side);

            if (_asset != null && _nodes.Length == 0) LoadFromAsset();
            RefreshAll();
            _canvas.schedule.Execute(() => _canvas.Frame());
        }

        private VisualElement BuildToolbar()
        {
            var bar = Row();
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.marginLeft = 4;
            bar.style.marginTop = 4;

            bar.Add(new Button(AppendNode) { text = "Add node", tooltip = "Append a node past the last one" });
            bar.Add(new Button(InsertAfterSelected) { text = "Insert", tooltip = "Split the segment after the selected node" });
            bar.Add(new Button(DeleteSelected) { text = "Delete", tooltip = "Remove the selected node" });
            bar.Add(new Button(SnapAllToGrid) { text = "Snap all", tooltip = "Pull every node onto the grid" });
            bar.Add(new Button(Straighten) { text = "Straighten", tooltip = "Turn every illegal segment into an axis run and a 45-degree run, moving no node" });
            bar.Add(new Button(() => _canvas.Frame()) { text = "Frame" });

            _shapeField = new EnumField(MapShape.Route) { tooltip = "A route runs terminus to terminus; a loop joins the last node back to the first" };
            _shapeField.style.width = 90;
            _shapeField.RegisterValueChangedCallback(evt =>
            {
                RecordUndo("Change map shape");
                _shape = (MapShape)evt.newValue;
                Touch();
            });
            bar.Add(_shapeField);

            _gridField = new FloatField("Grid") { value = _grid, tooltip = "Lattice spacing in map units" };
            _gridField.style.width = 110;
            _gridField.RegisterValueChangedCallback(evt =>
            {
                _grid = Mathf.Max(1f, evt.newValue);
                RefreshAll();
            });
            bar.Add(_gridField);

            _snapToggle = new Toggle("45°") { value = _snap, tooltip = "Hold a dragged node to the eight compass directions out of its neighbours" };
            _snapToggle.style.marginLeft = 6;
            _snapToggle.RegisterValueChangedCallback(evt => _snap = evt.newValue);
            bar.Add(_snapToggle);

            return bar;
        }

        private VisualElement BuildLineSection()
        {
            var section = Section("Line");

            _assetField = new ObjectField("Asset") { objectType = typeof(LineDefinition), allowSceneObjects = false };
            _assetField.RegisterValueChangedCallback(evt =>
            {
                _asset = evt.newValue as LineDefinition;
                LoadFromAsset();
                RefreshAll();
            });
            section.Add(_assetField);

            _networkField = new ObjectField("Network") { objectType = typeof(NetworkDefinition), allowSceneObjects = false, tooltip = "Drawn behind the edited line, and validated with it" };
            _networkField.RegisterValueChangedCallback(evt =>
            {
                _network = evt.newValue as NetworkDefinition;
                RefreshAll();
            });
            section.Add(_networkField);

            _codeField = new TextField("Code") { tooltip = "The roundel's two letters" };
            _codeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _code) return;
                RecordUndo("Change line code");
                _code = evt.newValue;
                Touch();
            });
            section.Add(_codeField);

            _nameField = new TextField("Name") { tooltip = "An invented proper noun. Untranslated (D14)" };
            _nameField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _displayName) return;
                RecordUndo("Change line name");
                _displayName = evt.newValue;
                Touch();
            });
            section.Add(_nameField);

            _colorField = new ColorField("Colour") { showAlpha = false };
            _colorField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _color) return;
                RecordUndo("Change line colour");
                _color = evt.newValue;
                Touch();
            });
            section.Add(_colorField);

            _nodeCount = new Label { style = { opacity = 0.7f, marginTop = 4, marginBottom = 4 } };
            section.Add(_nodeCount);

            var buttons = Row();
            _saveButton = new Button(Save) { text = "Save to asset" };
            _saveButton.style.flexGrow = 1f;
            _revertButton = new Button(() => { LoadFromAsset(); RefreshAll(); }) { text = "Revert" };
            buttons.Add(_saveButton);
            buttons.Add(_revertButton);
            section.Add(buttons);

            return section;
        }

        private VisualElement BuildStationSection()
        {
            var section = Section("Stations");
            var buttons = Row();
            buttons.Add(new Button(AutoAssignStations) { text = "Auto-assign", tooltip = "Spread the stations evenly along the nodes, in order" });
            section.Add(buttons);
            _stationList = new VisualElement();
            section.Add(_stationList);
            return section;
        }

        private VisualElement BuildValidationSection()
        {
            var section = Section("Problems");
            _problems = new HelpBox("", HelpBoxMessageType.None);
            section.Add(_problems);

            // The network checks read the saved assets, so they are kept apart from the live ones rather than
            // contradicting them while an edit is still in the window.
            section.Add(new Label("Network, as saved") { style = { opacity = 0.7f, marginTop = 6 } });
            _networkProblems = new HelpBox("", HelpBoxMessageType.None);
            section.Add(_networkProblems);
            return section;
        }

        private VisualElement BuildPreviewSection()
        {
            var section = Section("Preview");

            var choice = new DropdownField(Aspects.Select(a => a.Label).ToList(), Mathf.Clamp(_aspect, 0, Aspects.Length - 1));
            choice.RegisterValueChangedCallback(evt =>
            {
                _aspect = Mathf.Max(0, Array.FindIndex(Aspects, a => a.Label == evt.newValue));
                RefreshPreview();
            });
            section.Add(choice);

            // The runtime element itself, not a copy of its drawing code: if the preview and the game ever disagree,
            // it is because the data changed, not because the tool drifted.
            _previewFrame = new VisualElement { style = { alignItems = Align.Center, marginTop = 6 } };
            _previewMap = new LineMapElement { Padding = 24f };
            _previewMap.style.backgroundColor = Palette.Paper;
            _previewMap.style.borderTopWidth = 1;
            _previewMap.style.borderBottomWidth = 1;
            _previewMap.style.borderLeftWidth = 1;
            _previewMap.style.borderRightWidth = 1;
            _previewMap.style.borderTopColor = Palette.Ink;
            _previewMap.style.borderBottomColor = Palette.Ink;
            _previewMap.style.borderLeftColor = Palette.Ink;
            _previewMap.style.borderRightColor = Palette.Ink;
            _previewFrame.Add(_previewMap);
            section.Add(_previewFrame);

            return section;
        }

        // ------------------------------------------------------------------ editing

        private void RecordUndo(string name) => Undo.RecordObject(this, name);

        /// <summary>Call after any buffered edit: marks the window dirty and pushes the change through the UI.</summary>
        private void Touch()
        {
            _dirty = true;
            RefreshAll();
        }

        private Vector2 SnapNode(int index, Vector2 raw)
        {
            var previous = Previous(index);
            var next = Next(index);
            return MapGrid.Snap(raw, previous, next, _grid, _snap);
        }

        private Vector2? Previous(int index)
        {
            if (_nodes.Length < 2) return null;
            if (index > 0) return _nodes[index - 1];
            return _shape == MapShape.Loop ? _nodes[_nodes.Length - 1] : (Vector2?)null;
        }

        private Vector2? Next(int index)
        {
            if (_nodes.Length < 2) return null;
            if (index < _nodes.Length - 1) return _nodes[index + 1];
            return _shape == MapShape.Loop ? _nodes[0] : (Vector2?)null;
        }

        private void MoveNode(int index, Vector2 position)
        {
            if (index < 0 || index >= _nodes.Length) return;
            _nodes[index] = SnapNode(index, position);
            Touch();
        }

        private void AppendNode() => AppendNode(SuggestedPosition());

        private void AppendNode(Vector2 position)
        {
            RecordUndo("Add map node");
            var list = new List<Vector2>(_nodes) { MapGrid.SnapToGrid(position, _grid) };
            _nodes = list.ToArray();
            _selected = _nodes.Length - 1;
            Touch();
        }

        /// <summary>Where a node added from the toolbar goes: one grid step past the last one, along the last run.</summary>
        private Vector2 SuggestedPosition()
        {
            if (_nodes.Length == 0) return Vector2.zero;
            var last = _nodes[_nodes.Length - 1];
            if (_nodes.Length == 1) return last + new Vector2(_grid * 2f, 0f);
            var run = (last - _nodes[_nodes.Length - 2]).normalized;
            return MapGrid.SnapToGrid(last + run * (_grid * 2f), _grid);
        }

        private void InsertAfterSelected()
        {
            if (_selected < 0 || _selected >= _nodes.Length - 1) return;
            InsertNode(_selected, Vector2.Lerp(_nodes[_selected], _nodes[_selected + 1], 0.5f));
        }

        /// <summary>Splits the segment leaving <paramref name="after"/> with a new node.</summary>
        private void InsertNode(int after, Vector2 position)
        {
            if (after < 0 || after >= _nodes.Length) return;
            RecordUndo("Insert map node");

            var list = new List<Vector2>(_nodes);
            list.Insert(after + 1, MapGrid.SnapToGrid(position, _grid));
            _nodes = list.ToArray();
            for (var i = 0; i < _stations.Length; i++)
                if (_stations[i] > after) _stations[i]++;

            _selected = after + 1;
            Touch();
        }

        private void DeleteSelected() => DeleteNode(_selected);

        private void DeleteNode(int index)
        {
            if (index < 0 || index >= _nodes.Length) return;
            RecordUndo("Delete map node");

            var list = new List<Vector2>(_nodes);
            list.RemoveAt(index);
            _nodes = list.ToArray();

            // A station left standing on the deleted node becomes unplaced rather than silently sliding to another:
            // validation says so straight away, and Undo puts it back.
            for (var i = 0; i < _stations.Length; i++)
            {
                if (_stations[i] == index) _stations[i] = -1;
                else if (_stations[i] > index) _stations[i]--;
            }

            _selected = Mathf.Min(index, _nodes.Length - 1);
            Touch();
        }

        private void SnapAllToGrid()
        {
            RecordUndo("Snap nodes to grid");
            for (var i = 0; i < _nodes.Length; i++) _nodes[i] = MapGrid.SnapToGrid(_nodes[i], _grid);
            Touch();
        }

        /// <summary>
        /// Gives every segment a legal shape by inserting a corner into the ones that have none: straight along the
        /// dominant axis, then 45 degrees into the far end, the way a transit diagram turns. It moves no node, so
        /// every station stays exactly where it was put; only the corners between them are new.
        /// </summary>
        private void Straighten()
        {
            if (_nodes.Length < 2) return;
            RecordUndo("Straighten map");

            var result = new List<Vector2>();
            var moved = new int[_nodes.Length];
            for (var i = 0; i < _nodes.Length; i++)
            {
                moved[i] = result.Count;
                result.Add(_nodes[i]);

                var last = i == _nodes.Length - 1;
                if (last && _shape != MapShape.Loop) break;

                var from = _nodes[i];
                var to = _nodes[(i + 1) % _nodes.Length];
                if (MapGrid.IsAligned(from, to)) continue;
                result.Add(Corner(from, to));
            }

            for (var i = 0; i < _stations.Length; i++)
                if (_stations[i] >= 0 && _stations[i] < moved.Length) _stations[i] = moved[_stations[i]];

            _nodes = result.ToArray();
            _selected = -1;
            Touch();
        }

        /// <summary>Where the turn happens: along the longer axis first, leaving a true diagonal into the far end.</summary>
        private static Vector2 Corner(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var x = Mathf.Abs(delta.x);
            var y = Mathf.Abs(delta.y);
            return x > y
                ? new Vector2(from.x + Mathf.Sign(delta.x) * (x - y), from.y)
                : new Vector2(from.x, from.y + Mathf.Sign(delta.y) * (y - x));
        }

        private int StationCount => _asset != null && _asset.StationCount > 0 ? _asset.StationCount : _stations.Length;

        private void AssignStation(int station, int node)
        {
            if (station < 0) return;
            RecordUndo("Assign station to node");
            if (_stations.Length != StationCount) Array.Resize(ref _stations, StationCount);
            if (station >= _stations.Length) return;
            _stations[station] = node;
            _armed = station + 1 < _stations.Length ? station + 1 : -1;
            Touch();
        }

        /// <summary>Stations in order over the nodes, evenly spread. The common first draft of any line.</summary>
        private void AutoAssignStations()
        {
            var count = StationCount;
            if (count <= 0 || _nodes.Length == 0) return;
            RecordUndo("Auto-assign stations");

            var assigned = new int[count];
            for (var i = 0; i < count; i++)
                assigned[i] = count == 1
                    ? 0
                    : Mathf.RoundToInt(i * (_nodes.Length - 1f) / (count - 1f));

            _stations = assigned;
            _builtStations = -1;
            Touch();
        }

        /// <summary>The node a click landed on, or -1. Screen-space so the tolerance is in pixels.</summary>
        private int NodeAt(Vector2 screen, Func<Vector2, Vector2> toScreen, float radius)
        {
            var best = -1;
            var bestDistance = radius;
            for (var i = 0; i < _nodes.Length; i++)
            {
                var distance = Vector2.Distance(toScreen(_nodes[i]), screen);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = i;
            }

            return best;
        }

        // ------------------------------------------------------------------ asset

        private void LoadFromAsset()
        {
            _builtStations = -1;
            _selected = -1;
            _armed = -1;
            _dirty = false;

            if (_asset == null)
            {
                _nodes = Array.Empty<Vector2>();
                _stations = Array.Empty<int>();
                return;
            }

            _nodes = _asset.MapNodes.ToArray();
            _stations = _asset.StationNodeIndices.ToArray();
            _shape = _asset.MapShape;
            _code = _asset.Code ?? "";
            _displayName = _asset.DisplayName ?? "";
            _color = _asset.Color;
            if (_canvas != null) _canvas.schedule.Execute(() => _canvas.Frame());
        }

        private void Save()
        {
            if (_asset == null) return;

            var problems = Problems();
            if (problems.Count > 0 &&
                !EditorUtility.DisplayDialog("Line map has problems",
                    string.Join("\n", problems) + "\n\nSave anyway?", "Save anyway", "Cancel")) return;

            // Through SerializedObject rather than the asset's own setters: it records the Undo entry, and the map
            // stays something the Editor assembly writes without Game growing an API for it.
            var serialized = new SerializedObject(_asset);
            serialized.FindProperty("code").stringValue = _code;
            serialized.FindProperty("displayName").stringValue = _displayName;
            serialized.FindProperty("color").colorValue = _color;
            serialized.FindProperty("mapShape").enumValueIndex = (int)_shape;

            var nodes = serialized.FindProperty("mapNodes");
            nodes.arraySize = _nodes.Length;
            for (var i = 0; i < _nodes.Length; i++) nodes.GetArrayElementAtIndex(i).vector2Value = _nodes[i];

            var stations = serialized.FindProperty("stationNodeIndices");
            stations.arraySize = _stations.Length;
            for (var i = 0; i < _stations.Length; i++) stations.GetArrayElementAtIndex(i).intValue = _stations[i];

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);
            _dirty = false;
            RefreshAll();
        }

        // ------------------------------------------------------------------ refresh

        /// <summary>What is wrong with the edit in the window, right now.</summary>
        private List<string> Problems() => LineMapValidation.ValidateLine(_nodes, _stations, StationCount, _shape);

        /// <summary>
        /// What is wrong with the network on disk, minus the edited line's own problems: those are already in the
        /// live list, and the saved copy of them is about to be replaced.
        /// </summary>
        private List<string> NetworkProblems()
        {
            var problems = LineMapValidation.ValidateNetwork(_network);
            if (_asset == null || _network == null) return problems;

            var index = -1;
            for (var i = 0; i < _network.LineCount; i++)
                if (_network.Line(i) == _asset) index = i;
            if (index < 0) return problems;

            var prefix = LineMapValidation.LineLabel(_asset, index) + ": ";
            problems.RemoveAll(problem => problem.StartsWith(prefix, StringComparison.Ordinal));
            return problems;
        }

        private void RefreshAll()
        {
            if (_canvas == null) return;

            _assetField.SetValueWithoutNotify(_asset);
            _networkField.SetValueWithoutNotify(_network);
            _codeField.SetValueWithoutNotify(_code);
            _nameField.SetValueWithoutNotify(_displayName);
            _colorField.SetValueWithoutNotify(_color);
            _shapeField.SetValueWithoutNotify(_shape);
            _gridField.SetValueWithoutNotify(_grid);
            _snapToggle.SetValueWithoutNotify(_snap);

            _nodeCount.text = $"{_nodes.Length} node{(_nodes.Length == 1 ? "" : "s")}, " +
                              $"{StationCount} station{(StationCount == 1 ? "" : "s")}" +
                              (_dirty ? "  ·  unsaved" : "");
            _saveButton.SetEnabled(_asset != null);
            _revertButton.SetEnabled(_asset != null && _dirty);
            titleContent = new GUIContent(_dirty ? "Line Map*" : "Line Map");

            _hint.text = _armed >= 0
                ? $"Click a node to put station {_armed + 1} on it. Escape cancels."
                : "Drag a node to move it. Shift-click empty space adds one, Alt-click a segment splits it, " +
                  "right-click or Delete removes one. Arrows nudge by a grid step, middle-drag pans, the wheel zooms.";

            RefreshStations();
            RefreshProblems();
            RefreshPreview();
            _canvas.Sync();
        }

        private void RefreshStations()
        {
            var count = StationCount;
            if (_stations.Length != count && count > 0) Array.Resize(ref _stations, count);

            if (_builtStations != count)
            {
                _stationList.Clear();
                _builtStations = count;
                for (var i = 0; i < count; i++) _stationList.Add(BuildStationRow(i));
            }

            for (var i = 0; i < _stationList.childCount; i++)
            {
                var row = _stationList[i];
                if (row.userData is StationRow widgets) widgets.Refresh(this, i);
            }
        }

        /// <summary>The widgets of one station row, kept together so <see cref="RefreshStations"/> can update them.</summary>
        private sealed class StationRow
        {
            public Label Name;
            public IntegerField Node;
            public Button Pick;

            public void Refresh(LineMapEditorWindow window, int index)
            {
                var level = window._asset != null ? window._asset.Station(index) : null;
                var station = level != null && !string.IsNullOrEmpty(level.DisplayName) ? level.DisplayName : "—";
                Name.text = $"{index + 1} · {station}";
                Node.SetValueWithoutNotify(index < window._stations.Length ? window._stations[index] : -1);
                Pick.text = window._armed == index ? "Picking" : "Pick";
            }
        }

        private VisualElement BuildStationRow(int index)
        {
            var row = Row();
            row.style.alignItems = Align.Center;

            var widgets = new StationRow
            {
                Name = new Label { style = { flexGrow = 1f, flexShrink = 1f, overflow = Overflow.Hidden } },
                Node = new IntegerField { style = { width = 46 } },
                Pick = new Button { text = "Pick", style = { width = 60 } },
            };

            widgets.Node.RegisterValueChangedCallback(evt =>
            {
                if (index >= _stations.Length || _stations[index] == evt.newValue) return;
                RecordUndo("Assign station to node");
                _stations[index] = evt.newValue;
                Touch();
            });
            widgets.Pick.clicked += () =>
            {
                _armed = _armed == index ? -1 : index;
                RefreshAll();
            };

            row.Add(widgets.Name);
            row.Add(widgets.Node);
            row.Add(widgets.Pick);
            row.userData = widgets;
            return row;
        }

        private void RefreshProblems()
        {
            Report(_problems, Problems(), "This line is fit to ship.");
            Report(_networkProblems, NetworkProblems(), "The rest of the network is clean.");
        }

        private static void Report(HelpBox box, List<string> problems, string clean)
        {
            box.text = problems.Count == 0 ? clean : string.Join("\n", problems);
            box.messageType = problems.Count == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
        }

        private void RefreshPreview()
        {
            if (_previewMap == null) return;

            if (_preview == null)
            {
                _preview = CreateInstance<LineDefinition>();
                _preview.hideFlags = HideFlags.HideAndDontSave;
            }

            _preview.SetUp("preview", _code, _displayName, _color, _asset != null ? _asset.Levels : null);
            _preview.SetMap(_shape, _nodes.ToArray(), _stations.ToArray());
            _previewMap.SetLine(_preview);
            _previewMap.style.color = _color;

            var aspect = Aspects[Mathf.Clamp(_aspect, 0, Aspects.Length - 1)].Aspect;
            const float width = 260f;
            _previewMap.style.width = width;
            _previewMap.style.height = width / aspect;
        }

        // ------------------------------------------------------------------ small widgets

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Foldout Section(string title)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.style.marginBottom = 6;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
            return foldout;
        }

        // ------------------------------------------------------------------ the canvas

        /// <summary>
        /// The drawing surface: the grid, the other lines behind, the edited polyline, and a handle per node. It owns
        /// nothing — every edit goes back through the window, so Undo sees one entry per gesture.
        /// </summary>
        private sealed class MapCanvas : VisualElement
        {
            private const float HandleRadius = 6f;
            private const float PickRadius = 13f;

            private static readonly Color Background = new Color(0.16f, 0.17f, 0.18f);
            private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.06f);
            private static readonly Color GridMajor = new Color(1f, 1f, 1f, 0.13f);
            private static readonly Color Guide = new Color(1f, 0.85f, 0.3f, 0.5f);
            private static readonly Color Handle = new Color(0.92f, 0.93f, 0.94f);
            private static readonly Color Selection = new Color(1f, 0.72f, 0.2f);

            private readonly LineMapEditorWindow _owner;
            private readonly List<Label> _numbers = new List<Label>();

            private Vector2 _origin = new Vector2(40f, 40f);
            private float _zoom = 0.3f;
            private int _dragging = -1;
            private bool _panning;
            private Vector2 _panFrom;

            public MapCanvas(LineMapEditorWindow owner)
            {
                _owner = owner;
                focusable = true;
                style.backgroundColor = Background;
                style.overflow = Overflow.Hidden;

                generateVisualContent += Draw;
                RegisterCallback<GeometryChangedEvent>(_ => Sync());
                RegisterCallback<PointerDownEvent>(OnPointerDown);
                RegisterCallback<PointerMoveEvent>(OnPointerMove);
                RegisterCallback<PointerUpEvent>(OnPointerUp);
                RegisterCallback<WheelEvent>(OnWheel);
                RegisterCallback<KeyDownEvent>(OnKeyDown);
            }

            private Vector2 ToScreen(Vector2 map) => _origin + map * _zoom;

            private Vector2 ToMap(Vector2 screen) => (screen - _origin) / _zoom;

            /// <summary>Fits every node, plus the rest of the network, into the view.</summary>
            public void Frame()
            {
                var rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f) return;

                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                var any = false;
                foreach (var node in AllNodes())
                {
                    min = Vector2.Min(min, node);
                    max = Vector2.Max(max, node);
                    any = true;
                }

                if (!any)
                {
                    _origin = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
                    _zoom = 0.3f;
                    Sync();
                    return;
                }

                const float margin = 48f;
                var span = max - min;
                var sx = span.x > 0.01f ? (rect.width - margin * 2f) / span.x : float.MaxValue;
                var sy = span.y > 0.01f ? (rect.height - margin * 2f) / span.y : float.MaxValue;
                _zoom = Mathf.Clamp(Mathf.Min(sx, sy), 0.02f, 4f);
                _origin = new Vector2(
                    (rect.width - span.x * _zoom) * 0.5f - min.x * _zoom,
                    (rect.height - span.y * _zoom) * 0.5f - min.y * _zoom);
                Sync();
            }

            private IEnumerable<Vector2> AllNodes()
            {
                foreach (var node in _owner._nodes) yield return node;
                if (_owner._network == null) yield break;
                for (var i = 0; i < _owner._network.LineCount; i++)
                {
                    var line = _owner._network.Line(i);
                    if (line == null || line == _owner._asset) continue;
                    foreach (var node in line.MapNodes) yield return node;
                }
            }

            /// <summary>Repositions the station numbers and repaints. Called after every change.</summary>
            public void Sync()
            {
                var stations = _owner._stations;
                while (_numbers.Count < stations.Length)
                {
                    var label = new Label
                    {
                        pickingMode = PickingMode.Ignore,
                        style =
                        {
                            position = Position.Absolute,
                            color = Handle,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            fontSize = 11,
                        },
                    };
                    _numbers.Add(label);
                    Add(label);
                }

                for (var i = 0; i < _numbers.Count; i++)
                {
                    var node = i < stations.Length ? stations[i] : -1;
                    var visible = node >= 0 && node < _owner._nodes.Length;
                    _numbers[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!visible) continue;

                    var point = ToScreen(_owner._nodes[node]);
                    _numbers[i].text = (i + 1).ToString();
                    _numbers[i].style.left = point.x + HandleRadius + 4f;
                    _numbers[i].style.top = point.y - 8f;
                }

                MarkDirtyRepaint();
            }

            // ---- drawing ----

            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                DrawGrid(painter);
                DrawGhosts(painter);
                DrawGuides(painter);
                DrawLine(painter);
                DrawHandles(painter);
            }

            private void DrawGrid(Painter2D painter)
            {
                var rect = contentRect;
                var step = _owner._grid;
                if (step <= 0f) return;

                // Thin the lattice out rather than drawing a solid field of lines when zoomed out.
                var spacing = step * _zoom;
                var major = 5;
                while (spacing < 6f && step < 100000f)
                {
                    step *= 5f;
                    spacing = step * _zoom;
                }

                var min = ToMap(Vector2.zero);
                var max = ToMap(new Vector2(rect.width, rect.height));
                var firstX = Mathf.Ceil(min.x / step) * step;
                var firstY = Mathf.Ceil(min.y / step) * step;

                painter.lineWidth = 1f;
                for (var pass = 0; pass < 2; pass++)
                {
                    painter.strokeColor = pass == 0 ? GridLine : GridMajor;
                    painter.BeginPath();
                    var drawn = 0;
                    for (var x = firstX; x <= max.x && drawn < 400; x += step, drawn++)
                    {
                        if (IsMajor(x, step, major) != (pass == 1)) continue;
                        var screen = ToScreen(new Vector2(x, 0f)).x;
                        painter.MoveTo(new Vector2(screen, 0f));
                        painter.LineTo(new Vector2(screen, rect.height));
                    }

                    drawn = 0;
                    for (var y = firstY; y <= max.y && drawn < 400; y += step, drawn++)
                    {
                        if (IsMajor(y, step, major) != (pass == 1)) continue;
                        var screen = ToScreen(new Vector2(0f, y)).y;
                        painter.MoveTo(new Vector2(0f, screen));
                        painter.LineTo(new Vector2(rect.width, screen));
                    }

                    painter.Stroke();
                }
            }

            private static bool IsMajor(float value, float step, int every) =>
                Mathf.Abs(Mathf.Repeat(value / step, every)) < 0.001f;

            private void DrawGhosts(Painter2D painter)
            {
                var network = _owner._network;
                if (network == null) return;

                for (var i = 0; i < network.LineCount; i++)
                {
                    var line = network.Line(i);
                    if (line == null || line == _owner._asset || line.MapNodes.Count < 2) continue;

                    painter.strokeColor = new Color(line.Color.r, line.Color.g, line.Color.b, 0.35f);
                    painter.lineWidth = 6f;
                    painter.lineCap = LineCap.Round;
                    painter.lineJoin = LineJoin.Round;
                    painter.BeginPath();
                    painter.MoveTo(ToScreen(line.MapNodes[0]));
                    for (var n = 1; n < line.MapNodes.Count; n++) painter.LineTo(ToScreen(line.MapNodes[n]));
                    if (line.MapShape == MapShape.Loop) painter.ClosePath();
                    painter.Stroke();
                }
            }

            /// <summary>The rays a dragged node is snapping to, so the 45-degree rule is visible while it is applied.</summary>
            private void DrawGuides(Painter2D painter)
            {
                if (_dragging < 0 || !_owner._snap) return;

                var anchors = new List<Vector2>(2);
                var previous = _owner.Previous(_dragging);
                var next = _owner.Next(_dragging);
                if (previous.HasValue) anchors.Add(previous.Value);
                if (next.HasValue) anchors.Add(next.Value);

                var reach = Mathf.Max(contentRect.width, contentRect.height) / Mathf.Max(_zoom, 0.0001f);
                painter.strokeColor = Guide;
                painter.lineWidth = 1f;
                painter.BeginPath();
                foreach (var anchor in anchors)
                    foreach (var direction in MapGrid.Directions)
                    {
                        painter.MoveTo(ToScreen(anchor));
                        painter.LineTo(ToScreen(anchor + (Vector2)direction * reach));
                    }

                painter.Stroke();
            }

            private void DrawLine(Painter2D painter)
            {
                var nodes = _owner._nodes;
                if (nodes.Length < 2) return;

                painter.strokeColor = _owner._color;
                painter.lineWidth = 8f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(ToScreen(nodes[0]));
                for (var i = 1; i < nodes.Length; i++) painter.LineTo(ToScreen(nodes[i]));
                if (_owner._shape == MapShape.Loop) painter.ClosePath();
                painter.Stroke();
            }

            private void DrawHandles(Painter2D painter)
            {
                var nodes = _owner._nodes;
                var stations = new HashSet<int>(_owner._stations);

                for (var i = 0; i < nodes.Length; i++)
                {
                    var point = ToScreen(nodes[i]);
                    var station = stations.Contains(i);
                    var selected = i == _owner._selected;

                    // A station reads as a ring on the route; a plain corner as a small square.
                    if (station)
                    {
                        painter.fillColor = Handle;
                        painter.BeginPath();
                        painter.Arc(point, HandleRadius + 1f, Angle.Degrees(0f), Angle.Degrees(360f));
                        painter.Fill();
                        painter.strokeColor = _owner._color;
                        painter.lineWidth = 3f;
                        painter.BeginPath();
                        painter.Arc(point, HandleRadius + 1f, Angle.Degrees(0f), Angle.Degrees(360f));
                        painter.Stroke();
                    }
                    else
                    {
                        painter.fillColor = Handle;
                        FillSquare(painter, point, HandleRadius * 0.8f);
                    }

                    if (!selected) continue;
                    painter.strokeColor = Selection;
                    painter.lineWidth = 2f;
                    painter.BeginPath();
                    painter.Arc(point, HandleRadius + 6f, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.Stroke();
                }
            }

            private static void FillSquare(Painter2D painter, Vector2 centre, float half)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(centre.x - half, centre.y - half));
                painter.LineTo(new Vector2(centre.x + half, centre.y - half));
                painter.LineTo(new Vector2(centre.x + half, centre.y + half));
                painter.LineTo(new Vector2(centre.x - half, centre.y + half));
                painter.ClosePath();
                painter.Fill();
            }

            // ---- input ----

            private void OnPointerDown(PointerDownEvent evt)
            {
                Focus();
                var local = (Vector2)evt.localPosition;
                var node = _owner.NodeAt(local, ToScreen, PickRadius);

                if (evt.button == 2)
                {
                    _panning = true;
                    _panFrom = local;
                    this.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button == 1)
                {
                    if (node >= 0) _owner.DeleteNode(node);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0) return;

                if (node >= 0)
                {
                    if (_owner._armed >= 0)
                    {
                        _owner.AssignStation(_owner._armed, node);
                        evt.StopPropagation();
                        return;
                    }

                    _owner._selected = node;
                    _dragging = node;
                    _owner.RecordUndo("Move map node");
                    this.CapturePointer(evt.pointerId);
                    _owner.RefreshAll();
                    evt.StopPropagation();
                    return;
                }

                if (evt.shiftKey)
                {
                    _owner.AppendNode(ToMap(local));
                    evt.StopPropagation();
                    return;
                }

                if (evt.altKey)
                {
                    var segment = SegmentAt(local);
                    if (segment >= 0) _owner.InsertNode(segment, ToMap(local));
                    evt.StopPropagation();
                    return;
                }

                _owner._selected = -1;
                _owner.RefreshAll();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (_panning)
                {
                    var local = (Vector2)evt.localPosition;
                    _origin += local - _panFrom;
                    _panFrom = local;
                    Sync();
                    evt.StopPropagation();
                    return;
                }

                if (_dragging < 0) return;
                _owner.MoveNode(_dragging, ToMap(evt.localPosition));
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!_panning && _dragging < 0) return;
                _panning = false;
                _dragging = -1;
                this.ReleasePointer(evt.pointerId);
                Sync();
                evt.StopPropagation();
            }

            private void OnWheel(WheelEvent evt)
            {
                var local = (Vector2)evt.localMousePosition;
                var before = ToMap(local);
                _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-evt.delta.y * 0.05f), 0.02f, 4f);
                _origin += local - ToScreen(before);
                Sync();
                evt.StopPropagation();
            }

            private void OnKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape && _owner._armed >= 0)
                {
                    _owner._armed = -1;
                    _owner.RefreshAll();
                    evt.StopPropagation();
                    return;
                }

                var selected = _owner._selected;
                if (selected < 0 || selected >= _owner._nodes.Length) return;

                if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                {
                    _owner.DeleteNode(selected);
                    evt.StopPropagation();
                    return;
                }

                var step = Vector2.zero;
                switch (evt.keyCode)
                {
                    case KeyCode.LeftArrow: step = new Vector2(-1f, 0f); break;
                    case KeyCode.RightArrow: step = new Vector2(1f, 0f); break;
                    case KeyCode.UpArrow: step = new Vector2(0f, -1f); break;
                    case KeyCode.DownArrow: step = new Vector2(0f, 1f); break;
                    default: return;
                }

                _owner.RecordUndo("Nudge map node");
                _owner._nodes[selected] += step * _owner._grid;
                _owner.Touch();
                evt.StopPropagation();
            }

            /// <summary>The segment a click landed on, named by the node it leaves, or -1.</summary>
            private int SegmentAt(Vector2 screen)
            {
                var nodes = _owner._nodes;
                var best = -1;
                var bestDistance = PickRadius;
                var count = _owner._shape == MapShape.Loop ? nodes.Length : nodes.Length - 1;

                for (var i = 0; i < count; i++)
                {
                    var a = ToScreen(nodes[i]);
                    var b = ToScreen(nodes[(i + 1) % nodes.Length]);
                    var distance = DistanceToSegment(screen, a, b);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    best = i;
                }

                return best;
            }

            private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
            {
                var direction = b - a;
                var length = direction.sqrMagnitude;
                if (length < 0.0001f) return Vector2.Distance(point, a);
                var t = Mathf.Clamp01(Vector2.Dot(point - a, direction) / length);
                return Vector2.Distance(point, a + direction * t);
            }
        }
    }
}
