using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The one object every UI Toolkit screen registers with. It owns the shared <see cref="PanelSettings"/>, insets
    /// each screen's root by the device safe area, and holds the active line's colour so no screen ever names one.
    /// </summary>
    /// <remarks>
    /// The work order (4.1, 4.2) describes the line colour as the USS variable <c>--line-current</c>, published by
    /// the shell. **That is not possible.** UI Toolkit can only *read* custom properties from C#
    /// (<c>CustomStyleProperty&lt;T&gt;</c> plus <c>customStyleResolved</c>); the setter, <c>SetCustomProperty</c>,
    /// is internal to the UIElements module and not public API on 6000.7.0a6 — verified by compiling against it.
    ///
    /// So the same contract is kept by a different mechanism: a screen tags an element with
    /// <see cref="LineBackgroundClass"/>, <see cref="LineTextClass"/> or <see cref="LineBorderClass"/> and the shell
    /// tints it. Screens still never name a line's colour, and the swap is still one call. `tokens.uss` keeps a
    /// <c>--line-current</c> fallback so a screen opened outside the shell still renders.
    /// </remarks>
    [DisallowMultipleComponent]
    public class UiShell : MonoBehaviour
    {
        /// <summary>Tint this element's background with the active line colour.</summary>
        public const string LineBackgroundClass = "line-bg";

        /// <summary>Tint this element's text with the active line colour.</summary>
        public const string LineTextClass = "line-fg";

        /// <summary>Tint this element's border with the active line colour.</summary>
        public const string LineBorderClass = "line-border";

        [Tooltip("Shared by every screen. Portrait 1080x1920, Match 0.5 — see 03.Data/Ui/PanelSettings.asset.")]
        [SerializeField] private PanelSettings panelSettings;

        [SerializeField] private StyleSheet tokens;
        [SerializeField] private StyleSheet components;

        /// <summary>Tag a label with one of these and the shell keeps it on the right face for the active locale.</summary>
        public const string SignageClass = "signage";
        public const string BodyClass = "body";
        public const string NumeralsClass = "numerals";

        private readonly System.Collections.Generic.List<VisualElement> _roots = new();
        private Rect _appliedSafeArea = new(-1f, -1f, -1f, -1f);
        private bool _facesApplied;

        public PanelSettings PanelSettings => panelSettings;

        /// <summary>The active line's colour. Screens read this only through the tint classes.</summary>
        public Color LineColour { get; private set; } = new Color32(0x9A, 0xCD, 0x32, 0xFF);

        public static UiShell Create(Transform parent, PanelSettings settings, StyleSheet tokenSheet, StyleSheet componentSheet = null)
        {
            var go = new GameObject("UI Shell");
            go.transform.SetParent(parent, false);
            var shell = go.AddComponent<UiShell>();
            shell.panelSettings = settings;
            shell.tokens = tokenSheet;
            shell.components = componentSheet;
            return shell;
        }

        private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            foreach (var root in _roots) ApplyFonts(root);
        }

        /// <summary>
        /// Puts every tagged label on the face the Fonts asset table gives for the active locale (3.4). Doing it here
        /// rather than in each screen is what keeps the en/es/fr builds free of Japanese glyphs: one lookup, one rule.
        /// </summary>
        public void ApplyFonts(VisualElement root)
        {
            if (root == null || LocalizationSettings.SelectedLocale == null) return;

            var applied = Apply(root, SignageClass, "font.signage");
            applied &= Apply(root, BodyClass, "font.body");
            applied &= Apply(root, NumeralsClass, "font.numerals");
            if (!applied) _facesApplied = false;
        }

        /// <summary>
        /// Retries the face lookup until the asset table can answer it.
        /// </summary>
        /// <remarks>
        /// The screens are built and registered before <c>com.unity.localization</c> has finished loading its asset
        /// tables, and <see cref="LocalizedAssetDatabase.GetLocalizedAsset{T}(string, string)"/> answers null rather
        /// than blocking while that is still in flight. A single pass at registration therefore leaves *every* label
        /// on UI Toolkit's fallback face — the whole game in the wrong type, in every locale, with nothing in the
        /// console to say so.
        ///
        /// Waiting on <c>LocalizationSettings.InitializationOperation</c> would be the direct fix and is not
        /// available: it hands back an Addressables <c>AsyncOperationHandle</c>, which drags
        /// <c>Unity.ResourceManager</c> into this assembly's references (see `CLAUDE.md`). So the shell simply asks
        /// again each frame until the answer comes, which costs three dictionary lookups for the handful of frames
        /// it takes, and nothing at all afterwards.
        /// </remarks>
        private void RetryFaces()
        {
            if (_facesApplied || LocalizationSettings.SelectedLocale == null) return;

            _facesApplied = true;
            foreach (var root in _roots) ApplyFonts(root);
        }

        private static bool Apply(VisualElement root, string className, string key)
        {
            var face = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>("Fonts", key);
            if (face == null) return false;
            var definition = new StyleFontDefinition(face);
            foreach (var element in root.Query(className: className).Build()) element.style.unityFontDefinition = definition;
            return true;
        }

        /// <summary>
        /// Adds a screen's root to the shell: token sheet, safe area and the current line tint. Safe to call again
        /// after the screen has built its tree — which a screen must do, because the tint pass can only find
        /// elements that already carry the marker classes.
        /// </summary>
        public void Register(VisualElement root)
        {
            if (root == null) return;
            if (!_roots.Contains(root)) _roots.Add(root);
            if (tokens != null && !root.styleSheets.Contains(tokens)) root.styleSheets.Add(tokens);
            if (components != null && !root.styleSheets.Contains(components)) root.styleSheets.Add(components);
            ApplySafeArea(root);
            ApplyLineColour(root);
            ApplyFonts(root);
        }

        public void Unregister(VisualElement root) => _roots.Remove(root);

        /// <summary>Sets the line colour and re-tints every registered screen. Call on each state change.</summary>
        public void SetLineColour(Color colour)
        {
            if (LineColour == colour) return;
            LineColour = colour;
            foreach (var root in _roots) ApplyLineColour(root);
        }

        private void Update()
        {
            RetryFaces();

            // The safe area changes on rotation and, on iOS, after the first frame. Re-inset only when it moves.
            if (Screen.safeArea == _appliedSafeArea) return;
            _appliedSafeArea = Screen.safeArea;
            foreach (var root in _roots) ApplySafeArea(root);
        }

        /// <summary>
        /// Insets the root by the device safe area, converted from screen pixels into panel space so it survives
        /// the reference-resolution scaling.
        /// </summary>
        public void ApplySafeArea(VisualElement root)
        {
            var panel = root?.panel;
            if (panel == null) return;

            var safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) return;

            // Screen space has y up from the bottom; panel space has y down from the top.
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));
            var full = panel.visualTree.layout;
            if (full.width <= 0f || full.height <= 0f) return;

            root.style.paddingLeft = Mathf.Max(0f, topLeft.x);
            root.style.paddingTop = Mathf.Max(0f, topLeft.y);
            root.style.paddingRight = Mathf.Max(0f, full.width - bottomRight.x);
            root.style.paddingBottom = Mathf.Max(0f, full.height - bottomRight.y);
        }

        /// <summary>Applies the active line colour to every element in <paramref name="root"/> carrying a tint class.</summary>
        public void ApplyLineColour(VisualElement root)
        {
            if (root == null) return;
            foreach (var element in root.Query(className: LineBackgroundClass).Build())
                element.style.backgroundColor = LineColour;
            foreach (var element in root.Query(className: LineTextClass).Build())
                element.style.color = LineColour;
            foreach (var element in root.Query(className: LineBorderClass).Build())
            {
                element.style.borderTopColor = LineColour;
                element.style.borderBottomColor = LineColour;
                element.style.borderLeftColor = LineColour;
                element.style.borderRightColor = LineColour;
            }
        }
    }
}
