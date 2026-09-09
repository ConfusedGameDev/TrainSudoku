using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The 3D board (PRD sections 4 and 7). The root and its groups are generated with the scene; per-level content
    /// (tiles with colliders, tunnels, clue labels, track pieces) is built from primitives and bent track meshes each
    /// time a level loads. Handles the tap-to-place interaction through a Core <see cref="PlacementSession"/> and the
    /// long-press erase.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardView
    {
        private const float TileHeight = 0.1f;
        private const float TileInset = 0.04f;
        private const float RayLength = 200f;
        private const float HoldDuration = 0.5f;
        private const float HoldMoveTolerancePixels = 40f;
        private const float MarkerInset = 0.36f;

        [SerializeField] private Transform tiles;
        [SerializeField] private Transform pieces;
        [SerializeField] private Transform tunnels;
        [SerializeField] private Transform clues;
        [SerializeField] private Transform markers;
        [SerializeField] private BoardCamera boardCamera;
        [SerializeField] private TrackAssets trackAssets;

        private BoardCell[,] _cells;
        private TextMesh[] _columnClues;
        private TextMesh[] _rowClues;
        private readonly Dictionary<(int X, int Y), PieceView> _pieceViews = new Dictionary<(int, int), PieceView>();
        private PlacementSession _session;
        private TrackMeshProfile _profile;
        private bool _interactable;

        // Current press, for taps and long presses.
        private bool _pressing;
        private bool _pressConsumed;
        private bool _holdTriggered;
        private float _pressTime;
        private Vector2 _pressPosition;
        private (int X, int Y)? _pressCell;
        private GameObject _holdIndicator;

        public LevelData Level { get; private set; }

        /// <summary>Core state of the board being played: fixed pieces plus the player's pieces.</summary>
        public Board Board { get; private set; }

        /// <summary>Cell hit by the most recent tap, or null when the tap missed the grid.</summary>
        public (int X, int Y)? LastTappedCell { get; private set; }

        /// <summary>Validator output for the current board, refreshed after every change (PRD section 3.4).</summary>
        public WinResult LastResult { get; private set; }

        public bool IsGenerated => tiles != null && pieces != null && tunnels != null && clues != null && markers != null;

        public event Action Interacted;
        public event Action Completed;

        /// <summary>A piece was placed or erased. Raised before the validator runs.</summary>
        public event Action BoardChanged;

        /// <summary>Creates the empty board root under <paramref name="parent"/>. Works in the Editor and at runtime.</summary>
        public static BoardView Create(Transform parent, BoardCamera camera, TrackAssets trackAssets)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.tiles = view.Group("Tiles");
            view.pieces = view.Group("Pieces");
            view.tunnels = view.Group("Tunnels");
            view.clues = view.Group("Clues");
            view.markers = view.Group("Markers");
            view.boardCamera = camera;
            view.trackAssets = trackAssets;
            return view;
        }

        /// <summary>Runtime configuration for scenes generated before these references existed.</summary>
        public void Configure(TrackAssets assets)
        {
            if (assets != null) trackAssets = assets;
            if (markers == null) markers = Group("Markers");
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Camera UnityCamera => boardCamera != null ? boardCamera.Camera : Camera.main;

        // ------------------------------------------------------------------ IBoardView

        public void Load(LevelDefinition level, LevelProgress resume)
        {
            Clear();
            LastTappedCell = null;
            if (level == null) return;

            Level = level.ToLevelData();
            Board = new Board(Level);
            _session = new PlacementSession(Board);
            _profile = trackAssets != null ? trackAssets.ResolveProfile() : TrackAssets.PlaceholderProfile();

            BuildTiles();
            foreach (var piece in Level.FixedPieces) SpawnPiece(piece.X, piece.Y, piece.Key, true, false);
            if (resume != null) RestorePieces(resume);
            BuildTunnels();
            BuildClues();

            if (boardCamera != null) boardCamera.SetTarget(ContentBounds());

            // Colour the clues for the fixed pieces, but never win a level the player has not touched.
            Validate(false);
        }

        /// <summary>
        /// What the camera has to frame: the grid plus the tunnel ring and the train's height as a minimum, grown by
        /// everything actually built (tunnel mouths, letters, clue labels), so the view centres on the visible content.
        /// </summary>
        private Bounds ContentBounds()
        {
            var height = (float)BoardLayout.Height;
            var bounds = new Bounds(
                new Vector3(0f, height / 2f, 0f),
                new Vector3(2f * (float)BoardLayout.HalfWidth(Level.Width), height, 2f * (float)BoardLayout.HalfDepth(Level.Height)));
            foreach (var group in new[] { tiles, pieces, tunnels, clues })
                foreach (var renderer in group.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Puts a saved attempt back on the board (auto-save, PRD section 6) and shows its pieces without animation.</summary>
        private void RestorePieces(LevelProgress resume)
        {
            resume.ApplyTo(Board);
            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
                if (Board[x, y] is Piece piece && !piece.IsFixed) SpawnPiece(x, y, piece.Key, false, false);
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (!interactable)
            {
                EndPress();
                _session?.Cancel();
                RefreshSelection();
            }
        }

        /// <summary>Raises <see cref="Completed"/> without checking the board. Editor-only shortcut behind the pause menu debug button.</summary>
        public void ForceComplete() => Completed?.Invoke();

        // ------------------------------------------------------------------ validation

        private static readonly Color ClueExceeded = new Color(0.95f, 0.40f, 0.35f);

        /// <summary>Runs the Core validator, colours the clues and, after a player action, fires the win.</summary>
        private void Validate(bool announceWin)
        {
            if (Board == null) return;
            LastResult = WinChecker.Evaluate(Board);
            ApplyClueColors(LastResult);
            if (announceWin && LastResult.IsWin) Completed?.Invoke();
        }

        private void OnBoardChanged()
        {
            BoardChanged?.Invoke();
            Validate(true);
        }

        /// <summary>Satisfied lines turn green, lines holding more pieces than their clue turn red.</summary>
        private void ApplyClueColors(WinResult result)
        {
            if (_columnClues == null || result == null) return;
            for (var x = 0; x < _columnClues.Length; x++)
                _columnClues[x].color = ClueColor(result.ColumnSatisfied[x], Board.ColumnCount(x) > Level.ColumnClues[x]);
            for (var y = 0; y < _rowClues.Length; y++)
                _rowClues[y].color = ClueColor(result.RowSatisfied[y], Board.RowCount(y) > Level.RowClues[y]);
        }

        private static Color ClueColor(bool satisfied, bool exceeded) =>
            satisfied ? UiBuilder.Success : exceeded ? ClueExceeded : UiBuilder.TextColor;

        // ------------------------------------------------------------------ building

        private void Clear()
        {
            foreach (var group in new[] { tiles, pieces, tunnels, clues, markers })
                if (group != null) UiBuilder.Clear(group);
            // The bent meshes belong to the level that just went away, and the next one may use a different profile.
            TrackMeshBender.Clear();
            _cells = null;
            _columnClues = null;
            _rowClues = null;
            _pieceViews.Clear();
            _holdIndicator = null;
            _session = null;
            Level = null;
            Board = null;
            EndPress();
        }

        private void BuildTiles()
        {
            _cells = new BoardCell[Level.Width, Level.Height];
            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Cell ({x},{y})";
                tile.transform.SetParent(tiles, false);
                var (wx, wz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
                tile.transform.localPosition = new Vector3((float)wx, -TileHeight / 2f, (float)wz);
                var size = (float)BoardLayout.CellSize - TileInset;
                tile.transform.localScale = new Vector3(size, TileHeight, size);
                tile.GetComponent<MeshRenderer>().sharedMaterial = TileMaterial(x, y);

                var cell = tile.AddComponent<BoardCell>();
                cell.Set(x, y);
                _cells[x, y] = cell;
            }
        }

        private static Material TileMaterial(int x, int y) => (x + y) % 2 == 0 ? BoardMaterials.Tile : BoardMaterials.TileAlt;

        private PieceView SpawnPiece(int x, int y, PieceKey key, bool isFixed, bool animate)
        {
            var go = new GameObject($"{(isFixed ? "Fixed" : "Piece")} {key} ({x},{y})");
            go.transform.SetParent(pieces, false);
            var (wx, wz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
            var lift = trackAssets != null ? trackAssets.VerticalOffset : 0f;
            go.transform.localPosition = new Vector3((float)wx, lift, (float)wz);

            go.AddComponent<MeshFilter>().sharedMesh = TrackMeshBender.ForKey(_profile, key);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = isFixed
                ? trackAssets != null ? trackAssets.FixedTrackMaterial : BoardMaterials.FixedTrack
                : trackAssets != null ? trackAssets.TrackMaterial : BoardMaterials.Track;

            var view = go.AddComponent<PieceView>();
            view.Set(x, y, isFixed);
            _pieceViews[(x, y)] = view;
            if (animate) view.PlayScaleIn();
            return view;
        }

        private void RemovePiece(int x, int y)
        {
            if (!_pieceViews.TryGetValue((x, y), out var view)) return;
            _pieceViews.Remove((x, y));
            Destroy(view.gameObject);
        }

        private void BuildTunnels()
        {
            BuildTunnel(Level.Entrance, "Entrance", "S", BoardMaterials.Entrance);
            BuildTunnel(Level.Exit, "Exit", "E", BoardMaterials.Exit);
        }

        private void BuildTunnel(Tunnel tunnel, string name, string letter, Material material)
        {
            if (!tunnel.IsOnPerimeter(Level.Width, Level.Height)) return;

            var holder = new GameObject(name);
            holder.transform.SetParent(tunnels, false);
            var (wx, wz) = BoardLayout.TunnelCenter(tunnel, Level.Width, Level.Height);
            holder.transform.localPosition = new Vector3((float)wx, 0f, (float)wz);
            // Face the board: +Z of the tunnel points opposite to the side it sits on.
            holder.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(tunnel.Side.Opposite()), 0f);

            var mouth = Primitive(PrimitiveType.Cube, holder.transform, material, "Mouth", false);
            mouth.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mouth.transform.localScale = new Vector3(0.8f, 0.7f, 0.5f);

            var hole = Primitive(PrimitiveType.Cube, holder.transform, BoardMaterials.FixedPiece, "Hole", false);
            hole.transform.localPosition = new Vector3(0f, 0.25f, 0.2f);
            hole.transform.localScale = new Vector3(0.5f, 0.5f, 0.15f);

            var label = Label(holder.transform, letter, 0.6f, Color.white);
            label.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        }

        private void BuildClues()
        {
            _columnClues = new TextMesh[Level.Width];
            for (var x = 0; x < Level.Width; x++)
            {
                var (wx, wz) = BoardLayout.ColumnClueAnchor(x, Level.Width, Level.Height);
                _columnClues[x] = Label(clues, Level.ColumnClues[x].ToString(), 0.7f, UiBuilder.TextColor);
                _columnClues[x].transform.localPosition = new Vector3((float)wx, 0.05f, (float)wz);
                _columnClues[x].name = $"Column clue {x}";
            }

            _rowClues = new TextMesh[Level.Height];
            for (var y = 0; y < Level.Height; y++)
            {
                var (wx, wz) = BoardLayout.RowClueAnchor(y, Level.Width, Level.Height);
                _rowClues[y] = Label(clues, Level.RowClues[y].ToString(), 0.7f, UiBuilder.TextColor);
                _rowClues[y].transform.localPosition = new Vector3((float)wx, 0.05f, (float)wz);
                _rowClues[y].name = $"Row clue {y}";
            }
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, Material material, string name, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            // Only cell tiles and markers need colliders; everything else must not block the tap raycast.
            if (!keepCollider && go.TryGetComponent<Collider>(out var collider))
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return go;
        }

        /// <summary>World-space text turned to face the camera so it stays readable under the pitch.</summary>
        private TextMesh Label(Transform parent, string text, float worldHeight, Color color)
        {
            var go = new GameObject($"Label {text}");
            go.transform.SetParent(parent, false);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = UiBuilder.Font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = worldHeight / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = UiBuilder.Font.material;
            var pitch = boardCamera != null ? boardCamera.PitchDegrees : 60f;
            go.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            return mesh;
        }

        // ------------------------------------------------------------------ selection visuals

        /// <summary>Rebuilds the markers and the tile tint from the session state.</summary>
        private void RefreshSelection()
        {
            if (markers != null) UiBuilder.Clear(markers);
            _holdIndicator = null;
            if (_cells == null) return;

            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
                _cells[x, y].GetComponent<MeshRenderer>().sharedMaterial = TileMaterial(x, y);

            if (_session == null || !_session.IsActive) return;

            _cells[_session.X, _session.Y].GetComponent<MeshRenderer>().sharedMaterial = BoardMaterials.TileSelected;
            var (cx, cz) = BoardLayout.CellCenter(_session.X, _session.Y, Level.Width, Level.Height);
            foreach (var side in _session.Available)
            {
                var forced = _session.IsForced(side);
                var marker = Primitive(PrimitiveType.Cube, markers, forced ? BoardMaterials.MarkerForced : BoardMaterials.Marker, $"Marker {side}", true);
                var (dx, dz) = BoardLayout.Step(side);
                marker.transform.localPosition = new Vector3((float)(cx + dx * MarkerInset), 0.08f, (float)(cz + dz * MarkerInset));
                marker.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(side), 0f);
                marker.transform.localScale = new Vector3(0.18f, 0.06f, 0.26f);
                marker.AddComponent<BoardMarker>().Set(side);
            }
        }

        private void ShowHoldProgress((int X, int Y) cell, float progress)
        {
            if (_holdIndicator == null)
            {
                _holdIndicator = Primitive(PrimitiveType.Cylinder, markers, BoardMaterials.Hold, "Hold Ring", false);
                var (cx, cz) = BoardLayout.CellCenter(cell.X, cell.Y, Level.Width, Level.Height);
                _holdIndicator.transform.localPosition = new Vector3((float)cx, 0.14f, (float)cz);
            }

            var d = 0.9f * Mathf.Clamp01(progress);
            _holdIndicator.transform.localScale = new Vector3(d, 0.005f, d);
        }

        private void HideHoldProgress()
        {
            if (_holdIndicator == null) return;
            Destroy(_holdIndicator);
            _holdIndicator = null;
        }

        // ------------------------------------------------------------------ interaction

        private void Update()
        {
            if (!_interactable || _cells == null || _session == null)
            {
                if (_pressing) EndPress();
                return;
            }

            var pointer = PointerSample.Read();
            if (!pointer.HasPointer) return;

            if (pointer.PressedThisFrame) BeginPress(pointer);
            else if (_pressing && pointer.IsPressed) ContinuePress(pointer);
            else if (_pressing) ReleasePress();
        }

        private void BeginPress(PointerSample pointer)
        {
            if (IsOverUi(pointer.TouchId)) return;

            _pressing = true;
            _pressConsumed = false;
            _holdTriggered = false;
            _pressTime = Time.unscaledTime;
            _pressPosition = pointer.Position;
            _pressCell = null;
            LastTappedCell = null;
            Interacted?.Invoke();

            var camera = UnityCamera;
            if (camera == null) return;
            var ray = camera.ScreenPointToRay(pointer.Position);
            if (!Physics.Raycast(ray, out var hit, RayLength)) return;

            if (hit.collider.TryGetComponent<BoardMarker>(out var marker))
            {
                _pressConsumed = true;
                ApplyChoose(_session.Choose(marker.Side));
                return;
            }

            if (hit.collider.TryGetComponent<BoardCell>(out var cell))
            {
                _pressCell = (cell.X, cell.Y);
                LastTappedCell = _pressCell;
            }
        }

        private void ContinuePress(PointerSample pointer)
        {
            if (_pressConsumed || _holdTriggered || !_pressCell.HasValue) return;
            var cell = _pressCell.Value;
            if (!(Board[cell.X, cell.Y] is Piece piece))
            {
                return;
            }

            if ((pointer.Position - _pressPosition).sqrMagnitude > HoldMoveTolerancePixels * HoldMoveTolerancePixels)
            {
                // The finger slid away: no longer a long press on this piece.
                _pressCell = null;
                _pressConsumed = true;
                HideHoldProgress();
                return;
            }

            var progress = (Time.unscaledTime - _pressTime) / HoldDuration;
            ShowHoldProgress(cell, progress);
            if (progress < 1f) return;

            _holdTriggered = true;
            HideHoldProgress();
            if (piece.IsFixed)
            {
                if (_pieceViews.TryGetValue(cell, out var view)) view.PlayShake();
                AudioCuePlayer.Play(AudioCue.Error);
            }
            else if (Board.TryErase(cell.X, cell.Y))
            {
                RemovePiece(cell.X, cell.Y);
                _session.Cancel();
                RefreshSelection();
                AudioCuePlayer.Play(AudioCue.Erase);
                OnBoardChanged();
            }
        }

        private void ReleasePress()
        {
            var cell = _pressCell;
            var consumed = _pressConsumed || _holdTriggered;
            EndPress();
            if (consumed) return;

            if (!cell.HasValue)
            {
                // Tap outside the grid clears the selection.
                if (_session.IsActive)
                {
                    _session.Cancel();
                    RefreshSelection();
                }

                return;
            }

            // A short tap on a placed piece does nothing (PRD 4.7); Select rejects occupied cells silently.
            var occupied = Board[cell.Value.X, cell.Value.Y].HasValue;
            var outcome = _session.Select(cell.Value.X, cell.Value.Y);
            switch (outcome)
            {
                case SelectOutcome.AutoPlaced:
                    OnPlaced();
                    break;
                case SelectOutcome.Rejected:
                    if (!occupied) AudioCuePlayer.Play(AudioCue.Error);
                    RefreshSelection();
                    break;
                default:
                    RefreshSelection();
                    break;
            }
        }

        private void ApplyChoose(ChooseOutcome outcome)
        {
            if (outcome == ChooseOutcome.Placed) OnPlaced();
            else RefreshSelection();
        }

        private void OnPlaced()
        {
            var (x, y) = _session.LastPlacedCell;
            if (_session.LastPlaced.HasValue) SpawnPiece(x, y, _session.LastPlaced.Value, false, true);
            RefreshSelection();
            AudioCuePlayer.Play(AudioCue.Place);
            OnBoardChanged();
        }

        private void EndPress()
        {
            _pressing = false;
            _pressCell = null;
            _pressConsumed = false;
            _holdTriggered = false;
            HideHoldProgress();
        }

        private static bool IsOverUi(int touchId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            return touchId >= 0 ? eventSystem.IsPointerOverGameObject(touchId) : eventSystem.IsPointerOverGameObject();
        }

        /// <summary>One frame of the primary pointer: the first touch when a touchscreen is active, else the mouse.</summary>
        private readonly struct PointerSample
        {
            public readonly bool HasPointer;
            public readonly bool PressedThisFrame;
            public readonly bool IsPressed;
            public readonly Vector2 Position;
            public readonly int TouchId;

            private PointerSample(bool pressedThisFrame, bool isPressed, Vector2 position, int touchId)
            {
                HasPointer = true;
                PressedThisFrame = pressedThisFrame;
                IsPressed = isPressed;
                Position = position;
                TouchId = touchId;
            }

            public static PointerSample Read()
            {
                var touch = Touchscreen.current;
                if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
                {
                    var primary = touch.primaryTouch;
                    return new PointerSample(primary.press.wasPressedThisFrame, primary.press.isPressed, primary.position.ReadValue(), primary.touchId.ReadValue());
                }

                var mouse = Mouse.current;
                if (mouse != null)
                    return new PointerSample(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, mouse.position.ReadValue(), -1);

                return default;
            }
        }
    }
}
