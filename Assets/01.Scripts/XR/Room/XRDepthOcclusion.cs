using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Environment depth occlusion (XR-PRD 5.6): the player's real hands, arms and furniture hide the board. It hands the
    /// occlusion material templates to <see cref="XROcclusionMaterials"/>, switches the headset's depth on once the
    /// spatial-data permission is granted, and publishes the two values the occlusion shaders need besides AR
    /// Foundation's own: world to tracking space, and how much depth fuzz to forgive.
    /// </summary>
    public sealed class XRDepthOcclusion : MonoBehaviour
    {
        private static readonly int WorldToTrackablesId = Shader.PropertyToID("_XRWorldToTrackables");
        private static readonly int BiasId = Shader.PropertyToID("_XROcclusionBias");

        [SerializeField] private AROcclusionManager occlusion;
        [SerializeField] private ARShaderOcclusion shaderOcclusion;
        [SerializeField] private XROrigin origin;
        [SerializeField] private Material occludedLit;
        [SerializeField] private Material occludedText;

        [Tooltip("How far behind a real surface a virtual one may be and still show, in metres. Keeps the depth map's fuzz " +
                 "from nibbling at the board where a hand rests on it.")]
        [SerializeField] private float bias = 0.03f;

        private void Awake()
        {
            // Before any Start, so every board material is made from the occlusion templates from the first.
            XROcclusionMaterials.Configure(occludedLit, occludedText);
            Shader.SetGlobalMatrix(WorldToTrackablesId, Matrix4x4.identity);
            Shader.SetGlobalFloat(BiasId, bias);
            if (occlusion != null) occlusion.enabled = false;
            if (shaderOcclusion != null) shaderOcclusion.enabled = false;
        }

        private async void Start()
        {
            // Depth needs the spatial-data permission that board placement asks for (5.2); wait for it.
            while (this != null && !XRScenePermission.IsGranted) await Awaitable.WaitForSecondsAsync(0.5f);
            if (this == null) return;
            if (occlusion != null) occlusion.enabled = true;
            if (shaderOcclusion != null) shaderOcclusion.enabled = true;
            Debug.Log("[XR occlusion] Environment depth on.");
        }

        private void LateUpdate()
        {
            // The depth matrices are in tracking space, which is the trackables' parent; shaders work in world space.
            var toTrackables = origin != null && origin.TrackablesParent != null ? origin.TrackablesParent.worldToLocalMatrix : Matrix4x4.identity;
            Shader.SetGlobalMatrix(WorldToTrackablesId, toTrackables);
            Shader.SetGlobalFloat(BiasId, bias);
        }
    }
}
