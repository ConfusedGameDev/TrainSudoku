using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The dot-matrix announcement strip: amber text on near-black, scrolling right to left under a tiling grille.
    /// </summary>
    /// <remarks>
    /// <b>One argument</b> (D5). The mockup's signature device was a kana line stacked above a roman one, and
    /// <see cref="Announce"/> takes a single key because that pair no longer exists: a sign is one line, in the
    /// active locale.
    ///
    /// The grille is a tiling texture, not a shader. UI Toolkit takes no per-element custom shader at runtime (7.1),
    /// so this is the one place the design deliberately reaches for a bitmap.
    ///
    /// It runs on <b>unscaled</b> time, so the strip keeps moving while the game is paused (work order 9).
    /// </remarks>
    public class LedStrip : VisualElement
    {
        private const string Table = "UI";

        private readonly Label _text = new Label();
        private readonly VisualElement _grille = new VisualElement();
        private readonly Queue<(string Key, object[] Args)> _pending = new Queue<(string, object[])>();

        private IVisualElementScheduledItem _ticker;
        private float _offset;

        /// <summary>Set when a message is waiting for the strip to be laid out before it can be parked off-screen.</summary>
        private bool _awaitingLayout;
        private float _lastTime;

        /// <summary>Scroll speed in pixels per second at the 1080x1920 reference.</summary>
        public float Speed { get; set; } = 140f;

        /// <summary>The gap left after a message before the next one enters.</summary>
        public float Gap { get; set; } = 220f;

        public LedStrip()
        {
            AddToClassList("led-strip");
            style.backgroundColor = Palette.LedGround;
            style.overflow = Overflow.Hidden;

            _text.pickingMode = PickingMode.Ignore;
            _text.style.color = Palette.Led;
            _text.style.position = Position.Absolute;
            _text.style.unityTextAlign = TextAnchor.MiddleLeft;
            _text.style.whiteSpace = WhiteSpace.NoWrap;
            _text.style.letterSpacing = 4f;
            Add(_text);

            _grille.pickingMode = PickingMode.Ignore;
            _grille.style.position = Position.Absolute;
            _grille.style.left = 0;
            _grille.style.right = 0;
            _grille.style.top = 0;
            _grille.style.bottom = 0;
            Add(_grille);

            // A dot-matrix board is a grid of lamps, and without the grille this is just amber text on black. The
            // strip draws its own by default so no screen has to remember to hand it one; SetGrille still overrides.
            SetGrille(BuildGrille());

            RegisterCallback<AttachToPanelEvent>(_ => Start());
            RegisterCallback<DetachFromPanelEvent>(_ => Stop());
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>
        /// The lamp grid: a tiling cell that is transparent except for one dark row and one dark column, so tiled
        /// across the strip it darkens the seams between lamps and leaves the lamp faces alone.
        /// </summary>
        /// <remarks>
        /// It is generated rather than imported because it is four lines of code and a 64-byte texture, and 7.1
        /// rules out the shader that would otherwise do this. The cell is deliberately small: at 8 px on the
        /// 1080-wide reference the grid reads as texture at arm's length rather than as a visible lattice.
        /// </remarks>
        private static Texture2D BuildGrille()
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LedGrille",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            var seam = new Color32(0, 0, 0, 140);
            var lamp = new Color32(0, 0, 0, 0);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = x == 0 || y == 0 ? seam : lamp;

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>The tiling grille. Assigned by the shell or a screen; without it the strip is simply plain.</summary>
        public void SetGrille(Texture2D texture)
        {
            if (texture == null) return;
            _grille.style.backgroundImage = new StyleBackground(texture);
            _grille.style.backgroundRepeat = new StyleBackgroundRepeat(
                new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));
            _grille.style.backgroundSize = new StyleBackgroundSize(
                new BackgroundSize(new Length(texture.width, LengthUnit.Pixel), new Length(texture.height, LengthUnit.Pixel)));
        }

        /// <summary>
        /// Queues a message by localisation key, with anything the entry's placeholders need. The key is resolved in
        /// the active locale at the moment it is shown, so a locale change mid-queue announces the rest in the new
        /// language — which is also why the arguments are kept rather than the finished string.
        /// </summary>
        public void Announce(string localizationKey, params object[] args)
        {
            if (string.IsNullOrEmpty(localizationKey)) return;
            _pending.Enqueue((localizationKey, args));
            if (string.IsNullOrEmpty(_text.text)) ShowNext();
        }

        /// <summary>Drops anything queued and clears the strip.</summary>
        public void Clear()
        {
            _pending.Clear();
            _text.text = "";
        }

        private void ShowNext()
        {
            if (_pending.Count == 0) return;
            var (key, args) = _pending.Dequeue();
            _text.text = Resolve(key, args);
            Enter();
        }

        /// <summary>
        /// Parks the current message just off the right-hand edge, ready to scroll in.
        /// </summary>
        /// <remarks>
        /// <b>The width may not exist yet.</b> The first <c>Announce</c> arrives from a screen's <c>Refresh</c> on
        /// the frame the screens are built, which is before the panel has run a layout pass, and an unlaid-out
        /// element's <c>contentRect</c> is <see cref="float.NaN"/> -- not zero. Seeding the offset from it left
        /// every arithmetic step downstream NaN, and because every comparison against NaN is false, neither the
        /// geometry guard nor the wrap-around in <see cref="Tick"/> could ever recover: the strip sat still for the
        /// rest of the session. It only ever looked fine because changing locale re-announces after layout.
        ///
        /// So a message that arrives too early is held, and <see cref="OnGeometryChanged"/> starts it the moment
        /// the strip has a width.
        /// </remarks>
        private void Enter()
        {
            var width = contentRect.width;
            if (float.IsNaN(width) || width <= 0f)
            {
                _awaitingLayout = true;
                return;
            }

            _awaitingLayout = false;
            _offset = width;
            ApplyOffset();
        }

        private static string Resolve(string key, object[] args)
        {
            if (LocalizationSettings.SelectedLocale == null) return key;
            var value = args == null || args.Length == 0
                ? LocalizationSettings.StringDatabase.GetLocalizedString(Table, key)
                : LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            _text.style.fontSize = Mathf.Round(Mathf.Max(18f, evt.newRect.height * 0.44f));
            _text.style.top = 0;
            _text.style.height = evt.newRect.height;

            // Only a message still waiting for a width is started here. The old guard was "_offset <= 0", which
            // also fired on any resize that happened to land while a message was halfway across -- the safe-area
            // re-inset, or the locale font swap changing the strip's height -- and snapped it back to the right.
            if (_awaitingLayout) Enter();
        }

        private void Start()
        {
            _lastTime = Time.realtimeSinceStartup;
            _ticker?.Pause();
            _ticker = schedule.Execute(Tick).Every(16);
        }

        private void Stop() => _ticker?.Pause();

        private void Tick()
        {
            // Real time, not Time.deltaTime: the strip must keep running while the game is paused.
            var now = Time.realtimeSinceStartup;
            var delta = Mathf.Clamp(now - _lastTime, 0f, 0.1f);
            _lastTime = now;

            if (string.IsNullOrEmpty(_text.text)) return;

            // Belt and braces: if anything ever leaves the offset unusable, re-park rather than freeze.
            if (_awaitingLayout || float.IsNaN(_offset))
            {
                Enter();
                return;
            }

            _offset -= Speed * delta;
            var width = _text.resolvedStyle.width;
            if (_offset < -(width + Gap))
            {
                if (_pending.Count > 0) { ShowNext(); return; }
                Enter();                       // nothing queued: loop the current message
                return;
            }

            ApplyOffset();
        }

        private void ApplyOffset() => _text.style.left = Mathf.Round(_offset);
    }
}
