using System;
using System.Collections.Generic;

namespace TrainSudoku.XR.Rules
{
    /// <summary>Where the headset's own settings are kept: PlayerPrefs on the device, a dictionary in the tests.</summary>
    public interface IPreferenceStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
    }

    public sealed class InMemoryPreferenceStore : IPreferenceStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public int GetInt(string key, int fallback) => _values.TryGetValue(key, out var value) && value is int number ? number : fallback;
        public void SetInt(string key, int value) => _values[key] = value;
        public string GetString(string key, string fallback) => _values.TryGetValue(key, out var value) && value is string text ? text : fallback;
        public void SetString(string key, string value) => _values[key] = value;
    }

    /// <summary>
    /// The wrist menu's settings that belong to the headset rather than to progress (XR-PRD 6.4): the dominant hand,
    /// music and effects volume, and the language. Kept beside the board's anchor, never in <c>save.json</c> (X1:
    /// nothing XR-only enters the shared save format). Re-place board and the height nudge act on the anchor itself,
    /// so they hold no value here. Read once and written through, so reading one every frame costs nothing.
    /// </summary>
    public sealed class XRPreferences
    {
        public const int VolumeSteps = 10;

        public const string DominantHandKey = "tsugi.xr.dominantHand";
        public const string MusicKey = "tsugi.xr.musicVolume";
        public const string EffectsKey = "tsugi.xr.effectsVolume";
        public const string LocaleKey = "tsugi.xr.locale";

        private readonly IPreferenceStore _store;
        private Hand _dominantHand;
        private int _music;
        private int _effects;
        private string _locale;

        public XRPreferences(IPreferenceStore store, Hand defaultHand = Hand.Right)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            var hand = store.GetInt(DominantHandKey, -1);
            _dominantHand = hand == (int)Hand.Left ? Hand.Left : hand == (int)Hand.Right ? Hand.Right : defaultHand;
            _music = Clamp(store.GetInt(MusicKey, VolumeSteps));
            _effects = Clamp(store.GetInt(EffectsKey, VolumeSteps));
            _locale = store.GetString(LocaleKey, "") ?? "";
        }

        /// <summary>The hand that grabs: the tray docks on its side (4.1).</summary>
        public Hand DominantHand
        {
            get => _dominantHand;
            set
            {
                _dominantHand = value;
                _store.SetInt(DominantHandKey, (int)value);
            }
        }

        /// <summary>The wrist the menu is worn on: the other one (6.4).</summary>
        public Hand WristHand => _dominantHand == Hand.Left ? Hand.Right : Hand.Left;

        /// <summary>Music volume, 0 to <see cref="VolumeSteps"/>.</summary>
        public int MusicVolume => _music;

        /// <summary>Effects volume, 0 to <see cref="VolumeSteps"/>.</summary>
        public int EffectsVolume => _effects;

        public double MusicLevel => (double)_music / VolumeSteps;
        public double EffectsLevel => (double)_effects / VolumeSteps;

        /// <summary>Turns the music up or down by <paramref name="delta"/> steps, within range, and returns the new volume.</summary>
        public int StepMusic(int delta) => _music = Step(MusicKey, _music, delta);

        /// <summary>Turns the effects up or down by <paramref name="delta"/> steps, within range, and returns the new volume.</summary>
        public int StepEffects(int delta) => _effects = Step(EffectsKey, _effects, delta);

        /// <summary>The chosen locale's code, or empty for the system's choice.</summary>
        public string LocaleCode
        {
            get => _locale;
            set
            {
                _locale = value ?? "";
                _store.SetString(LocaleKey, _locale);
            }
        }

        /// <summary>The locale after <paramref name="current"/> in <paramref name="codes"/>, wrapping round; the first when <paramref name="current"/> is not among them.</summary>
        public static string NextLocale(IReadOnlyList<string> codes, string current)
        {
            if (codes == null || codes.Count == 0) return current ?? "";
            for (var i = 0; i < codes.Count; i++)
                if (string.Equals(codes[i], current, StringComparison.OrdinalIgnoreCase))
                    return codes[(i + 1) % codes.Count];
            return codes[0];
        }

        private int Step(string key, int value, int delta)
        {
            var next = Clamp(value + delta);
            if (next != value) _store.SetInt(key, next);
            return next;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > VolumeSteps ? VolumeSteps : value;
    }
}
