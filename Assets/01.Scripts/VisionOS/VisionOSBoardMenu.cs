using TrainSudoku.Core;
using TrainSudoku.XR;
using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// On Vision Pro the game's menu (the wrist menu's panel) is opened from the signboard and stands to the left of the
    /// board, and the wrist roundel is switched off.
    /// </summary>
    /// <remarks>
    /// <b>Why</b> (2026-10-04, headset). Every way of wearing the menu on the wrist failed: on the back of the wrist the
    /// player's own hand covers it, because visionOS composites the real hands over everything (no depth trick beats
    /// it); a stand-in floating beside the hand, inner side and then outer side, still clipped and was hard to hit. The
    /// call was "ditch it": the signboard opens the menu instead (<see cref="VisionOSSignPause"/>: PAUSE in play, MENU on
    /// the maps), and the panel opens beside the board, in front of the player, where nothing covers it.
    ///
    /// <b>The roundel</b> is hidden (<see cref="VisionOSPanelSurface.Suppress"/>) and its root disabled, which is what
    /// <c>XRPanelTouch</c> reads (<c>enabledInHierarchy</c>) to skip a button — so an unseen press on the back of the
    /// wrist cannot open the menu either. <c>XRWristMenu</c> itself is untouched (VisionOS-PRD 2): the panel, its
    /// pages, Settings and every entry are the game's own.
    ///
    /// <b>The placement.</b> <c>XRWristMenu.Open</c> places the panel ahead of the eyes when no roundel shows. This runs
    /// after it (<see cref="DefaultExecutionOrderAttribute"/>), recognises a fresh placement as the panel standing
    /// anywhere other than where this last put it, and moves it to the player's left of whatever stands on the board
    /// root — the board in play, the map card on the maps — clear of its edge and faced to the eyes. Its presses are
    /// read against its transform, so they move with it.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class VisionOSBoardMenu : MonoBehaviour
    {
        const string k_Tag = "[AVP menu]";

        /// <summary>The panel's width (XRWristMenu.PanelMetres), the gap to the board's edge, and its lift, in metres.</summary>
        const float k_PanelWidth = 0.2f;
        const float k_Gap = 0.06f;
        const float k_Lift = 0.02f;

        static VisionOSBoardMenu s_Instance;

        XRGame m_Game;
        XRWristMenu m_Menu;
        XRBoardPlacement m_Placement;
        Transform m_Panel;
        UIDocument m_Roundel;
        Vector3 m_Placed;
        bool m_HasPlaced;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSBoardMenu>() != null) return;
            var host = new GameObject(nameof(VisionOSBoardMenu));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSBoardMenu>();
        }
#endif

        void Awake() => s_Instance = this;

        /// <summary>
        /// What the wrist roundel did (<c>XRGame.OnWristToggled</c>): in play, pause and then open the menu, since every
        /// change of state closes it; elsewhere, open it, or close it if it is open.
        /// </summary>
        public static void Toggle()
        {
            if (s_Instance == null || !s_Instance.Find()) return;
            var game = s_Instance.m_Game;
            var menu = s_Instance.m_Menu;
            var flow = game.Flow;
            if (flow == null) return;

            switch (WristMenuPlan.Toggle(flow.State, menu.IsOpen))
            {
                case WristToggle.OpenAndPause:
                    flow.PauseGame();
                    Open(menu, flow);
                    break;
                case WristToggle.Open:
                    Open(menu, flow);
                    break;
                case WristToggle.Close:
                    menu.Close();
                    break;
            }
        }

        /// <summary>XRGame.OpenMenu's titles, read on every redraw so they follow a change of language.</summary>
        static void Open(XRWristMenu menu, GameFlow flow)
        {
            // The line map's title is the localised "line map" rather than the line's name: the game keeps its network private.
            var key = flow.State == GameState.Pause ? XRKeys.PauseTitle
                : flow.State == GameState.LevelSelect ? XRKeys.LineMapTitle
                : XRKeys.MastheadNetwork;
            menu.Open(WristMenuPlan.Items(flow.State), () => XRText.Get(key));
            Debug.Log($"{k_Tag} Opened in {flow.State}.");
        }

        void LateUpdate()
        {
            if (!Find()) return;
            // Every frame, cheaply: the root is not attached on the first frames, and it is built into later.
            var roundel = m_Roundel != null ? m_Roundel.rootVisualElement : null;
            if (roundel != null && roundel.enabledSelf) roundel.SetEnabled(false);

            if (!m_Menu.IsOpen)
            {
                m_HasPlaced = false;
                return;
            }

            if (m_HasPlaced && (m_Panel.position - m_Placed).sqrMagnitude < 1e-8f) return;
            Place();
        }

        bool Find()
        {
            if (m_Panel != null && m_Game != null) return true;
            if (m_Game == null) m_Game = FindAnyObjectByType<XRGame>();
            if (m_Game == null || m_Game.Menu == null) return false;
            m_Menu = m_Game.Menu;
            m_Placement = FindAnyObjectByType<XRBoardPlacement>();
            foreach (var document in m_Menu.GetComponentsInChildren<UIDocument>(true))
            {
                if (document.name == "Panel") m_Panel = document.transform;
                else if (document.name == "Roundel") m_Roundel = document;
            }

            if (m_Panel == null) return false;
            if (m_Roundel != null)
            {
                VisionOSPanelSurface.Suppress(m_Roundel);
                Debug.Log($"{k_Tag} Wrist roundel switched off; the signboard opens the menu.");
            }

            return true;
        }

        /// <summary>To the player's left of what stands on the board root, upright and faced to the eyes.</summary>
        void Place()
        {
            m_HasPlaced = true;
            m_Placed = m_Panel.position;

            var head = Camera.main != null ? Camera.main.transform : null;
            var root = m_Placement != null && m_Placement.IsPlaced ? m_Placement.BoardRoot : null;
            if (head == null || root == null) return;

            // Half the footprint in cells: the board in play and paused, the map card otherwise.
            var level = m_Game.Display != null ? m_Game.Display.Level : null;
            var state = m_Game.Flow != null ? m_Game.Flow.State : GameState.MainMenu;
            var onBoard = level != null && (state == GameState.Play || state == GameState.Pause);
            var halfWidth = onBoard ? (float)BoardLayout.HalfWidth(level.Width) : XRPlatformMap.HalfWidth;
            var halfDepth = onBoard ? (float)BoardLayout.HalfDepth(level.Height) : XRPlatformMap.HalfWidth;
            var cell = root.lossyScale.x;

            var left = -Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            var across = Vector3.ProjectOnPlane(root.right, Vector3.up).normalized;
            var along = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            // How far the footprint reaches toward the player's left, whichever way the board is turned.
            var reach = (Mathf.Abs(Vector3.Dot(left, across)) * halfWidth + Mathf.Abs(Vector3.Dot(left, along)) * halfDepth) * cell;

            var bottom = root.position + left * (reach + k_Gap + k_PanelWidth / 2f) + Vector3.up * k_Lift;
            var away = bottom + Vector3.up * (k_PanelWidth / 2f) - head.position;
            m_Panel.SetPositionAndRotation(bottom, Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : Vector3.forward, Vector3.up));
            m_Placed = m_Panel.position;
            Debug.Log($"{k_Tag} Panel to the left of the {(onBoard ? "board" : "map")}, {reach * 100f:F0} cm from its centre.");
        }
    }
}
