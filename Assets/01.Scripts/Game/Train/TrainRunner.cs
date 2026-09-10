using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Runs the train along the solved board's <see cref="TrackPath"/> (PRD section 8): locomotive plus wagons, each a
    /// fixed distance behind the previous one, at a constant speed. Cars are hidden while they are inside a tunnel.
    /// Raises <see cref="Finished"/> when the last car has left through the exit, or after a short wait when the board
    /// has no route (the editor's debug win).
    /// </summary>
    public sealed class TrainRunner : MonoBehaviour
    {
        private const double TunnelMouthDistance = 0.5;
        private const float NoRouteDuration = 1.5f;
        private const float DefaultSpacing = 0.9f;
        private const float DefaultSpeed = 2.5f;

        /// <summary>The destination plate: how high above the car it rides, how tall its text is, and the pitch it
        /// is held at so it stays readable under the board camera.</summary>
        private const float PlateHeight = 0.72f;
        private const float PlateTextHeight = 0.22f;
        private const float PlateReadingPitch = 60f;

        [SerializeField] private TrainAssets assets;
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

        public event Action Finished;

        public static TrainRunner Create(Transform parent, TrainAssets assets)
        {
            var go = new GameObject("Train");
            go.transform.SetParent(parent, false);
            var runner = go.AddComponent<TrainRunner>();
            runner.assets = assets;
            runner.cars = new GameObject("Cars").transform;
            runner.cars.SetParent(go.transform, false);
            return runner;
        }

        public void Configure(TrainAssets trainAssets)
        {
            if (trainAssets != null) assets = trainAssets;
            if (cars == null)
            {
                cars = new GameObject("Cars").transform;
                cars.SetParent(transform, false);
            }
        }

        private float Spacing => assets != null ? assets.CarSpacing : DefaultSpacing;
        private float Speed => assets != null ? assets.SpeedCellsPerSecond : DefaultSpeed;

        /// <summary>
        /// The station the train is bound for, shown on the locomotive's plate (D13). Set it before
        /// <see cref="Run"/>; the name itself is an untranslated proper noun (D14).
        /// </summary>
        public void SetDestination(string station) => _destination = station ?? "";

        /// <summary>Starts the run. Without a connected route the runner waits briefly and then finishes.</summary>
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
            // Start with the locomotive just inside the entrance tunnel so it emerges on the first frame.
            _distance = TunnelMouthDistance - 0.05;
            PlaceCars();
        }

        public void Stop()
        {
            _running = false;
            _path = null;
            SceneObjects.Clear(cars);
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
            if (lastCar > _path.Length - TunnelMouthDistance + 0.1) Finish();
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

                // Hidden while inside either tunnel, so it appears at the entrance mouth and vanishes at the exit mouth.
                var visible = s >= TunnelMouthDistance && s <= _path.Length - TunnelMouthDistance;
                foreach (var renderer in _renderers[i]) renderer.enabled = visible;
            }

            // The plate rides the locomotive but keeps the camera's pitch, like the tunnel letters: a destination
            // board is there to be read, and one that turns with every curve would only be legible by luck.
            if (_plate != null) _plate.rotation = Quaternion.Euler(PlateReadingPitch, 0f, 0f);
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
                foreach (var collider in car.GetComponentsInChildren<Collider>()) Destroy(collider);
                if (i == 0) FitDestinationPlate(car.transform);
                _cars.Add(car.transform);
                _renderers.Add(car.GetComponentsInChildren<Renderer>());
            }
        }

        /// <summary>
        /// The destination board above the cab: the station name and the Japanese for "bound for". Environment art,
        /// so it reads the same in every locale (D13); without a face that has the glyphs, no plate is fitted.
        /// </summary>
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
            text.color = Palette.Led;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            _plate = go.transform;
        }

        /// <summary>Boxy stand-in cars until the kit models are assigned in <see cref="TrainAssets"/>.</summary>
        private GameObject BuildPlaceholderCar(bool locomotive)
        {
            var root = new GameObject(locomotive ? "Locomotive" : "Wagon");
            root.transform.SetParent(cars, false);
            var body = locomotive ? new Color(0.70f, 0.18f, 0.15f) : new Color(0.25f, 0.40f, 0.65f);
            var trim = new Color(0.15f, 0.15f, 0.17f);

            Box(root.transform, "Chassis", new Vector3(0f, 0.10f, 0f), new Vector3(0.62f, 0.10f, 0.85f), trim);
            if (locomotive)
            {
                Box(root.transform, "Boiler", new Vector3(0f, 0.30f, 0.10f), new Vector3(0.44f, 0.32f, 0.55f), body);
                Box(root.transform, "Cab", new Vector3(0f, 0.36f, -0.28f), new Vector3(0.56f, 0.44f, 0.26f), body);
                var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chimney.name = "Chimney";
                chimney.transform.SetParent(root.transform, false);
                chimney.transform.localPosition = new Vector3(0f, 0.55f, 0.25f);
                chimney.transform.localScale = new Vector3(0.12f, 0.10f, 0.12f);
                chimney.GetComponent<MeshRenderer>().sharedMaterial = TrainMaterial(trim);
            }
            else
            {
                Box(root.transform, "Body", new Vector3(0f, 0.30f, 0f), new Vector3(0.56f, 0.30f, 0.72f), body);
            }

            return root;
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = TrainMaterial(color);
        }

        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        private static Material TrainMaterial(Color color)
        {
            if (Materials.TryGetValue(color, out var material) && material != null) return material;
            material = new Material(BoardMaterials.Track) { name = "Train", color = color };
            Materials[color] = material;
            return material;
        }
    }
}
