using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>Keeps the camera pitched at the board and far enough back that the whole board fits, re-fitting when the viewport aspect changes.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCamera : MonoBehaviour
    {
        [Range(30f, 90f)] [SerializeField] private float pitchDegrees = 60f;

        [Tooltip("Share of the viewport the board may fill, leaving room for the HUD at the top and bottom.")]
        [Range(0.5f, 1f)] [SerializeField] private float verticalFraction = 0.8f;

        [Range(0.5f, 1f)] [SerializeField] private float horizontalFraction = 0.96f;

        private Camera _camera;
        private double _halfWidth;
        private double _halfDepth;
        private float _lastAspect = -1f;
        private bool _hasTarget;

        public float PitchDegrees => pitchDegrees;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
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
            if (_hasTarget && !Mathf.Approximately(_camera.aspect, _lastAspect)) Fit();
        }

        private void Fit()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _lastAspect = _camera.aspect;
            var placement = CameraFit.Solve(_halfWidth, _halfDepth, pitchDegrees, _camera.fieldOfView, _camera.aspect, horizontalFraction, verticalFraction);
            transform.SetPositionAndRotation(
                new Vector3(0f, (float)placement.Height, -(float)placement.Back),
                Quaternion.Euler(pitchDegrees, 0f, 0f));
        }
    }
}
