using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Art hooks for the track (PRD section 7). Assign the kit's straight segment (spline-track.fbx or
    /// spline-segment.fbx, Read/Write enabled, modelled along +Z, one cell long) and its material; while they are
    /// empty the board falls back to a procedural placeholder track that runs through the same bender.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Track Assets", fileName = "TrackAssets")]
    public sealed class TrackAssets : ScriptableObject
    {
        [Tooltip("Straight one-cell track segment with its length along local Z. Must be Read/Write enabled.")]
        [SerializeField] private Mesh segmentMesh = null;

        [Tooltip("Material for player pieces. Leave empty for the placeholder material.")]
        [SerializeField] private Material trackMaterial = null;

        [Tooltip("Material for fixed pieces so they read as locked. Leave empty for the placeholder material.")]
        [SerializeField] private Material fixedTrackMaterial = null;

        [Tooltip("Lifts the track above the tile surface.")]
        [SerializeField] private float verticalOffset = 0f;

        public Mesh SegmentMesh => segmentMesh;
        public float VerticalOffset => verticalOffset;
        public Material TrackMaterial => trackMaterial != null ? trackMaterial : BoardMaterials.Track;
        public Material FixedTrackMaterial => fixedTrackMaterial != null ? fixedTrackMaterial : BoardMaterials.FixedTrack;

        /// <summary>The assigned segment when it can be read, otherwise the procedural placeholder.</summary>
        public Mesh ResolveSegment()
        {
            if (segmentMesh == null) return ProceduralTrackMesh.Straight();
            if (!segmentMesh.isReadable)
            {
                Debug.LogWarning($"TrackAssets: '{segmentMesh.name}' is not Read/Write enabled; using the placeholder track.", this);
                return ProceduralTrackMesh.Straight();
            }

            return segmentMesh;
        }
    }
}
