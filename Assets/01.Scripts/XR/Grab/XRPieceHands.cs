using System;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Pieces in the player's hands (XR-PRD 4.2 to 4.4). It turns what the <see cref="IGrabInput"/> reports into
    /// <see cref="PieceDrop"/> calls and shows what each one did: the piece in the hand, the ghost over the cell beneath
    /// it, the flight back to where an illegal drop came from, and a piece let go or thrown off the platform falling into
    /// the room before its puff or burst of steam. While a level is in play nothing else changes the board.
    /// </summary>
    /// <remarks>
    /// The ghost is the whole contract of a release: a piece lands on the cell its ghost is over, and with no ghost
    /// showing — above the hover band, or off the grid — letting go is letting go off the platform.
    /// </remarks>
    public sealed class XRPieceHands : MonoBehaviour
    {
        /// <summary>How far above the platform a piece held at a distance rides, in cells, so it reads as held over its ghost.</summary>
        private const float DistantLift = 0.4f;

        /// <summary>How far below the pinch a piece held in the hand hangs, in metres, so the fingers do not hide it.</summary>
        private const float HoldDrop = 0.012f;

        /// <summary>A distant hold rides the platform only this far along the ray, in metres; beyond, it stays on the ray.</summary>
        private const float MaxRideDistance = 5f;

        private const float ReturnSeconds = 0.25f;
        private const float ReturnArc = 0.05f;

        /// <summary>How long a piece let go off the platform tumbles about the room before it puffs, in seconds.</summary>
        private const float FallSeconds = 3f;

        /// <summary>The ghost's translucent slab over its cell, and how far it floats clear of the real slab's top, in cells.</summary>
        private const float GhostSlabThickness = 0.02f;
        private const float GhostSlabLift = 0.005f;

        private static readonly Hand[] Hands = { Hand.Left, Hand.Right };

        private readonly Held[] _held = { new Held(), new Held() };
        private IGrabInput _input;
        private XRSteam _steam;
        private XRBoardDisplay _display;
        private XRTray _tray;
        private PieceDrop _drop;

        /// <summary>How high above the platform a held piece still shows its ghost and lands on release, in metres (4.3).</summary>
        public float HoverBand { get; set; } = 0.10f;

        /// <summary>Hand speed above which letting go is a throw, in m/s (4.4).</summary>
        public float ThrowThreshold { get; set; } = (float)ThrowGesture.DefaultThreshold;

        public bool IsHolding => _drop != null && (_drop.IsHolding(Hand.Left) || _drop.IsHolding(Hand.Right));

        /// <summary>Every grab and release, as <see cref="PieceDrop"/> answered it: the clock, the coach and the log read these.</summary>
        public event Action<DropResult> Acted;

        /// <summary>The board changed and the display has caught up: time to check for the win.</summary>
        public event Action BoardChanged;

        private sealed class Held
        {
            public GameObject Piece;
            public GameObject Ghost;
            public MeshRenderer GhostTrack;
            public MeshRenderer GhostSlab;
            public readonly ThrowGesture Gesture = new ThrowGesture();

            /// <summary>The cell the ghost is over: where a release lands. Null when no ghost shows.</summary>
            public (int X, int Y)? Cell;
        }

        public static XRPieceHands Create(Transform parent, IGrabInput input, XRSteam steam)
        {
            var go = new GameObject("Piece Hands");
            go.transform.SetParent(parent, false);
            var hands = go.AddComponent<XRPieceHands>();
            hands._input = input;
            hands._steam = steam;
            input.Grabbed += hands.OnGrabbed;
            input.Released += hands.OnReleased;
            input.HoverChanged += hands.OnHoverChanged;
            return hands;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.Grabbed -= OnGrabbed;
                _input.Released -= OnReleased;
                _input.HoverChanged -= OnHoverChanged;
            }

            End();
        }

        /// <summary>
        /// Takes over the board on show: every cell becomes a target a hand can lift a piece from, by a close pinch only.
        /// Pieces sit a cell apart, and a ray aimed at one kept landing on a neighbour (4.2, revised after the XR6 check).
        /// </summary>
        public void Begin(XRBoardDisplay display, XRTray tray)
        {
            End();
            _display = display;
            _tray = tray;
            _drop = new PieceDrop(display.Board);
            for (var y = 0; y < display.Level.Height; y++)
            for (var x = 0; x < display.Level.Width; x++)
            {
                int cellX = x, cellY = y;
                var board = display.Board;
                _input.AddTarget(display.CellVolume(x, y), GrabTarget.Cell(x, y),
                    () => _drop != null && _drop.Board == board && board[cellX, cellY].HasValue, directOnly: true);
            }
        }

        /// <summary>Lets go of the board: a piece still in a hand puffs, and grabs are ignored until the next <see cref="Begin"/>.</summary>
        public void End()
        {
            foreach (var held in _held)
            {
                if (held.Piece != null && _steam != null) _steam.Puff(held.Piece.transform.position);
                Discard(held);
            }

            _drop = null;
            if (_tray != null) _tray.Holding = false;
        }

        private void OnGrabbed(Hand hand, GrabTarget target)
        {
            if (_drop == null) return;
            var result = target.IsTray ? _drop.GrabFromTray(hand, target.Key) : _drop.GrabFromCell(hand, target.X, target.Y);
            switch (result.Outcome)
            {
                case DropOutcome.Taken:
                case DropOutcome.Lifted:
                    Hold(hand, result.Key, target);
                    if (result.BoardChanged)
                    {
                        _display.HighlightCell(target.X, target.Y, false);
                        _display.Sync(true);
                        BoardChanged?.Invoke();
                    }
                    break;
                case DropOutcome.Refused when result.RefuseReason == RefuseReason.FixedPiece:
                    _display.ShakePiece(target.X, target.Y);
                    break;
            }

            Acted?.Invoke(result);
        }

        private void OnReleased(Hand hand)
        {
            if (_drop == null || !_drop.IsHolding(hand)) return;
            var held = _held[(int)hand];
            var thrown = held.Gesture.IsThrow(ThrowThreshold);
            var (vx, vy, vz) = held.Gesture.Velocity;
            var result = _drop.Release(hand, held.Cell, thrown);

            var piece = held.Piece;
            held.Piece = null;
            Discard(held);
            if (result.BoardChanged) _display.Sync(true);
            if (piece != null) Play(result, piece, new Vector3((float)vx, (float)vy, (float)vz));
            if (result.BoardChanged) BoardChanged?.Invoke();
            Acted?.Invoke(result);
        }

        /// <summary>What happens to the piece that left the hand. The board has already changed; this is the show.</summary>
        private void Play(DropResult result, GameObject piece, Vector3 velocity)
        {
            switch (result.Outcome)
            {
                case DropOutcome.Placed:
                case DropOutcome.Moved:
                    // The display scales the landed piece in on its cell.
                    Destroy(piece);
                    break;
                case DropOutcome.Replaced:
                    Destroy(piece);
                    if (result.Cell is { } cell && _steam != null) _steam.Puff(_display.CellWorldPosition(cell.X, cell.Y));
                    break;
                case DropOutcome.Returned when result.Origin.HasValue:
                    // Back into the cell it was lifted from: the display already shows it there, so hide that until it lands.
                    var origin = result.Origin.Value;
                    var board = _display.Board;
                    _display.SetPieceVisible(origin.X, origin.Y, false);
                    XRPieceFlight.Arc(piece, _display.CellWorldPosition(origin.X, origin.Y), ReturnSeconds, ReturnArc, () =>
                    {
                        if (_display != null && _display.Board == board) _display.SetPieceVisible(origin.X, origin.Y, true);
                    });
                    break;
                case DropOutcome.Returned:
                    if (_tray != null) XRPieceFlight.Arc(piece, _tray.SlotWorldPosition(result.Key), ReturnSeconds, ReturnArc, null);
                    else Destroy(piece);
                    break;
                case DropOutcome.Thrown:
                    Fall(piece, velocity, true);
                    break;
                default:
                    // Let go off the platform, or an illegal drop with no way back.
                    Fall(piece, velocity, false);
                    break;
            }
        }

        /// <summary>
        /// Into the room as a real body, then a puff where it ends up, a few seconds later or the moment it touches the
        /// board (X19, revised after the XR7 headset check). A throw ends in the bigger burst.
        /// </summary>
        private void Fall(GameObject piece, Vector3 velocity, bool thrown)
        {
            var steam = _steam;
            var board = _display == null ? null : _display.transform.parent != null ? _display.transform.parent : _display.transform;
            XRPieceFlight.Fall(piece, velocity, FallSeconds, board, at =>
            {
                if (steam == null) return;
                if (thrown) steam.Burst(at);
                else steam.Puff(at);
            });
        }

        private void Update()
        {
            if (_drop == null || _display == null || _display.Level == null) return;
            foreach (var hand in Hands)
            {
                var held = _held[(int)hand];
                if (held.Piece == null || !_drop.IsHolding(hand)) continue;
                if (_input.TryGetHold(hand, out var hold))
                {
                    Follow(held, hold);
                    held.Gesture.Add(Time.unscaledTimeAsDouble, hold.Hand.x, hold.Hand.y, hold.Hand.z);
                }

                ShowGhost(held, _drop.Hover(hand, held.Cell));
            }

            if (_tray != null) _tray.Holding = IsHolding;
        }

        /// <summary>
        /// Carries the piece with the hand, its yaw locked to the board's grid (4.1): at the pinch for a direct hold, or
        /// riding the platform under the ray for a distant one (X16). Then finds the cell under its centre.
        /// </summary>
        private void Follow(Held held, GrabHold hold)
        {
            var board = _display.transform;
            var scale = board.lossyScale.y;
            Vector3 position;
            if (!hold.Distant)
                position = hold.Hand - board.up * HoldDrop;
            else if (new Plane(board.up, board.position).Raycast(hold.Ray, out var along) && along < MaxRideDistance)
                position = hold.Ray.GetPoint(along) + board.up * (DistantLift * scale);
            else
                position = hold.Point;

            held.Piece.transform.SetPositionAndRotation(position, board.rotation);
            held.Piece.transform.localScale = board.lossyScale;

            var local = board.InverseTransformPoint(position);
            var level = _display.Level;
            held.Cell = BoardPick.HoverCell(local.x, local.y, local.z, level.Width, level.Height, HoverBand / scale);
        }

        private void ShowGhost(Held held, GhostTint tint)
        {
            if (held.Ghost == null) return;
            var show = tint != GhostTint.None && held.Cell.HasValue;
            if (held.Ghost.activeSelf != show) held.Ghost.SetActive(show);
            if (!show) return;

            var (x, y) = held.Cell.Value;
            var (wx, wz) = BoardLayout.CellCenter(x, y, _display.Level.Width, _display.Level.Height);
            held.Ghost.transform.localPosition = new Vector3((float)wx, 0f, (float)wz);
            var material = tint == GhostTint.Legal ? XRBoardMaterials.GhostLegal : XRBoardMaterials.GhostIllegal;
            held.GhostTrack.sharedMaterial = material;
            held.GhostSlab.sharedMaterial = material;
        }

        private void Hold(Hand hand, PieceKey key, GrabTarget target)
        {
            var held = _held[(int)hand];
            Discard(held);

            var board = _display.transform;
            held.Piece = new GameObject($"Held {key} ({hand})");
            held.Piece.AddComponent<MeshFilter>().sharedMesh = _display.TrackMeshFor(key);
            held.Piece.AddComponent<MeshRenderer>().sharedMaterial = _display.PlayerTrackMaterial;
            var start = target.IsTray && _tray != null ? _tray.SlotWorldPosition(key) : _display.CellWorldPosition(target.X, target.Y);
            held.Piece.transform.SetPositionAndRotation(start, board.rotation);
            held.Piece.transform.localScale = board.lossyScale;

            held.Ghost = new GameObject($"Ghost {key} ({hand})");
            held.Ghost.transform.SetParent(board, false);
            held.Ghost.SetActive(false);
            held.GhostTrack = GhostPart(held.Ghost.transform, "Track", _display.TrackMeshFor(key),
                new Vector3(0f, _display.PieceLift + GhostSlabLift, 0f), Vector3.one);
            var size = (float)BoardLayout.CellSize - XRBoardDisplay.TileInset;
            held.GhostSlab = GhostPart(held.Ghost.transform, "Slab", _display.TileMesh,
                new Vector3(0f, GhostSlabLift + GhostSlabThickness / 2f, 0f), new Vector3(size, GhostSlabThickness, size));

            held.Gesture.Clear();
            held.Cell = null;
        }

        private static MeshRenderer GhostPart(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static void Discard(Held held)
        {
            if (held.Piece != null) Destroy(held.Piece);
            if (held.Ghost != null) Destroy(held.Ghost);
            held.Piece = null;
            held.Ghost = null;
            held.GhostTrack = null;
            held.GhostSlab = null;
            held.Cell = null;
            held.Gesture.Clear();
        }

        private void OnHoverChanged(GrabTarget target, bool hovered)
        {
            if (target.IsTray || _display == null || _display.Board == null) return;
            _display.HighlightCell(target.X, target.Y, hovered && _display.Board[target.X, target.Y].HasValue);
        }
    }
}
