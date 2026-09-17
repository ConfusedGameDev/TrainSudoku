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

        [Header("Line colour")]
        [Tooltip("The body material on the car models - M_TrainColor. It is cloned and tinted with the active line's colour; leave it empty and the cars keep the colour they were authored in.")]
        [SerializeField] private Material bodyMaterial = null;

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

        /// <summary>
        /// The material the line colour replaces on every car. Matched by <b>reference</b> rather than by slot
        /// index, so it finds the body panels on the locomotive and on a wagon alike and survives the models being
        /// re-imported in another order — a car is three renderers and the body is not the only slot on any of them.
        /// Null means an untinted train, which is a supported state.
        /// </summary>
        public Material BodyMaterial => bodyMaterial;

        /// <summary>The face for the destination plate, or null for a train without one (D13).</summary>
        public Font SignageFont => signageFont;

        /// <summary>What follows the station name on the plate.</summary>
        public string DestinationSuffix => destinationSuffix;
    }
}
