using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Runs the train along the solved board's <see cref="TrackPath"/> (XR-PRD 8): locomotive plus wagons, each a fixed
    /// distance behind the one ahead, at a constant speed, hidden while inside a tunnel. Raises <see cref="Finished"/>
    /// when the last car has left through the exit, or after a short wait when the board has no route. Forked from the
    /// phone's <c>TrainRunner</c> without its audio, which XR10 brings back spatialised.
    /// </summary>
    public sealed class XRTrainRun : MonoBehaviour
    {
        private const double DefaultRevealDistance = 0.5;
        private const float NoRouteDuration = 1.5f;
        private const float DefaultSpacing = 0.9f;
        private const float DefaultSpeed = 2.5f;

        /// <summary>The destination plate: how high above the car it rides and how tall its text is.</summary>
        private const float PlateHeight = 0.72f;
        private const float PlateTextHeight = 0.22f;

        [SerializeField] private XRBoardAssets assets;
        [SerializeField] private Transform cars;

        private readonly List<Transform> _cars = new List<Transform>();
        private readonly List<Renderer[]> _renderers = new List<Renderer[]>();
        private Transform _plate;
        private string _destination = "";
        private TrackPath _path;
        private double _distance;
        private float _noRouteRemaining;
        private bool _running;

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

        /// <summary>The station the train is bound for, shown on the locomotive's plate (D13). An untranslated proper noun.</summary>
        public void SetDestination(string station) => _destination = station ?? "";

        public void Run(Board board)
        {
            Stop();
            _running = true;
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
            _plate = null;
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

            _distance += Speed * Time.deltaTime;
            PlaceCars();
            var lastCar = _distance - (_cars.Count - 1) * Spacing;
            if (lastCar > _path.Length - RevealDistance + 0.1) Finish();
        }

        private void Finish()
        {
            Stop();
            Finished?.Invoke();
        }

        private void PlaceCars()
        {
            var height = assets != null ? assets.HeightOffset : 0f;
            var yaw = assets != null ? assets.ModelYawOffset : 0f;
            for (var i = 0; i < _cars.Count; i++)
            {
                var s = _distance - i * Spacing;
                var sample = _path.Sample(s);
                var car = _cars[i];
                car.localPosition = new Vector3((float)sample.X, height, (float)sample.Z);
                var forward = new Vector3((float)sample.TangentX, 0f, (float)sample.TangentZ);
                car.localRotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, yaw, 0f);
                var visible = s >= RevealDistance && s <= _path.Length - RevealDistance;
                foreach (var renderer in _renderers[i]) renderer.enabled = visible;
            }

            // The plate rides the locomotive but turns to face the player like the clue signs: a destination board is
            // there to be read, and one that turns with every curve would only be legible by luck.
            var head = Camera.main;
            if (_plate == null || head == null) return;
            var up = transform.up;
            var away = Vector3.ProjectOnPlane(_plate.position - head.transform.position, up);
            if (away.sqrMagnitude > 1e-8f) _plate.rotation = Quaternion.LookRotation(away, up);
        }

        private void BuildCars()
        {
            var count = assets != null ? assets.CarCount : 3;
            for (var i = 0; i < count; i++)
            {
                var prefab = assets == null ? null : i == 0 ? assets.Locomotive : assets.Wagons[i - 1];
                var car = prefab != null ? Instantiate(prefab, cars) : BuildPlaceholderCar(i == 0);
                car.name = i == 0 ? "Locomotive" : $"Wagon {i}";
                car.transform.SetParent(cars, false);
                car.transform.localScale = Vector3.one * (assets != null ? assets.ModelScale : 1f);
                XROcclusionMaterials.Convert(car);
                foreach (var collider in car.GetComponentsInChildren<Collider>()) Destroy(collider);
                if (i == 0) FitDestinationPlate(car.transform);
                _cars.Add(car.transform);
                _renderers.Add(car.GetComponentsInChildren<Renderer>());
            }
        }

        /// <summary>The station name and the Japanese for "bound for" above the cab: environment art (D13).</summary>
        private void FitDestinationPlate(Transform locomotive)
        {
            var font = assets != null ? assets.SignageFont : null;
            if (font == null || string.IsNullOrEmpty(_destination)) return;

            var go = new GameObject("Destination Plate");
            go.transform.SetParent(locomotive, false);
            go.transform.localPosition = new Vector3(0f, PlateHeight, 0f);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.text = $"{_destination} {assets.DestinationSuffix}".Trim();
            text.fontSize = 64;
            text.characterSize = PlateTextHeight / 6.4f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = XRPalette.Led;
            go.GetComponent<MeshRenderer>().sharedMaterial = XROcclusionMaterials.Text(font);
            _plate = go.transform;
        }

        /// <summary>Boxy stand-in cars for assets with no train models.</summary>
        private GameObject BuildPlaceholderCar(bool locomotive)
        {
            var root = new GameObject(locomotive ? "Locomotive" : "Wagon");
            root.transform.SetParent(cars, false);
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
