using System.Collections.Generic;
using TrainSudoku.XR.Rules;
using UnityEngine.Localization.Settings;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The wrist menu's language setting (XR-PRD 6.4, X24): one of the project's Localization locales, chosen on the
    /// headset and remembered in <see cref="XRPreferences"/>.
    /// </summary>
    /// <remarks>
    /// The shared Localization settings take the system's language at start and remember no choice, and changing them is
    /// a shared change (X25), so XR keeps its own choice and applies it once the board is placed. XR's copy follows it
    /// through the `XR` String Table (<see cref="XRText"/>); station and line names are untranslated proper nouns.
    /// </remarks>
    public static class XRLocale
    {
        /// <summary>The selected locale's code, or empty before Localization has chosen one.</summary>
        public static string CurrentCode =>
            LocalizationSettings.SelectedLocale != null ? LocalizationSettings.SelectedLocale.Identifier.Code : "";

        /// <summary>
        /// A language's name in its own words, for the settings row: the player looking for their language may not read
        /// the one on show. Not in the String Table, since a name must not change with the language it is listed in.
        /// The XR ja atlas bakes these, so its face can draw all four.
        /// </summary>
        public static string NativeName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "—";
            switch (code.Split('-')[0])
            {
                case "en": return "English";
                case "ja": return "日本語";
                case "es": return "Español";
                case "fr": return "Français";
                default: return code.ToUpperInvariant();
            }
        }

        /// <summary>Every name <see cref="NativeName"/> can return, for the atlas bake.</summary>
        public static readonly string[] NativeNames = { "English", "日本語", "Español", "Français", "—" };

        /// <summary>Selects the locale the player chose, if they chose one and it still exists.</summary>
        public static void Apply(XRPreferences preferences)
        {
            var code = preferences.LocaleCode;
            if (string.IsNullOrEmpty(code) || LocalizationSettings.AvailableLocales == null) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null && locale != LocalizationSettings.SelectedLocale) LocalizationSettings.SelectedLocale = locale;
        }

        /// <summary>Walks to the next available locale and remembers it.</summary>
        public static void Next(XRPreferences preferences)
        {
            var locales = LocalizationSettings.AvailableLocales != null ? LocalizationSettings.AvailableLocales.Locales : null;
            if (locales == null || locales.Count < 2) return;
            var codes = new List<string>(locales.Count);
            foreach (var locale in locales) codes.Add(locale.Identifier.Code);
            preferences.LocaleCode = XRPreferences.NextLocale(codes, CurrentCode);
            Apply(preferences);
        }
    }
}
