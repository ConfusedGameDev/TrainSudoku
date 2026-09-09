using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>One uGUI screen. The <see cref="GameManager"/> shows the panels whose <see cref="IsVisibleIn"/> matches the flow state.</summary>
    public abstract class PanelBase : MonoBehaviour
    {
        protected GameManager Game { get; private set; }
        protected GameFlow Flow => Game.Flow;
        protected RectTransform Root { get; private set; }

        public void Initialize(GameManager game)
        {
            Game = game;
            Root = (RectTransform)transform;
            Build();
            gameObject.SetActive(false);
        }

        /// <summary>Creates the panel's widgets once.</summary>
        protected abstract void Build();

        public abstract bool IsVisibleIn(GameState state);

        /// <summary>Called on every state change while the panel is visible, right after it is shown.</summary>
        public virtual void Refresh(GameState state)
        {
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
