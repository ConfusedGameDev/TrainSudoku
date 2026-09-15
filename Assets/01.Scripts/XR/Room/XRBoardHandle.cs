using System.Collections.Generic;
using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// What moves the board (XR-PRD 5.3, revised after the XR6 and XR7 headset checks): a rounded rail wrapped round the
    /// platform's near corner on the side away from the tray, with a leg along each edge. Touched, it lights and opens
    /// out along the near edge, round the far corner and up the far side, so there is an end of it for each hand; left
    /// alone for <see cref="StayOpenSeconds"/>, it folds back to its corner. It takes both hands to move the board; a
    /// single pinch, the false positive the corner knob kept giving, only lights and opens it. Held in both hands, their
    /// midpoint carries the board, height included, the line between them steers it, and spreading or closing them
    /// scales it, from 4 to 9 cm a cell; the board only follows once the hands have carried it 3 cm, turned it 12
    /// degrees or changed their span by 8%. Let go with either hand and <see cref="XRBoardPlacement"/> settles the board
    /// onto a surface it is close to and re-anchors it. A fingertip or pinch point within 2 cm of the rail counts as a
    /// touch, so the player sees where to take hold.
    /// </summary>
    /// <remarks>
    /// The rail follows the platform on show through <see cref="SetFootprint"/>: a 6x6 board's corner is a cell nearer
    /// the middle than an 8x8's, and the map card's is further out still. It is taken by a close pinch only, never a
    /// ray, a poke or the gaze (<see cref="XRDirectReach"/>).
    ///
    /// It is a simple interactable: XRI reports the grab and moves nothing. A grab interactable would detach the handle
    /// from the board for the length of the grab, which left it behind in the air while the board moved.
    /// </remarks>
    public sealed class XRBoardHandle : MonoBehaviour
    {
        /// <summary>How far outside the platform's edge the rail runs, and how high above the surface, in cells.</summary>
        private const float GapCells = 0.45f;
        private const float HeightCells = 0.25f;

        /// <summary>How far each leg runs along its edge from the corner, in cells (12 cm): room for a hand on each.</summary>
        private const float LegCells = 2f;

        private const float ThicknessCells = 0.35f;

        /// <summary>How much fatter than the rail its grab volume is, so a pinch need not be exact.</summary>
        private const float GrabPaddingCells = 0.25f;

        /// <summary>
        /// How close to the grab volume a fingertip or pinch point counts as touching the rail, in cells (2 cm), and how
        /// far it must then move away before it stops (5 cm), so the light does not flicker with the hand at its edge.
        /// </summary>
        private const float TouchCells = 0.35f;
        private const float LetGoCells = 0.8f;

        /// <summary>How far outside the grab volume a pinch is still the rail's rather than the corner cell's, in cells.</summary>
        private const float ClaimCells = 0.2f;

        /// <summary>How far the two hands must carry the rail, in metres, or turn it, in degrees, before the board follows.</summary>
        private const float DeadZoneMetres = 0.03f;
        private const float DeadZoneDegrees = 12f;

        /// <summary>How much the span between the hands must change, as a share, before the board scales.</summary>
        private const float DeadZoneGrowth = 0.08f;

        /// <summary>
        /// How quickly the scale follows the hands, as a time constant in seconds: tracking jitter in their span is
        /// magnified at the far edge of a big board, so the scale is eased a little where the position is not.
        /// </summary>
        private const float ScaleEasing = 0.08f;

        /// <summary>
        /// How long the rail takes to open out to the far corner and to fold back, in seconds, and how long it stays open
        /// once nothing is on or near it: long enough for the second hand to reach the far end.
        /// </summary>
        private const float OpenSeconds = 0.25f;
        private const float FoldSeconds = 0.35f;
        private const float StayOpenSeconds = 0.8f;

        /// <summary>A 6x6 platform's half-width, in cells, until the game says what is on show.</summary>
        private const float DefaultHalfWidth = 4f;

        private const int ArcSegments = 10;
        private const int RailSides = 12;
        private const int VolumeSides = 8;

        private readonly List<IXRSelectInteractor> _holders = new List<IXRSelectInteractor>(2);
        private readonly List<Vector3> _path = new List<Vector3>();
        private XRBoardPlacement _placement;
        private XRSimpleInteractable _interactable;
        private MeshRenderer _rail;
        private MeshCollider _volume;
        private Mesh _railMesh;
        private Mesh _volumeMesh;
        private Pose _startBoard;
        private Vector3 _startGrab;
        private Vector3 _startSpan;
        private float _startCell;
        private float _startSpread;
        private float _cell;
        private bool _available = true;
        private bool _moving;
        private bool _touched;
        private bool _lit;
        private float _halfWidth = DefaultHalfWidth;

        /// <summary>0 folded round its corner, 1 open to the far one; and how long it has been left alone.</summary>
        private float _open;
        private float _idle = StayOpenSeconds;

        // Shared by every tube built: the rail is rebuilt each frame while it opens or folds.
        private static readonly List<Vector3> TubeVertices = new List<Vector3>();
        private static readonly List<Vector3> TubeNormals = new List<Vector3>();
        private static readonly List<int> TubeTriangles = new List<int>();

        /// <summary>-1 for the left-hand corner, +1 for the right.</summary>
        private float _side = -1f;

        public bool IsHeld => _holders.Count > 0;

        /// <summary>The player's dominant hand: the rail wraps the near corner on the other side, away from the tray.</summary>
        public Hand DominantHand
        {
            set
            {
                var side = value == Hand.Right ? -1f : 1f;
                if (Mathf.Approximately(side, _side)) return;
                _side = side;
                Rebuild();
            }
        }

        /// <summary>Wraps the rail round the corner of a platform <paramref name="halfWidth"/> cells either side of the middle of its near edge.</summary>
        public void SetFootprint(float halfWidth)
        {
            if (Mathf.Approximately(halfWidth, _halfWidth)) return;
            _halfWidth = halfWidth;
            Rebuild();
        }

        public static XRBoardHandle Create(Transform boardRoot, XRBoardPlacement placement)
        {
            var go = new GameObject("Board Handle");
            go.transform.SetParent(boardRoot, false);
            go.transform.localPosition = new Vector3(0f, HeightCells, 0f);

            var rail = new GameObject("Rail");
            rail.transform.SetParent(go.transform, false);

            var handle = go.AddComponent<XRBoardHandle>();
            handle._placement = placement;
            handle._railMesh = new Mesh { name = "Handle Rail" };
            handle._volumeMesh = new Mesh { name = "Handle Grab Volume" };
            rail.AddComponent<MeshFilter>().sharedMesh = handle._railMesh;
            handle._rail = rail.AddComponent<MeshRenderer>();
            handle._rail.sharedMaterial = XRBoardMaterials.SignPole;

            // The grab volume is a fatter copy of the rail. It comes first: the interactable collects its colliders when it wakes.
            handle._volume = go.AddComponent<MeshCollider>();
            handle.Rebuild();
            XRDirectReach.Apply(go);

            var interactable = go.AddComponent<XRGrabOnlyInteractable>();
            interactable.selectMode = InteractableSelectMode.Multiple;
            interactable.selectEntered.AddListener(handle.OnGrabbed);
            interactable.selectExited.AddListener(handle.OnReleased);
            handle._interactable = interactable;
            return handle;
        }

        /// <summary>
        /// Shows or hides the handle. It hides while any piece is held, so a hand cannot catch it by accident mid-drop
        /// (5.3); a handle already in a hand stays until it is let go.
        /// </summary>
        public void SetAvailable(bool available)
        {
            if (available == _available || (!available && IsHeld)) return;
            _available = available;
            _interactable.enabled = available;
            _rail.enabled = available;
            if (available) return;
            _touched = false;
            // Back to its corner, so it comes back where it rests rather than across the near edge.
            _open = 0f;
            _idle = StayOpenSeconds;
            Rebuild();
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_holders.Count >= 2 || _holders.Contains(args.interactorObject)) return;
            _holders.Add(args.interactorObject);
            // The second hand on: from here the pair carries the board.
            if (_holders.Count == 2) Rebase();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!_holders.Remove(args.interactorObject)) return;
            // Either hand letting go ends the move. A pair that never left the dead zone moved nothing to re-anchor.
            if (_moving) _placement.EndMove();
            _moving = false;
        }

        /// <summary>Takes the board's pose and the two hands' as they are now as the start of the move.</summary>
        private void Rebase()
        {
            var root = _placement.BoardRoot;
            _startBoard = new Pose(root.position, root.rotation);
            var a = GrabPoint(_holders[0]);
            var b = GrabPoint(_holders[1]);
            _startGrab = (a + b) / 2f;
            _startSpan = Vector3.ProjectOnPlane(b - a, Vector3.up);
            _startCell = _placement.CellSize;
            _cell = _startCell;
            _startSpread = Mathf.Max(0.02f, (b - a).magnitude);
        }

        private void Update()
        {
            _touched = _available && Touching(_touched ? LetGoCells : TouchCells);
            var lit = IsHeld || _touched || (_interactable != null && _interactable.isHovered);
            if (lit != _lit) Light(lit);
            Unfold(lit);

            // One hand only lights it: the board moves in both.
            if (_holders.Count < 2) return;

            // Their midpoint carries the board, and the line between them steers it.
            var a = GrabPoint(_holders[0]);
            var b = GrabPoint(_holders[1]);
            var grab = (a + b) / 2f;
            var span = Vector3.ProjectOnPlane(b - a, Vector3.up);
            var yaw = _startSpan.sqrMagnitude > 1e-8f && span.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(_startSpan, span, Vector3.up) : 0f;
            // Spreading or closing the hands scales the board (X6, revised 2026-09-14).
            var target = Mathf.Clamp(_startCell * (b - a).magnitude / _startSpread, XRBoardPlacement.MinCellSize, XRBoardPlacement.MaxCellSize);

            if (!_moving)
            {
                if ((grab - _startGrab).magnitude < DeadZoneMetres && Mathf.Abs(yaw) < DeadZoneDegrees &&
                    Mathf.Abs(target / _startCell - 1f) < DeadZoneGrowth) return;
                // Out of the dead zone: the board catches up with the hands and follows them from here.
                _moving = true;
                _placement.BeginMove();
            }

            _cell = Mathf.Lerp(_cell, target, 1f - Mathf.Exp(-Time.deltaTime / ScaleEasing));
            var grow = _cell / _startCell;

            // Turn and scale about the grab, not the board's origin, so the rail stays where the hands are.
            var turn = Quaternion.Euler(0f, yaw, 0f);
            _placement.MoveTo(new Pose(grab + turn * ((_startBoard.position - _startGrab) * grow), turn * _startBoard.rotation), _cell);
        }

        private Vector3 GrabPoint(IXRSelectInteractor holder) => holder.GetAttachTransform(_interactable).position;

        // ------------------------------------------------------------------ the rail

        /// <summary>
        /// Opens the rail out while it is lit, and folds it back once it has been left alone for
        /// <see cref="StayOpenSeconds"/>. A hidden rail stays folded.
        /// </summary>
        private void Unfold(bool lit)
        {
            _idle = lit ? 0f : _idle + Time.deltaTime;
            var target = _available && _idle < StayOpenSeconds ? 1f : 0f;
            if (Mathf.Approximately(_open, target)) return;
            _open = Mathf.MoveTowards(_open, target, Time.deltaTime / (target > _open ? OpenSeconds : FoldSeconds));
            Rebuild();
        }

        /// <summary>The rail's centre line for how open it is, then the rail and its grab volume rebuilt along it.</summary>
        private void Rebuild()
        {
            if (_railMesh == null) return;
            TracePath(_open * _open * (3f - 2f * _open));

            Tube(_railMesh, _path, ThicknessCells / 2f, RailSides);
            Tube(_volumeMesh, _path, ThicknessCells / 2f + GrabPaddingCells, VolumeSides);
            // Reassigned so the collider takes the new shape.
            _volume.sharedMesh = null;
            _volume.sharedMesh = _volumeMesh;
        }

        /// <summary>
        /// The rail's centre line, in the handle's space, <paramref name="open"/> from 0 to 1. Folded, it is the L round
        /// the near corner away from the tray: a leg up the side, a quarter circle round the corner at
        /// <see cref="GapCells"/> from the platform, and <see cref="LegCells"/> along the near edge. Opening runs that
        /// edge leg on to the far corner, round it and up the far side, so the open rail is a U with an end for each hand.
        /// </summary>
        private void TracePath(float open)
        {
            _path.Clear();
            var near = _side * _halfWidth;
            var far = -near;

            // The whole U, from the resting corner's leg round to the far corner's.
            _path.Add(new Vector3(near + _side * GapCells, 0f, LegCells));
            for (var i = 0; i <= ArcSegments; i++) _path.Add(Corner(near, _side, Mathf.PI / 2f * (ArcSegments - i) / ArcSegments));
            var folded = PathLength(_path) + LegCells;
            for (var i = 0; i <= ArcSegments; i++) _path.Add(Corner(far, -_side, Mathf.PI / 2f * i / ArcSegments));
            _path.Add(new Vector3(far - _side * GapCells, 0f, LegCells));

            // Then cut to the length on show.
            var full = PathLength(_path);
            Truncate(_path, Mathf.Lerp(Mathf.Min(folded, full), full, open));
        }

        /// <summary>
        /// A point on the quarter circle round the platform corner at <paramref name="x"/>: <paramref name="angle"/> 0 is
        /// on the near edge's side, a right angle on the side edge's, which lies towards <paramref name="outward"/>.
        /// </summary>
        private static Vector3 Corner(float x, float outward, float angle) =>
            new Vector3(x + outward * Mathf.Sin(angle) * GapCells, 0f, -Mathf.Cos(angle) * GapCells);

        private static float PathLength(List<Vector3> path)
        {
            var length = 0f;
            for (var i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        /// <summary>Cuts <paramref name="path"/> off <paramref name="length"/> along it.</summary>
        private static void Truncate(List<Vector3> path, float length)
        {
            var travelled = 0f;
            for (var i = 1; i < path.Count; i++)
            {
                var step = Vector3.Distance(path[i - 1], path[i]);
                if (travelled + step < length)
                {
                    travelled += step;
                    continue;
                }

                var end = Vector3.Lerp(path[i - 1], path[i], step > 1e-6f ? (length - travelled) / step : 0f);
                path.RemoveRange(i, path.Count - i);
                // A stub too short to have a direction would leave the end cap facing nowhere.
                if (Vector3.Distance(path[i - 1], end) > 1e-3f || path.Count < 2) path.Add(end);
                return;
            }
        }

        /// <summary>
        /// A round tube of <paramref name="radius"/> along a level <paramref name="path"/>, capped at both ends. Triangles
        /// wind so that cross(p1 - p0, p2 - p0) points out of the tube, as the built-in primitives do.
        /// </summary>
        private static void Tube(Mesh mesh, List<Vector3> path, float radius, int sides)
        {
            var vertices = TubeVertices;
            var normals = TubeNormals;
            var triangles = TubeTriangles;
            vertices.Clear();
            normals.Clear();
            triangles.Clear();
            var ring = sides + 1;
            for (var i = 0; i < path.Count; i++)
            {
                var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                var across = Vector3.Cross(tangent, Vector3.up);
                for (var j = 0; j <= sides; j++)
                {
                    var angle = 2f * Mathf.PI * j / sides;
                    var normal = Mathf.Cos(angle) * Vector3.up + Mathf.Sin(angle) * across;
                    vertices.Add(path[i] + normal * radius);
                    normals.Add(normal);
                }
            }

            for (var i = 0; i < path.Count - 1; i++)
            for (var j = 0; j < sides; j++)
            {
                var a = i * ring + j;
                var c = a + ring;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(c);
                triangles.Add(a + 1);
                triangles.Add(c + 1);
                triangles.Add(c);
            }

            Cap(vertices, normals, triangles, path[0], (path[1] - path[0]).normalized, radius, sides, false);
            Cap(vertices, normals, triangles, path[path.Count - 1], (path[path.Count - 1] - path[path.Count - 2]).normalized, radius, sides, true);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>A flat disc closing the tube, facing along <paramref name="tangent"/> at the end and against it at the start.</summary>
        private static void Cap(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 centre, Vector3 tangent,
            float radius, int sides, bool end)
        {
            var across = Vector3.Cross(tangent, Vector3.up);
            var normal = end ? tangent : -tangent;
            var first = vertices.Count;
            vertices.Add(centre);
            normals.Add(normal);
            for (var j = 0; j <= sides; j++)
            {
                var angle = 2f * Mathf.PI * j / sides;
                vertices.Add(centre + (Mathf.Cos(angle) * Vector3.up + Mathf.Sin(angle) * across) * radius);
                normals.Add(normal);
            }

            for (var j = 0; j < sides; j++)
            {
                triangles.Add(first);
                triangles.Add(end ? first + 1 + j : first + 2 + j);
                triangles.Add(end ? first + 2 + j : first + 1 + j);
            }
        }

        // ------------------------------------------------------------------ touch and light

        /// <summary>A fingertip or pinch point within <paramref name="margin"/> cells of the rail's grab volume.</summary>
        private bool Touching(float margin)
        {
            var reach = ThicknessCells / 2f + GrabPaddingCells + margin;
            foreach (var tip in XRTouchPoints.Fingertips)
                if (NearRail(tip.Position, reach))
                    return true;
            foreach (var pinch in XRTouchPoints.PinchPoints)
                if (NearRail(pinch, reach))
                    return true;
            return false;
        }

        /// <summary>
        /// Whether a pinch at <paramref name="world"/> is on the rail: inside its grab volume or within
        /// <see cref="ClaimCells"/> of it. No board piece is taken from there (<see cref="XRIGrabInput.Reserved"/>): the rail
        /// wraps the corner cell, and a pinch on the rail kept lifting that cell's piece instead (the second XR8 check).
        /// </summary>
        public bool Claims(Vector3 world) => _available && NearRail(world, ThicknessCells / 2f + GrabPaddingCells + ClaimCells);

        private bool NearRail(Vector3 world, float reach)
        {
            var point = transform.InverseTransformPoint(world);
            for (var i = 0; i < _path.Count - 1; i++)
            {
                var from = _path[i];
                var along = _path[i + 1] - from;
                var t = along.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - from, along) / along.sqrMagnitude) : 0f;
                if ((from + along * t - point).sqrMagnitude <= reach * reach) return true;
            }

            return false;
        }

        /// <summary>The platform edge's yellow while touched, hovered or held; the sign poles' steel at rest.</summary>
        private void Light(bool on)
        {
            _lit = on;
            if (_rail != null) _rail.sharedMaterial = on ? XRBoardMaterials.PlatformEdge : XRBoardMaterials.SignPole;
        }

        private void OnDestroy()
        {
            if (_railMesh != null) Destroy(_railMesh);
            if (_volumeMesh != null) Destroy(_volumeMesh);
        }
    }
}
