using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The train run (work order 2.1). Also derived rather than designed: the LED strip alone over a full-screen
    /// transparent target that skips the animation. Nothing else is on screen, because the run is the thing to watch.
    /// </summary>
    public sealed class TrainRunScreen : UiScreen
    {
        private LedStrip _led;

        public override bool IsVisibleIn(GameState state) => state == GameState.TrainRun;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen--transparent");

            // A full-screen skip target. It must not paint, only catch the tap.
            var skip = new Button(Skip);
            skip.style.flexGrow = 1f;
            skip.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            skip.style.borderTopWidth = 0;
            skip.style.borderBottomWidth = 0;
            skip.style.borderLeftWidth = 0;
            skip.style.borderRightWidth = 0;
            skip.style.marginTop = 0;
            skip.style.marginBottom = 0;
            skip.style.marginLeft = 0;
            skip.style.marginRight = 0;
            root.Add(skip);

            _led = new LedStrip();
            _led.style.height = PlayScreen.BottomBarHeight;
            root.Add(_led);
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            _led.Clear();
            _led.Announce("trainrun.departing");
        }

        private void Skip()
        {
            AudioCuePlayer.Play(AudioCue.UiClick);
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }
    }
}
