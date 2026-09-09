using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>Keeps the camera pitched at the board and far enough back that the whole board fits, re-fitting when the viewport aspect changes.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCamera : MonoBehaviour
    {
        [Range(30f, 90f)] [SerializeField] private float pitchDegrees = 60f;

        [Tooltip("Share of the viewport height the board may fill, leaving room for the HUD at the top.")]
        [Range(0.5f, 1f)] [SerializeField] private float verticalFraction = 0.86f;

        [Range(0.5f, 1f)] [SerializeField] private float horizontalFraction = 0.96f;

        private Camera _camera;
        private double _halfWidth;
        private double _halfDepth;
        private float _lastAspect = -1f;
        private bool _hasTarget;

        public float PitchDegrees => pitchDegrees;
        public Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        /// <summary>Adds the component to a camera if it is missing and gives the camera the board's solid backdrop.</summary>
        public static BoardCamera Attach(Camera camera)
        {
            if (camera == null) return null;
            if (!camera.TryGetComponent<BoardCamera>(out var rig)) rig = camera.gameObject.AddComponent<BoardCamera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiBuilder.Background;
            rig.transform.SetPositionAndRotation(new Vector3(0f, 10f, -5f), Quaternion.Euler(rig.pitchDegrees, 0f, 0f));
            return rig;
        }

        public void SetTarget(double halfWidth, double halfDepth)
        {
            _halfWidth = halfWidth;
            _halfDepth = halfDepth;
            _hasTarget = true;
            Fit();
        }

        private void LateUpdate()
        {
            if (_hasTarget && !Mathf.Approximately(Camera.aspect, _lastAspect)) Fit();
        }

        private void Fit()
        {
            _lastAspect = Camera.aspect;
            var placement = CameraFit.Solve(_halfWidth, _halfDepth, pitchDegrees, Camera.fieldOfView, Camera.aspect, horizontalFraction, verticalFraction);
            transform.SetPositionAndRotation(
                new Vector3(0f, (float)placement.Height, -(float)placement.Back),
                Quaternion.Euler(pitchDegrees, 0f, 0f));
        }
    }
}
