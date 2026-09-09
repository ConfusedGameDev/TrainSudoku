using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Art hooks for the track (PRD section 7). Assign the kit rails (spline-track.fbx, Read/Write enabled, modelled
    /// along +Z and one cell long) and, separately, the cross tie repeated along them (spline-segment.fbx, modelled
    /// across the track with its depth along Z). While they are empty the board falls back to a procedural
    /// placeholder track that runs through the same bender.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Track Assets", fileName = "TrackAssets")]
    public sealed class TrackAssets : ScriptableObject
    {
        [Tooltip("Rails, one cell long with their length along local Z. Must be Read/Write enabled.")]
        [SerializeField] private Mesh segmentMesh = null;

        [Tooltip("Cross tie repeated along the track, modelled across it. Leave empty for rails only. Must be Read/Write enabled.")]
        [SerializeField] private Mesh sleeperMesh = null;

        [Tooltip("How many sleepers a full one-cell straight gets. Tiles are bent to join into a continuous deck, so a count below about 6 leaves visible seams on curves; lower it deliberately for a ladder of separate ties.")]
        [Range(0, 24)] [SerializeField] private int sleepersPerCell = 6;

        [Tooltip("A curve is only pi/4 of a cell long. On, it gets proportionally fewer sleepers so the spacing stays even; off, every piece gets the same count and curves pack them tighter.")]
        [SerializeField] private bool evenSleeperSpacing = true;

        [Tooltip("Rings a curved piece is cut into before bending. Higher is smoother; straights are never cut.")]
        [Range(1, 48)] [SerializeField] private int curveSlices = 16;

        [Tooltip("Narrows the track across its direction of travel. Curves have a radius of only 0.5, so wide art pinches on the inside.")]
        [Range(0.2f, 1.5f)] [SerializeField] private float widthScale = 1f;

        [Tooltip("Extra narrowing for sleepers only. The kit tie is a full cell wide, which reaches the centre of a curve and piles up on the inside of turns.")]
        [Range(0.2f, 1.5f)] [SerializeField] private float sleeperWidthScale = 0.7f;

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

        /// <summary>The meshes and shaping the bender should build pieces from.</summary>
        public TrackMeshProfile ResolveProfile()
        {
            var rails = Readable(segmentMesh);
            // The placeholder already carries its own sleepers, so pairing it with one would lay them down twice.
            if (rails == null)
                return new TrackMeshProfile(ProceduralTrackMesh.Straight(), null, sleepersPerCell, evenSleeperSpacing, curveSlices, widthScale, sleeperWidthScale);
            return new TrackMeshProfile(rails, Readable(sleeperMesh), sleepersPerCell, evenSleeperSpacing, curveSlices, widthScale, sleeperWidthScale);
        }

        /// <summary>The default profile for a board with no <see cref="TrackAssets"/> assigned.</summary>
        public static TrackMeshProfile PlaceholderProfile() =>
            new TrackMeshProfile(ProceduralTrackMesh.Straight(), null, 4, true, 16, 1f);

        /// <summary>The mesh when it can be read, otherwise null with a warning: bending needs CPU access.</summary>
        private Mesh Readable(Mesh mesh)
        {
            if (mesh == null) return null;
            if (mesh.isReadable) return mesh;
            Debug.LogWarning($"TrackAssets: '{mesh.name}' is not Read/Write enabled; ignoring it.", this);
            return null;
        }
    }
}
