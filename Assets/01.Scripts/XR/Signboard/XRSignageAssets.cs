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

        public PanelSettings PanelSettings => panelSettings;
        public FontAsset HeadingFont => headingFont;
        public FontAsset BodyFont => bodyFont;
        public Texture2D Mark => mark;
    }
}
