using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    public sealed class MainMenuPanel : PanelBase
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        public override bool IsVisibleIn(GameState state) => state == GameState.MainMenu;

        protected override void BuildWidgets()
        {
            UiBuilder.FullScreen(Root, "Background", UiBuilder.Background);
            var column = UiBuilder.Column(Root, "Content", 32, new RectOffset(120, 120, 200, 200), TextAnchor.MiddleCenter);
            UiBuilder.Stretch(column);

            UiBuilder.Spacer(column);
            var title = UiBuilder.Label(column, "Title", "TrainSudoku", 120, UiBuilder.TextColor, TextAnchor.MiddleCenter, 160);
            title.fontStyle = FontStyle.Bold;
            UiBuilder.Label(column, "Subtitle", "Lay the track. Match the clues. Run the train.", 38, UiBuilder.Muted, TextAnchor.MiddleCenter, 60);
            UiBuilder.Spacer(column);

            playButton = UiBuilder.Button(column, "Play", 150);
            quitButton = UiBuilder.Button(column, "Quit", 110, false);
            UiBuilder.Spacer(column, 0.6f);
        }

        protected override void Wire()
        {
            UiBuilder.Wire(playButton, AudioCue.UiConfirm, () => Flow.ShowLevelSelect());
            UiBuilder.Wire(quitButton, AudioCue.UiBack, () => Game.Quit());
            // Quit only makes sense where the OS has no other way to close the game (PRD section 5).
            quitButton.gameObject.SetActive(IsDesktop());
        }

        private static bool IsDesktop()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return true;
                default:
                    return false;
            }
        }
    }
}
