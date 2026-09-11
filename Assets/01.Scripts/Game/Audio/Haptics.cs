using System.Runtime.InteropServices;

namespace TrainSudoku.Game
{
    /// <summary>The Taptic Engine feels, in the order the native bridge expects them.</summary>
    public enum HapticFeel
    {
        None = -1,

        // UIImpactFeedbackGenerator styles.
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Soft = 3,
        Rigid = 4,

        // UISelectionFeedbackGenerator: the faint tick a picker gives as a value changes.
        Selection = 5,

        // UINotificationFeedbackGenerator types.
        Success = 6,
        Warning = 7,
        Failure = 8,
    }

    /// <summary>
    /// iOS haptics, played through <c>Assets/00.Plugins/iOS/TrainSudokuHaptics.mm</c>. Every call is a no-op off
    /// device, on a phone with no Taptic Engine and when the player has System Haptics off, so callers never check.
    /// Haptics are decoration: nothing may be legible only through them.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Off switch for the whole feature; a settings toggle would drive this.</summary>
        public static bool Enabled = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _TSHapticsPrepare();
        [DllImport("__Internal")] private static extern void _TSHapticsImpact(int style);
        [DllImport("__Internal")] private static extern void _TSHapticsSelection();
        [DllImport("__Internal")] private static extern void _TSHapticsNotification(int type);
#endif

        /// <summary>Warms the engine so the first tap of a session is not late. Cheap; safe to call repeatedly.</summary>
        public static void Prepare()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            _TSHapticsPrepare();
#endif
        }

        public static void Play(HapticFeel feel)
        {
            if (!Enabled || feel == HapticFeel.None) return;
#if UNITY_IOS && !UNITY_EDITOR
            switch (feel)
            {
                case HapticFeel.Selection:
                    _TSHapticsSelection();
                    break;
                case HapticFeel.Success:
                    _TSHapticsNotification(0);
                    break;
                case HapticFeel.Warning:
                    _TSHapticsNotification(1);
                    break;
                case HapticFeel.Failure:
                    _TSHapticsNotification(2);
                    break;
                default:
                    _TSHapticsImpact((int)feel);
                    break;
            }
#endif
        }

        /// <summary>Plays the feel that goes with a sound cue. This is the whole mapping; tune it here.</summary>
        public static void Play(AudioCue cue) => Play(FeelOf(cue));

        public static HapticFeel FeelOf(AudioCue cue) => cue switch
        {
            // Board.
            AudioCue.Place => HapticFeel.Light,
            AudioCue.Erase => HapticFeel.Medium,
            AudioCue.Error => HapticFeel.Failure,
            AudioCue.FinalPiece => HapticFeel.Success,
            AudioCue.TrainStart => HapticFeel.Soft,
            AudioCue.CellSelect => HapticFeel.Selection,
            AudioCue.SideChosen => HapticFeel.Selection,
            AudioCue.LineCleared => HapticFeel.Light,

            // The loops. A looping cue is started once, so this would fire once rather than every frame — but a loop
            // that buzzes at all is wrong, and AudioCuePlayer.PlayLoop does not call Haptics anyway.
            AudioCue.TrainMoving => HapticFeel.None,
            AudioCue.EraseHold => HapticFeel.None,

            AudioCue.WinFanfareOne => HapticFeel.Success,
            AudioCue.WinFanfareTwo => HapticFeel.Success,
            AudioCue.WinFanfareThree => HapticFeel.Success,

            // Station UI: the faintest tick there is, so a menu never buzzes.
            AudioCue.UiClick => HapticFeel.Selection,
            AudioCue.UiBack => HapticFeel.Selection,
            AudioCue.UiConfirm => HapticFeel.Light,
            AudioCue.MapOpen => HapticFeel.Selection,
            AudioCue.StationSelect => HapticFeel.Selection,
            AudioCue.StarAwarded => HapticFeel.Light,
            AudioCue.LineUnlocked => HapticFeel.Success,

            _ => HapticFeel.None,
        };
    }
}
