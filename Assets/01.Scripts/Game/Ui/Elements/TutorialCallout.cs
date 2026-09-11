using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The tutorial's speech bubble: one line of instruction on a paper card, with a tail pointing at the cell the
    /// player is being asked to tap. The visible half of <see cref="TrainSudoku.Core.TutorialCoach"/>.
    /// </summary>
    /// <remarks>
    /// <b>It replaced a band pinned above the LED strip.</b> A notice at the bottom of the screen talks about
    /// something in the middle of the board, so the eye has to travel between the two and a first-time player has to
    /// work out which cell the sentence means. Anchoring the words to the cell removes both problems at once.
    ///
    /// <b>Nothing here picks.</b> The card floats over the board and would otherwise swallow placements: a picking
    /// element makes <c>EventSystem.IsPointerOverGameObject</c> answer yes, and <c>BoardView.BeginPress</c> asks it
    /// first (work order 4.4). Every element in the tree is <see cref="PickingMode.Ignore"/>.
    ///
    /// It flips to the far side of the cell rather than running off screen, and the card and the tail move as one:
    /// <see cref="PointAt"/> takes the cell's centre and both of its edges already projected into panel space, so
    /// this class does no 3D maths — that belongs to the screen, which owns the camera.
    /// </remarks>
    public sealed class TutorialCallout : VisualElement
    {
        /// <summary>The cross-fade between one instruction and the next. Short: it is one line, not a screen.</summary>
        private const float FadeSeconds = 0.18f;

        /// <summary>The tail's footprint and how far it stands off the cell's edge.</summary>
        private const float TailWidth = 44f;
        private const float TailHeight = 26f;
        private const float TailGap = 14f;

        /// <summary>How close to the card's corner the tail may point before it stops following the cell.</summary>
        private const float TailMargin = 34f;

        /// <summary>Keeps the card off the edges of the camera strip.</summary>
        private const float ScreenMargin = 24f;

        private readonly VisualElement _card = new VisualElement();
        private readonly Label _text = new Label();
        private readonly Tail _tail = new Tail();

        private IVisualElementScheduledItem _fade;
        private string _key;
        private bool _allowed = true;
        private bool _placed;

        public TutorialCallout()
        {
            AddToClassList("callout");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.display = DisplayStyle.None;

            _card.AddToClassList("card");
            _card.AddToClassList("callout__card");
            _card.pickingMode = PickingMode.Ignore;
            Add(_card);

            _text.AddToClassList(UiShell.BodyClass);
            _text.AddToClassList("callout__text");
            _text.pickingMode = PickingMode.Ignore;
            _card.Add(_text);

            // The tail wears the line-colour class, so the shell tints it with everything else and the cell's ring
            // — which is the same colour on the board — reads as the other end of the same pointer.
            _tail.AddToClassList(UiShell.LineTextClass);
            _tail.pickingMode = PickingMode.Ignore;
            _tail.style.position = Position.Absolute;
            _tail.style.width = TailWidth;
            _tail.style.height = TailHeight;
            Add(_tail);
        }

        /// <summary>
        /// Shows the line behind <paramref name="key"/>, or takes the callout away when it is null. Repeating the
        /// key it is already showing does nothing, so the caller may push on every board change without restarting
        /// the fade.
        /// </summary>
        public void Show(string key)
        {
            if (key == _key) return;

            var wasShowing = IsShowing;
            _key = key;
            if (!string.IsNullOrEmpty(key)) _text.text = Signage.Text(key);
            Apply(wasShowing);
        }

        /// <summary>
        /// Whether the callout is allowed on screen at all. The Play screen stays up through the pause, the train
        /// run and the win, and an instruction is only true while the player can act on it.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (visible == _allowed) return;

            var wasShowing = IsShowing;
            _allowed = visible;
            Apply(wasShowing);
        }

        /// <summary>Re-resolves the current line. For a locale change, which leaves the key alone and the copy stale.</summary>
        public void Refresh()
        {
            if (!string.IsNullOrEmpty(_key)) _text.text = Signage.Text(_key);
        }

        /// <summary>
        /// Puts the card beside the cell. All three points are in the coordinates of this element's parent, already
        /// projected; <paramref name="host"/> is the area the card must stay inside — the camera strip.
        /// </summary>
        /// <param name="centre">The cell's centre, which the tail points at.</param>
        /// <param name="far">The mid-point of the cell's far edge, where a card above it sits.</param>
        /// <param name="near">The mid-point of the near edge, where a card below it sits.</param>
        public void PointAt(Vector2 centre, Vector2 far, Vector2 near, Rect host)
        {
            var size = _card.layout.size;
            if (size.x <= 1f || size.y <= 1f)
            {
                // The card has not been laid out yet. Park it where it will be wanted and try again next frame,
                // rather than flashing it at the origin.
                if (!_placed) style.visibility = Visibility.Hidden;
                return;
            }

            // Above the cell when the cell is in the lower half of the strip, below it otherwise: the card then
            // covers the part of the board the player is not being asked to look at.
            var above = centre.y > host.center.y;
            var anchor = above ? far : near;

            var top = above
                ? anchor.y - TailGap - TailHeight - size.y
                : anchor.y + TailGap + TailHeight;
            top = Mathf.Clamp(top, host.yMin + ScreenMargin, host.yMax - ScreenMargin - size.y);

            var left = Mathf.Clamp(centre.x - size.x / 2f,
                host.xMin + ScreenMargin, Mathf.Max(host.xMin + ScreenMargin, host.xMax - ScreenMargin - size.x));

            style.left = left;
            style.top = top;

            _tail.PointsDown = above;
            _tail.style.left = Mathf.Clamp(centre.x - left - TailWidth / 2f,
                TailMargin, Mathf.Max(TailMargin, size.x - TailMargin - TailWidth));
            _tail.style.top = above ? size.y - 1f : -TailHeight + 1f;

            _placed = true;
            style.visibility = Visibility.Visible;
        }

        private bool IsShowing => _allowed && !string.IsNullOrEmpty(_key);

        private void Apply(bool wasShowing)
        {
            _fade?.Pause();

            if (!IsShowing)
            {
                style.display = DisplayStyle.None;
                _placed = false;
                return;
            }

            style.display = DisplayStyle.Flex;
            if (!wasShowing)
            {
                _text.style.opacity = 1f;
                return;
            }

            _fade = Motion.Play(this, FadeSeconds, t => _text.style.opacity = Motion.EaseOut(t));
        }

        /// <summary>
        /// The triangle joining the card to the cell. Drawn rather than styled because USS has no way to make one,
        /// and it carries the card's own 4 px ink border along its two outer edges so the bubble reads as one shape.
        /// </summary>
        private sealed class Tail : VisualElement
        {
            private const float Border = 4f;

            private bool _pointsDown = true;

            public Tail()
            {
                generateVisualContent += Draw;
                RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            }

            /// <summary>True when the card is above the cell, so the tail hangs off its bottom edge.</summary>
            public bool PointsDown
            {
                get => _pointsDown;
                set
                {
                    if (_pointsDown == value) return;
                    _pointsDown = value;
                    MarkDirtyRepaint();
                }
            }

            private void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 2f || rect.height <= 2f) return;

                var painter = context.painter2D;
                var tip = new Vector2(rect.center.x, _pointsDown ? rect.yMax : rect.yMin);
                var baseY = _pointsDown ? rect.yMin : rect.yMax;
                var left = new Vector2(rect.xMin, baseY);
                var right = new Vector2(rect.xMax, baseY);

                // Filled first, in the card's white, and overlapping its edge by the border width so the seam
                // between the two is covered rather than drawn twice.
                painter.fillColor = Color.white;
                painter.BeginPath();
                painter.MoveTo(left + Nudge);
                painter.LineTo(tip);
                painter.LineTo(right + Nudge);
                painter.ClosePath();
                painter.Fill();

                painter.strokeColor = Palette.Ink;
                painter.lineWidth = Border;
                painter.lineJoin = LineJoin.Miter;
                painter.BeginPath();
                painter.MoveTo(left);
                painter.LineTo(tip);
                painter.LineTo(right);
                painter.Stroke();
            }

            private Vector2 Nudge => new Vector2(0f, _pointsDown ? -Border : Border);
        }
    }
}
