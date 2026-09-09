using System;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Placeholder board for M2: shows the level's clues as text and offers two buttons, one that counts as a board tap
    /// and one that declares the level solved, so the whole screen flow can be exercised before the real board exists.
    /// </summary>
    public sealed class StubBoardView : MonoBehaviour, IBoardView
    {
        private Text _title;
        private Text _details;
        private Text _tapCount;
        private CanvasGroup _group;
        private int _taps;

        public event Action Interacted;
        public event Action Completed;

        public static StubBoardView Create(RectTransform area)
        {
            var go = new GameObject("Stub Board");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(area, false);
            UiBuilder.Stretch(rect);
            var view = go.AddComponent<StubBoardView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();

            var column = UiBuilder.Column(transform, "Content", 24, new RectOffset(48, 48, 32, 32), TextAnchor.MiddleCenter);
            UiBuilder.Stretch(column);

            _title = UiBuilder.Label(column, "Title", "", 56, UiBuilder.TextColor, TextAnchor.MiddleCenter, 80);
            _details = UiBuilder.Label(column, "Details", "", 34, UiBuilder.Muted, TextAnchor.MiddleCenter, 220);
            var note = UiBuilder.Label(column, "Note", "Stub board. The real board arrives in M3.", 28, UiBuilder.Muted, TextAnchor.MiddleCenter, 50);
            note.fontStyle = FontStyle.Italic;

            UiBuilder.Button(column, "Tap the board", () =>
            {
                _taps++;
                _tapCount.text = $"Taps: {_taps}";
                Interacted?.Invoke();
            }, AudioCue.Place, 140);
            _tapCount = UiBuilder.Label(column, "Taps", "Taps: 0", 30, UiBuilder.Muted, TextAnchor.MiddleCenter, 44);

            UiBuilder.Button(column, "Complete level (stub)", () => Completed?.Invoke(), AudioCue.UiConfirm, 110);
        }

        public void Load(LevelDefinition level)
        {
            _taps = 0;
            _tapCount.text = "Taps: 0";
            if (level == null)
            {
                _title.text = "No level";
                _details.text = "";
                return;
            }

            var data = level.ToLevelData();
            _title.text = string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;
            _details.text =
                $"{data.Width} x {data.Height}, {data.FixedPieces.Count} fixed pieces\n" +
                $"Columns: {string.Join(" ", data.ColumnClues)}\n" +
                $"Rows: {string.Join(" ", data.RowClues)}\n" +
                $"Entrance {data.Entrance}, exit {data.Exit}";
        }

        public void SetInteractable(bool interactable)
        {
            _group.interactable = interactable;
            _group.blocksRaycasts = interactable;
        }
    }
}
