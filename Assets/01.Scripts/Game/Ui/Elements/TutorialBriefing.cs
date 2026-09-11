using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The rules, in three cards, shown once before the tutorial station's first rail. The same chrome as the pause
    /// screen — an ink scrim with a centred <c>.card</c> — because it is the same kind of thing: the game stopped,
    /// saying something, waiting.
    /// </summary>
    /// <remarks>
    /// <b>Not a <see cref="UiScreen"/>.</b> Screens are chosen by <c>GameFlow</c>, which throws on a transition it
    /// does not know, so a briefing screen would mean a new state in Core for something only one level ever shows.
    /// It lives inside the Play screen instead, absolutely positioned over it.
    ///
    /// <b>It picks, deliberately.</b> Every other overlay on the Play screen is <see cref="PickingMode.Ignore"/> so
    /// taps reach the 3D board (work order 4.4); this one is the exception, because swallowing them is the whole
    /// point of a modal. Hiding it sets <c>display: none</c>, which stops it picking, so the board comes back.
    /// </remarks>
    public sealed class TutorialBriefing : VisualElement
    {
        /// <summary>The three cards, as String Table key pairs. Three is the most anyone will read before playing.</summary>
        private static readonly (string Title, string Body)[] Pages =
        {
            ("tutorial.brief1_title", "tutorial.brief1_body"),
            ("tutorial.brief2_title", "tutorial.brief2_body"),
            ("tutorial.brief3_title", "tutorial.brief3_body"),
        };

        private const float FadeSeconds = 0.2f;

        private readonly Label _title = Signage.SignageLabel("", 60);
        private readonly Label _body = Signage.BodyLabel("", 36);
        private readonly Label _count = Signage.Numerals("", 28);
        private readonly Button _next;
        private readonly VisualElement _card;

        private int _page;

        /// <summary>Raised when the last card is dismissed and the board is the player's.</summary>
        public event Action Finished;

        /// <summary>Whether the rules are on screen. While they are, nothing behind them may take a tap.</summary>
        public bool IsOpen => style.display == DisplayStyle.Flex;

        public TutorialBriefing()
        {
            AddToClassList("scrim");
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.justifyContent = Justify.Center;
            style.display = DisplayStyle.None;

            _card = Signage.Column();
            _card.AddToClassList("card");
            Signage.Inset(_card);
            _card.style.paddingTop = 40;
            _card.style.paddingBottom = 40;
            _card.style.paddingLeft = 40;
            _card.style.paddingRight = 40;
            Add(_card);

            _card.Add(_title);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            Signage.Margins(rule, 0f, 0f, 12f, 28f);
            _card.Add(rule);

            _body.style.whiteSpace = WhiteSpace.Normal;
            _card.Add(_body);

            var footer = Signage.Row();
            footer.style.alignItems = Align.Center;
            Signage.Margins(footer, 0f, 0f, 36f, 0f);
            _card.Add(footer);

            _count.style.color = Palette.InkDim;
            _count.style.unityTextAlign = TextAnchor.MiddleLeft;
            footer.Add(_count);
            footer.Add(Signage.Spacer());

            _next = Signage.Button("", AudioCue.UiConfirm, Advance, true);
            _next.style.minWidth = 320;
            _next.style.flexShrink = 0;
            footer.Add(_next);
        }

        /// <summary>Opens at the first card. Safe to call again; it always restarts from the beginning.</summary>
        public void Open()
        {
            _page = 0;
            style.display = DisplayStyle.Flex;
            ShowPage();
            Motion.Play(this, FadeSeconds, t => style.opacity = Motion.EaseOut(t));
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
            style.opacity = 1f;
        }

        /// <summary>Re-resolves the copy. For a locale change, which leaves the page alone and the words stale.</summary>
        public void Refresh()
        {
            if (style.display == DisplayStyle.None) return;
            ShowPage();
        }

        private void Advance()
        {
            if (_page + 1 < Pages.Length)
            {
                _page++;
                ShowPage();
                Motion.Play(_card, FadeSeconds, t => _body.style.opacity = Motion.EaseOut(t));
                return;
            }

            Close();
            Finished?.Invoke();
        }

        private void ShowPage()
        {
            var (title, body) = Pages[_page];
            _title.text = Signage.Text(title);
            _body.text = Signage.Text(body);
            _count.text = $"{_page + 1} / {Pages.Length}";
            _next.text = Signage.Text(_page + 1 < Pages.Length ? "tutorial.brief_next" : "tutorial.brief_start");
        }
    }
}
