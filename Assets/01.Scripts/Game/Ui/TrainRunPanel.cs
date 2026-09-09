using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Overlay for the train animation (PRD section 8). Until M8 supplies the real run it simply waits a few seconds;
    /// a tap anywhere skips to the win screen either way.
    /// </summary>
    public sealed class TrainRunPanel : PanelBase
    {
        [SerializeField] private float stubDuration = 3f;

        private Text _message;
        private float _remaining;

        public override bool IsVisibleIn(GameState state) => state == GameState.TrainRun;

        protected override void Build()
        {
            // An almost invisible full-screen button catches the skip tap.
            var skipRect = UiBuilder.FullScreen(Root, "Skip", new Color(0f, 0f, 0f, 0.001f));
            var skip = skipRect.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() =>
            {
                AudioCuePlayer.Play(AudioCue.UiClick);
                Skip();
            });

            var column = UiBuilder.Column(Root, "Content", 12, new RectOffset(72, 72, 0, 140), TextAnchor.LowerCenter);
            UiBuilder.Stretch(column);
            column.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            UiBuilder.Spacer(column);
            _message = UiBuilder.Label(column, "Message", "", 48, UiBuilder.TextColor, TextAnchor.MiddleCenter, 70);
            UiBuilder.Label(column, "Hint", "Tap to skip", 34, UiBuilder.Muted, TextAnchor.MiddleCenter, 50);
        }

        public override void Refresh(GameState state)
        {
            _remaining = stubDuration;
            _message.text = "The train is running...";
            AudioCuePlayer.Play(AudioCue.TrainStart);
        }

        private void Update()
        {
            if (Flow == null || Flow.State != GameState.TrainRun) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) Flow.FinishTrainRun();
        }

        private void Skip()
        {
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }
    }
}
