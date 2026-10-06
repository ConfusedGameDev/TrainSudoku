using TrainSudoku.XR;
using UnityEngine;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// The shared XR code's platform settings, set to Vision Pro's values before the scene starts.
    /// </summary>
    /// <remarks>
    /// Each is a seam the shared code exposes with the Quest's value as its default (VisionOS-PRD 2, V-h):
    /// <list type="bullet">
    /// <item><b><see cref="XRBoardPlacement.MinCellSize"/> 6 cm</b>, up from 5. Shrunk to the Quest's floor, a piece on
    /// the board was too small to pinch off it reliably with Vision Pro hand tracking (2026-10-05). A board saved
    /// smaller comes back at the new floor: the restore clamps to it.</item>
    /// <item><b><see cref="XRSignboard.FollowsViewer"/> off.</b> The sign turns to face the player once per view and
    /// then holds still: turning every frame, it moved under the fingertip reaching for MENU or PAUSE.</item>
    /// </list>
    /// </remarks>
    public static class VisionOSTuning
    {
        public const float MinCellSize = 0.06f;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        public static void Apply()
        {
            XRBoardPlacement.MinCellSize = MinCellSize;
            XRSignboard.FollowsViewer = false;
            Debug.Log($"[AVP tuning] Board cells at least {MinCellSize * 100f:F0} cm; the signboard holds still.");
        }
    }
}
