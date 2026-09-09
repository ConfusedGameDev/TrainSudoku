using System;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The 3D board (PRD section 7, milestone M3). The root and its groups are generated once with the scene; the
    /// per-level content (one tile with a collider per cell, tunnel mouths outside the perimeter, clue labels beyond
    /// the tunnels, placeholder geometry for the fixed pieces) is built from primitives each time a level loads.
    /// M4 replaces the piece placeholders with bent track meshes.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardView
    {
        private const float TileHeight = 0.1f;
        private const float TileInset = 0.04f;
        private const float RayLength = 200f;

        [SerializeField] private Transform tiles;
        [SerializeField] private Transform pieces;
        [SerializeField] private Transform tunnels;
        [SerializeField] private Transform clues;
        [SerializeField] private BoardCamera boardCamera;

        private BoardCell[,] _cells;
        private TextMesh[] _columnClues;
        private TextMesh[] _rowClues;
        private bool _interactable;

        public LevelData Level { get; private set; }

        /// <summary>Core state of the board being played. Fixed pieces only until M4 adds placement.</summary>
        public Board Board { get; private set; }

        /// <summary>Cell hit by the most recent tap, or null when the tap missed the grid.</summary>
        public (int X, int Y)? LastTappedCell { get; private set; }

        public bool IsGenerated => tiles != null && pieces != null && tunnels != null && clues != null;

        public event Action Interacted;
        public event Action Completed;

        /// <summary>Creates the empty board root under <paramref name="parent"/>. Works in the Editor and at runtime.</summary>
        public static BoardView Create(Transform parent, BoardCamera camera)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.tiles = view.Group("Tiles");
            view.pieces = view.Group("Pieces");
            view.tunnels = view.Group("Tunnels");
            view.clues = view.Group("Clues");
            view.boardCamera = camera;
            return view;
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Camera UnityCamera => boardCamera != null ? boardCamera.Camera : Camera.main;

        // ------------------------------------------------------------------ IBoardView

        public void Load(LevelDefinition level)
        {
            Clear();
            LastTappedCell = null;
            if (level == null) return;

            Level = level.ToLevelData();
            Board = new Board(Level);

            BuildTiles();
            BuildFixedPieces();
            BuildTunnels();
            BuildClues();

            if (boardCamera != null) boardCamera.SetTarget(BoardLayout.HalfWidth(Level.Width), BoardLayout.HalfDepth(Level.Height));
        }

        public void SetInteractable(bool interactable) => _interactable = interactable;

        /// <summary>Raises <see cref="Completed"/>. Used by the editor-only debug button until M7 wires the validator.</summary>
        public void ForceComplete() => Completed?.Invoke();

        // ------------------------------------------------------------------ building

        private void Clear()
        {
            foreach (var group in new[] { tiles, pieces, tunnels, clues })
                if (group != null) UiBuilder.Clear(group);
            _cells = null;
            _columnClues = null;
            _rowClues = null;
            Level = null;
            Board = null;
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
                tile.GetComponent<MeshRenderer>().sharedMaterial = (x + y) % 2 == 0 ? BoardMaterials.Tile : BoardMaterials.TileAlt;

                var cell = tile.AddComponent<BoardCell>();
                cell.Set(x, y);
                _cells[x, y] = cell;
            }
        }

        private void BuildFixedPieces()
        {
            foreach (var piece in Level.FixedPieces)
            {
                var holder = new GameObject($"Fixed {piece.Key} ({piece.X},{piece.Y})");
                holder.transform.SetParent(pieces, false);
                var (wx, wz) = BoardLayout.CellCenter(piece.X, piece.Y, Level.Width, Level.Height);
                holder.transform.localPosition = new Vector3((float)wx, 0f, (float)wz);

                var (a, b) = PieceKeys.Connections(piece.Key);
                AddArm(holder.transform, a);
                AddArm(holder.transform, b);

                var hub = Primitive(PrimitiveType.Cube, holder.transform, BoardMaterials.FixedPiece, "Hub");
                hub.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                hub.transform.localScale = new Vector3(0.22f, 0.1f, 0.22f);
            }
        }

        /// <summary>A bar from the cell centre to the midpoint of one side.</summary>
        private static void AddArm(Transform parent, Direction side)
        {
            var arm = Primitive(PrimitiveType.Cube, parent, BoardMaterials.FixedPiece, $"Arm {side}");
            var (dx, dz) = BoardLayout.Step(side);
            arm.transform.localPosition = new Vector3((float)dx * 0.25f, 0.05f, (float)dz * 0.25f);
            arm.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(side), 0f);
            arm.transform.localScale = new Vector3(0.18f, 0.1f, 0.5f);
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

            var mouth = Primitive(PrimitiveType.Cube, holder.transform, material, "Mouth");
            mouth.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mouth.transform.localScale = new Vector3(0.8f, 0.7f, 0.5f);

            var hole = Primitive(PrimitiveType.Cube, holder.transform, BoardMaterials.FixedPiece, "Hole");
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

        /// <summary>Recolours the clue labels; M7 calls this when the validator reports a line satisfied.</summary>
        public void SetClueColors(bool[] columnsSatisfied, bool[] rowsSatisfied)
        {
            if (_columnClues == null) return;
            for (var x = 0; x < _columnClues.Length; x++)
                _columnClues[x].color = columnsSatisfied != null && columnsSatisfied[x] ? UiBuilder.Success : UiBuilder.TextColor;
            for (var y = 0; y < _rowClues.Length; y++)
                _rowClues[y].color = rowsSatisfied != null && rowsSatisfied[y] ? UiBuilder.Success : UiBuilder.TextColor;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            // Only the cell tiles need colliders; everything else must not block the tap raycast.
            if (go.TryGetComponent<Collider>(out var collider)) Destroy(collider);
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

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (!_interactable || _cells == null) return;
            var camera = UnityCamera;
            if (camera == null) return;
            if (!TryGetPress(out var position, out var touchId)) return;
            if (IsOverUi(touchId)) return;

            LastTappedCell = null;
            var ray = camera.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out var hit, RayLength) && hit.collider.TryGetComponent<BoardCell>(out var cell))
                LastTappedCell = (cell.X, cell.Y);

            Interacted?.Invoke();
        }

        private static bool TryGetPress(out Vector2 position, out int touchId)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                touchId = touch.primaryTouch.touchId.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                touchId = -1;
                return true;
            }

            position = default;
            touchId = -1;
            return false;
        }

        private static bool IsOverUi(int touchId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            return touchId >= 0 ? eventSystem.IsPointerOverGameObject(touchId) : eventSystem.IsPointerOverGameObject();
        }
    }
}
