using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Art hooks for the XR board and train. The fields carry the same names as the phone's <c>TrackAssets</c> and
    /// <c>TrainAssets</c>, so <c>XRFoundationSetup</c> can seed this asset from theirs; after that it is XR's own and
    /// is re-tuned independently (X24). The meshes and models themselves are the phone's train kit, used in place.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku XR/Board Assets", fileName = "XRBoardAssets")]
    public sealed class XRBoardAssets : ScriptableObject
    {
        [Header("Track")]
        [Tooltip("Rails, one cell long with their length along local Z. Must be Read/Write enabled.")]
        [SerializeField] private Mesh segmentMesh = null;

        [Tooltip("Cross tie repeated along the track. Leave empty for rails only. Must be Read/Write enabled.")]
        [SerializeField] private Mesh sleeperMesh = null;

        [Range(0, 24)] [SerializeField] private int sleepersPerCell = 6;
        [SerializeField] private bool evenSleeperSpacing = true;
        [Range(1, 48)] [SerializeField] private int curveSlices = 16;
        [Range(0.2f, 1.5f)] [SerializeField] private float widthScale = 1f;
        [Range(0.2f, 1.5f)] [SerializeField] private float sleeperWidthScale = 0.7f;

        [Tooltip("Material for player pieces. Leave empty for the placeholder material.")]
        [SerializeField] private Material trackMaterial = null;

        [Tooltip("Material for fixed pieces. Leave empty for flat ink.")]
        [SerializeField] private Material fixedTrackMaterial = null;

        [SerializeField] private float verticalOffset = 0f;

        [Header("Tunnels")]
        [Tooltip("The tunnel the train runs through, modelled along local Z. Leave empty for the procedural arch portal.")]
        [SerializeField] private GameObject tunnelModel = null;

        [Tooltip("How far the tunnel runs out from the board edge, in cells. Capped at BoardLayout.Padding.")]
        [Range(0.2f, 1f)] [SerializeField] private float tunnelLength = 0.95f;

        [SerializeField] private Vector2 tunnelBore = new Vector2(0.8f, 0.8f);

        [Header("Environment art (D13)")]
        [Tooltip("Face for the Japanese platform decals and the destination plate. Without one they are skipped.")]
        [SerializeField] private Font signageFont = null;

        [SerializeField] private string platformWarning = "きけん";   // きけん, "danger"
        [SerializeField] private string destinationSuffix = "行";            // 行, "bound for"

        [Header("Train")]
        [SerializeField] private GameObject locomotive = null;
        [SerializeField] private GameObject[] wagons = new GameObject[2];
        [SerializeField] private float carSpacing = 0.9f;
        [SerializeField] private float speedCellsPerSecond = 2.5f;
        [SerializeField] private float modelYawOffset = 0f;
        [SerializeField] private float modelScale = 1f;
        [SerializeField] private float heightOffset = 0f;

        public GameObject TunnelModel => tunnelModel;
        public float TunnelLength => tunnelLength;
        public Vector2 TunnelBore => tunnelBore;
        public float VerticalOffset => verticalOffset;
        public Font SignageFont => signageFont;
        public string PlatformWarning => platformWarning;
        public string DestinationSuffix => destinationSuffix;
        public Material TrackMaterial => trackMaterial != null ? trackMaterial : XRBoardMaterials.Track;
        public Material FixedTrackMaterial => fixedTrackMaterial != null ? fixedTrackMaterial : XRBoardMaterials.FixedTrack;

        public GameObject Locomotive => locomotive;
        public GameObject[] Wagons => wagons ?? new GameObject[0];
        public int CarCount => 1 + Wagons.Length;
        public float CarSpacing => carSpacing;
        public float SpeedCellsPerSecond => speedCellsPerSecond;
        public float ModelYawOffset => modelYawOffset;
        public float ModelScale => modelScale <= 0f ? 1f : modelScale;
        public float HeightOffset => heightOffset;

        /// <summary>The meshes and shaping the bender builds pieces from.</summary>
        public TrackMeshProfile ResolveProfile()
        {
            var rails = Readable(segmentMesh);
            // The placeholder already carries its own sleepers, so pairing it with one would lay them down twice.
            if (rails == null)
                return new TrackMeshProfile(ProceduralTrackMesh.Straight(), null, sleepersPerCell, evenSleeperSpacing, curveSlices, widthScale, sleeperWidthScale);
            return new TrackMeshProfile(rails, Readable(sleeperMesh), sleepersPerCell, evenSleeperSpacing, curveSlices, widthScale, sleeperWidthScale);
        }

        /// <summary>The profile for a board with no assets assigned.</summary>
        public static TrackMeshProfile PlaceholderProfile() =>
            new TrackMeshProfile(ProceduralTrackMesh.Straight(), null, 4, true, 16, 1f);

        private Mesh Readable(Mesh mesh)
        {
            if (mesh == null) return null;
            if (mesh.isReadable) return mesh;
            Debug.LogWarning($"XRBoardAssets: '{mesh.name}' is not Read/Write enabled; ignoring it.", this);
            return null;
        }
    }
}
