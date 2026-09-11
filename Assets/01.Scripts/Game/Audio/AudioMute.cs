using UnityEngine;
using UnityEngine.Audio;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The player-facing mute, remembered across launches. It cuts the mixer's master volume, or a global volume when
    /// there is no mixer to cut.
    /// </summary>
    /// <remarks>
    /// It deliberately does <b>not</b> use <c>AudioListener.pause</c>, which is what it used before the audio layer.
    /// That flag freezes the audio DSP, so a one-shot fired while it is set never sounds at all — which makes it
    /// indistinguishable from the game's own pause (where the music must stop but the menu's buttons must not), and
    /// would swallow the click of the button that unmutes. A volume cut keeps everything running at zero gain, so the
    /// two concerns stay separate.
    /// </remarks>
    public static class AudioMute
    {
        /// <summary>Unchanged from the pre-M22 toggle, so a player's existing setting carries over.</summary>
        public const string PrefsKey = "audio.muted";

        private const float MutedDecibels = -80f;

        private static AudioMixer _mixer;
        private static string _parameter;

        public static bool Muted { get; private set; }

        /// <summary>Tells the mute which mixer to cut. Null is legal and drops it to the global fallback.</summary>
        public static void SetMixer(AudioMixer mixer, string parameter)
        {
            _mixer = mixer;
            _parameter = parameter;
            Apply();
        }

        /// <summary>Restores the remembered setting. Called in Awake, before any screen binds, so a muted player is silent from frame one.</summary>
        public static void Load()
        {
            Muted = PlayerPrefs.GetInt(PrefsKey, 0) != 0;
            Apply();
        }

        public static void Set(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(PrefsKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        private static void Apply()
        {
            // SetFloat answers false when the parameter is not exposed, which is the fallback trigger: an un-exposed
            // parameter is a setup step not yet done, not a fault.
            if (_mixer != null && !string.IsNullOrEmpty(_parameter) &&
                _mixer.SetFloat(_parameter, Muted ? MutedDecibels : 0f))
            {
                AudioListener.volume = 1f;
                return;
            }

            AudioListener.volume = Muted ? 0f : 1f;
        }
    }
}
