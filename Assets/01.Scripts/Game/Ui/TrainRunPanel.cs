using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Overlay while the train runs (PRD section 8). The animation itself is driven by <see cref="TrainRunner"/>;
    /// this panel shows the caption and lets a tap anywhere skip to the win screen.
    /// </summary>
    public sealed class TrainRunPanel : PanelBase
    {
        [SerializeField] private Button skipButton;
        [SerializeField] private Text message;

        public override bool IsVisibleIn(GameState state) => state == GameState.TrainRun;

        protected override void BuildWidgets()
        {
            // An almost invisible full-screen button catches the skip tap.
            var skipRect = UiBuilder.FullScreen(Root, "Skip", new Color(0f, 0f, 0f, 0.001f));
            skipButton = skipRect.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;

            var column = UiBuilder.Column(Root, "Content", 12, new RectOffset(72, 72, 0, 140), TextAnchor.LowerCenter);
            UiBuilder.Stretch(column);
            column.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            UiBuilder.Spacer(column);
            message = UiBuilder.Label(column, "Message", "The train is running...", 48, UiBuilder.TextColor, TextAnchor.MiddleCenter, 70);
            UiBuilder.Label(column, "Hint", "Tap to skip", 34, UiBuilder.Muted, TextAnchor.MiddleCenter, 50);
        }

        protected override void Wire()
        {
            UiBuilder.Wire(skipButton, AudioCue.UiClick, Skip);
        }

        public override void Refresh(GameState state)
        {
            message.text = "The train is running...";
            AudioCuePlayer.Play(AudioCue.TrainStart);
        }

        private void Skip()
        {
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }
    }
}
