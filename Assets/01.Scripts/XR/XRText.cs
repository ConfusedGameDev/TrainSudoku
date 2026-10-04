using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TrainSudoku.XR
{
    /// <summary>
    /// XR's copy (XR-PRD 9, X24): the `XR` String Table, beside the phone's `UI` table in the project's one Localization
    /// setup, in the same four locales. XR's own reader, since XR never calls the phone's <c>Signage</c> (X20).
    /// </summary>
    /// <remarks>
    /// Station and line names are untranslated proper nouns on the level assets, and TSUGI / NEXT STATION are the
    /// masthead's, so none of those are here. A missing row reads as its key, which is loud on purpose.
    /// </remarks>
    public static class XRText
    {
        public const string Table = "XR";

        private static bool _subscribed;
        private static Action _changed;

        /// <summary>The player picked another language: every XR panel rebuilds its copy and picks its fonts again.</summary>
        public static event Action Changed
        {
            add
            {
                Subscribe();
                _changed += value;
            }
            remove => _changed -= value;
        }

        /// <summary>The selected locale is Japanese, which draws with XR's own baked Noto face rather than Barlow.</summary>
        public static bool IsJapanese
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                return locale != null && locale.Identifier.Code.StartsWith("ja", StringComparison.Ordinal);
            }
        }

        public static string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (LocalizationSettings.SelectedLocale == null) return key;
            var text = args != null && args.Length > 0
                ? LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args)
                : LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);
            return string.IsNullOrEmpty(text) ? key : text;
        }

        private static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private static void OnLocaleChanged(Locale locale) => _changed?.Invoke();
    }
}
