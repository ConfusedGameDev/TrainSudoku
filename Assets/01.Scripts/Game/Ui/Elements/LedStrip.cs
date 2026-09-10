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
            Add(_text);

            _grille.pickingMode = PickingMode.Ignore;
            _grille.style.position = Position.Absolute;
            _grille.style.left = 0;
            _grille.style.right = 0;
            _grille.style.top = 0;
            _grille.style.bottom = 0;
            Add(_grille);

            RegisterCallback<AttachToPanelEvent>(_ => Start());
            RegisterCallback<DetachFromPanelEvent>(_ => Stop());
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
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
            _offset = contentRect.width;      // enter from the right-hand edge
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
            if (_offset <= 0f) _offset = evt.newRect.width;
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

            _offset -= Speed * delta;
            var width = _text.resolvedStyle.width;
            if (_offset < -(width + Gap))
            {
                if (_pending.Count > 0) { ShowNext(); return; }
                _offset = contentRect.width;   // nothing queued: loop the current message
            }

            ApplyOffset();
        }

        private void ApplyOffset() => _text.style.left = Mathf.Round(_offset);
    }
}
