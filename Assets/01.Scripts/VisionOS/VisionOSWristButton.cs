using TrainSudoku.Core;
using TrainSudoku.XR;
using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// The wrist menu's button, floating beside the hand instead of on the back of the wrist, where the player's own
    /// hand and arm cover it.
    /// </summary>
    /// <remarks>
    /// <b>Why</b> (2026-10-04, headset: "I do not want it on top of the hand, I want it to be floating to the left or
    /// right of the hand so that it is always visible"). <c>XRWristMenu</c> wears its roundel 3 cm out of the back of
    /// the wrist. On the Quest the hand under it is a rendered mesh drawn behind it; on Vision Pro it is the real hand,
    /// which visionOS composites over everything, so the fingers and the reaching hand hide it. Pulling its depth
    /// toward the eye did not help (the compositor's hands win regardless).
    ///
    /// <b>How, without editing the wrist menu.</b> The roundel is placed and pressed inside one
    /// <c>XRWristMenu.LateUpdate</c>, so it cannot be moved from outside (VisionOSWristPanelOffset). Instead this is a
    /// second roundel, owned here: shown exactly when the game's own would be (the same watch-check, read off its
    /// root's visibility), standing <see cref="k_Side"/> to the outer side of it — right of the right hand, left of the
    /// left — and faced to the eyes. (The first version stood it on the inner side, so the pressing hand would not
    /// cross the wearing arm; on the headset that hand still clipped it on the way across.) The game's roundel is kept off screen
    /// through <see cref="VisionOSPanelSurface.Suppress"/>. A press does what <c>XRGame.OnWristToggled</c> does,
    /// through the public calls it uses: <see cref="WristMenuPlan.Toggle"/>, then pause and open, open, or close.
    /// Its one difference is the line map's title, which reads LINE MAP rather than the line's name (the game keeps its
    /// network private).
    ///
    /// The game's roundel still takes a press where it always was, unseen. That is harmless — a fingertip on the back
    /// of the wrist is no accident — and leaving it alone keeps every Quest file untouched.
    /// </remarks>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class VisionOSWristButton : MonoBehaviour
    {
        const string k_Tag = "[AVP wrist]";
        const float k_PixelsPerUnit = 100f;
        const float k_Pixels = 160f;

        /// <summary>A little larger than the Quest's 3 cm: it no longer rides on the wrist, so it can afford to be.</summary>
        const float k_Metres = 0.04f;

        /// <summary>How far beside the wrist, on its outer side, and how far up, in metres.</summary>
        const float k_Side = 0.11f;
        const float k_Up = 0.02f;

        /// <summary>How quickly it follows the wrist: steadier than the joint, which shakes, but not lagging.</summary>
        const float k_Follow = 14f;

        const float k_SettleSeconds = 0.3f;
        const float k_Cooldown = 1f;

        /// <summary>Where the button stands while shown; the panel opens above it (VisionOSWristPanelOffset).</summary>
        public static bool Shown { get; private set; }
        public static Vector3 Centre { get; private set; }

        XRGame m_Game;
        XRWristMenu m_Menu;
        UIDocument m_GameRoundel;
        UIDocument m_Document;
        Button m_Button;
        readonly XRPanelTouch m_Touch = new XRPanelTouch("wrist side button", k_PixelsPerUnit, 0.01f, 0.02f);
        float m_ShownAt;
        float m_LastPress = float.NegativeInfinity;
        bool m_Placed;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSWristButton>() != null) return;
            var host = new GameObject(nameof(VisionOSWristButton));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSWristButton>();
        }
