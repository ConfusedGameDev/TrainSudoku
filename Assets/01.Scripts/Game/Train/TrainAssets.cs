using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Art hooks for the train (PRD sections 7 and 8). Assign the kit's locomotive and carriage prefabs; while they
    /// are empty the runner builds placeholder cars from primitives.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Train Assets", fileName = "TrainAssets")]
    public sealed class TrainAssets : ScriptableObject
    {
        [Tooltip("Locomotive model, e.g. train-locomotive-a.fbx. Leave empty for a placeholder.")]
        [SerializeField] private GameObject locomotive = null;

        [Tooltip("Carriage models in order behind the locomotive, e.g. two of the train-carriage-*.fbx files. Empty entries become placeholders.")]
        [SerializeField] private GameObject[] wagons = new GameObject[2];

        [Tooltip("Distance between car centres along the track, in cells.")]
        [SerializeField] private float carSpacing = 0.9f;

        [Tooltip("Travel speed in cells per second. 2.5 is the PRD's 0.4 s per cell.")]
        [SerializeField] private float speedCellsPerSecond = 2.5f;

        [Tooltip("Rotation about Y applied to the models so they face along the track (+Z is forward).")]
        [SerializeField] private float modelYawOffset = 0f;

        [Tooltip("Uniform scale applied to the models.")]
        [SerializeField] private float modelScale = 1f;

        [Tooltip("Height of the car origin above the track.")]
        [SerializeField] private float heightOffset = 0f;

        [Header("Destination plate (D13)")]
        [Tooltip("Face for the plate on the locomotive. This is environment art, so it carries Japanese in every locale - leave it empty and no plate is fitted.")]
        [SerializeField] private Font signageFont = null;

        [Tooltip("Follows the station name on the plate: 'bound for'. Untranslated, like the plate itself.")]
        [SerializeField] private string destinationSuffix = "\u884c";   // 行, "bound for"

        public GameObject Locomotive => locomotive;
        public GameObject[] Wagons => wagons ?? new GameObject[0];
        public int CarCount => 1 + Wagons.Length;
        public float CarSpacing => Mathf.Max(0.1f, carSpacing);
        public float SpeedCellsPerSecond => Mathf.Max(0.1f, speedCellsPerSecond);
        public float ModelYawOffset => modelYawOffset;
        public float ModelScale => modelScale <= 0f ? 1f : modelScale;
        public float HeightOffset => heightOffset;

        /// <summary>The face for the destination plate, or null for a train without one (D13).</summary>
        public Font SignageFont => signageFont;

        /// <summary>What follows the station name on the plate.</summary>
        public string DestinationSuffix => destinationSuffix;
    }
}
