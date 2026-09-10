using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Keeps the camera pitched at the board and framed so the whole board (tiles, tunnels, clue labels and the train)
    /// is on screen, re-fitting whenever the viewport aspect or the projection changes. Works with a perspective camera
    /// (moves it back along the view direction) and an orthographic one (grows the orthographic size). The projection is
    /// shifted so the board is centred in the strip below the HUD instead of the whole screen, and the camera aims at
    /// the centre of the content rather than the grid, so one-sided clue labels do not push the board off centre.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCamera : MonoBehaviour
    {
        /// <summary>Camera distance from the aim point when orthographic, where distance does not affect framing.</summary>
        private const float OrthographicDistance = 30f;

        [Range(30f, 90f)] [SerializeField] private float pitchDegrees = 60f;

        [Tooltip("Share of the strip below the HUD the board may fill vertically; the rest is breathing room.")]
        [Range(0.5f, 1f)] [SerializeField] private float verticalFraction = 0.9f;

        [Tooltip("Share of the viewport width the board may fill; the rest is breathing room.")]
        [Range(0.5f, 1f)] [SerializeField] private float horizontalFraction = 0.94f;

        [Tooltip("Share of the viewport height reserved along the top edge for the HUD (pause button, level name, clock).")]
        [Range(0f, 0.4f)] [SerializeField] private float hudFraction = 0.1f;

        private Camera _camera;
        private Vector3 _aim;          // centre of the box's bottom face, the point the camera looks at
        private double _halfWidth;
        private double _halfDepth;
        private double _height;
        private float _lastAspect = -1f;
        private bool _lastOrthographic;
        private float _lastFieldOfView;
        private bool _hasTarget;

        public float PitchDegrees => pitchDegrees;
        public Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        /// <summary>Viewport width over height as the camera currently renders it.</summary>
        public float Aspect => Camera.pixelHeight > 0 ? (float)Camera.pixelWidth / Camera.pixelHeight : 1f;

        /// <summary>Adds the component to a camera if it is missing and gives the camera the board's solid backdrop.</summary>
        public static BoardCamera Attach(Camera camera)
        {
            if (camera == null) return null;
            if (!camera.TryGetComponent<BoardCamera>(out var rig)) rig = camera.gameObject.AddComponent<BoardCamera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.BoardBackground;
            rig.transform.SetPositionAndRotation(new Vector3(0f, 10f, -5f), Quaternion.Euler(rig.pitchDegrees, 0f, 0f));
            return rig;
        }

        /// <summary>Frames a world-space box; the camera aims at the centre of its bottom face.</summary>
        public void SetTarget(Bounds bounds)
        {
            _aim = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            _halfWidth = bounds.extents.x;
            _halfDepth = bounds.extents.z;
            _height = bounds.size.y;
            _hasTarget = true;
            Fit(Aspect);
        }

        /// <summary>Frames a box centred on the origin: half extents on the ground plus a height above it.</summary>
        public void SetTarget(double halfWidth, double halfDepth, double height) =>
            SetTarget(new Bounds(new Vector3(0f, (float)height / 2f, 0f), new Vector3(2f * (float)halfWidth, (float)height, 2f * (float)halfDepth)));

        private void LateUpdate()
        {
            if (!_hasTarget) return;
            var camera = Camera;
            if (!Mathf.Approximately(Aspect, _lastAspect) || camera.orthographic != _lastOrthographic || !Mathf.Approximately(camera.fieldOfView, _lastFieldOfView))
                Fit(Aspect);
        }

        private void OnValidate()
        {
            if (_hasTarget && Camera != null) Fit(Aspect);
        }

        /// <summary>Places the camera and builds its projection for the given viewport aspect. Public so tools can fit for an aspect the Editor is not showing.</summary>
        public void Fit(float aspect)
        {
            var camera = Camera;
            _lastAspect = aspect;
            _lastOrthographic = camera.orthographic;
            _lastFieldOfView = camera.fieldOfView;
            if (!_hasTarget || aspect <= 0f) return;

            // The board is centred in the strip below the HUD: fit symmetrically into that strip's height, then shift
            // the projection down by the HUD share so the aim point (the origin) lands in the strip's middle.
            var strip = 1f - hudFraction;
            var vertical = strip * verticalFraction;

            Matrix4x4 projection;
            if (camera.orthographic)
            {
                var size = (float)CameraFit.SolveOrthographicSize(_halfWidth, _halfDepth, _height, pitchDegrees, aspect, horizontalFraction, vertical);
                camera.orthographicSize = size;
                Place(OrthographicDistance);
                projection = Matrix4x4.Ortho(-size * aspect, size * aspect, -size, size, camera.nearClipPlane, camera.farClipPlane);
            }
            else
            {
                var placement = CameraFit.Solve(_halfWidth, _halfDepth, _height, pitchDegrees, camera.fieldOfView, aspect, horizontalFraction, vertical);
                Place((float)placement.Distance);
                projection = Matrix4x4.Perspective(camera.fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);
            }

            // Clip-space shift: y' = y - hud * w moves the image down by hudFraction of the viewport height.
            var shift = Matrix4x4.identity;
            shift.m13 = -hudFraction;
            camera.projectionMatrix = shift * projection;
        }

        private void Place(float distance)
        {
            var pitch = pitchDegrees * Mathf.Deg2Rad;
            transform.SetPositionAndRotation(
                _aim + new Vector3(0f, distance * Mathf.Sin(pitch), -distance * Mathf.Cos(pitch)),
                Quaternion.Euler(pitchDegrees, 0f, 0f));
        }
    }
}