#endif

        void LateUpdate()
        {
            if (!Find()) return;
            if (m_Button == null && !Build()) return;

            var root = m_GameRoundel.rootVisualElement;
            var wanted = root != null && root.style.visibility.value == Visibility.Visible && m_Menu.Offered;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (wanted && head != null) Place(head);

            if (wanted != Shown)
            {
                Shown = wanted;
                m_Document.rootVisualElement.style.visibility = wanted ? Visibility.Visible : Visibility.Hidden;
                m_ShownAt = Time.unscaledTime;
                m_Touch.Disarm();
                if (!wanted) m_Placed = false;
            }

            if (!Shown) return;
            m_Touch.IgnoredHand = m_Menu.WristHand == Hand.Left ? InteractorHandedness.Left : InteractorHandedness.Right;
            m_Touch.Update(m_Document.transform, m_Document, Time.unscaledTime - m_ShownAt >= k_SettleSeconds);
        }

        bool Find()
        {
            if (m_GameRoundel != null && m_Game != null) return true;
            if (m_Game == null) m_Game = FindAnyObjectByType<XRGame>();
            if (m_Game == null || m_Game.Menu == null) return false;
            m_Menu = m_Game.Menu;
            foreach (var document in m_Menu.GetComponentsInChildren<UIDocument>(true))
                if (document.name == "Roundel") m_GameRoundel = document;
            if (m_GameRoundel == null) return false;
            VisionOSPanelSurface.Suppress(m_GameRoundel);
            return true;
        }

        bool Build()
        {
            var assets = Resources.FindObjectsOfTypeAll<XRSignageAssets>();
            if (assets.Length == 0 || assets[0].PanelSettings == null) return false;

            if (m_Document == null)
            {
                var go = new GameObject("Wrist Side Button");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * (k_Metres / (k_Pixels / k_PixelsPerUnit));
                m_Document = go.AddComponent<UIDocument>();
                m_Document.panelSettings = assets[0].PanelSettings;
                m_Document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
                m_Document.worldSpaceSize = new Vector2(k_Pixels, k_Pixels);
                m_Document.pivot = Pivot.BottomCenter;
            }

            // Not attached to its panel until a frame after it is made.
            var root = m_Document.rootVisualElement;
            if (root == null) return false;

            // The game's roundel, drawn the same way (XRWristMenu.BuildRoundel): paper, an ink ring, the app mark.
            m_Button = new Button(Press) { text = "" };
            m_Button.style.flexGrow = 1;
            m_Button.style.marginLeft = m_Button.style.marginRight = m_Button.style.marginTop = m_Button.style.marginBottom = 0f;
            m_Button.style.paddingLeft = m_Button.style.paddingRight = m_Button.style.paddingTop = m_Button.style.paddingBottom = 0f;
            m_Button.style.backgroundColor = XRPalette.Paper;
            m_Button.style.borderTopWidth = m_Button.style.borderBottomWidth = m_Button.style.borderLeftWidth = m_Button.style.borderRightWidth = 10f;
            m_Button.style.borderTopColor = m_Button.style.borderBottomColor = m_Button.style.borderLeftColor = m_Button.style.borderRightColor = XRPalette.Ink;
            var radius = k_Pixels / 2f;
            m_Button.style.borderTopLeftRadius = m_Button.style.borderTopRightRadius = radius;
            m_Button.style.borderBottomLeftRadius = m_Button.style.borderBottomRightRadius = radius;
            if (assets[0].Mark != null) m_Button.style.backgroundImage = assets[0].Mark;
            root.Clear();
            root.Add(m_Button);
            root.style.visibility = Visibility.Hidden;
            m_Touch.Clear();
            m_Touch.Add(m_Button, Press);
            Debug.Log($"{k_Tag} Side button built; the wrist roundel is hidden in its favour.");
            return true;
        }

        /// <summary>Beside the game's roundel, on the hand's outer side, upright and facing the eyes.</summary>
        void Place(Transform head)
        {
            // The game's roundel document is pivoted at its bottom centre; its centre is half its height up.
            var wrist = m_GameRoundel.transform.TransformPoint(0f, k_Pixels / k_PixelsPerUnit / 2f, 0f);
            var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            var side = Vector3.Dot(wrist - head.position, right) >= 0f ? 1f : -1f;
            // Outward: right of the right hand, left of the left one (2026-10-04, headset). Inward, the pressing hand
            // still clipped it on its way across.
            var target = wrist + right * side * k_Side + Vector3.up * k_Up;

            var centre = m_Placed ? Vector3.Lerp(Centre, target, 1f - Mathf.Exp(-k_Follow * Time.unscaledDeltaTime)) : target;
            m_Placed = true;
            Centre = centre;

            // The player's side of a document is −z, so +z points away from the eyes.
            var away = centre - head.position;
            var rotation = Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : head.forward, Vector3.up);
            m_Document.transform.SetPositionAndRotation(centre - rotation * Vector3.up * (k_Metres / 2f), rotation);
        }

        /// <summary>XRGame.OnWristToggled, through the public calls it makes.</summary>
        void Press()
        {
            if (Time.unscaledTime - m_LastPress < k_Cooldown) return;
            m_LastPress = Time.unscaledTime;

            var flow = m_Game.Flow;
            if (flow == null) return;
            Debug.Log($"{k_Tag} Side button pressed in {flow.State}.");
            switch (WristMenuPlan.Toggle(flow.State, m_Menu.IsOpen))
            {
                case WristToggle.OpenAndPause:
                    flow.PauseGame();
                    Open(flow);
                    break;
                case WristToggle.Open:
                    Open(flow);
                    break;
                case WristToggle.Close:
                    m_Menu.Close();
                    break;
            }
        }

        void Open(GameFlow flow)
        {
            var title = flow.State == GameState.Pause ? "PAUSED" : flow.State == GameState.LevelSelect ? "LINE MAP" : "NETWORK";
            m_Menu.Open(WristMenuPlan.Items(flow.State), title);
        }
    }
}
