using UnityEngine;
using UnityEngine.UIElements;
using static TrainSudoku.XR.XRSignageUi;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The tutorial's line, on a small sign standing over the cell it is about (XR-PRD 7): a world-space UI Toolkit card
    /// in the signboard's language, turning to face the head like the clue signs.
    /// </summary>
    /// <remarks>
    /// It hangs off the board root, not the board, so the pause's dimming walks past it (the sign hides then anyway),
    /// and it is on Ignore Raycast so its panel collider is never in a hand's way on the way down to the cell. Its
    /// document stays on and shows or hides through the root's visibility: a document switched off loses its contents.
    /// </remarks>
    public sealed class XRTutorialCallout : MonoBehaviour
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 420f;
        private const float PixelsPerUnit = 100f;

        /// <summary>18 cm wide at the standard 6 cm cell; sized in cells from that, so it scales with the board.</summary>
        private const float WidthMetres = 0.18f;
        private const float StandardCell = 0.06f;

        /// <summary>How high over the cell's piece the card's bottom edge stands, in cells: above a hand coming down to it.</summary>
        private const float Lift = 2.2f;

        private XRSignageAssets _assets;
        private XRBoardDisplay _display;
        private UIDocument _document;
        private (int X, int Y)? _cell;

        /// <summary>A point in the board root's space to stand over instead of a cell: the board lesson's line, over the near edge.</summary>
        private Vector3? _point;
        private string _text;
        private bool? _visible;

        public static XRTutorialCallout Create(Transform boardRoot, XRBoardDisplay display, XRSignageAssets assets)
        {
            var go = new GameObject("Tutorial Callout") { layer = 2 };   // Ignore Raycast
            go.transform.SetParent(boardRoot, false);
            go.transform.localScale = Vector3.one * (WidthMetres / (PanelWidth / PixelsPerUnit) / StandardCell);
            var callout = go.AddComponent<XRTutorialCallout>();
            callout._assets = assets;
            callout._display = display;
            callout._document = go.AddComponent<UIDocument>();
            callout._document.panelSettings = assets != null ? assets.PanelSettings : null;
            callout._document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
            callout._document.worldSpaceSize = new Vector2(PanelWidth, PanelHeight);
            callout._document.pivot = Pivot.BottomCenter;
            return callout;
        }

        /// <summary>Shows <paramref name="text"/> over the cell; a null cell or empty text hides the sign.</summary>
        public void Show((int X, int Y)? cell, string text)
        {
            _point = null;
            _cell = string.IsNullOrEmpty(text) ? null : cell;
            if (_cell.HasValue) Say(text);
        }

        /// <summary>Shows <paramref name="text"/> over <paramref name="point"/>, in the board root's space: a line about the platform, not a cell.</summary>
        public void ShowAt(Vector3 point, string text)
        {
            _cell = null;
            _point = string.IsNullOrEmpty(text) ? (Vector3?)null : point;
            if (_point.HasValue) Say(text);
        }

        private void Say(string text)
        {
            if (text == _text) return;
            _text = text;
            Build(text);
        }

        public void Hide() => Show(null, null);

        private void Build(string text)
        {
            var root = _document.rootVisualElement;
            if (root == null) return;
            root.Clear();
            root.style.justifyContent = Justify.FlexEnd;
            var card = Card(root, 8f, 28f, 22f, 30f);
            card.style.flexGrow = 0;
            var label = XRSignageUi.Text(_assets, text, 46f, false, XRPalette.Ink);
            label.style.whiteSpace = WhiteSpace.Normal;
            card.Add(label);
        }

        private void LateUpdate()
        {
            var show = _point.HasValue || (_cell.HasValue && _display != null && _display.Level != null);
            SetVisible(show);
            if (!show) return;

            // Re-read every frame: the handle moves the board, and the text was built before the document attached.
            if (_document.rootVisualElement != null && _document.rootVisualElement.childCount == 0 && _text != null) Build(_text);

            var root = transform.parent;
            var foot = _point.HasValue ? root.TransformPoint(_point.Value) : _display.CellWorldPosition(_cell.Value.X, _cell.Value.Y);
            var at = foot + root.up * (Lift * root.lossyScale.y);
            transform.position = at;

            var head = Camera.main;
            if (head == null) return;
            var away = Vector3.ProjectOnPlane(at - head.transform.position, root.up);
            if (away.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(away, root.up);
        }

        private void SetVisible(bool show)
        {
            if (_visible == show) return;
            var root = _document.rootVisualElement;
            if (root == null) return;
            root.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            _visible = show;
        }
    }
}
