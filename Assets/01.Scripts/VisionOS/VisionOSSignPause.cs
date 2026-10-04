using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using TrainSudoku.XR;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// The pause, on the station signboard: a PAUSE button while a level is in play, and the pause menu's choices on
    /// the sign's pause view. The player never has to reach for the wrist to stop, restart or leave a level.
    /// </summary>
    /// <remarks>
    /// <b>Why</b> (2026-10-04, headset). The wrist roundel is the Quest's only way into the pause, and on Vision Pro it
    /// does not work well: visionOS composites the player's real hands over the frame, so the finger reaching for the
    /// roundel hides it, and a 4 cm target on a moving wrist is hard to hit. Pulling the roundel's depth toward the eye
    /// (so the compositor would draw it over the hand) was tried and changed nothing on the headset. The signboard
    /// stands still behind the board, already takes fingertip presses, and is where the player is looking.
    ///
    /// <b>What goes where.</b> In play, PAUSE at the right of the station head, where RESUME sits on the pause view. In
    /// the pause, the sign already offers RESUME; under its LED strip this replaces the line "Retry, the line map and
    /// settings are on the wrist menu" with RETRY, LINE MAP and RE-PLACE BOARD — the wrist menu's pause entries, which
    /// the Quest opens together with the pause and a sign press does not (the headset report: "we are missing the
    /// pause menu"). Settings and the height nudge stay on the wrist; nothing here removes anything from it.
    ///
    /// <b>How, without editing the sign.</b> <c>Assets/01.Scripts/XR/</c> is not edited on this branch (VisionOS-PRD
    /// 2). So this adds buttons to the card the sign has just built and presses them with its own
    /// <see cref="XRPanelTouch"/> against the sign's panel, the same way the sign presses its own. The sign clears and
    /// rebuilds its card on every view (<c>Begin</c>), which detaches what was added; it is put back whenever the state
    /// calls for it and the buttons are no longer on a panel. The actions are the ones <c>XRGame</c> runs for the wrist
    /// entries, through the same public calls. If the Quest ever wants the same, it belongs in <c>ShowStation</c> and
    /// <c>ShowPaused</c> as a seam commit on <c>feat/MetaXR</c>, and this component goes.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VisionOSSignPause : MonoBehaviour
    {
        const string k_Tag = "[AVP pause]";
        const float k_PixelsPerUnit = 100f;   // XRSignboard.PixelsPerUnit, private there

        /// <summary>The start of the sign's pause footer, which the menu's buttons replace.</summary>
        const string k_WristHint = "Retry, the line map";

        XRGame m_Game;
        XRSignboard m_Sign;
        XRBoardPlacement m_Placement;
        UIDocument m_Document;
        XRSignageAssets m_Assets;

        /// <summary>What was added to the card on show, and the state it was added for.</summary>
        readonly List<Button> m_Buttons = new List<Button>();
        GameState m_BuiltFor;
        readonly XRPanelTouch m_Touch = new XRPanelTouch("sign pause", k_PixelsPerUnit);

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSSignPause>() != null) return;
            var host = new GameObject(nameof(VisionOSSignPause));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSSignPause>();
        }
#endif

        void LateUpdate()
        {
            if (!Find()) return;

            var state = m_Game.Flow != null ? m_Game.Flow.State : GameState.MainMenu;
            var offered = (state == GameState.Play || state == GameState.Pause) && m_Sign.isActiveAndEnabled;
            if (!offered)
            {
                m_Touch.Disarm();
                return;
            }

            if (m_Buttons.Count == 0 || m_Buttons[0].panel == null || m_BuiltFor != state) Attach(state);
            if (m_Buttons.Count > 0) m_Touch.Update(m_Document.transform, m_Document);
        }

        bool Find()
        {
            if (m_Document != null && m_Game != null) return true;
            if (m_Game == null) m_Game = FindAnyObjectByType<XRGame>();
            if (m_Sign == null) m_Sign = FindAnyObjectByType<XRSignboard>(FindObjectsInactive.Include);
            if (m_Game == null || m_Sign == null) return false;
            m_Placement = FindAnyObjectByType<XRBoardPlacement>();
            m_Document = m_Sign.GetComponentInChildren<UIDocument>(true);
            // For the heading font; the buttons are still built, in the default font, without it.
            var assets = Resources.FindObjectsOfTypeAll<XRSignageAssets>();
            m_Assets = assets.Length > 0 ? assets[0] : null;
            if (m_Document != null) Debug.Log($"{k_Tag} Found the signboard; the pause and its menu go on it.");
            return m_Document != null;
        }

        void Attach(GameState state)
        {
            m_Buttons.Clear();
            m_Touch.Clear();
            m_BuiltFor = state;

            var root = m_Document.rootVisualElement;
            // The card is the root's only child and the station head is the card's first (XRSignboard.Begin, StationHead).
            if (root == null || root.childCount == 0) return;
            var card = root[0];
            if (card.childCount == 0) return;

            if (state == GameState.Play)
            {
                var head = card[0];
                head.Add(Spacer());
                head.Add(Add("PAUSE", Pause));
                return;
            }

            // The pause view: swap the wrist hint for the menu itself.
            var last = card[card.childCount - 1];
            if (last is Label hint && hint.text != null && hint.text.StartsWith(k_WristHint, StringComparison.Ordinal))
                card.Remove(hint);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 22f;
            card.Add(row);
            row.Add(Add("RETRY", Retry));
            row.Add(Add("LINE MAP", LineMap));
            if (m_Placement != null) row.Add(Add("RE-PLACE BOARD", Replace));
            // A size down from the sign's 50 px buttons: three of those overrun the card's 1164 px inside width.
            foreach (var child in row.Children()) child.style.fontSize = 42f;
            // The sign's own buttons carry a left margin for the gap between them; the first in a centred row does not need it.
            row[0].style.marginLeft = 0f;
        }

        Button Add(string text, Action clicked)
        {
            var button = XRSignageUi.Button(m_Assets, text, clicked, false);
            m_Buttons.Add(button);
            m_Touch.Add(button, clicked);
            return button;
        }

        static VisualElement Spacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1f;
            return spacer;
        }

        // ------------------------------------------------------------------ what XRGame runs for the wrist entries

        void Pause() => When(GameState.Play, "Paused", flow => flow.PauseGame());

        void Retry() => When(GameState.Pause, "Retry", flow => flow.Retry());

        void LineMap() => When(GameState.Pause, "Line map", flow => flow.ShowLevelSelect());

        /// <summary>
        /// <c>XRGame.OnReplaceBoard</c>, minus what does not apply here: the menu it closes is the wrist's, and the
        /// game is already paused. <see cref="VisionOSResumeOnPlace"/> resumes play once the board lands.
        /// </summary>
        void Replace() => When(GameState.Pause, "Re-place board", _ => m_Placement.Replace());

        void When(GameState state, string what, Action<GameFlow> action)
        {
            var flow = m_Game != null ? m_Game.Flow : null;
            if (flow == null || flow.State != state) return;
            Debug.Log($"{k_Tag} {what} from the signboard.");
            action(flow);
        }
    }
}
