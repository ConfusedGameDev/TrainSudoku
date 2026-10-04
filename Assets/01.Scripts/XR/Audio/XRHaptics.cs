using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Controller haptics (XR-PRD 9): a cue's buzz, sent to the hand that caused it through the rig's own
    /// <see cref="HapticImpulsePlayer"/>s. Bare hands have nothing to buzz, so this is decoration, and nothing may be
    /// legible only through it.
    /// </summary>
    /// <remarks>
    /// The cue table is the one source of feel: XRI's own <see cref="SimpleHapticFeedback"/> on the rig's interactors
    /// pulses on every hover and select, which here would double every grab, so <see cref="TakeOver"/> switches it off.
    /// </remarks>
    public static class XRHaptics
    {
        private static HapticImpulsePlayer _left;
        private static HapticImpulsePlayer _right;

        /// <summary>Finds the two controllers' players and silences XRI's own feedback. Cheap to call again.</summary>
        public static void TakeOver()
        {
            foreach (var feedback in Object.FindObjectsByType<SimpleHapticFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                feedback.enabled = false;

            _left = _right = null;
            foreach (var player in Object.FindObjectsByType<HapticImpulsePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switch (HandOf(player))
                {
                    case Hand.Left when _left == null:
                        _left = player;
                        break;
                    case Hand.Right when _right == null:
                        _right = player;
                        break;
                }
            }
        }

        /// <summary>Buzzes <paramref name="hand"/>'s controller, if it is holding one.</summary>
        public static void Play(Hand hand, float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f) return;
            var player = hand == Hand.Left ? _left : _right;
            if (player == null || !player.isActiveAndEnabled) return;
            player.SendHapticImpulse(amplitude, seconds);
        }

        /// <summary>A player's hand: the handedness of an interactor on its controller, else its controller's name.</summary>
        private static Hand? HandOf(HapticImpulsePlayer player)
        {
            var interactor = player.GetComponentInChildren<XRBaseInteractor>(true);
            if (interactor != null)
            {
                if (interactor.handedness == InteractorHandedness.Left) return Hand.Left;
                if (interactor.handedness == InteractorHandedness.Right) return Hand.Right;
            }

            for (var t = player.transform; t != null; t = t.parent)
            {
                if (t.name.Contains("Left")) return Hand.Left;
                if (t.name.Contains("Right")) return Hand.Right;
            }

            return null;
        }
    }
}
