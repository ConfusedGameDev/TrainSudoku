using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The stand-in for the XR shell until the flow arrives (XR7): once the board is placed, it plays the shipped
    /// stations through on their own — show the level, lay its solution a piece at a time along the route, run the
    /// train, next station — so the board can be checked on a headset before anything lays track by hand (XR6).
    /// </summary>
    public sealed class XRBoardDemo : MonoBehaviour
    {
        [SerializeField] private NetworkDefinition network;
        [SerializeField] private XRBoardAssets assets;

        [Tooltip("Hangs the board in the room. Without one the demo floats the board in front of the head itself.")]
        [SerializeField] private XRBoardPlacement placement;

        [Tooltip("Flat station index the demo starts from.")]
        [SerializeField] private int startStation;

        [Tooltip("Alternate board sizes (6x6, 7x7, 8x8, 6x6, ...) so the board is seen to grow from its near edge.")]
        [SerializeField] private bool sizeTour = true;

        [Tooltip("World size of one cell, in metres, when floating without a placement (XR-PRD X6).")]
        [SerializeField] private float cellSize = 0.06f;

        [SerializeField] private float secondsPerPiece = 0.3f;
        [SerializeField] private float secondsBetweenStations = 2f;

        private readonly List<(LevelDefinition Level, LineDefinition Line)> _stations = new List<(LevelDefinition Level, LineDefinition Line)>();
        private XRBoardDisplay _display;

        private IEnumerator Start()
        {
            if (network == null)
            {
                Debug.LogError("[XR demo] No network assigned.", this);
                yield break;
            }

            foreach (var line in network.Lines)
            {
                if (line == null) continue;
                for (var i = 0; i < line.StationCount; i++)
                    if (line.Station(i) is LevelDefinition station) _stations.Add((station, line));
            }

            if (_stations.Count == 0) yield break;
            var order = sizeTour ? SizeTour(_stations) : _stations;

            Transform parent;
            if (placement != null)
            {
                while (!placement.IsPlaced) yield return null;
                parent = placement.BoardRoot;
            }
            else
            {
                yield return new WaitForSeconds(1f);
                FloatInFrontOfHead();
                parent = transform;
            }

            _display = XRBoardDisplay.Create(parent, assets);
            if (placement != null)
            {
                // Shadows on the real table only when there is a table under the board.
                _display.ShowShadowCatcher(placement.IsOnSurface);
                placement.Placed += () => _display.ShowShadowCatcher(placement.IsOnSurface);
                placement.SurfaceChanged += () => _display.ShowShadowCatcher(placement.IsOnSurface);
            }
            var index = Mathf.Clamp(startStation, 0, order.Count - 1);
            while (true)
            {
                yield return PlayStation(order[index]);
                index = (index + 1) % order.Count;
            }
        }

        /// <summary>Stations of each size in network order, taken in turn: the first 6x6, the first 7x7, the first 8x8, the second 6x6...</summary>
        private static List<(LevelDefinition Level, LineDefinition Line)> SizeTour(List<(LevelDefinition Level, LineDefinition Line)> stations)
        {
            var bySize = stations.GroupBy(s => s.Level.Width * 100 + s.Level.Height).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            var tour = new List<(LevelDefinition Level, LineDefinition Line)>();
            for (var i = 0; tour.Count < stations.Count; i++)
                foreach (var size in bySize)
                    if (i < size.Count) tour.Add(size[i]);
            return tour;
        }

        /// <summary>Without a placement: the board's near edge 0.36 m ahead of the head and 0.5 m below it, facing the gaze.</summary>
        private void FloatInFrontOfHead()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var forward = Vector3.forward;
            var position = new Vector3(0f, 1f, 0.36f);
            if (head != null)
            {
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f) forward = flat.normalized;
                position = head.position + forward * 0.36f + Vector3.down * 0.5f;
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = Vector3.one * cellSize;
        }

        private IEnumerator PlayStation((LevelDefinition Level, LineDefinition Line) station)
        {
            var level = station.Level.ToLevelData();
            _display.Load(level, station.Line != null ? station.Line.Color : XRPalette.Warn);
            Debug.Log($"[XR demo] {station.Level.DisplayName} ({level.Width}x{level.Height})");

            // Some boards take a solver a second or two; off the main thread, the headset keeps its frame rate.
            var shownAt = Time.time;
            var solve = Task.Run(() => Solver.TrySolve(level, out var solved) ? solved : null);
            while (!solve.IsCompleted) yield return null;
            yield return new WaitForSeconds(Mathf.Max(0f, 1f - (Time.time - shownAt)));

            var solution = solve.IsFaulted ? null : solve.Result;
            if (solution == null)
            {
                Debug.LogWarning($"[XR demo] Could not solve {station.Level.DisplayName}; skipping it.");
                yield return new WaitForSeconds(secondsBetweenStations);
                yield break;
            }

            foreach (var (x, y) in LayingOrder(solution))
            {
                if (!(solution[x, y] is Piece piece) || piece.IsFixed || _display.Board[x, y].HasValue) continue;
                if (!_display.Board.TryPlace(x, y, piece.Key))
                    _display.Board.SetUnchecked(x, y, new Piece(piece.Key, false));
                _display.Sync(true);
                yield return new WaitForSeconds(secondsPerPiece);
            }

            var finished = false;
            _display.RunTrain(station.Level.DisplayName, () => finished = true);
            while (!finished) yield return null;
            yield return new WaitForSeconds(secondsBetweenStations);
        }

        /// <summary>The solved route from S to E, then any cell it does not pass through.</summary>
        private static IEnumerable<(int X, int Y)> LayingOrder(Board solution)
        {
            var seen = new HashSet<(int X, int Y)>();
            var route = WinChecker.Evaluate(solution).Path;
            if (route != null)
                foreach (var cell in route)
                    if (seen.Add(cell)) yield return cell;
            for (var y = 0; y < solution.Height; y++)
            for (var x = 0; x < solution.Width; x++)
                if (seen.Add((x, y))) yield return (x, y);
        }
    }
}
