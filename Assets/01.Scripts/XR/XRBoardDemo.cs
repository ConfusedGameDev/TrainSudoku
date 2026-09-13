using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// XR4's stand-in for the XR shell: puts a board in front of the player and plays the shipped stations through on
    /// their own — show the level, lay its solution a piece at a time along the route, run the train, next station — so
    /// the board display can be checked on a headset before anything lays track by hand (XR6). The XR flow replaces it
    /// at XR7.
    /// </summary>
    public sealed class XRBoardDemo : MonoBehaviour
    {
        [SerializeField] private NetworkDefinition network;
        [SerializeField] private XRBoardAssets assets;

        [Tooltip("Flat station index (lines in order) the demo starts from.")]
        [SerializeField] private int startStation;

        [Tooltip("World size of one cell, in metres (XR-PRD X6).")]
        [SerializeField] private float cellSize = 0.06f;

        [Tooltip("Where the board centre goes at start: this far ahead of the head and this far below it, in metres.")]
        [SerializeField] private float distanceAhead = 0.6f;
        [SerializeField] private float dropBelowEyes = 0.5f;

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

            // Let head tracking settle before reading where the player is looking.
            yield return new WaitForSeconds(1f);
            PlaceInFrontOfHead();
            _display = XRBoardDisplay.Create(transform, assets);

            var index = Mathf.Clamp(startStation, 0, _stations.Count - 1);
            while (true)
            {
                yield return PlayStation(_stations[index]);
                index = (index + 1) % _stations.Count;
            }
        }

        /// <summary>Faces the board away from the player along their gaze, flattened, so its south edge is the near one.</summary>
        private void PlaceInFrontOfHead()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var forward = Vector3.forward;
            var position = new Vector3(0f, 1f, distanceAhead);
            if (head != null)
            {
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f) forward = flat.normalized;
                position = head.position + forward * distanceAhead + Vector3.down * dropBelowEyes;
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
