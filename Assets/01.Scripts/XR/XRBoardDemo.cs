using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TrainSudoku.Core;
using TrainSudoku.Game;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The stand-in for the XR shell until the flow arrives (XR7). Once the board is placed it plays the shipped
    /// stations in turn: the level with the tray beside it, laid by the player's hands (XR6), then the train, then the
    /// next station. The clock starts on the first grab (4.2), and every solve is logged and appended to
    /// <c>xr-star-sample.csv</c> against the level's star times — the sample XR-PRD section 9 asks XR6 for. With
    /// <see cref="autoPlay"/> it lays each solution along the route itself, as the XR4 demo did.
    /// </summary>
    public sealed class XRBoardDemo : MonoBehaviour
    {
        private const string SampleFile = "xr-star-sample.csv";

        /// <summary>The placeholder station clock: how high above the far edge it floats and how tall its text is, in cells.</summary>
        private const float ClockHeight = 2.1f;
        private const float ClockBeyondEdge = 0.4f;
        private const float ClockTextHeight = 0.42f;

        [SerializeField] private NetworkDefinition network;
        [SerializeField] private XRBoardAssets assets;

        [Tooltip("Hangs the board in the room. Without one the demo floats the board in front of the head itself.")]
        [SerializeField] private XRBoardPlacement placement;

        [Tooltip("The hands: the grab interface on XRI (XR-PRD 10.4). Without one the demo plays the stations itself.")]
        [SerializeField] private XRIGrabInput grabInput;

        [Tooltip("Flat station index the demo starts from.")]
        [SerializeField] private int startStation;

        [Tooltip("Alternate board sizes (6x6, 7x7, 8x8, 6x6, ...) so the board is seen to grow from its near edge.")]
        [SerializeField] private bool sizeTour;

        [Tooltip("Lay each solution along the route itself, as at XR4, instead of waiting for the player's hands.")]
        [SerializeField] private bool autoPlay;

        [Header("Hands (XR-PRD 4)")]
        [Tooltip("The tray docks on this side of the edge the player stands at (4.1). Becomes a setting at XR8.")]
        [SerializeField] private Hand dominantHand = Hand.Right;

        [Tooltip("How high above the platform a held piece shows its ghost and lands on release, in metres (4.3).")]
        [SerializeField] private float hoverBand = 0.10f;

        [Tooltip("Hand speed above which letting go is a throw, in m/s (4.4).")]
        [SerializeField] private float throwThreshold = 1.2f;

        [Tooltip("World size of one cell, in metres, when floating without a placement (XR-PRD X6).")]
        [SerializeField] private float cellSize = 0.06f;

        [SerializeField] private float secondsPerPiece = 0.3f;
        [SerializeField] private float secondsBetweenStations = 2f;

        private readonly List<(LevelDefinition Level, LineDefinition Line)> _stations = new List<(LevelDefinition Level, LineDefinition Line)>();
        private readonly PlayTimer _timer = new PlayTimer();
        private XRBoardDisplay _display;
        private XRTray _tray;
        private XRPieceHands _hands;
        private TextMesh _clock;
        private LevelDefinition _current;
        private bool _inPlay;
        private bool _solved;
        private int _grabs;
        private int _landings;

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

            if (!autoPlay && grabInput != null)
            {
                _hands = XRPieceHands.Create(transform, grabInput, XRSteam.Create(null));
                _hands.Acted += OnActed;
                _hands.BoardChanged += OnBoardChanged;
                _tray = XRTray.Create(_display, grabInput, dominantHand);
                _clock = BuildClock(_display.transform);
            }
            else if (!autoPlay)
            {
                Debug.LogWarning("[XR demo] No grab input assigned; the stations play themselves.", this);
            }

            var index = Mathf.Clamp(startStation, 0, order.Count - 1);
            while (true)
            {
                yield return PlayStation(order[index]);
                index = (index + 1) % order.Count;
            }
        }

        private void Update()
        {
            // Grabs only while a level is in play, and never while the handle is carrying the board.
            if (grabInput != null) grabInput.AcceptsGrabs = _inPlay && (placement == null || !placement.IsMoving);
            if (_hands != null)
            {
                _hands.HoverBand = hoverBand;
                _hands.ThrowThreshold = throwThreshold;
                if (placement != null && placement.Handle != null) placement.Handle.SetAvailable(!_hands.IsHolding);
            }

            if (_tray != null) _tray.DominantHand = dominantHand;
            // The handle's knob rests at the near corner away from the tray.
            if (placement != null && placement.Handle != null) placement.Handle.DominantHand = dominantHand;
            _timer.Tick(Time.deltaTime);
            UpdateClock();
        }

        /// <summary>The clock turns about the vertical to face the player, like the clue signs.</summary>
        private void LateUpdate()
        {
            var head = Camera.main;
            if (_clock == null || head == null || !_clock.gameObject.activeSelf) return;
            var up = _display.transform.up;
            var away = Vector3.ProjectOnPlane(_clock.transform.position - head.transform.position, up);
            if (away.sqrMagnitude > 1e-8f) _clock.transform.rotation = Quaternion.LookRotation(away, up);
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
            _current = station.Level;
            _solved = false;
            Debug.Log($"[XR demo] {station.Level.DisplayName} ({level.Width}x{level.Height})");

            if (_hands != null) yield return PlayByHand(station.Level);
            else yield return PlayItself(station.Level, level);

            if (_solved)
            {
                var finished = false;
                _display.RunTrain(station.Level.DisplayName, () => finished = true);
                while (!finished) yield return null;
            }

            yield return new WaitForSeconds(secondsBetweenStations);
        }

        /// <summary>XR6: the tray beside the board and the player's hands on it, until the board is won.</summary>
        private IEnumerator PlayByHand(LevelDefinition station)
        {
            _tray.Build();
            _hands.Begin(_display, _tray);
            PlaceClock();
            _timer.Reset();
            _grabs = 0;
            _landings = 0;
            _inPlay = true;
            while (!_solved) yield return null;

            _inPlay = false;
            _timer.Stop();
            _hands.End();
            RecordSample(station);
        }

        /// <summary>XR4: lays the solution a piece at a time along the route. Leaves <see cref="_solved"/> false for a board it cannot solve.</summary>
        private IEnumerator PlayItself(LevelDefinition station, LevelData level)
        {
            // Some boards take a solver a second or two; off the main thread, the headset keeps its frame rate.
            var shownAt = Time.time;
            var solve = Task.Run(() => Solver.TrySolve(level, out var solved) ? solved : null);
            while (!solve.IsCompleted) yield return null;
            yield return new WaitForSeconds(Mathf.Max(0f, 1f - (Time.time - shownAt)));

            var solution = solve.IsFaulted ? null : solve.Result;
            if (solution == null)
            {
                Debug.LogWarning($"[XR demo] Could not solve {station.DisplayName}; skipping it.");
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

            _solved = true;
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

        private void OnActed(DropResult result)
        {
            switch (result.Outcome)
            {
                case DropOutcome.Taken:
                case DropOutcome.Lifted:
                    _grabs++;
                    // The first grab of any piece starts the clock (4.2); later grabs leave it running.
                    _timer.Start();
                    break;
                case DropOutcome.Placed:
                case DropOutcome.Moved:
                case DropOutcome.Replaced:
                    _landings++;
                    break;
            }

            Debug.Log($"[XR hands] {result}");
        }

        private void OnBoardChanged()
        {
            if (_display.LastResult != null && _display.LastResult.IsWin) _solved = true;
        }

        /// <summary>
        /// One line of the star-timing sample (XR-PRD 9): how long the station took by hand against the thresholds its
        /// asset carries for the phone, to the log and to a CSV in the app's data folder, where <c>adb pull</c> finds it.
        /// </summary>
        private void RecordSample(LevelDefinition station)
        {
            var seconds = _timer.Elapsed;
            var times = station.StarTimes;
            var stars = ProgressTracker.StarsFor(seconds, times);
            var size = $"{station.Width}x{station.Height}";
            Debug.Log($"[XR play] {station.DisplayName} ({station.Id}, {size}) solved in {seconds:F1} s by hand: " +
                      $"{stars} star(s) against 3 at {times[0]:F0} s and 2 at {times[1]:F0} s; {_grabs} grabs, {_landings} landings.");

            try
            {
                var path = Path.Combine(Application.persistentDataPath, SampleFile);
                var header = !File.Exists(path);
                using (var writer = File.AppendText(path))
                {
                    if (header) writer.WriteLine("utc,level,name,size,seconds,stars,threeStars,twoStars,grabs,landings");
                    writer.WriteLine(string.Join(",",
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), station.Id, $"\"{station.DisplayName}\"", size,
                        seconds.ToString("F2", CultureInfo.InvariantCulture), stars.ToString(CultureInfo.InvariantCulture),
                        times[0].ToString("F0", CultureInfo.InvariantCulture), times[1].ToString("F0", CultureInfo.InvariantCulture),
                        _grabs.ToString(CultureInfo.InvariantCulture), _landings.ToString(CultureInfo.InvariantCulture)));
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[XR play] Could not append to {SampleFile}: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ the placeholder clock (the signboard is XR7's)

        private static TextMesh BuildClock(Transform board)
        {
            var go = new GameObject("Station Clock (until XR7)");
            go.transform.SetParent(board, false);
            var clock = go.AddComponent<TextMesh>();
            clock.font = XRPalette.Font;
            clock.fontSize = 64;
            clock.characterSize = ClockTextHeight / 6.4f;
            clock.anchor = TextAnchor.LowerCenter;
            clock.alignment = TextAlignment.Center;
            clock.color = XRPalette.Led;
            go.GetComponent<MeshRenderer>().sharedMaterial = XROcclusionMaterials.Text(XRPalette.Font);
            go.SetActive(false);
            return clock;
        }

        /// <summary>Over the far edge, which moves with the board's size.</summary>
        private void PlaceClock()
        {
            if (_clock == null || _display.Level == null) return;
            _clock.transform.localPosition = new Vector3(0f, ClockHeight, (float)BoardLayout.HalfDepth(_display.Level.Height) + ClockBeyondEdge);
            _clock.gameObject.SetActive(true);
        }

        private void UpdateClock()
        {
            if (_clock == null || _current == null) return;
            var times = _current.StarTimes;
            var targets = times[0] > 0
                ? $"3 stars {ProgressTracker.FormatTime(times[0])}   2 stars {ProgressTracker.FormatTime(times[1])}"
                : "";
            var clock = ProgressTracker.FormatTime(_timer.Elapsed);
            _clock.text = _solved
                ? $"{_current.DisplayName}\nSolved in {clock}: {ProgressTracker.StarsFor(_timer.Elapsed, times)} stars"
                : $"{_current.DisplayName}   {clock}\n{targets}";
        }
    }
}
