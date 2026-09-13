using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The XR board (XR-PRD 5.1 and 5.5): tiles, tunnels, standing clue signs, fixed and player track, and the
    /// validator's clue colouring, all placed by Core's <see cref="BoardLayout"/> at one unit per cell. The caller scales
    /// the parent (0.06 makes a cell 6 cm), so nothing here knows about metres.
    /// </summary>
    /// <remarks>
    /// It shows the board and takes no input. Whatever changes <see cref="Board"/> — the grab layer from XR6, the XR4
    /// demo — calls <see cref="Sync"/> afterwards, which brings the pieces on show in line with the board and recolours
    /// the clues. Built in the spirit of the phone's <c>BoardView</c>, which stays the phone's (X21).
    /// </remarks>
    public sealed class XRBoardDisplay : MonoBehaviour
    {
        internal const float TileHeight = 0.1f;
        internal const float TileInset = 0.04f;
        private const float TileChamfer = 0.035f;

        /// <summary>How far above a slab's top the volume a hand aims at to lift its piece reaches, in cells (XR6).</summary>
        private const float GrabVolumeTop = 0.5f;

        /// <summary>How far the shadow catcher reaches past the board on every side, in cells, and how far above the
        /// table it floats so it never fights the real surface for depth.</summary>
        private const float ShadowCatcherMargin = 3f;
        private const float ShadowCatcherLift = 0.01f;

        /// <summary>A clue sign's face: the phone's chip, stood upright on a pole so it reads from any edge (5.5).</summary>
        private const float ChipRadius = 0.38f;
        private const float ChipRingWidth = 0.065f;
        private const float ChipNumeral = 0.48f;
        private const float ChipDepth = 0.01f;
        private const float SignFaceHeight = 0.62f;
        private const float SignPoleWidth = 0.07f;
        private const float CluePopScale = 1.2f;
        private const float CluePopDuration = 0.16f;

        /// <summary>The raised lip along a platform edge, and the decals painted along it (D13).</summary>
        private const float PlatformEdgeDepth = 0.22f;
        private const float PlatformEdgeHeight = 0.05f;
        private const float DecalSpacing = 2.2f;
        private const float DecalHeight = 0.17f;

        /// <summary>The fallback portal's opening, for assets with no tunnel model.</summary>
        private const float PortalHalfWidth = 0.32f;
        private const float PortalSpringLine = -0.05f;
        private const float PortalCrown = 0.34f;

        /// <summary>The kit connector as modelled: a 0.9 liner, 0.2 deep, with a 0.7 bore; see the phone's BoardView.</summary>
        private const float ConnectorDepth = 0.2f;
        private const float ConnectorWall = 0.1f;
        private static readonly Vector2 ConnectorBore = new Vector2(0.7f, 0.7f);
        private const float CapDepth = 0.04f;

        /// <summary>How far into the tunnel a car is revealed, as a share of the tunnel's length.</summary>
        private const float TunnelRevealFraction = 0.35f;

        [SerializeField] private XRBoardAssets assets;

        private Transform _tiles;
        private Transform _pieces;
        private Transform _tunnels;
        private Transform _clues;
        private Transform _decor;
        private XRTrainRun _train;
        private TrackMeshProfile _profile;
        private Mesh _chipDisc;
        private Mesh _chipRing;
        private ClueSign[] _columnClues;
        private ClueSign[] _rowClues;
        private bool[] _columnSatisfied;
        private bool[] _rowSatisfied;
        private GameObject _shadowCatcher;
        private bool _shadowCatcherVisible = true;
        private Mesh _tileMesh;
        private MeshRenderer[,] _tileRenderers;
        private BoxCollider[,] _cellVolumes;
        private readonly List<ClueSign> _signs = new List<ClueSign>();
        private readonly Dictionary<(int X, int Y), (PieceView View, Piece Piece)> _shown = new Dictionary<(int X, int Y), (PieceView View, Piece Piece)>();

        public LevelData Level { get; private set; }
        public Board Board { get; private set; }
        public WinResult LastResult { get; private set; }

        /// <summary>A row or column holds more pieces than its clue asks for.</summary>
        public bool HasOverfullLine { get; private set; }

        /// <summary>The chamfered slab every tile is cut from, at unit size; the tray's tiles and the ghost share it.</summary>
        public Mesh TileMesh => _tileMesh;

        /// <summary>How high a piece sits above the top of its slab, in cells.</summary>
        public float PieceLift => assets != null ? assets.VerticalOffset : 0f;

        /// <summary>The material player track is drawn in, on the board, in the tray and in the hand.</summary>
        public Material PlayerTrackMaterial => XROcclusionMaterials.Occluded(assets != null ? assets.TrackMaterial : XRBoardMaterials.Track);

        /// <summary>The track for a key as this level's profile bends it. Dropped with the level on the next <see cref="Load"/>.</summary>
        public Mesh TrackMeshFor(PieceKey key) => TrackMeshBender.ForKey(_profile, key);

        /// <summary>The volume a hand aims at to lift the piece on a cell: the slab and the air above it.</summary>
        public Collider CellVolume(int x, int y) => InGrid(x, y) && _cellVolumes != null ? _cellVolumes[x, y] : null;

        /// <summary>Where a piece on the cell sits, in world space.</summary>
        public Vector3 CellWorldPosition(int x, int y)
        {
            var (wx, wz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
            return transform.TransformPoint(new Vector3((float)wx, PieceLift, (float)wz));
        }

        /// <summary>The fixed piece on the cell refuses a grab: it wobbles and stays put (4.2).</summary>
        public void ShakePiece(int x, int y)
        {
            if (_shown.TryGetValue((x, y), out var shown) && shown.View != null) shown.View.PlayShake();
        }

        /// <summary>Hides the piece on show at a cell, while its double flies back into it.</summary>
        public void SetPieceVisible(int x, int y, bool visible)
        {
            if (_shown.TryGetValue((x, y), out var shown) && shown.View != null && shown.View.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.enabled = visible;
        }

        /// <summary>Marks the slab under a piece a hand is aiming at, in the selection yellow.</summary>
        public void HighlightCell(int x, int y, bool on)
        {
            if (!InGrid(x, y) || _tileRenderers == null || _tileRenderers[x, y] == null) return;
            _tileRenderers[x, y].sharedMaterial = on ? XRBoardMaterials.PlatformEdge : TileMaterial(x, y);
        }

        private bool InGrid(int x, int y) => Level != null && x >= 0 && y >= 0 && x < Level.Width && y < Level.Height;

        public static XRBoardDisplay Create(Transform parent, XRBoardAssets assets)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var display = go.AddComponent<XRBoardDisplay>();
            display.assets = assets;
            display._tiles = display.Group("Tiles");
            display._pieces = display.Group("Pieces");
            display._tunnels = display.Group("Tunnels");
            display._clues = display.Group("Clues");
            display._decor = display.Group("Decor");
            display._train = XRTrainRun.Create(go.transform, assets);
            return display;
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>Shows a level with only its fixed pieces, and the clues tinted in <paramref name="lineColour"/> once satisfied.</summary>
        public void Load(LevelData level, Color lineColour)
        {
            Clear();
            if (level == null) return;

            Level = level;
            Board = new Board(level);
            // The near edge sits on the parent's origin, so a bigger board grows away from the player and the edge
            // they stand at never moves between levels (X17). Lifted by the slab height so the slabs rest on the
            // surface the origin is on, rather than sinking into it.
            transform.localPosition = new Vector3(0f, TileHeight, (float)BoardLayout.HalfDepth(level.Height));
            _profile = assets != null ? assets.ResolveProfile() : XRBoardAssets.PlaceholderProfile();
            XRBoardMaterials.SetLineColour(lineColour);

            BuildTiles();
            BuildTunnels();
            BuildClues();
            BuildDecor();
            BuildShadowCatcher();
            Sync(false);
        }

        /// <summary>
        /// Shows or hides the surface that catches the board's shadows on the real table (5.6). Off for a board that
        /// floats where no surface was found: a shadow on nothing reads as a fault.
        /// </summary>
        public void ShowShadowCatcher(bool visible)
        {
            _shadowCatcherVisible = visible;
            if (_shadowCatcher != null) _shadowCatcher.SetActive(visible);
        }

        /// <summary>
        /// Brings the pieces on show in line with <see cref="Board"/> and recolours the clues. With
        /// <paramref name="animate"/>, new player pieces scale in and newly satisfied clues pop.
        /// </summary>
        public void Sync(bool animate)
        {
            if (Board == null) return;
            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
            {
                var piece = Board[x, y];
                var isShown = _shown.TryGetValue((x, y), out var shown);
                if (isShown && piece.HasValue && shown.Piece.Equals(piece.Value)) continue;
                if (isShown)
                {
                    Kill(shown.View.gameObject);
                    _shown.Remove((x, y));
                }

                if (piece.HasValue) Spawn(x, y, piece.Value, animate && !piece.Value.IsFixed);
            }

            Validate(animate);
        }

        /// <summary>Where along the route a car becomes visible: a third of the way into the tunnel.</summary>
        public double TunnelRevealDistance =>
            assets == null || assets.TunnelModel == null
                ? 0.5
                : TrackPath.TunnelExtension - TunnelLength * TunnelRevealFraction;

        /// <summary>Runs the train along the board's route and calls <paramref name="finished"/> when it is through.</summary>
        public void RunTrain(string destination, Action finished)
        {
            void OnFinished()
            {
                _train.Finished -= OnFinished;
                finished?.Invoke();
            }

            _train.RevealDistance = TunnelRevealDistance;
            _train.SetDestination(destination);
            _train.Finished += OnFinished;
            _train.Run(Board);
        }

        public void Clear()
        {
            if (_train != null) _train.Stop();
            foreach (var group in new[] { _tiles, _pieces, _tunnels, _clues, _decor }) ClearChildren(group);
            // The bent meshes belong to the level that went away, and the next one may use another profile.
            TrackMeshBender.Clear();
            DestroyMesh(ref _chipDisc);
            DestroyMesh(ref _chipRing);
            // Not destroyed: ProceduralBoardMesh caches the slab and hands the same mesh to every level, so destroying
            // it here left the next level's tiles with no mesh at all.
            _tileMesh = null;
            _tileRenderers = null;
            _cellVolumes = null;
            _signs.Clear();
            _shown.Clear();
            _shadowCatcher = null;
            _columnClues = null;
            _rowClues = null;
            _columnSatisfied = null;
            _rowSatisfied = null;
            Level = null;
            Board = null;
            LastResult = null;
            HasOverfullLine = false;
        }

        private void OnDestroy() => Clear();

        /// <summary>Clue signs turn about the vertical to face the player's head, so they read from every edge (X17, 5.5).</summary>
        private void LateUpdate()
        {
            var head = Camera.main;
            if (head == null || _signs.Count == 0) return;
            var up = transform.up;
            var eye = head.transform.position;
            foreach (var sign in _signs)
            {
                var away = Vector3.ProjectOnPlane(sign.Root.transform.position - eye, up);
                if (away.sqrMagnitude > 1e-8f) sign.Root.transform.rotation = Quaternion.LookRotation(away, up);
            }
        }

        // ------------------------------------------------------------------ validation

        /// <summary>Satisfied lines turn to the line colour, overfilled ones red; a line that has just been satisfied pops.</summary>
        private void Validate(bool announce)
        {
            LastResult = WinChecker.Evaluate(Board);
            if (_columnClues == null) return;
            var overfull = false;
            for (var x = 0; x < _columnClues.Length; x++)
            {
                var exceeded = Board.ColumnCount(x) > Level.ColumnClues[x];
                overfull |= exceeded;
                _columnClues[x].Dress(LastResult.ColumnSatisfied[x], exceeded);
                if (announce && LastResult.ColumnSatisfied[x] && !_columnSatisfied[x]) PopScale.Play(_columnClues[x].Face, CluePopScale, CluePopDuration);
                _columnSatisfied[x] = LastResult.ColumnSatisfied[x];
            }

            for (var y = 0; y < _rowClues.Length; y++)
            {
                var exceeded = Board.RowCount(y) > Level.RowClues[y];
                overfull |= exceeded;
                _rowClues[y].Dress(LastResult.RowSatisfied[y], exceeded);
                if (announce && LastResult.RowSatisfied[y] && !_rowSatisfied[y]) PopScale.Play(_rowClues[y].Face, CluePopScale, CluePopDuration);
                _rowSatisfied[y] = LastResult.RowSatisfied[y];
            }

            HasOverfullLine = overfull;
        }

        // ------------------------------------------------------------------ building

        private void BuildTiles()
        {
            var size = (float)BoardLayout.CellSize - TileInset;
            _tileMesh = ProceduralBoardMesh.ChamferedTile(TileChamfer / size, TileChamfer / TileHeight);
            _tileRenderers = new MeshRenderer[Level.Width, Level.Height];
            _cellVolumes = new BoxCollider[Level.Width, Level.Height];
            for (var y = 0; y < Level.Height; y++)
            for (var x = 0; x < Level.Width; x++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Cell ({x},{y})";
                tile.transform.SetParent(_tiles, false);
                var (wx, wz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
                tile.transform.localPosition = new Vector3((float)wx, -TileHeight / 2f, (float)wz);
                tile.transform.localScale = new Vector3(size, TileHeight, size);
                tile.GetComponent<MeshFilter>().sharedMesh = _tileMesh;
                _tileRenderers[x, y] = tile.GetComponent<MeshRenderer>();
                _tileRenderers[x, y].sharedMaterial = TileMaterial(x, y);

                // The cube's collider is kept and stretched upward from the slab's foot to GrabVolumeTop above its top:
                // it is what a hand aims at to lift the piece on the cell. In the tile's own units the slab spans -0.5 to 0.5.
                var volume = tile.GetComponent<BoxCollider>();
                var top = 0.5f + GrabVolumeTop / TileHeight;
                volume.center = new Vector3(0f, (top - 0.5f) / 2f, 0f);
                volume.size = new Vector3(1f, top + 0.5f, 1f);
                _cellVolumes[x, y] = volume;
            }
        }

        private static Material TileMaterial(int x, int y) => (x + y) % 2 == 0 ? XRBoardMaterials.Tile : XRBoardMaterials.TileAlt;

        private void Spawn(int x, int y, Piece piece, bool animate)
        {
            var go = new GameObject($"{(piece.IsFixed ? "Fixed" : "Piece")} {piece.Key} ({x},{y})");
            go.transform.SetParent(_pieces, false);
            var (wx, wz) = BoardLayout.CellCenter(x, y, Level.Width, Level.Height);
            go.transform.localPosition = new Vector3((float)wx, assets != null ? assets.VerticalOffset : 0f, (float)wz);
            go.AddComponent<MeshFilter>().sharedMesh = TrackMeshBender.ForKey(_profile, piece.Key);
            go.AddComponent<MeshRenderer>().sharedMaterial = XROcclusionMaterials.Occluded(piece.IsFixed
                ? assets != null ? assets.FixedTrackMaterial : XRBoardMaterials.FixedTrack
                : assets != null ? assets.TrackMaterial : XRBoardMaterials.Track);

            var view = go.AddComponent<PieceView>();
            view.Set(x, y, piece.IsFixed);
            if (animate) view.PlayScaleIn();
            _shown[(x, y)] = (view, piece);
        }

        /// <summary>How far a tunnel runs out from the board edge, capped at the padding ring the layout reserves.</summary>
        private float TunnelLength =>
            assets == null ? 0f : Mathf.Clamp(assets.TunnelLength, 0.1f, (float)BoardLayout.Padding);

        private void BuildTunnels()
        {
            BuildTunnel(Level.Entrance, "Entrance", XRBoardMaterials.Entrance);
            BuildTunnel(Level.Exit, "Exit", XRBoardMaterials.Exit);
        }

        /// <summary>One tunnel. The two ends are told apart by the plug at the far end: ink for the entrance, red for the exit.</summary>
        private void BuildTunnel(Tunnel tunnel, string name, Material plug)
        {
            if (!tunnel.IsOnPerimeter(Level.Width, Level.Height)) return;

            var holder = new GameObject(name);
            holder.transform.SetParent(_tunnels, false);
            var (wx, wz) = BoardLayout.TunnelCenter(tunnel, Level.Width, Level.Height);
            holder.transform.localPosition = new Vector3((float)wx, 0f, (float)wz);
            holder.transform.localRotation = Quaternion.Euler(0f, (float)BoardLayout.Yaw(tunnel.Side.Opposite()), 0f);

            var model = assets != null ? assets.TunnelModel : null;
            if (model != null) BuildTunnelTube(holder.transform, model, plug);
            else BuildTunnelArch(holder.transform, plug);
        }

        /// <summary>The kit connector stretched into a tube growing outward from the board edge (local z +0.5).</summary>
        private void BuildTunnelTube(Transform holder, GameObject model, Material plug)
        {
            var length = TunnelLength;
            var bore = assets.TunnelBore;

            var tube = Instantiate(model, holder);
            tube.name = "Tube";
            XROcclusionMaterials.Convert(tube);
            foreach (var collider in tube.GetComponentsInChildren<Collider>()) Kill(collider);

            var floor = ConnectorWall * bore.y;
            tube.transform.localPosition = new Vector3(0f, assets.VerticalOffset - floor, (float)(0.5 - length / 2.0));
            tube.transform.localRotation = Quaternion.identity;
            tube.transform.localScale = new Vector3(bore.x, bore.y, (float)(length / ConnectorDepth));

            var boreWidth = ConnectorBore.x * bore.x;
            var boreHeight = ConnectorBore.y * bore.y;
            var cap = Primitive(PrimitiveType.Cube, holder, plug, "Cap");
            cap.transform.localPosition = new Vector3(0f, assets.VerticalOffset + boreHeight / 2f, (float)(0.5 - length) + CapDepth);
            cap.transform.localScale = new Vector3(boreWidth, boreHeight, CapDepth);
        }

        private void BuildTunnelArch(Transform holder, Material material)
        {
            var mouth = new GameObject("Mouth");
            mouth.transform.SetParent(holder, false);
            mouth.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            mouth.transform.localScale = new Vector3(0.8f, 0.7f, 0.5f);
            mouth.AddComponent<MeshFilter>().sharedMesh = ProceduralBoardMesh.ArchPortal(PortalHalfWidth, PortalSpringLine, PortalCrown);
            mouth.AddComponent<MeshRenderer>().sharedMaterial = material;

            var hole = Primitive(PrimitiveType.Cube, holder, XRBoardMaterials.TunnelInterior, "Hole");
            hole.transform.localPosition = new Vector3(0f, 0.31f, 0.16f);
            hole.transform.localScale = new Vector3(0.62f, 0.62f, 0.15f);
        }

        /// <summary>
        /// A clue is a standing sign: the phone's chip (a disc, a ring and the numeral) raised on a pole. The sign's root
        /// turns to face the player in <see cref="LateUpdate"/>; the face on it is what pops when the line is satisfied.
        /// </summary>
        private void BuildClues()
        {
            _chipDisc = new Mesh { name = "Clue Face" };
            ProceduralBoardMesh.FillRing(_chipDisc, 0f, ChipRadius, 1f);
            _chipRing = new Mesh { name = "Clue Ring" };
            ProceduralBoardMesh.FillRing(_chipRing, ChipRadius - ChipRingWidth, ChipRadius, 1f);

            _columnClues = new ClueSign[Level.Width];
            _columnSatisfied = new bool[Level.Width];
            for (var x = 0; x < Level.Width; x++)
            {
                var (wx, wz) = BoardLayout.ColumnClueAnchor(x, Level.Width, Level.Height);
                _columnClues[x] = BuildClue(Level.ColumnClues[x], (float)wx, (float)wz, $"Column clue {x}");
            }

            _rowClues = new ClueSign[Level.Height];
            _rowSatisfied = new bool[Level.Height];
            for (var y = 0; y < Level.Height; y++)
            {
                var (wx, wz) = BoardLayout.RowClueAnchor(y, Level.Width, Level.Height);
                _rowClues[y] = BuildClue(Level.RowClues[y], (float)wx, (float)wz, $"Row clue {y}");
            }
        }

        private ClueSign BuildClue(int value, float wx, float wz, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_clues, false);
            root.transform.localPosition = new Vector3(wx, 0f, wz);

            // A cylinder primitive is two units tall, so a scale of h/2 makes it h tall.
            var poleHeight = SignFaceHeight - ChipRadius;
            var pole = Primitive(PrimitiveType.Cylinder, root.transform, XRBoardMaterials.SignPole, "Pole");
            pole.transform.localPosition = new Vector3(0f, poleHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(SignPoleWidth, poleHeight / 2f, SignPoleWidth);

            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, SignFaceHeight, 0f);

            var fill = ChipPart(face.transform, "Fill", _chipDisc, ChipDepth);
            var ring = ChipPart(face.transform, "Ring", _chipRing, ChipDepth * 0.5f);
            var numeral = Label(face.transform, value.ToString(), ChipNumeral, XRPalette.Ink);

            var sign = new ClueSign(root, face, numeral, fill, ring);
            sign.Dress(false, false);
            _signs.Add(sign);
            return sign;
        }

        /// <summary>One disc of a sign's face, stood up into the numeral's plane and set just behind it.</summary>
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
        /// Environment art (D13): a raised yellow lip along each long platform edge with the Japanese warning painted
        /// on it. It is art, so it does not turn to face the player, and without a signage face the text is skipped.
        /// </summary>
        private void BuildDecor()
        {
            var halfDepth = Level.Height * (float)BoardLayout.CellSize / 2f;
            var width = Level.Width * (float)BoardLayout.CellSize;
            var font = assets != null ? assets.SignageFont : null;
            var warning = assets != null ? assets.PlatformWarning : null;

            foreach (var side in new[] { -1f, 1f })
            {
                var z = side * (halfDepth + PlatformEdgeDepth / 2f);
                var lip = Primitive(PrimitiveType.Cube, _decor, XRBoardMaterials.PlatformEdge, "Platform Edge");
                lip.transform.localPosition = new Vector3(0f, 0f, z);
                lip.transform.localScale = new Vector3(width + PlatformEdgeDepth * 2f, PlatformEdgeHeight, PlatformEdgeDepth);

                if (font == null || string.IsNullOrEmpty(warning)) continue;
                var count = Mathf.Max(1, Mathf.RoundToInt(width / DecalSpacing));
                for (var i = 0; i < count; i++)
                {
                    var decal = Text(_decor, font, warning, DecalHeight, XRPalette.Ink);
                    decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    decal.transform.localPosition = new Vector3(-width / 2f + width * (i + 0.5f) / count, PlatformEdgeHeight / 2f + 0.005f, z);
                }
            }
        }

        private static TextMesh Label(Transform parent, string text, float worldHeight, Color color)
        {
            var mesh = Text(parent, XRPalette.Font, text, worldHeight, color);
            mesh.name = "Numeral";
            return mesh;
        }

        private static TextMesh Text(Transform parent, Font font, string text, float worldHeight, Color color)
        {
            var go = new GameObject($"Text {text}");
            go.transform.SetParent(parent, false);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = worldHeight / 6.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = XROcclusionMaterials.Text(font);
            return mesh;
        }

        /// <summary>
        /// The invisible surface at table level that catches the board's shadows (5.6). It covers the board's footprint
        /// and a margin all round, so a sign's, the train's or a hand's shadow lands on the real table beside the board.
        /// </summary>
        private void BuildShadowCatcher()
        {
            var material = assets != null ? assets.ShadowCatcherMaterial : null;
            if (material == null) return;

            var quad = Primitive(PrimitiveType.Quad, _decor, material, "Shadow Catcher");
            // A quad faces -Z; laid back a quarter turn it faces up, and its Y becomes the board's Z.
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, -TileHeight + ShadowCatcherLift, 0f);
            quad.transform.localScale = new Vector3(
                2f * ((float)BoardLayout.HalfWidth(Level.Width) + ShadowCatcherMargin),
                2f * ((float)BoardLayout.HalfDepth(Level.Height) + ShadowCatcherMargin), 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            _shadowCatcher = quad;
            quad.SetActive(_shadowCatcherVisible);
        }

        /// <summary>A primitive with its collider removed: only tiles are aimed at.</summary>
        private static GameObject Primitive(PrimitiveType type, Transform parent, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (go.TryGetComponent<Collider>(out var collider)) Kill(collider);
            return go;
        }

        private static void ClearChildren(Transform group)
        {
            if (group == null) return;
            for (var i = group.childCount - 1; i >= 0; i--) Kill(group.GetChild(i).gameObject);
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode, so the display can be built and inspected from an editor probe.</summary>
        private static void Kill(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh != null) Kill(mesh);
            mesh = null;
        }
    }

    /// <summary>
    /// One clue sign. Its face is filled in the line colour once the row or column holds exactly its clue, ringed in the
    /// closed grey until then, and ringed in red when the line is overfilled.
    /// </summary>
    internal readonly struct ClueSign
    {
        public readonly GameObject Root;
        public readonly GameObject Face;
        private readonly TextMesh _numeral;
        private readonly MeshRenderer _fill;
        private readonly MeshRenderer _ring;

        public ClueSign(GameObject root, GameObject face, TextMesh numeral, MeshRenderer fill, MeshRenderer ring)
        {
            Root = root;
            Face = face;
            _numeral = numeral;
            _fill = fill;
            _ring = ring;
        }

        public void Dress(bool satisfied, bool exceeded)
        {
            if (_fill == null || _ring == null || _numeral == null) return;
            _fill.sharedMaterial = satisfied ? XRBoardMaterials.ClueChip : XRBoardMaterials.ClueChipIdle;
            _ring.sharedMaterial = satisfied
                ? XRBoardMaterials.ClueChipEdge
                : exceeded ? XRBoardMaterials.ClueChipError : XRBoardMaterials.ClueChipIdleEdge;
            _numeral.color = exceeded && !satisfied ? XRPalette.ClueExceeded : XRPalette.Ink;
        }
    }
}
