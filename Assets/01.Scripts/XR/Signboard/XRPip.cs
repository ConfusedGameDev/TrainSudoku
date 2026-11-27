using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Pip, the cat conductor, on an XR panel: a square element showing one body texture per mood, with the idle bob,
    /// the beckoning paw and the cheer's hops of the web edition (<c>ui/pip.ts</c>) and the restyle handoff's
    /// <c>PipMascot</c>. XR's own copy (X20); the art is under <c>02.Graphics/XR/Pip</c>.
    /// </summary>
    /// <remarks>
    /// She does not animate herself: whoever owns the panel calls <see cref="Tick"/> from its <c>LateUpdate</c> with
    /// unscaled time, as the signboard does for its stamp, so nothing depends on a world-space panel's scheduler and
    /// she keeps breathing through the pause. She never takes a press. With no art imported she draws nothing.
    /// </remarks>
    public sealed class XRPip : VisualElement
    {
        private const float BobPeriod = 2.4f;
        private const float BobHeight = 0.033f;
        private const float PawPeriod = 0.8f;
        private const float PawOut = -10f;
        private const float PawIn = 30f;
        private const float HopPeriod = 0.6f;
        private const int HopCount = 2;
        private const float HopLift = 0.10f;
        private const float HopSquash = 0.04f;

        private readonly XRSignageAssets _assets;
        private readonly float _size;
        private readonly VisualElement _body = new VisualElement();
        private readonly VisualElement _paw = new VisualElement();
        private float _moodStart = float.NaN;

        public XRPipMood Mood { get; private set; }

        public XRPip(XRSignageAssets assets, float size, XRPipMood mood = XRPipMood.Idle)
        {
            _assets = assets;
            _size = size;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            // Hops squash about her feet.
            style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f));

            Layer(_body);
            Layer(_paw);
            if (assets != null && assets.PipPaw != null)
            {
                _paw.style.backgroundImage = assets.PipPaw;
                _paw.style.transformOrigin = new TransformOrigin(
                    Length.Percent(assets.PipPawPivot.x * 100f), Length.Percent(assets.PipPawPivot.y * 100f));
            }

            Add(_body);
            Add(_paw);
            SetMood(mood);
        }

        private static void Layer(VisualElement layer)
        {
            layer.pickingMode = PickingMode.Ignore;
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0f;
            layer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            layer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            layer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            layer.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        /// <summary>Switches the body and restarts the mood's motion. The same mood again changes nothing, so a cheer is not cut short.</summary>
        public void SetMood(XRPipMood mood)
        {
            if (mood == Mood && !float.IsNaN(_moodStart)) return;
            Mood = mood;
            _moodStart = Time.unscaledTime;
            var body = _assets != null ? _assets.PipBody(mood) : null;
            _body.style.backgroundImage = body != null ? new StyleBackground(body) : new StyleBackground(StyleKeyword.None);
            _paw.style.display = mood == XRPipMood.Wave && _assets != null && _assets.PipPaw != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>One frame of her motion at <paramref name="time"/>, in unscaled seconds.</summary>
        public void Tick(float time)
        {
            var since = time - _moodStart;
            var lift = 0f;
            var wide = 1f;
            var tall = 1f;

            if (Mood == XRPipMood.Cheer && since < HopPeriod * HopCount)
            {
                var hop = Mathf.Sin(since % HopPeriod / HopPeriod * Mathf.PI);
                lift = -HopLift * _size * hop;
                wide = 1f + HopSquash * hop;
                tall = 1f - HopSquash * hop;
            }
            else if (Mood != XRPipMood.Sleep)
            {
                lift = -BobHeight * _size * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time / BobPeriod));
            }

            style.translate = new Translate(0f, lift);
            style.scale = new Scale(new Vector3(wide, tall, 1f));

            if (Mood != XRPipMood.Wave) return;
            var swing = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * since / PawPeriod);
            _paw.style.rotate = new Rotate(new Angle(Mathf.Lerp(PawOut, PawIn, swing), AngleUnit.Degree));
        }
    }
}
