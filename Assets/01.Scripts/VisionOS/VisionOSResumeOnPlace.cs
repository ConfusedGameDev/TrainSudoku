using TrainSudoku.Core;
using TrainSudoku.XR;
using UnityEngine;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Resumes the game as soon as a board re-placed from the pause lands, so the player goes straight back to playing.
    /// </summary>
    /// <remarks>
    /// <c>XRGame.OnReplaceBoard</c> pauses a level in play and runs first placement again, and on the Quest the flow then
    /// waits on the pause for a RESUME press (XR-PRD 5.3, 6.4). On Vision Pro the player asked for the board's landing to
    /// be that press (2026-10-04): setting the board down is already a deliberate act, and a second trip to the wrist to
    /// resume read as the game ignoring it.
    ///
    /// <b>Only a pause is resumed.</b> <see cref="XRBoardPlacement.Placed"/> also fires on the first placement and on a
    /// board restored from its anchor, but those happen on the menus, never in the pause. It does not fire when the
    /// handle sets the board down, so moving the board mid-pause still leaves the pause alone.
    ///
    /// This sits here rather than in <c>XRGame</c> because <c>Assets/01.Scripts/XR/</c> is not edited on this branch
    /// (VisionOS-PRD 2). If the Quest wants the same behaviour, it belongs in <c>OnReplaceBoard</c> as a seam commit on
    /// <c>feat/MetaXR</c>, and this component goes.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VisionOSResumeOnPlace : MonoBehaviour
    {
        const string k_Tag = "[AVP resume]";

        XRGame m_Game;
        XRBoardPlacement m_Placement;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSResumeOnPlace>() != null) return;
            var host = new GameObject(nameof(VisionOSResumeOnPlace));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSResumeOnPlace>();
        }
#endif

        /// <summary>Looks for the game and its placement until both exist, then listens and stops looking.</summary>
        void Update()
        {
            if (m_Placement != null) return;
            if (m_Game == null) m_Game = FindAnyObjectByType<XRGame>();
            var placement = FindAnyObjectByType<XRBoardPlacement>();
            if (m_Game == null || placement == null) return;

            m_Placement = placement;
            m_Placement.Placed += OnPlaced;
            enabled = false;
            Debug.Log($"{k_Tag} Listening for the board to land.");
        }

        void OnDestroy()
        {
            if (m_Placement != null) m_Placement.Placed -= OnPlaced;
        }

        void OnPlaced()
        {
            var flow = m_Game != null ? m_Game.Flow : null;
            if (flow == null || flow.State != GameState.Pause) return;
            Debug.Log($"{k_Tag} Board re-placed; resuming.");
            flow.ResumeGame();
        }
    }
}
