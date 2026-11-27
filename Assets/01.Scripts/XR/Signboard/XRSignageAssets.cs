using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The signboard's hooks (XR-PRD 6.2, 8): the world-space panel it draws on, the two Barlow faces and the tsugi
    /// mark. The fonts and the mark are the phone's art, used in place (X24); the panel settings are XR's own.
    /// Window > TrainSudoku > XR > Add Flow to XR Scene creates and fills it.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/XR/Signage Assets", fileName = "XRSignageAssets")]
    public sealed class XRSignageAssets : ScriptableObject
    {
        [Tooltip("World Space render mode, colliders matched to the document's bounding box (poke needs the depth).")]
        [SerializeField] private PanelSettings panelSettings;

        [Tooltip("Signage headings: Barlow Condensed SemiBold, as on the phone.")]
        [SerializeField] private FontAsset headingFont;

        [Tooltip("Body copy and buttons: Barlow Medium.")]
        [SerializeField] private FontAsset bodyFont;

        [Tooltip("The app roundel with its corners cut to a circle: the masthead's mark.")]
        [SerializeField] private Texture2D mark;

        [Tooltip("XR's own Japanese face (X24): Noto Sans JP Medium baked from the XR String Table's ja rows plus the station " +
                 "and line names. Window > TrainSudoku > XR > Rebuild XR ja Font Atlas writes and assigns it.")]
        [SerializeField] private FontAsset japaneseFont;

        [Header("Pip, the cat conductor (02.Graphics/XR/Pip)")]
        [Tooltip("One square texture per mood, all framed alike. Window > TrainSudoku > XR > Import Pip fills these.")]
        [SerializeField] private Texture2D pipIdle;
        [SerializeField] private Texture2D pipCheer;
        [SerializeField] private Texture2D pipWorry;
        [SerializeField] private Texture2D pipSleep;

        [Tooltip("The wave pose without its paw: the paw is a separate layer so it can swing.")]
        [SerializeField] private Texture2D pipWaveBase;

        [Tooltip("The paw only, on the same canvas as the bodies, so it overlays 1:1.")]
        [SerializeField] private Texture2D pipPaw;

        [Tooltip("The paw's wrist as a fraction of the square canvas, from the top left. Change it only if the art changes.")]
        [SerializeField] private Vector2 pipPawPivot = new Vector2(0.1984f, 0.8095f);

        public PanelSettings PanelSettings => panelSettings;

        public Texture2D PipPaw => pipPaw;
        public Vector2 PipPawPivot => pipPawPivot;

        /// <summary>Pip's body for a mood; null until the art is imported, which draws no Pip at all.</summary>
        public Texture2D PipBody(Rules.XRPipMood mood)
        {
            switch (mood)
            {
                case Rules.XRPipMood.Wave: return pipWaveBase;
                case Rules.XRPipMood.Cheer: return pipCheer;
                case Rules.XRPipMood.Worry: return pipWorry;
                case Rules.XRPipMood.Sleep: return pipSleep;
                default: return pipIdle;
            }
        }

        public Texture2D Mark => mark;

        /// <summary>Headings in the selected language: Barlow Condensed, or XR's Noto face in Japanese.</summary>
        public FontAsset HeadingFont => XRText.IsJapanese && japaneseFont != null ? japaneseFont : headingFont;

        /// <summary>Body copy in the selected language: Barlow, or XR's Noto face in Japanese.</summary>
        public FontAsset BodyFont => XRText.IsJapanese && japaneseFont != null ? japaneseFont : bodyFont;

        /// <summary>
        /// XR's Noto face whatever the language, for the language button: it names each language in its own script,
        /// and the bake carries those names. Barlow when the face has not been baked yet.
        /// </summary>
        public FontAsset JapaneseFont => japaneseFont != null ? japaneseFont : headingFont;

        /// <summary>The Editor's setup writes the baked face here.</summary>
        public void SetJapaneseFont(FontAsset font) => japaneseFont = font;
    }
}
