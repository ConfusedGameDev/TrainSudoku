using TrainSudoku.XR;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Opens the wrist menu's panel up and in from the wrist, toward the middle of the view, rather than straight above
    /// the roundel, so the arm wearing it does not cover it.
    /// </summary>
    /// <remarks>
    /// <b>Why</b> (2026-10-04, headset: "the wrist pause menu is still hard to press … move it so that it appears
    /// offsetted from the wrist so that we do not cover it with the user's hand or arm"). <c>XRWristMenu.PlacePanel</c>
    /// stands the panel's bottom edge 6 cm above the roundel. On the Quest the hand there is a rendered mesh drawn
    /// behind the panel; on Vision Pro it is the player's real hand and forearm, which visionOS composites over
    /// everything, so the panel sits right where the wearing arm hides it.
    ///
    /// <b>Only the panel moves, not the roundel.</b> The panel is placed once, when it opens, and its fingertip
    /// presses are then read against wherever its transform stands — so moving it after <c>PlacePanel</c> moves the
    /// presses with it. The roundel is re-placed on the wrist every frame inside <c>XRWristMenu.LateUpdate</c>,
    /// immediately before its presses are read, so moving it from here would show it in one place and press it in
    /// another. Offsetting the roundel needs its placement constants exposed: a seam commit on <c>feat/MetaXR</c>
    /// (VisionOS-PRD 2).
    ///
    /// Runs late (<see cref="DefaultExecutionOrderAttribute"/>) so it sees the panel after <c>PlacePanel</c> in the
    /// same frame, and recognises a fresh placement as the panel standing anywhere other than where this last put it.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class VisionOSWristPanelOffset : MonoBehaviour
    {
        const string k_Tag = "[AVP wrist]";

        /// <summary>Further up than the Quest's 6 cm, and this far in toward the middle of the view, in metres.</summary>
        const float k_Up = 0.10f;
        const float k_Inward = 0.12f;

        /// <summary>The panel's bottom edge above the side button's centre (VisionOSWristButton), in metres.</summary>
        const float k_AboveButton = 0.05f;

        /// <summary>The panel's height for facing it to the eyes; XRWristMenu.PanelMetres is its width, about its height.</summary>
        const float k_PanelHalfHeight = 0.1f;

        XRWristMenu m_Menu;
        Transform m_Panel;
        UIDocument m_Roundel;
        Vector3 m_Placed;
        bool m_HasPlaced;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSWristPanelOffset>() != null) return;
            var host = new GameObject(nameof(VisionOSWristPanelOffset));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSWristPanelOffset>();
        }
#endif

        void LateUpdate()
        {
            if (!Find()) return;
            if (!m_Menu.IsOpen)
            {
                m_HasPlaced = false;
                return;
            }

            if (m_HasPlaced && (m_Panel.position - m_Placed).sqrMagnitude < 1e-8f) return;
            Offset();
        }

        bool Find()
        {
            if (m_Panel != null) return true;
            if (m_Menu == null) m_Menu = FindAnyObjectByType<XRWristMenu>();
            if (m_Menu == null) return false;
            foreach (var document in m_Menu.GetComponentsInChildren<UIDocument>(true))
            {
                if (document.name == "Panel") m_Panel = document.transform;
                else if (document.name == "Roundel") m_Roundel = document;
            }

            return m_Panel != null;
        }

        void Offset()
        {
            m_HasPlaced = true;
            m_Placed = m_Panel.position;

            // Only a panel opened over the wrist: one opened ahead of the eyes has no arm under it.
            var head = Camera.main != null ? Camera.main.transform : null;
            var root = m_Roundel != null ? m_Roundel.rootVisualElement : null;
            if (head == null || root == null || root.resolvedStyle.visibility != Visibility.Visible) return;

            Vector3 bottom;
            if (VisionOSWristButton.Shown)
            {
                // Straight above the side button, which already stands clear of the hand.
                bottom = VisionOSWristButton.Centre + Vector3.up * k_AboveButton;
            }
            else
            {
                bottom = m_Panel.position;
                // In toward the middle of the view: across the head's right axis, on whichever side the wrist is.
                var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
                var side = Vector3.Dot(bottom - head.position, right) >= 0f ? 1f : -1f;
                bottom += Vector3.up * k_Up - right * side * k_Inward;
            }

            // Faced to the eyes the way PlacePanel faces it.
            var away = bottom + Vector3.up * k_PanelHalfHeight - head.position;
            m_Panel.SetPositionAndRotation(bottom, Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : Vector3.forward, Vector3.up));
            m_Placed = m_Panel.position;
            Debug.Log($"{k_Tag} Panel opened " + (VisionOSWristButton.Shown ? "above the side button." : $"{k_Up * 100f:F0} cm up and {k_Inward * 100f:F0} cm in from the wrist."));
        }
    }
}
