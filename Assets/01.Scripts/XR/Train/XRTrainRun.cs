using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Runs the train along the solved board's <see cref="TrackPath"/> (XR-PRD 8): locomotive plus wagons, each a set
    /// distance behind the one ahead, hidden while inside a tunnel. Raises <see cref="Finished"/> when the last car has
    /// left through the exit, or after a short wait when the board has no route. Forked from the phone's
    /// <c>TrainRunner</c> without its audio, which XR10 brings back spatialised. A hand on a car shakes the train and
    /// hurries it on, cartoon fashion (<see cref="Push"/>).
    /// </summary>
    /// <remarks>
    /// Each car is a pivot on the rails carrying its model, so the squash and stretch scales along the direction of
    /// travel whatever way the model was authored (<see cref="XRBoardAssets.ModelYawOffset"/> sits on the model). The
    /// destination plate rides beside the pivots rather than under one, so the stretch never shears its text.
    /// </remarks>
    public sealed class XRTrainRun : MonoBehaviour
    {
        private const double DefaultRevealDistance = 0.5;
        private const float NoRouteDuration = 1.5f;
        private const float DefaultSpacing = 0.9f;
        private const float DefaultSpeed = 2.5f;

        /// <summary>The destination plate: how high above the car it rides and how tall its text is, in model units.</summary>
        private const float PlateHeight = 0.72f;
        private const float PlateTextHeight = 0.22f;

        /// <summary>
        /// A hand on the train: how close a fingertip or pinch point must come to a car's box, in cells (about 1 cm), and
        /// how much of a car's shake the next one down the couplings takes.
        /// </summary>
        private const float TouchMarginCells = 0.2f;
        private const float CouplingShare = 0.7f;

        /// <summary>At full turbulence, how far a car sways and hops, in cells, how far it pitches and rolls, in degrees, and how quickly.</summary>
        private const float SwayCells = 0.14f;
        private const float BounceCells = 0.09f;
        private const float PitchDegrees = 7f;
        private const float RollDegrees = 14f;
        private const float ShakeHertz = 11f;

        /// <summary>How quickly the shake dies away once the hand is off, as a time constant in seconds.</summary>
        private const float ShakeFadeSeconds = 0.5f;

        /// <summary>The rattle every car keeps while the train is hurrying, as a share of full turbulence at top speed.</summary>
        private const float HurryRattle = 0.4f;

        /// <summary>
        /// The push, as extra speed over cruising: a kick at first contact, a climb per second while the hand stays, a cap
        /// (three times cruising in all), and the time constant it eases back to cruising with once let go.
        /// </summary>
        private const float KickBoost = 0.5f;
        private const float BoostPerSecond = 1.5f;
        private const float MaxBoost = 2f;
        private const float BoostFadeSeconds = 1.5f;

        /// <summary>
        /// Squash and stretch: how much longer a car grows per unit of push (1.3 times at top speed, thinner by as much so
        /// it keeps its volume), and the spring each car follows the one ahead with, so the stretch runs down the train
        /// from the locomotive to the last car. Loosely damped, so it overshoots and wobbles back like rubber.
        /// </summary>
        private const float StretchPerBoost = 0.15f;
        private const float StretchStiffness = 220f;
        private const float StretchDamping = 9f;
        private const float MinStretch = 0.7f;
        private const float MaxStretch = 1.5f;

        /// <summary>The poke: the touched car squashes at this rate before it springs out.</summary>
        private const float PokeSquash = -2.5f;

        /// <summary>How far a car rears, nose up, per unit of stretch a second as it stretches (and dips as it shrinks), in degrees, and the most it will.</summary>
        private const float LeanPerStretchSpeed = 5f;
        private const float MaxLeanDegrees = 15f;

        /// <summary>
        /// Steam chuffed from the locomotive's roof while hurrying, from this much push up: once every this many seconds at
        /// top speed, and proportionally slower below it (under a second just past the threshold).
        /// </summary>
        private const float ChuffSeconds = 0.35f;
        private const float ChuffFromBoost = 0.2f;

        [SerializeField] private XRBoardAssets assets;
        [SerializeField] private Transform cars;

        private readonly List<Transform> _cars = new List<Transform>();
        private readonly List<Renderer[]> _renderers = new List<Renderer[]>();
        private Transform _plate;
        private Renderer _plateRenderer;
        private XRSteam _steam;
        private string _destination = "";
        private TrackPath _path;
        private double _distance;
        private double _tail;
        private float _noRouteRemaining;
        private float _chuff;
        private bool _running;

        // Per car: where it rides unshaken and which way it heads, its box in that heading, its shake, its stretch and
        // how fast that is changing, and whether it is out of the tunnels.
        private readonly List<Vector3> _rest = new List<Vector3>();
        private readonly List<Quaternion> _heading = new List<Quaternion>();
        private readonly List<Bounds> _boxes = new List<Bounds>();
        private readonly List<float> _shake = new List<float>();
        private readonly List<float> _stretch = new List<float>();
        private readonly List<float> _stretchSpeed = new List<float>();
        private readonly List<bool> _shown = new List<bool>();
        private float _boost;
        private bool _touching;

        public bool IsRunning => _running;

        /// <summary>How far along the route a car becomes visible; the board sets it from the tunnel it built.</summary>
        public double RevealDistance { get; set; } = DefaultRevealDistance;

        public event Action Finished;

        public static XRTrainRun Create(Transform parent, XRBoardAssets assets)
        {
            var go = new GameObject("Train");
            go.transform.SetParent(parent, false);
            var run = go.AddComponent<XRTrainRun>();
            run.assets = assets;
            run.cars = new GameObject("Cars").transform;
            run.cars.SetParent(go.transform, false);
            return run;
        }

        private float Spacing => assets != null ? assets.CarSpacing : DefaultSpacing;
        private float Speed => assets != null ? assets.SpeedCellsPerSecond : DefaultSpeed;
        private float ModelScale => assets != null ? assets.ModelScale : 1f;

        /// <summary>The station the train is bound for, shown on the locomotive's plate (D13). An untranslated proper noun.</summary>
        public void SetDestination(string station) => _destination = station ?? "";

        public void Run(Board board)
        {
            Stop();
            _running = true;
            // Made on the first run, so a train built in the Editor gets one too: the steam's particle system is set up in its Awake.
            if (_steam == null) _steam = XRSteam.Create(transform);
            if (board == null || !TrackPath.TryBuild(board, out _path))
            {
                _path = null;
                _noRouteRemaining = NoRouteDuration;
                return;
            }

            BuildCars();
            _distance = RevealDistance - 0.05;
            PlaceCars();
        }

        public void Stop()
        {
            _running = false;
            _path = null;
            if (cars != null)
                for (var i = cars.childCount - 1; i >= 0; i--) Destroy(cars.GetChild(i).gameObject);
            _cars.Clear();
            _renderers.Clear();
            _rest.Clear();
            _heading.Clear();
            _boxes.Clear();
            _shake.Clear();
            _stretch.Clear();
            _stretchSpeed.Clear();
            _shown.Clear();
            _boost = 0f;
            _chuff = 0f;
            _touching = false;
            _plate = null;
            _plateRenderer = null;
        }

        private void Update()
        {
            if (!_running) return;
            if (_path == null)
            {
                _noRouteRemaining -= Time.deltaTime;
                if (_noRouteRemaining <= 0f) Finish();
                return;
            }

            Push(Time.deltaTime);
            Spring(Time.deltaTime);
            Chuff(Time.deltaTime);
            _distance += Speed * (1f + _boost) * Time.deltaTime;
            PlaceCars();
            if (_tail > _path.Length - RevealDistance + 0.1) Finish();
        }

        private void Finish()
        {
            Stop();
            Finished?.Invoke();
        }

        private void PlaceCars()
        {
            var height = assets != null ? assets.HeightOffset : 0f;
            var time = Time.time * ShakeHertz;
            var s = _distance;
            for (var i = 0; i < _cars.Count; i++)
            {
                // The couplings stretch with the cars, so a hurrying train draws out rather than the cars running into each other.
                if (i > 0) s -= Spacing * 0.5f * (_stretch[i - 1] + _stretch[i]);
                var sample = _path.Sample(s);
                var forward = new Vector3((float)sample.TangentX, 0f, (float)sample.TangentZ);
                _rest[i] = new Vector3((float)sample.X, height, (float)sample.Z);
                _heading[i] = Quaternion.LookRotation(forward, Vector3.up);

                Shake(i, time, out var offset, out var pitch, out var roll);
                // Rearing as it stretches, like a cartoon car pulling away; nose-up is a negative pitch.
                pitch -= Mathf.Clamp(_stretchSpeed[i] * LeanPerStretchSpeed, -MaxLeanDegrees, MaxLeanDegrees);
                var stretch = _stretch[i];
                var squash = 1f / Mathf.Sqrt(stretch);
                // Lifted by what the tilt would sink into the rails, so a rocking car rides on them rather than through them.
                var extents = _boxes[i].extents;
                offset.y += Mathf.Abs(Mathf.Sin(pitch * Mathf.Deg2Rad)) * extents.z * stretch +
                            Mathf.Abs(Mathf.Sin(roll * Mathf.Deg2Rad)) * extents.x * squash;

                var car = _cars[i];
                car.localPosition = _rest[i] + _heading[i] * offset;
                car.localRotation = _heading[i] * Quaternion.Euler(pitch, 0f, roll);
                car.localScale = new Vector3(squash, squash, stretch);
                var visible = s >= RevealDistance && s <= _path.Length - RevealDistance;
                _shown[i] = visible;
                foreach (var renderer in _renderers[i]) renderer.enabled = visible;
            }

            _tail = s;

            // The plate rides the locomotive but turns to face the player like the clue signs: a destination board is
            // there to be read, and one that turns with every curve would only be legible by luck.
            if (_plate == null || _cars.Count == 0) return;
            _plate.position = _cars[0].TransformPoint(0f, PlateHeight * ModelScale, 0f);
            _plateRenderer.enabled = _shown[0];
            var head = Camera.main;
            if (head == null) return;
            var up = transform.up;
            var away = Vector3.ProjectOnPlane(_plate.position - head.transform.position, up);
            if (away.sqrMagnitude > 1e-8f) _plate.rotation = Quaternion.LookRotation(away, up);
        }

        private void BuildCars()
        {
            var count = assets != null ? assets.CarCount : 3;
            var yaw = Quaternion.Euler(0f, assets != null ? assets.ModelYawOffset : 0f, 0f);
            for (var i = 0; i < count; i++)
            {
                var pivot = new GameObject(i == 0 ? "Locomotive" : $"Wagon {i}").transform;
                pivot.SetParent(cars, false);

                var prefab = assets == null ? null : i == 0 ? assets.Locomotive : assets.Wagons[i - 1];
                var model = prefab != null ? Instantiate(prefab, pivot) : BuildPlaceholderCar(i == 0, pivot);
                model.name = "Model";
                model.transform.SetParent(pivot, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = yaw;
                model.transform.localScale = Vector3.one * ModelScale;
                XROcclusionMaterials.Convert(model);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);

                _boxes.Add(Measure(pivot));
                _cars.Add(pivot);
                _renderers.Add(model.GetComponentsInChildren<Renderer>());
                _rest.Add(Vector3.zero);
                _heading.Add(Quaternion.identity);
                _shake.Add(0f);
                _stretch.Add(1f);
                _stretchSpeed.Add(0f);
                _shown.Add(false);
            }

            FitDestinationPlate();
        }

        /// <summary>
        /// A car's box in its heading frame (forward along +Z), in cells, from its renderers: what a hand must come within
        /// <see cref="TouchMarginCells"/> of to touch it. Measured once, with the pivot standing at the origin unscaled.
        /// </summary>
        private Bounds Measure(Transform pivot)
        {
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            var box = new Bounds();
            var any = false;
            foreach (var renderer in pivot.GetComponentsInChildren<Renderer>())
            {
                var local = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    var point = cars.InverseTransformPoint(renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, sign)));
                    if (any) box.Encapsulate(point);
                    else box = new Bounds(point, Vector3.zero);
                    any = true;
                }
            }

            // A car that draws nothing: the placeholder's size.
            return any ? box : new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(0.62f, 0.5f, 0.85f));
        }

        // ------------------------------------------------------------------ a hand on the train

        /// <summary>
        /// A hand on the train: the car it touches shakes at full turbulence and passes a share of it down the couplings
        /// either way, squashes, and puffs steam where it was touched; the whole train is pushed on, with a kick at first
        /// contact and a steady climb while the hand stays, up to <see cref="MaxBoost"/>. The shake dies away once it is
        /// let go, but a hurrying train keeps rattling until it is back to cruising.
        /// </summary>
        private void Push(float deltaTime)
        {
            var fade = Mathf.Exp(-deltaTime / ShakeFadeSeconds);
            for (var i = 0; i < _shake.Count; i++) _shake[i] *= fade;

            var touched = TouchedCar(out var at);
            if (touched >= 0)
            {
                for (var i = 0; i < _shake.Count; i++) _shake[i] = Mathf.Max(_shake[i], Mathf.Pow(CouplingShare, Mathf.Abs(i - touched)));
                if (!_touching)
                {
                    _stretchSpeed[touched] += PokeSquash;
                    if (_steam != null) _steam.Puff(at);
                }

                _boost = Mathf.Min(MaxBoost, _boost + (_touching ? BoostPerSecond * deltaTime : KickBoost));
            }
            else
            {
                _boost *= Mathf.Exp(-deltaTime / BoostFadeSeconds);
            }

            _touching = touched >= 0;
            var rattle = HurryRattle * _boost / MaxBoost;
            for (var i = 0; i < _shake.Count; i++) _shake[i] = Mathf.Max(_shake[i], rattle);
        }

        /// <summary>
        /// Each car's stretch springs towards the one ahead of it, and the locomotive's towards the push, so a change of
        /// pace runs down the train from front to back.
        /// </summary>
        private void Spring(float deltaTime)
        {
            // A long frame would throw a stiff spring off: step no more than a thirtieth of a second.
            var step = Mathf.Min(deltaTime, 1f / 30f);
            for (var i = 0; i < _stretch.Count; i++)
            {
                var target = i == 0 ? 1f + StretchPerBoost * _boost : _stretch[i - 1];
                _stretchSpeed[i] += (StretchStiffness * (target - _stretch[i]) - StretchDamping * _stretchSpeed[i]) * step;
                _stretch[i] = Mathf.Clamp(_stretch[i] + _stretchSpeed[i] * step, MinStretch, MaxStretch);
            }
        }

        /// <summary>Steam off the locomotive's roof while hurrying, coming quicker the faster it goes.</summary>
        private void Chuff(float deltaTime)
        {
            if (_steam == null || _cars.Count == 0 || !_shown[0] || _boost < ChuffFromBoost) return;
            _chuff -= deltaTime * (1f + _boost) / 3f;
            if (_chuff > 0f) return;
            _chuff = ChuffSeconds;
            var box = _boxes[0];
            _steam.Puff(_cars[0].TransformPoint(new Vector3(box.center.x, box.max.y, box.center.z + box.extents.z * 0.5f)));
        }

        /// <summary>The car a fingertip or pinch point is on, or -1, and where. Only a car out of the tunnels can be touched.</summary>
        private int TouchedCar(out Vector3 at)
        {
            foreach (var tip in XRTouchPoints.Fingertips)
            {
                var car = CarAt(tip.Position);
                if (car < 0) continue;
                at = tip.Position;
                return car;
            }

            foreach (var pinch in XRTouchPoints.PinchPoints)
            {
                var car = CarAt(pinch);
                if (car < 0) continue;
                at = pinch;
                return car;
            }

            at = default;
            return -1;
        }

        private int CarAt(Vector3 world)
        {
            var point = cars.InverseTransformPoint(world);
            for (var i = 0; i < _cars.Count; i++)
            {
                if (!_shown[i]) continue;
                var box = _boxes[i];
                box.Expand(2f * TouchMarginCells);
                if (box.Contains(Quaternion.Inverse(_heading[i]) * (point - _rest[i]))) return i;
            }

            return -1;
        }

        /// <summary>Car <paramref name="i"/>'s shake this frame, in its heading frame: a sway and a hop, a pitch and a roll.</summary>
        private void Shake(int i, float time, out Vector3 offset, out float pitch, out float roll)
        {
            var amount = _shake[i];
            if (amount < 1e-3f)
            {
                offset = Vector3.zero;
                pitch = 0f;
                roll = 0f;
                return;
            }

            // Each car on its own stretch of the noise, so they rattle apart. The hop is only ever up, off the rails.
            var seed = 11.3f * (i + 1);
            offset = new Vector3(Wobble(time, seed) * SwayCells, Mathf.Abs(Wobble(time, seed + 3.7f)) * BounceCells, 0f) * amount;
            pitch = Wobble(time, seed + 7.1f) * PitchDegrees * amount;
            roll = Wobble(time, seed + 5.3f) * RollDegrees * amount;
        }

        /// <summary>Smooth noise from -1 to 1.</summary>
        private static float Wobble(float time, float channel) => Mathf.Clamp(Mathf.PerlinNoise(time, channel) * 2f - 1f, -1f, 1f);

        /// <summary>The station name and the Japanese for "bound for" above the cab: environment art (D13).</summary>
        private void FitDestinationPlate()
        {
            var font = assets != null ? assets.SignageFont : null;
            if (font == null || string.IsNullOrEmpty(_destination)) return;

            var go = new GameObject("Destination Plate");
            go.transform.SetParent(cars, false);
            // Sized as it was when it hung off the model, which carries the model scale.
            go.transform.localScale = Vector3.one * ModelScale;
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.text = $"{_destination} {assets.DestinationSuffix}".Trim();
            text.fontSize = 64;
            text.characterSize = PlateTextHeight / 6.4f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = XRPalette.Led;
            _plateRenderer = go.GetComponent<MeshRenderer>();
            _plateRenderer.sharedMaterial = XROcclusionMaterials.Text(font);
            _plate = go.transform;
        }

        /// <summary>Boxy stand-in cars for assets with no train models.</summary>
        private static GameObject BuildPlaceholderCar(bool locomotive, Transform parent)
        {
            var root = new GameObject(locomotive ? "Locomotive" : "Wagon");
            root.transform.SetParent(parent, false);
            var body = locomotive ? new Color(0.70f, 0.18f, 0.15f) : new Color(0.25f, 0.40f, 0.65f);
            Box(root.transform, "Chassis", new Vector3(0f, 0.10f, 0f), new Vector3(0.62f, 0.10f, 0.85f), new Color(0.15f, 0.15f, 0.17f));
            Box(root.transform, "Body", new Vector3(0f, 0.30f, 0f), new Vector3(0.56f, 0.30f, 0.72f), body);
            return root;
        }

        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            if (!Materials.TryGetValue(color, out var material) || material == null)
                Materials[color] = material = new Material(XRBoardMaterials.Track) { name = "XR Train", color = color };
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
