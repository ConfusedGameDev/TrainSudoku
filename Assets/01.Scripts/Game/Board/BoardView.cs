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

        /// <summary>How far the top of a slab is cut back, in world units. The mesh is generated to match the tile's
        /// own proportions, so the chamfer is the same size on a wide board as on a small one.</summary>
        private const float TileChamfer = 0.035f;

        /// <summary>
        /// A clue chip, converted from the artboard at its 95 px cell pitch = one cell: a 72 px disc with a 6 px ring
        /// and a 46 px numeral on it. The chip is barely wider than the numeral it replaces, which is what keeps the
        /// camera fit where M17 put it -- <see cref="ContentBounds"/> encapsulates the clue group.
        /// </summary>
        private const float ChipRadius = 0.38f;
        private const float ChipRingWidth = 0.065f;
        private const float ChipNumeral = 0.48f;

        /// <summary>How far the chip sits behind the numeral, and how high the whole clue floats off the ground.</summary>
        private const float ChipDepth = 0.01f;
        private const float ChipHeight = 0.05f;

        /// <summary>The erase ring's radii, in cells, and how long a clue's pop lasts (work order 9).</summary>
        private const float HoldRingOuter = 0.42f;
        private const float HoldRingInner = 0.31f;
        private const float CluePopDuration = 0.16f;
        private const float CluePopScale = 1.2f;

        /// <summary>The raised lip along a platform edge, and the decals painted along it (D13).</summary>
        private const float PlatformEdgeDepth = 0.22f;
        private const float PlatformEdgeHeight = 0.05f;
        private const float DecalSpacing = 2.2f;
        private const float DecalHeight = 0.17f;

        /// <summary>The tunnel portal's opening, as fractions of the mouth block (see ProceduralBoardMesh.ArchPortal).
        /// Only the fallback mouth uses these, for a project with no tunnel model assigned.</summary>
        private const float PortalHalfWidth = 0.32f;
        private const float PortalSpringLine = -0.05f;
        private const float PortalCrown = 0.34f;

        /// <summary>
        /// The kit connector as modelled: a rectangular liner 0.9 wide and 0.9 tall over a 0.2 depth, sitting on the
        /// ground and centred on its other two axes, with a 0.7 by 0.7 bore through it and so a 0.1 wall all round.
        /// Stretching Z by <c>length / ConnectorDepth</c> turns that ring into the tube the train runs through.
        /// </summary>
        private const float ConnectorDepth = 0.2f;
        private const float ConnectorWall = 0.1f;
        private static readonly Vector2 ConnectorBore = new Vector2(0.7f, 0.7f);

        /// <summary>How thick the plug at the tunnel's far end is.</summary>
        private const float CapDepth = 0.04f;

        /// <summary>How far into the tunnel a car is revealed, as a share of the tunnel's length.</summary>
        private const float TunnelRevealFraction = 0.35f;

        /// <summary>The tutorial's ring. Wider than the erase ring so the two never read as the same cue.</summary>
        private const float GuideRingInner = 0.37f;
        private const float GuideRingOuter = 0.47f;

        [SerializeField] private Transform tiles;
        [SerializeField] private Transform pieces;
        [SerializeField] private Transform tunnels;
        [SerializeField] private Transform clues;
        [SerializeField] private Transform markers;
        [SerializeField] private Transform decor;
        [SerializeField] private Transform guide;
        [SerializeField] private BoardCamera boardCamera;
        [SerializeField] private TrackAssets trackAssets;

        private BoardCell[,] _cells;
        private ClueView[] _columnClues;
        private ClueView[] _rowClues;
        private Mesh _chipDisc;
        private Mesh _chipRing;
        private readonly Dictionary<(int X, int Y), PieceView> _pieceViews = new Dictionary<(int, int), PieceView>();
        private PlacementSession _session;
        private TrackMeshProfile _profile;
        private bool _interactable;
        private bool[] _columnSatisfied;
        private bool[] _rowSatisfied;
        private Mesh _holdMesh;
        private float _eraseRingScale = 1f;
        private Mesh _guideMesh;
        private GuideRing _guideRing;
        private TutorialGuide _guide;

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

        /// <summary>Track pieces currently on the board, or zero when no level is loaded.</summary>
        public int PieceCount => Board != null ? Board.PieceCount : 0;

        /// <summary>Track pieces a solved board holds. Summed from the row clues, which equal the column clues.</summary>
        public int TotalRails
        {
            get
            {
                if (Level == null) return 0;
                var total = 0;
                foreach (var clue in Level.RowClues) total += clue;
                return total;
            }
        }

        public bool IsGenerated => tiles != null && pieces != null && tunnels != null && clues != null && markers != null;

        public event Action Interacted;
        public event Action Completed;

        /// <summary>A piece was placed or erased. Raised before the validator runs.</summary>
        public event Action BoardChanged;

        /// <summary>
        /// A row (true) or column (false) at that index has just gone from unsatisfied to satisfied through a player
        /// action. The Play screen prints it on the LED strip (work order 9, clue satisfied). Loading a saved board
        /// never raises it, for the same reason loading never announces a win.
        /// </summary>
        public event Action<bool, int> LineCleared;

        /// <summary>
        /// What the player just did, for the tutorial coach (<see cref="TutorialCoach"/>). Raised <b>after</b> the
        /// validator has run, so a listener reading <see cref="HasOverfullLine"/> in the handler sees the board as it
        /// now is rather than as it was before the action.
        /// </summary>
        public event Action<BoardAction> Acted;

        /// <summary>
        /// Whether any row or column holds more track than its clue allows — the red state of the clue chips, as a
        /// single answer. Recomputed by the validator on every change.
        /// </summary>
        public bool HasOverfullLine { get; private set; }

        /// <summary>The cell the player currently has selected, for the tutorial's sub-steps.</summary>
        public (int X, int Y)? SelectedCell =>
            _session != null && _session.IsActive ? (_session.X, _session.Y) : ((int X, int Y)?)null;

        /// <summary>The first side already taken on the selected cell, if any.</summary>
        public Direction? ChosenSide => _session != null && _session.IsActive ? _session.First : null;

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
            view.decor = view.Group("Decor");
            view.guide = view.Group("Guide");
            view.boardCamera = camera;
            view.trackAssets = trackAssets;
            return view;
        }

        /// <summary>Runtime configuration for scenes generated before these references existed.</summary>
        public void Configure(TrackAssets assets)
        {
            if (assets != null) trackAssets = assets;
            if (markers == null) markers = Group("Markers");
            if (decor == null) decor = Group("Decor");
            if (guide == null) guide = Group("Guide");
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
            BuildDecor();

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
            // Decor is deliberately absent: environment art must never be able to move the camera. Everything
            // BuildDecor lays down sits inside the tunnel ring, which the minimum box above already covers.
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

        /// <summary>Runs the Core validator, colours the clues and, after a player action, fires the win.</summary>
        private void Validate(bool announceWin)
        {
            if (Board == null) return;
            LastResult = WinChecker.Evaluate(Board);
            ApplyClueColors(LastResult, announceWin);
            if (announceWin && LastResult.IsWin) Completed?.Invoke();
        }

        private void OnBoardChanged()
        {
            BoardChanged?.Invoke();
            Validate(true);
        }

        /// <summary>
        /// Satisfied lines turn green, lines holding more pieces than their clue turn red. A line that has just
        /// become satisfied also pops its clue and is announced, but only when the change came from a player action.
        /// </summary>
        private void ApplyClueColors(WinResult result, bool announce)
        {
            if (_columnClues == null || result == null) return;
            var overfull = false;
            for (var x = 0; x < _columnClues.Length; x++)
            {
                var exceeded = Board.ColumnCount(x) > Level.ColumnClues[x];
                overfull |= exceeded;
                _columnClues[x].Dress(result.ColumnSatisfied[x], exceeded);
                if (announce && result.ColumnSatisfied[x] && !_columnSatisfied[x]) Cleared(_columnClues[x], false, x);
                _columnSatisfied[x] = result.ColumnSatisfied[x];
            }

            for (var y = 0; y < _rowClues.Length; y++)
            {
                var exceeded = Board.RowCount(y) > Level.RowClues[y];
                overfull |= exceeded;
                _rowClues[y].Dress(result.RowSatisfied[y], exceeded);
                if (announce && result.RowSatisfied[y] && !_rowSatisfied[y]) Cleared(_rowClues[y], true, y);
                _rowSatisfied[y] = result.RowSatisfied[y];
            }

            HasOverfullLine = overfull;
        }

        private void Cleared(ClueView clue, bool isRow, int index)
        {
            // The pop is on the chip's root, so it carries the numeral with it rather than popping the two apart.
            PopScale.Play(clue.Root, CluePopScale, CluePopDuration);
            LineCleared?.Invoke(isRow, index);
        }

        // ------------------------------------------------------------------ building

        private void Clear()
        {
            foreach (var group in new[] { tiles, pieces, tunnels, clues, markers, decor, guide })
                if (group != null) SceneObjects.Clear(group);
            // The bent meshes belong to the level that just went away, and the next one may use a different profile.
            TrackMeshBender.Clear();
            _cells = null;
            _columnClues = null;
            _rowClues = null;
            DestroyMesh(ref _chipDisc);
            DestroyMesh(ref _chipRing);
            _columnSatisfied = null;
            _rowSatisfied = null;
            DestroyHoldMesh();
            DestroyMesh(ref _guideMesh);
            _guideRing = null;
            _guide = TutorialGuide.None;
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
                // A slab, not a box: the cube's collider is kept for picking, its mesh swapped for a chamfered one
                // of exactly the same extents (see ProceduralBoardMesh), so ContentBounds cannot move.
                tile.GetComponent<MeshFilter>().sharedMesh = ProceduralBoardMesh.ChamferedTile(TileChamfer / size, TileChamfer / TileHeight);
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

        /// <summary>
        /// How far a tunnel runs out from the board edge, in cells. Capped at <see cref="BoardLayout.Padding"/>: that
        /// ring is what <see cref="ContentBounds"/> already reserves, so a tunnel inside it cannot move the camera fit.
        /// </summary>
        private float TunnelLength =>
            trackAssets == null ? 0f : Mathf.Clamp(trackAssets.TunnelLength, 0.1f, (float)BoardLayout.Padding);

        /// <summary>
        /// Where along the route the train should become visible: a third of the way into the tunnel, so a car is
        /// revealed inside the tube rather than in the open. Without a tunnel model this is the old arch's mid-plane.
        /// </summary>
        public double TunnelRevealDistance =>
            trackAssets == null || trackAssets.TunnelModel == null
                ? 0.5
                : TrackPath.TunnelExtension - TunnelLength * TunnelRevealFraction;

        private void BuildTunnels()
        {
            BuildTunnel(Level.Entrance, "Entrance", BoardMaterials.Entrance);
            BuildTunnel(Level.Exit, "Exit", BoardMaterials.Exit);
        }

        /// <summary>
        /// One tunnel mouth. It carries no lettering: the artboard has none, and the two ends are told apart by the
        /// plug at the far end of each tube -- ink for the entrance, signage red for the exit.
        /// </summary>
        private void BuildTunnel(Tunnel tunnel, string name, Material material)
        {
            if (!tunnel.IsOnPerimeter(Level.Width, Level.Height)) return;

            var holder = new GameObject(name);
            holder.transform.SetParent(tunnels, false);
            var (wx, wz) = BoardLayout.TunnelCenter(tunnel, Level.Width, Level.Height);
            holder.transform.localPosition = new Vector3((float)wx, 0f, (float)wz);
            // Face the board: +Z of the tunnel points opposite to the side it sits on.
            holder.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(tunnel.Side.Opposite()), 0f);

            var model = trackAssets != null ? trackAssets.TunnelModel : null;
            if (model != null) BuildTunnelTube(holder.transform, model, material);
            else BuildTunnelArch(holder.transform, material);
        }

        /// <summary>
        /// The tunnel proper: the kit connector stretched along its length into a tube the train runs through. It grows
        /// <b>outward</b> from the board edge — the holder sits half a cell outside it, so the edge is local z +0.5 —
        /// and <see cref="TunnelLength"/> is capped at <see cref="BoardLayout.Padding"/>, which is the ring the camera
        /// already frames. A tube inside that ring cannot move the fit; a longer one would quietly shrink the board.
        /// </summary>
        private void BuildTunnelTube(Transform holder, GameObject model, Material interior)
        {
            var length = TunnelLength;
            var bore = trackAssets.TunnelBore;

            var tube = Instantiate(model, holder);
            tube.name = "Tube";
            // Nothing in a tunnel is tappable: a collider here would swallow taps meant for the board.
            foreach (var collider in tube.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            // Sunk by its own floor slab so the bore's floor, not the model's underside, lands on the rails: the train
            // runs at the tile surface and would otherwise clip straight through the lip on its way in.
            var floor = ConnectorWall * bore.y;
            tube.transform.localPosition = new Vector3(0f, trackAssets.VerticalOffset - floor, (float)(0.5 - length / 2.0));
            tube.transform.localRotation = Quaternion.identity;
            tube.transform.localScale = new Vector3(bore.x, bore.y, (float)(length / ConnectorDepth));

            // The tube looks out onto the background at its far end, which would make it a window rather than a hole.
            // The plug is what carries the S/E colouring now that both mouths wear the same kit material.
            var boreWidth = ConnectorBore.x * bore.x;
            var boreHeight = ConnectorBore.y * bore.y;
            var cap = Primitive(PrimitiveType.Cube, holder, interior, "Cap", false);
            cap.transform.localPosition = new Vector3(0f, trackAssets.VerticalOffset + boreHeight / 2f, (float)(0.5 - length) + CapDepth);
            cap.transform.localScale = new Vector3(boreWidth, boreHeight, CapDepth);
        }

        /// <summary>The fallback mouth for a project with no tunnel model: a swept arch spanning the same unit cube.</summary>
        private void BuildTunnelArch(Transform holder, Material material)
        {
            var mouth = new GameObject("Mouth");
            mouth.transform.SetParent(holder, false);
            mouth.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mouth.transform.localScale = new Vector3(0.8f, 0.7f, 0.5f);
            mouth.AddComponent<MeshFilter>().sharedMesh = ProceduralBoardMesh.ArchPortal(PortalHalfWidth, PortalSpringLine, PortalCrown);
            mouth.AddComponent<MeshRenderer>().sharedMaterial = material;

            // The darkness the arch looks into. It has to be wider and taller than the opening, or the portal is a
            // window onto the background rather than a tunnel.
            var hole = Primitive(PrimitiveType.Cube, holder, BoardMaterials.FixedPiece, "Hole", false);
            hole.transform.localPosition = new Vector3(0f, 0.31f, 0.16f);
            hole.transform.localScale = new Vector3(0.62f, 0.62f, 0.15f);
        }

        /// <summary>
        /// A clue is a chip with its number on it: a disc, a ring around it and the numeral, all sharing one root so
        /// the satisfied pop moves them together. The two meshes are built once and shared by every chip on the board.
        /// </summary>
        private void BuildClues()
        {
            _chipDisc = new Mesh { name = "Clue Chip" };
            ProceduralBoardMesh.FillRing(_chipDisc, 0f, ChipRadius, 1f);
            _chipRing = new Mesh { name = "Clue Chip Ring" };
            ProceduralBoardMesh.FillRing(_chipRing, ChipRadius - ChipRingWidth, ChipRadius, 1f);

            _columnClues = new ClueView[Level.Width];
            _columnSatisfied = new bool[Level.Width];
            for (var x = 0; x < Level.Width; x++)
            {
                var (wx, wz) = BoardLayout.ColumnClueAnchor(x, Level.Width, Level.Height);
                _columnClues[x] = BuildClue(Level.ColumnClues[x], (float)wx, (float)wz, $"Column clue {x}");
            }

            _rowClues = new ClueView[Level.Height];
            _rowSatisfied = new bool[Level.Height];
            for (var y = 0; y < Level.Height; y++)
            {
                var (wx, wz) = BoardLayout.RowClueAnchor(y, Level.Width, Level.Height);
                _rowClues[y] = BuildClue(Level.RowClues[y], (float)wx, (float)wz, $"Row clue {y}");
            }
        }

        private ClueView BuildClue(int value, float wx, float wz, string name)
        {
            var pitch = boardCamera != null ? boardCamera.PitchDegrees : 60f;
            var root = new GameObject(name);
            root.transform.SetParent(clues, false);
            root.transform.localPosition = new Vector3(wx, ChipHeight, wz);
            root.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var fill = ChipPart(root.transform, "Fill", _chipDisc, ChipDepth);
            var ring = ChipPart(root.transform, "Ring", _chipRing, ChipDepth * 0.5f);

            var label = Label(root.transform, value.ToString(), ChipNumeral, Palette.Ink);
            // The root already carries the pitch; the label would otherwise apply it a second time.
            label.transform.localRotation = Quaternion.identity;
            label.transform.localPosition = Vector3.zero;
            label.name = "Numeral";

            var clue = new ClueView(root, label, fill, ring);
            clue.Dress(false, false);
            return clue;
        }

        /// <summary>
        /// One flat disc of a chip, stood up into the numeral's plane. <see cref="ProceduralBoardMesh.FillRing"/>
        /// builds in XZ, so it is turned a quarter to face the camera the way the text does, and pushed a little way
        /// behind the glyph so the two never z-fight.
        /// </summary>
        private static MeshRenderer ChipPart(Transform parent, string name, Mesh mesh, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localPosition = new Vector3(0f, 0f, depth);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return go.AddComponent<MeshRenderer>();
        }

        /// <summary>
        /// Environment art (D13): a raised yellow lip along each long platform edge, with the Japanese warning
        /// painted along it the way a real platform carries one.
        /// </summary>
        /// <remarks>
        /// This is the one place Japanese is allowed in every locale, because it is <b>geometry and a font atlas,
        /// never a <c>VisualElement</c></b>. Without a signage face assigned in <see cref="TrackAssets"/> the lips
        /// are still laid but the text is skipped — an unpainted platform edge is fine, a row of missing-glyph
        /// boxes is not.
        ///
        /// Everything here sits between the board edge and the tunnel mouths, inside the ring the camera is already
        /// told to keep in view, and the group is left out of <see cref="ContentBounds"/> so it can never move the
        /// camera however it grows.
        /// </remarks>
        private void BuildDecor()
        {
            if (decor == null) return;

            var halfDepth = Level.Height * (float)BoardLayout.CellSize / 2f;
            var width = Level.Width * (float)BoardLayout.CellSize;
            var font = trackAssets != null ? trackAssets.SignageFont : null;
            var warning = trackAssets != null ? trackAssets.PlatformWarning : null;

            foreach (var side in new[] { -1f, 1f })
            {
                var z = side * (halfDepth + PlatformEdgeDepth / 2f);
                var lip = Primitive(PrimitiveType.Cube, decor, BoardMaterials.PlatformEdge, "Platform Edge", false);
                lip.transform.localPosition = new Vector3(0f, 0f, z);
                lip.transform.localScale = new Vector3(width + PlatformEdgeDepth * 2f, PlatformEdgeHeight, PlatformEdgeDepth);

                if (font == null || string.IsNullOrEmpty(warning)) continue;

                // Repeat the warning along the lip, evenly, at least once even on the narrowest board.
                var count = Mathf.Max(1, Mathf.RoundToInt(width / DecalSpacing));
                for (var i = 0; i < count; i++)
                {
                    var x = -width / 2f + width * (i + 0.5f) / count;
                    var decal = Decal(font, warning, DecalHeight);
                    decal.transform.localPosition = new Vector3(x, PlatformEdgeHeight / 2f + 0.005f, z);
                }
            }
        }

        /// <summary>Text painted flat on the ground, reading west to east, rather than turned to face the camera.</summary>
        private TextMesh Decal(Font font, string text, float worldHeight)
        {
            var go = new GameObject($"Decal {text}");
            go.transform.SetParent(decor, false);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = worldHeight / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Palette.Ink;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return mesh;
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
            mesh.font = Palette.Font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = worldHeight / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Font.material;
            var pitch = boardCamera != null ? boardCamera.PitchDegrees : 60f;
            go.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            return mesh;
        }

        // ------------------------------------------------------------------ selection visuals

        /// <summary>
        /// Rebuilds the tile tints and the edge markers from the session state (PRD section 4). The selected cell wears
        /// the platform yellow and each of its four neighbours is marked with how that side reads: green where the
        /// player may connect, red where they may not, the line colour where they must. A side that leaves the board
        /// has no neighbouring slab to tint, so it keeps a marker slab at the cell's edge instead — that is where the
        /// S/E tunnel and a plain board wall show up.
        /// </summary>
        private void RefreshSelection()
        {
            if (markers != null) SceneObjects.Clear(markers);
            _holdIndicator = null;
            if (_cells == null) return;

            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
                _cells[x, y].GetComponent<MeshRenderer>().sharedMaterial = TileMaterial(x, y);

            if (_session == null || !_session.IsActive) return;

            _cells[_session.X, _session.Y].GetComponent<MeshRenderer>().sharedMaterial = BoardMaterials.TileSelected;
            var (cx, cz) = BoardLayout.CellCenter(_session.X, _session.Y, Level.Width, Level.Height);
            foreach (var side in DirectionExtensions.All)
            {
                var mark = _session.MarkOf(side);
                var material = MarkMaterial(mark);
                var nx = _session.X + side.Dx();
                var ny = _session.Y + side.Dy();
                if (Board.InBounds(nx, ny))
                {
                    _cells[nx, ny].GetComponent<MeshRenderer>().sharedMaterial = material;
                    continue;
                }

                var marker = Primitive(PrimitiveType.Cube, markers, material, $"Marker {side}", true);
                var (dx, dz) = BoardLayout.Step(side);
                marker.transform.localPosition = new Vector3((float)(cx + dx * MarkerInset), 0.08f, (float)(cz + dz * MarkerInset));
                marker.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(side), 0f);
                marker.transform.localScale = new Vector3(0.18f, 0.06f, 0.26f);
                marker.AddComponent<BoardMarker>().Set(side, mark);
            }
        }

        /// <summary>The slab colour for one side of the selected cell. Chosen wears the selected cell's own yellow, so
        /// a made connection reads as part of the piece being built rather than as another offer.</summary>
        private static Material MarkMaterial(SideMark mark)
        {
            switch (mark)
            {
                case SideMark.Open: return BoardMaterials.TileOpen;
                case SideMark.Forced: return BoardMaterials.TileForced;
                case SideMark.Chosen: return BoardMaterials.TileSelected;
                default: return BoardMaterials.TileBlocked;
            }
        }

        /// <summary>
        /// The erase ring's colour and how far its radii are scaled, from the Game Manager's inspector. The colour
        /// goes straight to the shared material, so it applies to a ring already on screen.
        /// </summary>
        public void SetEraseRing(Color colour, float scale)
        {
            BoardMaterials.SetHoldColour(colour);
            _eraseRingScale = Mathf.Max(0.05f, scale);
        }

        // ------------------------------------------------------------------ the tutorial's guide

        /// <summary>
        /// Where the tutorial is pointing (<see cref="TutorialCoach"/>): the board rings that cell, and while the
        /// guide is locked it refuses every tap that is not on it. <see cref="TutorialGuide.None"/> turns both off,
        /// which is the state every non-tutorial level stays in.
        /// </summary>
        public void SetGuide(TutorialGuide value)
        {
            _guide = value;
            RefreshGuideRing();
        }

        /// <summary>
        /// The three world points the callout needs: the guided target's centre and the mid-points of the edges
        /// away from and towards the camera. Both edges are given rather than one offset mirrored, because the
        /// board is seen at a pitch and the two do not project to the same distance on screen.
        /// </summary>
        public bool TryGuideAnchor(out Vector3 centre, out Vector3 far, out Vector3 near)
        {
            centre = far = near = Vector3.zero;
            if (Level == null || !_guide.Active) return false;

            var local = GuidePoint();
            centre = transform.TransformPoint(local);
            far = transform.TransformPoint(local + new Vector3(0f, 0f, (float)BoardLayout.CellSize / 2f));
            near = transform.TransformPoint(local - new Vector3(0f, 0f, (float)BoardLayout.CellSize / 2f));
            return true;
        }

        /// <summary>
        /// Where the ring goes, in board-local space: the cell to tap. For a side the player is being asked for,
        /// that is the neighbour — or, when the side leaves the board, the edge marker standing in for it.
        /// </summary>
        private Vector3 GuidePoint()
        {
            var (x, y) = (_guide.X, _guide.Y);
            if (_guide.Side.HasValue)
            {
                var side = _guide.Side.Value;
                var nx = x + side.Dx();
                var ny = y + side.Dy();
                if (Board != null && Board.InBounds(nx, ny))
                {
                    var (ix, iz) = BoardLayout.CellCenter(nx, ny, Level.Width, Level.Height);
                    return new Vector3((float)ix, 0f, (float)iz);
                }

                var (ex, ez) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
                var (sx, sz) = BoardLayout.Step(side);
                return new Vector3((float)(ex + sx * MarkerInset), 0f, (float)(ez + sz * MarkerInset));
            }

            var (cx, cz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
            return new Vector3((float)cx, 0f, (float)cz);
        }

        /// <summary>
        /// Builds or moves the ring. It hangs off its own <c>Guide</c> group rather than <c>Markers</c>, because
        /// <see cref="RefreshSelection"/> clears that group wholesale on every selection change — and for the same
        /// reason it is a ring of its own rather than a tile material, which that method also rewrites.
        /// </summary>
        private void RefreshGuideRing()
        {
            if (guide == null || Level == null || !_guide.Active)
            {
                if (guide != null) SceneObjects.Clear(guide);
                _guideRing = null;
                DestroyMesh(ref _guideMesh);
                return;
            }

            if (_guideRing == null)
            {
                _guideMesh = new Mesh { name = "Guide Ring" };
                ProceduralBoardMesh.FillRing(_guideMesh, GuideRingInner, GuideRingOuter, 1f);

                var go = new GameObject("Guide Ring");
                go.transform.SetParent(guide, false);
                go.AddComponent<MeshFilter>().sharedMesh = _guideMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = BoardMaterials.Guide;
                _guideRing = go.AddComponent<GuideRing>();
            }

            _guideRing.transform.localPosition = GuidePoint() + new Vector3(0f, 0.16f, 0f);
        }

        /// <summary>Whether a tap on this cell is allowed through while the tutorial has the board locked.</summary>
        private bool AllowsCell(int x, int y)
        {
            if (!_guide.Locked) return true;

            var tap = _guide.TapCell;
            if (tap.X == x && tap.Y == y) return true;

            // Tapping the guided cell again while a side is being asked for only cancels the selection. Backing
            // out of a choice is never the wrong move, so it is not refused.
            return _guide.Side.HasValue && _guide.X == x && _guide.Y == y;
        }

        /// <summary>The same for an edge marker, which stands for a side that leaves the board.</summary>
        private bool AllowsMarker(Direction side) =>
            !_guide.Locked || (_guide.Side.HasValue && _guide.Side.Value == side);

        /// <summary>
        /// A tap the locked tutorial will not take. It is answered by the ring rather than by the error cue: the
        /// player has not done anything wrong, they have simply looked away from what is being pointed at.
        /// </summary>
        private void Refuse()
        {
            _pressConsumed = true;
            _pressCell = null;
            if (_guideRing != null) _guideRing.Pop();
            Acted?.Invoke(BoardAction.Refused);
        }

        /// <summary>
        /// The erase ring: a flat annulus that fills round the held cell over <see cref="HoldDuration"/>, linearly,
        /// so the sweep is a readable countdown rather than a guess (work order 9).
        /// </summary>
        private void ShowHoldProgress((int X, int Y) cell, float progress)
        {
            if (_holdIndicator == null)
            {
                _holdMesh = new Mesh { name = "Hold Ring" };
                _holdIndicator = new GameObject("Hold Ring");
                _holdIndicator.transform.SetParent(markers, false);
                var (cx, cz) = BoardLayout.CellCenter(cell.X, cell.Y, Level.Width, Level.Height);
                _holdIndicator.transform.localPosition = new Vector3((float)cx, 0.14f, (float)cz);
                _holdIndicator.AddComponent<MeshFilter>().sharedMesh = _holdMesh;
                _holdIndicator.AddComponent<MeshRenderer>().sharedMaterial = BoardMaterials.Hold;
            }

            ProceduralBoardMesh.FillRing(_holdMesh, HoldRingInner * _eraseRingScale, HoldRingOuter * _eraseRingScale,
                Mathf.Clamp01(progress));
        }

        private void HideHoldProgress()
        {
            if (_holdIndicator == null) return;
            Destroy(_holdIndicator);
            _holdIndicator = null;
            DestroyHoldMesh();
        }

        /// <summary>The ring owns its mesh, so it has to be released with it; a generated mesh is not collected.</summary>
        private void DestroyHoldMesh() => DestroyMesh(ref _holdMesh);

        private void DestroyMesh(ref Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
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

            // An edge marker stands for a side that leaves the board. A blocked one is a wall, so it is not a target:
            // let the press fall through rather than consume it.
            if (hit.collider.TryGetComponent<BoardMarker>(out var marker) && _session.MarkOf(marker.Side) != SideMark.Blocked)
            {
                if (!AllowsMarker(marker.Side))
                {
                    Refuse();
                    return;
                }

                _pressConsumed = true;
                ApplyChoose(_session.Choose(marker.Side));
                return;
            }

            if (hit.collider.TryGetComponent<BoardCell>(out var cell))
            {
                if (!AllowsCell(cell.X, cell.Y))
                {
                    Refuse();
                    return;
                }

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
                Acted?.Invoke(BoardAction.EraseRefused);
            }
            else if (Board.TryErase(cell.X, cell.Y))
            {
                RemovePiece(cell.X, cell.Y);
                _session.Cancel();
                RefreshSelection();
                AudioCuePlayer.Play(AudioCue.Erase);
                OnBoardChanged();
                Acted?.Invoke(BoardAction.Erased);
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
                    Acted?.Invoke(BoardAction.Deselected);
                }

                return;
            }

            // While a cell is selected, its marked neighbours are the choice: tapping one makes a connection instead of
            // moving the selection. A blocked neighbour falls through and selects normally, so red stays informational.
            if (_session.IsActive && TryNeighbourSide(cell.Value, out var side) && _session.MarkOf(side) != SideMark.Blocked)
            {
                ApplyChoose(_session.Choose(side));
                return;
            }

            // A short tap on a placed piece does nothing (PRD 4.7); Select rejects occupied cells silently.
            var occupied = Board[cell.Value.X, cell.Value.Y].HasValue;
            var outcome = _session.Select(cell.Value.X, cell.Value.Y);
            switch (outcome)
            {
                case SelectOutcome.AutoPlaced:
                    OnPlaced(BoardAction.AutoPlaced);
                    break;
                case SelectOutcome.Rejected:
                    if (!occupied) AudioCuePlayer.Play(AudioCue.Error);
                    RefreshSelection();
                    break;
                case SelectOutcome.Selected:
                    Haptics.Play(HapticFeel.Selection);
                    RefreshSelection();
                    Acted?.Invoke(BoardAction.Selected);
                    break;
                default:
                    RefreshSelection();
                    Acted?.Invoke(BoardAction.Deselected);
                    break;
            }
        }

        /// <summary>True when the tapped cell is orthogonally adjacent to the selected one, and on which side it lies.</summary>
        private bool TryNeighbourSide((int X, int Y) cell, out Direction side)
        {
            foreach (var candidate in DirectionExtensions.All)
                if (_session.X + candidate.Dx() == cell.X && _session.Y + candidate.Dy() == cell.Y)
                {
                    side = candidate;
                    return true;
                }

            side = Direction.North;
            return false;
        }

        private void ApplyChoose(ChooseOutcome outcome)
        {
            if (outcome == ChooseOutcome.Placed)
            {
                OnPlaced(BoardAction.Connected);
                return;
            }

            // Narrowing to the first side, or letting it go again, is a change of selection rather than a placement.
            if (outcome != ChooseOutcome.Ignored) Haptics.Play(HapticFeel.Selection);
            RefreshSelection();

            // Reverting puts the cell back to how it looked on selection, so it reads to the coach as a fresh one.
            if (outcome == ChooseOutcome.Narrowed) Acted?.Invoke(BoardAction.Narrowed);
            else if (outcome == ChooseOutcome.Reverted) Acted?.Invoke(BoardAction.Selected);
        }

        private void OnPlaced(BoardAction action)
        {
            var (x, y) = _session.LastPlacedCell;
            if (_session.LastPlaced.HasValue) SpawnPiece(x, y, _session.LastPlaced.Value, false, true);
            RefreshSelection();
            AudioCuePlayer.Play(AudioCue.Place);
            OnBoardChanged();
            Acted?.Invoke(action);
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

    /// <summary>
    /// One clue on the board: the number, and the chip it sits on. The chip is filled in the active line once its row
    /// or column holds exactly as many pieces as it asks for, ringed in the closed grey until then, and ringed in the
    /// signage red when the line has been overfilled.
    /// </summary>
    public readonly struct ClueView
    {
        public readonly GameObject Root;
        private readonly TextMesh _numeral;
        private readonly MeshRenderer _fill;
        private readonly MeshRenderer _ring;

        public ClueView(GameObject root, TextMesh numeral, MeshRenderer fill, MeshRenderer ring)
        {
            Root = root;
            _numeral = numeral;
            _fill = fill;
            _ring = ring;
        }

        public void Dress(bool satisfied, bool exceeded)
        {
            if (_fill == null || _ring == null || _numeral == null) return;

            _fill.sharedMaterial = satisfied ? BoardMaterials.ClueChip : BoardMaterials.ClueChipIdle;
            _ring.sharedMaterial = satisfied
                ? BoardMaterials.ClueChipEdge
                : exceeded
                    ? BoardMaterials.ClueChipError
                    : BoardMaterials.ClueChipIdleEdge;

            // The numeral sits on a light chip either way, so it stays ink unless the line is overfilled, which is
            // the one state that has to read as a fault rather than as progress.
            _numeral.color = exceeded && !satisfied ? Palette.ClueExceeded : Palette.Ink;
        }
    }
}
